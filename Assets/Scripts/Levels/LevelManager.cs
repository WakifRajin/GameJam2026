using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// Manages level objectives, win/lose conditions, and progression
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [Header("Level Info")]
        [SerializeField] private string levelName = "Level 1";
        [SerializeField] private int levelNumber = 1;
        [TextArea(3, 5)]
        [SerializeField] private string levelDescription = "Restore power and send a distress signal";
        
        [Header("Objectives")]
        [SerializeField] private List<GameObjective> objectives = new List<GameObjective>();
        private List<ObjectiveTracker> objectiveTrackers = new List<ObjectiveTracker>();
        
        [Header("Time Limit")]
        [SerializeField] private bool hasTimeLimit = true;
        [SerializeField] private float timeLimitInSeconds = 300f; // 5 minutes
        private float timeRemaining;
        
        [Header("References")]
        [SerializeField] private RoverAttributeManager roverAttributes;
        [SerializeField] private GridInventoryManager gridInventoryManager; // UPDATED: Use new inventory
        [SerializeField] private DayNightCycle dayNightCycle;
        [SerializeField] private SignalTower signalTower;
        
        [Header("Win/Lose Conditions")]
        [SerializeField] private bool failOnPowerDepletion = false;
        [SerializeField] private bool failOnTimeout = true;
        
        private bool levelStarted = false;
        private bool levelCompleted = false;
        private bool levelFailed = false;
        
        // Track collected items by type
        private Dictionary<string, int> collectedItemCounts = new Dictionary<string, int>();
        
        // Events
        public event Action OnLevelStarted;
        public event Action OnLevelCompleted;
        public event Action OnLevelFailed;
        public event Action<ObjectiveTracker> OnObjectiveCompleted;
        public event Action<float> OnTimeUpdated; // Time remaining
        
        public bool IsLevelActive => levelStarted && !levelCompleted && !levelFailed;
        public float TimeRemaining => timeRemaining;
        public List<ObjectiveTracker> Objectives => objectiveTrackers;
        public string LevelName => levelName;
        public string LevelDescription => levelDescription;

        private void Start()
        {
            FindReferences();
            InitializeObjectives();
            
            // Start level after a short delay
            Invoke(nameof(StartLevel), 1f);
        }

        private void FindReferences()
        {
            if (roverAttributes == null)
                roverAttributes = FindObjectOfType<RoverAttributeManager>();
            
            // UPDATED: Find GridInventoryManager instead of old InventoryManager
            if (gridInventoryManager == null)
                gridInventoryManager = FindObjectOfType<GridInventoryManager>();
            
            if (dayNightCycle == null)
                dayNightCycle = FindObjectOfType<DayNightCycle>();
            
            if (signalTower == null)
                signalTower = FindObjectOfType<SignalTower>();
        }

        private void InitializeObjectives()
        {
            objectiveTrackers.Clear();
            collectedItemCounts.Clear();
            
            foreach (var objective in objectives)
            {
                if (objective == null)
                {
                    Debug.LogWarning("LevelManager: Null objective found, skipping");
                    continue;
                }
                
                var tracker = new ObjectiveTracker { objective = objective };
                tracker.OnCompleted += HandleObjectiveCompleted;
                objectiveTrackers.Add(tracker);
                
                Debug.Log($"Initialized objective: {objective.objectiveTitle} (Type: {objective.objectiveType})");
            }
        }

        public void StartLevel()
        {
            if (levelStarted) return;
            
            levelStarted = true;
            timeRemaining = timeLimitInSeconds;
            
            Debug.Log($"=== {levelName} Started ===");
            Debug.Log(levelDescription);
            
            // Subscribe to events
            SubscribeToEvents();
            
            OnLevelStarted?.Invoke();
        }

        private void Update()
        {
            if (!IsLevelActive) return;
            
            // Update timer
            if (hasTimeLimit)
            {
                timeRemaining -= Time.deltaTime;
                OnTimeUpdated?.Invoke(timeRemaining);
                
                if (timeRemaining <= 0 && failOnTimeout)
                {
                    FailLevel("Time's up!");
                }
            }
            
            // Check for failure conditions
            CheckFailureConditions();
            
            // Update objectives
            UpdateObjectives();
            
            // Check for level completion
            CheckLevelCompletion();
        }

        private void SubscribeToEvents()
        {
            if (roverAttributes != null)
            {
                roverAttributes.OnPowerDepleted += HandlePowerDepleted;
            }
            
            // UPDATED: Subscribe to GridInventoryManager events
            if (gridInventoryManager != null)
            {
                gridInventoryManager.OnItemAddedToGrid += HandleItemCollected;
                gridInventoryManager.OnResourceChanged += HandleResourceChanged;
                Debug.Log("✓ Subscribed to GridInventoryManager events");
            }
            else
            {
                Debug.LogError("LevelManager: GridInventoryManager not found! Objectives won't update.");
            }
            
            if (signalTower != null)
            {
                signalTower.OnTowerActivated += HandleTowerActivated;
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void UnsubscribeFromEvents()
        {
            if (roverAttributes != null)
            {
                roverAttributes.OnPowerDepleted -= HandlePowerDepleted;
            }
            
            // UPDATED: Unsubscribe from GridInventoryManager
            if (gridInventoryManager != null)
            {
                gridInventoryManager.OnItemAddedToGrid -= HandleItemCollected;
                gridInventoryManager.OnResourceChanged -= HandleResourceChanged;
            }
            
            if (signalTower != null)
            {
                signalTower.OnTowerActivated -= HandleTowerActivated;
            }
        }

        #region Event Handlers

        // UPDATED: Handle item collection from GridInventoryManager
        private void HandleItemCollected(CollectibleItem item, int x, int y)
        {
            if (item == null) return;
            
            Debug.Log($"LevelManager: Item collected - {item.itemName} (Type: {item.itemType})");
            
            // Track by item type
            string itemTypeKey = item.itemType.ToString();
            if (!collectedItemCounts.ContainsKey(itemTypeKey))
            {
                collectedItemCounts[itemTypeKey] = 0;
            }
            collectedItemCounts[itemTypeKey]++;
            
            // Track by resource type (for specific objectives)
            string resourceKey = item.resourceType;
            if (!string.IsNullOrEmpty(resourceKey))
            {
                if (!collectedItemCounts.ContainsKey(resourceKey))
                {
                    collectedItemCounts[resourceKey] = 0;
                }
                collectedItemCounts[resourceKey]++;
            }
            
            // Update relevant objectives
            UpdateCollectionObjectives();
        }

        private void HandleResourceChanged(string resourceType, float amount)
        {
            Debug.Log($"LevelManager: Resource changed - {resourceType}: {amount}");
            UpdateCollectionObjectives();
        }

        private void UpdateCollectionObjectives()
        {
            foreach (var tracker in objectiveTrackers)
            {
                if (tracker.isCompleted) continue;
                
                var objective = tracker.objective;
                
                switch (objective.objectiveType)
                {
                    case ObjectiveType.CollectItems:
                        UpdateCollectItemsObjective(tracker);
                        break;
                }
            }
        }

        private void UpdateCollectItemsObjective(ObjectiveTracker tracker)
        {
            var objective = tracker.objective;
            
            // Count items by resource type
            if (!string.IsNullOrEmpty(objective.targetResourceType))
            {
                // Check by resource type (e.g., "Material", "PowerCell")
                if (collectedItemCounts.ContainsKey(objective.targetResourceType))
                {
                    int count = collectedItemCounts[objective.targetResourceType];
                    tracker.UpdateProgress(count);
                    Debug.Log($"Objective '{objective.objectiveTitle}': {count}/{objective.targetValue}");
                }
                else
                {
                    // Try checking resources in inventory
                    if (gridInventoryManager != null)
                    {
                        float resourceAmount = gridInventoryManager.GetResource(objective.targetResourceType);
                        tracker.UpdateProgress(resourceAmount);
                        Debug.Log($"Objective '{objective.objectiveTitle}' (Resource): {resourceAmount}/{objective.targetValue}");
                    }
                }
            }
            else
            {
                // Count all items in inventory
                if (gridInventoryManager != null)
                {
                    tracker.UpdateProgress(gridInventoryManager.UsedSlots);
                }
            }
        }

        private void HandleTowerActivated()
        {
            Debug.Log("LevelManager: Tower activated!");
            
            foreach (var tracker in objectiveTrackers)
            {
                if (tracker.objective.objectiveType == ObjectiveType.RepairObject ||
                    tracker.objective.objectiveType == ObjectiveType.ActivateObject)
                {
                    tracker.UpdateProgress(tracker.objective.targetValue);
                }
            }
        }

        private void HandlePowerDepleted()
        {
            if (failOnPowerDepletion)
            {
                FailLevel("Power depleted!");
            }
        }

        #endregion

        #region Objective Updates

        private void UpdateObjectives()
        {
            foreach (var tracker in objectiveTrackers)
            {
                if (tracker.isCompleted) continue;
                
                var objective = tracker.objective;
                
                switch (objective.objectiveType)
                {
                    case ObjectiveType.Survival:
                        UpdateSurvivalObjective(tracker);
                        break;
                        
                    case ObjectiveType.ReachLocation:
                        UpdateReachLocationObjective(tracker);
                        break;
                        
                    case ObjectiveType.RepairObject:
                        UpdateRepairObjective(tracker);
                        break;
                }
            }
        }

        private void UpdateSurvivalObjective(ObjectiveTracker tracker)
        {
            if (hasTimeLimit)
            {
                float survived = timeLimitInSeconds - timeRemaining;
                tracker.UpdateProgress(survived);
            }
        }

        private void UpdateReachLocationObjective(ObjectiveTracker tracker)
        {
            if (roverAttributes == null || tracker.objective.targetLocation == null) return;
            
            float distance = Vector3.Distance(
                roverAttributes.transform.position,
                tracker.objective.targetLocation.position
            );
            
            if (distance <= tracker.objective.targetValue)
            {
                tracker.UpdateProgress(tracker.objective.targetValue);
            }
        }

        private void UpdateRepairObjective(ObjectiveTracker tracker)
        {
            if (signalTower != null && tracker.objective.targetObject == signalTower.gameObject)
            {
                if (signalTower.IsFullyActivated)
                {
                    tracker.UpdateProgress(tracker.objective.targetValue);
                }
            }
        }

        #endregion

        #region Level Completion

        private void HandleObjectiveCompleted(ObjectiveTracker tracker)
        {
            Debug.Log($"✓ Objective Completed: {tracker.objective.objectiveTitle}");
            OnObjectiveCompleted?.Invoke(tracker);
        }

        private void CheckLevelCompletion()
        {
            // Check if all required objectives are complete
            bool allRequiredComplete = true;
            
            foreach (var tracker in objectiveTrackers)
            {
                if (!tracker.objective.isOptional && !tracker.isCompleted)
                {
                    allRequiredComplete = false;
                    break;
                }
            }
            
            if (allRequiredComplete)
            {
                CompleteLevel();
            }
        }

        private void CompleteLevel()
        {
            if (levelCompleted) return;
            
            levelCompleted = true;
            Debug.Log($"✓✓✓ {levelName} COMPLETED! ✓✓✓");
            
            OnLevelCompleted?.Invoke();
        }

        private void CheckFailureConditions()
        {
            // Add any custom failure conditions here
        }

        private void FailLevel(string reason)
        {
            if (levelFailed) return;
            
            levelFailed = true;
            Debug.Log($"✗✗✗ {levelName} FAILED: {reason} ✗✗✗");
            
            OnLevelFailed?.Invoke();
        }

        #endregion

        #region Public Methods

        public void RestartLevel()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            );
        }

        public void LoadNextLevel()
        {
            int nextSceneIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex + 1;
            if (nextSceneIndex < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneIndex);
            }
            else
            {
                Debug.Log("No next level available");
            }
        }

        #endregion

        #region Debug

        [ContextMenu("Complete All Objectives")]
        private void DebugCompleteAll()
        {
            foreach (var tracker in objectiveTrackers)
            {
                tracker.UpdateProgress(tracker.objective.targetValue);
            }
        }

        [ContextMenu("Print Objective Status")]
        private void DebugPrintStatus()
        {
            Debug.Log("=== OBJECTIVE STATUS ===");
            foreach (var tracker in objectiveTrackers)
            {
                Debug.Log($"{tracker.objective.objectiveTitle}: {tracker.currentProgress}/{tracker.objective.targetValue} " +
                         $"({tracker.ProgressPercentage * 100:F0}%) - {(tracker.isCompleted ? "COMPLETE" : "IN PROGRESS")}");
            }
            
            Debug.Log("=== COLLECTED ITEMS ===");
            foreach (var kvp in collectedItemCounts)
            {
                Debug.Log($"{kvp.Key}: {kvp.Value}");
            }
        }

        #endregion
    }
}