using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

namespace GameJam2026
{
    /// <summary>
    /// The signal tower that needs to be repaired and powered
    /// Always visible, distance tracked for navigation via RoverUIManager
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
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        
        private TowerState currentState = TowerState.Broken;
        private int currentScrapMetal = 0;
        private float currentPower = 0f;
        private GameObject player;
        private bool playerInRange = false;
        private float distanceToPlayer = 0f;
        
        // Prevent accidental auto-activation
        private bool manualActivationOnly = true;
        
        public event System.Action OnTowerRepaired;
        public event System.Action OnTowerActivated;
        
        public TowerState CurrentState => currentState;
        public bool IsFullyActivated => currentState == TowerState.Active;
        public float DistanceToPlayer => distanceToPlayer;
        public bool IsRepaired => currentState == TowerState.Repaired || currentState == TowerState.Active;

        private void Start()
        {
            // IMPORTANT: Force state to Broken on start
            currentState = TowerState.Broken;
            
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
                Debug.LogError("SignalTower: GridInventoryManager not found on player!");
            }
            
            if (roverAttributes == null)
            {
                Debug.LogError("SignalTower: RoverAttributeManager not found on player!");
            }
            
            // Always show the tower from the start
            UpdateVisualState();
            
            if (interactionPromptUI != null)
            {
                interactionPromptUI.SetActive(false);
            }
            
            DebugLog($"SignalTower initialized at {transform.position}");
            DebugLog($"- State: {currentState}");
            DebugLog($"- Scrap Required: {scrapMetalRequired}");
            DebugLog($"- Power Required: {powerRequired}");
            DebugLog($"- Manual Activation Only: {manualActivationOnly}");
        }

        private void Update()
        {
            UpdateDistanceToPlayer();
            CheckPlayerDistance();
            
            // Check for interaction input
            if (playerInRange && Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame)
            {
                DebugLog($"Interact key pressed! Current state: {currentState}");
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
            
            if (playerInRange != wasInRange)
            {
                DebugLog($"Player in range changed: {playerInRange} (Distance: {distanceToPlayer:F2}m)");
                
                if (interactionPromptUI != null)
                {
                    bool shouldShow = playerInRange && currentState != TowerState.Active;
                    interactionPromptUI.SetActive(shouldShow);
                    DebugLog($"Prompt UI set to: {shouldShow}");
                }
            }
        }

        private void UpdatePrompt()
        {
            if (!playerInRange || promptText == null) return;
            
            switch (currentState)
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
                    promptText.text = "Tower Online - Signal Transmitting!";
                    break;
            }
        }

        private void AttemptInteraction()
        {
            DebugLog($"=== AttemptInteraction called ===");
            DebugLog($"Current State: {currentState}");
            
            switch (currentState)
            {
                case TowerState.Broken:
                    AttemptRepair();
                    break;
                    
                case TowerState.Repaired:
                    AttemptActivation();
                    break;
                    
                case TowerState.Active:
                    DebugLog("Tower already active!");
                    break;
            }
        }

        private void AttemptRepair()
        {
            int playerScrap = GetPlayerScrapMetal();
            
            DebugLog($"=== Attempting REPAIR ===");
            DebugLog($"Need: {scrapMetalRequired}, Have: {playerScrap}");
            
            if (playerScrap >= scrapMetalRequired)
            {
                // Consume scrap metal from inventory
                ConsumeScrapMetal(scrapMetalRequired);
                
                // Repair tower
                RepairTower();
            }
            else
            {
                Debug.LogWarning($"❌ Not enough scrap metal! Need {scrapMetalRequired}, have {playerScrap}");
            }
        }

        private void AttemptActivation()
        {
            float havePower = roverAttributes != null ? roverAttributes.CurrentPower : 0f;
            
            DebugLog($"=== Attempting ACTIVATION ===");
            DebugLog($"Need: {powerRequired}, Have: {havePower:F0}");
            
            if (roverAttributes != null && havePower >= powerRequired)
            {
                // Consume power
                roverAttributes.ModifyPower(-powerRequired);
                
                // Activate tower
                ActivateTower();
            }
            else
            {
                Debug.LogWarning($"❌ Not enough power! Need {powerRequired}, have {havePower:F0}");
            }
        }

        private void RepairTower()
        {
            if (currentState != TowerState.Broken)
            {
                Debug.LogWarning($"RepairTower called but tower is already {currentState}!");
                return;
            }
            
            DebugLog("=== REPAIRING TOWER ===");
            
            currentState = TowerState.Repaired;
            currentScrapMetal = scrapMetalRequired;
            
            UpdateVisualState();
            
            if (repairEffect != null)
            {
                repairEffect.Play();
            }
            
            if (audioSource != null && repairSound != null)
            {
                audioSource.PlayOneShot(repairSound);
            }
            
            OnTowerRepaired?.Invoke();
            Debug.Log("✓ Tower Repaired! Now needs power to activate.");
        }

        private void ActivateTower()
        {
            if (currentState != TowerState.Repaired)
            {
                Debug.LogWarning($"ActivateTower called but tower state is {currentState}, not Repaired!");
                return;
            }
            
            DebugLog("=== ACTIVATING TOWER ===");
            
            currentState = TowerState.Active;
            currentPower = powerRequired;
            
            UpdateVisualState();
            
            if (activationEffect != null)
            {
                activationEffect.Play();
            }
            
            if (audioSource != null && activationSound != null)
            {
                audioSource.PlayOneShot(activationSound);
            }
            
            if (interactionPromptUI != null)
            {
                interactionPromptUI.SetActive(false);
            }
            
            OnTowerActivated?.Invoke();
            Debug.Log("✓✓✓ Tower Activated! Distress signal sent! ✓✓✓");
        }

        private void UpdateVisualState()
        {
            // Update models based on state
            if (brokenModel != null) 
                brokenModel.SetActive(currentState == TowerState.Broken);
            if (repairedModel != null) 
                repairedModel.SetActive(currentState == TowerState.Repaired);
            if (activeModel != null) 
                activeModel.SetActive(currentState == TowerState.Active);
            
            // Update light color and intensity based on state
            if (towerLight != null)
            {
                switch (currentState)
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
            
            DebugLog($"Tower visual updated to: {currentState}");
        }

        private int GetPlayerScrapMetal()
        {
            if (gridInventoryManager == null) 
            {
                Debug.LogWarning("GridInventoryManager is null!");
                return 0;
            }
            
            int count = 0;
            var grid = gridInventoryManager.InventoryGrid;
            
            // Count items in grid that are Materials
            for (int y = 0; y < gridInventoryManager.GridHeight; y++)
            {
                for (int x = 0; x < gridInventoryManager.GridWidth; x++)
                {
                    var item = grid[x, y];
                    if (item != null && item.itemType == ItemType.Material)
                    {
                        count++;
                    }
                }
            }
            
            return count;
        }

        private void ConsumeScrapMetal(int amount)
        {
            if (gridInventoryManager == null) return;
            
            int consumed = 0;
            var grid = gridInventoryManager.InventoryGrid;
            
            // Find and remove scrap metal items
            for (int y = 0; y < gridInventoryManager.GridHeight && consumed < amount; y++)
            {
                for (int x = 0; x < gridInventoryManager.GridWidth && consumed < amount; x++)
                {
                    var item = grid[x, y];
                    if (item != null && item.itemType == ItemType.Material)
                    {
                        gridInventoryManager.RemoveItemAt(x, y);
                        consumed++;
                        DebugLog($"Consumed scrap metal {consumed}/{amount}");
                    }
                }
            }
            
            DebugLog($"✓ Consumed {consumed} scrap metal");
        }

        // PUBLIC METHOD - Only for manual/scripted activation
        public void ForceRepair()
        {
            if (currentState == TowerState.Broken)
            {
                Debug.Log("ForceRepair called - skipping requirements");
                RepairTower();
            }
        }

        public void ForceActivate()
        {
            if (currentState == TowerState.Repaired)
            {
                Debug.Log("ForceActivate called - skipping requirements");
                ActivateTower();
            }
        }

        private void DebugLog(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[SignalTower] {message}");
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw interaction range
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
            
            // Draw label
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 3f, $"Signal Tower\nState: {currentState}");
            #endif
        }

        public enum TowerState
        {
            Broken,
            Repaired,
            Active
        }
        
        #region Debug Methods
        
        [ContextMenu("Force Repair Tower")]
        private void DebugRepairTower()
        {
            ForceRepair();
        }
        
        [ContextMenu("Force Activate Tower")]
        private void DebugActivateTower()
        {
            if (currentState == TowerState.Broken)
            {
                ForceRepair();
            }
            if (currentState == TowerState.Repaired)
            {
                ForceActivate();
            }
        }
        
        [ContextMenu("Reset Tower")]
        private void DebugResetTower()
        {
            currentState = TowerState.Broken;
            currentScrapMetal = 0;
            currentPower = 0;
            UpdateVisualState();
            Debug.Log("Tower reset to broken state");
        }
        
        [ContextMenu("Print Tower Status")]
        private void DebugPrintStatus()
        {
            Debug.Log("=== SIGNAL TOWER STATUS ===");
            Debug.Log($"State: {currentState}");
            Debug.Log($"Player in range: {playerInRange} (Distance: {distanceToPlayer:F2}m)");
            Debug.Log($"GridInventoryManager: {(gridInventoryManager != null ? "Found" : "NULL")}");
            Debug.Log($"RoverAttributes: {(roverAttributes != null ? "Found" : "NULL")}");
            Debug.Log($"Scrap metal in inventory: {GetPlayerScrapMetal()}");
            Debug.Log($"Rover power: {(roverAttributes != null ? roverAttributes.CurrentPower.ToString("F0") : "N/A")}");
            Debug.Log($"Interaction prompt active: {(interactionPromptUI != null ? interactionPromptUI.activeSelf.ToString() : "NULL")}");
        }
        
        #endregion
    }
}