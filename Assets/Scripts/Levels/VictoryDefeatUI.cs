using UnityEngine;
using UnityEngine.UI;

namespace GameJam2026
{
    /// <summary>
    /// Handles victory and defeat panel animations and buttons
    /// </summary>
    public class VictoryDefeatUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private GameObject defeatPanel;
        
        [Header("References")]
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private Animator victoryAnimator;
        [SerializeField] private Animator defeatAnimator;
        
        [Header("Buttons")]
        [Tooltip("These were never connected in the Inspector, so the end-of-level panels were dead. Wired in Start now.")]
        [SerializeField] private Button victoryRestartButton;
        [SerializeField] private Button victoryNextLevelButton;
        [SerializeField] private Button defeatRestartButton;
        [SerializeField] private Button defeatMainMenuButton;

        [Header("Settings")]
        [SerializeField] private string victoryTrigger = "Show";
        [SerializeField] private string defeatTrigger = "Show";
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        
        private void Start()
        {
            if (levelManager == null)
                levelManager = FindObjectOfType<LevelManager>();
            
            // Get animators if not assigned
            if (victoryAnimator == null && victoryPanel != null)
                victoryAnimator = victoryPanel.GetComponent<Animator>();
            
            if (defeatAnimator == null && defeatPanel != null)
                defeatAnimator = defeatPanel.GetComponent<Animator>();
            
            // Set animators to use unscaled time
            if (victoryAnimator != null)
            {
                victoryAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }
            
            if (defeatAnimator != null)
            {
                defeatAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }
            
            // Hide panels initially
            if (victoryPanel != null)
                victoryPanel.SetActive(false);
            
            if (defeatPanel != null)
                defeatPanel.SetActive(false);
            
            WireButtons();

            // Subscribe to events
            if (levelManager != null)
            {
                levelManager.OnLevelCompleted += ShowVictoryPanel;
                levelManager.OnLevelFailed += ShowDefeatPanel;
            }
        }

        private void OnDestroy()
        {
            if (levelManager != null)
            {
                levelManager.OnLevelCompleted -= ShowVictoryPanel;
                levelManager.OnLevelFailed -= ShowDefeatPanel;
            }
        }

        private void ShowVictoryPanel()
        {
            if (victoryPanel != null)
            {
                victoryPanel.SetActive(true);
                
                if (victoryAnimator != null && !string.IsNullOrEmpty(victoryTrigger))
                {
                    victoryAnimator.SetTrigger(victoryTrigger);
                }
            }
        }

        private void ShowDefeatPanel()
        {
            if (defeatPanel != null)
            {
                defeatPanel.SetActive(true);
                
                if (defeatAnimator != null && !string.IsNullOrEmpty(defeatTrigger))
                {
                    defeatAnimator.SetTrigger(defeatTrigger);
                }
            }
        }

        private void WireButtons()
        {
            if (victoryRestartButton != null)
            {
                victoryRestartButton.onClick.RemoveListener(OnRestartClicked);
                victoryRestartButton.onClick.AddListener(OnRestartClicked);
            }

            if (defeatRestartButton != null)
            {
                defeatRestartButton.onClick.RemoveListener(OnRestartClicked);
                defeatRestartButton.onClick.AddListener(OnRestartClicked);
            }

            if (defeatMainMenuButton != null)
            {
                defeatMainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
                defeatMainMenuButton.onClick.AddListener(OnMainMenuClicked);
            }

            if (victoryNextLevelButton != null)
            {
                // Nothing to advance to on the final level - hide rather than offer a dead end.
                bool hasNext = levelManager != null && levelManager.HasNextLevel;
                victoryNextLevelButton.gameObject.SetActive(hasNext);

                if (hasNext)
                {
                    victoryNextLevelButton.onClick.RemoveListener(OnNextLevelClicked);
                    victoryNextLevelButton.onClick.AddListener(OnNextLevelClicked);
                }
            }
        }

        // Button callbacks
        public void OnRestartClicked()
        {
            if (levelManager != null)
            {
                levelManager.RestartLevel();
            }
        }

        public void OnNextLevelClicked()
        {
            if (levelManager != null)
            {
                levelManager.LoadNextLevel();
            }
        }

        public void OnMainMenuClicked()
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
        }
        
        public void OnQuitClicked()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
    }
}