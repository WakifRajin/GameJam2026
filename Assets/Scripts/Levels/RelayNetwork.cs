using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// Coordinates a chain of relay towers.
    ///
    /// Owns the "which one next?" question so the HUD, the objectives and the beacon light
    /// all agree, and so a level can have any number of relays instead of the single tower
    /// LevelManager originally assumed.
    /// </summary>
    public class RelayNetwork : MonoBehaviour
    {
        [Header("Relays (in chain order)")]
        [SerializeField] private List<SignalTower> relays = new List<SignalTower>();

        [Header("Beacon")]
        [Tooltip("Pulse the next target's light so it can be found across the map.")]
        [SerializeField] private bool pulseTargetBeacon = true;
        [SerializeField] private float beaconPulseSpeed = 2f;
        [SerializeField] private float beaconMinIntensity = 4f;
        [SerializeField] private float beaconMaxIntensity = 14f;
        [Tooltip("Beacon range while a relay is the active target. Restored when it goes online.")]
        [SerializeField] private float beaconRange = 120f;

        /// <summary>Relay, index. Fired once per relay as it comes online.</summary>
        public event Action<SignalTower, int> OnRelayActivated;
        /// <summary>Fired when every relay in the chain is online.</summary>
        public event Action OnAllRelaysActivated;
        /// <summary>Fired when the current target changes, including at startup.</summary>
        public event Action<SignalTower> OnTargetChanged;

        private SignalTower currentTarget;
        private readonly Dictionary<SignalTower, float> originalLightRange = new Dictionary<SignalTower, float>();

        public IReadOnlyList<SignalTower> Relays => relays;
        public int TotalRelays => relays.Count;

        public int ActivatedCount
        {
            get
            {
                int count = 0;
                foreach (var relay in relays)
                {
                    if (relay != null && relay.IsFullyActivated) count++;
                }
                return count;
            }
        }

        public bool AllActivated => TotalRelays > 0 && ActivatedCount >= TotalRelays;

        /// <summary>The next relay the player can actually work on: unlocked and not yet online.</summary>
        public SignalTower CurrentTarget => currentTarget;

        private void Start()
        {
            if (relays.Count == 0)
            {
                // Convenience for a level that just drops towers in the scene.
                relays.AddRange(FindObjectsByType<SignalTower>(FindObjectsSortMode.None));
                Debug.Log($"[RelayNetwork] No relays assigned; found {relays.Count} in the scene.");
            }

            for (int i = 0; i < relays.Count; i++)
            {
                var relay = relays[i];
                if (relay == null) continue;

                if (relay.TowerLight != null) originalLightRange[relay] = relay.TowerLight.range;

                int index = i;
                relay.OnTowerActivated += () => HandleRelayActivated(relay, index);
            }

            RecalculateTarget();
        }

        private void Update()
        {
            if (pulseTargetBeacon) DriveBeacon();
        }

        private void HandleRelayActivated(SignalTower relay, int index)
        {
            Debug.Log($"[RelayNetwork] {relay.RelayName} online ({ActivatedCount}/{TotalRelays})");

            RestoreLight(relay);
            OnRelayActivated?.Invoke(relay, index);

            RecalculateTarget();

            if (AllActivated)
            {
                Debug.Log("[RelayNetwork] === ENTIRE RELAY CHAIN ONLINE ===");
                OnAllRelaysActivated?.Invoke();
            }
        }

        /// <summary>First relay that is neither online nor locked behind an offline prerequisite.</summary>
        private void RecalculateTarget()
        {
            SignalTower next = null;
            foreach (var relay in relays)
            {
                if (relay == null || relay.IsFullyActivated || relay.IsLocked) continue;
                next = relay;
                break;
            }

            if (next == currentTarget) return;

            if (currentTarget != null) RestoreLight(currentTarget);

            currentTarget = next;
            OnTargetChanged?.Invoke(currentTarget);

            if (currentTarget != null)
            {
                Debug.Log($"[RelayNetwork] Next target: {currentTarget.RelayName} ({currentTarget.GetRequirementSummary()})");
            }
        }

        private void DriveBeacon()
        {
            if (currentTarget == null || currentTarget.TowerLight == null) return;

            float t = (Mathf.Sin(Time.time * beaconPulseSpeed) + 1f) * 0.5f;
            currentTarget.TowerLight.intensity = Mathf.Lerp(beaconMinIntensity, beaconMaxIntensity, t);
            currentTarget.TowerLight.range = beaconRange;
        }

        /// <summary>Hands the light back to the tower's own state visuals.</summary>
        private void RestoreLight(SignalTower relay)
        {
            if (relay == null || relay.TowerLight == null) return;
            if (originalLightRange.TryGetValue(relay, out float range)) relay.TowerLight.range = range;
        }

        /// <summary>Metres from a position to the current target, or -1 when the chain is done.</summary>
        public float DistanceToTarget(Vector3 from)
        {
            if (currentTarget == null) return -1f;
            return Vector3.Distance(from, currentTarget.transform.position);
        }

        [ContextMenu("Print Relay Chain")]
        private void DebugPrintChain()
        {
            var report = new System.Text.StringBuilder($"=== RELAY CHAIN ({ActivatedCount}/{TotalRelays}) ===\n");
            foreach (var relay in relays)
            {
                if (relay == null) { report.AppendLine("  <missing>"); continue; }
                string state = relay.IsFullyActivated ? "ONLINE" : relay.IsLocked ? "LOCKED" : relay.CurrentState.ToString();
                report.AppendLine($"  {relay.RelayName}: {state} - {relay.GetRequirementSummary()}");
            }
            report.AppendLine($"  target: {(currentTarget != null ? currentTarget.RelayName : "none")}");
            Debug.Log(report.ToString());
        }
    }
}
