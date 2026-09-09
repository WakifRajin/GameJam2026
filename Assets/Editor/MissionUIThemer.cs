using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GameJam2026.EditorTools
{
    /// <summary>
    /// One-shot restyle of a level's UI onto the shared palette.
    ///
    /// The HUD was assembled panel by panel over time, so it had four different backgrounds,
    /// pure-green bars and a cream-coloured menu skin that shared nothing with the in-game
    /// chrome. Rather than hand-editing every element in every scene, this walks the known
    /// panels and applies UIPalette consistently. Re-runnable and idempotent.
    ///
    /// Run from: GameJam2026 > UI > Apply Mission Theme (Open Scene)
    /// </summary>
    public static class MissionUIThemer
    {
        [MenuItem("GameJam2026/UI/Apply Mission Theme (Open Scene)")]
        public static void ApplyToOpenScene()
        {
            int changes = 0;

            changes += ThemeHudPanels();
            changes += ThemeStatPanel("PowerPanel", "Battery", UIPalette.Power);
            changes += ThemeStatPanel("HeatPanel", "Core Temp", UIPalette.Heat);
            changes += ThemeStatPanel("CommPanel", "Signal", UIPalette.Comm);
            changes += ThemeObjectivesBlock();
            changes += ThemeControlsPanel();
            changes += ThemeRelayHud();
            changes += ThemeTowerPrompt();
            changes += ThemeFullScreenPanels();
            changes += ThemeInventory();

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[MissionUIThemer] Applied theme to '{scene.name}' - {changes} element(s) restyled.");
        }

        #region Helpers

        private static GameObject Find(string path)
        {
            var go = GameObject.Find(path);
            if (go != null) return go;

            // Fall back to a name search that also reaches inactive objects.
            foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!candidate.scene.IsValid()) continue;
                if (candidate.name == path) return candidate;
            }
            return null;
        }

        private static int SetImage(GameObject go, Color color)
        {
            if (go == null) return 0;
            var img = go.GetComponent<Image>();
            if (img == null) return 0;
            Undo.RecordObject(img, "Theme UI");
            img.color = color;
            EditorUtility.SetDirty(img);
            return 1;
        }

        private static int SetText(TextMeshProUGUI t, Color color, float? size = null, FontStyles? style = null)
        {
            if (t == null) return 0;
            Undo.RecordObject(t, "Theme UI");
            t.color = color;
            if (size.HasValue) t.fontSize = size.Value;
            if (style.HasValue) t.fontStyle = style.Value;
            EditorUtility.SetDirty(t);
            return 1;
        }

        private static TextMeshProUGUI Child(GameObject parent, string name) =>
            parent == null ? null : parent.transform.Find(name)?.GetComponent<TextMeshProUGUI>();

        #endregion

        /// <summary>Every HUD panel gets the same background, so they read as one system.</summary>
        private static int ThemeHudPanels()
        {
            int n = 0;
            foreach (var name in new[] { "PowerPanel", "HeatPanel", "CommPanel", "ObjectiveTitle",
                                         "ObjectivesPanel", "RelayHUD", "TowerInteractionPanel" })
            {
                n += SetImage(Find(name), UIPalette.PanelBackground);
            }
            return n;
        }

        /// <summary>
        /// A rover stat readout: dark track, accent fill, label left in muted type, value right
        /// in the accent colour. Previously all three used white-on-translucent-white.
        /// </summary>
        private static int ThemeStatPanel(string panelName, string label, Color accent)
        {
            var panel = Find(panelName);
            if (panel == null) return 0;
            int n = 0;

            // The bare "Image" child under each stat panel is the bar track.
            var track = panel.transform.Find("Image")?.gameObject;
            n += SetImage(track, UIPalette.BarTrack);

            foreach (var img in panel.GetComponentsInChildren<Image>(true))
            {
                if (img.gameObject == panel) continue;
                if (img.type == Image.Type.Filled || img.name.ToLower().Contains("fill"))
                {
                    Undo.RecordObject(img, "Theme UI");
                    img.color = accent;
                    EditorUtility.SetDirty(img);
                    n++;
                }
                if (img.name.EndsWith("Icon"))
                {
                    Undo.RecordObject(img, "Theme UI");
                    img.color = accent;

                    // Image tint MULTIPLIES, so a black glyph stays black no matter the colour.
                    // The source icons are pure black, so swap in the whitened variant that
                    // sits beside them; without this the icons render invisible on dark panels.
                    if (img.sprite != null && !img.sprite.name.EndsWith("_white"))
                    {
                        string whitePath = $"Assets/Icons/{img.sprite.name}_white.png";
                        var white = AssetDatabase.LoadAssetAtPath<Sprite>(whitePath);
                        if (white != null) img.sprite = white;
                    }

                    EditorUtility.SetDirty(img);
                    n++;
                }
            }

            foreach (var slider in panel.GetComponentsInChildren<Slider>(true))
            {
                if (slider.fillRect != null)
                {
                    var fill = slider.fillRect.GetComponent<Image>();
                    if (fill != null)
                    {
                        Undo.RecordObject(fill, "Theme UI");
                        fill.color = accent;
                        EditorUtility.SetDirty(fill);
                        n++;
                    }
                }
                if (slider.targetGraphic is Image bg)
                {
                    Undo.RecordObject(bg, "Theme UI");
                    bg.color = UIPalette.BarTrack;
                    EditorUtility.SetDirty(bg);
                    n++;
                }
            }

            // Label muted and small, value bold in the accent hue.
            foreach (var t in panel.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                bool isValue = t.name.Contains("Percentage") || t.name.Contains("Percent");
                if (isValue)
                {
                    n += SetText(t, accent, 26f, FontStyles.Bold);
                    t.alignment = TextAlignmentOptions.MidlineRight;
                }
                else
                {
                    n += SetText(t, UIPalette.TextSecondary, 18f, FontStyles.Normal);
                    t.alignment = TextAlignmentOptions.MidlineLeft;
                    if (!string.IsNullOrEmpty(label)) t.text = label;
                }
                EditorUtility.SetDirty(t);
            }
            return n;
        }

        /// <summary>Level title block + the timer, which was 16pt pure green jammed on a bar.</summary>
        private static int ThemeObjectivesBlock()
        {
            int n = 0;
            var title = Find("ObjectiveTitle");
            if (title != null)
            {
                var texts = title.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in texts)
                {
                    if (t.name == "Text (TMP) (1)")
                    {
                        // The countdown - now the most prominent thing in the block.
                        n += SetText(t, UIPalette.TextPrimary, 30f, FontStyles.Bold);
                        t.alignment = TextAlignmentOptions.MidlineLeft;
                    }
                    else if (t.fontSize >= 25f)
                    {
                        n += SetText(t, UIPalette.TextPrimary, 30f, FontStyles.Bold);
                    }
                    else
                    {
                        n += SetText(t, UIPalette.TextSecondary, 17f, FontStyles.Normal);
                    }
                }

                var bar = title.transform.Find("time")?.GetComponent<Image>();
                if (bar != null)
                {
                    Undo.RecordObject(bar, "Theme UI");
                    bar.color = UIPalette.Success; // LevelUIManager recolours this as time drains
                    EditorUtility.SetDirty(bar);
                    n++;
                }
            }

            n += SetImage(Find("ObjectivesPanel"), UIPalette.PanelBackgroundSoft);

            // The per-objective rows are spawned from a prefab at runtime; theme the prefab too.
            var prefabPath = AssetDatabase.FindAssets("t:GameObject ObjectiveItem");
            foreach (var guid in prefabPath)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                bool dirty = false;
                var bg = root.GetComponent<Image>();
                if (bg != null) { bg.color = UIPalette.WithAlpha(UIPalette.BarTrack, 0.55f); dirty = true; }

                foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (t.name == "Progress") { t.color = UIPalette.TextSecondary; t.fontSize = 18f; }
                    else { t.color = UIPalette.TextPrimary; t.fontSize = 19f; }
                    dirty = true;
                }
                if (dirty)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    n++;
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
            return n;
        }

        /// <summary>The controls list dominated the top-right at 25pt. Quiet it down.</summary>
        private static int ThemeControlsPanel()
        {
            var panel = Find("Panel");
            if (panel == null) return 0;

            int n = SetImage(panel, UIPalette.PanelBackgroundSoft);
            foreach (var t in panel.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                n += SetText(t, UIPalette.TextMuted, 17f, FontStyles.Normal);
            }
            return n;
        }

        private static int ThemeRelayHud()
        {
            var hud = Find("RelayHUD");
            if (hud == null) return 0;

            int n = SetImage(hud, UIPalette.PanelBackground);
            n += SetText(Child(hud, "ChainText"), UIPalette.Comm, 26f, FontStyles.Bold);
            n += SetText(Child(hud, "TargetText"), UIPalette.TextPrimary, 23f, FontStyles.Normal);
            n += SetText(Child(hud, "ReqText"), UIPalette.TextSecondary, 18f, FontStyles.Normal);

            var arrow = Child(hud, "DirectionArrow");
            if (arrow != null)
            {
                n += SetText(arrow, UIPalette.Success, 46f, FontStyles.Bold);
                arrow.text = "▲"; // solid triangle reads better than a caret
            }
            return n;
        }

        private static int ThemeTowerPrompt()
        {
            var panel = Find("TowerInteractionPanel");
            if (panel == null) return 0;

            int n = SetImage(panel, UIPalette.PanelBackgroundHeavy);
            foreach (var t in panel.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                bool header = t.name == "Text (TMP)";
                n += SetText(t, header ? UIPalette.Comm : UIPalette.TextPrimary,
                             header ? 18f : 21f, header ? FontStyles.Bold : FontStyles.Normal);
            }
            return n;
        }

        /// <summary>Pause / victory / defeat were a cream skin with black text. Bring them in.</summary>
        private static int ThemeFullScreenPanels()
        {
            int n = 0;
            foreach (var (name, accent) in new[]
            {
                ("PauseMenu", UIPalette.TextPrimary),
                ("VictoryPanel", UIPalette.Success),
                ("DefeatPanel", UIPalette.Danger),
            })
            {
                var panel = Find(name);
                if (panel == null) continue;

                n += SetImage(panel, UIPalette.PanelBackgroundHeavy);

                foreach (var t in panel.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    bool isHeadline = t.fontSize >= 50f;
                    n += SetText(t, isHeadline ? accent : UIPalette.TextPrimary,
                                 isHeadline ? 72f : 24f,
                                 isHeadline ? FontStyles.Bold : FontStyles.Normal);
                }

                foreach (var b in panel.GetComponentsInChildren<Button>(true))
                {
                    var img = b.GetComponent<Image>();
                    if (img == null) continue;

                    Undo.RecordObject(img, "Theme UI");
                    img.color = UIPalette.ButtonFace;
                    EditorUtility.SetDirty(img);

                    Undo.RecordObject(b, "Theme UI");
                    var colors = b.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
                    colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                    colors.selectedColor = colors.highlightedColor;
                    b.colors = colors;
                    EditorUtility.SetDirty(b);
                    n++;
                }
            }
            return n;
        }

        private static int ThemeInventory()
        {
            int n = 0;
            n += SetImage(Find("InventoryPanel"), UIPalette.PanelBackgroundHeavy);
            n += SetImage(Find("InventoryContainer"), UIPalette.PanelBackgroundSoft);
            n += SetImage(Find("UpgradeConteiner"), UIPalette.PanelBackgroundSoft);
            n += SetImage(Find("ItemInfoPanel"), UIPalette.WithAlpha(UIPalette.BarTrack, 0.6f));
            n += SetImage(Find("ActionButtonsPanel"), Color.clear);
            n += SetImage(Find("ResourceDisplay"), UIPalette.WithAlpha(UIPalette.BarTrack, 0.6f));

            foreach (var name in new[] { "materialsText", "techText", "powerText" })
            {
                var t = Find(name)?.GetComponent<TextMeshProUGUI>();
                Color c = name.StartsWith("materials") ? UIPalette.TextPrimary
                        : name.StartsWith("tech") ? UIPalette.Comm : UIPalette.Power;
                n += SetText(t, c, 20f, FontStyles.Bold);
            }
            return n;
        }
    }
}
