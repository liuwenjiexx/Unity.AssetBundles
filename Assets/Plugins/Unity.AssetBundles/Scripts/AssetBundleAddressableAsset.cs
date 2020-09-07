using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AssetBundleAddressableAsset : ScriptableObject
{
    public List<AssetInfo> assets = new List<AssetInfo>();


    [Serializable]
    public class AssetInfo : IComparable<AssetInfo>
    {
        public string assetName;
        public string guid;
        public string bundleName;

        public int CompareTo(AssetInfo other)
        {
            if (other == null)
                return -1;
            return this.guid.CompareTo(other.guid);
        }
    }

    int FindIndex(string guid)
    {
        for (int i = 0; i < assets.Count; i++)
        {
            if (assets[i].guid == guid)
            {
                return i;
            }
        }
        return -1;
    }

    public bool Add(string guid, string assetName, string bundleName)
    {
        AssetInfo assetInfo = null;
        int index = FindIndex(guid);
        if (index >= 0)
        {
            assetInfo = assets[index];
            if (assetInfo != null)
            {
                if (assetInfo.assetName == assetName && assetInfo.bundleName == bundleName)
                    return false;
            }
        }
        assetInfo = new AssetInfo() { guid = guid, assetName = assetName, bundleName = bundleName };
        if (index >= 0)
        {
            assets[index] = assetInfo;
        }
        else
        {
            assets.InsertSorted(assetInfo);
        }
        return true;
    }

    public bool Remove(string guid)
    {
        int index = FindIndex(guid);
        if (index >= 0)
        {
            assets.RemoveAt(index);
            return true;
        }
        return false;
    }
}
