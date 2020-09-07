using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.GUIExtensions;
using System;
using System.Linq;
using UnityEngine.Localizations;
using UnityEditor.Build.Internal;
using System.IO;
using System.Text;

namespace UnityEditor.Build
{
    public class EditorAssetBundleSettingsWindow : EditorWindow
    {

        private Vector2 scrollPos;

        [MenuItem(EditorAssetBundles.MenuPrefix + "Settings", priority = EditorAssetBundles.BuildMenuPriority + 1)]
        public static void ShowWindow()
        {
            GetWindow<EditorAssetBundleSettingsWindow>().Show();
        }

        private void OnEnable()
        {
            using (EditorAssetBundles.EditorLocalizationValues.BeginScope())
            {
                titleContent = new GUIContent("Build Asset Bundle Config".Localization());
            }
        }


        bool? showAdvancedOptions;
        bool ShowAdvancedOptions
        {
            get
            {
                if (showAdvancedOptions == null)
                    showAdvancedOptions = EditorPrefs.GetBool(typeof(EditorAssetBundleSettingsWindow).FullName + ".showAdvancedOptions", false);
                return showAdvancedOptions.Value;
            }
            set
            {
                if (showAdvancedOptions != value)
                {
                    showAdvancedOptions = value;
                    EditorPrefs.SetBool(typeof(EditorAssetBundleSettingsWindow).FullName + ".showAdvancedOptions", showAdvancedOptions.Value);
                }
            }
        }

        private void OnGUI()
        {
            //BuildAssetBundleSettings config = Config;

            using (EditorAssetBundles.EditorLocalizationValues.BeginScope())
            {
                using (new GUILayout.HorizontalScope())
                {
                    //GUILayout.Label("Mode".Localization(), GUILayout.ExpandWidth(false));
                    EditorAssetBundles.Mode = (AssetBundleMode)EditorGUILayout.EnumPopup(EditorAssetBundles.Mode, GUILayout.ExpandWidth(false));
                    if (GUILayout.Button("Release".Localization()))
                    {
                        BuildAssetBundles.Release();
                    }
                    if (GUILayout.Button("Build".Localization()))
                    {
                        BuildAssetBundles.Build();
                    }
                }

                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Refresh".Localization()))
                    {
                        BuildAssetBundles.UpdateAllAssetBundleNames();
                    }
                    if (GUILayout.Button("Remove Unused Bundle Names".Localization()))
                    {
                        EditorAssetBundles.RemoveUnusedAssetBundleNames();
                    }
                    if (GUILayout.Button("gen AssetBundleNames.dll".Localization()))
                    {
                        BuildAssetBundles.GenerateAssetBundleNamesClass();
                    }
                    if (GUILayout.Button("Delete All Crypto".Localization()))
                    {
                        BuildAssetBundles.DeleteAllCryptoOrSignatureBuildAssetBundle();
                    }
                }

                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Open Directory".Localization());
                    if (GUILayout.Button("Local".Localization()))
                    {
                        EditorAssetBundles.OpenLocalDirectory_Menu();
                    }
                    if (GUILayout.Button("StreamingAssets"))
                    {
                        EditorAssetBundles.OpenStreamingAssetsDirectory_Menu();
                    }
                    if (GUILayout.Button("Output".Localization()))
                    {
                        EditorAssetBundles.OpenBuildDirectory_Menu();
                    }
                }
                GUIVersionList();


                using (var sv = new GUILayout.ScrollViewScope(scrollPos))
                using (var checker = new EditorGUI.ChangeCheckScope())
                using (var enabledScope = new EditorGUILayout.ToggleGroupScope(new GUIContent("Enabled".Localization()), EditorAssetBundleSettings.Enabled))
                {
                    scrollPos = sv.scrollPosition;
                    EditorAssetBundleSettings.Enabled = enabledScope.enabled;
                    ShowAdvancedOptions = EditorGUILayout.Toggle("Show Advanced Options".Localization(), ShowAdvancedOptions);

                    EditorAssetBundleSettings.Options = (BuildAssetBundleOptions)EditorGUILayout.EnumFlagsField(new GUIContent("Options".Localization()), EditorAssetBundleSettings.Options);

                    AssetBundleSettings.BundleVersion = EditorGUILayout.DelayedTextField(new GUIContent("Bundle Version".Localization()), AssetBundleSettings.BundleVersion ?? string.Empty);
                    AssetBundleSettings.Channel = EditorGUILayout.DelayedTextField(new GUIContent("Channel".Localization()), AssetBundleSettings.Channel ?? string.Empty);

                    if (ShowAdvancedOptions)
                    {
                        AssetBundleSettings.BuildManifestPath = new GUIContent("Build Path".Localization()).FolderField(AssetBundleSettings.BuildManifestPath ?? string.Empty, "AssetBundle Build Path", relativePath: ".");
                        AssetBundleSettings.StreamingAssetsManifestPath = new GUIContent("StreamingAssets Path".Localization()).FolderField(AssetBundleSettings.StreamingAssetsManifestPath ?? string.Empty, "StreamingAssets Path", relativePath: ".");
                        EditorAssetBundleSettings.StreamingAssetsExcludeGroup = EditorGUILayout.DelayedTextField(new GUIContent("StreamingAssets Exclude Group".Localization()), EditorAssetBundleSettings.StreamingAssetsExcludeGroup ?? string.Empty);

                        AssetBundleSettings.LocalManifestPath = EditorGUILayout.DelayedTextField(new GUIContent("Local Path".Localization()), AssetBundleSettings.LocalManifestPath ?? string.Empty);

                    }


                    EditorAssetBundleSettings.AssetBundleName = EditorGUILayout.DelayedTextField(new GUIContent("AssetBundle Name".Localization()), EditorAssetBundleSettings.AssetBundleName ?? string.Empty);

                    using (new GUILayout.HorizontalScope())
                    {
                        EditorAssetBundleSettings.AssetName = EditorGUILayout.DelayedTextField(new GUIContent("Asset Name".Localization()), EditorAssetBundleSettings.AssetName ?? string.Empty);
                        GUILayout.Label("Lower".Localization(), GUILayout.ExpandWidth(false));
                        EditorAssetBundleSettings.AssetNameToLower = GUILayout.Toggle(EditorAssetBundleSettings.AssetNameToLower, GUIContent.none, GUILayout.ExpandWidth(false));
                    }

                    if (ShowAdvancedOptions)
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            EditorGUILayout.PrefixLabel(new GUIContent("App Version Format".Localization()));
                            AssetBundleSettings.AppVersionFormat = EditorGUILayoutx.DelayedPlaceholderField(AssetBundleSettings.AppVersionFormat ?? string.Empty, new GUIContent("{0}.{1}.{2}"));
                        }
                        AssetBundleSettings.VersionFile = EditorGUILayout.DelayedTextField(new GUIContent("Version File".Localization()), AssetBundleSettings.VersionFile ?? string.Empty);

                        EditorAssetBundleSettings.BundleCodeResetOfAppVersion = EditorGUILayout.Toggle(new GUIContent("BundleCode Reset Of AppVersion".Localization(), "BundleCode Reset Of AppVersion".Localization()), EditorAssetBundleSettings.BundleCodeResetOfAppVersion);



                        if (EditorAssetBundleSettings.AssetBundleNamesClass == null)
                            EditorAssetBundleSettings.AssetBundleNamesClass = new EditorAssetBundleSettings.AssetBundleNamesClassSettings();


                        using (var tg = new EditorGUILayout.ToggleGroupScope("AssetBundleNames Class".Localization(), EditorAssetBundleSettings.AssetBundleNamesClassSettings.Enabled))
                        {
                            EditorGUI.indentLevel++;
                            using (new GUILayout.VerticalScope())
                            {
                                EditorAssetBundleSettings.AssetBundleNamesClassSettings.Enabled = tg.enabled;
                                EditorAssetBundleSettings.AssetBundleNamesClassSettings.FilePath = EditorGUILayout.DelayedTextField("File Path".Localization(), EditorAssetBundleSettings.AssetBundleNamesClassSettings.FilePath);
                                EditorAssetBundleSettings.AssetBundleNamesClassSettings.AssetNameClass = EditorGUILayout.DelayedTextField("Asset Name Class".Localization(), EditorAssetBundleSettings.AssetBundleNamesClassSettings.AssetNameClass);
                            }
                            EditorGUI.indentLevel--;
                        }
                    }



                    using (new GUILayout.HorizontalScope())
                    {
                        var obj = (AssetBundleGroup)EditorGUILayout.ObjectField("Local Group".Localization(), EditorAssetBundleSettings.LocalGroup.Asset, typeof(AssetBundleGroup), false);
                        if (EditorAssetBundleSettings.LocalGroup.Asset != obj)
                        {
                            var v = EditorAssetBundleSettings.LocalGroup;
                            v.Asset = obj;
                            EditorAssetBundleSettings.LocalGroup = v;  
                        }

                        if (!EditorAssetBundleSettings.LocalGroup)
                        {
                            if (GUILayout.Button("Create".Localization(), GUILayout.ExpandWidth(false)))
                            {
                                string path = EditorUtility.SaveFilePanel("AssetBundle Group", "Assets", BuildAssetBundles.LocalGroupName, "asset");
                                if (!string.IsNullOrEmpty(path))
                                {
                                    var asset = ScriptableObject.CreateInstance<AssetBundleGroup>();
                                    asset.items.Add(new AssetBundleGroup.BundleItem());
                                    path = path.Substring(Path.GetFullPath(".").Length + 1);
                                    AssetDatabase.CreateAsset(asset, path);

                                    EditorAssetBundleSettings.LocalGroup = new AssetObjectReferenced(asset);
                                }
                            }
                        }
                    }
            
                    if (!EditorAssetBundleSettings.LocalGroup)
                    {
                        EditorGUILayout.HelpBox("Require local group".Localization(), MessageType.Error);
                    }

                    EditorAssetBundleSettings.Groups = new GUIContent("Groups".Localization()).ArrayField(EditorAssetBundleSettings.Groups, (item, index) =>
                    {
                        var val= (AssetBundleGroup)EditorGUILayout.ObjectField(item.Asset, typeof(AssetBundleGroup), false);
                        if (val != item.Asset)
                        {
                            item.Asset = val;
                            GUI.changed = true;
                        }
                        return item;
                    }, initExpand: true) as AssetObjectReferenced[];

               

                    using (var g = new EditorGUILayoutx.Scopes.FoldoutHeaderGroupScope(false, new GUIContent("Download".Localization())))
                    {
                        if (g.Visiable)
                        {
                            using (new EditorGUILayoutx.Scopes.IndentLevelVerticalScope())
                            {
                                AssetBundleSettings.DownloadUrl = EditorGUILayoutx.DelayedPlaceholderField(new GUIContent("Download Url".Localization()), AssetBundleSettings.DownloadUrl, new GUIContent("http://"));
                                AssetBundleSettings.RequireDownload = EditorGUILayout.Toggle("Require Download".Localization(), AssetBundleSettings.RequireDownload);
                                AssetBundleSettings.DownloadManifestPath = EditorGUILayout.DelayedTextField(new GUIContent("Download Manifest Path".Localization()), AssetBundleSettings.DownloadManifestPath ?? string.Empty);
                                AssetBundleSettings.DownloadVersionFile = EditorGUILayout.DelayedTextField(new GUIContent("Download Version File".Localization()), AssetBundleSettings.DownloadVersionFile ?? string.Empty);
                                AssetBundleSettings.ReleasePath = EditorGUILayout.DelayedTextField(new GUIContent("Release Path".Localization()), AssetBundleSettings.ReleasePath ?? string.Empty);
                            }
                        }
                    }

                    if (EditorAssetBundleSettings.PreBuildPlayer == null)
                        EditorAssetBundleSettings.PreBuildPlayer = new EditorAssetBundleSettings.PreBuildPlayerSettings();

                    using (var g = new EditorGUILayoutx.Scopes.FoldoutHeaderGroupScope(false, new GUIContent("Pre Build Player".Localization())))
                    {
                        if (g.Visiable)
                        {
                            EditorGUI.indentLevel++;
                            using (new GUILayout.VerticalScope())
                            {
                                EditorAssetBundleSettings.PreBuildPlayerSettings.AutoBuildAssetBundle = EditorGUILayout.Toggle("Auto Build AssetBundle".Localization(), EditorAssetBundleSettings.PreBuildPlayerSettings.AutoBuildAssetBundle);
                            }
                            EditorGUI.indentLevel--;
                        }
                    }

                    if (EditorAssetBundleSettings.PostBuildPlayer == null)
                        EditorAssetBundleSettings.PostBuildPlayer = new EditorAssetBundleSettings.PostBuildPlayerSettings();

                    using (var g = new EditorGUILayoutx.Scopes.FoldoutHeaderGroupScope(false, new GUIContent("Post Build Player".Localization())))
                    {
                        if (g.Visiable)
                        {
                            EditorGUI.indentLevel++;
                            using (new GUILayout.VerticalScope())
                            {
                                EditorAssetBundleSettings.PostBuildPlayerSettings.ClearStreamingAssets = EditorGUILayout.Toggle("Clear StreamingAssets".Localization(), EditorAssetBundleSettings.PostBuildPlayerSettings.ClearStreamingAssets);
                            }
                            EditorGUI.indentLevel--;
                        }
                    }

                    if (EditorAssetBundleSettings.PostBuild == null)
                        EditorAssetBundleSettings.PostBuild = new EditorAssetBundleSettings.PostBuildSettings();

                    using (var g = new EditorGUILayoutx.Scopes.FoldoutHeaderGroupScope(false, new GUIContent("Post Build".Localization())))
                    {
                        if (g.Visiable)
                        {
                            EditorGUI.indentLevel++;
                            using (new GUILayout.VerticalScope())
                            {
                                EditorAssetBundleSettings.PostBuildSettings.ShowFolder = EditorGUILayout.Toggle("Show Folder".Localization(), EditorAssetBundleSettings.PostBuildSettings.ShowFolder);
                            }
                            EditorGUI.indentLevel--;
                        }
                    }

                    if (EditorAssetBundleSettings.IgnorePaths == null)
                        EditorAssetBundleSettings.IgnorePaths = new string[0];
                    EditorAssetBundleSettings.IgnorePaths = new GUIContent("Exclude Directory".Localization()).ArrayField(EditorAssetBundleSettings.IgnorePaths, (item, index) =>
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            item = EditorGUILayout.DelayedTextField(item);
                        }
                        return item;
                    }, createInstance: () => "") as string[];

             
                    EditorAssetBundleSettings.ReleasePath = EditorGUILayout.DelayedTextField(new GUIContent("Release Path".Localization()), EditorAssetBundleSettings.ReleasePath);

                    if (EditorAssetBundleSettings.ExcludeExtensions == null)
                        EditorAssetBundleSettings.ExcludeExtensions = new string[0];
                    EditorAssetBundleSettings.ExcludeExtensions = new GUIContent("Exclude Extension".Localization()).ArrayField(EditorAssetBundleSettings.ExcludeExtensions, (item, index) =>
                    {
                        item = EditorGUILayout.DelayedTextField(item);
                        return item;
                    }, createInstance: () => "") as string[];


                    if (EditorAssetBundleSettings.ExcludeTypeNames == null)
                        EditorAssetBundleSettings.ExcludeTypeNames = new string[0];
                    EditorAssetBundleSettings.ExcludeTypeNames = new GUIContent("Exclude Type Name".Localization()).ArrayField(EditorAssetBundleSettings.ExcludeTypeNames, (item, index) =>
                    {
                        item = EditorGUILayout.DelayedTextField(item);
                        return item;
                    }, createInstance: () => "") as string[];

                    if (EditorAssetBundleSettings.ExcludeDependencyExtensions == null)
                        EditorAssetBundleSettings.ExcludeDependencyExtensions = new string[0];
                    EditorAssetBundleSettings.ExcludeDependencyExtensions = new GUIContent("Exclude Dependency Extension".Localization()).ArrayField(EditorAssetBundleSettings.ExcludeDependencyExtensions, (item, index) =>
                    {
                        return EditorGUILayout.DelayedTextField(item);
                    }, createInstance: () => "") as string[];

                    if (showAdvancedOptions.Value)
                    {
                        using (var g = new EditorGUILayoutx.Scopes.FoldoutHeaderGroupScope(false, new GUIContent("Auto Dependency".Localization())))
                        {
                            if (g.Visiable)
                            {
                                using (new EditorGUILayoutx.Scopes.IndentLevelVerticalScope())
                                {
                                    EditorAssetBundleSettings.AutoDependencyBundleName = EditorGUILayout.DelayedTextField("Bundle Name".Localization(), EditorAssetBundleSettings.AutoDependencyBundleName);
                                    EditorAssetBundleSettings.AutoDependencySplit = EditorGUILayout.DelayedIntField("Bundle Split".Localization(), EditorAssetBundleSettings.AutoDependencySplit);
                                }
                            }
                        }

                    }

                    using (var tg = new EditorGUILayout.ToggleGroupScope("Preload".Localization(), AssetBundleSettings.PreloadEnabled))
                    {
                        AssetBundleSettings.PreloadEnabled = tg.enabled;
                        if (tg.enabled)
                        {
                            EditorGUI.indentLevel++;
                            using (new EditorGUILayoutx.Scopes.IndentLevelVerticalScope("box"))
                            {
                                AssetBundleSettings.PreloadInclude = EditorGUILayout.DelayedTextField(new GUIContent("Include".Localization(), "AssetBundle Name"), AssetBundleSettings.PreloadInclude ?? string.Empty);
                                AssetBundleSettings.PreloadExclude = EditorGUILayout.DelayedTextField("Exclude".Localization(), AssetBundleSettings.PreloadExclude ?? string.Empty);
                            }
                            EditorGUI.indentLevel--;
                        }
                    }

                    using (var tg = new EditorGUILayout.ToggleGroupScope("Crypto".Localization(), AssetBundleSettings.CryptoEnabled))
                    {
                        AssetBundleSettings.CryptoEnabled = tg.enabled;
                        if (tg.enabled)
                        {
                            EditorGUI.indentLevel++;
                            using (new EditorGUILayoutx.Scopes.IndentLevelVerticalScope("box"))
                            {
                                EditorAssetBundleSettings.CryptoKey = EditorGUILayoutx.Base64TextField(new GUIContent("Crypto Key".Localization()), EditorAssetBundleSettings.CryptoKey ?? string.Empty, 8);
                                EditorAssetBundleSettings.CryptoIV = EditorGUILayoutx.Base64TextField(new GUIContent("Crypto IV".Localization()), EditorAssetBundleSettings.CryptoIV ?? string.Empty, 8);
                                AssetBundleSettings.CryptoInclude = EditorGUILayout.DelayedTextField(new GUIContent("Include".Localization(), "AssetBundle Name"), AssetBundleSettings.CryptoInclude ?? string.Empty);
                                AssetBundleSettings.CryptoExclude = EditorGUILayout.DelayedTextField("Exclude".Localization(), AssetBundleSettings.CryptoExclude ?? string.Empty);
                            }
                            EditorGUI.indentLevel--;
                        }
                    }

                    using (var tg = new EditorGUILayout.ToggleGroupScope("Signature".Localization(), AssetBundleSettings.SignatureEnabled))
                    {
                        AssetBundleSettings.SignatureEnabled = tg.enabled;
                        if (tg.enabled)
                        {
                            EditorGUI.indentLevel++;
                            using (new EditorGUILayoutx.Scopes.IndentLevelVerticalScope("box"))
                            {
                                string pubKey;
                                EditorAssetBundleSettings.SignatureKeyPath = EditorGUILayoutx.CryptoKeyField(new GUIContent("SignatureKeyPath".Localization()), EditorAssetBundleSettings.SignatureKeyPath ?? string.Empty, out pubKey);
                                if (!string.IsNullOrEmpty(pubKey))
                                    AssetBundleSettings.SignaturePublicKey = pubKey;
                                using (new EditorGUI.DisabledGroupScope(!string.IsNullOrEmpty(EditorAssetBundleSettings.SignatureKeyPath)))
                                {
                                    AssetBundleSettings.SignaturePublicKey = EditorGUILayout.DelayedTextField(new GUIContent("SignaturePubKey".Localization()), AssetBundleSettings.SignaturePublicKey ?? string.Empty);
                                }
                                AssetBundleSettings.SignatureInclude = EditorGUILayout.DelayedTextField(new GUIContent("Include".Localization(), "AssetBundle Name"), AssetBundleSettings.SignatureInclude ?? string.Empty);
                                AssetBundleSettings.SignatureExclude = EditorGUILayout.DelayedTextField("Exclude".Localization(), AssetBundleSettings.SignatureExclude ?? string.Empty);
                            }
                            EditorGUI.indentLevel--;
                        }
                    }

                    GUILayout.Space(10);

                    if (checker.changed)
                    {
                        //Debug.Log("Changed [" + config.IgnorePaths.Last()+"]");
                        //Save();

                        EditorAssetBundleSettings.Provider.Save();
                    }
                }
            }


        }


        internal static AssetBundleVersion[] versionList;
        static Vector2 versionListScrollPos;

        void GUIVersionList()
        {

            using (var header = new EditorGUILayoutx.Scopes.FoldoutHeaderGroupScope(true, new GUIContent("Version".Localization())))
            {
                if (header.Visiable)
                {
                    if (versionList == null)
                    {
                        List<AssetBundleVersion> list = new List<AssetBundleVersion>();
                        string dir = BuildAssetBundles.GetOutputPath();
                        if (File.Exists(dir))
                        {
                            foreach (var file in Directory.GetFiles(dir, AssetBundleSettings.VersionFile, SearchOption.AllDirectories))
                            {
                                try
                                {
                                    var item = JsonUtility.FromJson<AssetBundleVersion>(File.ReadAllText(file, Encoding.UTF8));
                                    if (item != null)
                                        list.Add(item);
                                }
                                catch { }
                            }
                        }
                        versionList = list.ToArray();
                    }
                    if (versionList.Length > 0)
                    {
                        using (var sv = new GUILayout.ScrollViewScope(versionListScrollPos, GUILayout.MinHeight(0), GUILayout.MaxHeight(200)))
                        {
                            versionListScrollPos = sv.scrollPosition;
                            using (new EditorGUILayoutx.Scopes.IndentLevelVerticalScope())
                            {
                                foreach (var g in versionList.OrderByDescending(o => o.bundleCode)
                                    .OrderByDescending(o => Version.Parse(o.appVersion))
                                    .GroupBy(o => o.bundleCode)
                                    .ToArray())
                                {

                                    GUILayout.Label(new GUIContent(g.Key.ToString(), "Bundle Code".Localization()));

                                    foreach (var item in g)
                                    {
                                        using (new EditorGUILayoutx.Scopes.IndentLevelVerticalScope())
                                        using (var checker = new EditorGUI.ChangeCheckScope())
                                        {
                                            //using (new GUILayout.HorizontalScope())
                                            //{
                                            //    GUILayout.Label(item.platform);
                                            //    GUILayout.FlexibleSpace();
                                            //    GUILayout.Label(item.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"), GUILayout.ExpandWidth(false));
                                            //    if (GUILayout.Button("Delete".Localization(), GUILayout.ExpandWidth(false)))
                                            //    {
                                            //        if (EditorUtility.DisplayDialog("Delete".Localization(), "Delete".Localization() + $" {item.platform} <{item.bundleCode}>", "ok".Localization(), "cancel".Localization()))
                                            //        {
                                            //            remove = item;
                                            //        }
                                            //    }
                                            //}

                                            using (new EditorGUILayoutx.Scopes.IndentLevelVerticalScope())
                                            {
                                                using (new GUILayout.HorizontalScope())
                                                {
                                                    EditorGUILayout.PrefixLabel("App Version".Localization());
                                                    GUILayout.Label(item.appVersion.ToString());
                                                }

                                                EditorGUILayout.LabelField("Hash".Localization(), item.hash);
                                                EditorGUILayout.LabelField("Commit Id".Localization(), item.commitId);

                                                //using (new GUILayout.HorizontalScope())
                                                //{
                                                //    EditorGUILayout.PrefixLabel("Build Time".Localization());
                                                //    GUILayout.Label(item.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                                                //}

                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        GUILayout.Label("no version, generate menu  <Build/Build AssetBundle>");
                    }
                }
            }

            //if (remove != null)
            //{
            //    //BuildAssetBundles.RemoveRootVersion(remove);
            //    string outputPath = BuildAssetBundles.GetOutputPath(remove);
            //    if (Directory.Exists(outputPath))
            //    {
            //        Directory.Delete(outputPath, true);
            //        BuildAssetBundles.DeleteOutputEmptyDirectory(outputPath);
            //    }

            //    versionList = null;
            //}

        }

    }
}