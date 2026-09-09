using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJam2026
{
    /// <summary>
    /// Grid inventory: stackable slots, weight budget, a derived resource ledger and save/load.
    ///
    /// Resource model - resources are NOT banked separately when an item is picked up. The
    /// ledger is derived from what is actually held (plus loose resources granted by exchanges),
    /// so an item can never be counted twice and discarding it never leaves phantom credit.
    /// Spending a resource drains the loose pool first, then eats real items - which is the
    /// equivalent-exchange fantasy the game is built around.
    /// </summary>
    public class GridInventoryManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RoverAttributeManager roverAttributes;
        [SerializeField] private ItemDatabase itemDatabase;

        [Header("Grid Settings")]
        [SerializeField] private int gridWidth = 6;
        [SerializeField] private int gridHeight = 4;
        [Tooltip("Reject pickups that would push total cargo weight past the rover's capacity.")]
        [SerializeField] private bool enforceWeightLimit = true;

        [Header("Pickup Behaviour")]
        [Tooltip("Top up existing stacks before claiming an empty slot.")]
        [SerializeField] private bool autoStackOnPickup = true;
        [Tooltip("Consumables like power cells apply their effect on pickup instead of taking a slot.")]
        [SerializeField] private bool autoUseInstantItems = false;

        [Header("Persistence")]
        [SerializeField] private string saveKey = "rover_inventory";

        [Header("Input System")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string inventoryActionName = "Inventory";
        private InputAction inventoryAction;

        // Flat storage, index = y * gridWidth + x. Never null; empty slots hold an empty stack.
        private ItemStack[] slots;

        // Loose resources from exchanges/rewards that are not backed by a held item.
        private readonly Dictionary<string, float> bankedResources = new Dictionary<string, float>();

        // Lifetime pickup totals per resource - never decreases, used for "collect N" objectives.
        // Value = resource worth gathered; Units = number of items gathered. A power cell worth
        // 15 power is 15 value but 1 unit, and objectives count units.
        private readonly Dictionary<string, float> lifetimeCollected = new Dictionary<string, float>();
        private readonly Dictionary<string, int> lifetimeUnits = new Dictionary<string, int>();

        private bool isOpen;

        #region Events

        /// <summary>
        /// Item, x, y. A slot now shows this item - fired by pickups, moves and merges alike.
        /// This is a "repaint this slot" signal, NOT a pickup signal: subscribe to
        /// OnItemCollected instead if you are counting what the player has gathered.
        /// </summary>
        public event Action<CollectibleItem, int, int> OnItemAddedToGrid;
        /// <summary>Item, quantity. Fired once per genuine pickup, never for moves or sorts.</summary>
        public event Action<CollectibleItem, int> OnItemCollected;
        /// <summary>x, y. Fired when a slot becomes empty.</summary>
        public event Action<int, int> OnItemRemovedFromGrid;
        /// <summary>Canonical resource key, new total.</summary>
        public event Action<string, float> OnResourceChanged;
        /// <summary>x, y. Fired for any change to a slot, including quantity-only changes.</summary>
        public event Action<int, int> OnSlotChanged;
        /// <summary>Fired after a resize or a load, when every slot must be redrawn.</summary>
        public event Action OnInventoryRebuilt;
        public event Action<float, float> OnWeightChanged;          // current, capacity
        public event Action<CollectibleItem, string> OnPickupRejected; // item, reason
        public event Action OnInventoryOpened;
        public event Action OnInventoryClosed;

        #endregion

        #region Properties

        public int GridWidth => gridWidth;
        public int GridHeight => gridHeight;
        public int TotalSlots => gridWidth * gridHeight;
        public bool IsOpen => isOpen;

        /// <summary>Number of occupied slots - not unit count, see TotalItemCount.</summary>
        public int UsedSlots
        {
            get
            {
                int used = 0;
                for (int i = 0; i < Slots.Length; i++)
                {
                    if (!Slots[i].IsEmpty) used++;
                }
                return used;
            }
        }

        public int TotalItemCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Slots.Length; i++) count += Slots[i].quantity;
                return count;
            }
        }

        /// <summary>
        /// Read-only snapshot in the legacy [x, y] shape. Allocates - do not call per frame.
        /// Prefer GetStackAt / GetItemAt.
        /// </summary>
        public CollectibleItem[,] InventoryGrid
        {
            get
            {
                var view = new CollectibleItem[gridWidth, gridHeight];
                for (int y = 0; y < gridHeight; y++)
                {
                    for (int x = 0; x < gridWidth; x++) view[x, y] = Slots[Index(x, y)].item;
                }
                return view;
            }
        }

        public float CargoCapacity =>
            Rover != null ? Rover.MaxCargoCapacity : float.PositiveInfinity;

        #endregion

        #region Unity lifecycle

        private void Awake()
        {
            Initialize();

            // Resolved here, not in Start - OnEnable runs first and needs it to subscribe.
            if (roverAttributes == null) roverAttributes = GetComponent<RoverAttributeManager>();

            if (inputActions != null)
            {
                inventoryAction = inputActions.FindAction(inventoryActionName);
                if (inventoryAction == null)
                {
                    Debug.LogWarning($"GridInventoryManager: action '{inventoryActionName}' not found in {inputActions.name}.");
                }
            }
        }

        private void Start()
        {
            // The rover starts empty; make sure its cargo readout agrees with the grid.
            SyncCargoWeight();
            OnInventoryRebuilt?.Invoke();
        }

        private void OnEnable()
        {
            if (inventoryAction != null)
            {
                inventoryAction.Enable();
                inventoryAction.performed += OnInventoryKeyPressed;
            }

            if (roverAttributes != null)
            {
                roverAttributes.OnCargoChanged += HandleCargoChanged;
            }
        }

        private void OnDisable()
        {
            if (inventoryAction != null)
            {
                inventoryAction.performed -= OnInventoryKeyPressed;
                inventoryAction.Disable();
            }

            if (roverAttributes != null)
            {
                roverAttributes.OnCargoChanged -= HandleCargoChanged;
            }
        }

        private void OnInventoryKeyPressed(InputAction.CallbackContext context) => ToggleInventory();

        private void HandleCargoChanged(float current, float capacity) => OnWeightChanged?.Invoke(current, capacity);

        #endregion

        #region Grid plumbing

        private int Index(int x, int y) => y * gridWidth + x;

        /// <summary>
        /// Slot storage. Allocates on first touch so the inventory is usable before Awake -
        /// script execution order, edit-mode tooling and early pickups all hit this.
        /// </summary>
        private ItemStack[] Slots
        {
            get
            {
                if (slots == null || slots.Length != gridWidth * gridHeight) Initialize();
                return slots;
            }
        }

        /// <summary>
        /// Rover reference, resolved on first touch. Same reasoning as Slots - anything that
        /// runs before Awake (or an unwired inspector field) still gets a live reference.
        /// </summary>
        private RoverAttributeManager Rover
        {
            get
            {
                if (roverAttributes == null) roverAttributes = GetComponent<RoverAttributeManager>();
                return roverAttributes;
            }
        }

        private void Initialize()
        {
            AllocateSlots(gridWidth, gridHeight);

            foreach (string key in ResourceIds.All)
            {
                if (!bankedResources.ContainsKey(key)) bankedResources[key] = 0f;
                if (!lifetimeCollected.ContainsKey(key)) lifetimeCollected[key] = 0f;
                if (!lifetimeUnits.ContainsKey(key)) lifetimeUnits[key] = 0;
            }
        }

        private void AllocateSlots(int width, int height)
        {
            slots = new ItemStack[width * height];
            for (int i = 0; i < slots.Length; i++) slots[i] = new ItemStack(null, 0);
        }

        public bool IsValidPosition(int x, int y) =>
            x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;

        public ItemStack GetStackAt(int x, int y) =>
            IsValidPosition(x, y) ? Slots[Index(x, y)] : null;

        public CollectibleItem GetItemAt(int x, int y)
        {
            var stack = GetStackAt(x, y);
            return stack != null && !stack.IsEmpty ? stack.item : null;
        }

        public int GetQuantityAt(int x, int y)
        {
            var stack = GetStackAt(x, y);
            return stack != null ? stack.quantity : 0;
        }

        public int GetEmptySlots() => TotalSlots - UsedSlots;

        /// <summary>
        /// Grows or shrinks the grid, preserving contents in reading order. Stacks that no longer
        /// fit are returned so the caller can drop them in the world rather than silently voiding them.
        /// </summary>
        public List<ItemStack> Resize(int newWidth, int newHeight)
        {
            var overflow = new List<ItemStack>();
            newWidth = Mathf.Max(1, newWidth);
            newHeight = Mathf.Max(1, newHeight);
            if (newWidth == gridWidth && newHeight == gridHeight) return overflow;

            var surviving = new List<ItemStack>();
            for (int i = 0; i < Slots.Length; i++)
            {
                if (!Slots[i].IsEmpty) surviving.Add(Slots[i]);
            }

            gridWidth = newWidth;
            gridHeight = newHeight;
            AllocateSlots(gridWidth, gridHeight);

            int cursor = 0;
            foreach (var stack in surviving)
            {
                if (cursor < Slots.Length) Slots[cursor++] = stack;
                else overflow.Add(stack);
            }

            SyncCargoWeight();
            OnInventoryRebuilt?.Invoke();
            NotifyAllResources();

            if (overflow.Count > 0)
            {
                Debug.LogWarning($"GridInventoryManager: resize dropped {overflow.Count} stack(s) that no longer fit.");
            }
            return overflow;
        }

        #endregion

        #region Adding items

        /// <summary>Legacy single-unit pickup. True only if the unit was accepted.</summary>
        public bool AddItem(CollectibleItem item) => AddItems(item, 1) == 1;

        /// <summary>
        /// Adds up to <paramref name="quantity"/> units, filling partial stacks first.
        /// Returns how many were actually accepted (0 when nothing fit).
        /// </summary>
        public int AddItems(CollectibleItem item, int quantity)
        {
            if (item == null || quantity <= 0) return 0;

            if (autoUseInstantItems && IsInstantUseItem(item))
            {
                for (int i = 0; i < quantity; i++) ApplyItemEffect(item);
                TrackLifetime(item, quantity);
                OnItemCollected?.Invoke(item, quantity);
                NotifyResource(ResourceIds.Of(item));
                return quantity;
            }

            int remaining = quantity;
            int accepted = 0;

            if (autoStackOnPickup)
            {
                for (int i = 0; i < Slots.Length && remaining > 0; i++)
                {
                    if (!Slots[i].CanStackWith(item)) continue;

                    int room = LimitByWeight(item, Mathf.Min(Slots[i].FreeSpace, remaining));
                    if (room <= 0) continue;

                    Slots[i].Add(room);
                    remaining -= room;
                    accepted += room;
                    RaiseSlotAdded(i, item);
                }
            }

            int maxStack = Mathf.Max(1, item.stackSize);
            for (int i = 0; i < Slots.Length && remaining > 0; i++)
            {
                if (!Slots[i].IsEmpty) continue;

                int room = LimitByWeight(item, Mathf.Min(maxStack, remaining));
                if (room <= 0) break; // out of weight, not out of slots

                Slots[i] = new ItemStack(item, room);
                remaining -= room;
                accepted += room;
                RaiseSlotAdded(i, item);
            }

            if (accepted > 0)
            {
                TrackLifetime(item, accepted);
                SyncCargoWeight();
                OnItemCollected?.Invoke(item, accepted);
                NotifyResource(ResourceIds.Of(item));
            }

            if (remaining > 0)
            {
                string reason = GetEmptySlots() == 0 ? "Inventory full" : "Cargo capacity exceeded";
                OnPickupRejected?.Invoke(item, reason);
            }

            return accepted;
        }

        /// <summary>Places units in one specific slot. Merges if the slot already holds the same item.</summary>
        public bool AddItemAt(CollectibleItem item, int x, int y, int quantity = 1)
        {
            if (item == null || quantity <= 0 || !IsValidPosition(x, y)) return false;

            int i = Index(x, y);
            var stack = Slots[i];
            if (!stack.IsEmpty && stack.item != item) return false;

            int room = stack.IsEmpty ? Mathf.Max(1, item.stackSize) : stack.FreeSpace;
            int toAdd = LimitByWeight(item, Mathf.Min(room, quantity));
            if (toAdd <= 0) return false;

            if (stack.IsEmpty) Slots[i] = new ItemStack(item, toAdd);
            else stack.Add(toAdd);

            TrackLifetime(item, toAdd);
            RaiseSlotAdded(i, item);
            SyncCargoWeight();
            OnItemCollected?.Invoke(item, toAdd);
            NotifyResource(ResourceIds.Of(item));
            return true;
        }

        /// <summary>How many units of this item may be added before the weight budget is hit.</summary>
        private int LimitByWeight(CollectibleItem item, int desired)
        {
            if (desired <= 0) return 0;
            if (!enforceWeightLimit || Rover == null) return desired;
            if (item.weight <= 0f) return desired;

            float headroom = Rover.MaxCargoCapacity - GetTotalWeight();
            if (headroom <= 0f) return 0;

            return Mathf.Min(desired, Mathf.FloorToInt(headroom / item.weight));
        }

        /// <summary>True if the item would be accepted right now (used for pickup prompts).</summary>
        public bool CanAddItem(CollectibleItem item, int quantity = 1)
        {
            if (item == null || quantity <= 0) return false;
            if (autoUseInstantItems && IsInstantUseItem(item)) return true;
            if (LimitByWeight(item, quantity) < quantity) return false;

            int room = 0;
            int maxStack = Mathf.Max(1, item.stackSize);
            for (int i = 0; i < Slots.Length && room < quantity; i++)
            {
                if (Slots[i].IsEmpty) room += maxStack;
                else if (Slots[i].item == item) room += Slots[i].FreeSpace;
            }
            return room >= quantity;
        }

        #endregion

        #region Removing items

        /// <summary>Removes the whole stack at a position. Returns the item type that was there.</summary>
        public CollectibleItem RemoveItemAt(int x, int y)
        {
            var stack = GetStackAt(x, y);
            if (stack == null || stack.IsEmpty) return null;

            CollectibleItem item = stack.item;
            RemoveItemsAt(x, y, stack.quantity);
            return item;
        }

        /// <summary>Removes up to <paramref name="quantity"/> units from one slot. Returns how many went.</summary>
        public int RemoveItemsAt(int x, int y, int quantity)
        {
            var stack = GetStackAt(x, y);
            if (stack == null || stack.IsEmpty || quantity <= 0) return 0;

            CollectibleItem item = stack.item;
            int removed = stack.Remove(quantity);
            if (removed <= 0) return 0;

            if (stack.IsEmpty) OnItemRemovedFromGrid?.Invoke(x, y);
            OnSlotChanged?.Invoke(x, y);

            SyncCargoWeight();
            NotifyResource(ResourceIds.Of(item));
            return removed;
        }

        /// <summary>Removes units of an item from anywhere in the grid. Returns how many were removed.</summary>
        public int RemoveItems(CollectibleItem item, int quantity)
        {
            if (item == null || quantity <= 0) return 0;

            int remaining = quantity;
            for (int y = 0; y < gridHeight && remaining > 0; y++)
            {
                for (int x = 0; x < gridWidth && remaining > 0; x++)
                {
                    if (Slots[Index(x, y)].item != item) continue;
                    remaining -= RemoveItemsAt(x, y, remaining);
                }
            }
            return quantity - remaining;
        }

        /// <summary>Removes units of any item in a category. Returns units removed.</summary>
        public int RemoveItemsOfType(ItemType type, int quantity)
        {
            int remaining = quantity;
            for (int y = 0; y < gridHeight && remaining > 0; y++)
            {
                for (int x = 0; x < gridWidth && remaining > 0; x++)
                {
                    var stack = Slots[Index(x, y)];
                    if (stack.IsEmpty || stack.item.itemType != type) continue;
                    remaining -= RemoveItemsAt(x, y, remaining);
                }
            }
            return quantity - remaining;
        }

        public void DiscardItem(int x, int y)
        {
            var item = GetItemAt(x, y);
            if (item == null) return;

            int amount = GetQuantityAt(x, y);
            RemoveItemsAt(x, y, amount);
            Debug.Log($"Discarded {amount}x {item.itemName}");
        }

        /// <summary>Uses one unit from a slot, applying its effect to the rover.</summary>
        public bool ConsumeItem(int x, int y)
        {
            var item = GetItemAt(x, y);
            if (item == null) return false;

            if (!ApplyItemEffect(item))
            {
                Debug.Log($"{item.itemName} has no direct effect - spend it on an exchange or upgrade instead.");
                return false;
            }

            RemoveItemsAt(x, y, 1);
            return true;
        }

        private bool IsInstantUseItem(CollectibleItem item)
        {
            switch (item.itemType)
            {
                case ItemType.PowerCell:
                case ItemType.Fuel:
                case ItemType.CoolingUnit:
                case ItemType.Consumable:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Applies a single unit's effect. False when the item does nothing on its own.</summary>
        private bool ApplyItemEffect(CollectibleItem item)
        {
            if (Rover == null) return false;

            bool applied = false;

            if (item.powerValue > 0f)
            {
                Rover.ModifyPower(item.powerValue);
                applied = true;
            }

            if (item.heatReduction > 0f)
            {
                Rover.ModifyHeat(-item.heatReduction);
                applied = true;
            }

            if (applied)
            {
                Debug.Log($"Consumed {item.itemName} (+{item.powerValue} power, -{item.heatReduction} heat)");
            }
            return applied;
        }

        #endregion

        #region Moving, splitting, sorting

        /// <summary>
        /// Moves a stack. Merges into a matching stack at the destination, otherwise swaps.
        /// Total weight is unchanged by a move, so no capacity check is needed.
        /// </summary>
        public bool MoveItem(int fromX, int fromY, int toX, int toY)
        {
            if (!IsValidPosition(fromX, fromY) || !IsValidPosition(toX, toY)) return false;
            if (fromX == toX && fromY == toY) return false;

            int from = Index(fromX, fromY);
            int to = Index(toX, toY);

            var source = Slots[from];
            var dest = Slots[to];
            if (source.IsEmpty) return false;

            if (!dest.IsEmpty && dest.item == source.item && dest.FreeSpace > 0)
            {
                int moved = dest.Add(source.quantity);
                source.Remove(moved);
            }
            else
            {
                Slots[from] = dest;
                Slots[to] = source;
            }

            RefreshSlot(fromX, fromY);
            RefreshSlot(toX, toY);
            return true;
        }

        /// <summary>Splits part of a stack into the first free slot. False if there is nowhere to go.</summary>
        public bool SplitStack(int x, int y, int amount)
        {
            var stack = GetStackAt(x, y);
            if (stack == null || stack.IsEmpty || amount <= 0 || amount >= stack.quantity) return false;

            for (int i = 0; i < Slots.Length; i++)
            {
                if (!Slots[i].IsEmpty) continue;

                CollectibleItem item = stack.item;
                stack.Remove(amount);
                Slots[i] = new ItemStack(item, amount);

                RefreshSlot(x, y);
                RefreshSlot(i % gridWidth, i / gridWidth);
                return true;
            }
            return false;
        }

        /// <summary>Merges partial stacks of the same item, then packs everything to the front.</summary>
        public void CompactAndSort()
        {
            var stacks = new List<ItemStack>();
            for (int i = 0; i < Slots.Length; i++)
            {
                if (!Slots[i].IsEmpty) stacks.Add(Slots[i]);
            }

            // Merge same-item partials into the earliest stack.
            for (int i = 0; i < stacks.Count; i++)
            {
                for (int j = i + 1; j < stacks.Count && !stacks[i].IsEmpty; j++)
                {
                    if (stacks[j].IsEmpty || stacks[j].item != stacks[i].item) continue;
                    int moved = stacks[i].Add(stacks[j].quantity);
                    stacks[j].Remove(moved);
                }
            }
            stacks.RemoveAll(s => s.IsEmpty);

            stacks.Sort((a, b) =>
            {
                int byType = a.item.itemType.CompareTo(b.item.itemType);
                if (byType != 0) return byType;
                int byName = string.Compare(a.item.itemName, b.item.itemName, StringComparison.OrdinalIgnoreCase);
                return byName != 0 ? byName : b.quantity.CompareTo(a.quantity);
            });

            AllocateSlots(gridWidth, gridHeight);
            for (int i = 0; i < stacks.Count && i < Slots.Length; i++) Slots[i] = stacks[i];

            OnInventoryRebuilt?.Invoke();
        }

        #endregion

        #region Queries

        public float GetTotalWeight()
        {
            float total = 0f;
            for (int i = 0; i < Slots.Length; i++) total += Slots[i].TotalWeight;
            return total;
        }

        /// <summary>Total units of a specific item asset held.</summary>
        public int CountOf(CollectibleItem item)
        {
            if (item == null) return 0;
            int count = 0;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].item == item) count += Slots[i].quantity;
            }
            return count;
        }

        /// <summary>Total units across every item of a given category.</summary>
        public int CountOfType(ItemType type)
        {
            int count = 0;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (!Slots[i].IsEmpty && Slots[i].item.itemType == type) count += Slots[i].quantity;
            }
            return count;
        }

        public bool HasItem(CollectibleItem item, int quantity = 1) => CountOf(item) >= quantity;

        /// <summary>Every distinct item held, with its total unit count.</summary>
        public Dictionary<CollectibleItem, int> GetContents()
        {
            var contents = new Dictionary<CollectibleItem, int>();
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].IsEmpty) continue;
                contents.TryGetValue(Slots[i].item, out int existing);
                contents[Slots[i].item] = existing + Slots[i].quantity;
            }
            return contents;
        }

        #endregion

        #region Resource ledger

        /// <summary>
        /// Spendable total for a resource: the loose banked amount plus the value locked up in
        /// every held item that contributes to it.
        /// </summary>
        public float GetResource(string resourceType)
        {
            string key = ResourceIds.Normalize(resourceType);
            if (string.IsNullOrEmpty(key)) return 0f;

            bankedResources.TryGetValue(key, out float total);

            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].IsEmpty) continue;
                if (ResourceIds.Of(Slots[i].item) != key) continue;
                total += ResourceIds.ValueOf(Slots[i].item) * Slots[i].quantity;
            }
            return total;
        }

        public bool HasResource(string resourceType, float amount) =>
            GetResource(resourceType) >= amount - 0.0001f;

        /// <summary>Grants loose resource not backed by an item (exchange rewards, mission payouts).</summary>
        public void AddResource(string resourceType, float amount)
        {
            string key = ResourceIds.Normalize(resourceType);
            if (string.IsNullOrEmpty(key) || Mathf.Approximately(amount, 0f)) return;

            bankedResources.TryGetValue(key, out float current);
            bankedResources[key] = current + amount;

            if (amount > 0f)
            {
                lifetimeCollected.TryGetValue(key, out float lifetime);
                lifetimeCollected[key] = lifetime + amount;
            }

            NotifyResource(key);
        }

        /// <summary>
        /// Spends a resource: loose pool first, then real items (lowest unit value first, so the
        /// player loses as little cargo as possible). Fails without side effects if the total is short.
        /// </summary>
        public bool ConsumeResource(string resourceType, float amount)
        {
            string key = ResourceIds.Normalize(resourceType);
            if (string.IsNullOrEmpty(key)) return false;
            if (amount <= 0f) return true;
            if (!HasResource(key, amount)) return false;

            float remaining = amount;

            bankedResources.TryGetValue(key, out float banked);
            float fromBank = Mathf.Min(banked, remaining);
            if (fromBank > 0f)
            {
                bankedResources[key] = banked - fromBank;
                remaining -= fromBank;
            }

            while (remaining > 0.0001f)
            {
                int bestX = -1, bestY = -1;
                float bestValue = float.MaxValue;

                for (int y = 0; y < gridHeight; y++)
                {
                    for (int x = 0; x < gridWidth; x++)
                    {
                        var stack = Slots[Index(x, y)];
                        if (stack.IsEmpty || ResourceIds.Of(stack.item) != key) continue;

                        float value = ResourceIds.ValueOf(stack.item);
                        if (value <= 0f) continue;
                        if (value < bestValue)
                        {
                            bestValue = value;
                            bestX = x;
                            bestY = y;
                        }
                    }
                }

                if (bestX < 0) break; // nothing left that carries this resource

                int unitsNeeded = Mathf.Max(1, Mathf.CeilToInt(remaining / bestValue));
                int removed = RemoveItemsAt(bestX, bestY, unitsNeeded);
                if (removed <= 0) break;
                remaining -= removed * bestValue;
            }

            // Overshoot from an indivisible item comes back as change in the loose pool.
            if (remaining < -0.0001f)
            {
                bankedResources.TryGetValue(key, out float change);
                bankedResources[key] = change - remaining;
            }

            NotifyResource(key);
            return true;
        }

        /// <summary>
        /// Lifetime resource WORTH gathered, ignoring anything spent. One 15-power cell counts 15.
        /// For "collect N items" objectives use GetLifetimeUnits instead.
        /// </summary>
        public float GetLifetimeCollected(string resourceType)
        {
            string key = ResourceIds.Normalize(resourceType);
            return lifetimeCollected.TryGetValue(key, out float value) ? value : 0f;
        }

        /// <summary>
        /// Lifetime number of ITEMS gathered for a resource, ignoring anything spent.
        /// One 15-power cell counts 1. This is what "collect N" objectives want.
        /// </summary>
        public int GetLifetimeUnits(string resourceType)
        {
            string key = ResourceIds.Normalize(resourceType);
            return lifetimeUnits.TryGetValue(key, out int units) ? units : 0;
        }

        private void TrackLifetime(CollectibleItem item, int quantity)
        {
            string key = ResourceIds.Of(item);
            if (string.IsNullOrEmpty(key)) return;

            lifetimeCollected.TryGetValue(key, out float value);
            lifetimeCollected[key] = value + ResourceIds.ValueOf(item) * quantity;

            lifetimeUnits.TryGetValue(key, out int units);
            lifetimeUnits[key] = units + quantity;
        }

        private void NotifyResource(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            OnResourceChanged?.Invoke(key, GetResource(key));
        }

        private void NotifyAllResources()
        {
            foreach (string key in ResourceIds.All) NotifyResource(key);
        }

        #endregion

        #region Notification helpers

        private void RaiseSlotAdded(int index, CollectibleItem item)
        {
            int x = index % gridWidth;
            int y = index / gridWidth;
            OnItemAddedToGrid?.Invoke(item, x, y);
            OnSlotChanged?.Invoke(x, y);
        }

        private void RefreshSlot(int x, int y)
        {
            var stack = Slots[Index(x, y)];
            if (stack.IsEmpty) OnItemRemovedFromGrid?.Invoke(x, y);
            else OnItemAddedToGrid?.Invoke(stack.item, x, y);
            OnSlotChanged?.Invoke(x, y);
        }

        /// <summary>Pushes the real inventory weight onto the rover instead of accumulating deltas.</summary>
        private void SyncCargoWeight()
        {
            if (Rover == null) return;
            Rover.SetCargoWeight(GetTotalWeight());
        }

        #endregion

        #region Open / close

        public void ToggleInventory()
        {
            if (isOpen) CloseInventory();
            else OpenInventory();
        }

        public void OpenInventory()
        {
            if (isOpen) return;
            isOpen = true;
            OnInventoryOpened?.Invoke();
        }

        public void CloseInventory()
        {
            if (!isOpen) return;
            isOpen = false;
            OnInventoryClosed?.Invoke();
        }

        #endregion

        #region Persistence

        public InventorySaveData CaptureState()
        {
            var data = new InventorySaveData { gridWidth = gridWidth, gridHeight = gridHeight };

            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    var stack = Slots[Index(x, y)];
                    if (stack.IsEmpty) continue;

                    data.slots.Add(new SlotSaveData
                    {
                        x = x,
                        y = y,
                        itemName = string.IsNullOrEmpty(stack.item.itemName) ? stack.item.name : stack.item.itemName,
                        quantity = stack.quantity
                    });
                }
            }

            foreach (var pair in bankedResources)
            {
                data.bankedResourceKeys.Add(pair.Key);
                data.bankedResourceValues.Add(pair.Value);
            }
            return data;
        }

        public void RestoreState(InventorySaveData data)
        {
            if (data == null) return;

            gridWidth = Mathf.Max(1, data.gridWidth);
            gridHeight = Mathf.Max(1, data.gridHeight);
            AllocateSlots(gridWidth, gridHeight);

            if (itemDatabase == null)
            {
                Debug.LogError("GridInventoryManager: no ItemDatabase assigned - saved items cannot be restored.");
            }
            else
            {
                foreach (var slot in data.slots)
                {
                    var item = itemDatabase.Find(slot.itemName);
                    if (item == null)
                    {
                        Debug.LogWarning($"GridInventoryManager: save references unknown item '{slot.itemName}'.");
                        continue;
                    }
                    if (IsValidPosition(slot.x, slot.y))
                    {
                        Slots[Index(slot.x, slot.y)] = new ItemStack(item, slot.quantity);
                    }
                }
            }

            bankedResources.Clear();
            foreach (string key in ResourceIds.All) bankedResources[key] = 0f;

            int count = Mathf.Min(data.bankedResourceKeys.Count, data.bankedResourceValues.Count);
            for (int i = 0; i < count; i++)
            {
                bankedResources[data.bankedResourceKeys[i]] = data.bankedResourceValues[i];
            }

            SyncCargoWeight();
            OnInventoryRebuilt?.Invoke();
            NotifyAllResources();
        }

        [ContextMenu("Save Inventory")]
        public void Save()
        {
            PlayerPrefs.SetString(saveKey, JsonUtility.ToJson(CaptureState()));
            PlayerPrefs.Save();
            Debug.Log($"Inventory saved to '{saveKey}'.");
        }

        [ContextMenu("Load Inventory")]
        public void Load()
        {
            if (!PlayerPrefs.HasKey(saveKey))
            {
                Debug.Log($"No inventory save under '{saveKey}'.");
                return;
            }
            RestoreState(JsonUtility.FromJson<InventorySaveData>(PlayerPrefs.GetString(saveKey)));
            Debug.Log($"Inventory loaded from '{saveKey}'.");
        }

        [ContextMenu("Clear Inventory Save")]
        public void ClearSave() => PlayerPrefs.DeleteKey(saveKey);

        #endregion

        #region Debug

        [ContextMenu("Print Inventory")]
        private void DebugPrintInventory()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== INVENTORY GRID ===");

            for (int y = 0; y < gridHeight; y++)
            {
                report.Append($"Row {y}: ");
                for (int x = 0; x < gridWidth; x++)
                {
                    var stack = Slots[Index(x, y)];
                    report.Append(stack.IsEmpty ? "[ Empty ] " : $"[{stack.item.itemName} x{stack.quantity}] ");
                }
                report.AppendLine();
            }

            report.AppendLine($"Slots: {UsedSlots}/{TotalSlots}   Units: {TotalItemCount}");
            report.AppendLine($"Weight: {GetTotalWeight():F1}/{CargoCapacity:F1}");
            report.AppendLine("=== RESOURCES ===");
            foreach (string key in ResourceIds.All)
            {
                report.AppendLine($"{key}: {GetResource(key):F1} (lifetime {GetLifetimeCollected(key):F1})");
            }
            Debug.Log(report.ToString());
        }

        [ContextMenu("Add Test Resources")]
        private void DebugAddResources()
        {
            AddResource(ResourceIds.Materials, 100f);
            AddResource(ResourceIds.Tech, 100f);
            AddResource(ResourceIds.Power, 100f);
        }

        [ContextMenu("Compact And Sort")]
        private void DebugSort() => CompactAndSort();

        #endregion
    }
}
