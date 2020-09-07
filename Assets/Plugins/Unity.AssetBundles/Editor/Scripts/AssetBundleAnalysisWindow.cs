using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace UnityEditor.Build
{ 


    class AssetBundleAnalysisWindow : EditorWindow
    {

        private AssetBundleManifest manifest;
        private string[] allAssetBundleNames;
        Vector2 scrollPos;
        Vector2 scrollPos2;
        bool isDependencyExpanded = true;
        bool isDependencySelfExpanded = true;
        bool isOwnerDependencyExpanded = true;
        bool isOwnerObjectExpanded = true;

        [MenuItem("Build/AssetBundle/Analysis")]
        static void ShowWindow()
        {
            var win = GetWindow<AssetBundleAnalysisWindow>();

            win.titleContent = new GUIContent("AssetBundle");
            win.autoRepaintOnSceneChange = true;
            win.Show();
        }

        private void OnEnable()
        {
        }

        private void Update()
        {
            if (manifest != AssetBundles.Manifest)
            {
                manifest = AssetBundles.Manifest;
                if (manifest)
                {
                    allAssetBundleNames = manifest.GetAllAssetBundles();
                }
            }
        }

        private void OnGUI()
        {
            if (AssetBundles.mainManifest == null)
            {
                GUILayout.Label("no loaded AssetBundleManifest");
                return;
            }

            if (GUILayout.Button("UnloadUnused"))
            {
                AssetBundles.UnloadUnused(true);
            }

            using (new GUILayout.HorizontalScope())
            {
                using (var sv = new GUILayout.ScrollViewScope(scrollPos, "box", GUILayout.MaxWidth(Screen.width * 0.3f)))
                {
                    scrollPos = sv.scrollPosition;
                    foreach (var abInfo in AssetBundles.mainManifest.AssetBundleInfos.OrderBy(o => o.Name))
                    {
                        DrawAssetBundle(abInfo);
                    }
                }

                using (var sv = new GUILayout.ScrollViewScope(scrollPos2, "box", GUILayout.MaxWidth(Screen.width * 0.7f)))
                {
                    scrollPos2 = sv.scrollPosition;

                    DrawDetails(selectedAssetBundleInfo);
                }
            }
        }

        private AssetBundles.AssetBundleInfo selectedAssetBundleInfo;
        static GUIStyle bgStyle;

        static GUIStyle BgStyle
        {
            get
            {
                if (bgStyle == null)
                {
                    GUIStyle style = new GUIStyle();
                    style.normal.background = EditorGUIUtility.whiteTexture;
                    bgStyle = style;
                }
                return bgStyle;
            }
        }

        bool IsAssetBundleLoaded(string assetBundleName)
        {
            bool isLoaded = false;
            var abInfo = AssetBundles.mainManifest.GetAssetBundleInfo(assetBundleName);
            AssetBundles.AssetBundleRef abRef;
            if (AssetBundles.abRefs.TryGetValue(abInfo.Key, out abRef))
            {
                if (abRef.AssetBundle)
                {
                    isLoaded = true;
                }
            }
            return isLoaded;
        }

        void DrawAssetBundle(AssetBundles.AssetBundleInfo abInfo)
        {
            bool isLoaded = IsAssetBundleLoaded(abInfo.Name);
            string loadError = null;
            AssetBundles.AssetBundleRef abRef;
            if (AssetBundles.abRefs.TryGetValue(abInfo.Key, out abRef))
            {
                if (abRef.AssetBundle)
                {
                }
                else
                {
                    if (abRef.Error != null)
                    {
                        loadError = abRef.Error.Message;
                    }
                }
            }



            int controllId;
            controllId = GUIUtility.GetControlID(FocusType.Passive);
            AssetBundleItemState state = (AssetBundleItemState)GUIUtility.GetStateObject(typeof(AssetBundleItemState), controllId);


            if (selectedAssetBundleInfo == abInfo)
            {
                GUI.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.1f);
            }
            else
            {
                GUI.backgroundColor = Color.clear;
            }
            using (new GUILayout.HorizontalScope(BgStyle))
            {

                if (isLoaded)
                    GUI.color = Color.white;
                else if (loadError != null)
                    GUI.color = Color.red;
                else
                    GUI.color = Color.gray;

                if (GUILayout.Button(abInfo.Name, "label"))
                {
                    state.isExpanded = !state.isExpanded;
                    selectedAssetBundleInfo = abInfo;
                }
                GUI.color = Color.white;

                if (state.isExpanded)
                {

                }
            }
            GUI.backgroundColor = Color.white;

        }


        void DrawDetails(AssetBundles.AssetBundleInfo abInfo)
        {
            if (abInfo == null)
                return;
            bool isLoaded = IsAssetBundleLoaded(abInfo.Name);
            string loadError = null;
            AssetBundles.AssetBundleRef abRef;
            if (AssetBundles.abRefs.TryGetValue(abInfo.Key, out abRef))
            {
                if (abRef.AssetBundle)
                {
                }
                else
                {
                    if (abRef.Error != null)
                    {
                        loadError = abRef.Error.Message;
                    }
                }
            }

            GUILayout.Label(abInfo.Name);
            GUILayout.Space(5);
            if (loadError != null)
            {
                GUI.color = Color.red;
                GUILayout.Label(loadError);
                GUI.color = Color.white;
            }

            var deps = manifest.GetDirectDependencies(abInfo.Name);

            if (GUILayout.Button(string.Format("Dependency ({0})", deps.Length), "label"))
            {
                isDependencyExpanded = !isDependencyExpanded;
            }

            if (isDependencyExpanded)
            {
                foreach (var dep in deps)
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Space(16);
                        GUILayout.Label(dep);
                    }
                }
            }
            GUILayout.Space(5);


            var depSelf = (from abName in AssetBundles.AllAssetBundleNames()
                           where manifest.GetDirectDependencies(abName).Contains(abInfo.Name)
                           select abName).ToArray();
            if (GUILayout.Button(string.Format("Dependency Self ({0})", depSelf.Length), "label"))
            {
                isDependencySelfExpanded = !isDependencySelfExpanded;
            }

            if (isDependencySelfExpanded)
            {
                foreach (var abName in depSelf)
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Space(16);
                        GUILayout.Label(abName);
                    }
                }
            }
            GUILayout.Space(5);

            List<AssetBundles.AssetBundleRef> ownerDeps = new List<AssetBundles.AssetBundleRef>();
            if (abRef != null)
            {
                foreach (var item in AssetBundles.abRefs.Values)
                {
                    if (item == abRef)
                        continue;
                    if (!item.IsDependent)
                        continue;
                    if (item.AssetBundleInfo.Dependencies != null)
                    {
                        foreach (var dep in item.AssetBundleInfo.Dependencies)
                        {
                            if (dep == abRef.AssetBundleInfo)
                            {
                                ownerDeps.Add(item);
                            }
                        }
                    }
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button(string.Format("Owner Dependency ({0})", ownerDeps.Count), "label"))
                {
                    isOwnerDependencyExpanded = !isOwnerDependencyExpanded;
                }
            }

            if (isOwnerDependencyExpanded)
            {
                foreach (var item in ownerDeps)
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Space(16);
                        GUILayout.Label(item.AssetBundleInfo.Name);
                    }
                }
            }

            List<object> ownerObjects = new List<object>();
            if (isLoaded && abRef != null)
            {
                foreach (WeakReference weakRef in abRef.Owners)
                {
                    object obj = weakRef.Target;
                    if (obj != null)
                    {
                        ownerObjects.Add(obj);
                    }
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button(string.Format("Owner Object ({0})", ownerObjects.Count), "label"))
                {
                    isOwnerObjectExpanded = !isOwnerObjectExpanded;
                }
            }
            if (isOwnerObjectExpanded)
            {
                if (isLoaded && abRef != null)
                {

                    foreach (var obj in ownerObjects)
                    {
                        if (obj != null)
                        {
                            using (new GUILayout.HorizontalScope())
                            {
                                GUILayout.Space(16);
                                GUILayout.Label(string.Format("{0} ({1})", (obj.ToString() ?? string.Empty), obj.GetType().FullName));
                            }
                        }
                    }
                }
            }

        }


        private class AssetBundleItemState
        {
            public bool isExpanded;
        }

    }

}