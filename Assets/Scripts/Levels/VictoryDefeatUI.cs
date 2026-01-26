using UnityEngine;

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