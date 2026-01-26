using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;

namespace GameJam2026
{
    /// <summary>
    /// Manages pause menu with proper animation support
    /// </summary>
    public class PauseMenuManager : MonoBehaviour
    {
        [Header("Pause Key")]
        [SerializeField] private Key pauseKey = Key.Escape;
        
        [Header("Menu Panels")]
        [SerializeField] private GameObject pauseMenuPanel;
        [SerializeField] private GameObject settingsPanel;
        
        [Header("Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button quitButton;
        
        [Header("Animation")]
        [SerializeField] private Animator pauseMenuAnimator;
        [SerializeField] private string showTrigger = "Show";
        [SerializeField] private string hideTrigger = "Hide";
        [SerializeField] private bool useAnimations = true;
        [SerializeField] private float animationDelay = 0.3f; // Time for hide animation
        
        [Header("Settings")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private bool useSceneName = true;
        [SerializeField] private int mainMenuBuildIndex = 0;
        
        [Header("Audio")]
        [SerializeField] private AudioClip buttonClickSound;
        
        private bool isPaused = false;
        private bool isAnimating = false;
        private AudioSource audioSource;
        private InventoryUI inventoryUI;
        private CanvasGroup canvasGroup;
        
        public bool IsPaused => isPaused;

        private void Start()
        {
            // Get audio source
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null && buttonClickSound != null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            
            // Get inventory UI reference
            inventoryUI = FindObjectOfType<InventoryUI>();
            
            // Get or add canvas group for fading
            if (pauseMenuPanel != null)
            {
                canvasGroup = pauseMenuPanel.GetComponent<CanvasGroup>();
                if (canvasGroup == null && !useAnimations)
                {
                    canvasGroup = pauseMenuPanel.AddComponent<CanvasGroup>();
                }
            }
            
            // Get animator if not assigned
            if (pauseMenuAnimator == null && pauseMenuPanel != null)
            {
                pauseMenuAnimator = pauseMenuPanel.GetComponent<Animator>();
            }
            
            // CRITICAL: Set animator to unscaled time
            if (pauseMenuAnimator != null)
            {
                pauseMenuAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
                Debug.Log("Pause menu animator set to Unscaled Time");
            }
            
            // Setup button listeners
            if (resumeButton != null)
                resumeButton.onClick.AddListener(OnResume);
            
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettings);
            
            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(OnMainMenu);
            
            if (quitButton != null)
                quitButton.onClick.AddListener(OnQuit);
            
            // Hide pause menu initially
            if (pauseMenuPanel != null)
                pauseMenuPanel.SetActive(false);
            
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
            
            Debug.Log("Pause Menu initialized");
        }

        private void Update()
        {
            // Don't allow pausing during animations
            if (isAnimating) return;
            
            // Check for pause key press
            if (Keyboard.current != null && Keyboard.current[pauseKey].wasPressedThisFrame)
            {
                // Don't pause if inventory is open
                if (inventoryUI != null && inventoryUI.IsOpen)
                {
                    return;
                }
                
                if (isPaused)
                {
                    OnResume();
                }
                else
                {
                    OnPause();
                }
            }
        }

        public void OnPause()
        {
            if (isPaused || isAnimating) return;
            
            // Don't pause if inventory is open
            if (inventoryUI != null && inventoryUI.IsOpen)
            {
                return;
            }
            
            isPaused = true;
            
            // DON'T pause time immediately if using animations
            if (!useAnimations)
            {
                Time.timeScale = 0f;
            }
            
            if (pauseMenuPanel != null)
            {
                pauseMenuPanel.SetActive(true);
            }
            
            // Play show animation
            if (useAnimations && pauseMenuAnimator != null && !string.IsNullOrEmpty(showTrigger))
            {
                pauseMenuAnimator.SetTrigger(showTrigger);
                StartCoroutine(PauseAfterAnimation());
            }
            else if (!useAnimations && canvasGroup != null)
            {
                StartCoroutine(FadeIn());
            }
            else
            {
                // No animation, pause immediately
                Time.timeScale = 0f;
            }
            
            // Show and unlock cursor
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            
            Debug.Log("Game paused");
        }

        private IEnumerator PauseAfterAnimation()
        {
            isAnimating = true;
            
            // Wait for animation using unscaled time
            float elapsed = 0f;
            while (elapsed < animationDelay)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            
            // Now pause the game
            Time.timeScale = 0f;
            isAnimating = false;
        }

        public void OnResume()
        {
            PlayButtonSound();
            
            if (!isPaused || isAnimating) return;
            
            // Play hide animation
            if (useAnimations && pauseMenuAnimator != null && !string.IsNullOrEmpty(hideTrigger))
            {
                pauseMenuAnimator.SetTrigger(hideTrigger);
                StartCoroutine(ResumeAfterAnimation());
            }
            else if (!useAnimations && canvasGroup != null)
            {
                StartCoroutine(FadeOutAndResume());
            }
            else
            {
                // No animation, resume immediately
                ResumeGame();
            }
        }

        private IEnumerator ResumeAfterAnimation()
        {
            isAnimating = true;
            
            // Unpause time so animation can play
            Time.timeScale = 1f;
            
            // Wait for animation
            yield return new WaitForSecondsRealtime(animationDelay);
            
            ResumeGame();
        }

        private void ResumeGame()
        {
            isPaused = false;
            isAnimating = false;
            Time.timeScale = 1f;
            
            if (pauseMenuPanel != null)
                pauseMenuPanel.SetActive(false);
            
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
            
            // Hide and lock cursor (for gameplay)
            #if !UNITY_EDITOR
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            #endif
            
            Debug.Log("Game resumed");
        }

        public void OnSettings()
        {
            PlayButtonSound();
            Debug.Log("Opening settings from pause menu");
            
            if (pauseMenuPanel != null)
                pauseMenuPanel.SetActive(false);
            
            if (settingsPanel != null)
                settingsPanel.SetActive(true);
        }

        public void OnBackToPauseMenu()
        {
            PlayButtonSound();
            Debug.Log("Back to pause menu");
            
            if (pauseMenuPanel != null)
                pauseMenuPanel.SetActive(true);
            
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
        }

        public void OnMainMenu()
        {
            PlayButtonSound();
            Debug.Log("Returning to main menu");
            
            // Unpause before loading
            Time.timeScale = 1f;
            isPaused = false;
            
            // Load main menu
            if (useSceneName)
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
            else
            {
                SceneManager.LoadScene(mainMenuBuildIndex);
            }
        }

        public void OnQuit()
        {
            PlayButtonSound();
            Debug.Log("Quitting game from pause menu");
            
            Time.timeScale = 1f;
            
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }

        private void PlayButtonSound()
        {
            if (audioSource != null && buttonClickSound != null)
            {
                audioSource.PlayOneShot(buttonClickSound);
            }
        }

        #region Fade Animations (if not using Animator)

        private IEnumerator FadeIn()
        {
            isAnimating = true;
            
            float elapsed = 0f;
            float duration = animationDelay;
            
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
                yield return null;
            }
            
            canvasGroup.alpha = 1f;
            Time.timeScale = 0f;
            isAnimating = false;
        }

        private IEnumerator FadeOutAndResume()
        {
            isAnimating = true;
            Time.timeScale = 1f; // Unpause for animation
            
            float elapsed = 0f;
            float duration = animationDelay;
            
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                yield return null;
            }
            
            canvasGroup.alpha = 0f;
            ResumeGame();
        }

        #endregion

        // Public method to check if game is paused (for other scripts)
        public static bool IsGamePaused()
        {
            PauseMenuManager pauseMenu = FindObjectOfType<PauseMenuManager>();
            return pauseMenu != null && pauseMenu.isPaused;
        }

        #region Debug Methods

        [ContextMenu("Force Pause")]
        private void DebugForcePause()
        {
            OnPause();
        }

        [ContextMenu("Force Resume")]
        private void DebugForceResume()
        {
            OnResume();
        }

        [ContextMenu("Print Status")]
        private void DebugPrintStatus()
        {
            Debug.Log("=== PAUSE MENU STATUS ===");
            Debug.Log($"Is Paused: {isPaused}");
            Debug.Log($"Is Animating: {isAnimating}");
            Debug.Log($"Time Scale: {Time.timeScale}");
            Debug.Log($"Use Animations: {useAnimations}");
            Debug.Log($"Animator: {(pauseMenuAnimator != null ? "Found" : "NULL")}");
            if (pauseMenuAnimator != null)
            {
                Debug.Log($"Animator Update Mode: {pauseMenuAnimator.updateMode}");
            }
        }

        #endregion
    }
}