using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Linq;

namespace UnityEditor.Build.AssetBundle
{

    /// <summary>
    /// All: {$BuildTarget}
    /// File: {$Directory|FileName|Extension}
    /// </summary>
    [Serializable]
    public class BuildAssetBundleConfig : ISerializationCallbackReceiver
    {
        ///<example>
        ///AssetBundles/{$BuildTarget}
        ///</example>
        public string OutputPath = "AssetBundles/{$BuildTarget}";
        public string CopyTo;
        /// <summary>
        /// 
        /// </summary>
        /// <example>
        /// Assets/StreamingAssets/AssetBundles
        /// </example>
        public string BuildCopyTo;
        /// <summary>
        /// 
        /// </summary>
        /// <example>
        /// .unity|unity3d
        /// </example>
        public string Variants;
        public string AssetBundleName = "{$Directory}";
        public string AssetName;
        public string AssetBundleNamesClassTemplate;

        public bool BuildGenerateAssetBundleNamesClass = true;
        public string AssetBundleNamesClassFilePath = "Assets/Plugins/gen/AssetBundleNames.dll";
        public string[] Components;
        public List<BuildAssetBundleItem> Items;
        [NonSerialized]
        private Dictionary<string, string> variantsMap;
        public string AssetValue;
        public string AssetClass;
        public string Options;
        public string[] IgnorePaths;

        public Dictionary<string, string> VariantsMap
        {
            get { return variantsMap; }
        }

        public void OnAfterDeserialize()
        {
            variantsMap = ToVariantsMap(Variants);
            if (Items == null)
                Items = new List<BuildAssetBundleItem>();

            foreach (var item in Items)
            {
                item.variantsMap = ToVariantsMap(item.Variants);
            }
        }

        static Dictionary<string, string> ToVariantsMap(string variants)
        {
            var variantsMap = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            if (!string.IsNullOrEmpty(variants))
            {
                foreach (var part in variants.Split(';'))
                {
                    if (string.IsNullOrEmpty(part))
                        continue;
                    string[] s = part.Split('|');
                    if (s.Length == 2)
                    {
                        variantsMap[s[0]] = s[1];
                    }
                    else if (s.Length == 1)
                    {
                        variantsMap[""] = s[0];
                    }
                }
            }
            return variantsMap;
        }


        public BuildAssetBundleItem FindItem(string assetPath)
        {
            if (Items != null)
            {
                foreach (var item in Items)
                {
                    if (item.Ignore)
                        continue;
                    if (!string.IsNullOrEmpty(item.Directory) && !PathDirectoryStartsWith(Path.GetDirectoryName(assetPath), item.Directory))
                        continue;
                    if (!string.IsNullOrEmpty(item.Pattern))
                    {
                        if (!item.PatternRegex.IsMatch(assetPath))
                            continue;
                    }
                    return item;
                }
            }
            return null;
        }

        public void OnBeforeSerialize()
        {

        }
        public static bool PathDirectoryStartsWith(string path1, string startPath)
        {
            path1 = ReplaceDirectorySeparatorChar(path1);
            startPath = ReplaceDirectorySeparatorChar(startPath);
            if (!path1.EndsWith("" + Path.DirectorySeparatorChar))
                path1 += Path.DirectorySeparatorChar;
            if (!startPath.EndsWith("" + Path.DirectorySeparatorChar))
                startPath += Path.DirectorySeparatorChar;
            return path1.StartsWith(startPath, StringComparison.InvariantCultureIgnoreCase);
        }
        internal static string ReplaceDirectorySeparatorChar(string path)
        {
            if (path == null)
                return null;
            if (Path.DirectorySeparatorChar == '/')
            {
                path = path.Replace('\\', '/');
            }
            else
            {
                path = path.Replace('/', '\\');
            }
            return path;
        }
    }
    [Serializable]
    public class BuildAssetBundleItem:ISerializationCallbackReceiver
    {
        public string Directory;
        public string Pattern;
        public string Variants;
        public string AssetBundleName;
        public string AssetName;
        public bool Ignore;
        public string AssetClass;

        internal Dictionary<string, string> variantsMap;
        public bool Preloaded;
        internal Regex regex;
        public string Tag;
        internal string[] TagArray;

        public Regex PatternRegex
        {
            get
            {
                if (regex == null)
                {
                    if (string.IsNullOrEmpty(Pattern))
                        regex = new Regex(".*");
                    else
                        regex = new Regex(Pattern, RegexOptions.IgnoreCase);
                }
                return regex;
            }
        }

        public void OnBeforeSerialize()
        { 
        }

        public void OnAfterDeserialize()
        {
            if (string.IsNullOrEmpty(Tag))
            {
                TagArray = new string[0];
            }
            else
            {
                TagArray = Tag.Split(',').Where(o => !string.IsNullOrEmpty(o)).ToArray();
            }
        }
    }
}