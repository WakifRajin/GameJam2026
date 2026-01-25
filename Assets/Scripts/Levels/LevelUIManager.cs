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

        private void Start()
        {
            if (levelManager == null)
            {
                levelManager = FindObjectOfType<LevelManager>();
            }
            
            if (levelManager != null)
            {
                SubscribeToEvents();
                InitializeUI();
            }
            
            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (defeatPanel != null) defeatPanel.SetActive(false);
            
            if (restartButton != null)
                restartButton.onClick.AddListener(() => levelManager?.RestartLevel());
            
            if (nextLevelButton != null)
                nextLevelButton.onClick.AddListener(() => levelManager?.LoadNextLevel());
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
            CreateObjectiveList();
        }

        private void CreateObjectiveList()
        {
            if (objectiveListContainer == null || objectivePrefab == null) return;
            
            foreach (var tracker in levelManager.Objectives)
            {
                GameObject objUI = Instantiate(objectivePrefab, objectiveListContainer);
                
                // Setup UI elements
                TextMeshProUGUI titleText = objUI.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
                TextMeshProUGUI progressText = objUI.transform.Find("Progress")?.GetComponent<TextMeshProUGUI>();
                Image checkmark = objUI.transform.Find("Checkmark")?.GetComponent<Image>();
                
                if (titleText != null)
                {
                    titleText.text = tracker.objective.objectiveTitle;
                }
                
                if (checkmark != null)
                {
                    checkmark.enabled = false;
                }
                
                objectiveUIElements[tracker] = objUI;
                
                // Subscribe to progress changes
                tracker.OnProgressChanged += (t) => UpdateObjectiveUI(t);
            }
        }

        private void UpdateObjectiveUI(ObjectiveTracker tracker)
        {
            if (!objectiveUIElements.ContainsKey(tracker)) return;
            
            GameObject objUI = objectiveUIElements[tracker];
            TextMeshProUGUI progressText = objUI.transform.Find("Progress")?.GetComponent<TextMeshProUGUI>();
            Image checkmark = objUI.transform.Find("Checkmark")?.GetComponent<Image>();
            
            if (progressText != null)
            {
                progressText.text = $"{tracker.currentProgress:F0} / {tracker.objective.targetValue:F0}";
            }
            
            if (checkmark != null && tracker.isCompleted)
            {
                checkmark.enabled = true;
                checkmark.color = Color.green;
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
                timerFillBar.fillAmount = timeRemaining / 300f; // Assuming 5 min default
                timerFillBar.color = timerText != null ? timerText.color : normalTimeColor;
            }
        }

        private void OnLevelStarted()
        {
            Debug.Log("Level UI: Level Started");
        }

        private void OnLevelCompleted()
        {
            if (victoryPanel != null)
            {
                victoryPanel.SetActive(true);
            }
        }

        private void OnLevelFailed()
        {
            if (defeatPanel != null)
            {
                defeatPanel.SetActive(true);
            }
        }

        private void OnObjectiveCompleted(ObjectiveTracker tracker)
        {
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
    }
}