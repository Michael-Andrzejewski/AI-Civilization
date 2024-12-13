using UnityEngine;

[System.Serializable]
public class Faction
{
    public string Name;
    public Color FactionColor;
    public int strength;
    public int numbers;
    public int spread;

    public Faction(string name, Color factionColor, int strength, int numbers, int spread)
    {
        Name = name;
        FactionColor = factionColor;
        this.strength = strength;
        this.numbers = numbers;
        this.spread = spread;
    }

    // If necessary, add default constructor so that it can be serialized properly
    public Faction() { }
}