using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GameJam2026
{
    /// <summary>
    /// Handles settings menu controls (volume, graphics, etc.)
    /// </summary>
    public class SettingsMenuManager : MonoBehaviour
    {
        [Header("Audio Settings")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private TextMeshProUGUI masterVolumeText;
        [SerializeField] private TextMeshProUGUI musicVolumeText;
        [SerializeField] private TextMeshProUGUI sfxVolumeText;
        
        [Header("Graphics Settings")]
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        
        [Header("Gameplay Settings")]
        [SerializeField] private Slider mouseSensitivitySlider;
        [SerializeField] private TextMeshProUGUI sensitivityText;
        
        [Header("Back Button")]
        [SerializeField] private Button backButton;
        
        private Resolution[] resolutions;

        private void Start()
        {
            // Load saved settings
            LoadSettings();
            
            // Setup listeners
            SetupAudioListeners();
            SetupGraphicsListeners();
            SetupGameplayListeners();
            
            if (backButton != null)
            {
                backButton.onClick.AddListener(OnBackClicked);
            }
            
            // Populate resolution dropdown
            SetupResolutionDropdown();
        }

        #region Audio Settings

        private void SetupAudioListeners()
        {
            if (masterVolumeSlider != null)
                masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            
            if (musicVolumeSlider != null)
                musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }

        private void OnMasterVolumeChanged(float value)
        {
            AudioListener.volume = value;
            if (masterVolumeText != null)
                masterVolumeText.text = $"{(int)(value * 100)}%";
            
            PlayerPrefs.SetFloat("MasterVolume", value);
        }

        private void OnMusicVolumeChanged(float value)
        {
            // Apply to music audio sources (you can implement this based on your audio system)
            if (musicVolumeText != null)
                musicVolumeText.text = $"{(int)(value * 100)}%";
            
            PlayerPrefs.SetFloat("MusicVolume", value);
        }

        private void OnSFXVolumeChanged(float value)
        {
            // Apply to SFX audio sources (you can implement this based on your audio system)
            if (sfxVolumeText != null)
                sfxVolumeText.text = $"{(int)(value * 100)}%";
            
            PlayerPrefs.SetFloat("SFXVolume", value);
        }

        #endregion

        #region Graphics Settings

        private void SetupGraphicsListeners()
        {
            if (qualityDropdown != null)
                qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
            
            if (fullscreenToggle != null)
                fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
            
            if (resolutionDropdown != null)
                resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        }

        private void SetupResolutionDropdown()
        {
            if (resolutionDropdown == null) return;
            
            resolutions = Screen.resolutions;
            resolutionDropdown.ClearOptions();
            
            System.Collections.Generic.List<string> options = new System.Collections.Generic.List<string>();
            int currentResolutionIndex = 0;
            
            for (int i = 0; i < resolutions.Length; i++)
            {
                string option = $"{resolutions[i].width} x {resolutions[i].height} @ {resolutions[i].refreshRate}Hz";
                options.Add(option);
                
                if (resolutions[i].width == Screen.currentResolution.width &&
                    resolutions[i].height == Screen.currentResolution.height)
                {
                    currentResolutionIndex = i;
                }
            }
            
            resolutionDropdown.AddOptions(options);
            resolutionDropdown.value = currentResolutionIndex;
            resolutionDropdown.RefreshShownValue();
        }

        private void OnQualityChanged(int qualityIndex)
        {
            QualitySettings.SetQualityLevel(qualityIndex);
            PlayerPrefs.SetInt("QualityLevel", qualityIndex);
            Debug.Log($"Quality changed to: {QualitySettings.names[qualityIndex]}");
        }

        private void OnFullscreenChanged(bool isFullscreen)
        {
            Screen.fullScreen = isFullscreen;
            PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
            Debug.Log($"Fullscreen: {isFullscreen}");
        }

        private void OnResolutionChanged(int resolutionIndex)
        {
            Resolution resolution = resolutions[resolutionIndex];
            Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
            PlayerPrefs.SetInt("ResolutionIndex", resolutionIndex);
            Debug.Log($"Resolution changed to: {resolution.width}x{resolution.height}");
        }

        #endregion

        #region Gameplay Settings

        private void SetupGameplayListeners()
        {
            if (mouseSensitivitySlider != null)
                mouseSensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        }

        private void OnSensitivityChanged(float value)
        {
            if (sensitivityText != null)
                sensitivityText.text = value.ToString("F1");
            
            PlayerPrefs.SetFloat("MouseSensitivity", value);
            
            // Apply to your camera controller if needed
            // FindObjectOfType<CameraController>()?.SetSensitivity(value);
        }

        #endregion

        #region Load/Save Settings

        private void LoadSettings()
        {
            // Load audio settings
            float masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
            float musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.8f);
            float sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
            
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.value = masterVolume;
                AudioListener.volume = masterVolume;
            }
            
            if (musicVolumeSlider != null)
                musicVolumeSlider.value = musicVolume;
            
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.value = sfxVolume;
            
            // Load graphics settings
            int qualityLevel = PlayerPrefs.GetInt("QualityLevel", QualitySettings.GetQualityLevel());
            if (qualityDropdown != null)
            {
                qualityDropdown.value = qualityLevel;
                QualitySettings.SetQualityLevel(qualityLevel);
            }
            
            bool fullscreen = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = fullscreen;
                Screen.fullScreen = fullscreen;
            }
            
            // Load gameplay settings
            float sensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 1f);
            if (mouseSensitivitySlider != null)
                mouseSensitivitySlider.value = sensitivity;
        }

        public void ResetToDefaults()
        {
            PlayerPrefs.DeleteAll();
            LoadSettings();
            Debug.Log("Settings reset to defaults");
        }

        #endregion

        private void OnBackClicked()
        {
            // This will be called by the back button
            // The specific menu manager (Main or Pause) will handle showing the correct panel
            
            MainMenuManager mainMenu = FindObjectOfType<MainMenuManager>();
            if (mainMenu != null)
            {
                mainMenu.OnBackToMain();
                return;
            }
            
            PauseMenuManager pauseMenu = FindObjectOfType<PauseMenuManager>();
            if (pauseMenu != null)
            {
                pauseMenu.OnBackToPauseMenu();
                return;
            }
        }
    }
}