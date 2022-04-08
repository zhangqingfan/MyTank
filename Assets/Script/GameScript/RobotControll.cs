using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.AI;

public class RobotControll :  PlayerControll
{
    ulong enemyNetworkID;
    const ulong invalidID = 999999;
    float maxMovingTime = 0;
    float detectRange;
    float moveRange;
    public Vector3 targetPos;
    public Vector3 lastTargetPos;

    public NavMeshAgent agent;

    new void Start()
    {
        base.Start();
        detectRange = 60.0f;
        moveRange = 40.0f;
        enemyNetworkID = invalidID;

        agent = GetComponent<NavMeshAgent>();
        agent.speed = maxSpeed * 0.7f;
        agent.stoppingDistance = 0.3f;
        agent.angularSpeed = 40f;
        agent.acceleration = 600f;
    }

    public override void StartAllCoroutine()
    {
        StartCoroutine(DetectEnemy());
        StartCoroutine(BarrelTowardsEnemy());
        StartCoroutine(HullTowardsTargetPos());
        StartCoroutine(MoveToRandomPos());
        StartCoroutine(TryShooting()); 
    }

    private void FixedUpdate()
    {
        if (currentHp.Value <= 0)
            return;

        if (lastTargetPos != targetPos)
        {
            bool bo = agent.SetDestination(targetPos);
            maxMovingTime = 0f;
            lastTargetPos = targetPos;
        }
    }

    public IEnumerator MoveToRandomPos()
    {
        float invertalTime = 1.0f;
        var delay = new WaitForSeconds(invertalTime);

        while (true) 
        { 
            yield return delay;

            if (enemyNetworkID != invalidID && agent.hasPath == true)
                continue;

            if (agent.hasPath == true)
            {
                maxMovingTime += invertalTime;
                var distance = Mathf.Abs(transform.position.x - targetPos.x) + Mathf.Abs(transform.position.z - targetPos.z);

                if (maxMovingTime <= 6.0f && distance >= agent.stoppingDistance)
                    continue;
            }

            agent.stoppingDistance = 0.5f;
            targetPos = transform.position;
            
            for(int i = 0; i < 150; i++)
            {
                var randomSpherePos = Random.insideUnitSphere * moveRange;
                var randomPos = Vector3.zero;
                randomPos.x = transform.position.x + randomSpherePos.x;
                randomPos.z = transform.position.z + randomSpherePos.z;
                //Debug.Log(randomSpherePos);

                NavMeshHit hit;
                if(NavMesh.SamplePosition(randomPos, out hit, 1f, 1 << NavMesh.GetAreaFromName("Walkable")) == true)
                {
                    targetPos = hit.position;
                    break;
                } 
            } 
        }
    }

    public IEnumerator DetectEnemy()
    {
        var delay = new WaitForSeconds(1.0f);
        List<PlayerControll> enemyList = new List<PlayerControll>();

        while (true)
        {
            yield return delay;

            enemyList.Clear(); 
            var cols = Physics.OverlapSphere(transform.position, detectRange, LayerMask.GetMask("Tank"));
            for(int i = 0; i < cols.Length;  i++)
            {
                var player = cols[i].gameObject.GetComponent<PlayerControll>();
                if (player == null || player.currentHp.Value == 0)
                    continue;

                if (player.teamIndex.Value != teamIndex.Value)
                    enemyList.Add(player);
            }
            
            if(enemyList.Count == 0)
            {
                enemyNetworkID = invalidID;
                agent.stoppingDistance = 0.5f;
                continue;
            }

            float minDistance = 999999;
            foreach(var p in enemyList)
            {
                var distance = Vector3.Distance(transform.position, p.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    enemyNetworkID = p.NetworkObjectId;
                    
                    targetPos = p.transform.position;
                    agent.stoppingDistance = 15f;
                }
            }
        }
    }

    public IEnumerator BarrelTowardsEnemy()
    {
        Quaternion rotateQuater = new Quaternion();
        Vector3 euler = Vector3.zero;

        while (true)
        {
            yield return null;

            NetworkObject enemy;
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(enemyNetworkID, out enemy) == false)
                continue;

            var player = enemy.GetComponent<PlayerControll>();
            if (player == null || player.currentHp.Value == 0)
                continue;

            //model problem. turrent default euler.x = -90!
            var dir = Vector3.Normalize(player.transform.position - turret.transform.position);
            rotateQuater = Quaternion.LookRotation(dir);
            euler = rotateQuater.eulerAngles;
            euler.x = turret.transform.eulerAngles.x;
            rotateQuater.eulerAngles = euler;
            turret.transform.rotation = Quaternion.RotateTowards(turret.transform.rotation, rotateQuater, Time.deltaTime * roateTurretFactor);
        }
    }

    public IEnumerator HullTowardsTargetPos()
    {
        Quaternion rotateQuater = new Quaternion();

        while (true)
        {
            yield return null;

            var dir = Vector3.Normalize(targetPos - transform.position);
            dir.y = 0;
            rotateQuater = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotateQuater, Time.deltaTime * rotateFactor);
        }
    }

    public IEnumerator TryShooting()
    {
        var delay = new WaitForSeconds(1.0f);

        while (true)
        {
            yield return delay;

            var dir = (bulletStartPos.transform.position - barrel.transform.position).normalized;
            Ray ray = new Ray(bulletStartPos.transform.position, dir);

            RaycastHit hit;
            if(Physics.Raycast(ray, out hit) == true)
            {
                var player = hit.collider.GetComponent<PlayerControll>();
                
                if (player != null && player.teamIndex.Value != teamIndex.Value)
                    TryShoot();
            }
        }
    }
}
