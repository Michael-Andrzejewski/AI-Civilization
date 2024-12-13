using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EnemyStats : MonoBehaviour
{
    //actual stats
    
    public float health = 100;
    public float baseDamage = 10;
    public float attackDelay = 1f; //how fast you attack
    public float attackRange = 2f;

    public Slider slider;
    public Gradient gradient;
    public Image fill;

    private float criticalChance = 1f; //1%, 1.5 would be 1.5%
    private float criticalMultiplier = 1.5f; //1.5x damage
    private float dodgeChance = 1f;
    private float counterattackChance = 1f;
    private float counterattackMultiplier = 1f; //1x damage

    // Start is called before the first frame update
    void Start()
    {
        slider.maxValue = health;
    }

    // Update is called once per frame
    void Update()
    {
        slider.value = health;

        fill.color = gradient.Evaluate(slider.normalizedValue);
    }
}
