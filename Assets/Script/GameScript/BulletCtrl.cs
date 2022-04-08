using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BulletCtrl : NetworkBehaviour
{
    public float speed = 100f;
    public int teamIndex;
    public ulong ownerNetObjId;
    public ulong ownerClientid;

    public Vector3 lastFramePos;
    public Vector3 tempPos;
    public float bornTime;

    private void Start()
    {
        speed = 160f;

        lastFramePos = transform.position;
        tempPos = transform.position;
        bornTime = Time.time;

        if(bornTime == Time.time)
        {
            var go0 = GameManager.instance.ShowInstance("EffectPrefab/Fire0", transform.position, 0.3f);
            go0.transform.rotation = transform.rotation;

            var go1 = GameManager.instance.ShowInstance("EffectPrefab/Fire1", transform.position, 1.0f);
            var euler = transform.eulerAngles;
            euler.x = 90;
            go1.transform.eulerAngles = euler;
        }
    }

    private void Update()
    {
        if (NetworkManager.IsServer == false)
            return;

        if (Time.time - bornTime > 5f)
            GetComponent<NetworkObject>().Despawn();

        if(tempPos != transform.position)
        {
            lastFramePos = tempPos;
            tempPos = transform.position;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (NetworkManager.IsServer == false)
            return;

        PlayerControll targetPlayer = other.GetComponent<PlayerControll>();
        if (targetPlayer == null)
            return;

        if (targetPlayer.NetworkObjectId == ownerNetObjId)
        {
            NoticeWrongHit("<color=red>You are hitting yourself!</color>\n", ownerClientid);
            GetComponent<NetworkObject>().Despawn();
            return;
        }

        if (targetPlayer.teamIndex.Value == teamIndex)
        {
            var netObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(ownerClientid);
            if (netObj != null && netObj.NetworkObjectId == ownerNetObjId)
                NoticeWrongHit("<color=red>You are hitting your teammate!</color>\n", ownerClientid);
                
            GetComponent<NetworkObject>().Despawn();
            return;
        }

        var hitPos = GetHitPos(lastFramePos, transform.position);
        var box = targetPlayer.transform.GetComponent<BoxCollider>();
        targetPlayer.TakeDamageClientRpc(box.transform.InverseTransformPoint(hitPos), (transform.position - lastFramePos).normalized);

        int bulletDamage = (targetPlayer.shieldLevel.Value > 0 ? 0 : 60);
        targetPlayer.shieldLevel.Value -= 1;
        targetPlayer.shieldLevel.Value = Mathf.Clamp(targetPlayer.shieldLevel.Value, 0, targetPlayer.shieldLevel.Value);

        targetPlayer.currentHp.Value -= bulletDamage;
        targetPlayer.currentHp.Value = (targetPlayer.currentHp.Value < 0 ? 0 : targetPlayer.currentHp.Value);

        if (targetPlayer.currentHp.Value == 0) 
        {
            targetPlayer.DeathClientRpc(ownerNetObjId, GameManager.instance.respawnTime);

            GameManager.instance.StartCoroutine(GameManager.instance.RespawnPlayer(targetPlayer));
            GameManager.instance.teamArray[teamIndex].score += 1;
            GameManager.instance.UpdateTeamScoreClientRpc(teamIndex, GameManager.instance.teamArray[teamIndex].score);
        }

        GetComponent<NetworkObject>().Despawn();
    }

    Vector3 GetHitPos(Vector3 outsidePos, Vector3 insidePos)
    {
        var dir = (insidePos - outsidePos).normalized;
        Ray ray = new Ray(outsidePos, dir);

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, Vector3.Distance(outsidePos, insidePos)) == true)
        {
            //Debug.Log(transform.position + " / " + hit.point);
            //hit.point += dir * 0.7f; 
            return hit.point;
        } 
        Debug.Log("No dect!!");
        return insidePos;
    }


    [ClientRpc]
    public void ShowBulletClientRpc(bool bo)
    {
        gameObject.SetActive(bo);
    }
  

    public void NoticeWrongHit(string str, ulong clientid)
    {
        var clientParam = new ClientRpcParams();
        clientParam.Send = new ClientRpcSendParams();
        clientParam.Send.TargetClientIds = new ulong[] { clientid };
        GameManager.instance.BroadcastMessageClientRpc(str, clientParam);
    }
}
