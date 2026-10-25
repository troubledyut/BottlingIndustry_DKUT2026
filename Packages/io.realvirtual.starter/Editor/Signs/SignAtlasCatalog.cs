// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace realvirtual
{
    //! Editor-only metadata for a single sign in the atlas (parsed from spritesheet.tsv).
    public class SignEntry
    {
        public int Index;
        public string Category;      // raw key, e.g. "safe_condition_signs"
        public string Name;          // raw name, e.g. "emergency-exit-2"
        public int Col;
        public int Row;
        public string DisplayName;   // pretty, e.g. "Emergency Exit 2"
        public string CategoryLabel; // pretty, e.g. "Safe Condition"
    }

    //! Editor-only catalog that loads the sign atlas texture and its TSV metadata for the picker UI.
    //! The runtime Sign component does not use this - it only needs the cell index.
    public static class SignAtlasCatalog
    {
        public struct CategoryInfo
        {
            public string Key;
            public string Label;
        }

        //! The four ISO sign categories in atlas (row-major) order.
        public static readonly CategoryInfo[] Categories =
        {
            new CategoryInfo { Key = "safe_condition_signs", Label = "Safe Condition" },
            new CategoryInfo { Key = "hazard_warning_signs", Label = "Hazard" },
            new CategoryInfo { Key = "prohibition_signs", Label = "Prohibition" },
            new CategoryInfo { Key = "mandatory_signs", Label = "Mandatory" }
        };

        private const string AtlasTexturePath = "Modules/Signs/atlas/spritesheet.png";
        private const string AtlasTsvPath = "Modules/Signs/atlas/spritesheet.tsv";

        private static List<SignEntry> _entries;
        private static Texture2D _atlas;

        //! All signs in the atlas, ordered by index.
        public static IReadOnlyList<SignEntry> Entries
        {
            get
            {
                EnsureLoaded();
                return _entries;
            }
        }

        //! The shared atlas texture asset.
        public static Texture2D Atlas
        {
            get
            {
                if (_atlas == null)
                    _atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(
                        RealvirtualAssetPaths.StarterRuntime(AtlasTexturePath));
                return _atlas;
            }
        }

        //! Normalized atlas sub-rectangle for GUI.DrawTextureWithTexCoords (origin bottom-left).
        public static Rect TexCoords(SignEntry entry)
        {
            Sign.GetUV(entry.Index, Sign.AtlasColumns, Sign.AtlasRows, out var uvMin, out var uvMax);
            return new Rect(uvMin.x, uvMin.y, uvMax.x - uvMin.x, uvMax.y - uvMin.y);
        }

        //! Normalized atlas sub-rectangle for a raw cell index.
        public static Rect TexCoords(int index)
        {
            Sign.GetUV(index, Sign.AtlasColumns, Sign.AtlasRows, out var uvMin, out var uvMax);
            return new Rect(uvMin.x, uvMin.y, uvMax.x - uvMin.x, uvMax.y - uvMin.y);
        }

        //! Asset path of an individual sign icon texture (used to build the per-sign material).
        public static string IconAssetPath(string signName)
        {
            return RealvirtualAssetPaths.StarterRuntime("Modules/Signs/icons/" + signName + ".png");
        }

        //! Loads an individual sign icon texture by name, or null if it does not exist.
        public static Texture2D LoadIcon(string signName)
        {
            if (string.IsNullOrEmpty(signName)) return null;
            return AssetDatabase.LoadAssetAtPath<Texture2D>(IconAssetPath(signName));
        }

        //! Returns the sign entry for the given index, or null if not found.
        public static SignEntry Find(int index)
        {
            EnsureLoaded();
            return _entries.FirstOrDefault(e => e.Index == index);
        }

        //! Maps a raw category key to its display label.
        public static string CategoryLabel(string key)
        {
            foreach (var c in Categories)
                if (c.Key == key) return c.Label;
            return Prettify(key);
        }

        //! Forces a reload of the TSV/atlas on next access (e.g. after the atlas is updated).
        public static void Invalidate()
        {
            _entries = null;
            _atlas = null;
        }

        private static void EnsureLoaded()
        {
            if (_entries != null) return;
            _entries = new List<SignEntry>();

            try
            {
                var assetPath = RealvirtualAssetPaths.StarterRuntime(AtlasTsvPath);
                var fullPath = Path.GetFullPath(assetPath);
                if (!File.Exists(fullPath))
                {
                    Debug.LogWarning($"[Sign] Atlas metadata not found at {assetPath}");
                    return;
                }

                var lines = File.ReadAllLines(fullPath);
                for (int i = 1; i < lines.Length; i++) // skip header row
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var cols = line.Split('\t');
                    if (cols.Length < 5) continue;

                    if (!int.TryParse(cols[0], out var index)) continue;
                    var category = cols[1];
                    var name = cols[2];
                    int.TryParse(cols[3], out var col);
                    int.TryParse(cols[4], out var row);

                    _entries.Add(new SignEntry
                    {
                        Index = index,
                        Category = category,
                        Name = name,
                        Col = col,
                        Row = row,
                        DisplayName = Prettify(name),
                        CategoryLabel = CategoryLabel(category)
                    });
                }

                _entries = _entries.OrderBy(e => e.Index).ToList();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Sign] Failed to read atlas metadata: {e.Message}");
            }
        }

        //! Turns a raw name like "emergency-exit-2" or "safe_condition_signs" into "Emergency Exit 2".
        private static string Prettify(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            var spaced = raw.Replace('-', ' ').Replace('_', ' ').Trim();
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced);
        }
    }
}
