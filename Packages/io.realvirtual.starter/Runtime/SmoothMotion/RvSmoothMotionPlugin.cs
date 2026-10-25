// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

#if REALVIRTUAL_PROFESSIONAL && UNITY_EDITOR
using UnityEditor;

namespace realvirtual
{
    //! Enforces the platform settings of the native Smooth Motion plugin (rv_smooth_motion.dll): Windows
    //! x64 editor + standalone only. The DLL must never be bundled into Android/WebGL/... builds where it
    //! cannot load - SmoothMotion.IsAvailable already probes for that and falls back to the managed
    //! kernel, but a wrongly-tagged plugin would still bloat unrelated player builds.
    //!
    //! Also discards every native Smooth Motion context right before an Editor assembly reload (plan-281
    //! Finding #4): [NonSerialized] handles on SmoothMotion/Drive are wiped by the reload itself, but the
    //! Rust-side registry entries they owned are not - without this hook they leak for the rest of the
    //! Editor session. RvSmoothMotionNative.ResetAll() is a no-op if the DLL was never loaded.
    public static class RvSmoothMotionPlugin
    {
        private const string PluginPath =
            "Packages/io.realvirtual.professional/Runtime/Plugins/SmoothMotion/rv_smooth_motion.dll";

        [InitializeOnLoadMethod]
        private static void OnLoad()
        {
            EnforcePluginSettings();
            AssemblyReloadEvents.beforeAssemblyReload += RvSmoothMotionNative.ResetAll;
        }

        //! Reports the current importer state for the native Smooth Motion plugin - diagnostics only,
        //! never called from OnLoad(). Useful when IsAvailable is true (DLL loads) but the platform
        //! restriction the Editor guard applies needs to be confirmed independently.
        public static string DiagnosePluginImporter()
        {
            var raw = AssetImporter.GetAtPath(PluginPath);
            if (raw == null) return "no importer found at " + PluginPath;
            var importer = raw as PluginImporter;
            if (importer == null) return "importer type: " + raw.GetType().FullName + " (not a PluginImporter)";
            return $"PluginImporter OK - AnyPlatform={importer.GetCompatibleWithAnyPlatform()} " +
                   $"Editor={importer.GetCompatibleWithEditor()} " +
                   $"Win64={importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64)} " +
                   $"Android={importer.GetCompatibleWithPlatform(BuildTarget.Android)} " +
                   $"WebGL={importer.GetCompatibleWithPlatform(BuildTarget.WebGL)} " +
                   $"CPU={importer.GetEditorData("CPU")}";
        }

        private static void EnforcePluginSettings()
        {
            var importer = AssetImporter.GetAtPath(PluginPath) as PluginImporter;
            if (importer == null) return; // DLL not present (e.g. Starter-only checkout)

            bool ok = !importer.GetCompatibleWithAnyPlatform()
                      && importer.GetCompatibleWithEditor()
                      && importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64)
                      && !importer.GetCompatibleWithPlatform(BuildTarget.Android)
                      && !importer.GetCompatibleWithPlatform(BuildTarget.WebGL)
                      && importer.GetEditorData("CPU") == "x86_64";
            if (ok) return;

            importer.SetCompatibleWithAnyPlatform(false);
            importer.SetCompatibleWithEditor(true);
            importer.SetEditorData("OS", "Windows");
            importer.SetEditorData("CPU", "x86_64");
            importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, true);
            importer.SetPlatformData(BuildTarget.StandaloneWindows64, "CPU", "x86_64");
            importer.SetCompatibleWithPlatform(BuildTarget.Android, false);
            importer.SetCompatibleWithPlatform(BuildTarget.WebGL, false);
            importer.SaveAndReimport();
            Logger.Message("rv_smooth_motion.dll plugin platform settings enforced (Windows x64 only)", null);
        }
    }
}
#endif
