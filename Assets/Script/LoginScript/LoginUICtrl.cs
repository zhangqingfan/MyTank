using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LoginUICtrl : MonoBehaviour
{
    private Button HostButton;
    private Button ClientButton;
    private Button QuitButton;

    public static LoginUICtrl instance { get; private set; }

    private void Awake()
    {
        instance = this;
    }

    // Start is called before the first frame update
    void Start()
    {
        HostButton = GameObject.Find("Canvas").transform.Find("Host").GetComponent<Button>();
        HostButton.onClick.AddListener(() => ClickHostButton());

        ClientButton = GameObject.Find("Canvas").transform.Find("Client").GetComponent<Button>();
        ClientButton.onClick.AddListener(() => ClickClientButton());

        QuitButton = GameObject.Find("Canvas").transform.Find("Quit").GetComponent<Button>();
        QuitButton.onClick.AddListener(() => ClickQuitButton());
    }

    // Update is called once per frame
    void Update()
    { 
        
    }

    void ClickHostButton() 
    {
        GameManager.instance.StartAsHost();
        EnableAllButton(false);
    }

    void ClickClientButton()
    { 
        GameManager.instance.StartAsClient();
    }

    void ClickQuitButton()
    {
        Debug.Log("I am clicking quit now!");
    }

    public void ShowAllButton(bool bo)
    {
        HostButton.gameObject.SetActive(bo) ;
        ClientButton.gameObject.SetActive(bo);
        QuitButton.gameObject.SetActive(bo);
    }

    public void EnableAllButton(bool bo)
    {
        HostButton.enabled = bo;
        ClientButton.enabled = bo;
        QuitButton.enabled = bo;
    }

    private void OnDestroy()
    {
        
    }
}

