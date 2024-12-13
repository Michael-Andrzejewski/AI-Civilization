using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EvolStats : MonoBehaviour
{
    public string charName;
    public string faction;
    
    public float attackDamage;
    public float attackSpeed;
    public float maxHealth;
    public float currentHealth;
    public float movementSpeed;
    public float attackRange = 1f;
    public float healthRegen = 0f;
    public bool regen = false;

    public TextMeshPro nameTag;
    public Slider healthBar;
    public bool killed = false;

    void Start()
    {
        //InitializeStats();
        InitializeHealthBar();
        nameTag.text = charName;
        if (regen)
        {
            StartCoroutine(RegenHealth());
        }
    }

    void Update()
    {
        UpdateHealthBar();
    }

    void InitializeStats()
    {
        attackDamage = Random.Range(1, 5);
        attackSpeed = Random.Range(1, 3);
        maxHealth = Random.Range(50, 100);
        currentHealth = maxHealth;
        movementSpeed = Random.Range(1, 4);
    }

    void InitializeHealthBar()
    {
        healthBar.maxValue = maxHealth;
        healthBar.value = currentHealth;
    }

    void UpdateHealthBar()
    {
        healthBar.value = currentHealth;
    }

    IEnumerator RegenHealth()
    {
        if (currentHealth < maxHealth)
        {
            if (currentHealth + healthRegen > maxHealth)
            {
                currentHealth = maxHealth;
            }
            else
            {
                currentHealth += healthRegen;
            }
        }
        yield return new WaitForSeconds(1f);
        StartCoroutine(RegenHealth());
    }


}