using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ItemCtrl : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        PlayerControll targetPlayer = other.GetComponent<PlayerControll>();
        if (targetPlayer == null)
            return;

        if (name == "Health(Clone)")
        {
            string[] pathArray = new string[] { "EffectPrefab/HealthEffect", "EffectPrefab/ItemEffect" };
            float[] durationArray = new float[] { 0.7f, 1.0f };
            targetPlayer.StartCoroutine(targetPlayer.PlayEffect(pathArray, durationArray, targetPlayer.transform));   
        }
           
        if (name == "Shield(Clone)")
        {
            string[] pathArray = new string[] { "EffectPrefab/ShieldEffect", "EffectPrefab/ItemEffect" };
            float[] durationArray = new float[] { 0.7f, 1.0f };
            targetPlayer.StartCoroutine(targetPlayer.PlayEffect(pathArray, durationArray, targetPlayer.transform));  
        }
    
        ////////////////////////////////////////////////////////////////
        
        if (NetworkManager.IsServer == false)
            return;

        if (name == "Shield(Clone)")
            ShieldEffect(targetPlayer);

        if (name == "Health(Clone)")
            HealthEffect(targetPlayer);

        gameObject.SetActive(false);
        ShowItemClientRpc(false);

        GameManager.instance.StartCoroutine(GameManager.instance.ReShowItem(this));
    }

    [ClientRpc]
    public void ShowItemClientRpc(bool bo)
    {
        gameObject.SetActive(bo);
    }

    void ShieldEffect(PlayerControll player)
    {
        if (NetworkManager.IsServer == false)
            return;
    }

    void HealthEffect(PlayerControll player)
    {
        if (NetworkManager.IsServer == false)
            return;

        player.currentHp.Value = player.MaxHp.Value;
    }
}