using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EnemyBehavior : MonoBehaviour
{
    public Material damageMaterial;

    public Slider slider;
    public Gradient gradient;
    public Image fill;

    public bool damageRandomizer = true;

    private GameObject player;
    EnemyStats enemyStats;
    PlayerStats playerStats;
    PlayerBehavior playerBehavior;

    private float health;
    private float baseDamage;
    private float attackDelay;

    private float attackRange;

    private Material startMaterial;

    // Start is called before the first frame update
    void Start()
    {
        enemyStats = GetComponent<EnemyStats>();

        health = enemyStats.health;
        baseDamage = enemyStats.baseDamage;
        attackDelay = enemyStats.attackDelay;
        attackRange = enemyStats.attackRange;

        slider.maxValue = health;

        StartCoroutine(MoveTowardsEnemy());
    }

    private void Update()
    {
        slider.value = enemyStats.health;

        fill.color = gradient.Evaluate(slider.normalizedValue);
    }

    public GameObject FindClosestPlayer() //thanks Unity, borrowed your code
    {
        GameObject[] gos;
        gos = GameObject.FindGameObjectsWithTag("Player");
        GameObject closest = null;
        float distance = Mathf.Infinity;
        Vector3 position = transform.position;
        foreach (GameObject go in gos)
        {
            Vector3 diff = go.transform.position - position;
            float curDistance = diff.sqrMagnitude;
            if (curDistance < distance)
            {
                closest = go;
                distance = curDistance;
            }
        }
        return closest;
    }

    IEnumerator MoveTowardsEnemy()
    {
        yield return null;
        //find enemy
        player = FindClosestPlayer();



        if (startMaterial == null)
        {
            startMaterial = player.GetComponent<MeshRenderer>().material;
        }

        if (player == null)
        {
            //end game
            yield return null;
        }

        //move towards them if the distance is still small
        if (Vector3.Distance(player.transform.position, transform.position) > attackRange) //yes, I realize this is stupid/inefficient but I'm feeling lazy
        {
            float step = 0.02f;
            transform.position = Vector3.MoveTowards(transform.position, player.transform.position, step);
            StartCoroutine(MoveTowardsEnemy());
        }
        else //player is close enough to attack
        {
            playerBehavior = player.GetComponent<PlayerBehavior>();
            StartCoroutine(Attack());
        }
    }

    IEnumerator Attack()
    {
        yield return new WaitForSeconds(attackDelay);

        if (damageRandomizer)
        {
            float randDamage = Random.Range(baseDamage * 0.5f, baseDamage * 1.5f);
            playerBehavior.health -= randDamage;
        }
        else
        {
            playerBehavior.health -= baseDamage;
        }

        
        StartCoroutine(DamageRecolor());
        if (playerBehavior.health <= 0)
        {
            //enemy death animation, xp gain, etc
            
            Destroy(player);
            StartCoroutine(MoveTowardsEnemy());
        }
        else
        {
            StartCoroutine(Attack());
        }
    }

    IEnumerator DamageRecolor()
    {
        player.GetComponent<MeshRenderer>().material = damageMaterial;
        yield return new WaitForSeconds(0.1f);
        player.GetComponent<MeshRenderer>().material = startMaterial;
    }

    void OnDestroy()
    {
        if (player == null)
        {
            return;
        }
        player.GetComponent<MeshRenderer>().material = startMaterial;
        slider.value = 0;
    }
}
