// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    #region doc
    //! Displays a single safety sign on a quad using a dedicated material per icon.

    //! The Sign component shows one of the 248 ISO safety-sign pictograms. The pictogram is provided
    //! by the renderer's material: in the editor, choosing a sign creates (on demand) a Material
    //! Variant of SignBase whose base map is that sign's icon, and assigns it to this object. Because
    //! the icon lives in a real sharedMaterial, it survives GLB export (each sign becomes a distinct
    //! glTF material + texture in realvirtual WEB).
    //!
    //! The size is applied through the local scale. The mesh is not managed by this component -
    //! assign your own mesh to the MeshFilter (e.g. a quad).
    //!
    //! Key Features:
    //! - One dedicated material per icon (GLB-export friendly, prefab-safe)
    //! - Size from 0.05 m to 0.5 m via local scale (mesh is user-assigned)
    //! - Live update in the editor (selection via the Sign picker window)
    //!
    //! To pick a sign in the editor, use the "Choose Sign..." button in the inspector.
    //! For detailed documentation see: https://doc.realvirtual.io/components-and-scripts/display/sign
    #endregion
    [SelectionBase]
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [AddComponentMenu("realvirtual/Display/Sign")]
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/display/sign")]
    public class Sign : realvirtualBehavior
    {
        //! Number of columns in the sign atlas grid (used by the editor picker thumbnails).
        public const int AtlasColumns = 16;

        //! Number of rows in the sign atlas grid (used by the editor picker thumbnails).
        public const int AtlasRows = 16;

        //! Total number of cells in the atlas grid (some trailing cells may be empty).
        public const int AtlasCellCount = AtlasColumns * AtlasRows;

        //! Minimum value of the size slider.
        public const float MinSize = 0.1f;

        //! Maximum value of the size slider.
        public const float MaxSize = 1f;

        //! Sign edge length in meters at Size = MaxSize (Size = MinSize gives 0.05 m).
        public const float MaxSizeMeters = 0.5f;

        [SerializeField] private int SignIndex = 0; //!< Atlas cell index of the displayed sign (0-based, row-major). Picker source of truth.
        [SerializeField] private string SignName; //!< Name of the displayed sign (matches the icon/material name).

        [Range(MinSize, MaxSize)]
        public float Size = 1f; //!< Relative sign size (0.1 to 1), mapped linearly to a 0.05 to 0.5 m square sign.

        //! The actual square sign edge length in meters derived from Size.
        public float SizeInMeters => Size * MaxSizeMeters;

        //! Atlas cell index of the displayed sign. Setting it clamps to a valid cell and refreshes the display.
        public int Index
        {
            get => SignIndex;
            set
            {
                SignIndex = Mathf.Clamp(value, 0, AtlasCellCount - 1);
                Apply();
            }
        }

        //! Name of the displayed sign (matches the icon and material asset name).
        public string DisplayedSignName
        {
            get => SignName;
            set => SignName = value;
        }

        //! Calculates the atlas UV rectangle (bottom-left to top-right) for a given cell index.
        //! Used by the editor picker to draw atlas thumbnails.
        public static void GetUV(int index, int columns, int rows, out Vector2 uvMin, out Vector2 uvMax)
        {
            if (columns < 1) columns = 1;
            if (rows < 1) rows = 1;
            index = Mathf.Clamp(index, 0, columns * rows - 1);

            int col = index % columns;
            int row = index / columns;

            float du = 1f / columns;
            float dv = 1f / rows;

            // Atlas origin is top-left (row 0 at the top); Unity UV origin is bottom-left.
            uvMin = new Vector2(col * du, 1f - (row + 1) * dv);
            uvMax = new Vector2((col + 1) * du, 1f - row * dv);
        }

        //! Applies the current sign size to this object. Kept named RegenerateMesh for backward
        //! compatibility with the editor tools.
        public void RegenerateMesh() => Apply();

        //! Applies the size via local scale. The mesh and material are not touched here (assign your
        //! own mesh; the per-icon material is managed by the editor picker).
        public void Apply()
        {
            float s = SizeInMeters;
            var ls = transform.localScale;
            if (!Mathf.Approximately(ls.x, s) || !Mathf.Approximately(ls.y, s) || !Mathf.Approximately(ls.z, s))
                transform.localScale = new Vector3(s, s, s);
        }

        private void OnEnable()
        {
            Apply();
        }

#if UNITY_EDITOR
        //! Logs every Sign in the scene with its index, size and assigned material. Distinct sign
        //! settings must map to distinct sharedMaterials (signs reusing an icon share one material).
        [ContextMenu("Diagnose Signs")]
        private void DiagnoseSigns()
        {
            var signs = FindObjectsByType<Sign>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[Sign] Diagnose - {signs.Length} sign(s) in scene:");
            foreach (var s in signs)
            {
                var r = s.GetComponent<MeshRenderer>();
                var mat = r != null ? r.sharedMaterial : null;
                bool prefabInstance = UnityEditor.PrefabUtility.IsPartOfPrefabInstance(s);
                sb.AppendLine(
                    $"  '{s.name}'  index={s.Index}  name={s.DisplayedSignName}  size={s.Size}" +
                    $"  material={(mat != null ? mat.name : "<none>")}  prefabInstance={prefabInstance}");
            }
            Debug.Log(sb.ToString(), this);
        }

        private void OnValidate()
        {
            SignIndex = Mathf.Clamp(SignIndex, 0, AtlasCellCount - 1);
            Size = Mathf.Clamp(Size, MinSize, MaxSize);

            // Defer to avoid touching the transform/renderer during serialization/import.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                Apply();
            };
        }
#endif
    }
}
