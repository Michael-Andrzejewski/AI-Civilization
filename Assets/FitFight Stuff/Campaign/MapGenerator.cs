using UnityEngine;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour
{
    public GameObject levelNodePrefab;
    public int numberOfNodes;
    public float mapHeight;
    public float mapWidth;
    public float minNodeSeparation;
    public int overallLevel;
    public int numberOfFactions;
    public Color lineColor;
    public float lineWidth = 0.1f;
    public string generatedMapPlayerPref = "MapGenerated";
    public List<Faction> Factions;
    public bool forceRegenerateMap = false; // check this to regenerate the map each time

    private SaveAndLoadMap saveAndLoadMap; // Add reference to SaveAndLoadMap component

    private void Start()
    {
        saveAndLoadMap = GetComponent<SaveAndLoadMap>(); // Assign SaveAndLoadMap component

        bool mapGenerated = PlayerPrefs.GetInt(generatedMapPlayerPref, 0) == 1;

        if (!mapGenerated || forceRegenerateMap)
        {
            List<LevelNode> levelNodes = GenerateRandomNodes();

            // Convert LevelNode points to Points for triangulation
            List<Point> points = new List<Point>();
            foreach (LevelNode node in levelNodes)
            {
                points.Add(new Point(node.transform.position.x, node.transform.position.z));
            }
            List<Triangle> triangulation = DelaunayHelper.Delaun(points);

            DrawLinesAndConnectNodes(triangulation, levelNodes);

            // Generate factions
            GenerateFactions();

            // Assign factions to nodes
            AssignFactions(levelNodes);

            // Save map data after generating it
            saveAndLoadMap.SaveMapData();

            PlayerPrefs.SetInt(generatedMapPlayerPref, 1);
        }
        else
        {
            // Load the map data if it's already generated
            saveAndLoadMap.LoadMapData();
        }
    }

    private List<LevelNode> GenerateRandomNodes()
    {
        List<Vector3> nodePositions = new List<Vector3>();
        List<LevelNode> levelNodes = new List<LevelNode>();

        for (int i = 0; i < numberOfNodes; i++)
        {
            Vector3 randomPosition;
            bool validPosition;

            do
            {
                validPosition = true;
                randomPosition = new Vector3(
                    Random.Range(-mapWidth / 2, mapWidth / 2),
                    0,
                    Random.Range(-mapHeight / 2, mapHeight / 2));

                foreach (Vector3 nodePosition in nodePositions)
                {
                    if (Vector3.Distance(randomPosition, nodePosition) < minNodeSeparation)
                    {
                        validPosition = false;
                        break;
                    }
                }
            } while (!validPosition);

            nodePositions.Add(randomPosition);

            GameObject newNodeGO = Instantiate(levelNodePrefab, randomPosition, Quaternion.identity);
            newNodeGO.transform.SetParent(transform);
            LevelNode newNode = newNodeGO.GetComponent<LevelNode>();
            levelNodes.Add(newNode);
        }

        return levelNodes;
    }

    private void DrawLinesAndConnectNodes(List<Triangle> triangulation, List<LevelNode> levelNodes)
    {
        foreach (Triangle triangle in triangulation)
        {
            for (int i = 0; i < 3; i++)
            {
                int j = (i + 1) % 3;
                Vector3 nodeAPosition = triangle.vertices[i].pos;
                Vector3 nodeBPosition = triangle.vertices[j].pos;

                LevelNode nodeA = levelNodes.Find(node => node.transform.position.x == nodeAPosition.x && node.transform.position.z == nodeAPosition.y);
                LevelNode nodeB = levelNodes.Find(node => node.transform.position.x == nodeBPosition.x && node.transform.position.z == nodeBPosition.y);

                if (nodeA != null && nodeB != null && !nodeA.connectedNodes.Contains(nodeB) && !nodeB.connectedNodes.Contains(nodeA))
                {
                    // Add a line between the nodes
                    Vector3 lineStartPosition = new Vector3(nodeAPosition.x, 0, nodeAPosition.y);
                    Vector3 lineEndPosition = new Vector3(nodeBPosition.x, 0, nodeBPosition.y);
                    DrawLine(lineStartPosition, lineEndPosition);

                    // Connect the nodes
                    nodeA.connectedNodes.Add(nodeB);
                    nodeB.connectedNodes.Add(nodeA);
                }
            }
        }
    }

    public void DrawLine(Vector3 start, Vector3 end)
    {
        GameObject line = new GameObject();
        line.transform.SetParent(transform);

        LineRenderer lineRenderer = line.AddComponent<LineRenderer>();
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;

        // Set a material and color for the line if desired
        lineRenderer.material = new Material(Shader.Find("Unlit/Color"));
        lineRenderer.material.color = lineColor; // Change the color to red
    }

    private void GenerateFactions()
    {
        //Factions is a global list
        Factions = new List<Faction>
    {
        new Faction("Red", Color.red, 3, 10, 1),
        new Faction("Blue", Color.blue, 4, 15, 2),
        new Faction("Green", Color.green, 5, 20, 3),
        new Faction("Yellow", Color.yellow, 6, 25, 4),
        new Faction("Black", Color.black, 7, 30, 5),
        new Faction("Orange", new Color(1, 0.5f, 0), 8, 35, 6),
        new Faction("Purple", new Color(0.5f, 0, 1), 9, 40, 7),
    };
    }

    private void AssignFactions(List<LevelNode> levelNodes)
    {
        // Shuffle the factions list to randomize the selection of factions
        List<Faction> shuffledFactions = new List<Faction>(Factions);
        Shuffle(shuffledFactions);

        List<LevelNode> remainingNodes = new List<LevelNode>(levelNodes);

        // Clamp the number of factions to between 0 and the smallest count: available factions and nodes
        int clampedFactionCount = Mathf.Clamp(numberOfFactions, 0, Mathf.Min(shuffledFactions.Count, remainingNodes.Count));

        for (int i = 0; i < clampedFactionCount; i++)
        {
            Faction faction = shuffledFactions[i];
            // Choose a random starting node that has not been assigned a faction yet
            LevelNode startingNode = remainingNodes[Random.Range(0, remainingNodes.Count)];

            // Assign the starting node to the faction, apply the faction values, and change its color
            AssignNodeToFaction(startingNode, faction);
            Debug.Log("Faction " + faction.Name + " set to node: " + startingNode.name);

            // Remove the starting node from the list of remaining nodes
            remainingNodes.Remove(startingNode);
        }
        SpreadFaction();
    }

    private void AssignNodeToFaction(LevelNode node, Faction faction)
    {
        // Existing implementation for assigning nodes to faction
        node.factionName = faction.Name;
        node.strength = faction.strength;
        node.number = faction.numbers;
        node.currColor = faction.FactionColor;
        node.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Standard"));
        node.GetComponent<MeshRenderer>().material.color = faction.FactionColor;
        Debug.Log("Node values: " + node.factionName + " " + node.strength);
    }

    private void SpreadFaction()
    {
        // Initialize the list with unclaimed nodes
        List<LevelNode> unclaimedNodes = new List<LevelNode>();
        foreach (LevelNode node in FindObjectsOfType<LevelNode>())
        {
            if (string.IsNullOrEmpty(node.factionName))
            {
                unclaimedNodes.Add(node);
            }
        }

        // Repeat the process until there are no unclaimed nodes
        while (unclaimedNodes.Count > 0)
        {
            // Iterate backward since we'll be removing nodes from the list during the loop
            for (int i = unclaimedNodes.Count - 1; i >= 0; i--)
            {
                LevelNode unclaimedNode = unclaimedNodes[i];

                // Get a list of connected nodes that have a faction assigned
                List<LevelNode> adjacentClaimedNodes = unclaimedNode.connectedNodes.FindAll(node => !string.IsNullOrEmpty(node.factionName));

                // If there are adjacent nodes with factions, randomly select one and claim the unclaimed node
                if (adjacentClaimedNodes.Count > 0)
                {
                    LevelNode claimingNode = adjacentClaimedNodes[Random.Range(0, adjacentClaimedNodes.Count)];
                    ClaimNode(unclaimedNode, claimingNode);
                    unclaimedNodes.RemoveAt(i); // Remove the node from the list of unclaimed nodes
                }
            }
        }
    }

    void ClaimNode(LevelNode nodeToClaim, LevelNode claimingNode)
    {
        nodeToClaim.factionName = claimingNode.factionName;
        nodeToClaim.currColor = claimingNode.currColor;
        nodeToClaim.strength = claimingNode.strength;
        nodeToClaim.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Standard"));
        nodeToClaim.GetComponent<MeshRenderer>().material.color = claimingNode.currColor;
        //AssignNodeToFaction(nodeToClaim, claimingNode.factionValue);
    }

    // Shuffle the list with the Fisher-Yates algorithm
    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int rnd = Random.Range(0, i + 1);

            // Swap elements
            T temp = list[i];
            list[i] = list[rnd];
            list[rnd] = temp;
        }
    }

    
}