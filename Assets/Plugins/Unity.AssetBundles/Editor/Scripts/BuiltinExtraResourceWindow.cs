using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;
using System;
using Object = UnityEngine.Object;
using UnityEditor.Experimental;
using System.Text.RegularExpressions;
using System.IO;
using System.Text;
using UnityEditor.VersionControl;
using UnityEditor.Experimental.SceneManagement;

public class BuiltinExtraResourceWindow : EditorWindow
{
    Dictionary<string, AssetInfo[]> assets;
    Vector2 scrollPos;

    Dictionary<Object, AssetInfo> builtinExtraRes;
    Dictionary<string, AssetInfo> builtinExtraResMap;

    List<AssetInfo> builtinExtraResList;

    HashSet<AssetInfo> allSelected;


    public static string UnityBuiltinExtraDirectory = 
#if BUILTIN_ASSETS
    "Assets"
#else
"Packages/unity.builtinassets"
#endif
        ;


    public static string TextureDirectory = "Textures";
    public static string SpriteDirectory = "Sprites";
    public static string MaterialDirectory = "Materials";

    [Serializable]
    class AssetInfo
    {
        public string guid;
        public long localId;
        public string type;
        public Object asset;
        public string assetPath;
        public bool isBulitin;
        public AssetInfo replace;
        public string uniqueId
        {
            get
            {
                return guid + "/" + localId;
            }
        }
    }
    public class AssetTypes
    {
        public const string LightmapParameters = "2";
        public const string Material = "2";
        public const string Shader = "3";
        public const string Sprite = "2";
        public const string Texture2D = "2";
    }
    private void OnEnable()
    {
        titleContent = new GUIContent("Builtin Extra Resource");
        if (assets == null)
            assets = new Dictionary<string, AssetInfo[]>();
        if (builtinExtraRes == null)
            builtinExtraRes = new Dictionary<Object, AssetInfo>();
        if (builtinExtraResList == null)
            builtinExtraResList = new List<AssetInfo>();
        if (builtinExtraResMap == null)
            builtinExtraResMap = new Dictionary<string, AssetInfo>();
        if (allSelected == null)
            allSelected = new HashSet<AssetInfo>();

        if (builtinExtraResList.Count == 0)
        {
            GetBuiltinExtraResource();
        }
        else
        {
            foreach (var item in builtinExtraResList)
            {
                item.replace = FindBulitinReplaceAssetInfo(item.asset);
            }
        }
        builtinExtraRes.Clear();
        foreach (var item in builtinExtraResList)
        {
            builtinExtraRes[item.asset] = item;
        }
        builtinExtraResMap.Clear();
        foreach (var item in builtinExtraResList)
        {
            builtinExtraResMap[item.uniqueId] = item;
        }
        if (selectedBuiltinDeps == null)
            selectedBuiltinDeps = new List<AssetInfo>();

    }

    static SerializedProperty FindMaterialExternalObjectProperty(SerializedObject modelImporterObj, string name)
    {
        SerializedProperty materials = modelImporterObj.FindProperty("m_Materials");
        SerializedProperty externalObjects = modelImporterObj.FindProperty("m_ExternalObjects");
        int exteralIndex = -1;
        for (int i = 0; i < materials.arraySize; i++)
        {
            var spMat = materials.GetArrayElementAtIndex(i);
            var matName = spMat.FindPropertyRelative("name").stringValue;

            if (matName != name)
                continue;

            var matType = spMat.FindPropertyRelative("type").stringValue;
            var matAss = spMat.FindPropertyRelative("assembly").stringValue;

            for (int j = 0; j < externalObjects.arraySize; j++)
            {
                var exteralObj = externalObjects.GetArrayElementAtIndex(j);
                string exteralName = exteralObj.FindPropertyRelative("first.name").stringValue;
                string exteralType = exteralObj.FindPropertyRelative("first.type").stringValue;
                if (exteralName == matName && exteralType == matType)
                {
                    exteralIndex = j;
                    SerializedProperty exteralSecond = exteralObj.FindPropertyRelative("second");
                    return exteralSecond;
                    break;
                }
            }
            break;
        }
        return null;
    }

    static void SetMaterial(ModelImporter modelImporter, string name, Material mat)
    {
        SerializedObject modelImporterObj = new SerializedObject(modelImporter);

        var spMat = FindMaterialExternalObjectProperty(modelImporterObj, name);
        if (spMat != null)
        {
            spMat.objectReferenceValue = mat;
        }
        modelImporterObj.ApplyModifiedProperties();
    }

    static Dictionary<string, Material> GetMaterials(ModelImporter modelImporter)
    {
        SerializedObject modelImporterObj = new SerializedObject(modelImporter);
        SerializedProperty materials = modelImporterObj.FindProperty("m_Materials");

        //var pop = modelImporterObj.GetIterator();
        //while (pop.NextVisible(true))
        //    Debug.Log(pop.propertyPath);
        Dictionary<string, Material> dic = new Dictionary<string, Material>();

        for (int i = 0; i < materials.arraySize; i++)
        {
            var spMat = materials.GetArrayElementAtIndex(i);
            var matName = spMat.FindPropertyRelative("name").stringValue;
            var spMat2 = FindMaterialExternalObjectProperty(modelImporterObj, matName);
            Material mat = null;
            if (spMat2 != null)
            {
                mat = spMat2.objectReferenceValue as Material;
            }

            dic[matName] = mat;
        }
        return dic;
    }

    IEnumerable<GameObject> FindPrefabs(GameObject prefab)
    {
        if (!prefab)
            yield break;
        //PrefabAssetType prefabAssetType = PrefabUtility.GetPrefabAssetType(prefab);
        //Debug.Log("------:" + prefab.name + ", " + prefabAssetType + ", " + PrefabUtility.IsPartOfModelPrefab(prefab));

        //Debug.Log(PrefabUtility.GetPrefabInstanceStatus(prefab) + ", " + PrefabUtility.IsAnyPrefabInstanceRoot(prefab));
        //Debug.Log(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefab));
        //var root = PrefabUtility.GetOutermostPrefabInstanceRoot(prefab);
        //if (root)
        //    Debug.Log("root: " + PrefabUtility.GetPrefabAssetType(root));
        //UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetPrefabStage(prefab)
        HashSet<string> assetPaths = new HashSet<string>();

        foreach (Transform t in prefab.GetComponentsInChildren<Transform>())
        {
            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
            if (!assetPaths.Contains(assetPath))
            {
                assetPaths.Add(assetPath);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                yield return go;
            }
        }


        //if (prefabAssetType == PrefabAssetType.Regular)
        //{
        //    if (PrefabUtility.IsAnyPrefabInstanceRoot(prefab))
        //        yield break;
        //    foreach (Transform child in prefab.transform)
        //    {
        //        foreach (var model in FindPrefabModel(child.gameObject))
        //            yield return model;
        //    }
        //}
        //else if (prefabAssetType == PrefabAssetType.Model)
        //{
        //    Debug.Log("model: " + prefab);
        //    yield return prefab;
        //}
    }

    private void OnSelectionChange()
    {
        //UpdateSelectedBuiltinDependencies();
        FindAllDependencies();
        Repaint();

        //GameObject prefab = Selection.activeGameObject;
        //if (PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Regular)
        //{
        //    //var go = Object.Instantiate(prefab);
        //    //prefab = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));
        //    //PrefabUtility.RevertPrefabInstance(go, InteractionMode.AutomatedAction);
        //    //Debug.Log(AssetDatabase.GetAssetPath(go));
        //    // go =(GameObject) PrefabUtility.InstantiatePrefab(go);
        //    var go = prefab;

        //    var stage = PrefabStageUtility.GetPrefabStage(prefab);
        //    //go = stage.prefabContentsRoot;
        //    foreach (var c in FindPrefabs(go))
        //    {

        //    }
        //    //PrefabUtility.UnloadPrefabContents(prefab);
        //    //Object.DestroyImmediate(go);

        //}

        //PrefabAssetType prefabAssetType = PrefabUtility.GetPrefabAssetType(go);
        //Debug.Log(prefabAssetType);
        //if (prefabAssetType == PrefabAssetType.Model)
        //{
        //    Debug.Log(AssetDatabase.GetAssetPath(Selection.activeObject));
        //}

        //string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        //var modelImporter = AssetImporter.GetAtPath(path) as ModelImporter;

        //if (modelImporter)
        //{
        //Debug.Log(modelImporter.GetType().Name + ", " + path);

        //SerializedObject modelImporterObj = new SerializedObject(modelImporter);
        //SerializedProperty materials = modelImporterObj.FindProperty("m_Materials");
        //SerializedProperty externalObjects = modelImporterObj.FindProperty("m_ExternalObjects");

        //var pop = modelImporterObj.GetIterator();
        //while (pop.NextVisible(true))
        //    Debug.Log(pop.propertyPath);

        //bool hasMat = false;
        //int exteralIndex = -1;
        //for (int i = 0; i < materials.arraySize; i++)
        //{
        //    var spMat = materials.GetArrayElementAtIndex(i);
        //    var matName = spMat.FindPropertyRelative("name").stringValue;
        //    var matType = spMat.FindPropertyRelative("type").stringValue;
        //    var matAss = spMat.FindPropertyRelative("assembly").stringValue;
        //    Debug.Log(matName + ", " + matType + ", " + matAss);
        //    Material mat = null;
        //    //if (matName != null)
        //    //{
        //    //    mat= spMat.FindPropertyRelative("second").objectReferenceValue as Material;
        //    //}

        //    for (int j = 0; j < externalObjects.arraySize; j++)
        //    {
        //        var exteralObj = externalObjects.GetArrayElementAtIndex(j);
        //        string exteralName = exteralObj.FindPropertyRelative("first.name").stringValue;
        //        string exteralType = exteralObj.FindPropertyRelative("first.type").stringValue;
        //        if (exteralName == matName && exteralType == matType)
        //        {
        //            SerializedProperty exteralSecond = exteralObj.FindPropertyRelative("second");
        //            mat = exteralSecond != null ? exteralSecond.objectReferenceValue as Material : null;
        //            exteralIndex = j;
        //            Debug.Log("mat:" + mat);
        //            break;
        //        }
        //    }

        //    Debug.Log(" " + i + " " + mat);
        //    if (mat)
        //    {
        //        hasMat = true;
        //    }
        //}
        //Debug.Log("array:" + materials.arraySize + hasMat);

        //if (modelImporter.importMaterials && modelImporter.materialLocation == ModelImporterMaterialLocation.InPrefab)
        //{
        //    foreach (var item in GetMaterials(modelImporter))
        //    {
        //        Debug.Log(item.Key + ", " + item.Value);
        //    }
        //}

        //}
    }

    [MenuItem("Build/AssetBundle/Builtin Extra Resource")]
    static void ShowWindow()
    {
        GetWindow<BuiltinExtraResourceWindow>()
            .Show();
    }

    void GetBuiltinExtraResource()
    {
        Debug.Log("GetBuiltinExtraResource");
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Resources/unity_builtin_extra"))
        {
            string guid;
            long localId;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid, out localId))
            {
                //builtinAssetNames += guid + ", " + localId + ", " + asset.name + ", " + asset.GetType().Name + "\n";
                AssetInfo assetInfo = new AssetInfo()
                {
                    guid = guid,
                    localId = localId,
                    asset = asset,
                    assetPath = AssetDatabase.GUIDToAssetPath(guid),
                    replace = FindBulitinReplaceAssetInfo(asset),
                };

                builtinExtraResList.Add(assetInfo);
            }
        }
    }


    void FindAllDependencies()
    {
        allSelected.Clear();
        assets.Clear();
        Object[] objs = Selection.objects;
        assets = FindAllDependencies(objs);

    }


    Dictionary<string,AssetInfo[]> FindAllDependencies(Object[] objs)
    {

        List<AssetInfo> list = new List<AssetInfo>();
        string[] extensions = new string[] { ".mat", ".unity", ".prefab" };


        if (objs.Length == 1)
        {
            if (objs[0] is DefaultAsset)
            {
                string assetPath = AssetDatabase.GetAssetPath(objs[0]);
                if (!string.IsNullOrEmpty(assetPath) && Directory.Exists(assetPath))
                {
                    objs = AssetDatabase.FindAssets(null, new string[] { assetPath })
                        .Select(o => AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(o), typeof(Object)))
                        .ToArray();
                }
            }
        }
        Dictionary<string, AssetInfo[]> result = new Dictionary<string, AssetInfo[]>();


        foreach (var asset in objs)
        {
            string assetPath = AssetDatabase.GetAssetPath(asset);
            string extension = Path.GetExtension(assetPath).ToLower();
            //if (!extensions.Contains(extension))
            //    continue;
            //var asset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(Object));

            //if (!(asset is Material)  )
            //{
            //    //Debug.LogError("no material :" + asset + ", " + asset.GetType().Name);
            //    continue;
            //}

            list.Clear();
            //Debug.Log(asset.name + ", " + asset.GetType().Name);

            foreach (var dep in EditorUtility.CollectDependencies(new Object[] { asset }))
            {
                string depAssetPath = AssetDatabase.GetAssetPath(dep);
                if (!depAssetPath.StartsWith("Resources/"))
                    continue;
                string depGuid;
                long depLocalId;
                //if (!(dep is Shader))
                //    continue;
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(dep, out depGuid, out depLocalId))
                {
                    AssetInfo depAssetInfo = new AssetInfo()
                    {
                        guid = depGuid,
                        localId = depLocalId,
                        asset = dep,
                        assetPath = AssetDatabase.GetAssetPath(dep),
                        isBulitin = true,
                        //replace = FindBulitinReplaceAssetInfo(dep),
                    };

                    AssetInfo builtinAsset;
                    if (builtinExtraRes.TryGetValue(dep, out builtinAsset))
                    {
                        if (builtinAsset.replace != null)
                            depAssetInfo.replace = builtinAsset.replace;
                    }

                    list.Add(depAssetInfo);
                }
            }


            if (list.Count > 0)
            {
                result[AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset))] = list.ToArray();
            }
        }
        return result;
    }


    /// <summary>
    /// 导出内置资源
    /// </summary>
    public static void ExportBuiltinAssets()
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Resources/unity_builtin_extra"))
        {
            string path = null;
            Object newAsset = null;
            if (asset is Material)
            {
                path = MaterialDirectory + "/" + asset.name + ".mat";
                newAsset = Object.Instantiate(asset);
            }
            else if (asset is Texture2D)
            {
                path = TextureDirectory + "/" + asset.name + ".asset";
                newAsset = Object.Instantiate(asset);
            }
            else if (asset is Sprite)
            {
                path = SpriteDirectory + "/" + asset.name + ".asset";
                newAsset = Object.Instantiate(asset);
            }
            else if (asset is LightmapParameters)
            {
                path = asset.name + ".giparams";
                newAsset = Instantiate(asset);
            }

            if (string.IsNullOrEmpty(path) || !newAsset)
                continue;
            path = UnityBuiltinExtraDirectory + "/" + path;
            if (File.Exists(path))
                continue;
            if (!Directory.Exists(Path.GetDirectoryName(path)))
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(newAsset, path);
            Debug.Log("export: " + path);
        }
        AssetDatabase.Refresh();
    }

    private void OnGUI()
    {

        if (Selection.objects.Length == 0)
        {
            GUILayout.Label("no asset selected");
        }
        else
        {
            GUILayout.Label("selected: " + AssetDatabase.GetAssetPath(Selection.objects[0]));
        }

        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Export Builtin Assets"))
            {
                ExportBuiltinAssets();
            }
            if (GUILayout.Button("Refresh"))
            {
                FindAllDependencies();
            }
        }

        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Select All AssetBundle"))
            {
                HashSet<Object> list = new HashSet<Object>();
                foreach (var abName in AssetDatabase.GetAllAssetBundleNames())
                {
                    foreach (var assetPath in AssetDatabase.GetAssetPathsFromAssetBundle(abName))
                    {
                        list.Add(AssetDatabase.LoadAssetAtPath<Object>(assetPath));
                    }
                }
                Selection.objects = list.ToArray();
            }

            if (GUILayout.Button("Select All"))
            {
                HashSet<Object> list = new HashSet<Object>();
                foreach (var assetPath in AssetDatabase.GetAllAssetPaths())
                {
                    list.Add(AssetDatabase.LoadAssetAtPath<Object>(assetPath));
                }
                Selection.objects = list.ToArray();
            }

            if (GUILayout.Button("Log All"))
            {
                ListSelectedAllDependencies();
            }

            //if (GUILayout.Button("Select Models"))
            //{
            //    List<Object> list = new List<Object>();
            //    foreach (var assetPath in AssetDatabase.FindAssets("t:Model").Select(AssetDatabase.GUIDToAssetPath))
            //    {
            //        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            //        if (importer)
            //        {
            //            Object asset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(Object));
            //            if (EditorUtility.CollectDependencies(new Object[] { asset }).Where(o => IsBuiltinAsset(o)).Count() > 0)
            //            {
            //                list.Add(asset);
            //            }
            //        }

            //    }
            //    Selection.objects = list.ToArray();
            //    Debug.Log(Selection.objects.Length);

            //}
        }

        //GUILayout.Label("Selected Builtin Dependencies");

        //using (new GUILayout.HorizontalScope())
        //{
        //    GUILayout.Space(16);
        //    if (selectedBuiltinDeps.Count > 0)
        //    {
        //        using (new GUILayout.VerticalScope("box"))
        //        {

        //            foreach (var item in selectedBuiltinDeps)
        //            {
        //                using (new GUILayout.HorizontalScope())
        //                {
        //                    GUILayout.Label($"{item.asset.GetType().Name} {item.asset.name} {item.guid}({item.localId})");
        //                    if (item.replace != null)
        //                    {
        //                        GUILayout.Label($"=> {item.replace.assetPath}");
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}


        GUIStyle buttonStyle = new GUIStyle("label");



        using (var sv = new GUILayout.ScrollViewScope(scrollPos))
        {
            scrollPos = sv.scrollPosition;
            foreach (var item in assets)
            {
                using (new GUILayout.HorizontalScope())
                {
                    string assetPath;
                    assetPath = AssetDatabase.GUIDToAssetPath(item.Key);
                    if (GUILayout.Button(assetPath, buttonStyle, GUILayout.ExpandWidth(false)))
                    {
                        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath(assetPath, typeof(Object)));
                    }
                }
                foreach (var dep in item.Value)
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        string assetPath;
                        assetPath = AssetDatabase.GUIDToAssetPath(dep.guid);
                        GUILayout.Space(16);
                        bool selected = false;
                        selected = allSelected.Contains(dep);
                        bool _selected = GUILayout.Toggle(selected, GUIContent.none, GUILayout.ExpandWidth(false));
                        if (_selected != selected)
                        {
                            selected = _selected;
                            if (selected)
                            {
                                allSelected.Add(dep);
                            }
                            else
                            {
                                allSelected.Remove(dep);
                            }
                        }
                        //GUILayout.Label($"{dep.assetPath} {dep.guid} ({dep.localId})");

                        if (dep.isBulitin)
                        {
                            if (GUILayout.Button($"{dep.asset.GetType().Name} <{dep.asset.name}> {dep.guid}({dep.localId})", buttonStyle, GUILayout.ExpandWidth(false)))
                            {
                                //<{depInfo.asset.name}>
                            }

                            if (dep.replace != null)
                            {
                                if (GUILayout.Button(new GUIContent($" => <{Path.GetFileName(dep.replace.assetPath)}> {dep.replace.guid}({dep.replace.localId})", $"{assetPath} => {dep.replace.assetPath}"), buttonStyle))
                                {
                                    EditorGUIUtility.PingObject(dep.replace.asset);
                                }
                            }
                            //}
                        }
                        else
                        {
                            GUILayout.Label("missing");
                        }

                    }
                }
            }
        }

        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button("All", GUILayout.ExpandWidth(false)))
            {
                allSelected.Clear();
                foreach (var o in assets.SelectMany(o => o.Value))
                {
                    allSelected.Add(o);
                }
            }
            if (GUILayout.Button("None", GUILayout.ExpandWidth(false)))
            {
                allSelected.Clear();
            }
            if (GUILayout.Button("Replace"))
            {
                //AssetDatabase.StartAssetEditing();
                foreach (var item in assets)
                {
                    foreach (var dep in item.Value)
                    {

                        if (allSelected.Contains(dep))
                        {
                            string assetPath = AssetDatabase.GUIDToAssetPath(item.Key);
                            if (ReplaceGuid(assetPath))
                            {
                                Debug.Log($"replace {assetPath}");
                                break;
                            }
                        }

                        //if (allSelected.Contains(dep) && dep.replace != null)
                        //{

                        //    if (asset is Material)
                        //    {
                        //        if (ReplaceGuid(asset, dep, dep.replace, AssetTypes.Shader))
                        //        {
                        //            Debug.Log($"replace {assetPath}");
                        //        }

                        //        //Material mat = (Material)asset;
                        //        //Shader shader = dep.replace.asset as Shader;
                        //        //if (mat && shader)
                        //        //{
                        //        //    mat.shader = shader;
                        //        //    Debug.Log($"replace {assetPath}");
                        //        //    //mat.shader = null;
                        //        //    //mat.shader = shader;

                        //        //    //AssetDatabase.ExtractAsset(mat, assetPath);
                        //        //    //Material newMat = new Material(shader);
                        //        //    //foreach (var p in MaterialEditor.GetMaterialProperties(new Object[] { mat }))
                        //        //    //{
                        //        //    //    Debug.Log(p.name + ", " + p.type);
                        //        //    //}
                        //        //    //EditorUtility.CopySerialized(asset, newMat);
                        //        //    //AssetDatabase.CreateAsset(asset, assetPath);
                        //        //    //AssetDatabase.CreateAsset(new Material( mat), "Assets/1.mat");
                        //        //    //MaterialEditor.ApplyMaterialPropertyDrawers(mat);
                        //        //    //EditorUtility.SetDirty(mat);
                        //        //    //if (RepleaceGuid(asset, mat.shader, shader, "3"))
                        //        //    //{
                        //        //    //    Debug.Log("replace: " + assetPath);
                        //        //    //}
                        //        //}

                        //    }
                        //}
                    }
                }
                //AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                FindAllDependencies();
            }

        }

    }

    AssetInfo FindBulitinReplaceAssetInfo(Object builtinAsset)
    {
        Object replaceAsset = null;
        string assetType = null;

        if (builtinAsset is Shader)
        {
            Shader shader = Shader.Find(builtinAsset.name);

            if (shader && builtinAsset != shader)
            {
                assetType = AssetTypes.Shader;
                replaceAsset = shader;
            }
        }
        /* else if (builtinAsset is Material)
         {
             assetType = AssetTypes.Material;
             replaceAsset = AssetDatabase.FindAssets("t:Material " + builtinAsset.name, new string[] { UnityBuiltinExtraDirectory })
                 .Select(AssetDatabase.GUIDToAssetPath)
                 .Select(o => AssetDatabase.LoadAssetAtPath(o, typeof(Material)))
                 .Where(o =>
              {
                  return o && o.name == builtinAsset.name;
              }).FirstOrDefault();
         }
         else if (builtinAsset is LightmapParameters)
         {
             assetType = AssetTypes.LightmapParameters;
             replaceAsset = AssetDatabase.FindAssets(builtinAsset.name, new string[] { UnityBuiltinExtraDirectory })
                 .Select(AssetDatabase.GUIDToAssetPath)
                 .Select(o => AssetDatabase.LoadAssetAtPath(o, typeof(LightmapParameters)))
                 .Where(o =>
                 {
                     return o && o.name == builtinAsset.name;
                 }).FirstOrDefault();
         }*/
        else
        {
            replaceAsset = AssetDatabase.FindAssets($"t:{builtinAsset.GetType().Name} {builtinAsset.name}", new string[] { UnityBuiltinExtraDirectory })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(o => AssetDatabase.LoadAssetAtPath(o, typeof(Object)))
                .Where(o =>
                {
                    return o && o.name == builtinAsset.name;
                }).FirstOrDefault();
        }
        if (replaceAsset)
        {
            if (builtinAsset is Material)
            {
                assetType = AssetTypes.Material;
            }
            else if (builtinAsset is Texture2D)
            {
                assetType = AssetTypes.Texture2D;
            }
            else if (builtinAsset is Sprite)
            {
                assetType = AssetTypes.Sprite;
            }
            else if (builtinAsset is LightmapParameters)
            {
                assetType = AssetTypes.LightmapParameters;
            }
        }


        if (replaceAsset)
        {
            string replaceAssetPath = AssetDatabase.GetAssetPath(replaceAsset);

            if (!string.IsNullOrEmpty(replaceAssetPath))
            {
                string guid;
                long fileId;

                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(replaceAsset, out guid, out fileId))
                {
                    AssetInfo repalce = new AssetInfo()
                    {
                        asset = replaceAsset,
                        assetPath = replaceAssetPath,
                        guid = guid,
                        localId = fileId,
                        type = assetType,
                    };
                    //Debug.Log("FindBulitinReplaceAssetInfo:" + builtinAsset + ">" + replaceAssetPath);
                    return repalce;
                }
            }
        }
        return null;
    }

    static Regex findGuidRegex = new Regex("\\{fileID:\\s*(?<fileId>\\w+),\\s*guid:\\s*(?<guid>\\w+),\\s*type:\\s*(?<type>\\w+)\\}");
    static string guidFormat = "{{fileID: {0}, guid: {1}, type: {2}}}";
    static Encoding encoding = new UTF8Encoding(false);

    public static bool ReplaceGuid(Object asset, Object origin, Object target, string targetType)
    {
        string originGuid, targetGuid;
        long originFileId, targetFileId;
        if (!origin || !target)
            return false;
        string assetPath = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(assetPath))
            return false;
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(origin, out originGuid, out originFileId))
            return false;
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out targetGuid, out targetFileId))
            return false;
        //string originStr=string.Format(originGuid,)
        //Debug.Log(targetGuid + ", " + targetFileId);
        string text = File.ReadAllText(assetPath);
        string newText = text;
        newText = findGuidRegex.Replace(text, (m) =>
         {
             var gGuid = m.Groups["guid"];
             var gFileId = m.Groups["fileId"];
             if (gGuid.Value == originGuid && gFileId.Value == originFileId.ToString())
             {
                 return string.Format(guidFormat, targetFileId, targetGuid, targetType);
             }

             return m.Value;
         });
        if (newText != text)
        {
            //Debug.Log(newText);
            File.WriteAllText(assetPath, newText, encoding);
            return true;
        }
        return false;
    }


    static bool ReplaceGuid(Object asset, AssetInfo origin, AssetInfo target, string targetType)
    {
        string assetPath = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(assetPath))
            return false;
        if (string.IsNullOrEmpty(origin.guid))
            return false;
        if (string.IsNullOrEmpty(target.guid))
            return false;

        string text = File.ReadAllText(assetPath);
        string newText = text;
        newText = findGuidRegex.Replace(text, (m) =>
        {
            var gGuid = m.Groups["guid"];
            var gFileId = m.Groups["fileId"];
            if (gGuid.Value == origin.guid && gFileId.Value == origin.localId.ToString())
            {
                return string.Format(guidFormat, target.localId, target.guid, targetType);
            }
            return m.Value;
        });
        if (newText != text)
        {
            //Debug.Log(newText);
            File.WriteAllText(assetPath, newText, encoding);
            return true;
        }
        return false;
    }

    const string GUID_UnityBuiltinExtra = "0000000000000000f000000000000000";

    bool ReplaceModelGuid(ModelImporter modelImporter)
    {
        if (!modelImporter)
            return false;
        if (FindAllDependencies(new Object[] { AssetDatabase.LoadAssetAtPath<Object>(modelImporter.assetPath) }).Count == 0)
            return false;
        bool changed = false;

        if (!modelImporter.importMaterials || (modelImporter.importMaterials && modelImporter.materialLocation == ModelImporterMaterialLocation.InPrefab))
        {
            modelImporter.SaveAndReimport();
            //var allMats = GetMaterials(modelImporter);
            //if (allMats.Where(o => o.Value).Count() == 0)
            //{
            //    //重新生成默认材质
            //    if (modelImporter.importMaterials)
            //    {
            //        modelImporter.importMaterials = false;
            //        modelImporter.SaveAndReimport();
            //        modelImporter.importMaterials = true;
            //        modelImporter.SaveAndReimport();
            //    }
            //    else
            //    {
            //        modelImporter.importMaterials = true;
            //        modelImporter.materialLocation = ModelImporterMaterialLocation.InPrefab;
            //        modelImporter.SaveAndReimport();
            //    }
            //}

            //allMats = GetMaterials(modelImporter);
            //foreach (var item in allMats)
            //{
            //    string name = item.Key;
            //    Material mat = item.Value;
            //    if (mat)
            //    {
            //        //changed |= ReplaceShader(mat);
            //        var a = GetBuiltinAssetInfo(mat);

            //        if (a != null && a.replace != null)
            //        {
            //            mat = a.replace.asset as Material;
            //            SetMaterial(modelImporter, name, mat);
            //        }
            //    }
            //}
        }
        return changed;
    }

    bool ReplaceShader(Material material)
    {
        if (!material)
            return false;
        if (!material.shader)
            return false;
        var builtin = GetBuiltinAssetInfo(material.shader);
        if (builtin == null || builtin.replace == null)
            return false;
        material.shader = builtin.replace.asset as Shader;
        return true;
    }

    bool ReplaceFileGuid(string filePath)
    {

        string text = File.ReadAllText(filePath);
        string newText = text;
        newText = findGuidRegex.Replace(text, (m) =>
        {
            var gGuid = m.Groups["guid"];
            var gFileId = m.Groups["fileId"];

            string uuid = gGuid.Value + "/" + gFileId.Value;
            AssetInfo assetInfo;

            if (builtinExtraResMap.TryGetValue(uuid, out assetInfo))
            {
                if (assetInfo.replace != null)
                {
                    return string.Format(guidFormat, assetInfo.replace.localId, assetInfo.replace.guid, assetInfo.replace.type);
                }
                //string assetPath = AssetDatabase.GUIDToAssetPath(gGuid.Value);
                //Object asset= AssetDatabase.LoadAssetAtPath(assetPath, typeof(Object));
                //if(asset is Material)
                //{
                //    Object newAsset= AssetDatabase.FindAssets("t:Material "+asset.name).Where(o=>o=>Path.get;
                //}
            }
            return m.Value;
        });
        if (newText != text)
        {
            //Debug.Log(newText);
            File.WriteAllText(filePath, newText, encoding);
            return true;
        }
        return false;
    }

    bool ReplaceGuid(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return false;

        if (assetPath.EndsWith(".asset"))
        {
            if (assetPath.StartsWith(UnityBuiltinExtraDirectory))
            {
                Debug.Log("ignore builtin : " + assetPath);
                return false;
            }
        }
        if (assetPath.EndsWith(".prefab"))
        {
            return ReplacePrefabGuid((GameObject)AssetDatabase.LoadAssetAtPath(assetPath, typeof(GameObject)));
        }


        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
        if (importer)
        {
            if (importer is ModelImporter)
            {
                return ReplaceModelGuid(importer as ModelImporter);
            }
        }

        return ReplaceFileGuid(assetPath);
    }
    bool ReplacePrefabGuid(GameObject asset)
    {

        if (!asset)
            return false;
        bool changed = false;
        string assetPath = AssetDatabase.GetAssetPath(asset);
        //var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);

        //foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        //{
        //    var mats = r.sharedMaterials;
        //    var matChanged = false;
        //    for (int i = 0; i < mats.Length; i++)
        //    {
        //        if (!mats[i])
        //            continue;
        //        AssetInfo builtin;
        //        if (builtinExtraRes.TryGetValue(mats[i], out builtin))
        //        {
        //            if (builtin.replace != null)
        //            {
        //                mats[i] = (Material)builtin.replace.asset;
        //                Debug.Log("mat >" + builtin.replace.assetPath);
        //                matChanged = true;
        //            }
        //        }
        //    }
        //    if (matChanged)
        //    {
        //        r.materials = mats;
        //        changed |= matChanged;
        //    }
        //}
        GameObject prefab = (GameObject)AssetDatabase.LoadAssetAtPath(assetPath, typeof(GameObject));


        foreach (var p in FindPrefabs(prefab))
        {
            PrefabAssetType prefabAssetType = PrefabUtility.GetPrefabAssetType(p);
            string prefabAssetPath = AssetDatabase.GetAssetPath(p);
            if (prefabAssetType == PrefabAssetType.Model)
            {
                changed |= ReplaceModelGuid(AssetImporter.GetAtPath(prefabAssetPath) as ModelImporter);
            }
            else
            {
                //Debug.Log("----- " + prefabAssetPath);
                changed |= ReplaceFileGuid(prefabAssetPath);
            }
        }

        //PrefabUtility.SaveAsPrefabAsset(go, assetPath);
        //Object.DestroyImmediate(go);

        return changed;
    }

    bool IsBuiltinAsset(Object asset)
    {
        if (!asset)
            return false;
        return builtinExtraRes.ContainsKey(asset);
    }
    AssetInfo GetBuiltinAssetInfo(Object asset)
    {
        if (!asset)
            return null;
        AssetInfo assetInfo;
        builtinExtraRes.TryGetValue(asset, out assetInfo);
        return assetInfo;
    }

    public void ListSelectedAllDependencies()
    {
        string msg;
        msg = "All Dependencies\n";
        foreach (Object asset in EditorUtility.CollectDependencies(Selection.objects))
        {
            string assetPath = AssetDatabase.GetAssetPath(asset);
            string guid;
            long localId;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid, out localId);
            msg += $"{AssetDatabase.AssetPathToGUID(assetPath)} <{localId}> <{asset.name}> <{asset.GetType().Name}> <{assetPath}>\n";
        }
        Debug.Log(msg);
    }
    public void ListSelectedBuiltinDependencies()
    {
        string msg;
        msg = "Builtin Dependencies\n";
        foreach (Object asset in EditorUtility.CollectDependencies(Selection.objects))
        {
            if (IsBuiltinAsset(asset))
            {
                string assetPath = AssetDatabase.GetAssetPath(asset);
                string guid;
                long localId;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid, out localId);
                msg += $"{AssetDatabase.AssetPathToGUID(assetPath)} <{localId}> <{asset.name}> <{asset.GetType().Name}> <{assetPath}>\n";
            }
        }
        Debug.Log(msg);
    }

    List<AssetInfo> selectedBuiltinDeps;

    public void UpdateSelectedBuiltinDependencies()
    {
        selectedBuiltinDeps.Clear();
        Object[] objs = Selection.objects;

        if (objs.Length == 1)
        {
            if (objs[0] is DefaultAsset)
            {
                string assetPath = AssetDatabase.GetAssetPath(objs[0]);
                if (!string.IsNullOrEmpty(assetPath) && Directory.Exists(assetPath))
                {
                    objs = AssetDatabase.FindAssets(null, new string[] { assetPath })
                        .Select(o => AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(o), typeof(Object)))
                        .ToArray();
                }
            }

        }

        foreach (Object asset in EditorUtility.CollectDependencies(objs))
        {
            var assetInfo = GetBuiltinAssetInfo(asset);
            if (assetInfo != null)
            {
                selectedBuiltinDeps.Add(assetInfo);
            }
        }
        Repaint();
    }

}
