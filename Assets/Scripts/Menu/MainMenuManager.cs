using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameJam2026
{
    /// <summary>
    /// Manages the main menu UI and scene transitions
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Menu Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject creditsPanel;
        
        [Header("Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button quitButton;
        
        [Header("Settings")]
        [SerializeField] private string firstLevelSceneName = "Level1";
        [SerializeField] private int firstLevelBuildIndex = 1; // Use if you prefer build index
        [SerializeField] private bool useSceneName = true; // true = use name, false = use build index
        
        [Header("Audio")]
        [SerializeField] private AudioSource menuMusic;
        [SerializeField] private AudioClip buttonClickSound;
        
        private AudioSource audioSource;

        private void Start()
        {
            // Ensure time is running
            Time.timeScale = 1f;
            
            // Get or add audio source for button sounds
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null && buttonClickSound != null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            
            // Setup button listeners
            if (startButton != null)
                startButton.onClick.AddListener(OnStartGame);
            
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettings);
            
            if (creditsButton != null)
                creditsButton.onClick.AddListener(OnCredits);
            
            if (quitButton != null)
                quitButton.onClick.AddListener(OnQuit);
            
            // Show main panel, hide others
            ShowMainPanel();
            
            Debug.Log("Main Menu initialized");
        }

        private void ShowMainPanel()
        {
            if (mainPanel != null) mainPanel.SetActive(true);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(false);
        }

        public void OnStartGame()
        {
            PlayButtonSound();
            Debug.Log("Starting game...");
            
            // Load first level
            if (useSceneName)
            {
                SceneManager.LoadScene(firstLevelSceneName);
            }
            else
            {
                SceneManager.LoadScene(firstLevelBuildIndex);
            }
        }

        public void OnSettings()
        {
            PlayButtonSound();
            Debug.Log("Opening settings");
            
            if (mainPanel != null) mainPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(true);
        }

        public void OnCredits()
        {
            PlayButtonSound();
            Debug.Log("Opening credits");
            
            if (mainPanel != null) mainPanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(true);
        }

        public void OnBackToMain()
        {
            PlayButtonSound();
            Debug.Log("Back to main menu");
            ShowMainPanel();
        }

        public void OnQuit()
        {
            PlayButtonSound();
            Debug.Log("Quitting game");
            
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
    }
}