using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace GameJam2026
{
    /// <summary>
    /// Manages UI for level objectives, timer, and completion
    /// </summary>
    public class LevelUIManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LevelManager levelManager;
        
        [Header("Objective UI")]
        [SerializeField] private Transform objectiveListContainer;
        [SerializeField] private GameObject objectivePrefab;
        
        [Header("Timer UI")]
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private Image timerFillBar;
        [SerializeField] private Color normalTimeColor = Color.green;
        [SerializeField] private Color warningTimeColor = Color.yellow;
        [SerializeField] private Color criticalTimeColor = Color.red;
        
        [Header("Level Info")]
        [SerializeField] private TextMeshProUGUI levelTitleText;
        [SerializeField] private TextMeshProUGUI levelDescriptionText;
        
        [Header("Completion UI")]
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private GameObject defeatPanel;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button nextLevelButton;
        
        private Dictionary<ObjectiveTracker, GameObject> objectiveUIElements = new Dictionary<ObjectiveTracker, GameObject>();
        // Prefab's own progress colour, so an objective that falls back below target can be
        // restored to its normal look instead of being stuck green.
        private Dictionary<ObjectiveTracker, Color> objectiveBaseColors = new Dictionary<ObjectiveTracker, Color>();
        private float totalTime = 300f;

        private void Start()
        {
            if (levelManager == null)
            {
                levelManager = FindObjectOfType<LevelManager>();
                if (levelManager == null)
                {
                    Debug.LogError("LevelUIManager: Could not find LevelManager!");
                    return;
                }
            }
            
            SubscribeToEvents();
            
            // Wait a frame for LevelManager to initialize, then setup UI
            Invoke(nameof(InitializeUI), 0.1f);
            
            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (defeatPanel != null) defeatPanel.SetActive(false);
            
            if (restartButton != null)
                restartButton.onClick.AddListener(() => levelManager?.RestartLevel());
            
            if (nextLevelButton != null)
            {
                nextLevelButton.onClick.AddListener(() => levelManager?.LoadNextLevel());

                // LoadNextLevel silently does nothing past the last scene, so hide the button
                // instead of offering a dead end.
                if (levelManager != null && !levelManager.HasNextLevel)
                {
                    nextLevelButton.gameObject.SetActive(false);
                }
            }
        }

        private void SubscribeToEvents()
        {
            levelManager.OnLevelStarted += OnLevelStarted;
            levelManager.OnLevelCompleted += OnLevelCompleted;
            levelManager.OnLevelFailed += OnLevelFailed;
            levelManager.OnObjectiveCompleted += OnObjectiveCompleted;
            levelManager.OnTimeUpdated += UpdateTimer;
        }

        private void InitializeUI()
        {
            DisplayLevelInfo();
            CreateObjectiveList();
            Debug.Log("LevelUIManager initialized");
        }

        private void DisplayLevelInfo()
        {
            // Read from LevelManager rather than hardcoding - this used to say
            // "Level 1: Distress Signal" in every level, including Level 2.
            if (levelManager == null) return;

            if (levelTitleText != null)
            {
                string name = levelManager.LevelName;
                levelTitleText.text = string.IsNullOrWhiteSpace(name)
                    ? $"Level {levelManager.LevelNumber}"
                    : name;
            }

            if (levelDescriptionText != null)
            {
                levelDescriptionText.text = levelManager.LevelDescription;
            }
        }

        private void CreateObjectiveList()
        {
            if (objectiveListContainer == null)
            {
                Debug.LogError("LevelUIManager: Objective List Container is not assigned!");
                return;
            }
            
            if (objectivePrefab == null)
            {
                Debug.LogError("LevelUIManager: Objective Prefab is not assigned!");
                return;
            }

            // Clear existing objectives
            foreach (Transform child in objectiveListContainer)
            {
                Destroy(child.gameObject);
            }
            objectiveUIElements.Clear();

            var objectives = levelManager.Objectives;
            Debug.Log($"Creating UI for {objectives.Count} objectives");

            foreach (var tracker in objectives)
            {
                CreateObjectiveUI(tracker);
            }
        }

        private void CreateObjectiveUI(ObjectiveTracker tracker)
        {
            GameObject objUI = Instantiate(objectivePrefab, objectiveListContainer);
            objUI.name = $"Objective_{tracker.objective.objectiveTitle}";
            
            // Find child elements by name
            Transform titleTransform = objUI.transform.Find("Title");
            Transform progressTransform = objUI.transform.Find("Progress");
            Transform checkmarkTransform = objUI.transform.Find("Checkmark");
            
            if (titleTransform == null)
            {
                Debug.LogError($"Could not find 'Title' child in ObjectiveItem prefab!");
                return;
            }
            
            TextMeshProUGUI titleText = titleTransform.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI progressText = progressTransform?.GetComponent<TextMeshProUGUI>();
            GameObject checkmark = checkmarkTransform?.gameObject;
            
            if (titleText != null)
            {
                titleText.text = tracker.objective.objectiveTitle;
                Debug.Log($"Set objective title: {tracker.objective.objectiveTitle}");
            }
            
            if (progressText != null)
            {
                progressText.text = $"0/{tracker.objective.targetValue:F0}";
            }
            
            if (checkmark != null)
            {
                checkmark.SetActive(false);
            }
            
            objectiveUIElements[tracker] = objUI;
            if (progressText != null) objectiveBaseColors[tracker] = progressText.color;

            // Subscribe to progress changes
            tracker.OnProgressChanged += (t) => UpdateObjectiveUI(t);
            tracker.OnUncompleted += (t) => UpdateObjectiveUI(t);
        }

        private void UpdateObjectiveUI(ObjectiveTracker tracker)
        {
            if (!objectiveUIElements.ContainsKey(tracker))
            {
                Debug.LogWarning($"Objective UI not found for: {tracker.objective.objectiveTitle}");
                return;
            }
            
            GameObject objUI = objectiveUIElements[tracker];
            
            Transform progressTransform = objUI.transform.Find("Progress");
            Transform checkmarkTransform = objUI.transform.Find("Checkmark");
            
            TextMeshProUGUI progressText = progressTransform?.GetComponent<TextMeshProUGUI>();
            GameObject checkmark = checkmarkTransform?.gameObject;
            
            if (progressText != null)
            {
                progressText.text = $"{tracker.currentProgress:F0}/{tracker.objective.targetValue:F0}";

                // Drive the colour from state in BOTH directions - a collection objective can
                // fall back below target when the player spends what they gathered.
                Color baseColor = objectiveBaseColors.TryGetValue(tracker, out Color c) ? c : Color.white;
                progressText.color = tracker.isCompleted ? Color.green : baseColor;
            }

            if (checkmark != null)
            {
                checkmark.SetActive(tracker.isCompleted);
            }
        }

        private void UpdateTimer(float timeRemaining)
        {
            if (timerText != null)
            {
                int minutes = Mathf.FloorToInt(timeRemaining / 60f);
                int seconds = Mathf.FloorToInt(timeRemaining % 60f);
                timerText.text = $"{minutes:00}:{seconds:00}";
                
                // Color based on time remaining
                if (timeRemaining < 30f)
                    timerText.color = criticalTimeColor;
                else if (timeRemaining < 60f)
                    timerText.color = warningTimeColor;
                else
                    timerText.color = normalTimeColor;
            }
            
            if (timerFillBar != null)
            {
                timerFillBar.fillAmount = timeRemaining / totalTime;
                timerFillBar.color = timerText != null ? timerText.color : normalTimeColor;
            }
        }

        private void OnLevelStarted()
        {
            Debug.Log("LevelUIManager: Level Started");
            totalTime = levelManager.TimeRemaining;
        }

        private void OnLevelCompleted()
        {
            Debug.Log("LevelUIManager: Level Completed");
            if (victoryPanel != null)
            {
                victoryPanel.SetActive(true);
            }
        }

        private void OnLevelFailed()
        {
            Debug.Log("LevelUIManager: Level Failed");
            if (defeatPanel != null)
            {
                defeatPanel.SetActive(true);
            }
        }

        private void OnObjectiveCompleted(ObjectiveTracker tracker)
        {
            Debug.Log($"LevelUIManager: Objective Completed - {tracker.objective.objectiveTitle}");
            UpdateObjectiveUI(tracker);
        }

        private void OnDestroy()
        {
            if (levelManager != null)
            {
                levelManager.OnLevelStarted -= OnLevelStarted;
                levelManager.OnLevelCompleted -= OnLevelCompleted;
                levelManager.OnLevelFailed -= OnLevelFailed;
                levelManager.OnObjectiveCompleted -= OnObjectiveCompleted;
                levelManager.OnTimeUpdated -= UpdateTimer;
            }
        }

        #region Debug
        
        [ContextMenu("Refresh Objectives UI")]
        private void DebugRefreshUI()
        {
            CreateObjectiveList();
        }

        [ContextMenu("Print Objective Count")]
        private void DebugPrintObjectives()
        {
            if (levelManager != null)
            {
                Debug.Log($"LevelManager has {levelManager.Objectives.Count} objectives");
                foreach (var obj in levelManager.Objectives)
                {
                    Debug.Log($"- {obj.objective.objectiveTitle}");
                }
            }
        }
        
        #endregion
    }
}