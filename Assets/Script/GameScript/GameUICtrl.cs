using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class GameUICtrl : MonoBehaviour
{
    public static GameUICtrl instance { get; private set; }
    public InputField chatInput;
    public Text chatText;
    public ScrollRect scrollRect;
    public PlayerControll myTankCtrl;
    public string  localPlayerName = "";
    public Slider[] sliders = new Slider[2];

    public RectTransform barrelPoint;
    public Text fireCDText;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        UpdateTeamSize();
        
        var players = GameObject.FindObjectsOfType<PlayerControll>();
        foreach (var p in players)
        {
            if (p.IsLocalPlayer == false) 
                continue;

            myTankCtrl = p;
            localPlayerName = myTankCtrl.headCanvas.transform.Find("Name").GetComponent<Text>().text;
            break;
        }

        fireCDText = GameObject.Find("Canvas").transform.Find("FireCD").GetComponent<Text>(); ;
    }

    public void UpdateTeamSize()
    {
        foreach (var s in sliders)
            s.value = 0;

        var players = GameObject.FindObjectsOfType<PlayerControll>();
        foreach(var p in players)
            sliders[p.teamIndex.Value].value += 1;
    }

    public void UpdateBarrelPoint(float angleX, float angleY)
    {
        barrelPoint.anchoredPosition = new Vector2(angleX * 15, angleY * 15);
    }

    public void UpdateFireCD(float timeOffset, float fireCD)
    {
        if (fireCDText == null)
            return;

        if (timeOffset >= fireCD)
        {
            fireCDText.color = Color.green; 
            fireCDText.text = string.Format("{0:F1}", fireCD);
            return;
        }

        fireCDText.color = Color.red;
        fireCDText.text = string.Format("{0:F1}", fireCD - (timeOffset));
    }

    // Update is called once per frame
    void Update()   
    {
        if (Input.GetKeyDown(KeyCode.Return) == true)
        {
            chatInput.ActivateInputField();
           
            if (chatInput.text != "")
            {
                string str = "<color=green>" + localPlayerName + ":" + chatInput.text + "</color>" + "\n";
                myTankCtrl.SendMessageServerRpc(str);
                chatInput.text = "";

                //Canvas.ForceUpdateCanvases();      
                //scrollRect.verticalNormalizedPosition = 0f;  
                //Canvas.ForceUpdateCanvases();  
            }
        }
    }

    
}
