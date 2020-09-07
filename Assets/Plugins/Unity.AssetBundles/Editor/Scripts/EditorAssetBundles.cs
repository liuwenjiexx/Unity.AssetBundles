using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using Object = UnityEngine.Object;
using System.IO;
using Coroutines;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEditor.Callbacks;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using UnityEngine.Localizations;
using System.Text.RegularExpressions;

namespace UnityEditor.Build
{
    public class EditorAssetBundles
    {

        internal const int BuildMenuPriority = 20;
        internal const int ModeMenuPriority = BuildMenuPriority + 20;
        internal const int OtherMenuPriority = ModeMenuPriority + 20;

        public const string MenuPrefix = "Build/AssetBundle/";
        private const string IsEditorAssetsModeMenu = MenuPrefix + "Editor Mode";
        private const string IsBuildModeMenu = MenuPrefix + "Build Mode";
        private const string IsRuntimeModeMenu = MenuPrefix + "Download Mode";
        private const string OpenBuildDirectoryMenu = MenuPrefix + "Open Build Direcotry";
        private const string OpenLocalDirectoryMenu = MenuPrefix + "Open Local Direcotry";
        private const string OpenStreamingAssetsDirectoryMenu = MenuPrefix + "Open StreamingAssets Direcotry";


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
        [UnityEngine.LogExtension.External.LogExtension]
        internal static ILogger Logger = Debug.unityLogger;
        private static LocalizationValues editorLocalizationValues;
        static Dictionary<string, Regex> regexs;
        public static Action<string, string> LoadAssetCallback;

        public static string PackageDir
        {
            get => BuildAssetBundles.PackageDir;
        }

        public static AssetBundleMode Mode
        {
            get { return (AssetBundleMode)PlayerPrefs.GetInt(PlayerPrefsKeyPrefix + "AssetBundleMode", (int)AssetBundleMode.Editor); }
            set { PlayerPrefs.SetInt(PlayerPrefsKeyPrefix + "AssetBundleMode", (int)value); }
        }

        public static LocalizationValues EditorLocalizationValues
        {
            get
            {
                if (editorLocalizationValues == null)
                    editorLocalizationValues = new DirectoryLocalizationValues(Path.Combine(PackageDir, "Editor/Localization"));
                return editorLocalizationValues;
            }
        }


        [MenuItem(IsEditorAssetsModeMenu, priority = ModeMenuPriority)]
        static void IsEditorAssetsMode_Menu()
        {
            Mode = AssetBundleMode.Editor;
        }

        [MenuItem(IsEditorAssetsModeMenu, validate = true)]
        static bool IsEditorAssetsMode_Menu_Validate()
        {
            Menu.SetChecked(IsEditorAssetsModeMenu, Mode == AssetBundleMode.Editor);
            return true;
        }
        [MenuItem(IsBuildModeMenu, priority = ModeMenuPriority)]
        static void IsBuildModeMenu_Menu()
        {
            Mode = AssetBundleMode.Build;
        }

        [MenuItem(IsBuildModeMenu, validate = true)]
        static bool IsBuildModeMenu_Menu_Validate()
        {
            Menu.SetChecked(IsBuildModeMenu, Mode == AssetBundleMode.Build);
            return true;
        }

        [MenuItem(IsRuntimeModeMenu, priority = ModeMenuPriority)]
        static void IsRuntimeMode_Menu()
        {
            Mode = AssetBundleMode.Download;
        }

        [MenuItem(IsRuntimeModeMenu, validate = true)]
        static bool IsRuntimeMode_Menu_Validate()
        {
            Menu.SetChecked(IsRuntimeModeMenu, Mode == AssetBundleMode.Download);
            return true;
        }
        // [MenuItem(OpenBuildDirectoryMenu, priority = OtherMenuPriority)]
        public static void OpenBuildDirectory_Menu()
        {

            string path = BuildAssetBundles.GetOutputPath();
            string manifestPath = Path.Combine(path, Path.GetFileName(path));
            if (File.Exists(manifestPath))
                EditorUtility.RevealInFinder(manifestPath);
            else
                EditorUtility.RevealInFinder(path);
        }

        // [MenuItem(OpenLocalDirectoryMenu, priority = OtherMenuPriority)]
        public static void OpenLocalDirectory_Menu()
        {
            string manifestPath = $"{Application.persistentDataPath}/{BuildAssetBundles.FormatString(AssetBundleSettings.LocalManifestPath)}";
            if (File.Exists(manifestPath))
                EditorUtility.RevealInFinder(manifestPath);
            else if (Directory.Exists(Path.GetDirectoryName(manifestPath)))
                EditorUtility.RevealInFinder(Path.GetDirectoryName(manifestPath));
            else
                Debug.Log("not exists path <" + manifestPath + "> click menu [Build/AssetBundle/Runtime Mode]");
        }
        // [MenuItem(OpenStreamingAssetsDirectoryMenu, priority = OtherMenuPriority)]
        public static void OpenStreamingAssetsDirectory_Menu()
        {
            string path = BuildAssetBundles.GetStreamingAssetsPath();
            string manifestPath = Path.Combine(path, Path.GetFileName(path));
            if (File.Exists(manifestPath))
                EditorUtility.RevealInFinder(manifestPath);
            else
                EditorUtility.RevealInFinder(path);
        }



        //[MenuItem(MenuPrefix + "Edit Config", priority = EditorAssetBundles.BuildMenuPriority + 1)]
        //public static void OpenConfigFile()
        //{
        //    BuildAssetBundles.LoadConfig();
        //    Application.OpenURL(Path.GetFullPath(BuildAssetBundles.ConfigFilePath));
        //}
        //  [MenuItem(MenuPrefix + "Remove Unused AssetBundle Names", priority = EditorAssetBundles.OtherMenuPriority)]
        public static void RemoveUnusedAssetBundleNames()
        {
            AssetDatabase.RemoveUnusedAssetBundleNames();
        }
        [MenuItem(MenuPrefix + "Help", priority = EditorAssetBundles.OtherMenuPriority + 20)]
        static void OpenREADME_Menu()
        {
            string assetPath = Path.Combine(BuildAssetBundles.PackageDir, "README.md");
            //  AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath));
            Application.OpenURL(Path.GetFullPath(assetPath));
        }

        [InitializeOnLoadMethod]
        static void InitializeOnLoadMethod()
        {
            EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
            UpateLoadHandler();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void RuntimeInitializeOnLoadMethod()
        {
            UpateLoadHandler();
        }

        static void UpateLoadHandler()
        {
            if (Mode == AssetBundleMode.Editor || !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                AssetBundles.LoadAssetHandler = EditorLoadAssetHandler;
            }
            else
            {
                AssetBundles.LoadAssetHandler = null;
            }
        }

        private static void EditorApplication_playModeStateChanged(PlayModeStateChange state)
        {
            UpateLoadHandler();
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

        //  private static string[] GetAllAssetPaths(string assetBundleName)

        public static void EditorLoadAssetHandler(AssetBundles.LoadAssetRequest request)
        {
            //Debug.Log("EditorLoadAssetHandler: " + request.assetBundleName);
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

            string assetBundleName;
            string assetName;
            Type type;
            assetBundleName = request.assetBundleName;
            assetName = request.assetName;
            type = request.assetType;

            assetBundleName = ResolveAssetBundleNameVariant(assetBundleName);

            string[] assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleName);

            // 即使是小写String, 调用 ToLower 也会产生 GC
            if (assetName != null)
                assetName = assetName.ToLower();


            if (assetPaths == null || assetPaths.Length == 0)
            {
                //LogFormat(LogType.Error, "editor load assetBundle fail " + assetBundleName);
                request.loadedAssets = new Object[0];
                request.Done();
                return;
            }

            if (type == null)
                type = typeof(Object);

            Object[] result = null;
            string addressableName = null;
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
                        var _addressableName = BuildAssetBundles.GetAddressableName(assetPath);
                        string addressableNameLow = _addressableName.ToLower();
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
                            addressableName = assetPaths[0];
                            break;
                        }
                    }
                }
            }

            if (result == null || result.Length == 0)
            {
                //LogFormat(LogType.Error, "editor load asset bundle fail. assetBundleName: {0}, assetName: {1}, type:{2}", assetBundleName, assetName, type);
            }

            request.loadedAssets = result ?? new Object[0];

            if (LoadAssetCallback != null && request.loadedAssets.Length > 0)
            {
                string assetPath = AssetDatabase.GetAssetPath(request.loadedAssets[0]);
                LoadAssetCallback?.Invoke(assetBundleName, assetPath);
            }


            if (request.isLoadScene)
            {
                EditorLoadSceneHandler(request);
                return;
            }
            request.Done();
        }


        private static void EditorLoadSceneHandler(AssetBundles.LoadAssetRequest request)
        {
            string sceneName = request.assetName;

            if (request.loadedAssets != null && request.loadedAssets.Length > 0)
            {
                Object sceneAsset = request.loadedAssets[0];


                string path = AssetDatabase.GetAssetPath(sceneAsset);

                var scenes = EditorBuildSettings.scenes;
                EditorBuildSettingsScene scene = null;
                for (int i = 0; i < scenes.Length; i++)
                {
                    if (string.Equals(scenes[i].path, path, StringComparison.InvariantCultureIgnoreCase))
                    {
                        scene = scenes[i];
                        break;
                    }
                }
                if (scene == null)
                {
                    scene = new EditorBuildSettingsScene(path, true);
                    AddedScene.Add(path);
                    scenes = scenes.Concat(new EditorBuildSettingsScene[] { scene }).ToArray();
                    EditorBuildSettings.scenes = scenes;
                    AssetDatabase.SaveAssets();
                }

                //Unloading the last loaded scene xxxx, is not supported. Please use SceneManager.LoadScene()/EditorSceneManager.OpenScene() to switch to another scene.
                //if (request.isAsync)
                //{

                //    var async = SceneManager.LoadSceneAsync(sceneName, request.sceneMode);
                //    async.allowSceneActivation = true;
                //    EditorApplication.CallbackFunction update = null;
                //    update = () =>
                //     {
                //         request.progress = async.progress;
                //         if (async.isDone)
                //         {
                //             request.Done();
                //         }
                //         else
                //         {
                //             EditorApplication.delayCall += update;
                //         }
                //     };

                //    update();
                //}
                else
                {
                    SceneManager.LoadScene(sceneName, request.sceneMode);
                    //SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
                }
            }
            else
            {
                Debug.LogError("not load scene " + request.assetBundleName + ", " + sceneName);
            }
            request.Done();
        }

        static List<string> AddedScene = new List<string>();


        static string[] ParseAssetBundleNameAndVariant(string assetBundleName)
        {
            if (string.IsNullOrEmpty(assetBundleName))
                return new string[] { "", "" };
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

        public static bool IsMatch(string pattern, string text, bool ignoreCase = false)
        {
            if (regexs == null)
                regexs = new Dictionary<string, Regex>();
            if (string.IsNullOrEmpty(pattern))
                return true;
            Regex m;
            if (!regexs.TryGetValue(pattern, out m))
            {
                RegexOptions options = RegexOptions.None;
                if (ignoreCase)
                    options |= RegexOptions.IgnoreCase;
                m = new Regex(pattern, options);
                regexs[pattern] = m;
            }
            return m.IsMatch(text);
        }



    }
}