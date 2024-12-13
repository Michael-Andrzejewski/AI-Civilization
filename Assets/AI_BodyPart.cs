using UnityEngine;
using System.Collections.Generic;

public class AI_BodyPart : MonoBehaviour
{
    public string partName;
    public float hitChance = 0f;
    public float painMultiplier = 1f;
    public bool isVital = false;
    public float maxHealth = 100f;
    public float health = 100f;
    public List<AI_BodyPart> childParts = new List<AI_BodyPart>();

    public float InflictDamage(float damage)
    {
        health = Mathf.Max(0, health - damage);
        return damage * painMultiplier;
    }

    public bool IsInjured()
    {
        return health < maxHealth;
    }

    public string GetHealthStatus()
    {
        return $"{partName}: {health:F1}/{maxHealth:F1}";
    }
} 