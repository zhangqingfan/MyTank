using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BulletCtrl : NetworkBehaviour
{
    public float speed = 100f;
    Rigidbody rigidBody;

    public override void OnNetworkSpawn()
    {
        Debug.Log("11111111111111111111111111111");
    }
     
    private void Start()
    {
        rigidBody = transform.GetComponent<Rigidbody>();
        rigidBody.velocity = transform.forward * 30f;
        //Destroy(this, 2.0f);
    }

    private void Update()
    {
        //Debug.DrawRay(transform.position, transform.forward, Color.red);
    }
}
