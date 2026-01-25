using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GameJam2026
{
    /// <summary>
    /// The broken signal tower that needs to be repaired
    /// </summary>
    public class SignalTower : MonoBehaviour
    {
        [Header("Requirements")]
        [SerializeField] private int scrapMetalRequired = 5;
        [SerializeField] private float powerRequired = 50f;
        
        [Header("References")]
        [SerializeField] private InventoryManager playerInventory;
        [SerializeField] private RoverAttributeManager roverAttributes;
        
        [Header("Visual")]
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
        
        public event System.Action OnTowerRepaired;
        public event System.Action OnTowerActivated;
        
        public TowerState CurrentState => currentState;
        public bool IsFullyActivated => currentState == TowerState.Active;

        private void Start()
        {
            player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = FindObjectOfType<RoverAttributeManager>()?.gameObject;
            }
            
            if (playerInventory == null && player != null)
            {
                playerInventory = player.GetComponent<InventoryManager>();
            }
            
            if (roverAttributes == null && player != null)
            {
                roverAttributes = player.GetComponent<RoverAttributeManager>();
            }
            
            UpdateVisualState();
            
            if (interactionPromptUI != null)
            {
                interactionPromptUI.SetActive(false);
            }
        }

        private void Update()
        {
            CheckPlayerDistance();
            
            if (playerInRange && Input.GetKeyDown(interactKey))
            {
                AttemptInteraction();
            }
            
            UpdatePrompt();
        }

        private void CheckPlayerDistance()
        {
            if (player == null) return;
            
            float distance = Vector3.Distance(transform.position, player.transform.position);
            bool wasInRange = playerInRange;
            playerInRange = distance <= interactionRange;
            
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
                    promptText.text = "Tower Online - Signal Transmitting";
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
            // Update models
            if (brokenModel != null) brokenModel.SetActive(currentState == TowerState.Broken);
            if (repairedModel != null) repairedModel.SetActive(currentState == TowerState.Repaired);
            if (activeModel != null) activeModel.SetActive(currentState == TowerState.Active);
            
            // Update light
            if (towerLight != null)
            {
                switch (currentState)
                {
                    case TowerState.Broken:
                        towerLight.color = brokenColor;
                        towerLight.intensity = 2f;
                        break;
                    case TowerState.Repaired:
                        towerLight.color = repairedColor;
                        towerLight.intensity = 5f;
                        break;
                    case TowerState.Active:
                        towerLight.color = activeColor;
                        towerLight.intensity = 10f;
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
    }
}