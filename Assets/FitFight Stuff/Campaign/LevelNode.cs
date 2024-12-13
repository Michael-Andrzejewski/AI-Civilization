using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class LevelNode : MonoBehaviour
{
    public string factionName;
    public int difficulty;
    public int strength;
    public int number;
    public List<LevelNode> connectedNodes;
    public string battlefieldType;
    public Color startingColor;
    public Color currColor;
    public Faction factionValue;

    //public TextMeshPro difficultyText;
    public GameObject battlefieldImage;

    
    private void Start()
    {
        //SetColor(startingColor);
        //difficultyText = GetComponentInChildren<TextMeshPro>();
        //difficultyText.text = difficulty.ToString();
    }
    

    //public void SetColor(Color color)
    //{
    //    MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
    //    Debug.Log("Attempted to set color to " + color);
    //    if (meshRenderer != null)
    //    {
    //        meshRenderer.material = new Material(Shader.Find("Standard"));
    //        meshRenderer.material.color = color;
    //    }
    //}
}