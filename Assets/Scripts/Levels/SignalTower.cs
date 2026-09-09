using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

namespace GameJam2026
{
    /// <summary>
    /// The signal tower that needs to be repaired and powered
    /// MANUAL INTERACTION ONLY - No auto-activation
    /// </summary>
    public class SignalTower : MonoBehaviour
    {
        [Header("Requirements")]
        [SerializeField] private int scrapMetalRequired = 5;
        [SerializeField] private float powerRequired = 50f;
        
        [Header("References")]
        [SerializeField] private GridInventoryManager gridInventoryManager;
        [SerializeField] private RoverAttributeManager roverAttributes;
        [SerializeField] private Transform playerTransform;
        
        [Header("Visual - Tower States")]
        [SerializeField] private Light towerLight;
        [SerializeField] private Color brokenColor = Color.red;
        [SerializeField] private Color repairedColor = Color.yellow;
        [SerializeField] private Color activeColor = Color.green;
        [SerializeField] private GameObject brokenModel;
        [SerializeField] private GameObject repairedModel;
        [SerializeField] private GameObject activeModel;
        [SerializeField] private ParticleSystem repairEffect;
        [SerializeField] private ParticleSystem activationEffect;
        
        [Header("Interaction")]
        [SerializeField] private float interactionRange = 5f;
        [SerializeField] private Key interactKey = Key.F;
        [SerializeField] private GameObject interactionPromptUI;
        [SerializeField] private TextMeshProUGUI promptText;
        
        [Header("Audio")]
        [SerializeField] private AudioClip repairSound;
        [SerializeField] private AudioClip activationSound;
        [SerializeField] private AudioSource audioSource;
        
        // IMPORTANT: Private state - cannot be changed externally
        private TowerState _currentState = TowerState.Broken;
        private int currentScrapMetal = 0;
        private float currentPower = 0f;
        private GameObject player;
        private bool playerInRange = false;
        private float distanceToPlayer = 0f;
        
        // Events
        public event System.Action OnTowerRepaired;
        public event System.Action OnTowerActivated;
        
        // PUBLIC READ-ONLY PROPERTIES
        public TowerState CurrentState => _currentState;
        public bool IsFullyActivated => _currentState == TowerState.Active;
        public bool IsRepaired => _currentState == TowerState.Repaired || _currentState == TowerState.Active;
        public float DistanceToPlayer => distanceToPlayer;
        public int ScrapMetalRequired => scrapMetalRequired;
        public float PowerRequired => powerRequired;

        private void Awake()
        {
            // FORCE state to Broken - this runs before Start()
            _currentState = TowerState.Broken;
            Debug.Log($"[SignalTower Awake] State forced to: {_currentState}");
        }

        private void Start()
        {
            // SAFETY CHECK - ensure still broken
            if (_currentState != TowerState.Broken)
            {
                Debug.LogError($"[SignalTower] State was changed to {_currentState} before Start()! Resetting to Broken.");
                _currentState = TowerState.Broken;
            }
            
            // Find player
            player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = FindObjectOfType<RoverAttributeManager>()?.gameObject;
            }
            
            if (player != null)
            {
                playerTransform = player.transform;
            }
            
            // Find GridInventoryManager
            if (gridInventoryManager == null && player != null)
            {
                gridInventoryManager = player.GetComponent<GridInventoryManager>();
            }
            
            if (roverAttributes == null && player != null)
            {
                roverAttributes = player.GetComponent<RoverAttributeManager>();
            }
            
            // Validate references
            if (gridInventoryManager == null)
            {
                Debug.LogError("[SignalTower] GridInventoryManager not found on player!");
            }
            
            if (roverAttributes == null)
            {
                Debug.LogError("[SignalTower] RoverAttributeManager not found on player!");
            }
            
            // Setup visuals
            UpdateVisualState();
            
            if (interactionPromptUI != null)
            {
                interactionPromptUI.SetActive(false);
            }
            
            Debug.Log($"[SignalTower] Initialized - State: {_currentState}, Scrap needed: {scrapMetalRequired}, Power needed: {powerRequired}");
        }

        private void Update()
        {
            UpdateDistanceToPlayer();
            CheckPlayerDistance();
            
            // Check for interaction input (F key)
            if (playerInRange && Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame)
            {
                Debug.Log($"[SignalTower] F key pressed - Current state: {_currentState}");
                AttemptInteraction();
            }
            
            UpdatePrompt();
        }

        private void UpdateDistanceToPlayer()
        {
            if (playerTransform == null) return;
            distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        }

        private void CheckPlayerDistance()
        {
            if (player == null) return;
            
            bool wasInRange = playerInRange;
            playerInRange = distanceToPlayer <= interactionRange;
            
            if (playerInRange != wasInRange && interactionPromptUI != null)
            {
                bool shouldShow = playerInRange && _currentState != TowerState.Active;
                interactionPromptUI.SetActive(shouldShow);
            }
        }

        private void UpdatePrompt()
        {
            if (!playerInRange || promptText == null) return;
            
            switch (_currentState)
            {
                case TowerState.Broken:
                    int haveScrap = GetPlayerScrapMetal();
                    promptText.text = $"[{interactKey}] Repair Tower\n" +
                                    $"Requires: {scrapMetalRequired} Scrap Metal\n" +
                                    $"You have: {haveScrap}";
                    break;
                    
                case TowerState.Repaired:
                    float havePower = roverAttributes != null ? roverAttributes.CurrentPower : 0f;
                    promptText.text = $"[{interactKey}] Power Tower\n" +
                                    $"Requires: {powerRequired} Power\n" +
                                    $"You have: {havePower:F0}";
                    break;
                    
                case TowerState.Active:
                    promptText.text = "✓ Tower Online - Signal Transmitting!";
                    break;
            }
        }

        private void AttemptInteraction()
        {
            Debug.Log($"[SignalTower] === INTERACTION ATTEMPT === State: {_currentState}");
            
            switch (_currentState)
            {
                case TowerState.Broken:
                    AttemptRepair();
                    break;
                    
                case TowerState.Repaired:
                    AttemptActivation();
                    break;
                    
                case TowerState.Active:
                    Debug.Log("[SignalTower] Tower is already active!");
                    break;
            }
        }

        private void AttemptRepair()
        {
            int playerScrap = GetPlayerScrapMetal();
            
            Debug.Log($"[SignalTower] Repair attempt - Need: {scrapMetalRequired}, Have: {playerScrap}");
            
            if (playerScrap >= scrapMetalRequired)
            {
                ConsumeScrapMetal(scrapMetalRequired);
                RepairTower();
            }
            else
            {
                Debug.LogWarning($"[SignalTower] ❌ Insufficient scrap! Need {scrapMetalRequired}, have {playerScrap}");
            }
        }

        private void AttemptActivation()
        {
            float havePower = roverAttributes != null ? roverAttributes.CurrentPower : 0f;
            
            Debug.Log($"[SignalTower] Activation attempt - Need: {powerRequired}, Have: {havePower:F0}");
            
            if (roverAttributes != null && havePower >= powerRequired)
            {
                roverAttributes.ModifyPower(-powerRequired);
                ActivateTower();
            }
            else
            {
                Debug.LogWarning($"[SignalTower] ❌ Insufficient power! Need {powerRequired}, have {havePower:F0}");
            }
        }

        private void RepairTower()
        {
            // SAFETY CHECK
            if (_currentState != TowerState.Broken)
            {
                Debug.LogError($"[SignalTower] RepairTower called but state is {_currentState}, not Broken! BLOCKING.");
                Debug.LogError($"Stack trace: {System.Environment.StackTrace}");
                return;
            }
            
            Debug.Log("[SignalTower] === REPAIRING TOWER ===");
            
            _currentState = TowerState.Repaired;
            currentScrapMetal = scrapMetalRequired;
            
            UpdateVisualState();
            
            if (repairEffect != null)
                repairEffect.Play();
            
            if (audioSource != null && repairSound != null)
                audioSource.PlayOneShot(repairSound);
            
            OnTowerRepaired?.Invoke();
            
            Debug.Log("[SignalTower] ✓ Tower Repaired! Now needs power to activate.");
        }

        private void ActivateTower()
        {
            // SAFETY CHECK
            if (_currentState != TowerState.Repaired)
            {
                Debug.LogError($"[SignalTower] ActivateTower called but state is {_currentState}, not Repaired! BLOCKING.");
                Debug.LogError($"Stack trace: {System.Environment.StackTrace}");
                return;
            }
            
            Debug.Log("[SignalTower] === ACTIVATING TOWER ===");
            
            _currentState = TowerState.Active;
            currentPower = powerRequired;
            
            UpdateVisualState();
            
            if (activationEffect != null)
                activationEffect.Play();
            
            if (audioSource != null && activationSound != null)
                audioSource.PlayOneShot(activationSound);
            
            if (interactionPromptUI != null)
                interactionPromptUI.SetActive(false);
            
            OnTowerActivated?.Invoke();
            
            Debug.Log("[SignalTower] ✓✓✓ TOWER ACTIVATED! Distress signal sent! ✓✓✓");
        }

        private void UpdateVisualState()
        {
            if (brokenModel != null) 
                brokenModel.SetActive(_currentState == TowerState.Broken);
            if (repairedModel != null) 
                repairedModel.SetActive(_currentState == TowerState.Repaired);
            if (activeModel != null) 
                activeModel.SetActive(_currentState == TowerState.Active);
            
            if (towerLight != null)
            {
                switch (_currentState)
                {
                    case TowerState.Broken:
                        towerLight.color = brokenColor;
                        towerLight.intensity = 3f;
                        towerLight.range = 15f;
                        break;
                    case TowerState.Repaired:
                        towerLight.color = repairedColor;
                        towerLight.intensity = 6f;
                        towerLight.range = 25f;
                        break;
                    case TowerState.Active:
                        towerLight.color = activeColor;
                        towerLight.intensity = 10f;
                        towerLight.range = 50f;
                        break;
                }
            }
        }

        private int GetPlayerScrapMetal()
        {
            if (gridInventoryManager == null) return 0;

            // Counts units, not occupied slots - a stack of 5 scrap is 5, not 1.
            return gridInventoryManager.CountOfType(ItemType.Material);
        }

        private void ConsumeScrapMetal(int amount)
        {
            if (gridInventoryManager == null) return;

            int consumed = gridInventoryManager.RemoveItemsOfType(ItemType.Material, amount);
            Debug.Log($"[SignalTower] Consumed {consumed} scrap metal");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }

        public enum TowerState
        {
            Broken,
            Repaired,
            Active
        }
        
        #region Debug Methods
        
        [ContextMenu("Print Tower Status")]
        private void DebugPrintStatus()
        {
            Debug.Log("=== SIGNAL TOWER STATUS ===");
            Debug.Log($"Current State: {_currentState}");
            Debug.Log($"Player in range: {playerInRange} ({distanceToPlayer:F2}m)");
            Debug.Log($"Scrap in inventory: {GetPlayerScrapMetal()}/{scrapMetalRequired}");
            Debug.Log($"Rover power: {(roverAttributes != null ? roverAttributes.CurrentPower : 0f):F0}/{powerRequired}");
            Debug.Log($"Prompt active: {(interactionPromptUI != null ? interactionPromptUI.activeSelf : false)}");
        }
        
        [ContextMenu("Reset Tower")]
        private void DebugResetTower()
        {
            _currentState = TowerState.Broken;
            currentScrapMetal = 0;
            currentPower = 0;
            UpdateVisualState();
            Debug.Log("[SignalTower] Tower reset to Broken state");
        }
        
        #endregion
    }
}