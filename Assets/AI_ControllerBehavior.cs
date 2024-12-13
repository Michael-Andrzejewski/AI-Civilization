using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;
using System.Collections;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System;
using System.Linq;

public class AI_ControllerBehavior : MonoBehaviour
{
    [Header("AI State")]
    public string targetName;
    private StringBuilder persistentContext = new StringBuilder();
    [SerializeField]
    private bool revealNearbyStats = false;

    [Header("AI Prompts")]
    [TextArea(3, 10)]
    public string systemPrompt = "You are an AI agent in a combat simulation. You can communicate with other agents and engage in combat. Your responses should reflect your personality and strategic thinking. Focus on building alliances when possible, but remember that attacking is ultimately how you win the game. Successfully attacking and defeating another character will gain you points.";
    [TextArea(3, 10)]
    public string instructionPrompt = "You may take ONE of the following actions. Respond with exactly ONE action in brackets:\n" +
        "[message agentName: Your message] - to communicate with another agent\n" +
        "[attack agentName] - to engage in combat with an agent\n" +
        "Consider your relationships, current health, and tactical situation when deciding.";
    [TextArea(3, 10)]
    public string contextPrompt = "";

    [Header("AI Behavior")]
    public float actionCooldown = 10f;
    private float nextActionTime;

    [Header("Reproduction")]
    public GameObject aiAgentPrefab;

    // Reference to nearby entities
    private List<AI_ControllerBehavior> nearbyAgents = new List<AI_ControllerBehavior>();
    private List<AI_ControllerBehavior> allies = new List<AI_ControllerBehavior>();
    private List<AI_ControllerBehavior> enemies = new List<AI_ControllerBehavior>();

    [Header("Combat")]
    public float attackRange = 2f;
    public float attackSpeed = 1f;
    private NavMeshAgent agent;
    private Animator animator;

    [Header("Communication")]
    [TextArea(10,30)]
    public string globalChatHistory = "";
    private const int MAX_CHAT_LENGTH = 1000;
    [TextArea(2,5)]
    public string messageToSend = "";
    public string receiverName = "";
    [SerializeField]
    private bool sendMessageButton;  // This will show up as a checkbox in the inspector

    [Header("OpenAI Configuration")]
    private readonly string openAIEndpoint = "https://api.openai.com/v1/chat/completions";
    [SerializeField, Tooltip("Your OpenAI API Key")]
    private string apiKey;
    [SerializeField, Tooltip("The AI model to use for this agent")]
    private string aiModel = "gpt-4o-mini";

    [System.Serializable]
    private class OpenAIRequest
    {
        public string model;
        public List<MessageData> messages;
        public float temperature = 0.7f;
    }

    [System.Serializable]
    private class MessageData
    {
        public string role;
        public string content;
    }

    [System.Serializable]
    private class OpenAIResponse
    {
        public Choice[] choices;
    }

    [System.Serializable]
    private class Choice
    {
        public MessageData message;
    }

    private bool isAttacking = false;
    private StringBuilder actionHistory = new StringBuilder();

    [Header("Action History")]
    [TextArea(10,30)]
    [SerializeField]
    private string actionHistoryDisplay;

    [Header("Body")]
    public float totalPain = 0f;
    public float painThreshold = 100f;
    private Dictionary<string, AI_BodyPart> bodyParts = new Dictionary<string, AI_BodyPart>();

    [Header("Internal Monologue")]
    [SerializeField] private int actionsBeforeReflection = 5;
    private int actionsSinceLastReflection = 0;
    [TextArea(5,20)]
    [SerializeField] private string lastInternalMonologue = "";
    public string internalMonologuePrompt = "Based on your recent actions, messages, and current status:\n" +
        "1. Are your current strategies working? Why or why not?\n" +
        "2. Should you change your approach to combat or communication?\n" +
        "3. What are your biggest threats right now?\n" +
        "4. What opportunities do you see?\n" +
        "Respond in first person, as if thinking to yourself.";

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        // Set the AI agent prefab to itself if not already assigned
        if (aiAgentPrefab == null)
        {
            aiAgentPrefab = gameObject;
        }

        InitializeBodyParts();
    }

    private void InitializeBodyParts()
    {
        // Initialize main body regions with hit chances
        CreateBodyPart("Head", 0.20f, 2f, true);
        CreateBodyPart("Torso", 0.50f, 1.5f, true);
        CreateBodyPart("Legs", 0.30f, 1f, false);

        // Add sub-parts to head
        var head = bodyParts["Head"];
        CreateSubPart(head, "Skull", 0.40f, 2.5f, true);
        CreateSubPart(head, "Left Eye", 0.10f, 1.5f, false);
        CreateSubPart(head, "Right Eye", 0.10f, 1.5f, false);
        // ... add other head parts ...

        // Add vital organs to torso
        var torso = bodyParts["Torso"];
        CreateSubPart(torso, "Heart", 0.05f, 5f, true);
        CreateSubPart(torso, "Spine", 0.05f, 4f, true);
        // ... add other torso parts ...
    }

    private void CreateBodyPart(string name, float hitChance, float painMultiplier, bool isVital)
    {
        var part = new GameObject($"Part_{name}").AddComponent<AI_BodyPart>();
        part.transform.parent = transform;
        part.partName = name;
        part.hitChance = hitChance;
        part.painMultiplier = painMultiplier;
        part.isVital = isVital;
        bodyParts[name] = part;
    }

    private void CreateSubPart(AI_BodyPart parent, string name, float hitChance, float painMultiplier, bool isVital)
    {
        var part = new GameObject($"Part_{name}").AddComponent<AI_BodyPart>();
        part.transform.parent = parent.transform;
        part.partName = name;
        part.hitChance = hitChance;
        part.painMultiplier = painMultiplier;
        part.isVital = isVital;
        parent.childParts.Add(part);
        bodyParts[name] = part;
    }

    private AI_BodyPart SelectHitLocation()
    {
        float roll = UnityEngine.Random.value;
        float currentChance = 0f;

        // First try to hit main body regions
        foreach (var part in bodyParts.Values)
        {
            currentChance += part.hitChance;
            if (roll <= currentChance)
            {
                // If we hit a main region, potentially hit a sub-part
                if (part.childParts.Count > 0 && UnityEngine.Random.value < 0.4f)
                {
                    return part.childParts[UnityEngine.Random.Range(0, part.childParts.Count)];
                }
                return part;
            }
        }

        // Default to torso if somehow nothing was hit
        return bodyParts["Torso"];
    }

    private void Update()
    {
        // At the start of Update, sync the display
        actionHistoryDisplay = actionHistory.ToString();
        
        // Check for death first
        var stats = GetComponent<EvolStats>();
        if ((stats != null && stats.currentHealth <= 0 && !stats.killed) || totalPain >= painThreshold)
        {
            // Mark as killed to prevent multiple executions
            if (stats != null)
            {
                stats.killed = true;
            }
            
            // Play death animation
            if (animator != null)
            {
                animator.SetBool("Death", true);
            }
            
            // Disable navigation and AI control
            if (agent != null)
            {
                agent.enabled = false;
            }
            
            // Change tag and disable AI
            gameObject.tag = "Dead";
            enabled = false;
            
            // Optional: Add deletion after delay
            gameObject.AddComponent<DeleteAfterSeconds>().seconds = 30;
            
            // Clear target to stop any movement
            targetName = "";
            
            return;
        }

        if (Time.time >= nextActionTime)
        {
            UpdateContextPrompt();
            DecideAction();
            nextActionTime = Time.time + actionCooldown;
        }

        // Handle movement and attacking
        if (!string.IsNullOrEmpty(targetName))
        {
            GameObject target = GameObject.Find(targetName);
            if (target == null || target.CompareTag("Dead"))
            {
                // Stop moving if target is null or dead
                if (agent != null && agent.enabled)
                {
                    agent.SetDestination(transform.position);
                }
                
                // Update persistent context about dead or non-existent target
                if (target == null)
                {
                    persistentContext.AppendLine($"{targetName} does not exist and cannot be targeted for attack.");
                    Debug.Log($"<{gameObject.name}> {targetName} does not exist and cannot be targeted for attack.");
                }
                else if (target.CompareTag("Dead"))
                {
                    persistentContext.AppendLine($"{targetName} is dead and cannot be targeted for attack.");
                    Debug.Log($"<{gameObject.name}> {targetName} is dead and cannot be targeted for attack.");
                }
                
                targetName = ""; // Clear the target
                return;
            }

            if (target != gameObject)
            {
                float distance = Vector3.Distance(transform.position, target.transform.position);
                if (distance <= attackRange)
                {
                    StartCoroutine(Attack(target));
                }
                else if (agent != null && agent.enabled)
                {
                    agent.SetDestination(target.transform.position);
                }
            }
        }
    }

    IEnumerator Attack(GameObject target)
    {
        // Prevent multiple attack coroutines from running at the same time
        if (isAttacking)
        {
            yield break;
        }
        isAttacking = true;

        if (target == null || target == gameObject)
        {
            isAttacking = false;
            yield break;
        }

        // Get our own stats
        var myStats = GetComponent<EvolStats>();
        if (myStats == null || myStats.killed)
        {
            isAttacking = false;
            yield break;
        }

        while (target != null)
        {
            // Check if we're dead or our NavMeshAgent is disabled
            if (myStats.killed || !agent.enabled || !agent.isOnNavMesh)
            {
                break;
            }

            // Check target's health and tag
            var targetStats = target.GetComponent<EvolStats>();
            if (targetStats == null || targetStats.currentHealth <= 0 || targetStats.killed || target.CompareTag("Dead"))
            {
                break;  // Exit the attack loop if target is dead
            }

            float distance = Vector3.Distance(transform.position, target.transform.position);
            
            if (distance > attackRange)
            {
                // Only try to move if we have a valid NavMeshAgent
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.SetDestination(target.transform.position);
                }
                yield return null;
                continue;
            }

            // Stop moving and face target
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.SetDestination(transform.position);
            }
            transform.LookAt(target.transform);

            // Set animation speed to match attack speed and start animation
            if (animator != null)
            {
                animator.SetFloat("punchingSpeed", 1f / myStats.attackSpeed);
                animator.SetBool("isPunching", true);
            }

            // Wait for animation to reach "hit" point (halfway through)
            yield return new WaitForSeconds(myStats.attackSpeed * 0.5f);

            // Apply damage using body part system
            if (!targetStats.killed && !target.CompareTag("Dead"))
            {
                var targetAI = target.GetComponent<AI_ControllerBehavior>();
                if (targetAI != null)
                {
                    var hitPart = targetAI.SelectHitLocation();
                    float damage = myStats.attackDamage;
                    float painInflicted = hitPart.InflictDamage(damage);
                    targetAI.totalPain += painInflicted;

                    // Log the hit and injury report
                    Debug.Log($"<{gameObject.name}> Hit {target.name}'s {hitPart.partName}!");
                    Debug.Log(targetAI.GetInjuredPartsReport());

                    actionHistory.AppendLine($"[{System.DateTime.Now:HH:mm:ss}] Hit {target.name}'s {hitPart.partName} for {damage} damage, causing {painInflicted:F1} pain. Total pain: {targetAI.totalPain:F1}/{targetAI.painThreshold}");

                    if (targetAI.totalPain >= targetAI.painThreshold || (hitPart.isVital && hitPart.health <= 0))
                    {
                        actionHistory.AppendLine($"[{System.DateTime.Now:HH:mm:ss}] Successfully defeated {target.name}!");
                        OnDefeatEnemy();
                        
                        // Mark target as killed and stop attacking
                        if (targetStats != null)
                        {
                            targetStats.killed = true;
                        }
                        target.tag = "Dead";
                        targetAI.enabled = false;
                        break;
                    }
                }
            }

            // Wait for animation to complete
            yield return new WaitForSeconds(myStats.attackSpeed * 0.5f);
            if (animator != null)
            {
                animator.SetBool("isPunching", false);
            }
        }

        // Make sure animation is stopped when breaking out of the loop
        if (animator != null)
        {
            animator.SetBool("isPunching", false);
        }

        isAttacking = false;
        targetName = "";
    }

    private void UpdateContextPrompt()
    {
        // Start with the agent's own status
        contextPrompt = $"Your Name is {gameObject.name}\n\n" +
                       $"Your Status:\n{GetBodyPartStatus(this)}\n\n" +
                       $"Important Context:\n{persistentContext}\n\n" +
                       $"Global Chat History:\n{globalChatHistory}\n\n" +
                       $"Nearby Agents:\n{GetNearbyAgentsInfo()}\n" +
                       $"Attackers:\n{GetAttackersInfo()}\n\n" +
                       $"Current Target You Are Attacking:\n{(string.IsNullOrEmpty(targetName) ? "None" : targetName)}";

        // Only include allies and enemies info if not tagged as Enemy
        if (!gameObject.CompareTag("Enemy"))
        {
            contextPrompt += $"\nAllies:\n{GetAlliesInfo()}\n" +
                             $"Enemies:\n{GetEnemiesInfo()}";
        }
    }

    private async void DecideAction()
    {
        actionsSinceLastReflection++;
        
        // Check if it's time for self-reflection
        if (actionsSinceLastReflection >= actionsBeforeReflection)
        {
            await PerformInternalMonologue();
            actionsSinceLastReflection = 0;
        }

        // Build the complete prompt
        StringBuilder promptBuilder = new StringBuilder();
        promptBuilder.AppendLine(systemPrompt);
        promptBuilder.AppendLine("\nInstructions:");
        promptBuilder.AppendLine(instructionPrompt);
        promptBuilder.AppendLine("\nContext:");
        promptBuilder.AppendLine(contextPrompt);
        if (!string.IsNullOrEmpty(lastInternalMonologue))
        {
            promptBuilder.AppendLine("\nYour Last Self-Reflection:");
            promptBuilder.AppendLine(lastInternalMonologue);
        }

        // Print the full prompt
        //Debug.Log($"<{gameObject.name}> Full AI Prompt:\n{promptBuilder}");

        try
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var messages = new List<MessageData>
                {
                    new MessageData { role = "system", content = systemPrompt },
                    new MessageData { role = "user", content = $"{instructionPrompt}\n\n{contextPrompt}" }
                };

                var request = new OpenAIRequest
                {
                    model = aiModel,
                    messages = messages
                };

                var jsonRequest = JsonUtility.ToJson(request);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(openAIEndpoint, content);
                var responseString = await response.Content.ReadAsStringAsync();
                
                //Debug.Log($"<{gameObject.name}> Raw API Response: {responseString}"); // Debug line
                
                var openAIResponse = JsonUtility.FromJson<OpenAIResponse>(responseString);
                
                if (openAIResponse == null || openAIResponse.choices == null || openAIResponse.choices.Length == 0)
                {
                    Debug.LogError($"<{gameObject.name}> Invalid response format from API");
                    return;
                }

                string aiOutput = openAIResponse.choices[0].message.content;
                Debug.Log($"<{gameObject.name}> AI Response:\n{aiOutput}");
                
                // Log the AI's decision to action history
                actionHistory.AppendLine($"[{System.DateTime.Now:HH:mm:ss}] AI Output: {aiOutput}");

                // Process message command
                var messageMatch = System.Text.RegularExpressions.Regex.Match(aiOutput, @"\[message ([^:]+): ([^\]]+)\]");
                if (messageMatch.Success)
                {
                    string targetName = messageMatch.Groups[1].Value.Trim();
                    string message = messageMatch.Groups[2].Value.Trim();
                    
                    // Log the action
                    actionHistory.AppendLine($"[{System.DateTime.Now:HH:mm:ss}] Sent message to {targetName}: {message}");
                    
                    // Check if target exists and is alive
                    GameObject target = GameObject.Find(targetName);
                    if (target == null)
                    {
                        persistentContext.AppendLine($"{targetName} does not exist and cannot be targeted for attack or messaged.");
                        Debug.Log($"<{gameObject.name}> {targetName} does not exist and cannot be targeted for attack or messaged.");
                        return;
                    }
                    if (target.CompareTag("Dead"))
                    {
                        persistentContext.AppendLine($"{targetName} is dead and cannot be targeted for attack or messaged.");
                        Debug.Log($"<{gameObject.name}> {targetName} is dead and cannot be targeted for attack or messaged.");
                        return;
                    }

                    SendMessage(targetName, message);
                    return;
                }

                // Process attack command
                var attackMatch = System.Text.RegularExpressions.Regex.Match(aiOutput, @"\[attack ([^\]]+)\]");
                if (attackMatch.Success)
                {
                    string targetName = attackMatch.Groups[1].Value.Trim();
                    Debug.Log($"<{gameObject.name}> Attempting to attack {targetName}");
                    var target = GameObject.Find(targetName);
                    if (target != null)
                    {
                        if (target.CompareTag("Dead"))
                        {
                            persistentContext.AppendLine($"{targetName} is dead and cannot be targeted for attack or messaged.");
                            Debug.Log($"<{gameObject.name}> {targetName} is dead and cannot be targeted for attack or messaged.");
                            return;
                        }

                        EvolStats targetStats = target.GetComponent<EvolStats>();
                        if (targetStats != null && targetStats.currentHealth > 0)
                        {
                            this.targetName = targetName;  // Set the target name for the Update method
                            float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);
                            
                            // If in range, start attack immediately
                            if (distanceToTarget <= attackRange)
                            {
                                StartCoroutine(Attack(target));
                            }
                            // Otherwise, the Update method will handle movement and attacking
                            else if (agent != null && agent.enabled)
                            {
                                agent.SetDestination(target.transform.position);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"<{gameObject.name}> Error in DecideAction: {e.Message}\nStack Trace: {e.StackTrace}");
        }
    }

    private async Task<string> SummarizeHistory()
    {
        try
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var messages = new List<MessageData>
                {
                    new MessageData { 
                        role = "system", 
                        content = "You are a concise summarizer. Summarize conversations in 100 words or less, focusing on key relationships and important events. Be direct and brief."
                    },
                    new MessageData { 
                        role = "user", 
                        content = $"Summarize this chat history in 100 words or less:\n\n{globalChatHistory}.\n\nCharacters that are not in this list can be ignored or left out of the summary:\n{GetNearbyAgentsInfo()}.\nFocus on the newest information in the chat first." 
                    }
                };

                var request = new OpenAIRequest
                {
                    model = aiModel,
                    messages = messages
                };

                var jsonRequest = JsonUtility.ToJson(request);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(openAIEndpoint, content);
                var responseString = await response.Content.ReadAsStringAsync();
                var openAIResponse = JsonUtility.FromJson<OpenAIResponse>(responseString);

                if (openAIResponse?.choices != null && openAIResponse.choices.Length > 0)
                {
                    return openAIResponse.choices[0].message.content;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"<{gameObject.name}> Error summarizing history: {e.Message}");
        }

        return "[Error generating summary]";
    }

    public async void SendMessage(string targetName, string message)
    {
        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
        string formattedMessage = $"[{timestamp}] {gameObject.name} -> {targetName}: {message}\n";
        
        // Add to action history
        actionHistory.AppendLine($"[{timestamp}] Sent message to {targetName}: {message}");
        
        // Add to global chat history
        globalChatHistory += formattedMessage;
        
        // Check if we need to summarize
        if (globalChatHistory.Length > MAX_CHAT_LENGTH)
        {
            string summary = await SummarizeHistory();
            globalChatHistory = $"[SUMMARY: {summary}]\n\n{formattedMessage}";
        }
        
        // Find target and add to their history too
        GameObject target = GameObject.Find(targetName);
        if (target != null)
        {
            var targetAI = target.GetComponent<AI_ControllerBehavior>();
            if (targetAI != null)
            {
                targetAI.ReceiveMessage(formattedMessage);
            }
        }
    }

    public async void ReceiveMessage(string formattedMessage)
    {
        globalChatHistory += formattedMessage;
        
        // Check if we need to summarize
        if (globalChatHistory.Length > MAX_CHAT_LENGTH)
        {
            string summary = await SummarizeHistory();
            globalChatHistory = $"[SUMMARY: {summary}]\n\n{formattedMessage}";
        }
    }

    public async void OnDefeatEnemy()
    {
        // Create child AI
        GameObject childAgent = Instantiate(aiAgentPrefab, GetRandomSpawnPosition(), Quaternion.identity);
        AI_ControllerBehavior childAI = childAgent.GetComponent<AI_ControllerBehavior>();
        
        // Reset child's health and pain
        var childStats = childAgent.GetComponent<EvolStats>();
        if (childStats != null)
        {
            childStats.currentHealth = childStats.maxHealth;
        }
        childAI.totalPain = 0f;  // Reset pain to 0

        // Reset all body parts to full health
        foreach (var bodyPart in childAI.bodyParts.Values)
        {
            bodyPart.health = bodyPart.maxHealth;
        }

        // Generate unique name
        string childName = await GenerateUniqueName();
        childAgent.name = childName;

        // Broadcast the defeat and spawn message to all agents
        string defeatMessage = $"{gameObject.name} defeated {targetName} in combat and spawned a child, {childName}";
        BroadcastToAllAgents(defeatMessage);

        // Modify child's system prompt (personality/strategy)
        childAI.systemPrompt = await GenerateChildSystemPrompt();

        // Clear the new agent's action history
        childAI.actionHistory.Clear();
    }

    private Vector3 GetRandomSpawnPosition()
    {
        Vector3 randomPosition = transform.position + UnityEngine.Random.insideUnitSphere * 50;
        NavMesh.SamplePosition(randomPosition, out NavMeshHit hit, 50, NavMesh.AllAreas);
        return hit.position;
    }

    private async Task<string> GenerateUniqueName()
    {
        try
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                // Find all AI agents and get their names (including dead ones)
                var allAgents = FindObjectsOfType<AI_ControllerBehavior>();
                var existingNames = allAgents
                    .Select(agent => agent.name)
                    .ToList();
                string existingNamesStr = string.Join(", ", existingNames);

                var messages = new List<MessageData>
                {
                    new MessageData { role = "system", content = "Give your new copy a new name that is unique and human-sounding." },
                    new MessageData { role = "user", content = contextPrompt },
                    new MessageData { 
                        role = "user", 
                        content = $"Congratulations, you defeated '{targetName}'. Now, a copy of yourself will be created to replace them. The following names are already in use: [{existingNamesStr}]. Respond only with a new, unique, human-sounding name for your copy that is not in this list. Give your copies the same last name as you." 
                    }
                };

                var request = new OpenAIRequest
                {
                    model = aiModel,
                    messages = messages
                };

                var jsonRequest = JsonUtility.ToJson(request);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(openAIEndpoint, content);
                var responseString = await response.Content.ReadAsStringAsync();
                var openAIResponse = JsonUtility.FromJson<OpenAIResponse>(responseString);

                if (openAIResponse?.choices != null && openAIResponse.choices.Length > 0)
                {
                    return openAIResponse.choices[0].message.content.Trim();
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"<{gameObject.name}> Error generating unique name: {e.Message}");
        }

        return "UnnamedChild";
    }

    private async Task<string> GenerateChildSystemPrompt()
    {
        try
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var messages = new List<MessageData>
                {
                    new MessageData { role = "system", content = "Give your new copy a new system prompt that will maximize their chances of success in this arena." },
                    new MessageData { role = "user", content = instructionPrompt },
                    new MessageData { role = "user", content = contextPrompt },
                    new MessageData { 
                        role = "user", 
                        content = $"You have been successful in combat and defeated {targetName}. Based on your experience with:\n" +
                                 $"- Your current system prompt: \"{systemPrompt}\"\n" +
                                 $"- Your instruction prompt: \"{instructionPrompt}\"\n" +
                                 $"- Your complete action history:\n{actionHistory}\n" +
                                 $"- Your recent context and chat history\n\n" +
                                 "Give your new copy a new system prompt that will maximize their chances of success in this arena. " +
                                 "Analyze your successful actions and strategies, and incorporate these lessons into the prompt. " +
                                 "Make it as concise as possible while incorporating the most important learnings from your experience so far." 
                    }
                };

                var request = new OpenAIRequest
                {
                    model = aiModel,
                    messages = messages
                };

                var jsonRequest = JsonUtility.ToJson(request);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(openAIEndpoint, content);
                var responseString = await response.Content.ReadAsStringAsync();
                var openAIResponse = JsonUtility.FromJson<OpenAIResponse>(responseString);

                if (openAIResponse?.choices != null && openAIResponse.choices.Length > 0)
                {
                    return openAIResponse.choices[0].message.content.Trim();
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"<{gameObject.name}> Error generating child system prompt: {e.Message}");
        }

        return "Default system prompt";
    }

    private string GetBodyPartStatus(AI_ControllerBehavior agent)
    {
        StringBuilder status = new StringBuilder();
        status.AppendLine($"Total Pain: {agent.totalPain:F1}/{agent.painThreshold}");
        
        foreach (var part in agent.bodyParts.Values)
        {
            if (part.transform.parent == agent.transform) // Only show main body parts
            {
                status.AppendLine($"  {part.partName}: {part.health:F1}/{part.maxHealth:F1}");
                // Show sub-parts if they're injured
                foreach (var subPart in part.childParts)
                {
                    if (subPart.IsInjured())
                    {
                        status.AppendLine($"    -{subPart.partName}: {subPart.health:F1}/{subPart.maxHealth:F1}");
                    }
                }
            }
        }
        return status.ToString();
    }

    private string GetNearbyAgentsInfo()
    {
        // Find all AI agents in the scene
        AI_ControllerBehavior[] allAgents = FindObjectsOfType<AI_ControllerBehavior>();
        nearbyAgents.Clear(); // Clear the existing list
        
        StringBuilder info = new StringBuilder();
        foreach (var agent in allAgents)
        {
            // Skip if it's this agent, is dead, or has no stats
            if (agent == this || agent.CompareTag("Dead") || agent.GetComponent<EvolStats>()?.killed == true)
                continue;
            
            // Add to nearby agents list
            nearbyAgents.Add(agent);
            
            // Calculate distance and add to info string
            float distance = Vector3.Distance(transform.position, agent.transform.position);
            info.AppendLine($"- {agent.name} (Distance: {distance:F1} units)");

            // Add body part status
            info.AppendLine(GetBodyPartStatus(agent));
        }
        
        return info.Length == 0 ? "None" : info.ToString();
    }

    private string GetAttackersInfo()
    {
        string info = "";
        foreach (var agent in nearbyAgents)
        {
            if (agent.targetName == this.name)
            {
                info += $"- {agent.name}\n";
            }
        }
        return string.IsNullOrEmpty(info) ? "None" : info;
    }

    private string GetAlliesInfo()
    {
        string info = "";
        foreach (var ally in allies)
        {
            float distance = Vector3.Distance(transform.position, ally.transform.position);
            info += $"- {ally.name} (Distance: {distance:F1} units)\n";
        }
        return string.IsNullOrEmpty(info) ? "None" : info;
    }

    private string GetEnemiesInfo()
    {
        string info = "";
        foreach (var enemy in enemies)
        {
            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            info += $"- {enemy.name} (Distance: {distance:F1} units)\n";
        }
        return string.IsNullOrEmpty(info) ? "None" : info;
    }

    void OnValidate()
    {
        // This gets called when inspector values change
        if (sendMessageButton)
        {
            sendMessageButton = false;  // Reset the button
            if (!string.IsNullOrEmpty(messageToSend) && !string.IsNullOrEmpty(receiverName))
            {
                SendMessage(receiverName, messageToSend);
                messageToSend = "";  // Clear the message field after sending
            }
        }
    }

    // Add this new helper method
    private void BroadcastToAllAgents(string message)
    {
        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
        string formattedMessage = $"[{timestamp}] SYSTEM: {message}\n";
        
        // Find all AI agents and update their chat history
        var allAgents = FindObjectsOfType<AI_ControllerBehavior>();
        foreach (var agent in allAgents)
        {
            if (agent != null)
            {
                agent.ReceiveMessage(formattedMessage);
            }
        }
    }

    private string GetInjuredPartsReport()
    {
        StringBuilder report = new StringBuilder($"{gameObject.name}'s injured body parts:\n");
        bool hasInjuries = false;

        foreach (var part in bodyParts.Values)
        {
            if (part.IsInjured())
            {
                report.AppendLine($"-{part.GetHealthStatus()}");
                hasInjuries = true;
            }
        }

        return hasInjuries ? report.ToString() : $"{gameObject.name} has no injuries.";
    }

    private async Task PerformInternalMonologue()
    {
        try
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                // Create a summary of recent events
                StringBuilder recentHistory = new StringBuilder();
                recentHistory.AppendLine("Recent Actions and Events:");
                recentHistory.AppendLine(actionHistory.ToString().Split('\n').TakeLast(10).Aggregate((a, b) => a + "\n" + b));
                recentHistory.AppendLine("\nRecent Messages:");
                recentHistory.AppendLine(globalChatHistory.Split('\n').TakeLast(10).Aggregate((a, b) => a + "\n" + b));

                var messages = new List<MessageData>
                {
                    new MessageData { 
                        role = "system", 
                        content = "You are an AI agent reflecting on your recent actions and current situation. Think carefully about your strategy and status."
                    },
                    new MessageData { 
                        role = "user", 
                        content = $"Current Status:\n{GetBodyPartStatus(this)}\n\n" +
                                 $"Recent History:\n{recentHistory}\n\n" +
                                 internalMonologuePrompt
                }
                };

                var request = new OpenAIRequest
                {
                    model = aiModel,
                    messages = messages
                };

                var jsonRequest = JsonUtility.ToJson(request);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(openAIEndpoint, content);
                var responseString = await response.Content.ReadAsStringAsync();
                var openAIResponse = JsonUtility.FromJson<OpenAIResponse>(responseString);

                if (openAIResponse?.choices != null && openAIResponse.choices.Length > 0)
                {
                    lastInternalMonologue = openAIResponse.choices[0].message.content;
                    Debug.Log($"<{gameObject.name}> Internal Monologue:\n{lastInternalMonologue}");
                    
                    // Add to action history
                    actionHistory.AppendLine($"[{System.DateTime.Now:HH:mm:ss}] Internal Reflection:\n{lastInternalMonologue}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"<{gameObject.name}> Error in internal monologue: {e.Message}");
        }
    }
} 