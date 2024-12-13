using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerStats : MonoBehaviour
{
    //attacking stats
    public string playerName;

    public float attackRange = 2f;
    public float health = 100;
    public float baseDamage = 10;
    public float attackDelay = 1f; //how fast you attack
    public float maxHealth;

    public float attackWill;
    public float healthWill;

    public Slider slider;
    public Gradient gradient;
    public Image fill;

    public TextMeshPro nameTag;
    public int kills;
    public int currentAttacks;


    public float criticalChance = 1f; //1%, 1.5 would be 1.5%
    public float criticalMultiplier = 1.5f; //1.5x damage
    public float dodgeChance = 1f;
    public float counterattackChance = 1f;
    public float counterattackMultiplier = 1f; //1x damage

    public bool GamblerClass = false;
    public bool ConsistencyClass = false;

    public void Awake()
    {
        if (PlayerPrefs.GetFloat("attackSpeed") <= 0)
        {
            PlayerPrefs.SetFloat("attackSpeed", 4);
        }
        attackDelay = PlayerPrefs.GetFloat("attackSpeed");
        slider.maxValue = health;
        //maxHealth = health;

        nameTag.text = playerName;

    }

    /*
    IEnumerator LeaderboardUpdate()
    {
        Debug.Log("Waiting");
        yield return new WaitForSeconds(5f);
        Debug.Log("maybe?");
        //playFabManager.SendLeaderboard((int)health);
        Debug.Log("Success maybe?");
    }
    */

    private void Update()
    {
        slider.value = health;
        fill.color = gradient.Evaluate(slider.normalizedValue);
    }

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

    //actual stats
    
    public float health = 100;
    
    public float baseDamage = 10;
    
    public float attackDelay = 1f; //how fast you attack

    private float criticalChance = 1f; //1%, 1.5 would be 1.5%
    private float criticalMultiplier = 1.5f; //1.5x damage
    private float dodgeChance = 1f;
    private float counterattackChance = 1f;
    private float counterattackMultiplier = 1f; //1x damage

    //potential stats
    private float endurance = 10f; //endurance determines how long you can continue attacking fast 
    //(determines attack speed graph and how sharply it slopes downward)
    private float enduranceRecharge = 1f; //determines how fast your endurance recharges when out of combat
    */






}
