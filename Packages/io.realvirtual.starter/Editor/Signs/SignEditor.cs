// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEditor;
using UnityEngine;

namespace realvirtual
{
    //! Custom inspector for the Sign component: shows the current sign preview, opens the picker
    //! window and assigns a dedicated per-icon material (a Material Variant of SignBase) created on
    //! demand. The per-sign material is what survives GLB export.
    [CustomEditor(typeof(Sign))]
    [CanEditMultipleObjects]
    public class SignEditor : UnityEditor.Editor
    {
        private void OnEnable()
        {
            foreach (var t in targets)
                EnsureSetup(t as Sign, false);

            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        //! Re-applies size after an undo/redo so the displayed sign matches the restored state.
        private void OnUndoRedo()
        {
            foreach (var t in targets)
                (t as Sign)?.RegenerateMesh();
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            var sign = (Sign)target;
            var atlas = SignAtlasCatalog.Atlas;
            var entry = SignAtlasCatalog.Find(sign.Index);

            // --- Current sign preview ---------------------------------------------------------
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                var rect = GUILayoutUtility.GetRect(96, 96, GUILayout.Width(96), GUILayout.Height(96));
                EditorGUI.DrawRect(rect, new Color(0.32f, 0.32f, 0.32f));
                if (atlas != null)
                    GUI.DrawTextureWithTexCoords(rect, atlas, SignAtlasCatalog.TexCoords(sign.Index));
                GUI.Box(rect, GUIContent.none);

                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.FlexibleSpace();
                    var nameStyle = new GUIStyle(EditorStyles.boldLabel) { wordWrap = true };
                    EditorGUILayout.LabelField(entry != null ? entry.DisplayName : "(no sign)", nameStyle);
                    EditorGUILayout.LabelField(entry != null ? entry.CategoryLabel : "—", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"Index {sign.Index}", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                }
            }

            EditorGUILayout.Space(4);

            // --- Choose sign ------------------------------------------------------------------
            var prevColor = GUI.backgroundColor;
            GUI.backgroundColor = EditorUIFactory.ColorPrimary;
            if (GUILayout.Button("Choose Sign…", GUILayout.Height(26)))
                SignPickerWindow.Open(sign);
            GUI.backgroundColor = prevColor;

            EditorGUILayout.Space(6);

            // --- Quad size --------------------------------------------------------------------
            serializedObject.Update();
            var sizeProp = serializedObject.FindProperty("Size");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.Slider(sizeProp, Sign.MinSize, Sign.MaxSize, new GUIContent("Size"));
            bool sizeChanged = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();

            float meters = sizeProp.floatValue * Sign.MaxSizeMeters;
            EditorGUILayout.LabelField("Edge length", $"{meters:0.000} m", EditorStyles.miniLabel);

            if (sizeChanged)
            {
                foreach (var t in targets)
                    (t as Sign)?.RegenerateMesh();
            }

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Assign / Rebuild Material"))
            {
                foreach (var t in targets)
                    EnsureSetup(t as Sign, true);
            }
        }

        //! Ensures SignBase is configured, the geometry/size is applied, and (when forced or when the
        //! renderer has no proper material yet) the current sign's per-icon material is assigned.
        public static void EnsureSetup(Sign sign, bool force)
        {
            if (sign == null) return;

            SignMaterialLibrary.EnsureBaseConfigured();
            sign.RegenerateMesh();

            var renderer = sign.GetComponent<MeshRenderer>();
            if (renderer == null) return;

            // Only auto-assign when the renderer is "fresh" (no material / a Unity default); never
            // clobber a deliberately assigned material on selection. The button forces assignment.
            if (!force && !IsDefaultOrMissing(renderer.sharedMaterial))
                return;

            // Resolve the sign name (stored on the component, else from the current index).
            string name = sign.DisplayedSignName;
            if (string.IsNullOrEmpty(name))
            {
                var entry = SignAtlasCatalog.Find(sign.Index);
                name = entry != null ? entry.Name : null;
            }
            if (string.IsNullOrEmpty(name)) return;

            var mat = SignMaterialLibrary.GetOrCreate(name);
            if (mat == null) return;

            SignMaterialLibrary.AssignToRenderer(renderer, mat);

            // Persist the resolved name on the component if it was empty.
            if (string.IsNullOrEmpty(sign.DisplayedSignName))
            {
                var so = new SerializedObject(sign);
                var np = so.FindProperty("SignName");
                if (np != null)
                {
                    np.stringValue = name;
                    so.ApplyModifiedProperties();
                }
            }
        }

        //! True when the material is unassigned or a Unity/pipeline built-in default.
        private static bool IsDefaultOrMissing(Material mat)
        {
            if (mat == null) return true;
            var path = AssetDatabase.GetAssetPath(mat);
            if (string.IsNullOrEmpty(path)) return false; // an in-scene instance: leave it alone
            return path.Contains("unity_builtin_extra")
                   || path.Contains("unity default resources")
                   || mat.name == "Default-Material"
                   || mat.name == "Lit";
        }
    }
}
