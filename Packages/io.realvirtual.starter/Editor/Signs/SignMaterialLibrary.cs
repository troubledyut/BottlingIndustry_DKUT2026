// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace realvirtual
{
    //! Editor-only library that creates and caches one Material Variant per sign icon.
    //! Each variant inherits from SignBase and overrides only the base map with the icon texture, so
    //! every Sign gets a distinct sharedMaterial that survives GLB export (the exporter serializes
    //! sharedMaterials, not MaterialPropertyBlocks).
    public static class SignMaterialLibrary
    {
        private const string BasePath = "Modules/Signs/Materials/SignBase.mat";
        private const string MaterialsFolder = "Modules/Signs/Materials/";
        private const string IconsFolder = "Modules/Signs/icons/";

        //! Loads the shared SignBase material (the variant parent).
        public static Material LoadBase()
        {
            return AssetDatabase.LoadAssetAtPath<Material>(RealvirtualAssetPaths.StarterRuntime(BasePath));
        }

        //! Returns the material for a sign by name, creating it on demand as a Material Variant of
        //! SignBase (with _BaseMap = the icon) if it does not exist yet. Returns null if the base
        //! material or the icon texture cannot be found.
        public static Material GetOrCreate(string signName)
        {
            if (string.IsNullOrEmpty(signName))
                return null;

            var matPath = RealvirtualAssetPaths.StarterRuntime(MaterialsFolder + signName + ".mat");

            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null)
                return existing;

            var baseMat = LoadBase();
            if (baseMat == null)
            {
                Debug.LogWarning($"[Sign] SignBase material not found at {RealvirtualAssetPaths.StarterRuntime(BasePath)}");
                return null;
            }

            EnsureBaseConfigured();

            var iconPath = RealvirtualAssetPaths.StarterRuntime(IconsFolder + signName + ".png");
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            if (icon == null)
            {
                Debug.LogWarning($"[Sign] Icon texture not found at {iconPath}");
                return null;
            }

            // Create a Material Variant of SignBase that overrides only the base map.
            var variant = new Material(baseMat) { parent = baseMat };
            variant.SetTexture(BaseMap, icon);

            AssetDatabase.CreateAsset(variant, matPath);
            AssetDatabase.SaveAssets();

            return variant;
        }

        //! Assigns a material to a renderer's first slot through SerializedObject, so the change is a
        //! proper per-instance prefab override (and undoable).
        public static void AssignToRenderer(MeshRenderer renderer, Material mat)
        {
            if (renderer == null || mat == null) return;
            var rso = new SerializedObject(renderer);
            var materials = rso.FindProperty("m_Materials");
            if (materials.arraySize == 0) materials.arraySize = 1;
            materials.GetArrayElementAtIndex(0).objectReferenceValue = mat;
            rso.ApplyModifiedProperties();
        }

        //! Configures SignBase once for alpha-clipped (cutout) rendering so variants inherit crisp
        //! transparent pictograms (and a glTF MASK alpha mode on export). Does nothing if already set.
        public static void EnsureBaseConfigured()
        {
            var baseMat = LoadBase();
            if (baseMat == null) return;

            if (baseMat.HasProperty(AlphaClip) && baseMat.GetFloat(AlphaClip) >= 0.5f)
                return; // already configured

            if (baseMat.HasProperty(AlphaClip)) baseMat.SetFloat(AlphaClip, 1f);
            baseMat.EnableKeyword("_ALPHATEST_ON");
            if (baseMat.HasProperty(Cutoff)) baseMat.SetFloat(Cutoff, 0.5f);
            baseMat.SetOverrideTag("RenderType", "TransparentCutout");
            baseMat.renderQueue = (int)RenderQueue.AlphaTest;

            EditorUtility.SetDirty(baseMat);
            AssetDatabase.SaveAssets();
        }

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int AlphaClip = Shader.PropertyToID("_AlphaClip");
        private static readonly int Cutoff = Shader.PropertyToID("_Cutoff");
    }
}
