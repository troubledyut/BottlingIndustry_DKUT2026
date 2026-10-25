#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class CopyUnityPLCConnectOnBuild : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.StandaloneWindows64)
            return;

        string buildOutputDir = Path.GetDirectoryName(report.summary.outputPath);
        string sourceDir = Path.Combine(Application.dataPath, "../UnityPLCConnect");
        string targetDir = Path.Combine(buildOutputDir, "UnityPLCConnect");

        if (!Directory.Exists(sourceDir))
        {
            Debug.LogWarning($"[CopyUnityPLCConnectOnBuild] Source directory missing: {sourceDir}");
            return;
        }

        Debug.Log($"[CopyUnityPLCConnectOnBuild] Shipping binaries to build output: {targetDir}");
        CopyDirectory(sourceDir, targetDir);
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            if (file.EndsWith(".bak", System.StringComparison.OrdinalIgnoreCase))
                continue;

            string destFileName = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, destFileName, true);
        }

        foreach (string subDir in Directory.GetDirectories(sourceDir))
        {
            string destSubDir = Path.Combine(targetDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, destSubDir);
        }
    }
}
#endif