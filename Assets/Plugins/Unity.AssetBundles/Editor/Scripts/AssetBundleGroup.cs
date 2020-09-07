using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public class AssetBundleGroup : ScriptableObject
{
    private bool isLocal = true;
    private bool isDebug;
    public List<BundleItem> items = new List<BundleItem>();

    public string GroupName
    {
        get { return name.ToLower(); }
    }

    public bool IsLocal { get => isLocal; set => isLocal = value; }
    public bool IsDebug { get => isDebug; set => isDebug = value; }

    public AssetBundleGroup.BundleItem FindGroupItem(string assetPath)
    {
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            if (item.IsMatch(assetPath, guid))
            {
                return item;
            }
        }
        return null;
    }

    public string GetAssetName(string assetPath)
    {
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        string assetName = null;
        bool match = false;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.IsMatch(assetPath, guid))
            {
                if (!string.IsNullOrEmpty(item.assetName))
                {
                    string _assetName = BuildAssetBundles.FormatString(item.assetName, assetPath);
                    if (!string.IsNullOrEmpty(_assetName))
                    {
                        if (item.assetNameToLower)
                            _assetName = _assetName.ToLower();
                        assetName = _assetName;
                    }
                }

                match = true;
            }
        }

        if (match)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                assetName = BuildAssetBundles.GetDefaultAddressableName(assetPath);
            }
        }

        return assetName;
    }
    public string GetAssetName(BundleItem item, string assetPath)
    {
        string assetName = null;

        if (item != null)
        {
            if (!string.IsNullOrEmpty(item.assetName))
            {
                assetName = BuildAssetBundles.FormatString(item.assetName, assetPath);
                if (item.assetNameToLower)
                    assetName = assetName.ToLower();
            }

            if (string.IsNullOrEmpty(assetName))
            {
                assetName = BuildAssetBundles.GetDefaultAddressableName(assetPath);
            }
        }
        return assetName;
    }

    public string GetBundleName(string assetPath, out string variant)
    {
        variant = null;
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        string bundleName = null;
        bool match = false;

        Dictionary<string, object> formatValues = new Dictionary<string, object>();
        BuildAssetBundles.GetFormatValues(assetPath, formatValues);

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.IsMatch(assetPath, guid))
            {
                if (!string.IsNullOrEmpty(item.bundleName))
                {
                    string _bundleName = BuildAssetBundles.FormatString(item.bundleName, formatValues);
                    if (!string.IsNullOrEmpty(_bundleName))
                    {
                        bundleName = _bundleName;
                    }
                }

                if (item.variants != null)
                {
                    foreach (var variantItem in item.variants)
                    {
                        if (AssetBundles.IsMatchIncludeExclude(assetPath, variantItem.include, variantItem.exclude))
                        {
                            if (!string.IsNullOrEmpty(variantItem.variant))
                                variant = BuildAssetBundles.FormatString(variantItem.variant, formatValues).ToLower();
                        }
                    }
                }

                match = true;
            }
        }

        if (match)
        {
            if (string.IsNullOrEmpty(bundleName))
            {
                bundleName = BuildAssetBundles.GetDefaultBundleName(assetPath);
            }
        }

        if (!string.IsNullOrEmpty(bundleName))
        {
            bundleName = GroupName + "/" + bundleName;
            bundleName = bundleName.ToLower();
        }

        return bundleName;
    }

    public string GetBundleName(BundleItem item, string assetPath, out string variant)
    {
        string bundleName = null;
        variant = null;
        if (item != null)
        {
            if (!string.IsNullOrEmpty(item.bundleName))
            {
                bundleName = BuildAssetBundles.FormatString(item.bundleName, assetPath);
            }
            if (string.IsNullOrEmpty(bundleName))
            {
                bundleName = BuildAssetBundles.GetDefaultBundleName(assetPath);
            }
            if (!string.IsNullOrEmpty(bundleName))
            {
                bundleName = GroupName + "/" + bundleName;
                bundleName = bundleName.ToLower();
            }
        }
        return bundleName;
    }


    [Serializable]
    public class BundleItem
    {
        public string include;
        public string exclude;
        public string bundleName;
        public string assetName;
        public bool assetNameToLower;
        //public List<string> guids = new List<string>();
        public List<string> includeGuids = new List<string>();
        public List<string> excludeGuids = new List<string>();

        public List<BundleVariant> variants = new List<BundleVariant>();

        public bool IsMatch(string assetPath)
        {
            return IsMatch(assetPath, AssetDatabase.AssetPathToGUID(assetPath));
        }

        public bool IsMatch(string assetPath, string guid)
        {
            if (string.IsNullOrEmpty(include))
                return false;

            if (!AssetBundles.IsMatchIncludeExclude(assetPath, include, exclude))
            {
                return false;
            }

            if (BuildAssetBundles.IsIgnoreAssetPath(assetPath))
                return false;

            if (excludeGuids.Count > 0)
            {
                if (guid == null)
                    guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (excludeGuids.Contains(guid))
                    return false;
            }
            if (includeGuids.Count > 0)
            {
                if (guid == null)
                    guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (includeGuids.Contains(guid))
                    return true;
            }


            return true;
        }
    }

    [Serializable]
    public class BundleVariant
    {
        public string include;
        public string exclude;
        public string variant;
    }


}
