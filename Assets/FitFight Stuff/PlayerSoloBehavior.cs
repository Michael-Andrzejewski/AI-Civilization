using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.AI;

public class PlayerSoloBehavior : MonoBehaviour
{
    private float attackRange;
    private float health;
    private float baseDamage;
    private float attackDelay; //how fast you attack

    public bool damageRandomizer = true;

    public NavMeshAgent agent;

    public GameObject damageNumberPrefab;

    public Material damageMaterial;
    private Material startMaterial;
    
    private GameObject closestEnemy;
    PlayerStats playerStats;
    PlayerStats enemyPlayerStats;

    public string tagToTarget = "Player";

    private bool alreadyAttacking = false;
    private bool alreadyMoving = false;

    private bool GamblerClass = false;
    private bool ConsistencyClass = false;

    private float minDamagePercentMultiplier = 0.5f;
    private float maxDamagePercentMultiplier = 1.5f;

    private float averageDamage = 0;
    private float totalDamage = 0;
    private float totalAttacks = 0;

    // Start is called before the first frame update
    void Start()
    {
        playerStats = GetComponent<PlayerStats>();
        attackRange = playerStats.attackRange;
        health = playerStats.health;
        baseDamage = playerStats.baseDamage;
        attackDelay = playerStats.attackDelay;

        GamblerClass = playerStats.GamblerClass;
        ConsistencyClass = playerStats.ConsistencyClass;

        if (GamblerClass)
        {
            attackDelay *= 2f;
            baseDamage *= 2f;
            minDamagePercentMultiplier = 0f;
            maxDamagePercentMultiplier = 2.1f;
        }
        if (ConsistencyClass)
        {
            attackDelay /= 2f;
            baseDamage /= 2f;
        }
        
    }

    // Update is called once per frame
    void Update()
    {

        

        MoveTowardsEnemy();

        if (closestEnemy != null && !alreadyAttacking && Vector3.Distance(transform.position, closestEnemy.transform.position) < attackRange)
        {
            
            enemyPlayerStats = closestEnemy.GetComponent<PlayerStats>();
            
            alreadyAttacking = true;
            StartCoroutine(Attack());
        }
    }

    void MoveTowardsEnemy()
    {
        closestEnemy = FindClosestEnemy();

        if (startMaterial == null)
        {
            startMaterial = closestEnemy.GetComponent<MeshRenderer>().material;
        }

        if (closestEnemy != null)
        {
            agent.SetDestination(closestEnemy.transform.position);
        }
        
        
    }

    public GameObject FindClosestEnemy() //thanks Unity, borrowed your code
    {
        GameObject[] gos;
        gos = GameObject.FindGameObjectsWithTag(tagToTarget);
        GameObject closest = null;
        float distance = Mathf.Infinity;
        Vector3 position = transform.position;
        foreach (GameObject go in gos)
        {
            if (go != gameObject) //ensure that we're not picking ourself
            {
                Vector3 diff = go.transform.position - position;
                float curDistance = diff.sqrMagnitude;
                if (curDistance < distance)
                {
                    closest = go;
                    distance = curDistance;
                }
            }
            
        }
        return closest;
    }

    IEnumerator Attack()
    {
        yield return new WaitForSeconds(attackDelay);
        enemyPlayerStats = closestEnemy.GetComponent<PlayerStats>();
        totalAttacks += 1;

        //Debug.Log("Attack!!");

        if (damageRandomizer)
        {
            if (GamblerClass)
            {
                bool lucky = (Random.value > 0.5f);
                if (lucky)
                {
                    float randLuckDamage = Random.Range(baseDamage * 1, baseDamage * maxDamagePercentMultiplier);
                    Debug.Log("lucky" + randLuckDamage);
                    enemyPlayerStats.health -= randLuckDamage;
                    totalDamage += randLuckDamage;
                    averageDamage = totalDamage / totalAttacks;
                    Debug.Log(averageDamage + " Average Damage Luck");

                    GameObject clone = Instantiate(damageNumberPrefab, closestEnemy.transform.position, Quaternion.identity);
                    clone.GetComponent<TextMeshPro>().text = randLuckDamage.ToString("0");
                } 
                else
                {
                    float randLuckDamage = Random.Range(baseDamage * minDamagePercentMultiplier, baseDamage * 1);
                    Debug.Log("unlucky" + randLuckDamage);
                    enemyPlayerStats.health -= randLuckDamage;
                    totalDamage += randLuckDamage;
                    averageDamage = totalDamage / totalAttacks;
                    Debug.Log(averageDamage + " Average Damage Luck");

                    GameObject clone = Instantiate(damageNumberPrefab, closestEnemy.transform.position, Quaternion.identity);
                    clone.GetComponent<TextMeshPro>().text = randLuckDamage.ToString("0");
                }
            }
            else
            {
                float randDamage = Random.Range(baseDamage * minDamagePercentMultiplier, baseDamage * maxDamagePercentMultiplier);
                enemyPlayerStats.health -= randDamage;
                totalDamage += randDamage;
                averageDamage = totalDamage / totalAttacks;
                Debug.Log(averageDamage + " Average Damage Normal");

                GameObject clone = Instantiate(damageNumberPrefab, closestEnemy.transform.position, Quaternion.identity);
                clone.GetComponent<TextMeshPro>().text = randDamage.ToString("0");
            }
            
            //Debug.Log(randDamage);
        }
        else
        {
            enemyPlayerStats.health -= baseDamage;
        }


        StartCoroutine(DamageRecolor());
        if (enemyPlayerStats.health <= 0)
        {
            //enemy death animation, xp gain, etc
            Destroy(closestEnemy);
            alreadyAttacking = false;
            alreadyMoving = false;
            playerStats.kills += 1;
        }
        else
        {
            StartCoroutine(Attack());
        }
    }

    IEnumerator DamageRecolor()
    {
        closestEnemy.GetComponent<MeshRenderer>().material = damageMaterial;
        yield return new WaitForSeconds(attackDelay / 2);
        closestEnemy.GetComponent<MeshRenderer>().material = startMaterial;
    }

    private void OnDestroy()
    {
        if (closestEnemy == null)
        {
            return;
        }
        closestEnemy.GetComponent<MeshRenderer>().material = startMaterial;
        
    }


}
