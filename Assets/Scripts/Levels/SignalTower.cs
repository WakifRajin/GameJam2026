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
        [Header("Identity")]
        [Tooltip("Shown on the relay HUD, e.g. \"Relay Alpha\".")]
        [SerializeField] private string relayName = "Signal Tower";

        [Header("Requirements")]
        [SerializeField] private int scrapMetalRequired = 5;
        [SerializeField] private float powerRequired = 50f;
        [Tooltip("Optional. This tower stays locked until the named tower is fully active.")]
        [SerializeField] private SignalTower prerequisiteTower;
        [Tooltip("ON: pay with Power Cells carried in cargo (matches how the repair spends scrap). OFF: siphon the rover's own battery, the old behaviour.")]
        [SerializeField] private bool powerFromCargo = true;

        [Header("Activation Rewards")]
        [Tooltip("Permanent comm range granted when this tower goes online.")]
        [SerializeField] private float commRangeReward = 0f;
        [Tooltip("Permanent max power granted (and immediately topped up) when this goes online.")]
        [SerializeField] private float maxPowerReward = 0f;
        [Tooltip("Permanent cargo capacity granted when this goes online.")]
        [SerializeField] private float cargoCapacityReward = 0f;
        [Tooltip("Extra inventory columns granted when this goes online (4 slots per column).")]
        [SerializeField] private int inventoryColumnsReward = 0;
        
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
        public string RelayName => string.IsNullOrWhiteSpace(relayName) ? name : relayName;
        public SignalTower PrerequisiteTower => prerequisiteTower;
        public Light TowerLight => towerLight;

        /// <summary>True while an earlier relay in the chain is still offline.</summary>
        public bool IsLocked => prerequisiteTower != null && !prerequisiteTower.IsFullyActivated;

        /// <summary>
        /// Power available to spend here. Power Cells in cargo by default, so activation costs
        /// something you had to haul - the rover's own battery is what keeps you alive.
        /// </summary>
        private float GetAvailablePower()
        {
            if (!powerFromCargo) return roverAttributes != null ? roverAttributes.CurrentPower : 0f;
            return gridInventoryManager != null ? gridInventoryManager.GetResource(ResourceIds.Power) : 0f;
        }

        private string PowerSourceLabel => powerFromCargo ? "cell power" : "battery";

        /// <summary>What the rover still needs to take the next step here, for HUD display.</summary>
        public string GetRequirementSummary()
        {
            if (IsLocked) return $"Locked · {prerequisiteTower.RelayName} first";

            switch (_currentState)
            {
                case TowerState.Broken:
                    return $"{scrapMetalRequired} scrap · {GetPlayerScrapMetal()} aboard";
                case TowerState.Repaired:
                    return $"{powerRequired:F0} power · {GetAvailablePower():F0} aboard";
                default:
                    return "Online";
            }
        }

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

            if (IsLocked)
            {
                promptText.text = $"{RelayName.ToUpperInvariant()}  ·  LOCKED\n" +
                                  $"{prerequisiteTower.RelayName} has to come online first";
                return;
            }

            switch (_currentState)
            {
                case TowerState.Broken:
                    promptText.text = $"[{interactKey}]  REPAIR\n" +
                                      $"{scrapMetalRequired} scrap  ·  {GetPlayerScrapMetal()} aboard";
                    break;

                case TowerState.Repaired:
                    // Reads the same source the activation actually spends from; this used to
                    // show the rover's battery while the spend came out of cargo cells.
                    promptText.text = $"[{interactKey}]  POWER UP\n" +
                                      $"{powerRequired:F0} power  ·  {GetAvailablePower():F0} aboard";
                    break;
                    
                case TowerState.Active:
                    promptText.text = "ONLINE  ·  TRANSMITTING";
                    break;
            }
        }

        private void AttemptInteraction()
        {
            Debug.Log($"[SignalTower] === INTERACTION ATTEMPT === State: {_currentState}");

            if (IsLocked)
            {
                Debug.Log($"[SignalTower] {RelayName} is locked - {prerequisiteTower.RelayName} must be online first.");
                return;
            }
            
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
            float havePower = GetAvailablePower();

            Debug.Log($"[SignalTower] Activation attempt - Need: {powerRequired} ({PowerSourceLabel}), Have: {havePower:F0}");

            if (havePower < powerRequired)
            {
                Debug.LogWarning($"[SignalTower] Insufficient {PowerSourceLabel}! Need {powerRequired}, have {havePower:F0}");
                return;
            }

            if (powerFromCargo)
            {
                // Spends Power Cells out of cargo, exactly as the repair spends scrap.
                if (gridInventoryManager == null ||
                    !gridInventoryManager.ConsumeResource(ResourceIds.Power, powerRequired))
                {
                    Debug.LogWarning("[SignalTower] Could not spend cell power from cargo.");
                    return;
                }
            }
            else
            {
                if (roverAttributes == null) return;
                roverAttributes.ModifyPower(-powerRequired);
            }

            ActivateTower();
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

            GrantActivationRewards();

            OnTowerActivated?.Invoke();
            
            Debug.Log("[SignalTower] ✓✓✓ TOWER ACTIVATED! Distress signal sent! ✓✓✓");
        }

        /// <summary>
        /// Toggles a state model, refusing to touch a prefab ASSET.
        ///
        /// These slots are meant to hold scene children. If one is wired to the prefab in the
        /// Project window instead, SetActive edits the asset on disk - the file then shows up
        /// dirty in source control after every play session. Guarding here rather than trusting
        /// the wiring, since dragging the wrong object in is an easy mistake to repeat.
        /// </summary>
        private void SetModelActive(GameObject model, bool active)
        {
            if (model == null) return;

#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(model))
            {
                Debug.LogWarning(
                    $"[SignalTower] {RelayName}: '{model.name}' is a prefab asset, not a scene object. " +
                    "Ignoring so the asset file is not modified - assign the scene child instead.");
                return;
            }
#endif

            model.SetActive(active);
        }

        /// <summary>
        /// Permanent upgrades for bringing this relay online. This is the level's reward
        /// curve: each relay makes the rover meaningfully more capable, so the run gets
        /// easier exactly as the distances get longer.
        /// </summary>
        private void GrantActivationRewards()
        {
            if (roverAttributes == null)
            {
                Debug.LogWarning($"[SignalTower] {RelayName}: no rover reference, rewards skipped.");
                return;
            }

            if (commRangeReward > 0f)
            {
                roverAttributes.AddMaxCommunicationRange(commRangeReward);
                Debug.Log($"[SignalTower] {RelayName} reward: +{commRangeReward} comm range");
            }

            if (maxPowerReward > 0f)
            {
                roverAttributes.AddMaxPower(maxPowerReward);
                Debug.Log($"[SignalTower] {RelayName} reward: +{maxPowerReward} max power (and refilled)");
            }

            if (cargoCapacityReward > 0f)
            {
                roverAttributes.AddMaxCargoCapacity(cargoCapacityReward);
                Debug.Log($"[SignalTower] {RelayName} reward: +{cargoCapacityReward} cargo capacity");
            }

            if (inventoryColumnsReward > 0 && gridInventoryManager != null)
            {
                gridInventoryManager.Resize(
                    gridInventoryManager.GridWidth + inventoryColumnsReward,
                    gridInventoryManager.GridHeight);
                Debug.Log($"[SignalTower] {RelayName} reward: +{inventoryColumnsReward} inventory column(s)");
            }
        }

        private void UpdateVisualState()
        {
            SetModelActive(brokenModel, _currentState == TowerState.Broken);
            SetModelActive(repairedModel, _currentState == TowerState.Repaired);
            SetModelActive(activeModel, _currentState == TowerState.Active);

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