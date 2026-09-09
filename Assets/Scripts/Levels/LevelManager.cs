using System;
using System.Collections;
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
        [SerializeField] private float timeLimitInSeconds = 300f;
        private float timeRemaining;
        
        [Header("References")]
        [SerializeField] private RoverAttributeManager roverAttributes;
        [SerializeField] private GridInventoryManager gridInventoryManager;
        [SerializeField] private DayNightCycle dayNightCycle;
        [SerializeField] private SignalTower signalTower;
        
        [Header("Win/Lose Conditions")]
        [SerializeField] private bool failOnPowerDepletion = false;
        [SerializeField] private bool failOnTimeout = true;
        
        [Header("Pause Settings")]
        [SerializeField] private bool pauseOnComplete = true;
        [SerializeField] private bool pauseOnFail = true;
        [SerializeField] private float pauseDelay = 1.5f; // Time for animations to play
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool enableTowerObjectiveDebug = true;
        
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
        public event Action<float> OnTimeUpdated;
        
        public bool IsLevelActive => levelStarted && !levelCompleted && !levelFailed;
        public float TimeRemaining => timeRemaining;
        public List<ObjectiveTracker> Objectives => objectiveTrackers;
        public string LevelName => levelName;
        public string LevelDescription => levelDescription;

        private void Start()
        {
            FindReferences();
            InitializeObjectives();
            
            Invoke(nameof(StartLevel), 1f);
        }

        private void FindReferences()
        {
            if (roverAttributes == null)
                roverAttributes = FindObjectOfType<RoverAttributeManager>();
            
            if (gridInventoryManager == null)
                gridInventoryManager = FindObjectOfType<GridInventoryManager>();
            
            if (dayNightCycle == null)
                dayNightCycle = FindObjectOfType<DayNightCycle>();
            
            if (signalTower == null)
                signalTower = FindObjectOfType<SignalTower>();
            
            DebugLog($"References found - Rover: {roverAttributes != null}, Inventory: {gridInventoryManager != null}, Tower: {signalTower != null}");
            
            if (signalTower != null)
            {
                Debug.Log($"[LevelManager] SignalTower found - Current state: {signalTower.CurrentState}");
            }
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
                
                // Add progress tracking for tower objectives
                if (objective.objectiveType == ObjectiveType.ActivateObject || 
                    objective.objectiveType == ObjectiveType.RepairObject)
                {
                    tracker.OnProgressChanged += (t) => 
                    {
                        Debug.LogWarning($"[TOWER OBJECTIVE] Progress changed: {t.objective.objectiveTitle} -> {t.currentProgress}/{t.objective.targetValue}");
                        Debug.LogWarning($"Stack trace:\n{System.Environment.StackTrace}");
                    };
                }
                
                tracker.OnCompleted += HandleObjectiveCompleted;
                objectiveTrackers.Add(tracker);
                
                DebugLog($"Initialized objective: {objective.objectiveTitle} (Type: {objective.objectiveType})");
            }
        }

        public void StartLevel()
        {
            if (levelStarted) return;
            
            levelStarted = true;
            timeRemaining = timeLimitInSeconds;
            
            Debug.Log($"=== {levelName} Started ===");
            Debug.Log(levelDescription);
            
            SubscribeToEvents();
            
            OnLevelStarted?.Invoke();
            
            // Print initial tower state
            if (signalTower != null && enableTowerObjectiveDebug)
            {
                Debug.Log($"[LevelManager] Initial tower state: {signalTower.CurrentState}");
            }
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
            
            CheckFailureConditions();
            UpdateObjectives();
            CheckLevelCompletion();
        }

        private void SubscribeToEvents()
        {
            if (roverAttributes != null && failOnPowerDepletion)
            {
                roverAttributes.OnPowerDepleted += HandlePowerDepleted;
            }
            
            if (gridInventoryManager != null)
            {
                gridInventoryManager.OnItemCollected += HandleItemCollected;
                gridInventoryManager.OnResourceChanged += HandleResourceChanged;
                DebugLog("✓ Subscribed to GridInventoryManager events");
            }
            else
            {
                Debug.LogError("LevelManager: GridInventoryManager not found!");
            }
            
            if (signalTower != null)
            {
                signalTower.OnTowerActivated += HandleTowerActivated;
                signalTower.OnTowerRepaired += HandleTowerRepaired;
                DebugLog("✓ Subscribed to SignalTower events (OnTowerActivated, OnTowerRepaired)");
            }
            else
            {
                Debug.LogError("LevelManager: SignalTower not found!");
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
            
            if (gridInventoryManager != null)
            {
                gridInventoryManager.OnItemCollected -= HandleItemCollected;
                gridInventoryManager.OnResourceChanged -= HandleResourceChanged;
            }
            
            if (signalTower != null)
            {
                signalTower.OnTowerActivated -= HandleTowerActivated;
                signalTower.OnTowerRepaired -= HandleTowerRepaired;
            }
        }

        #region Event Handlers

        private void HandleItemCollected(CollectibleItem item, int quantity)
        {
            if (item == null) return;
            
            DebugLog($"Item collected: {item.itemName} (Type: {item.itemType})");
            
            // IMPORTANT: Check tower state when item is collected
            if (signalTower != null && enableTowerObjectiveDebug)
            {
                Debug.Log($"[LevelManager] Tower state after item collection: {signalTower.CurrentState}");
            }
            
            // Track by item type
            string itemTypeKey = item.itemType.ToString();
            if (!collectedItemCounts.ContainsKey(itemTypeKey))
            {
                collectedItemCounts[itemTypeKey] = 0;
            }
            collectedItemCounts[itemTypeKey] += quantity;
            
            // Track by resource type if available
            string resourceKey = GetResourceTypeFromItem(item);
            if (!string.IsNullOrEmpty(resourceKey) && resourceKey != itemTypeKey)
            {
                if (!collectedItemCounts.ContainsKey(resourceKey))
                {
                    collectedItemCounts[resourceKey] = 0;
                }
                collectedItemCounts[resourceKey] += quantity;
            }
            
            DebugLog($"Tracked: {itemTypeKey} count = {collectedItemCounts[itemTypeKey]}");
            
            // Update ONLY collection objectives
            UpdateCollectionObjectives();
        }

        private void HandleResourceChanged(string resourceType, float amount)
        {
            DebugLog($"Resource changed: {resourceType} = {amount}");
            
            // Update ONLY collection objectives
            UpdateCollectionObjectives();
        }

        private void HandleTowerRepaired()
        {
            Debug.Log("=== TOWER REPAIRED EVENT RECEIVED ===");
            
            if (signalTower != null)
            {
                Debug.Log($"Tower state after repair: {signalTower.CurrentState}");
            }
            
            // Don't complete objectives on repair - only on activation!
        }

        private void HandleTowerActivated()
        {
            Debug.Log("=== TOWER ACTIVATED EVENT RECEIVED ===");
            
            if (signalTower != null)
            {
                Debug.Log($"Tower state: {signalTower.CurrentState}");
                Debug.Log($"Is fully activated: {signalTower.IsFullyActivated}");
            }
            
            // Complete ALL tower-related objectives (no target object check since we can't assign scene objects)
            foreach (var tracker in objectiveTrackers)
            {
                if (tracker.isCompleted) continue;
                
                var objective = tracker.objective;
                
                // Check if this objective is about activating/repairing tower
                if (objective.objectiveType == ObjectiveType.ActivateObject ||
                    objective.objectiveType == ObjectiveType.RepairObject)
                {
                    // Since there's only ONE tower in the level, complete any tower objective
                    if (signalTower != null && signalTower.IsFullyActivated)
                    {
                        Debug.Log($"[LevelManager] ✓ Completing tower objective: {objective.objectiveTitle}");
                        tracker.UpdateProgress(objective.targetValue);
                    }
                    else
                    {
                        Debug.LogWarning($"[LevelManager] Tower objective NOT completed - tower not fully activated!");
                        Debug.LogWarning($"Tower state: {(signalTower != null ? signalTower.CurrentState.ToString() : "NULL")}");
                    }
                }
            }
        }

        private void HandlePowerDepleted()
        {
            FailLevel("Power depleted!");
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
                        
                    // CRITICAL: DO NOT UPDATE TOWER OBJECTIVES HERE
                    // Tower objectives are ONLY updated via HandleTowerActivated event
                    case ObjectiveType.RepairObject:
                    case ObjectiveType.ActivateObject:
                        // BLOCKED - these are event-driven only
                        break;
                }
            }
        }

        private void UpdateCollectionObjectives()
        {
            foreach (var tracker in objectiveTrackers)
            {
                if (tracker.isCompleted) continue;
                
                if (tracker.objective.objectiveType == ObjectiveType.CollectItems)
                {
                    UpdateCollectItemsObjective(tracker);
                }
            }
        }

        private void UpdateCollectItemsObjective(ObjectiveTracker tracker)
        {
            var objective = tracker.objective;
            
            if (!string.IsNullOrEmpty(objective.targetResourceType))
            {
                // Objectives may name an item type ("Material") or a resource ("Materials"),
                // so try the literal key before falling back to the canonical one.
                string canonical = ResourceIds.Normalize(objective.targetResourceType);

                if (collectedItemCounts.TryGetValue(objective.targetResourceType, out int count) ||
                    collectedItemCounts.TryGetValue(canonical, out count))
                {
                    tracker.UpdateProgress(count);
                    DebugLog($"Objective '{objective.objectiveTitle}': {count}/{objective.targetValue}");
                }
                else if (gridInventoryManager != null)
                {
                    // Units, not resource worth: one 15-power cell is 1 towards "collect 5
                    // power cells", not 15. Lifetime, so spending on an upgrade cannot
                    // un-complete an objective.
                    tracker.UpdateProgress(gridInventoryManager.GetLifetimeUnits(canonical));
                }
            }
            else
            {
                // Count all items
                if (gridInventoryManager != null)
                {
                    tracker.UpdateProgress(gridInventoryManager.UsedSlots);
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
            if (roverAttributes == null) return;
            
            if (tracker.objective.targetObject != null)
            {
                float distance = Vector3.Distance(
                    roverAttributes.transform.position,
                    tracker.objective.targetObject.transform.position
                );
                
                if (distance <= tracker.objective.targetValue)
                {
                    tracker.UpdateProgress(tracker.objective.targetValue);
                }
            }
            else if (tracker.objective.targetLocation != null)
            {
                float distance = Vector3.Distance(
                    roverAttributes.transform.position,
                    tracker.objective.targetLocation.position
                );
                
                if (distance <= tracker.objective.targetValue)
                {
                    tracker.UpdateProgress(tracker.objective.targetValue);
                }
            }
        }

        private string GetResourceTypeFromItem(CollectibleItem item)
        {
            // Canonical key, so an objective spelled "Materials" matches an item that calls
            // itself "Material". ResourceIds owns every alias.
            return ResourceIds.Of(item);
        }

        #endregion

        #region Level Completion

        private void HandleObjectiveCompleted(ObjectiveTracker tracker)
        {
            Debug.Log($"✓ Objective Completed: {tracker.objective.objectiveTitle}");
            Debug.Log($"Progress: {tracker.currentProgress}/{tracker.objective.targetValue}");
            
            // Extra logging for tower objectives
            if (tracker.objective.objectiveType == ObjectiveType.ActivateObject ||
                tracker.objective.objectiveType == ObjectiveType.RepairObject)
            {
                Debug.Log($"[TOWER OBJECTIVE COMPLETED] {tracker.objective.objectiveTitle}");
                if (signalTower != null)
                {
                    Debug.Log($"Tower state when objective completed: {signalTower.CurrentState}");
                }
            }
            
            OnObjectiveCompleted?.Invoke(tracker);
        }

        private void CheckLevelCompletion()
        {
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
            
            // Pause after delay to allow victory animation to play
            if (pauseOnComplete)
            {
                StartCoroutine(PauseGameAfterDelay(pauseDelay));
            }
        }

        private void CheckFailureConditions()
        {
            // Add custom failure conditions here if needed
        }

        private void FailLevel(string reason)
        {
            if (levelFailed) return;
            
            levelFailed = true;
            Debug.Log($"✗✗✗ {levelName} FAILED: {reason} ✗✗✗");
            
            OnLevelFailed?.Invoke();
            
            // Pause after delay to allow defeat animation to play
            if (pauseOnFail)
            {
                StartCoroutine(PauseGameAfterDelay(pauseDelay));
            }
        }

        private IEnumerator PauseGameAfterDelay(float delay)
        {
            // Use unscaled time so this coroutine works even if timeScale changes
            float elapsedTime = 0f;
            
            while (elapsedTime < delay)
            {
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }
            
            Time.timeScale = 0f;
            Debug.Log($"Game paused after {delay}s animation delay");
        }

        #endregion

        #region Public Methods

        public void RestartLevel()
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
            );
        }

        public void LoadNextLevel()
        {
            Time.timeScale = 1f;
            
            int nextSceneIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex + 1;
            int totalScenes = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
            
            if (nextSceneIndex < totalScenes)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneIndex);
            }
            else
            {
                Debug.Log("No next level available. This was the last level!");
            }
        }

        public ObjectiveTracker GetObjective(string title)
        {
            return objectiveTrackers.Find(t => t.objective.objectiveTitle == title);
        }

        public void ForceCompleteLevel()
        {
            CompleteLevel();
        }

        public void ForceFailLevel(string reason = "Debug")
        {
            FailLevel(reason);
        }

        #endregion

        private void DebugLog(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[LevelManager] {message}");
            }
        }

        #region Debug Methods

        [ContextMenu("Complete All Objectives")]
        private void DebugCompleteAllObjectives()
        {
            foreach (var tracker in objectiveTrackers)
            {
                if (!tracker.isCompleted)
                {
                    tracker.UpdateProgress(tracker.objective.targetValue);
                }
            }
        }

        [ContextMenu("Print Objective Status")]
        private void DebugPrintStatus()
        {
            Debug.Log("=== OBJECTIVE STATUS ===");
            foreach (var tracker in objectiveTrackers)
            {
                string status = tracker.isCompleted ? "✓ COMPLETE" : "IN PROGRESS";
                Debug.Log($"{tracker.objective.objectiveTitle}: {tracker.currentProgress}/{tracker.objective.targetValue} " +
                         $"({tracker.ProgressPercentage * 100:F0}%) - {status}");
                
                if (tracker.objective.objectiveType == ObjectiveType.ActivateObject ||
                    tracker.objective.objectiveType == ObjectiveType.RepairObject)
                {
                    Debug.Log($"  -> Tower objective, target: {(tracker.objective.targetObject != null ? tracker.objective.targetObject.name : "NULL")}");
                }
            }
            
            Debug.Log("=== COLLECTED ITEMS ===");
            foreach (var kvp in collectedItemCounts)
            {
                Debug.Log($"{kvp.Key}: {kvp.Value}");
            }
            
            if (signalTower != null)
            {
                Debug.Log($"=== TOWER STATUS ===");
                Debug.Log($"State: {signalTower.CurrentState}");
                Debug.Log($"Is Fully Activated: {signalTower.IsFullyActivated}");
            }
        }

        [ContextMenu("Reset Level")]
        private void DebugResetLevel()
        {
            RestartLevel();
        }

        [ContextMenu("Complete Level")]
        private void DebugCompleteLevel()
        {
            ForceCompleteLevel();
        }

        [ContextMenu("Fail Level")]
        private void DebugFailLevel()
        {
            ForceFailLevel("Debug");
        }

        #endregion
    }
}