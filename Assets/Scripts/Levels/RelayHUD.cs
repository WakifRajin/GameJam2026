using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GameJam2026
{
    /// <summary>
    /// The Level 2 hook, made legible: which relay is next, how far, what it costs, and how
    /// long until the sun goes down.
    ///
    /// Solar only recharges while parked in daylight, so "nightfall in 14s" plus "relay is
    /// 400m away" is the decision the whole level is built around. Builds its own UI if the
    /// fields are left empty, so a scene only needs the component.
    /// </summary>
    public class RelayHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RelayNetwork relayNetwork;
        [SerializeField] private DayNightCycle dayNightCycle;
        [SerializeField] private RoverAttributeManager rover;
        [Tooltip("Parent canvas. Found automatically if left empty.")]
        [SerializeField] private Canvas targetCanvas;

        [Header("UI (auto-created when empty)")]
        [SerializeField] private TextMeshProUGUI chainText;
        [SerializeField] private TextMeshProUGUI targetText;
        [SerializeField] private TextMeshProUGUI requirementText;
        [SerializeField] private TextMeshProUGUI clockText;
        [SerializeField] private RectTransform directionArrow;

        [Header("Colours")]
        [SerializeField] private Color dayColor = new Color(1f, 0.85f, 0.35f);
        [SerializeField] private Color nightColor = new Color(0.45f, 0.65f, 1f);
        [SerializeField] private Color urgentColor = new Color(1f, 0.35f, 0.3f);
        [Tooltip("Seconds left before the nightfall countdown turns red.")]
        [SerializeField] private float urgentThreshold = 10f;

        private Transform roverTransform;
        private Camera mainCamera;

        private void Start()
        {
            if (relayNetwork == null) relayNetwork = FindObjectOfType<RelayNetwork>();
            if (dayNightCycle == null) dayNightCycle = FindObjectOfType<DayNightCycle>();
            if (rover == null) rover = FindObjectOfType<RoverAttributeManager>();
            if (rover != null) roverTransform = rover.transform;

            if (chainText == null) BuildUI();

            if (relayNetwork != null)
            {
                relayNetwork.OnTargetChanged += _ => RefreshTarget();
                relayNetwork.OnAllRelaysActivated += HandleAllOnline;
            }

            RefreshTarget();
        }

        private void LateUpdate()
        {
            mainCamera = mainCamera != null && mainCamera.isActiveAndEnabled ? mainCamera : Camera.main;

            UpdateClock();
            UpdateTargetReadout();
        }

        #region Readouts

        private void UpdateClock()
        {
            if (clockText == null || dayNightCycle == null) return;

            float seconds = dayNightCycle.SecondsUntilTransition(out bool nightIsComing);

            if (nightIsComing)
            {
                clockText.text = $"NIGHTFALL IN {seconds:F0}s";
                clockText.color = seconds <= urgentThreshold ? urgentColor : dayColor;
            }
            else
            {
                // No solar at night - the rover is running on what it already has.
                clockText.text = $"NIGHT - DAWN IN {seconds:F0}s";
                clockText.color = nightColor;
            }
        }

        private void UpdateTargetReadout()
        {
            if (relayNetwork == null) return;

            var target = relayNetwork.CurrentTarget;

            if (target == null)
            {
                if (targetText != null) targetText.text = "ALL RELAYS ONLINE";
                if (requirementText != null) requirementText.text = string.Empty;
                if (directionArrow != null) directionArrow.gameObject.SetActive(false);
                return;
            }

            if (roverTransform != null && targetText != null)
            {
                float distance = Vector3.Distance(roverTransform.position, target.transform.position);
                targetText.text = $"NEXT: {target.RelayName}  -  {distance:F0} m";
            }

            if (requirementText != null) requirementText.text = target.GetRequirementSummary();

            PointArrowAt(target.transform.position);
        }

        /// <summary>Rotates the chevron to the target's bearing relative to where the camera looks.</summary>
        private void PointArrowAt(Vector3 worldPosition)
        {
            if (directionArrow == null || roverTransform == null || mainCamera == null) return;

            if (!directionArrow.gameObject.activeSelf) directionArrow.gameObject.SetActive(true);

            Vector3 toTarget = worldPosition - roverTransform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f) return;

            Vector3 forward = mainCamera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) return;

            float bearing = Vector3.SignedAngle(forward.normalized, toTarget.normalized, Vector3.up);
            directionArrow.localRotation = Quaternion.Euler(0f, 0f, -bearing);
        }

        private void RefreshTarget()
        {
            if (chainText == null || relayNetwork == null) return;
            chainText.text = $"RELAY CHAIN  {relayNetwork.ActivatedCount}/{relayNetwork.TotalRelays}";
        }

        private void HandleAllOnline()
        {
            RefreshTarget();
            if (targetText != null) targetText.text = "ALL RELAYS ONLINE";
            if (requirementText != null) requirementText.text = "Signal away. Hold on.";
            if (directionArrow != null) directionArrow.gameObject.SetActive(false);
        }

        #endregion

        #region Auto-built UI

        private void BuildUI()
        {
            if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>() ?? FindObjectOfType<Canvas>();
            if (targetCanvas == null)
            {
                Debug.LogError("RelayHUD: no Canvas found; cannot build the relay readout.");
                return;
            }

            var panel = new GameObject("RelayHUD", typeof(RectTransform));
            panel.transform.SetParent(targetCanvas.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -12f);
            panelRect.sizeDelta = new Vector2(520f, 132f);

            var background = panel.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.55f);
            background.raycastTarget = false;

            chainText       = MakeLabel(panelRect, "ChainText",  new Vector2(0f, -8f),  28f, new Color(0.8f, 0.9f, 1f));
            targetText      = MakeLabel(panelRect, "TargetText", new Vector2(0f, -44f), 24f, Color.white);
            requirementText = MakeLabel(panelRect, "ReqText",    new Vector2(0f, -74f), 19f, new Color(0.85f, 0.85f, 0.85f));
            clockText       = MakeLabel(panelRect, "ClockText",  new Vector2(0f, -102f), 22f, dayColor);

            var arrow = new GameObject("DirectionArrow", typeof(RectTransform));
            arrow.transform.SetParent(panelRect, false);
            directionArrow = (RectTransform)arrow.transform;
            directionArrow.anchorMin = new Vector2(0f, 1f);
            directionArrow.anchorMax = new Vector2(0f, 1f);
            directionArrow.pivot = new Vector2(0.5f, 0.5f);
            directionArrow.anchoredPosition = new Vector2(42f, -56f);
            directionArrow.sizeDelta = new Vector2(56f, 56f);

            var arrowText = arrow.AddComponent<TextMeshProUGUI>();
            arrowText.text = "^";
            arrowText.fontSize = 44f;
            arrowText.color = new Color(0.4f, 1f, 0.6f);
            arrowText.alignment = TextAlignmentOptions.Center;
            arrowText.raycastTarget = false;

            Debug.Log("RelayHUD: built its own readout under " + targetCanvas.name);
        }

        private TextMeshProUGUI MakeLabel(RectTransform parent, string name, Vector2 position, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(76f, position.y - size - 6f);
            rect.offsetMax = new Vector2(-12f, position.y);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            label.text = string.Empty;
            return label;
        }

        #endregion
    }
}
