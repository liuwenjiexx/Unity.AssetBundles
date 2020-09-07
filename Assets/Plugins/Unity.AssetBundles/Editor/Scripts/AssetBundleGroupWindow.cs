using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.GUIExtensions;
using UnityEditor.Callbacks;
using System.Linq;
using System;
using Object = UnityEngine.Object;
using UnityEngine.Localizations;

public class AssetBundleGroupWindow : EditorWindow
{
    [NonSerialized]
    public AssetBundleGroup asset;
    public AssetBundleGroup collectionInclude;
    public AssetBundleGroup collectionExclude;


    Vector2 scrollPos;
    string[] allGroupAssetPaths;
    string groupAssetPath;

    public AssetBundleGroup Asset
    {
        get
        {
            if (!asset)
            {
                if (!string.IsNullOrEmpty(groupAssetPath))
                {
                    asset = AssetDatabase.LoadAssetAtPath<AssetBundleGroup>(groupAssetPath);
                    if (!asset)
                        groupAssetPath = null;
                }
            }
            return asset;
        }
        set
        {
            if (asset != value)
            {
                asset = value;
                if (asset)
                {
                    groupAssetPath = AssetDatabase.GetAssetPath(asset);
                }
                else
                {
                    groupAssetPath = null;
                }
                OnAssetChanged();
            }
        }
    }


    [MenuItem(EditorAssetBundles.MenuPrefix + "Group")]
    static void OpenWindow()
    {
        var win = GetWindow<AssetBundleGroupWindow>();
        win.OnAssetChanged();
        win.Show();
    }



    void OnAssetChanged()
    {
        //if (asset)
        //{
        //    titleContent = new GUIContent(asset.name + " Group");
        //}
        //else
        //{
        //    titleContent = new GUIContent("AssetBundle Group");
        //}
        UpdateAllAssetPaths();
    }

    [OnOpenAsset(1)]
    public static bool OnOpenAsset(int instanceID, int line)
    {
        Object asset = EditorUtility.InstanceIDToObject(instanceID);

        if (asset is AssetBundleGroup)
        {
            var win = GetWindow<AssetBundleGroupWindow>();
            win.Asset = asset as AssetBundleGroup;
            win.Show();
            return true;
        }

        return false;
    }

    void LoadAssetCallback(string assetBundleName, string assetPath)
    {

        if (!(collectionInclude || collectionExclude))
        {
            EditorAssetBundles.LoadAssetCallback -= LoadAssetCallback;
            return;
        }
        //Debug.Log("Load Asset: " + assetPath);
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (!string.IsNullOrEmpty(guid))
        {
            bool changed = false;

            if (collectionInclude)
            {
                var item = BuildAssetBundles.FindGroupItem(collectionInclude, assetPath);
                if (item != null)
                {
                    if (!item.includeGuids.Contains(guid))
                    {
                        item.includeGuids.Add(guid);
                        Debug.Log(AssetBundles.LogPrefix + $"<{collectionInclude.name}> Group Include : {assetPath}");
                        changed = true;
                        EditorUtility.SetDirty(collectionInclude);
                    }
                }
            }
            else if (collectionExclude)
            {
                var item = BuildAssetBundles.FindGroupItem(collectionExclude, assetPath);
                if (item != null)
                {
                    if (!item.excludeGuids.Contains(guid))
                    {
                        item.excludeGuids.Add(guid);
                        Debug.Log(AssetBundles.LogPrefix + $"<{collectionExclude.name}> Group Exclude : {assetPath}");
                        changed = true;
                        EditorUtility.SetDirty(collectionExclude);
                    }
                }
            }
            if (changed)
            {
                Repaint();
            }
        }

    }


    private void OnEnable()
    {
        titleContent = new GUIContent("AssetBundle Group");
        EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
        if (allGroupAssetPaths == null)
            UpdateAllAssetPaths();
    }

    private void EditorApplication_playModeStateChanged(PlayModeStateChange obj)
    {
        if (obj == PlayModeStateChange.EnteredPlayMode)
        {
            if (collectionExclude)
            {
                EditorAssetBundles.LoadAssetCallback -= LoadAssetCallback;
                EditorAssetBundles.LoadAssetCallback += LoadAssetCallback;
            }
        }
    }


    private void Update()
    {
        if (collectionInclude || collectionExclude)
        {
            //if (!EditorApplication.isPlayingOrWillChangePlaymode)
            //{
            //    StopCollection();
            //}
        }
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
        //EditorAssetBundles.LoadAssetCallback -= LoadAssetCallback;
        //recording = null;
    }


    void StartCollection()
    {

    }

    void StopCollection()
    {
        EditorAssetBundles.LoadAssetCallback -= LoadAssetCallback;
        collectionInclude = null;
        collectionExclude = null;
        //EditorApplication.isPlaying = false;
    }

    public AssetBundleGroup CreateAsset()
    {

        //string dir = AssetDatabase.GetAssetPath(Selection.activeObject);
        //if (string.IsNullOrEmpty(dir))
        //{
        //    dir = "Assets";
        //}
        //else
        //{
        //    if (!Directory.Exists(dir))
        //    {
        //        dir = Path.GetDirectoryName(dir);
        //    }
        //}
        //AssetDatabase.CreateAsset(asset, dir + "/AssetBundleCollection.asset");
        string path = EditorUtility.SaveFilePanel("AssetBundle Group", "Assets", "AssetBundleGroup", "asset");
        if (!string.IsNullOrEmpty(path))
        {
            var asset = ScriptableObject.CreateInstance<AssetBundleGroup>();

            asset.items.Add(new AssetBundleGroup.BundleItem());

            path = path.Substring(Path.GetFullPath(".").Length + 1);
            AssetDatabase.CreateAsset(asset, path);
            Asset = asset;
            UpdateAllAssetPaths();
        }
        return asset;
    }

    /*
    void Reload()
    {
        foreach (var item in asset.items)
            item.guids.Clear();

        foreach (var bundleName in AssetDatabase.GetAllAssetBundleNames())
        {
            foreach (var assetPath in AssetDatabase.GetAssetPathsFromAssetBundle(bundleName))
            {
                var item = BuildAssetBundles.FindGroupItem(asset, assetPath);

                if (item != null)
                {
                    string guid = AssetDatabase.AssetPathToGUID(assetPath);
                    if (!item.guids.Contains(guid))
                    {
                        item.guids.Add(guid);
                    }
                }
            }
        }
        EditorUtility.SetDirty(asset);
    }*/


    void ValidateAssets()
    {
        var asset = Asset;
        string assetPath;
        bool changed = false;
        for (int i = 0; i < asset.items.Count; i++)
        {
            var item = asset.items[i];
            for (int j = 0; j < item.excludeGuids.Count; j++)
            {
                string guid = item.excludeGuids[j];
                assetPath = AssetDatabase.GUIDToAssetPath(guid);

                if (string.IsNullOrEmpty(assetPath))
                {
                    item.excludeGuids.RemoveAt(j);
                    changed = true;
                    j--;
                    Debug.Log(AssetBundles.LogPrefix + $"Remove <{guid}>. missing");
                    continue;
                }
                if (!BuildAssetBundles.IsMatch(item, assetPath))
                {
                    item.excludeGuids.RemoveAt(j);
                    changed = true;
                    j--;
                    Debug.Log(AssetBundles.LogPrefix + $"Remove <{ assetPath}>. no matching include & exclude");
                    continue;
                }
            }
        }
        if (changed)
        {
            EditorUtility.SetDirty(asset);
        }
    }

    void UpdateAllAssetPaths()
    {
        allGroupAssetPaths = AssetDatabase.FindAssets("t:" + typeof(AssetBundleGroup).Name)
            .Select(AssetDatabase.GUIDToAssetPath)
            .ToArray();
    }

    Dictionary<AssetBundleGroup.BundleItem, List<string[]>> cachedAssets;

    void Load(AssetBundleGroup.BundleItem item)
    {
        if (cachedAssets == null)
            cachedAssets = new Dictionary<AssetBundleGroup.BundleItem, List<string[]>>();
        HashSet<string> list = new HashSet<string>();
        DateTime dt = DateTime.Now;
        var all = AssetDatabase.GetAllAssetPaths();
        //Debug.Log((DateTime.Now - dt).TotalMilliseconds.ToString("0.#"));

        foreach (var assetPath in all)
        {
            if (Directory.Exists(assetPath))
                continue;

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (item.IsMatch(assetPath, guid))
            {
                list.Add(guid);
            }
        }


        cachedAssets[item] = list.Select(o => new string[] { o, AssetDatabase.GUIDToAssetPath(o) }).OrderBy(o => o[1]).ToList();
        Debug.Log("total: " + list.Count + ", time " + (DateTime.Now - dt).TotalSeconds.ToString("0.#") + "s");
    }

    bool[] tttt = new bool[10];
    int displayMax = 100;

    public void OnGUI()
    {

        using (EditorAssetBundles.EditorLocalizationValues.BeginScope())
        {
            using (new GUILayout.HorizontalScope())
            {
                int selectedIndex = -1;
                if (groupAssetPath != null)
                {
                    selectedIndex = Array.IndexOf(allGroupAssetPaths, groupAssetPath);
                }
                using (var checker = new EditorGUI.ChangeCheckScope())
                {
                    selectedIndex = EditorGUILayout.Popup(selectedIndex, allGroupAssetPaths.Select(o => Path.GetFileNameWithoutExtension(o)).ToArray());
                    if (checker.changed)
                    {
                        Asset = AssetDatabase.LoadAssetAtPath<AssetBundleGroup>(allGroupAssetPaths[selectedIndex]);
                    }
                }
                if (GUILayout.Button("New", GUILayout.ExpandWidth(false)))
                {
                    CreateAsset();
                }
            }
            if (!Asset)
            {
                return;
            }


            string assetPath;


            asset.IsLocal = EditorGUILayout.Toggle("Local".Localization(), asset.IsLocal);
            asset.IsDebug = EditorGUILayout.Toggle("Debug".Localization(), asset.IsDebug);

            //using (new GUILayout.HorizontalScope())
            //{
            //    GUILayout.Label("Items");

            //    if (GUILayout.Button("+", "label", GUILayout.ExpandWidth(false)))
            //    {
            //        asset.items.Add(new AssetBundleGroup.Item());
            //    }
            //}

            using (var sv = new GUILayout.ScrollViewScope(scrollPos))
            using (var checker = new EditorGUI.ChangeCheckScope())
            {
                scrollPos = sv.scrollPosition;

                asset.items = (List<AssetBundleGroup.BundleItem>)new GUIContent("Items".Localization()).ArrayField(asset.items, (item, index) =>
                //for (int i = 0; i < asset.items.Count; i++)
                {

                    //using (new EditorGUILayoutx.Scopes.IndentLevelVerticalScope("box"))
                    {
                        //AssetBundleGroup.Item item = asset.items[i];
                        //if (item.includes == null)
                        //    item.includes = new string[0];
                        //item.includes = (string[])EditorGUILayoutx.ArrayField(new GUIContent("Include"), item.includes, (o, index) =>
                        //  {
                        //      return EditorGUILayout.DelayedTextField(o);
                        //  }, initExpand: true, createInstance: () => string.Empty);

                        //if (item.excludes == null)
                        //    item.excludes = new string[0];
                        //item.excludes = (string[])EditorGUILayoutx.ArrayField(new GUIContent("Exclude"), item.excludes, (o, index) =>
                        //{
                        //    return EditorGUILayout.DelayedTextField(o);
                        //}, initExpand: true, createInstance: () => string.Empty);

                        item.include = EditorGUILayout.DelayedTextField(new GUIContent("Include".Localization()), item.include);
                        if (string.IsNullOrEmpty(item.include))
                        {
                            EditorGUILayout.HelpBox("Empty".Localization(), MessageType.Error);
                        }

                        item.exclude = EditorGUILayout.DelayedTextField(new GUIContent("Exclude".Localization()), item.exclude);

                        item.bundleName = EditorGUILayoutx.DelayedPlaceholderField(new GUIContent("AssetBundle Name".Localization()), item.bundleName ?? string.Empty, new GUIContent(EditorAssetBundleSettings.AssetBundleName));

                        using (new GUILayout.HorizontalScope())
                        {
                            item.assetName = EditorGUILayoutx.DelayedPlaceholderField(new GUIContent("Asset Name".Localization()), item.assetName ?? string.Empty, new GUIContent(EditorAssetBundleSettings.AssetName));
                            using (new EditorGUI.DisabledGroupScope(string.IsNullOrEmpty(item.assetName)))
                            {
                                GUILayout.Label("Lower".Localization(), GUILayout.ExpandWidth(false));
                                if (string.IsNullOrEmpty(item.assetName))
                                {
                                    GUILayout.Toggle(EditorAssetBundleSettings.AssetNameToLower, GUIContent.none, GUILayout.ExpandWidth(false));
                                }
                                else
                                {
                                    item.assetNameToLower = GUILayout.Toggle(item.assetNameToLower, GUIContent.none, GUILayout.ExpandWidth(false));
                                }

                            }
                        }

                        item.variants = new GUIContent("Variant".Localization() + $" ({item.variants.Count})").ArrayField(item.variants, (variantItem, variantIndex) =>
                          {
                              variantItem.include = EditorGUILayout.DelayedTextField(new GUIContent("Include".Localization(), "Pattern"), variantItem.include ?? string.Empty);
                              variantItem.exclude = EditorGUILayout.DelayedTextField(new GUIContent("Exclude".Localization(), "Pattern"), variantItem.exclude ?? string.Empty);
                              variantItem.variant = EditorGUILayout.DelayedTextField("Variant".Localization(), variantItem.variant ?? string.Empty);
                              return variantItem;
                          }, initExpand: false, itemStyle: "box") as List<AssetBundleGroup.BundleVariant>;



                        using (var foldout = new EditorGUILayoutx.Scopes.FoldoutHeaderGroupScope(collectionInclude, new GUIContent("Include".Localization() + $" ({item.includeGuids.Count})")))
                        using (new EditorGUILayoutx.Scopes.IndentLevelVerticalScope())
                        {
                            if (foldout.Visiable)
                            {

                                using (new GUILayout.HorizontalScope())
                                {
                                    GUI.backgroundColor = collectionInclude ? Color.yellow : Color.white;

                                    if (GUILayout.Button(collectionInclude ? "Stop Collection".Localization() : "Start Collection".Localization()))
                                    {
                                        if (collectionInclude)
                                        {
                                            StopCollection();
                                        }
                                        else
                                        {
                                            EditorAssetBundles.LoadAssetCallback += LoadAssetCallback;
                                            collectionInclude = asset;
                                            collectionExclude = null;
                                            //EditorApplication.isPlaying = true;
                                        }
                                    }
                                    GUI.backgroundColor = Color.white;

                                    if (GUILayout.Button("Clear".Localization()))
                                    {
                                        foreach (var item2 in asset.items)
                                            item2.includeGuids.Clear();
                                        EditorUtility.SetDirty(asset);
                                    }
                                }

                                var array = item.includeGuids.Select(o => new string[] { o, AssetDatabase.GUIDToAssetPath(o) }).OrderBy(o => o[1]).ToArray();
                                for (int j = 0; j < array.Length; j++)
                                {
                                    string guid = array[j][0];
                                    assetPath = array[j][1];
                                    using (new GUILayout.HorizontalScope())
                                    {
                                        if (string.IsNullOrEmpty(assetPath))
                                        {
                                            GUI.color = Color.red;
                                            GUILayout.Label(guid + " (missing)");
                                            GUI.color = Color.white;
                                        }
                                        else
                                        {
                                            string assetName = BuildAssetBundles.GetAssetName(item, assetPath);
                                            if (GUILayout.Button(new GUIContent(assetName, assetPath), "label"))
                                            {
                                                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath(assetPath, typeof(Object)));
                                            }
                                        }
                                        if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                                        {
                                            item.includeGuids.Remove(guid);
                                        }
                                    }
                                }
                            }
                        }

                        using (var foldout = new EditorGUILayoutx.Scopes.FoldoutHeaderGroupScope(collectionExclude, new GUIContent("Exclude".Localization() + $" ({item.excludeGuids.Count})")))
                        using (new EditorGUILayoutx.Scopes.IndentLevelVerticalScope())
                        {
                            if (foldout.Visiable)
                            {
                                using (new GUILayout.HorizontalScope())
                                {

                                    GUI.backgroundColor = collectionExclude ? Color.yellow : Color.white;
                                    if (GUILayout.Button(collectionExclude ? "Stop Collection".Localization() : "Start Collection".Localization()))
                                    {
                                        if (collectionExclude)
                                        {
                                            StopCollection();
                                        }
                                        else
                                        {
                                            EditorAssetBundles.LoadAssetCallback += LoadAssetCallback;
                                            collectionExclude = asset;
                                            collectionInclude = null;
                                            //EditorApplication.isPlaying = true;
                                        }
                                    }
                                    GUI.backgroundColor = Color.white;

                                    if (GUILayout.Button("Clear".Localization()))
                                    {
                                        foreach (var item2 in asset.items)
                                            item2.excludeGuids.Clear();
                                        EditorUtility.SetDirty(asset);
                                    }

                                }

                                var array = item.excludeGuids.Select(o => new string[] { o, AssetDatabase.GUIDToAssetPath(o) }).OrderBy(o => o[1]).ToArray();
                                for (int j = 0; j < array.Length; j++)
                                {
                                    string guid = array[j][0];
                                    assetPath = array[j][1];
                                    using (new GUILayout.HorizontalScope())
                                    {
                                        if (string.IsNullOrEmpty(assetPath))
                                        {
                                            GUI.color = Color.red;
                                            GUILayout.Label(guid + " (missing)");
                                            GUI.color = Color.white;
                                        }
                                        else
                                        {
                                            string assetName = BuildAssetBundles.GetAssetName(item, assetPath);
                                            if (GUILayout.Button(new GUIContent(assetName, assetPath), "label"))
                                            {
                                                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath(assetPath, typeof(Object)));
                                            }
                                        }
                                        if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                                        {
                                            item.excludeGuids.Remove(guid);
                                        }
                                    }
                                }
                            }
                        }


                        using (var foldout = new EditorGUILayoutx.Scopes.FoldoutHeaderGroupScope(false, new GUIContent($"Preview".Localization()), onShow: () =>
                        {
                            Load(item);
                        }))
                        using (new EditorGUILayoutx.Scopes.IndentLevelVerticalScope())
                        {
                            if (foldout.Visiable)
                            {
                                List<string[]> list;
                                if (cachedAssets.TryGetValue(item, out list))
                                {
                                    int max = displayMax;
                                    var array = list;
                                    for (int j = 0; j < list.Count && j < max; j++)
                                    {
                                        string guid = array[j][0];
                                        assetPath = array[j][1];
                                        using (new GUILayout.HorizontalScope())
                                        {
                                            if (string.IsNullOrEmpty(assetPath))
                                            {
                                                GUI.color = Color.red;
                                                GUILayout.Label(guid + " (missing)");
                                                GUI.color = Color.white;
                                            }
                                            else
                                            {
                                                string assetName = BuildAssetBundles.GetAssetName(item, assetPath);
                                                string bundleName, variant;
                                                bundleName = Asset.GetBundleName(assetPath, out variant);
                                                if (GUILayout.Button(new GUIContent(assetName + " [" + bundleName + (string.IsNullOrEmpty(variant) ? "" : " ." + variant) + "]", assetPath), "label"))
                                                {
                                                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath(assetPath, typeof(Object)));
                                                }
                                            }
                                        }
                                    }
                                    if (list.Count > max)
                                    {
                                        GUILayout.Label("...");
                                    }
                                }
                            }
                        }

                    }
                    return item;
                }, initExpand: true, itemStyle: "box");
                if (checker.changed)
                {
                    EditorUtility.SetDirty(asset);
                }
            }

            using (new GUILayout.HorizontalScope())
            {

                if (GUILayout.Button("Clear Missing".Localization()))
                {
                    ValidateAssets();
                }
                //if (GUILayout.Button("Reset"))
                //{
                //    if (Asset.items.Sum(o => o.guids.Count) == 0 || EditorUtility.DisplayDialog("Confirm", "Delete all ?", "Yes", "No"))
                //    {
                //        Reload();
                //    }
                //}


            }
        }
    }



}