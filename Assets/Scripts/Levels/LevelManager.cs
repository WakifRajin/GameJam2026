using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

namespace GameJam2026
{
    /// <summary>
    /// Manages level progression, objectives, and win/lose conditions
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
        [SerializeField] private InventoryManager inventoryManager;
        [SerializeField] private DayNightCycle dayNightCycle;
        [SerializeField] private SignalTower signalTower;
        
        [Header("Win/Lose Conditions")]
        [SerializeField] private bool failOnPowerDepletion = false;
        [SerializeField] private bool failOnTimeout = true;
        
        private bool levelStarted = false;
        private bool levelCompleted = false;
        private bool levelFailed = false;
        
        // Events
        public event Action OnLevelStarted;
        public event Action OnLevelCompleted;
        public event Action OnLevelFailed;
        public event Action<ObjectiveTracker> OnObjectiveCompleted;
        public event Action<float> OnTimeUpdated; // Time remaining
        
        public bool IsLevelActive => levelStarted && !levelCompleted && !levelFailed;
        public float TimeRemaining => timeRemaining;
        public List<ObjectiveTracker> Objectives => objectiveTrackers;

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
            
            if (inventoryManager == null)
                inventoryManager = FindObjectOfType<InventoryManager>();
            
            if (dayNightCycle == null)
                dayNightCycle = FindObjectOfType<DayNightCycle>();
            
            if (signalTower == null)
                signalTower = FindObjectOfType<SignalTower>();
        }

        private void InitializeObjectives()
        {
            objectiveTrackers.Clear();
            
            foreach (var objective in objectives)
            {
                var tracker = new ObjectiveTracker { objective = objective };
                tracker.OnCompleted += HandleObjectiveCompleted;
                objectiveTrackers.Add(tracker);
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
                if (failOnPowerDepletion)
                    roverAttributes.OnPowerDepleted += () => FailLevel("Power depleted!");
            }
            
            if (inventoryManager != null)
            {
                inventoryManager.OnItemAdded += OnItemCollected;
            }
            
            if (signalTower != null)
            {
                signalTower.OnTowerActivated += OnSignalTowerActivated;
            }
        }

        private void UpdateObjectives()
        {
            foreach (var tracker in objectiveTrackers)
            {
                if (tracker.isCompleted || !tracker.isActive) continue;
                
                switch (tracker.objective.type)
                {
                    case ObjectiveType.CollectItems:
                        UpdateCollectObjective(tracker);
                        break;
                        
                    case ObjectiveType.RestorePower:
                        UpdatePowerObjective(tracker);
                        break;
                        
                    case ObjectiveType.RepairObject:
                        UpdateRepairObjective(tracker);
                        break;
                        
                    case ObjectiveType.SurviveUntil:
                        UpdateSurviveObjective(tracker);
                        break;
                }
            }
        }

        private void UpdateCollectObjective(ObjectiveTracker tracker)
        {
            if (inventoryManager == null) return;
            
            // Count items in inventory
            int count = 0;
            string targetType = tracker.objective.targetResourceType;
            
            foreach (var item in inventoryManager.Inventory)
            {
                if (string.IsNullOrEmpty(targetType) || item.itemType.ToString() == targetType)
                {
                    count++;
                }
            }
            
            tracker.UpdateProgress(count);
        }

        private void UpdatePowerObjective(ObjectiveTracker tracker)
        {
            if (roverAttributes == null) return;
            tracker.UpdateProgress(roverAttributes.CurrentPower);
        }

        private void UpdateRepairObjective(ObjectiveTracker tracker)
        {
            // Check if specific object is repaired
            if (signalTower != null && tracker.objective.targetObject == signalTower.gameObject)
            {
                float progress = signalTower.CurrentState == SignalTower.TowerState.Active ? 1f : 0f;
                tracker.UpdateProgress(progress);
            }
        }

        private void UpdateSurviveObjective(ObjectiveTracker tracker)
        {
            // Update based on time survived
            if (hasTimeLimit)
            {
                float timePassed = timeLimitInSeconds - timeRemaining;
                tracker.UpdateProgress(timePassed);
            }
        }

        private void OnItemCollected(CollectibleItem item)
        {
            Debug.Log($"Item collected: {item.itemName}");
        }

        private void OnSignalTowerActivated()
        {
            Debug.Log("Signal Tower Activated!");
            // This will be caught by the repair objective
        }

        private void HandleObjectiveCompleted(ObjectiveTracker tracker)
        {
            Debug.Log($"✓ Objective Complete: {tracker.objective.objectiveTitle}");
            OnObjectiveCompleted?.Invoke(tracker);
        }

        private void CheckLevelCompletion()
        {
            // Check if all required objectives are completed
            bool allRequired = true;
            foreach (var tracker in objectiveTrackers)
            {
                if (!tracker.objective.isOptional && !tracker.isCompleted)
                {
                    allRequired = false;
                    break;
                }
            }
            
            if (allRequired)
            {
                CompleteLevel();
            }
        }

        private void CheckFailureConditions()
        {
            // Add any additional failure conditions here
        }

        private void CompleteLevel()
        {
            if (levelCompleted) return;
            
            levelCompleted = true;
            Debug.Log($"=== LEVEL COMPLETE! ===");
            
            OnLevelCompleted?.Invoke();
            
            // Stop time or show victory screen
            Time.timeScale = 0f;
        }

        private void FailLevel(string reason)
        {
            if (levelFailed) return;
            
            levelFailed = true;
            Debug.Log($"=== LEVEL FAILED: {reason} ===");
            
            OnLevelFailed?.Invoke();
            
            // Stop time or show failure screen
            Time.timeScale = 0f;
        }

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
            // Load next level (implement based on your scene management)
            UnityEngine.SceneManagement.SceneManager.LoadScene(levelNumber + 1);
        }

        public ObjectiveTracker GetObjective(string title)
        {
            return objectiveTrackers.FirstOrDefault(t => t.objective.objectiveTitle == title);
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

        [ContextMenu("Fail Level")]
        private void DebugFailLevel()
        {
            FailLevel("Debug");
        }

        #endregion
    }
}