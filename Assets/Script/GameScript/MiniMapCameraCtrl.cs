using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MiniMapCameraCtrl : MonoBehaviour
{
    public PlayerControll myTankCtrl;
    public static MiniMapCameraCtrl instance { get; private set; }

    private void Awake()
    {
        instance = this;
    }

    void Start()
    {
        var players = GameObject.FindObjectsOfType<PlayerControll>();
        foreach (var p in players)
        {
            if (p.IsLocalPlayer == false)
                continue;
            
            myTankCtrl = p;
            break;
        }

        UpdateAllMiniMapIcon();
    }

    public void UpdateAllMiniMapIcon()
    {
        var players = GameObject.FindObjectsOfType<PlayerControll>();
        foreach (var p in players)
            UpdateMiniMapIcon(p);
    }

    public void UpdateMiniMapIcon(PlayerControll player)
    { 
        if (myTankCtrl == null)  
            return;

        var icon = player.transform.Find("miniMapIcon");
        string path = (player.teamIndex != myTankCtrl.teamIndex ? "Materials/PowerupHealth" : "Materials/PowerupShield");
        var mat = GameManager.instance.Load<Material>(path);
        icon.GetComponent<Renderer>().material = mat; 
    }

    // Update is called once per frame
    void Update()
    {
        var pos = myTankCtrl.transform.position;
        pos.y = transform.position.y;
        transform.position = pos;
    }
}
