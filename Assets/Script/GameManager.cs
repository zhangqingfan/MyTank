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
    public int score = 0;
}

public class GameManager : NetworkBehaviour
{
    public int respawnTime = 10;
    public int reshowTime = 10;

    public PlayerControll myTankCtrl;
    public TeamInfo[] teamArray = new TeamInfo[2];

    List<GameObject> healthItemList = new List<GameObject>();
    List<GameObject> shieldItemList = new List<GameObject>();

    Dictionary<string, List<GameObject>> gameObjPool = new Dictionary<string, List<GameObject>>();

    private void Start()
    {
        for (int i = 0; i < teamArray.Length; i++)
        {
            teamArray[i] = new TeamInfo();
            teamArray[i].index = i;
        }
    }

    public GameObject ShowInstance(string path, Vector3 pos, float duration)
    {
        var go = GetInstance(path, pos);
        StartCoroutine(RealseObj(path, go, duration));
        return go;
    }

    public GameObject GetInstance(string path, Vector3 pos)
    {
        if (gameObjPool.ContainsKey(path) == false)
            gameObjPool.Add(path, new List<GameObject>());

        if (gameObjPool[path].Count == 0)
        {
            //Debug.Log("really!");
            var a = Load<GameObject>(path);
            var b = GameObject.Instantiate(a);
            gameObjPool[path].Add(b);
        }

        var obj = gameObjPool[path].First();
        obj.transform.position = pos;
        obj.SetActive(true);
        gameObjPool[path].RemoveAt(0);

        return obj;
    }

    public IEnumerator RealseObj(string path, GameObject obj, float time = 0f)
    {
        if (gameObjPool.ContainsKey(path) == false)
            yield break;

        yield return new WaitForSeconds(time);

        obj.SetActive(false);
        gameObjPool[path].Add(obj);
    }

    public IEnumerator RespawnPlayer(PlayerControll player)
    {
        if (player.currentHp.Value > 0)
            yield break;

        yield return new WaitForSeconds(respawnTime);

        player.pos.Value = teamArray[player.teamIndex.Value].spawnPos;
        player.currentHp.Value = player.MaxHp.Value;
        player.RespawnClientRpc();

        if (player.IsRobot() == true)
            player.StartAllCoroutine();
    }

    public IEnumerator ReShowItem(ItemCtrl item)
    {
        yield return new WaitForSeconds(reshowTime);

        item.gameObject.SetActive(true);
        item.ShowItemClientRpc(true);
    }

    public void RemoveTank(ulong netObjId)
    {
        //The tank might not have teamIndex now. So iterator all team to delete.
        for (int i = 0; i < teamArray.Length; i++)
            teamArray[i].memberDictionay.Remove(netObjId);
    }

    public void AddTankTeam(NetworkObject netObj)
    {
        int minNum = 999999;
        int addTeamIndex = -1;
        for (int i = 0; i < teamArray.Length; i++)
        {
            int memberNum = teamArray[i].memberDictionay.Count();
            if (memberNum < minNum)
            {
                minNum = memberNum;
                addTeamIndex = i;
            }
        }

        //for test
        //addTeamIndex = 0;

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

    public T Load<T>(string path) where T : Object
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

        //this event is only for Host mode.
        NetworkManager.Singleton.SceneManager.LoadScene("Game", 0);
        NetworkManager.Singleton.SceneManager.OnLoadComplete += OnLoadScene;
    }

    //will be entered into many times!
    void OnLoadScene(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
    {
        if (NetworkManager.Singleton.IsServer == false)
            return;

        if (sceneName != "Game")
            return;

        if (myTankCtrl != null)
            return;

        for (int i = 0; i < teamArray.Length; i++)
            teamArray[i].spawnPos = GameObject.Find("Spawn" + i).transform.position;

        var players = GameObject.FindObjectsOfType<PlayerControll>();
        foreach (var p in players)
        {
            if (p.IsLocalPlayer == false)
                continue;

            myTankCtrl = p;
        }

        var shield = GameObject.Find("AllShieldPos");
        foreach (Transform child in shield.transform)
        {
            var go = GetInstance("ModelPrefab/Shield", child.position);
            //go.transform.position = child.position;
            go.GetComponent<NetworkObject>().Spawn();

            shieldItemList.Add(go);
        }

        var health = GameObject.Find("AllHealthPos");
        foreach (Transform child in health.transform)
        {
            var go = GetInstance("ModelPrefab/Health", child.position);
            //go.transform.position = ;
            go.GetComponent<NetworkObject>().Spawn();

            healthItemList.Add(go);
        }
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

        if (NetworkManager.Singleton.IsConnectedClient == false)
        {
            LoginUICtrl.instance.EnableAllButton(true);
            NetworkManager.Singleton.Shutdown();
        }
    }

    [ClientRpc]
    public void UpdateTeamScoreClientRpc(int teamIndex, int score)
    {
        GameUICtrl.instance.scores[teamIndex].text = score.ToString();
    }

    [ServerRpc(Delivery = RpcDelivery.Reliable)]
    public void SendMessageServerRpc(string str)
    {
        var index = str.IndexOf(":Robot");
        if (index > 0)
        {
            int num = -1;
            int.TryParse(str.Substring(index + 7, 1), out num);
            StartCoroutine(CreateRobot(num));
            return;
        }

        index = str.IndexOf("Remove");
        if (index > 0)
        {
            RemoveAllRobot();
            return;
        }

        BroadcastMessageClientRpc(str);
    }

    [ClientRpc]
    public void BroadcastMessageClientRpc(string str, ClientRpcParams clientRpcParams = default)
    {
        //Debug.Log("BroadcastMessageClientRpc :" + str);
        GameUICtrl.instance.chatText.text += str;
    }

    public IEnumerator CreateRobot(int num)
    {
        int totalNum = teamArray[0].memberDictionay.Count + teamArray[1].memberDictionay.Count;
        if (totalNum >= 10)
        {
            BroadcastMessageClientRpc("The number of players in the room is full!");
            yield break;
        }

        for (int i = 0; i < num; i++)
        {
            var a = Resources.Load<GameObject>("ModelPrefab/RobotTank");
            var tank = GameObject.Instantiate(a);
            tank.GetComponent<NetworkObject>().Spawn();
            
            yield return new WaitForSeconds(0.1f);

            AddTankTeam(tank.GetComponent<NetworkObject>());
            tank.GetComponent<RobotControll>().targetPos = tank.GetComponent<RobotControll>().transform.position;
            tank.GetComponent<RobotControll>().lastTargetPos = tank.GetComponent<RobotControll>().transform.position;
            tank.GetComponent<RobotControll>().agent.enabled = true;
            tank.GetComponent<PlayerControll>().StartAllCoroutine();

            yield return new WaitForSeconds(1f);
        }
    }

    public void RemoveAllRobot()
    {
        List<PlayerControll> deleteList = new List<PlayerControll>();
        for (int i = 0; i < 2; i++)
        {
            foreach(var p in teamArray[i].memberDictionay)
            {
                if(p.Value.headName.text == "Robot")                    
                    deleteList.Add(p.Value);
            }
        }

        foreach (var player in deleteList)
        {
            var netObj = player.GetComponent<NetworkObject>();
            RemoveTank(netObj.NetworkObjectId);
            netObj.Despawn();
        }
    }
}
