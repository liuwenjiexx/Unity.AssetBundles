using Coroutines;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.AssetBundleExtensions
{

    public interface IAssetLoader
    {
        UnityEngine.Object LoadAsset(string assetName, Type assetType, object lifetime = null);
        Task<UnityEngine.Object> LoadAssetAsync(string assetName, Type assetType, object lifetime = null);
    }


    public static class AssetBundlesExtensions
    {
        public static T LoadAsset<T>(this IAssetLoader loader, string assetName, object lifetime = null)
            where T : UnityEngine.Object
        {
            return loader.LoadAsset(assetName, typeof(T), lifetime) as T;
        }
        public static Task<T> LoadAssetAsync<T>(this IAssetLoader loader, string assetName, object lifetime = null)
            where T : UnityEngine.Object
        {
            return loader.LoadAssetAsync(assetName, typeof(T), lifetime).ContinueWith(t => t.Result as T);
        }

        public static GameObject Instantiate(this IAssetLoader loader, string assetName, Transform parent = null, object lifetime = null)
        {
            var prefab = loader.LoadAsset<GameObject>(assetName, lifetime);
            if (prefab)
                return GameObject.Instantiate(prefab);
            return null;
        }
        public static Task<GameObject> InstantiateAsync(this IAssetLoader loader, string assetName, Transform parent = null, object lifetime = null)
        {
            return loader.LoadAssetAsync<GameObject>(assetName, lifetime)
                 .ContinueWith(t =>
                 {
                     return Object.Instantiate(t.Result, parent);
                 });
        }
    }


    public class AssetBundlesLoader : IAssetLoader
    {
        public string assetNamePrefix;

        public AssetBundlesLoader(string assetNamePrefix)
        {
            if (string.IsNullOrEmpty(assetNamePrefix))
                this.assetNamePrefix = null;
            else
                this.assetNamePrefix = assetNamePrefix;
        }

        public UnityEngine.Object LoadAsset(string assetName, Type assetType, object lifetime = null)
        {
            if (assetNamePrefix != null)
                assetName = assetNamePrefix + assetName;

            return AssetBundles.LoadAsset(assetName, assetType, lifetime);
        }
        public Task<UnityEngine.Object> LoadAssetAsync(string assetName, Type assetType, object lifetime = null)
        {
            if (assetNamePrefix != null)
                assetName = assetNamePrefix + assetName;

            return AssetBundles.LoadAssetAsync(assetName, assetType, lifetime);
        }

    }

}