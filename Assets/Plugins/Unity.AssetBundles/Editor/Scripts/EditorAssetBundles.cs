using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using Object = UnityEngine.Object;
using System.IO;
using Coroutines;
using System.Linq;

namespace UnityEditor.Build.AssetBundle
{
    class EditorAssetBundles
    {

        private const string IsEditorAssetsModeMenu = "Build/AssetBundle/Editor Assets Mode";
        private static Dictionary<string, string[]> assetBundleNameAndVariants = new Dictionary<string, string[]>();
        private static Dictionary<string, Object> cachedAssets;
        private static string[] allAssetBundleNames;
        /// <summary>
        /// BaseName > Variant > AssetBundleName
        /// </summary>
        public static Dictionary<string, Dictionary<string, string>> allAssetBundlesWithVariantSet;
        /// <summary>
        /// AssetPath > AddressableName
        /// </summary>
        private static Dictionary<string, string[]> cachedAddressableName;

        internal const string PlayerPrefsKeyPrefix = "unity.assetbundles.";

        internal const string LogTag = "AssetBundles Editor";
        internal static ILogger Logger = new UnityEngine.LogExtension.LoggerExtension();

        public static bool IsEditorAssetsMode
        {
            get { return PlayerPrefs.GetInt(PlayerPrefsKeyPrefix + "IsEditorAssetsMode", 1) != 0; }
            set { PlayerPrefs.SetInt(PlayerPrefsKeyPrefix + "IsEditorAssetsMode", value ? 1 : 0); }
        }


        [MenuItem(IsEditorAssetsModeMenu)]
        static void IsEditorAssetsMode_Menu()
        {
            IsEditorAssetsMode = !IsEditorAssetsMode;
        }

        [MenuItem(IsEditorAssetsModeMenu, validate = true)]
        static bool IsEditorAssetsMode_Menu_Validate()
        {
            Menu.SetChecked(IsEditorAssetsModeMenu, IsEditorAssetsMode);
            return true;
        }

        [InitializeOnLoadMethod]
        static void InitializeOnLoadMethod()
        {
            if (IsEditorAssetsMode)
            {
                AssetBundles.LoadAssetHandler = EditorLoadAssetHandler;
                AssetBundles.LoadAssetAsyncHandler = EditorLoadAssetsAsyncHandler;
            }
        }



        public static Object GetOrCacheAsset(string assetPath, Type type)
        {
            if (cachedAssets == null)
                cachedAssets = new Dictionary<string, Object>();

            Object obj;
            if (!cachedAssets.TryGetValue(assetPath, out obj) || !obj)
            {
                if (type != null)
                    obj = AssetDatabase.LoadAssetAtPath(assetPath, type);
                else
                    obj = AssetDatabase.LoadAssetAtPath(assetPath, typeof(Object));
                cachedAssets[assetPath] = obj;
            }
            if (obj)
            {
                if (!type.IsAssignableFrom(obj.GetType()))
                    obj = null;
            }
            return obj;
        }
        class VariantInfo
        {
            public string baseName;
            public Dictionary<string, string> variants = new Dictionary<string, string>();
        }

        private static Object[] EditorLoadAssetHandler(string assetBundleName, string assetName, Type type, object owner)
        {
            if (allAssetBundleNames == null)
            {
                allAssetBundleNames = AssetDatabase.GetAllAssetBundleNames();

                allAssetBundlesWithVariantSet = new Dictionary<string, Dictionary<string, string>>();

                for (int i = 0; i < allAssetBundleNames.Length; i++)
                {
                    var tmp = ParseAssetBundleNameAndVariant(allAssetBundleNames[i]);
                    if (!string.IsNullOrEmpty(tmp[1]))
                    {
                        Dictionary<string, string> varintToName;
                        if (!allAssetBundlesWithVariantSet.TryGetValue(tmp[0], out varintToName))
                        {
                            varintToName = new Dictionary<string, string>();
                            allAssetBundlesWithVariantSet[tmp[0]] = varintToName;
                        }
                        varintToName[tmp[1]] = allAssetBundleNames[i];
                    }
                }

                cachedAddressableName = new Dictionary<string, string[]>();
            }

            assetBundleName = ResolveAssetBundleNameVariant(assetBundleName);

            string[] assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleName);

            // 即使是小写String, 调用 ToLower 也会产生 GC
            if (assetName != null)
                assetName = assetName.ToLower();


            if (assetPaths == null || assetPaths.Length == 0)
            {
                LogFormat(LogType.Error, "editor load assetBundle fail " + assetBundleName);
                return new Object[0];
            }

            if (type == null)
                type = typeof(Object);

            Object[] result = null;
            if (string.IsNullOrEmpty(assetName))
            {
                List<Object> objs = new List<Object>(assetPaths.Length);
                for (int i = 0; i < assetPaths.Length; i++)
                {
                    var obj = GetOrCacheAsset(assetPaths[i], type);
                    if (obj)
                    {
                        objs.Add(obj);
                    }
                }
                result = objs.ToArray();
            }
            else
            {
                string assetPath;
                for (int i = 0; i < assetPaths.Length; i++)
                {
                    assetPath = assetPaths[i];
                    string[] addressableNames;
                    if (!cachedAddressableName.TryGetValue(assetPath, out addressableNames))
                    {
                        addressableNames = new string[3];
                        var addressableName = BuildAssetBundles.GetAddressableName(assetPath);
                        string addressableNameLow = addressableName.ToLower();
                        addressableNames[0] = addressableNameLow;
                        addressableNames[1] = Path.GetFileNameWithoutExtension(addressableNameLow);
                        addressableNames[2] = Path.GetFileName(addressableNameLow);
                        cachedAddressableName[assetPath] = addressableNames;
                    }

                    if (Array.IndexOf(addressableNames, assetName) >= 0)
                    {
                        var obj = GetOrCacheAsset(assetPaths[i], type);
                        if (obj != null)
                        {
                            result = new Object[] { obj };
                            break;
                        }
                    }
                }
            }

            if (result == null || result.Length == 0)
            {
                LogFormat(LogType.Error, "editor load asset bundle fail. assetBundleName: {0}, assetName: {1}, type:{2}", assetBundleName, assetName, type);
            }

            return result ?? new Object[0];
        }

        private static Task<Object[]> EditorLoadAssetsAsyncHandler(string assetBundleName, string assetName, Type type, object owner)
        {
            return Task.Run<Object[]>(() =>
            {
                return EditorLoadAssetHandler(assetBundleName, assetName, type, owner);
            });
        }



        static string[] ParseAssetBundleNameAndVariant(string assetBundleName)
        {
            string[] nameAndVariant;
            if (!assetBundleNameAndVariants.TryGetValue(assetBundleName, out nameAndVariant))
            {
                string[] parts = assetBundleName.Split(new char[] { '.' }, 2, StringSplitOptions.None);
                if (parts.Length == 1)
                    nameAndVariant = new string[] { assetBundleName, "" };
                else
                    nameAndVariant = parts;
                assetBundleNameAndVariants[assetBundleName] = nameAndVariant;
            }
            return nameAndVariant;
        }
        static string ResolveAssetBundleNameVariant(string assetBundleName)
        {
            string[] nameAndVariant = ParseAssetBundleNameAndVariant(assetBundleName);

            Dictionary<string, string> variantSet;
            if (allAssetBundlesWithVariantSet.TryGetValue(nameAndVariant[0], out variantSet))
            {
                string newName = null;
                for (int i = 0, len = AssetBundles.Variants.Count; i < len; i++)
                {
                    if (variantSet.TryGetValue(AssetBundles.Variants[i], out newName))
                    {
                        break;
                    }
                }

                if (newName == null)
                {
                    LogFormat(LogType.Warning, "not found match variant. first variant: {0}, assetBundle:{1}", variantSet.Values.First(), assetBundleName);
                    return variantSet.Values.First();
                }
                return newName;
            }
            return assetBundleName;
        }
        internal static void LogFormat(LogType logType, string format, params object[] args)
        {
            Debug.unityLogger.Log(logType, LogTag, string.Format(format, args));
        }
    }
}