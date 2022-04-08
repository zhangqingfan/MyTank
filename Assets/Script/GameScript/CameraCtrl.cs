using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraCtrl : MonoBehaviour
{
    public PlayerControll tankCtrl;
    public float distance;
    public float height;
    public float scopeHeight;
    private float initFOV;

    public static CameraCtrl instance { get; private set; }

    private void Awake()
    {
        instance = this;
    }
    private void Start()
    {
        initFOV = Camera.main.fieldOfView;
        AttachLocalPlayer();
    }
    public void AttachLocalPlayer()
    {
        var players = GameObject.FindObjectsOfType<PlayerControll>();
        foreach (var p in players)
        {
            if (p.IsLocalPlayer == false)
                continue;

            tankCtrl = p;
            break;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (tankCtrl == null)
            return;

        if(tankCtrl.isScopeMode == false)
            FollowTarget(tankCtrl.turret);

        if (tankCtrl.isScopeMode == true)
            OpenScope(tankCtrl.turret);
    }

    void FollowTarget(GameObject turret)
    {
        Camera.main.fieldOfView = initFOV;

        float h = Input.GetAxis("Mouse X") * 4;   
        float v = Input.GetAxis("Mouse Y") * 4;
        transform.RotateAround(turret.transform.position, Vector3.up, h);
        //transform.RotateAround(tankCtrl.transform.position, tankCtrl.transform.right, -v);

        //transform.RotateAround(turret.transform.position, Vector3.up, h);
        //transform.RotateAround(tankCtrl.transform.position, Vector3.right, -v);


        //var rotation = Quaternion.Euler(0, tank.transform.eulerAngles.y, 0);
        var pos = turret.transform.position - transform.forward * distance; 
        pos.y = turret.transform.position.y + height;
        transform.position = pos;

        //Debug.Log(turret.transform.position + " / " + transform.position);
        Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = true;
    }

    void OpenScope(GameObject turret)
    {
        Camera.main.fieldOfView = initFOV / tankCtrl.scopeRatio;

        float h = Input.GetAxis("Mouse X") * 2;
        transform.RotateAround(turret.transform.position, turret.transform.forward, h);

        var pos = turret.transform.position - transform.forward * 0.4f;
        pos.y = pos.y + scopeHeight;
        transform.position = pos;
    }
}
