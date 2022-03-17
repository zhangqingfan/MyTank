using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerControll : NetworkBehaviour
{
    //[HideInInspector]
    public Canvas headCanvas;
    public GameObject hpPrefab;
    public GameObject turret;
    public GameObject barrel;
    public GameObject barrelBlowbackPos;
    public GameObject barrelOriginalPos;
    public GameObject bulletStartPos;

    private Rigidbody rigidBody;
    
    public NetworkVariable<Vector3> pos = new NetworkVariable<Vector3>();
    public NetworkVariable<int> teamIndex = new NetworkVariable<int>();
    public NetworkVariable<int> currentHp = new NetworkVariable<int>();
    public NetworkVariable<int> MaxHp = new NetworkVariable<int>();

    public float accelerateFactor;
    public float slowdownFactor;
    public float roateTurretFactor;
    public float maxSpeed;

    public float rotateFactor;
    public float rotateSpeedLostFactor;
    public float rotateMaxSpeedRation;
    public float currentMaxSpeed;
    public float currentSpeed;
    Vector2 moveDir;
   
    public int scopeRatio;
    public bool isScopeMode = false;

    public float fireCD;
    public float recentFireTime;

    public float depressionAngle;
    public float elevationAngle;

    private void Start()
    {
        maxSpeed = 4.0f;

        accelerateFactor = 0.01f;
        rotateSpeedLostFactor = 0.008f;
        slowdownFactor = 0.02f;

        rotateMaxSpeedRation = 0.6f;
        roateTurretFactor = 20f;
        rotateFactor = 40f;

        scopeRatio = 1;
        isScopeMode = false;

        fireCD = 6f;
        recentFireTime = -999999f;
        moveDir = Vector2.zero;

        depressionAngle = -5.0f;
        elevationAngle = 30.0f;

        teamIndex.OnValueChanged += ValueChange;
        pos.OnValueChanged += PositionChange;
        currentHp.OnValueChanged += HpChange;

        //StartCoroutine(WaitSceneLoaded(0));
        //to Ryan
        //Debug.Log(MiniMapCameraCtrl.instance);   
        ///MiniMapCameraCtrl.instance.UpdateMiniMapIcon(this);
        //Useless
        //InitPlayerInfo();
    }

    void PositionChange(Vector3 oldValue, Vector3 newValue)
    {
        Debug.Log(oldValue + "  /  " + newValue);
        transform.position = newValue;
    }

    void HpChange(int oldValue, int newValue)
    {
        Debug.Log(oldValue + "  /  " + newValue);
    }
    
    void ValueChange(int oldValue, int newValue)
    {
        //Debug.Log(oldValue + "  /  " + newValue);
        GameUICtrl.instance.UpdateTeamSize();
    }

    private void OnEnable()
    {
        Debug.Log("OnEnable");
    }
    override public void OnNetworkSpawn()
    {
        turret = transform.Find("Main_Turre").gameObject;
        bulletStartPos = turret.transform.Find("BulletPos").gameObject;
        barrel = turret.transform.Find("Main_Barre").gameObject;
        barrelBlowbackPos = turret.transform.Find("BarrelBlowbackPos").gameObject;
        barrelOriginalPos = turret.transform.Find("BarrelOriginalPos").gameObject;
        rigidBody = transform.GetComponent<Rigidbody>();

        headCanvas = transform.Find("HeadCanvas").GetComponent<Canvas>();

        //BUG, depends on the order: onNetworkSpawn / teamIndex.OnValueChanged
        //First initialize the existing tank in the scene, then initialize me.
        if (MiniMapCameraCtrl.instance != null)
            MiniMapCameraCtrl.instance.UpdateMiniMapIcon(this);

        //BUG, How I know when scene is ready(as a client)?
        if (IsLocalPlayer == true)
        {
            headCanvas.transform.Find("Name").gameObject.SetActive(false);
            StartCoroutine(WaitSceneLoaded());
        }
            
        //Useless
        //InitPlayerInfo();
    }

    IEnumerator WaitSceneLoaded()
    {
        while (SceneManager.GetActiveScene().name != "Game")
            yield return null;

        RequestTeamIndexServerRpc();

        StartCoroutine(UpdateScopeRatio());
        StartCoroutine(UpdateCurrentSpeed());
        StartCoroutine(UpdateRotation());
        StartCoroutine(UpdateBarrelRotation());
        StartCoroutine(UpdateBarrelCrossHair());
        StartCoroutine(ShowFireCD());
    }


    [ServerRpc(Delivery = RpcDelivery.Reliable)]
    public void RequestTeamIndexServerRpc(ServerRpcParams serverParams = default)
    {
        GameManager.instance.SpawnNewTank(serverParams.Receive.SenderClientId);
    }

    public void InitPlayerInfo()
    {
        return;
        //TODO....!!!!
        if(CameraCtrl.instance != null)
        {
            var hpPath = (CameraCtrl.instance.tankCtrl.teamIndex.Value == teamIndex.Value ? "UIPrefab/hp_green" : "UIPrefab/hp_red");
            hpPrefab = GameManager.instance.GetInstance(hpPath);
        }
    }

    private void FixedUpdate()
    {
        if (hpPrefab != null)
        {
            var y = (isScopeMode == false ? 0.7f : 0.9f);
            hpPrefab.transform.position = WorldPosToScreePos(Camera.main, turret.transform.position + new Vector3(0, y, 0));
        }

        moveDir.x = (Input.GetAxisRaw("Vertical") != 0 ? Input.GetAxisRaw("Vertical") : 0);
        moveDir.y = (Input.GetAxisRaw("Horizontal") != 0 ? Input.GetAxisRaw("Horizontal") : 0);
    }

    Vector3 euler = Vector3.zero;
    void Update()
    {
        euler = Camera.main.transform.eulerAngles;
        euler.x = headCanvas.transform.eulerAngles.x;
        euler.y = Camera.main.transform.eulerAngles.y;
        euler.z = headCanvas.transform.eulerAngles.z;
        headCanvas.transform.eulerAngles = euler;

        if (IsLocalPlayer == false)
            return;

        float distance = Time.deltaTime * Mathf.Abs(currentSpeed);
        if (currentSpeed >= 0)
            rigidBody.MovePosition(rigidBody.position + transform.forward * distance);
        if (currentSpeed < 0)
            rigidBody.MovePosition(rigidBody.position - transform.forward * distance);

        if (Input.GetButton("Fire1") == true)
            TryShoot(turret.transform.rotation);
    }

    public void UpdateHp(PlayerControll player)
    {
        //var image = player.hpPrefab.transform.Find("hp_front").GetComponent<Image>();
        //image.fillAmount = (float)player.currentHp / (float)player.MaxHp;
    }

    void TryShoot(Quaternion shootDir) //does it really need??
    {
        if(Time.time - recentFireTime < fireCD)
        {
            //currentHp = currentHp - 1;
            //currentHp = (currentHp > 0 ? currentHp : 0);
            UpdateHp(this);
            return;
        }

        recentFireTime = Time.time;
        StartCoroutine(BlowbackBarrel());
        RequestShootServerRpc();
    }

    IEnumerator BlowbackBarrel()
    {
        var startTime = Time.time;
        while (barrel.transform.position != barrelBlowbackPos.transform.position)
        {
            barrel.transform.position = Vector3.Lerp(barrelOriginalPos.transform.position, barrelBlowbackPos.transform.position, (Time.time - startTime) / 0.1f);
            yield return null;
        }

        startTime = Time.time;
        while (barrel.transform.position != barrelOriginalPos.transform.position)
        {
            barrel.transform.position = Vector3.Lerp(barrelBlowbackPos.transform.position, barrelOriginalPos.transform.position, (Time.time - startTime) / 1.2f);
            yield return null;
        }
    }

    [ServerRpc(Delivery = RpcDelivery.Reliable)]
    public void RequestShootServerRpc()
    {
        var bullet = GameManager.instance.GetInstance("ModelPrefab/Bullet");
        bullet.transform.position = bulletStartPos.transform.position;
        //Debug.Log(bullet.transform.position);
        Vector3 euler = bulletStartPos.transform.rotation.eulerAngles;
        euler.x = 0;
        euler.z = 0;
        bullet.transform.eulerAngles = euler;
        bullet.GetComponent<NetworkObject>().Spawn();
    }

    [ClientRpc(Delivery = RpcDelivery.Reliable)]
    void SyncShootClientRpc(Quaternion shootDir,  Vector3 shootPos) 
    { 
    
    }

    Vector3 WorldPosToScreePos(Camera cam, Vector3 worldPos)
    {
        var screenPos = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        return new Vector3(screenPos.x, screenPos.y);
    }

    IEnumerator UpdateBarrelCrossHair()
    {
        var barrelDir = Vector3.zero;
        var CameraDir = Vector3.zero;

        while (true)
        {
            if (GameUICtrl.instance != null)
            {
                barrelDir.y = turret.transform.eulerAngles.y;
                barrelDir = (Quaternion.Euler(barrelDir) * Vector3.forward).normalized;

                CameraDir.y = Camera.main.transform.eulerAngles.y;
                CameraDir = (Quaternion.Euler(CameraDir) * Vector3.forward).normalized;

                var dot = Vector3.Dot(barrelDir, CameraDir);

                //refine float to avoid abs(dot) > 1.0f
                dot = Mathf.Clamp(dot, -0.999999999f, 0.999999999f);

                //Debug.Log(dot);
                var angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
                var side = (Vector3.Cross(barrelDir, CameraDir).y > 0 ? -1 : 1) ;

                GameUICtrl.instance.UpdateBarrelPoint(angle * side, 0);
            }
            yield return null;
        }
    }

    IEnumerator ShowFireCD() 
    {
        var delay = new WaitForSeconds(0.1f);  //Good way to avoid GC!
        while (true)
        {
            Debug.Log(GameUICtrl.instance);
            GameUICtrl.instance.UpdateFireCD(Time.time - recentFireTime, fireCD);
            yield return delay;
        }
    }

    IEnumerator UpdateBarrelRotation()
    {
        Quaternion rotateQuater = new Quaternion();

        while (true)
        {
            if(Input.GetMouseButton(1) == true)
            {
                yield return null;
                continue;
            }

            var rotateEuler = turret.transform.eulerAngles;
            rotateEuler.y = Camera.main.transform.eulerAngles.y;
            rotateQuater.eulerAngles = rotateEuler;
            turret.transform.rotation = Quaternion.RotateTowards(turret.transform.rotation, rotateQuater, Time.deltaTime * roateTurretFactor);
            
            //Camera.main.ScreenToWorldPoint(mousePosition);
            yield return null;
        }
    }

    IEnumerator UpdateCurrentSpeed()
    {
        var delay = new WaitForSeconds(0.1f); //Good way to avoid GC!

        while (true)
        {  
            //Debug.Log(currentSpeed);
            if (moveDir.x == 0)
            {
                var delta = Mathf.Abs(currentSpeed) - Mathf.Abs(slowdownFactor);
                delta = (delta > 0 ? Mathf.Abs(slowdownFactor) : Mathf.Abs(currentSpeed));
                currentSpeed = (currentSpeed > 0 ? currentSpeed - delta : currentSpeed + delta);
                yield return null;
                continue;
            }

            //same direction
            if (currentSpeed * moveDir.x >= 0)
                currentSpeed = currentSpeed + accelerateFactor * moveDir.x;

            //negative direction 
            if (currentSpeed * moveDir.x < 0)
            {
                currentSpeed = currentSpeed + (accelerateFactor + slowdownFactor) * moveDir.x;
                //Debug.Log(currentSpeed);
            }

            //turning around
            if (moveDir.y != 0)
            {
                //TODO..BUG
                var delta = accelerateFactor * 0.8f;
                currentSpeed = (currentSpeed > 0 ? currentSpeed - delta : currentSpeed + delta);

                if (Mathf.Abs(currentSpeed) > Mathf.Abs(currentMaxSpeed))
                {
                    var lostSpeed = accelerateFactor * 0.201f;
                    currentSpeed = (currentSpeed > 0 ? currentMaxSpeed - lostSpeed : currentSpeed + lostSpeed);
                    //Debug.Log(currentSpeed);
                }
            }
           
            if(Mathf.Abs(currentSpeed) > Mathf.Abs(maxSpeed))
            {
                currentSpeed = (currentSpeed > 0 ? maxSpeed : (-1 * maxSpeed));
                //Debug.Log(currentSpeed);
            }

            yield return null;
        }
    }

    IEnumerator UpdateRotation()
    {
        while (true)
        {
            if (moveDir.y == 0)
            {
                currentMaxSpeed = maxSpeed;
                yield return null;
                continue;
            }
            
            var temp = transform.eulerAngles;

            if(moveDir.x >= 0)
                temp.y = temp.y +  moveDir.y * rotateFactor * Time.deltaTime;
            if(moveDir.x < 0)
                temp.y = temp.y - moveDir.y * rotateFactor * Time.deltaTime;

            transform.eulerAngles = temp;
            currentMaxSpeed = maxSpeed * rotateMaxSpeedRation;
            yield return null;
        }  
    }

    IEnumerator UpdateScopeRatio()
    {
        while (true)
        {
            if (Input.GetKeyDown(KeyCode.LeftShift) == true)
            {
                isScopeMode = !isScopeMode;
                yield return null;
                continue;
            }

            var wheel = Input.GetAxis("Mouse ScrollWheel");
            if (wheel > 0)
            {
                if(isScopeMode == false)
                {
                    isScopeMode = true;
                    yield return null;
                    continue;
                }

                scopeRatio = scopeRatio * 2;
                scopeRatio = (scopeRatio < 4 ? scopeRatio : 4);
            }
             
            else if (wheel < 0)
            {
                scopeRatio = scopeRatio / 2;

                if(scopeRatio <= 1)
                {
                    scopeRatio = 1;
                    isScopeMode = false;
                }    
            }

            yield return null;
        }
    }

    [ServerRpc(Delivery = RpcDelivery.Reliable)]
    public void SendMessageServerRpc(string str)
    {
        //Debug.Log("SendMessageServerRpc :" + str);
        BroadcastMessageClientRpc(str);
    }

    [ClientRpc]
    void BroadcastMessageClientRpc(string str)
    {
        //Debug.Log("BroadcastMessageClientRpc :" + str);
        GameUICtrl.instance.chatText.text += str;
    }
}
