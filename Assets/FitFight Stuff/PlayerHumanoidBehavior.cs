using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.AI;

public class PlayerHumanoidBehavior : MonoBehaviour
{
    private float attackRange;
    private float health;
    private float baseDamage;
    private float attackDelay; //how fast you attack

    private float healthWill;
    private float attackWill;

    public bool damageRandomizer = true;

    public NavMeshAgent agent;

    public GameObject damageNumberPrefab;

    public Material damageMaterial;
    public Material startMaterial;

    private GameObject closestEnemy;
    PlayerStats playerStats;
    PlayerStats enemyPlayerStats;

    public string tagToTarget = "Player";

    public bool dead = false;

    public bool alreadyAttacking = false;
    private bool alreadyMoving = false;

    private bool GamblerClass = false;
    private bool ConsistencyClass = false;

    private float minDamagePercentMultiplier = 0.5f;
    private float maxDamagePercentMultiplier = 1.5f;

    private float averageDamage = 0;
    private float totalDamage = 0;
    private float totalAttacks = 0;

    private bool enemyKilledByOtherCheck = false;

    private Animator animator;

    //public SpawnLevelEnemy spawnLevelEnemy;

    // Start is called before the first frame update
    void Start()
    {
        playerStats = GetComponent<PlayerStats>();
        animator = GetComponent<Animator>();

        attackRange = playerStats.attackRange;
        health = playerStats.health;
        baseDamage = playerStats.baseDamage;
        attackDelay = playerStats.attackDelay;

        attackWill = playerStats.attackWill;
        healthWill = playerStats.healthWill;

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

        StartCoroutine(RegenerateHealth());

    }

    public void LoadStats()
    {
        playerStats = GetComponent<PlayerStats>();
        animator = GetComponent<Animator>();

        attackRange = playerStats.attackRange;
        health = playerStats.health;
        baseDamage = playerStats.baseDamage;
        attackDelay = playerStats.attackDelay;

        attackWill = playerStats.attackWill;
        healthWill = playerStats.healthWill;

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

        if (dead)
        {
            //gameObject.tag = "dead";
            return; //nothing can happen if player is dead
        }

        if (!alreadyAttacking)
        {
            MoveTowardsEnemy();
        }
        
        //check if enemy is close enough to attack
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
        if (closestEnemy == null)
        {
            return;
        }

        if (startMaterial == null)
        {
            startMaterial = closestEnemy.GetComponentInChildren<SkinnedMeshRenderer>().material;
        }

        if (closestEnemy != null)
        {
            agent.SetDestination(closestEnemy.transform.position);
        }
        else
        {
            //end attacks
            animator.SetBool("isPunching", false);
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

    public IEnumerator Attack()
    {
        //stop agent from walking
        //start punch, set the punch to take up the whole amount of time
        //wait half-delay while punch is hitting
        //deal damage
        //wait half-delay while punch is retracting
        if (dead)
        {
            yield break; //nothing can happen if player is dead
        }

        agent.SetDestination(transform.position);
        transform.LookAt(closestEnemy.transform);

        animator.SetBool("isPunching", true);
        animator.SetFloat("punchingSpeed", (1.21f / (attackDelay / 2)));

        enemyPlayerStats = closestEnemy.GetComponent<PlayerStats>();
        totalAttacks += 1;

        // Check if the enemy is still in range before dealing damage
        if (Vector3.Distance(transform.position, closestEnemy.transform.position) < attackRange)
        {
            if (damageRandomizer)
            {
                if (GamblerClass)
                {
                    bool lucky = (Random.value > 0.5f);
                    if (lucky)
                    {
                        float randLuckDamage = Random.Range(baseDamage * 1, baseDamage * maxDamagePercentMultiplier);
                        Debug.Log("lucky" + randLuckDamage);
                        

                        // Calculate hit damage multiplier
                        float damageMultiplier = Mathf.Pow(1 + (attackWill / (100 * (1 + playerStats.currentAttacks))), 2f / 3f);

                        // Deal damage
                        enemyPlayerStats.health -= randLuckDamage * damageMultiplier;
                        playerStats.currentAttacks++;
                        totalDamage += randLuckDamage * damageMultiplier;
                        averageDamage = totalDamage / totalAttacks;
                        Debug.Log(averageDamage + " Average Damage Luck");

                        GameObject clone = Instantiate(damageNumberPrefab, closestEnemy.transform.position, Quaternion.identity);
                        clone.GetComponent<TextMeshPro>().text = randLuckDamage.ToString("0");
                    }
                    else
                    {
                        float randLuckDamage = Random.Range(baseDamage * minDamagePercentMultiplier, baseDamage * 1);
                        Debug.Log("unlucky" + randLuckDamage);
                        

                        // Calculate hit damage multiplier
                        float damageMultiplier = Mathf.Pow(1 + (attackWill / (100 * (1 + playerStats.currentAttacks))), 2f / 3f);

                        // Deal damage
                        enemyPlayerStats.health -= randLuckDamage * damageMultiplier;
                        playerStats.currentAttacks++;
                        totalDamage += randLuckDamage * damageMultiplier;
                        averageDamage = totalDamage / totalAttacks;
                        Debug.Log(averageDamage + " Average Damage Luck");

                        GameObject clone = Instantiate(damageNumberPrefab, closestEnemy.transform.position, Quaternion.identity);
                        clone.GetComponent<TextMeshPro>().text = randLuckDamage.ToString("0");
                    }
                }
                else
                {
                    float randDamage = Random.Range(baseDamage * minDamagePercentMultiplier, baseDamage * maxDamagePercentMultiplier);

                    //if enemy is already dead, don't give this agent the kill
                    if (enemyPlayerStats.health <= 0)
                    {
                        enemyKilledByOtherCheck = true;
                    }

                    // Calculate hit damage multiplier
                    float damageMultiplier = Mathf.Pow(1 + (attackWill / (100 * (1 + playerStats.currentAttacks))), 2f / 3f);

                    // Deal damage
                    float trueDamage = randDamage * damageMultiplier;
                    enemyPlayerStats.health -= trueDamage;
                    playerStats.currentAttacks++;

                    //if we eliminated the enemy on this punch
                    if (enemyPlayerStats.health <= 0 && !enemyKilledByOtherCheck)
                    {
                        playerStats.kills++;
                    }
                    totalDamage += trueDamage;
                    averageDamage = totalDamage / totalAttacks;
                    //Debug.Log(averageDamage + " Average Damage Normal");

                    GameObject clone = Instantiate(damageNumberPrefab, closestEnemy.transform.position, Quaternion.identity);
                    clone.GetComponent<TextMeshPro>().text = trueDamage.ToString("0");
                }

                //Debug.Log(randDamage);
            }
            else
            {
                //if enemy is already dead, don't give this agent the kill
                if (enemyPlayerStats.health <= 0)
                {
                    enemyKilledByOtherCheck = true;
                }

                // Calculate hit damage multiplier
                float damageMultiplier = Mathf.Pow(1 + (attackWill / (100 * (1 + playerStats.currentAttacks))), 2f / 3f);

                // Deal damage
                float trueDamage = baseDamage * damageMultiplier;
                enemyPlayerStats.health -= trueDamage;
                playerStats.currentAttacks++;

                //if we eliminated the enemy on this punch
                if (enemyPlayerStats.health <= 0 && !enemyKilledByOtherCheck)
                {
                    playerStats.kills++;
                }
                GameObject clone = Instantiate(damageNumberPrefab, closestEnemy.transform.position, Quaternion.identity);
                clone.GetComponent<TextMeshPro>().text = trueDamage.ToString("0");
            }
        }
        else
        {
            // Enemy is out of range, stop the attack
            animator.SetBool("isPunching", false);
            alreadyAttacking = false;
            yield break;
        }

        StartCoroutine(DamageRecolor());
        yield return new WaitForSeconds(attackDelay / 2);

        //if enemy is dead
        if (closestEnemy.GetComponent<PlayerStats>().health <= 0)
        {
            animator.SetBool("isPunching", false);

            //enemy death animation, xp gain, etc
            closestEnemy.GetComponent<Animator>().SetBool("Death", true);

            closestEnemy.GetComponent<PlayerHumanoidBehavior>().dead = true;

            closestEnemy.GetComponent<NavMeshAgent>().enabled = false;

            closestEnemy.tag = "Dead";

            alreadyAttacking = false;
            alreadyMoving = false;

        }
        else
        {
            animator.SetBool("isPunching", false);
            yield return new WaitForSeconds(attackDelay / 2);
            //if enemy is out of range
            if (Vector3.Distance(transform.position, closestEnemy.transform.position) > attackRange * 2)
            {
                animator.SetBool("isPunching", false);
                alreadyAttacking = false;
            }

            StartCoroutine(Attack());
        }


    }

    IEnumerator DamageRecolor()
    {
        closestEnemy.GetComponentInChildren<SkinnedMeshRenderer>().material = damageMaterial;
        yield return new WaitForSeconds(attackDelay / 2);
        closestEnemy.GetComponentInChildren<SkinnedMeshRenderer>().material = startMaterial;
    }

    private void OnDestroy()
    {
        if (closestEnemy == null)
        {
            return;
        }
        closestEnemy.GetComponentInChildren<SkinnedMeshRenderer>().material = startMaterial;
        
    }

    private IEnumerator RegenerateHealth()
    {
        while (!dead && playerStats.health > 0)
        {
            playerStats.health += healthWill;
            //Debug.Log("Regerated " + healthWill);

            // Make sure health doesn't exceed the maximum health
            if (playerStats.health > playerStats.slider.maxValue)
            {
                playerStats.health = playerStats.slider.maxValue;
            }

            yield return new WaitForSeconds(attackDelay);
        }
    }
}
