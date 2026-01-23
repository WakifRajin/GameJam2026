using UnityEngine;
using UnityEngine.UI;

namespace GameJam2026
{
    /// <summary>
    /// WiFi-style signal bars for communication range visualization
    /// </summary>
    public class SignalBarsUI : MonoBehaviour
    {
        [Header("Signal Bar Images")]
        [SerializeField] private Image[] signalBars; // Assign 4 or 5 bar images
        
        [Header("Colors")]
        [SerializeField] private Color excellentSignal = Color.green;
        [SerializeField] private Color goodSignal = Color.yellow;
        [SerializeField] private Color weakSignal = new Color(1f, 0.5f, 0f); // Orange
        [SerializeField] private Color noSignal = Color.red;
        [SerializeField] private Color inactiveBarColor = new Color(0.3f, 0.3f, 0.3f, 0.3f); // Dim gray

        [Header("Animation")]
        [SerializeField] private bool animateBars = true;
        [SerializeField] private float animationSpeed = 5f;

        private float currentSignalStrength = 1f; // 0 to 1
        private float[] targetBarAlpha;

        private void Awake()
        {
            if (signalBars != null)
            {
                targetBarAlpha = new float[signalBars.Length];
            }
        }

        private void Update()
        {
            if (animateBars)
            {
                AnimateBars();
            }
        }

        /// <summary>
        /// Update signal strength (0 to 1, where 1 is full signal)
        /// </summary>
        public void UpdateSignalStrength(float strength)
        {
            currentSignalStrength = Mathf.Clamp01(strength);
            UpdateBars();
        }

        private void UpdateBars()
        {
            if (signalBars == null || signalBars.Length == 0) return;

            int totalBars = signalBars.Length;
            int activeBars = Mathf.RoundToInt(currentSignalStrength * totalBars);

            // Determine color based on signal strength
            Color signalColor = GetSignalColor(currentSignalStrength);

            for (int i = 0; i < totalBars; i++)
            {
                if (signalBars[i] == null) continue;

                if (i < activeBars)
                {
                    // Active bar
                    targetBarAlpha[i] = 1f;
                    
                    if (!animateBars)
                    {
                        signalBars[i].color = signalColor;
                    }
                }
                else
                {
                    // Inactive bar
                    targetBarAlpha[i] = 0f;
                    
                    if (!animateBars)
                    {
                        signalBars[i].color = inactiveBarColor;
                    }
                }
            }
        }

        private void AnimateBars()
        {
            if (signalBars == null) return;

            Color signalColor = GetSignalColor(currentSignalStrength);

            for (int i = 0; i < signalBars.Length; i++)
            {
                if (signalBars[i] == null) continue;

                float currentAlpha = signalBars[i].color.a;
                float newAlpha = Mathf.Lerp(currentAlpha, targetBarAlpha[i], Time.deltaTime * animationSpeed);

                if (targetBarAlpha[i] > 0.5f)
                {
                    // Active bar - lerp to signal color
                    Color targetColor = signalColor;
                    targetColor.a = newAlpha;
                    signalBars[i].color = Color.Lerp(signalBars[i].color, targetColor, Time.deltaTime * animationSpeed);
                }
                else
                {
                    // Inactive bar - lerp to inactive color
                    Color targetColor = inactiveBarColor;
                    targetColor.a = newAlpha;
                    signalBars[i].color = Color.Lerp(signalBars[i].color, targetColor, Time.deltaTime * animationSpeed);
                }
            }
        }

        private Color GetSignalColor(float strength)
        {
            if (strength >= 0.75f)
                return excellentSignal;
            else if (strength >= 0.5f)
                return goodSignal;
            else if (strength >= 0.25f)
                return weakSignal;
            else
                return noSignal;
        }

        /// <summary>
        /// Get the current number of active bars
        /// </summary>
        public int GetActiveBars()
        {
            if (signalBars == null) return 0;
            return Mathf.RoundToInt(currentSignalStrength * signalBars.Length);
        }

        /// <summary>
        /// Force immediate update without animation
        /// </summary>
        public void ForceUpdate(float strength)
        {
            currentSignalStrength = Mathf.Clamp01(strength);
            
            if (signalBars == null || signalBars.Length == 0) return;

            int totalBars = signalBars.Length;
            int activeBars = Mathf.RoundToInt(currentSignalStrength * totalBars);
            Color signalColor = GetSignalColor(currentSignalStrength);

            for (int i = 0; i < totalBars; i++)
            {
                if (signalBars[i] == null) continue;

                if (i < activeBars)
                {
                    signalBars[i].color = signalColor;
                    targetBarAlpha[i] = 1f;
                }
                else
                {
                    signalBars[i].color = inactiveBarColor;
                    targetBarAlpha[i] = 0f;
                }
            }
        }
    }
}