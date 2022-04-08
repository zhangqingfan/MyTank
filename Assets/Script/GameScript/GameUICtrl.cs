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
    public Text KillInfo;
    public Text rebornTime;
    public PlayerControll myTankCtrl;
    public string  localPlayerName = "";
    public Slider[] sliders = new Slider[2];
    public Text[] scores = new Text[2];

    public RectTransform barrelPoint;
    public Text fireCDText;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        fireCDText = GameObject.Find("Canvas").transform.Find("FireCD").GetComponent<Text>();

        var players = GameObject.FindObjectsOfType<PlayerControll>();
        foreach (var p in players)
        {
            if (p.IsLocalPlayer == false) 
                continue;

            myTankCtrl = p;
            localPlayerName = myTankCtrl.headCanvas.transform.Find("Name").GetComponent<Text>().text;
            break;
        }
    }

    public void UpdateTeamSize()
    {
        foreach (var s in sliders)
            s.value = 0;

        var players = GameObject.FindObjectsOfType<PlayerControll>();
        foreach(var p in players)
            sliders[p.teamIndex.Value].value += 1;
    }

    public void UpdateAllHpColor()
    {
        if (myTankCtrl == null)
            return;

        var players = GameObject.FindObjectsOfType<PlayerControll>();
        foreach (var p in players)
            UpdateHpColor(p);
    }

    public void UpdateHpColor(PlayerControll player)
    {
        if (myTankCtrl == null)
            return;

        var hpColor = (player.teamIndex.Value == myTankCtrl.teamIndex.Value ? Color.green : Color.red);
        player.hpFrontImage.color = hpColor;
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

    public IEnumerator CountDownRebornTime(int rebornTime)
    {
        var delay = new WaitForSeconds(1);
        var countDown = rebornTime;
        
        this.rebornTime.gameObject.SetActive(true);
        
        while (countDown != 0)
        {
            this.rebornTime.text = countDown.ToString();
            yield return delay;
            countDown = countDown - 1;
            countDown = (countDown < 0 ? 0 : countDown);
        }
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
                GameManager.instance.SendMessageServerRpc(str);
                chatInput.text = "";

                //Canvas.ForceUpdateCanvases();      
                //scrollRect.verticalNormalizedPosition = 0f;  
                //Canvas.ForceUpdateCanvases();  
            }
        }
    }

    
}
