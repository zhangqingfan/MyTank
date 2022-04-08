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
    public Text headName;
    public Image hpFrontImage;
    public GameObject hpObj;
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
    public NetworkVariable<int> shieldLevel = new NetworkVariable<int>();

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

    public void Start()
    {
        maxSpeed = 8.0f;

        accelerateFactor = 0.04f;
        rotateSpeedLostFactor = 0.008f;
        slowdownFactor = 0.08f;

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

        teamIndex.OnValueChanged += TeamIndexChange;
        pos.OnValueChanged += PositionChange;
        currentHp.OnValueChanged += HpChange;

        //Debug.Log("PlayerControll start");
    }

    void PositionChange(Vector3 oldValue, Vector3 newValue)
    {
        //Debug.Log(oldValue + "  /  " + newValue);
        transform.position = newValue;
    }

    void HpChange(int oldValue, int newValue)
    {
        hpFrontImage.fillAmount = (float)currentHp.Value / (float)MaxHp.Value;   
    }

    void TeamIndexChange(int oldValue, int newValue)
    {
        if (GameUICtrl.instance == null || MiniMapCameraCtrl.instance == null)
            return;
        
        GameUICtrl.instance.UpdateTeamSize();
        GameUICtrl.instance.UpdateAllHpColor();
        MiniMapCameraCtrl.instance.UpdateAllMiniMapIcon();
    }

    override public void OnNetworkDespawn()
    {
        if(NetworkManager.Singleton.IsServer == true)
            GameManager.instance.RemoveTank(NetworkObjectId);
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
        hpObj = headCanvas.transform.Find("hp_green").gameObject;
        hpFrontImage = headCanvas.transform.Find("hp_green").Find("hp_front").GetComponent<Image>();
        headName = headCanvas.transform.Find("Name").GetComponent<Text>();

        //BUG, depends on the order: onNetworkSpawn / teamIndex.OnValueChanged
        //First initialize the existing tank in the scene, then initialize me.
        if (IsLocalPlayer == true)
        {
            headCanvas.transform.Find("Name").gameObject.SetActive(false);
            StartCoroutine(WaitSceneLoaded());
            return;
        }

        //MiniMapCameraCtrl.instance.UpdateMiniMapIcon(this);
        //GameUICtrl.instance.UpdateHpColor(this);
        //GameUICtrl.instance.UpdateTeamSize();
    }

    IEnumerator WaitSceneLoaded()
    {
        while (SceneManager.GetActiveScene().name != "Game")
            yield return null;

        RequestTeamIndexServerRpc();
        StartAllCoroutine();
    }

    public virtual void StartAllCoroutine()
    {
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
        var netObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(serverParams.Receive.SenderClientId);
        if (netObj == null)
        {
            Debug.Log("Can not find netobjId!!!");
            return;
        }

        Debug.Log("NetID:" + netObj.NetworkObjectId + " clientId:" + serverParams.Receive.SenderClientId);
        GameManager.instance.AddTankTeam(netObj);
    }

    private void FixedUpdate()
    {
        //var y = (isScopeMode == false ? 0.7f : 0.9f);
        //hpPrefab.transform.position = WorldPosToScreePos(Camera.main, turret.transform.position + new Vector3(0, y, 0));

        moveDir.x = (Input.GetAxisRaw("Vertical") != 0 ? Input.GetAxisRaw("Vertical") : 0);
        moveDir.y = (Input.GetAxisRaw("Horizontal") != 0 ? Input.GetAxisRaw("Horizontal") : 0);
    }

    Vector3 euler = Vector3.zero;
    protected void Update()
    {
        euler = headCanvas.transform.eulerAngles;
        euler.y = Camera.main.transform.eulerAngles.y;
        headCanvas.transform.eulerAngles = euler;

        if (IsLocalPlayer == false)
            return;

        float distance = Time.deltaTime * Mathf.Abs(currentSpeed);
        if (currentSpeed >= 0)
            rigidBody.MovePosition(rigidBody.position + transform.forward * distance);
        if (currentSpeed < 0)
            rigidBody.MovePosition(rigidBody.position - transform.forward * distance);

        if (Input.GetButton("Fire1") == true)
            TryShoot();

        //var dir = (bulletStartPos.transform.position - barrel.transform.position).normalized;
        //Debug.DrawRay(bulletStartPos.transform.position, dir, Color.red);
    }

    protected void TryShoot() //does it really need??
    {
        if(Time.time - recentFireTime < fireCD)
            return;

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
        //var bulletBase = GameManager.instance.ShowInstance("ModelPrefab/Bullet", bulletStartPos.transform.position, 0f);
        var a = Resources.Load<GameObject>("ModelPrefab/Bullet");
        var bullet = GameObject.Instantiate(a);

        var dir = (bulletStartPos.transform.position - barrel.transform.position).normalized;
        bullet.transform.rotation = Quaternion.LookRotation(dir);
        bullet.transform.position = bulletStartPos.transform.position;
        bullet.GetComponent<Rigidbody>().velocity = bullet.transform.forward * 50f;

        bullet.GetComponent<BulletCtrl>().teamIndex = teamIndex.Value;
        bullet.GetComponent<BulletCtrl>().ownerNetObjId = NetworkObjectId;
        bullet.GetComponent<BulletCtrl>().ownerClientid = OwnerClientId;
        bullet.GetComponent<BulletCtrl>().bornTime = Time.time;

        bullet.GetComponent<NetworkObject>().Spawn();
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
                hpObj.SetActive(!isScopeMode);
                yield return null;
                continue;
            }

            var wheel = Input.GetAxis("Mouse ScrollWheel");
            if (wheel > 0)
            {
                if(isScopeMode == false)
                {
                    isScopeMode = true;
                    hpObj.SetActive(false);
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
                    hpObj.SetActive(true);
                }    
            }

            yield return null;
        }
    }

    public IEnumerator PlayEffect(string[] pathArray,  float[] durationArray, Transform parent, Vector3 localPosition = default)
    {
        var length = (pathArray.Length < durationArray.Length ? pathArray.Length : durationArray.Length);
        
        for(int i = 0; i < length; i++)
        {
            //Debug.Log(parent.transform.TransformPoint(localPosition) + " / " + localPosition);
            var go = GameManager.instance.ShowInstance(pathArray[i], parent.transform.TransformPoint(localPosition), durationArray[i]);
            go.transform.SetParent(parent);
            yield return new WaitForSeconds(durationArray[i]);
        }
    }

    [ClientRpc]
    public void TakeDamageClientRpc(Vector3 localPos, Vector3 dir)
    {
        var box = transform.GetComponent<BoxCollider>();

        if(shieldLevel.Value > 0)
        {
            var go = GameManager.instance.ShowInstance("ModelPrefab/OnHitShield", box.transform.TransformPoint(localPos), 0.8f);
            go.transform.rotation = Quaternion.LookRotation(dir);
            go.transform.position -= dir * 0.3f;
            go.transform.SetParent(box.transform);
            return;
        }

        string[] pathArray = new string[] { "EffectPrefab/HitEffect0", "EffectPrefab/HitEffect1" };
        float[] durationArray = new float[] { 1.5f, 2.5f };
        StartCoroutine(PlayEffect(pathArray, durationArray, box.transform, localPos)); 
    }

    [ClientRpc]
    public void DeathClientRpc(ulong netObjId, int rebornTime)
    {
        GameManager.instance.ShowInstance("EffectPrefab/Explosion", transform.position, 2.5f);
        gameObject.SetActive(false);

        if (IsLocalPlayer == false)
            return;
           
        foreach(var net in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
        {
            if (net.NetworkObjectId != netObjId)
                continue;

            var killerCtrl = net.GetComponent<PlayerControll>();
            if (killerCtrl == null)
                break;

            CameraCtrl.instance.tankCtrl = killerCtrl;

            string info = "YOU ARE KILLED BY PLAYER " + killerCtrl.headName.text;
            GameUICtrl.instance.KillInfo.text = info;
            GameUICtrl.instance.KillInfo.gameObject.SetActive(true);
            
            break;
        }

        GameUICtrl.instance.StartCoroutine(GameUICtrl.instance.CountDownRebornTime(rebornTime)); 
    }

    [ClientRpc]
    public void RespawnClientRpc()
    {
        //todo...maybe some reborn effect

        gameObject.SetActive(true);

        if (IsLocalPlayer == false)
            return;

        GameUICtrl.instance.KillInfo.gameObject.SetActive(false);
        GameUICtrl.instance.rebornTime.gameObject.SetActive(false);
        CameraCtrl.instance.AttachLocalPlayer();
        StartAllCoroutine();
    }

    public bool IsRobot()
    {
        return (headName.text == "Robot" ? true : false);
    }
}
