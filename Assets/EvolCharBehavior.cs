using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TMPro;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;

public class EvolCharBehavior : MonoBehaviour
{
    public GameObject closestEnemy;

    public EvolStats evolStats;
    public Animator animator;
    public NavMeshAgent agent;

    public GameObject clonableDuplicate;
    public SkinnedMeshRenderer skinnedMeshRenderer;

    public float attackRange;
    public bool isAttacking = false;
    public bool isMoving = false;

    public string tagToAttack = "Enemy";
    public TextMeshPro nameTag;
    public float generalEvolutionStdDeviation = 0.1f;
    public bool geneticInstability = false;
    public bool similarColor = false;

    // OpenAI Configuration
    private readonly string openAIEndpoint = "https://api.openai.com/v1/chat/completions";
    [SerializeField, Tooltip("Your OpenAI API Key")]
    private string apiKey;
    [SerializeField, Range(0.5f, 20f)]
    private float aiThinkingInterval = 2f;
    
    public Queue<string> messageHistory = new Queue<string>();
    private const int MAX_MESSAGE_HISTORY = 10;
    public TextMeshPro messageDisplay;
    [SerializeField]
    private float messageDisplayDuration = 5f;
    
    // Static list to track defeated characters
    private static List<string> defeatedCharacters = new List<string>();

    [System.Serializable]
    private class OpenAIRequest
    {
        public string model = "gpt-4o-mini";
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

    [SerializeField, TextArea(3, 10)]
    private string systemPrompt = "You are a fighting character. Respond with exactly ONE action in brackets. You should prioritize communication over combat, but be ready to fight if necessary. Your responses should reflect your character's personality and experiences.";

    [SerializeField, TextArea(3, 10)]
    private string instructionPrompt = "You may communicate or attack other characters arbitrarily. Respond with ONE action in brackets:\n[message characterName: Your message] - to communicate\n[attack characterName] - to attack";

    [SerializeField, TextArea(3, 10)]
    private string contextPrompt = "Consider your character's history, relationships, and current situation when making decisions.";

    void Start()
    {
        evolStats = GetComponent<EvolStats>();
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();

        agent.speed = evolStats.movementSpeed;
        attackRange = evolStats.attackRange;

        // Initialize message display
        if (messageDisplay == null)
        {
            GameObject messageObj = new GameObject("MessageDisplay");
            messageObj.transform.SetParent(transform);
            messageObj.transform.localPosition = Vector3.up * 3f; // Position above character
            messageDisplay = messageObj.AddComponent<TextMeshPro>();
            messageDisplay.alignment = TextAlignmentOptions.Center;
            messageDisplay.fontSize = 4;
        }

        StartCoroutine(AIDecisionLoop());
        StartCoroutine(RecalculateClosestEnemyLoop());
    }

    IEnumerator AIDecisionLoop()
    {
        while (true)
        {
            if (evolStats.currentHealth > 0)
            {
                yield return new WaitForSeconds(aiThinkingInterval);
                _ = MakeAIDecision();
            }
            yield return null;
        }
    }

    IEnumerator RecalculateClosestEnemyLoop()
    {
        while (true)
        {
            if (evolStats.currentHealth > 0 && !isAttacking)
            {
                GameObject newClosestEnemy = FindClosestEnemy();
                if (newClosestEnemy != closestEnemy)
                {
                    Debug.Log($"<{gameObject.name}> Switching attention to {newClosestEnemy?.name ?? "no target"}");
                    closestEnemy = newClosestEnemy;
                }
            }
            yield return new WaitForSeconds(2f);
        }
    }

    async Task MakeAIDecision()
    {
        string prompt = GeneratePrompt();
        Debug.Log($"<{gameObject.name}> AI Prompt:\n{prompt}");
        string action = await GetAIResponse(prompt);
        Debug.Log($"<{gameObject.name}> AI Output: {action}");
        ProcessAIAction(action);
    }

    string GeneratePrompt()
    {
        StringBuilder contextBuilder = new StringBuilder();
        contextBuilder.AppendLine($"You are {gameObject.name}, a character in a fighting arena. Your stats are:");
        contextBuilder.AppendLine($"Health: {evolStats.currentHealth}/{evolStats.maxHealth}");
        contextBuilder.AppendLine($"Attack Damage: {evolStats.attackDamage}");
        contextBuilder.AppendLine($"Movement Speed: {evolStats.movementSpeed}");
        
        if (defeatedCharacters.Count > 0)
        {
            contextBuilder.AppendLine("\nDefeated characters:");
            foreach (string defeat in defeatedCharacters)
            {
                contextBuilder.AppendLine($"- {defeat}");
            }
        }

        // List all available characters
        contextBuilder.AppendLine("\nAvailable characters to interact with:");
        GameObject[] allCharacters = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject character in allCharacters)
        {
            if (character != gameObject && !character.CompareTag("Dead"))
            {
                EvolStats charStats = character.GetComponent<EvolStats>();
                if (charStats != null && charStats.currentHealth > 0)
                {
                    float distance = Vector3.Distance(transform.position, character.transform.position);
                    contextBuilder.AppendLine($"- {character.name} (Health: {charStats.currentHealth}/{charStats.maxHealth}, Distance: {distance:F1})");
                }
            }
        }
        
        if (closestEnemy != null)
        {
            var enemyStats = closestEnemy.GetComponent<EvolStats>();
            float distance = Vector3.Distance(transform.position, closestEnemy.transform.position);
            contextBuilder.AppendLine($"\nNearest enemy is {closestEnemy.name} at distance {distance:F1}:");
            contextBuilder.AppendLine($"Enemy Health: {enemyStats.currentHealth}/{enemyStats.maxHealth}");
        }

        contextBuilder.AppendLine("\nRecent messages:");
        foreach (string msg in messageHistory)
        {
            contextBuilder.AppendLine(msg);
        }

        contextBuilder.AppendLine("\n" + contextPrompt);

        StringBuilder prompt = new StringBuilder();
        prompt.AppendLine(contextBuilder.ToString());
        prompt.AppendLine("\n" + instructionPrompt);

        return prompt.ToString();
    }

    async Task<string> GetAIResponse(string prompt)
    {
        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var messages = new List<MessageData>
            {
                new MessageData { role = "system", content = systemPrompt }
            };

            // Add message history to provide context
            foreach (string msg in messageHistory)
            {
                // Convert message history format to chat format
                if (msg.Contains(" to ") && msg.Contains(": "))
                {
                    string[] parts = msg.Split(new[] { " to ", ": " }, System.StringSplitOptions.None);
                    if (parts.Length >= 3)
                    {
                        string speaker = parts[0].Trim('<', '>');
                        string messageContent = parts[2];
                        
                        // If this character was the speaker, mark as assistant, otherwise as user
                        string role = speaker == gameObject.name ? "assistant" : "user";
                        messages.Add(new MessageData { role = role, content = messageContent });
                    }
                }
            }

            // Add current prompt
            messages.Add(new MessageData { role = "user", content = prompt });

            var request = new OpenAIRequest
            {
                messages = messages
            };

            var jsonRequest = JsonUtility.ToJson(request);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(openAIEndpoint, content);
            var responseString = await response.Content.ReadAsStringAsync();
            var openAIResponse = JsonUtility.FromJson<OpenAIResponse>(responseString);

            return openAIResponse.choices[0].message.content;
        }
    }

    void ProcessAIAction(string action)
    {
        // Parse message command first (prioritize communication)
        var messageMatch = Regex.Match(action, @"\[message ([^:]+): ([^\]]+)\]");
        if (messageMatch.Success)
        {
            string targetName = messageMatch.Groups[1].Value;
            string message = messageMatch.Groups[2].Value;
            
            // Find the target character
            GameObject target = GameObject.Find(targetName);
            if (target != null && target.CompareTag("Enemy") && !target.CompareTag("Dead"))
            {
                EvolStats targetStats = target.GetComponent<EvolStats>();
                if (targetStats != null && targetStats.currentHealth > 0)
                {
                    DisplayMessage(message);
                    string formattedMessage = $"<{gameObject.name}> to {targetName}: {message}";
                    Debug.Log(formattedMessage);
                    
                    // Add message to both characters' history
                    AddToMessageHistory(formattedMessage);
                    EvolCharBehavior targetBehavior = target.GetComponent<EvolCharBehavior>();
                    if (targetBehavior != null)
                    {
                        targetBehavior.AddToMessageHistory(formattedMessage);
                    }
                }
            }
            return;
        }

        // Parse attack command
        var attackMatch = Regex.Match(action, @"\[attack ([^\]]+)\]");
        if (attackMatch.Success)
        {
            string targetName = attackMatch.Groups[1].Value;
            Debug.Log($"<{gameObject.name}> Attempting to attack {targetName}");
            var target = GameObject.Find(targetName);
            if (target != null && target.CompareTag("Enemy") && !target.CompareTag("Dead"))
            {
                EvolStats targetStats = target.GetComponent<EvolStats>();
                if (targetStats != null && targetStats.currentHealth > 0)
                {
                    closestEnemy = target;
                    float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);
                    
                    // Always move towards target when attacking
                    if (agent.enabled && target.GetComponent<NavMeshAgent>().enabled)
                    {
                        agent.SetDestination(target.transform.position);
                        Debug.Log($"<{gameObject.name}> Moving to attack {targetName}");
                    }
                    
                    // If in range, start the attack
                    if (distanceToTarget < attackRange)
                    {
                        StartCoroutine(Attack());
                    }
                }
            }
            return;
        }
    }

    void DisplayMessage(string message)
    {
        if (messageDisplay != null)
        {
            messageDisplay.text = $"{gameObject.name}: {message}";
            StartCoroutine(ClearMessageAfterDelay());
        }
    }

    IEnumerator ClearMessageAfterDelay()
    {
        yield return new WaitForSeconds(messageDisplayDuration);
        if (messageDisplay != null)
        {
            messageDisplay.text = "";
        }
    }

    void AddToMessageHistory(string message)
    {
        messageHistory.Enqueue(message);
        if (messageHistory.Count > MAX_MESSAGE_HISTORY)
        {
            messageHistory.Dequeue();
        }
    }

    // Modify the Update method to remove automatic combat
    void Update()
    {
        if (evolStats.currentHealth <= 0)
        {
            return;
        }

        // Only update movement if we're not attacking
        if (!isAttacking && agent.enabled)
        {
            agent.speed = evolStats.movementSpeed;
            
            // If we have a target and we're in range, start attacking
            if (closestEnemy != null && !closestEnemy.CompareTag("Dead"))
            {
                float distanceToTarget = Vector3.Distance(transform.position, closestEnemy.transform.position);
                if (distanceToTarget <= attackRange)
                {
                    StartCoroutine(Attack());
                }
            }
        }
    }

    void MoveTowardsEnemy()
    {
        closestEnemy = FindClosestEnemy();
        if (closestEnemy == null || closestEnemy.GetComponent<EvolStats>().currentHealth <= 0 || evolStats.currentHealth <= 0)
        {
            return;
        }
        if (GetComponent<NavMeshAgent>().enabled && closestEnemy.GetComponent<NavMeshAgent>().enabled)
        {
            agent.SetDestination(closestEnemy.transform.position);
        }
    }

    GameObject FindClosestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(tagToAttack);
        GameObject closest = null;
        float distance = Mathf.Infinity;
        Vector3 position = transform.position;
        
        foreach (GameObject enemy in enemies)
        {
            // Skip if enemy is tagged as Dead
            if (enemy.CompareTag("Dead"))
                continue;

            EvolStats enemyEvolStats = enemy.GetComponent<EvolStats>();
            if (enemy != gameObject && enemyEvolStats != null && enemyEvolStats.currentHealth > 0)
            {
                // Check if the faction of the enemy is not the same as this character's faction
                string enemyFaction = enemyEvolStats.faction;
                string thisFaction = evolStats.faction;

                if (thisFaction != enemyFaction || string.IsNullOrEmpty(thisFaction) || string.IsNullOrEmpty(enemyFaction))
                {
                    Vector3 diff = enemy.transform.position - position;
                    float curDistance = diff.sqrMagnitude;
                    if (curDistance < distance)
                    {
                        closest = enemy;
                        distance = curDistance;
                    }
                }
            }
        }
        return closest;
    }

    IEnumerator Attack()
    {
        // Prevent multiple attack coroutines from running at the same time
        if (isAttacking)
        {
            yield break;
        }
        isAttacking = true;

        while (closestEnemy != null && evolStats.currentHealth > 0 && !closestEnemy.CompareTag("Dead"))
        {
            float distanceToTarget = Vector3.Distance(transform.position, closestEnemy.transform.position);
            
            // If we're too far, move towards target
            if (distanceToTarget > attackRange)
            {
                animator.SetBool("isPunching", false);
                if (agent.enabled && closestEnemy.GetComponent<NavMeshAgent>().enabled)
                {
                    agent.SetDestination(closestEnemy.transform.position);
                }
                yield return null;
                continue;
            }

            // We're in range, stop moving and attack
            agent.SetDestination(transform.position);
            transform.LookAt(closestEnemy.transform);

            // Set animation speed to match attack speed
            animator.SetFloat("punchingSpeed", 1f / evolStats.attackSpeed);
            animator.SetBool("isPunching", true);

            // Wait for the animation to reach its "hit" point (approximately halfway through)
            yield return new WaitForSeconds(evolStats.attackSpeed * 0.5f);

            EvolStats enemyEvolStats = closestEnemy.GetComponent<EvolStats>();
            if (!enemyEvolStats.killed)
            {
                enemyEvolStats.currentHealth -= evolStats.attackDamage;
                Debug.Log($"<{gameObject.name}> Hit {closestEnemy.name} for {evolStats.attackDamage} damage! {closestEnemy.name}'s health: {enemyEvolStats.currentHealth}/{enemyEvolStats.maxHealth}");
            }

            if (enemyEvolStats.currentHealth <= 0 && !enemyEvolStats.killed)
            {
                enemyEvolStats.killed = true;
                string defeatMessage = $"{closestEnemy.name} was defeated by {gameObject.name}";
                defeatedCharacters.Add(defeatMessage);
                Debug.Log($"<{gameObject.name}> Defeated {closestEnemy.name}!");

                closestEnemy.GetComponent<Animator>().SetBool("Death", true);
                closestEnemy.GetComponent<NavMeshAgent>().enabled = false;
                closestEnemy.AddComponent<DeleteAfterSeconds>().seconds = 30;

                closestEnemy.GetComponent<EvolCharBehavior>().enabled = false;
                closestEnemy.tag = "Dead";

                SpawnClone();
                break;
            }

            // Wait for the animation to complete
            yield return new WaitForSeconds(evolStats.attackSpeed * 0.5f);
            animator.SetBool("isPunching", false);
        }

        isAttacking = false;
        // Try to find a new enemy when attack sequence ends
        closestEnemy = FindClosestEnemy();
    }

    async Task<string> GetConversationSummary()
    {
        if (messageHistory.Count == 0)
            return "";

        StringBuilder summaryPrompt = new StringBuilder();
        summaryPrompt.AppendLine("Please summarize the following conversation in a concise way that captures the key points and relationships. Summary should be one paragraph:");
        summaryPrompt.AppendLine("\nConversation:");
        foreach (string msg in messageHistory)
        {
            summaryPrompt.AppendLine(msg);
        }

        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var request = new OpenAIRequest
            {
                messages = new List<MessageData>
                {
                    new MessageData { role = "system", content = "You are a conversation summarizer. Provide concise, relevant summaries." },
                    new MessageData { role = "user", content = summaryPrompt.ToString() }
                }
            };

            var jsonRequest = JsonUtility.ToJson(request);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(openAIEndpoint, content);
            var responseString = await response.Content.ReadAsStringAsync();
            var openAIResponse = JsonUtility.FromJson<OpenAIResponse>(responseString);

            return openAIResponse.choices[0].message.content;
        }
    }

    async void SpawnClone()
    {
        // Spawn somewhere within 50 units of the parent
        Vector3 randomPosition = transform.position + Random.insideUnitSphere * 50;
        NavMesh.SamplePosition(randomPosition, out NavMeshHit hit, 50, 1);

        // Instantiate the clonableDuplicate instead of the gameObject itself
        GameObject clone = Instantiate(clonableDuplicate, hit.position, Quaternion.identity);

        // Apply the parent's color to the clone
        Color parentColor = skinnedMeshRenderer.material.color;

        // Find "beta_surface" SkinnedMeshRenderer in the clone's children
        SkinnedMeshRenderer cloneRenderer = null;
        SkinnedMeshRenderer[] renderers = clone.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer renderer in renderers)
        {
            if (renderer.name == "Beta_Surface")
            {
                cloneRenderer = renderer;
                break;
            }
        }

        if (cloneRenderer != null)
        {
            if (similarColor)
            {
                parentColor.r += Random.Range(-0.1f, 0.1f);
                parentColor.g += Random.Range(-0.1f, 0.1f);
                parentColor.b += Random.Range(-0.1f, 0.1f);
                parentColor.r = Mathf.Clamp01(parentColor.r);
                parentColor.g = Mathf.Clamp01(parentColor.g);
                parentColor.b = Mathf.Clamp01(parentColor.b);
            }

            cloneRenderer.material.color = parentColor;
        }

        EvolStats cloneStats = clone.GetComponent<EvolStats>();
        EvolCharBehavior cloneBehavior = clone.GetComponent<EvolCharBehavior>();
        
        // Transfer and summarize message history
        if (messageHistory.Count > 0)
        {
            string conversationSummary = await GetConversationSummary();
            if (!string.IsNullOrEmpty(conversationSummary))
            {
                string summaryMsg = $"<System> Inherited Memory Summary: {conversationSummary}";
                cloneBehavior.messageHistory.Enqueue(summaryMsg);
                Debug.Log($"<{clone.name}> {summaryMsg}");
            }
        }

        // Apply the parent's Clonable Duplicate to the clone
        cloneBehavior.clonableDuplicate = clonableDuplicate;
        clone.tag = "Enemy";

        UpdateCloneName(clone);

        // Remove the "Delete After Seconds" component if it exists on the spawned child
        if (clone.GetComponent<DeleteAfterSeconds>() != null)
        {
            Destroy(clone.GetComponent<DeleteAfterSeconds>());
        }

        // Apply Gaussian distribution to stats
        cloneStats.attackDamage = evolStats.attackDamage * (1 + RandomGaussian(0, generalEvolutionStdDeviation));
        if (cloneStats.attackDamage <= 0)
        {
            cloneStats.attackDamage = 1f;
        }
        cloneStats.maxHealth = evolStats.maxHealth * (1 + RandomGaussian(0, generalEvolutionStdDeviation));
        if (cloneStats.maxHealth <= 0)
        {
            cloneStats.maxHealth = 1f;
        }
        cloneStats.currentHealth = cloneStats.maxHealth;
        cloneStats.movementSpeed = evolStats.movementSpeed + RandomGaussian(0, generalEvolutionStdDeviation);

        if (geneticInstability)
        {
            cloneBehavior.generalEvolutionStdDeviation = generalEvolutionStdDeviation + RandomGaussian(0, generalEvolutionStdDeviation);
            if (cloneBehavior.generalEvolutionStdDeviation <= 0)
            {
                cloneBehavior.generalEvolutionStdDeviation = generalEvolutionStdDeviation;
            }
            cloneBehavior.geneticInstability = geneticInstability;
        }

        clone.GetComponent<NavMeshAgent>().speed = cloneStats.movementSpeed;
        cloneBehavior.similarColor = similarColor;
        cloneStats.charName = evolStats.charName;
    }

    float RandomGaussian(float mean, float stdDev)
    {
        float u1 = Random.value;
        float u2 = Random.value;
        float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
        return mean + stdDev * randStdNormal;
    }

    void UpdateCloneName(GameObject clone)
    {
        string parentName = gameObject.name;
        int genIndex = parentName.IndexOf("Gen");
        if (genIndex != -1)
        {
            int generationNumber = 0;
            if (int.TryParse(parentName.Substring(genIndex + 4), out generationNumber))
            {
                generationNumber++;
                string updatedName = parentName.Substring(0, genIndex) + "Gen " + generationNumber;
                clone.name = updatedName;
            }
            else
            {
                // Default behavior in case parsing fails
                clone.name = parentName + " - Gen 2";
            }
        }
        else
        {
            // If the parent doesn't have a generation number, set it to Gen 2 for the clone
            clone.name = parentName + " - Gen 2";
        }

        // Update the name tag of the clone
        TextMeshPro cloneNameTag = clone.GetComponentInChildren<TextMeshPro>();
        if (cloneNameTag != null)
        {
            cloneNameTag.text = clone.name;
        }
    }

    async void SpawnChild()
    {
        // Find valid spawn position
        Vector3 randomPosition = transform.position + Random.insideUnitSphere * 50;
        NavMesh.SamplePosition(randomPosition, out NavMeshHit hit, 50, 1);

        // Spawn and setup child
        GameObject child = Instantiate(clonableDuplicate, hit.position, Quaternion.identity);
        EvolCharBehavior childBehavior = child.GetComponent<EvolCharBehavior>();
        EvolStats childStats = child.GetComponent<EvolStats>();

        // Inherit properties with slight mutations
        childStats.attackDamage = evolStats.attackDamage * Random.Range(0.9f, 1.1f);
        childStats.maxHealth = evolStats.maxHealth * Random.Range(0.9f, 1.1f);
        childStats.currentHealth = childStats.maxHealth;
        childStats.movementSpeed = evolStats.movementSpeed * Random.Range(0.9f, 1.1f);

        // Inherit prompts with potential for mutation
        if (Random.value > 0.7f) // 30% chance to modify each prompt
        {
            childBehavior.systemPrompt = await GetModifiedPrompt(systemPrompt);
            childBehavior.instructionPrompt = await GetModifiedPrompt(instructionPrompt);
            childBehavior.contextPrompt = await GetModifiedPrompt(contextPrompt);
        }
        else
        {
            childBehavior.systemPrompt = systemPrompt;
            childBehavior.instructionPrompt = instructionPrompt;
            childBehavior.contextPrompt = contextPrompt;
        }

        // Set unique name
        string baseName = gameObject.name.Split(new[] { " Gen " }, System.StringSplitOptions.None)[0];
        int generation = GetGeneration() + 1;
        child.name = $"{baseName} Gen {generation}";
        
        if (child.GetComponentInChildren<TextMeshPro>() is TextMeshPro nameTag)
        {
            nameTag.text = child.name;
        }
    }

    private int GetGeneration()
    {
        string[] nameParts = gameObject.name.Split(new[] { " Gen " }, System.StringSplitOptions.None);
        if (nameParts.Length > 1 && int.TryParse(nameParts[1], out int generation))
        {
            return generation;
        }
        return 1;
    }

    private async Task<string> GetModifiedPrompt(string originalPrompt)
    {
        string modificationRequest = $"Modify this AI character prompt while maintaining its core purpose but adding a unique personality twist:\n\n{originalPrompt}";
        
        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var request = new OpenAIRequest
            {
                messages = new List<MessageData>
                {
                    new MessageData { role = "system", content = "You are a prompt engineer. Modify prompts to create unique but coherent variations." },
                    new MessageData { role = "user", content = modificationRequest }
                }
            };

            var jsonRequest = JsonUtility.ToJson(request);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(openAIEndpoint, content);
            var responseString = await response.Content.ReadAsStringAsync();
            var openAIResponse = JsonUtility.FromJson<OpenAIResponse>(responseString);

            return openAIResponse.choices[0].message.content;
        }
    }
}