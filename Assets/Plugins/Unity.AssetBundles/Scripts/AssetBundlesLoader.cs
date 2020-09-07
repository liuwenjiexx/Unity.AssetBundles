//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//namespace UnityEngine
//{
//    public class AssetBundlesLoader : IAssetLoader
//    {
//        public UnityEngine.Object Load(string assetName, Type assetType)
//        {
//            return AssetBundles.LoadAsset(assetName, assetType);
//        }
//        public GameObject Instantiate(string assetName, Transform parent = null)
//        {
//            GameObject prefab = Load(assetName, typeof(GameObject)) as GameObject;
//            if (!prefab)
//                return null;
//            return Reusable.Get(prefab, parent);
//        }
//    }
//}