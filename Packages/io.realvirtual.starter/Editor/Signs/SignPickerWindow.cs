// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace realvirtual
{
    //! Resizable popup picker for choosing a sign visually. Shows a searchable, category-filtered
    //! grid of atlas thumbnails with hover names; clicking a thumbnail selects it on the target Sign
    //! and updates its mesh live.
    public class SignPickerWindow : EditorWindow
    {
        private Sign _target;
        private string _search = "";
        private int _categoryTab; // 0 = All, 1..4 = categories
        private Vector2 _scroll;
        private int _hoverIndex = -1;

        private const float CellSize = 76f;
        private const float CellPad = 6f;

        private static readonly string[] Tabs;

        static SignPickerWindow()
        {
            var tabs = new List<string> { "All" };
            tabs.AddRange(SignAtlasCatalog.Categories.Select(c => c.Label));
            Tabs = tabs.ToArray();
        }

        //! Opens (or focuses) the picker window for the given Sign component.
        public static void Open(Sign target)
        {
            var window = GetWindow<SignPickerWindow>(false, "Select Sign", true);
            window._target = target;
            window.minSize = new Vector2(380, 340);
            window.wantsMouseMove = true;
            window.Focus();
            window.Repaint();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
        }

        private void OnGUI()
        {
            // Repaint on mouse move so the hover highlight / name follow the cursor.
            if (Event.current.type == EventType.MouseMove)
                Repaint();

            if (_target == null)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox("No Sign selected. Open this window from a Sign component's inspector.", MessageType.Info);
                return;
            }

            DrawHeader();
            var list = GetFiltered();
            DrawGrid(list);
            DrawStatusBar(list);
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label($"Sign for: {_target.name}", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label("Search", EditorStyles.miniLabel);
                _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.Width(200)) ?? "";
                if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(22)))
                {
                    _search = "";
                    GUI.FocusControl(null);
                }
            }

            // Category tabs with the active one tinted primary.
            var prev = GUI.backgroundColor;
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < Tabs.Length; i++)
                {
                    GUI.backgroundColor = _categoryTab == i ? EditorUIFactory.ColorPrimary : prev;
                    if (GUILayout.Button(Tabs[i], EditorStyles.miniButton, GUILayout.Height(20)))
                        _categoryTab = i;
                }
            }
            GUI.backgroundColor = prev;
        }

        private List<SignEntry> GetFiltered()
        {
            IEnumerable<SignEntry> entries = SignAtlasCatalog.Entries;

            if (_categoryTab > 0)
            {
                var key = SignAtlasCatalog.Categories[_categoryTab - 1].Key;
                entries = entries.Where(e => e.Category == key);
            }

            if (!string.IsNullOrWhiteSpace(_search))
            {
                var s = _search.Trim().ToLowerInvariant();
                entries = entries.Where(e =>
                    e.DisplayName.ToLowerInvariant().Contains(s) ||
                    e.Name.ToLowerInvariant().Contains(s) ||
                    e.CategoryLabel.ToLowerInvariant().Contains(s));
            }

            return entries.ToList();
        }

        private void DrawGrid(List<SignEntry> list)
        {
            var atlas = SignAtlasCatalog.Atlas;
            float viewWidth = position.width - 18f; // account for vertical scrollbar
            int columns = Mathf.Max(1, Mathf.FloorToInt((viewWidth - CellPad) / (CellSize + CellPad)));
            int rows = Mathf.CeilToInt(list.Count / (float)columns);
            float totalHeight = rows * (CellSize + CellPad) + CellPad;

            bool repaint = Event.current.type == EventType.Repaint;
            int hover = -1;

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var area = GUILayoutUtility.GetRect(viewWidth, totalHeight);

            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                int c = i % columns;
                int r = i / columns;

                float x = area.x + CellPad + c * (CellSize + CellPad);
                float y = area.y + CellPad + r * (CellSize + CellPad);
                var cellRect = new Rect(x, y, CellSize, CellSize);

                bool selected = entry.Index == _target.Index;
                bool hovered = entry.Index == _hoverIndex;

                if (selected)
                    EditorGUI.DrawRect(Grow(cellRect, 2f), EditorUIFactory.ColorPrimary);
                else if (hovered)
                    EditorGUI.DrawRect(cellRect, new Color(1f, 1f, 1f, 0.10f));

                var thumb = new Rect(cellRect.x + 4, cellRect.y + 4, cellRect.width - 8, cellRect.height - 8);
                EditorGUI.DrawRect(thumb, new Color(0.30f, 0.30f, 0.30f));
                if (atlas != null)
                    GUI.DrawTextureWithTexCoords(thumb, atlas, SignAtlasCatalog.TexCoords(entry));

                // Transparent overlay for click + native tooltip.
                if (GUI.Button(cellRect, new GUIContent(string.Empty, entry.DisplayName), GUIStyle.none))
                    Select(entry);

                if (repaint && cellRect.Contains(Event.current.mousePosition))
                    hover = entry.Index;
            }

            EditorGUILayout.EndScrollView();

            if (repaint)
                _hoverIndex = hover;
        }

        private void DrawStatusBar(List<SignEntry> list)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var hover = _hoverIndex >= 0 ? SignAtlasCatalog.Find(_hoverIndex) : null;
                string text = hover != null
                    ? $"{hover.DisplayName}   ·   {hover.CategoryLabel}   ·   #{hover.Index}"
                    : $"{list.Count} sign(s)";
                GUILayout.Label(text, EditorStyles.miniBoldLabel);
            }
        }

        private void Select(SignEntry entry)
        {
            if (_target == null) return;

            // Write through SerializedObject so the change is recorded as a per-instance prefab
            // override (a raw field assignment is NOT, so on a prefab instance it would not stick).
            var so = new SerializedObject(_target);
            so.FindProperty("SignIndex").intValue = entry.Index;
            var nameProp = so.FindProperty("SignName");
            if (nameProp != null) nameProp.stringValue = entry.Name;
            so.ApplyModifiedProperties();   // also registers Undo

            // Per-sign material (created on demand as a Material Variant of SignBase), assigned to
            // the renderer as a per-instance override (prefab-safe + survives GLB export).
            var mat = SignMaterialLibrary.GetOrCreate(entry.Name);
            SignMaterialLibrary.AssignToRenderer(_target.GetComponent<MeshRenderer>(), mat);

            _target.RegenerateMesh();
            EditorUtility.SetDirty(_target);

            if (!Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(_target.gameObject.scene);

            Repaint();
        }

        private static Rect Grow(Rect r, float by)
        {
            return new Rect(r.x - by, r.y - by, r.width + 2 * by, r.height + 2 * by);
        }
    }
}
