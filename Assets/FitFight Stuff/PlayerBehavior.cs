using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerBehavior : MonoBehaviour
{
    //attacking stats
    public float attackRange = 2f;

    /*
    //player trainable stats
    public int strengthStat = 1; //damage
    public int precisionStat = 1; //critical chance and critical multiplier
    public int agilityStat = 1; //dodge and health
    public int reflexStat = 1; //counterattack and dodge chance

    public int swordSkill = 1; //sword damage multiplier
    public int gunSkill = 1; //gun damage multiplier and use chance
    public int arrowSkill = 1; //arrow damage multiplier and use chance
    public int throwingSkill = 1; //how much damage throws do
    //public int healthStat = 1; //how much health player has
    */

    //actual stats
    
    public float health = 100;
    public float baseDamage = 10;
    public float attackDelay = 1f; //how fast you attack

    public bool damageRandomizer = true;

    public Slider slider;
    public Gradient gradient;
    public Image fill;

    public Material damageMaterial;
    private Material startMaterial;



    private GameObject closestEnemy;
    EnemyStats enemyStats;

    // Start is called before the first frame update
    void Start()
    {
        slider.maxValue = health;
        StartCoroutine(MoveTowardsEnemy());
        
    }

    private void Update()
    {
        
        slider.value = health;
        
        fill.color = gradient.Evaluate(slider.normalizedValue);
    }


    public GameObject FindClosestEnemy() //thanks Unity, borrowed your code
    {
        GameObject[] gos;
        gos = GameObject.FindGameObjectsWithTag("Enemy");
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
        closestEnemy = FindClosestEnemy();
        if (startMaterial == null)
        {
            startMaterial = closestEnemy.GetComponent<MeshRenderer>().material;
        }
        

        if (closestEnemy == null)
        {
            //end game
            yield return null;
        }

        //move towards them if the distance is still small
        if (Vector3.Distance(closestEnemy.transform.position, transform.position) > attackRange) //yes, I realize this is stupid/inefficient but I'm feeling lazy
        {
            float step = 0.02f;
            transform.position = Vector3.MoveTowards(transform.position, closestEnemy.transform.position, step);
            StartCoroutine(MoveTowardsEnemy());
        }
        else //player is close enough to attack
        {
            enemyStats = closestEnemy.GetComponent<EnemyStats>();
            StartCoroutine(Attack());
        }
    }

    IEnumerator Attack()
    {
        yield return new WaitForSeconds(attackDelay);

        if (damageRandomizer)
        {
            float randDamage = Random.Range(baseDamage * 0.5f, baseDamage * 1.5f);
            enemyStats.health -= randDamage;
            //Debug.Log(randDamage);
        }
        else
        {
            enemyStats.health -= baseDamage;
        }

        
        StartCoroutine(DamageRecolor());
        if (enemyStats.health <= 0)
        {
            //enemy death animation, xp gain, etc
            Destroy(closestEnemy);
            StartCoroutine(MoveTowardsEnemy());
        } else
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
        slider.value = 0;
    }
}
