using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TeamInfo
{
    public int index;
    public Vector3 spawnPos;
    public Dictionary<ulong, PlayerControll> memberDictionay = new Dictionary<ulong, PlayerControll>();
}

public class GameManager : MonoBehaviour
{
    public PlayerControll myTankCtrl;
    Dictionary<string, List<GameObject>> gameObjPool = new Dictionary<string, List<GameObject>>();
    TeamInfo[] teamArray = new TeamInfo[2];

    private void Start()
    {
        for (int i = 0; i < teamArray.Length; i++)
        {
            teamArray[i] = new TeamInfo();
            teamArray[i].index = i;
        }
    }

    public GameObject GetInstance(string path)
    {
        if(gameObjPool.ContainsKey(path) == false)
            gameObjPool.Add(path, new List<GameObject>());
        
        if(gameObjPool[path].Count == 0)
        {
            var a = Load<GameObject>(path);
            var b = GameObject.Instantiate(a);
            gameObjPool[path].Add(b);
        }

        var obj = gameObjPool[path].First();
        obj.SetActive(true);
        gameObjPool[path].RemoveAt(0);
        return obj;
    }

    public IEnumerator RealseObj(string path, GameObject obj, float time)
    {
        if (gameObjPool.ContainsKey(path) == false)
            yield break;

        yield return new WaitForSeconds(time);

        obj.SetActive(false);
        gameObjPool[path].Add(obj);
    }

    public void SpawnNewTank(ulong clientId)
    {
        int minNum = 999999;
        int addTeamIndex = -1;
        for (int i = 0; i < teamArray.Length; i++)
        {
            int memberNum = teamArray[i].memberDictionay.Count();
            if (memberNum <= minNum)
            {
                minNum = memberNum;
                addTeamIndex = i;
            }
        }

        var netObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
        if (netObj == null)
            return;

        var playerCtrl = netObj.GetComponent<PlayerControll>();
        if (playerCtrl == null)
            return;
         
        teamArray[addTeamIndex].memberDictionay[netObj.NetworkObjectId] = playerCtrl;
        playerCtrl.teamIndex.Value = addTeamIndex;

        playerCtrl.transform.position = teamArray[addTeamIndex].spawnPos;
        playerCtrl.pos.Value = teamArray[addTeamIndex].spawnPos;

        playerCtrl.MaxHp.Value = 100;
        playerCtrl.currentHp.Value = playerCtrl.MaxHp.Value;
    }

    public  T Load<T>(string path) where T : Object
    {
        return Resources.Load<T>(path);
    }

    public static GameManager instance { get; private set; }

    private void Awake()
    {
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartAsHost()
    {
        NetworkManager.Singleton.StartHost();
        NetworkManager.Singleton.SceneManager.LoadScene("Game", 0);
        NetworkManager.Singleton.SceneManager.OnLoadComplete += OnLoadScene;
        NetworkManager.Singleton.SceneManager.OnSynchronizeComplete += OnLoadScene11;
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += aaa;
    }

    void OnLoadScene(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
    {
        Debug.Log("333333333333333333333333333333333");

        if (NetworkManager.Singleton.IsServer == false)
            return;

        if (sceneName != "Game")
            return;

        for (int i = 0; i < teamArray.Length; i++)
            teamArray[i].spawnPos = GameObject.Find("Spawn" + i).transform.position;

        var players = GameObject.FindObjectsOfType<PlayerControll>();
        foreach (var p in players)
        {
            if (p.IsLocalPlayer == false)
                continue;
             
            myTankCtrl = p;
            //myTankCtrl.pos.Value = teamArray[myTankCtrl.teamIndex.Value].spawnPos;
        }
    }

    void OnLoadScene11(ulong clientId)
    {
        Debug.Log("11111111111111111111111111111");
    }

    void aaa(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        Debug.Log("222222222222222222222222222");
    }

    public void StartAsClient()
    {
        StartCoroutine(TryConnectToHost());
    }

    IEnumerator TryConnectToHost()
    {
        NetworkManager.Singleton.StartClient();
        
        LoginUICtrl.instance.EnableAllButton(false);
        yield return new WaitForSeconds(5f);
       
        if(NetworkManager.Singleton.IsConnectedClient == false)
        {
            LoginUICtrl.instance.EnableAllButton(true); 
            NetworkManager.Singleton.Shutdown();
        }
    }
}
