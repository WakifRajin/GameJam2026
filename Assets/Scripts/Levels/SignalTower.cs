using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
        [SerializeField] private InventoryManager playerInventory;
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
        [SerializeField] private KeyCode interactKey = KeyCode.F;
        [SerializeField] private GameObject interactionPromptUI;
        [SerializeField] private TextMeshProUGUI promptText;
        
        [Header("Audio")]
        [SerializeField] private AudioClip repairSound;
        [SerializeField] private AudioClip activationSound;
        [SerializeField] private AudioSource audioSource;
        
        private TowerState currentState = TowerState.Broken;
        private int currentScrapMetal = 0;
        private float currentPower = 0f;
        private GameObject player;
        private bool playerInRange = false;
        private float distanceToPlayer = 0f;
        
        public event System.Action OnTowerRepaired;
        public event System.Action OnTowerActivated;
        
        public TowerState CurrentState => currentState;
        public bool IsFullyActivated => currentState == TowerState.Active;
        public float DistanceToPlayer => distanceToPlayer;

        private void Start()
        {
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
            
            if (playerInventory == null && player != null)
            {
                playerInventory = player.GetComponent<InventoryManager>();
            }
            
            if (roverAttributes == null && player != null)
            {
                roverAttributes = player.GetComponent<RoverAttributeManager>();
            }
            
            // Always show the tower from the start
            UpdateVisualState();
            
            if (interactionPromptUI != null)
            {
                interactionPromptUI.SetActive(false);
            }
            
            Debug.Log($"Signal Tower initialized at {transform.position}");
        }

        private void Update()
        {
            UpdateDistanceToPlayer();
            CheckPlayerDistance();
            
            if (playerInRange && Input.GetKeyDown(interactKey))
            {
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
                if (interactionPromptUI != null)
                {
                    interactionPromptUI.SetActive(playerInRange && currentState != TowerState.Active);
                }
            }
        }

        private void UpdatePrompt()
        {
            if (!playerInRange || promptText == null) return;
            
            switch (currentState)
            {
                case TowerState.Broken:
                    promptText.text = $"[{interactKey}] Repair Tower\n" +
                                    $"Requires: {scrapMetalRequired} Scrap Metal\n" +
                                    $"You have: {GetPlayerScrapMetal()}";
                    break;
                    
                case TowerState.Repaired:
                    promptText.text = $"[{interactKey}] Power Tower\n" +
                                    $"Requires: {powerRequired} Power\n" +
                                    $"You have: {roverAttributes?.CurrentPower:F0}";
                    break;
                    
                case TowerState.Active:
                    promptText.text = "Tower Online - Signal Transmitting!";
                    break;
            }
        }

        private void AttemptInteraction()
        {
            switch (currentState)
            {
                case TowerState.Broken:
                    AttemptRepair();
                    break;
                    
                case TowerState.Repaired:
                    AttemptActivation();
                    break;
            }
        }

        private void AttemptRepair()
        {
            int playerScrap = GetPlayerScrapMetal();
            
            if (playerScrap >= scrapMetalRequired)
            {
                // Consume scrap metal from inventory
                ConsumeScrapMetal(scrapMetalRequired);
                
                // Repair tower
                RepairTower();
            }
            else
            {
                Debug.Log($"Not enough scrap metal! Need {scrapMetalRequired}, have {playerScrap}");
            }
        }

        private void AttemptActivation()
        {
            if (roverAttributes != null && roverAttributes.CurrentPower >= powerRequired)
            {
                // Consume power
                roverAttributes.ModifyPower(-powerRequired);
                
                // Activate tower
                ActivateTower();
            }
            else
            {
                Debug.Log($"Not enough power! Need {powerRequired}, have {roverAttributes?.CurrentPower:F0}");
            }
        }

        private void RepairTower()
        {
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
            Debug.Log("Tower Repaired!");
        }

        private void ActivateTower()
        {
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
            Debug.Log("Tower Activated! Distress signal sent!");
        }

        private void UpdateVisualState()
        {
            // Update models based on state
            // If you only have one model, leave these references empty and just use the light
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
        }

        private int GetPlayerScrapMetal()
        {
            if (playerInventory == null) return 0;
            
            int count = 0;
            foreach (var item in playerInventory.Inventory)
            {
                if (item != null && item.itemType == ItemType.Material)
                {
                    count++;
                }
            }
            return count;
        }

        private void ConsumeScrapMetal(int amount)
        {
            if (playerInventory == null) return;
            
            int consumed = 0;
            var inventory = new System.Collections.Generic.List<CollectibleItem>(playerInventory.Inventory);
            
            foreach (var item in inventory)
            {
                if (consumed >= amount) break;
                
                if (item != null && item.itemType == ItemType.Material)
                {
                    playerInventory.RemoveItem(item);
                    consumed++;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw interaction range
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
        
        [ContextMenu("Force Repair Tower")]
        private void DebugRepairTower()
        {
            RepairTower();
        }
        
        [ContextMenu("Force Activate Tower")]
        private void DebugActivateTower()
        {
            if (currentState == TowerState.Broken)
            {
                RepairTower();
            }
            ActivateTower();
        }
        
        [ContextMenu("Reset Tower")]
        private void DebugResetTower()
        {
            currentState = TowerState.Broken;
            UpdateVisualState();
            Debug.Log("Tower reset to broken state");
        }
        
        #endregion
    }
}