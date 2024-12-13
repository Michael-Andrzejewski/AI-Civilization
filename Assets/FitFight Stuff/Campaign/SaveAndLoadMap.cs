using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

public class SaveAndLoadMap : MonoBehaviour
{
    public MapGenerator mapGenerator;
    public string saveFileName = "mapData.save";

    [System.Serializable]
    public class SaveData
    {
        public List<LevelNodeData> levelNodesData;
        public List<Faction> factions;
    }

    [System.Serializable]
    public class FactionData
    {
        public string Name;
        public SerializableColor FactionColor; // Assuming SerializableColor is a representable version of Color
        public int strength;
        public int numbers;
        public int spread;

        public FactionData(Faction faction)
        {
            Name = faction.Name;
            FactionColor = new SerializableColor(faction.FactionColor);
            this.strength = faction.strength;
            this.numbers = faction.numbers;
            this.spread = faction.spread;
        }

        public Faction ToFaction()
        {
            return new Faction(Name, FactionColor.ToColor(), strength, numbers, spread);
        }
    }

    private void Start()
    {
        mapGenerator = GetComponent<MapGenerator>();
        bool mapGenerated = PlayerPrefs.GetInt(mapGenerator.generatedMapPlayerPref, 0) == 1;

        if (mapGenerated && mapGenerator.forceRegenerateMap == false)
        {
            LoadMapData();
        }
    }

    public void SaveMapData()
    {
        // Create the save data container
        SaveData saveData = new SaveData
        {
            levelNodesData = new List<LevelNodeData>(),

            // Convert Factions to FactionData list for serialization
            //factions = mapGenerator.Factions.ConvertAll(faction => new FactionData(faction))
        };

        List<LevelNodeData> levelNodesData = new List<LevelNodeData>();

        foreach (LevelNode node in FindObjectsOfType<LevelNode>())
        {
            LevelNodeData nodeData = new LevelNodeData();
            nodeData.SetPosition(node.transform.position);
            nodeData.factionName = node.factionName;
            nodeData.strength = node.strength;
            nodeData.number = node.number;
            nodeData.connectedNodeIndexes = new List<int>();

            foreach (LevelNode connectedNode in node.connectedNodes)
            {
                int connectedNodeIndex = FindNodeIndex(connectedNode);
                nodeData.connectedNodeIndexes.Add(connectedNodeIndex);
            }

            levelNodesData.Add(nodeData);
        }

        // Save the file
        string savePath = Path.Combine(Application.persistentDataPath, saveFileName);
        BinaryFormatter bf = new BinaryFormatter();

        using (FileStream file = File.Create(savePath))
        {
            bf.Serialize(file, saveData);
        }

        Debug.Log("Map data saved to " + savePath);
    }

    public void LoadMapData()
    {
        string savePath = Path.Combine(Application.persistentDataPath, saveFileName);

        if (!File.Exists(savePath))
        {
            Debug.LogError("Save file not found at: " + savePath);
            return;
        }

        List<LevelNodeData> levelNodesData;

        BinaryFormatter bf = new BinaryFormatter();

        using (FileStream file = File.Open(savePath, FileMode.Open))
        {
            levelNodesData = (List<LevelNodeData>)bf.Deserialize(file);
        }

        // Delete existing nodes
        foreach (LevelNode node in FindObjectsOfType<LevelNode>())
        {
            Destroy(node.gameObject);
        }

        // Create new nodes from save data
        List<LevelNode> loadedNodes = new List<LevelNode>();

        foreach (LevelNodeData nodeData in levelNodesData)
        {
            GameObject newNodeGO = Instantiate(mapGenerator.levelNodePrefab, nodeData.GetPosition(), Quaternion.identity);
            newNodeGO.transform.SetParent(mapGenerator.transform);
            LevelNode newNode = newNodeGO.GetComponent<LevelNode>();
            loadedNodes.Add(newNode);

            newNode.factionName = nodeData.factionName;
            Faction nodeFaction = mapGenerator.Factions.Find(f => f.Name == nodeData.factionName);
            if (nodeFaction != null)
            {
                Debug.Log("Node faction: " + nodeFaction.Name);
                Debug.Log("Node color: " + nodeFaction.FactionColor);

                newNode.currColor = nodeFaction.FactionColor;
            } else
            {
                Debug.Log("Node faction not determined");
            }
            newNode.strength = nodeData.strength;
            newNode.number = nodeData.number;
            newNode.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Standard"));
            newNode.GetComponent<MeshRenderer>().material.color = newNode.currColor;
        }

        // Connect loaded nodes
        for (int i = 0; i < loadedNodes.Count; i++)
        {
            LevelNodeData nodeData = levelNodesData[i];
            LevelNode loadedNode = loadedNodes[i];

            foreach (int connectedNodeIndex in nodeData.connectedNodeIndexes)
            {
                if (connectedNodeIndex < loadedNodes.Count)
                {
                    loadedNode.connectedNodes.Add(loadedNodes[connectedNodeIndex]);
                }
            }
        }

        // Redraw connection lines between nodes
        foreach (LevelNode node in loadedNodes)
        {
            foreach (LevelNode connectedNode in node.connectedNodes)
            {
                // Check to prevent drawing the same line multiple times
                if (loadedNodes.IndexOf(node) < loadedNodes.IndexOf(connectedNode))
                {
                    mapGenerator.DrawLine(node.transform.position, connectedNode.transform.position);
                }
            }
        }

        Debug.Log("Map data loaded from " + savePath);
    }

    private int FindNodeIndex(LevelNode nodeToFind)
    {
        LevelNode[] allNodes = FindObjectsOfType<LevelNode>();

        for (int i = 0; i < allNodes.Length; i++)
        {
            if (allNodes[i] == nodeToFind)
            {
                return i;
            }
        }

        return -1;
    }
}

[System.Serializable]
public class LevelNodeData
{
    public SerializableVector3 position;
    public string factionName;
    public int strength;
    public int number;
    public List<int> connectedNodeIndexes;

    public void SetPosition(Vector3 vector3)
    {
        position = new SerializableVector3(vector3);
    }

    public Vector3 GetPosition()
    {
        return position.ToVector3();
    }
}

[System.Serializable]
public class SerializableVector3
{
    public float x;
    public float y;
    public float z;

    public SerializableVector3(Vector3 vector3)
    {
        x = vector3.x;
        y = vector3.y;
        z = vector3.z;
    }

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}

// This is a placeholder for this class which would not exist in the original script
// Replace SerializableColor with your correct implementation or use a workaround as Unity's Color is serializable
[System.Serializable]
public class SerializableColor
{
    private float r;
    private float g;
    private float b;
    private float a;

    public SerializableColor(Color color)
    {
        r = color.r;
        g = color.g;
        b = color.b;
        a = color.a;
    }

    public Color ToColor()
    {
        return new Color(r, g, b, a);
    }
}