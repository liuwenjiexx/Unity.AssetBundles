//2020/8/2
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine.Internal
{

    /// <summary>
    /// Editor, Project: ProjectSettings/Packages/PackageName/TypeName.json
    /// Editor, User: UserSettings/Packages/PackageName/TypeName.json
    /// Runtime, Project: Resources/ProjectSettings/Packages/PackageName/TypeName.json
    /// Runtime, User: Resources/UserSettings/Packages/PackageName/TypeName.json
    /// </summary>
    internal class SettingsProvider
    {
        private bool isRuntime;
        private bool isProject;
        private string packageName;
        private object settings;
        private Encoding encoding;

        private string baseDir;

        public Func<object> OnFirstCreateInstance;
        private Type type;
        public Action<object> OnLoadAfter;

        static FileSystemWatcher fsw;
        static DateTime lastWriteTime;

        public event PropertyChangedDelegate PropertyChanged;

        public delegate void PropertyChangedDelegate(string propertyName);
        private int _playingChanged;

        public SettingsProvider(Type type, string packageName, bool isRuntime, bool isProject, string baseDir = null)
        {
            this.packageName = packageName;
            this.isRuntime = isRuntime;
            this.isProject = isProject;
            this.baseDir = baseDir;
            this.type = type;
            this.FileName = type.Name + ".json";

        }

        public object Settings
        {
            get
            {
#if UNITY_EDITOR
                if (_playingChanged != EditorSettingsProvider.playingChanged)
                {
                    _playingChanged = EditorSettingsProvider.playingChanged;
                    settings = null;
                }
#endif 

                if (settings == null)
                {
                    Load();
                }
                return settings;
            }
        }

        public string PackageName
        {
            get => packageName;
        }

        public bool IsRuntime
        {
            get => isRuntime;
        }

        public bool IsProject
        {
            get => isProject;
        }

        public Encoding Encoding
        {
            get => encoding ?? new UTF8Encoding(false);
            set => encoding = value;
        }

        public string FileName
        {
            get; set;
        }

        public string FilePath
        {
            get; set;
        }



        public string GetFilePath()
        {
            if (!string.IsNullOrEmpty(FilePath))
                return FilePath;
            string filePath;
            filePath = string.Empty;
            if (!string.IsNullOrEmpty(baseDir))
            {
                filePath = baseDir;
            }
            else
            {
                if (isRuntime)
                {
                    filePath = "Assets/Resources";
                }
            }


            if (isProject)
            {
                filePath = Path.Combine(filePath, "ProjectSettings");
            }
            else
            {
                filePath = Path.Combine(filePath, "UserSettings");
            }
            filePath = Path.Combine(filePath, "Packages", packageName);

            if (!string.IsNullOrEmpty(FileName))
                filePath = Path.Combine(filePath, FileName);
            else
                filePath = Path.Combine(filePath, "Settings.json");

            return filePath;
        }

        string GetResourcesPath()
        {

            string filePath;
            filePath = string.Empty;
            if (!string.IsNullOrEmpty(baseDir))
            {
                filePath = baseDir;
            }

            if (isProject)
            {
                filePath = Path.Combine(filePath, "ProjectSettings");
            }
            else
            {
                filePath = Path.Combine(filePath, "UserSettings");
            }
            filePath = Path.Combine(filePath, "Packages", packageName);

            if (!string.IsNullOrEmpty(FileName))
                filePath = Path.Combine(filePath, Path.GetFileNameWithoutExtension(FileName));


            return filePath;
        }


        public bool SetProperty<TValue>(string propertyName, ref TValue value, TValue newValue)
        {
            Type type = typeof(TValue);
            bool changed = false;
            if (type.IsArray || typeof(IList).IsAssignableFrom(type))
            {
                changed = !EqualElements(value as IList, newValue as IList);
            }
            else
            if (!object.Equals(value, newValue))
            {
                changed = true;
            }
            if (changed)
            {
                value = newValue;
                if (!Application.isPlaying)
                    Save();
                PropertyChanged?.Invoke(propertyName);
            }
            return changed;
        }
        public bool Set<TValue>(string propertyName, ref TValue value, TValue newValue)
        {
            return SetProperty(propertyName, ref value, newValue);
        }
        bool EqualElements(IList a, IList b)
        {
            if (object.Equals(a, b) || object.ReferenceEquals(a, b))
                return true;
            if (a == null)
            {
                if (b != null)
                    return false;
                if (b == null)
                    return true;
            }
            else
            {
                if (b == null)
                    return false;
            }
            if (a.Count != b.Count)
                return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!object.Equals(a[i], b[i]))
                    return false;
            }
            return true;
        }

        public void Load()
        {

            string filePath = null;
            try
            {
#if UNITY_EDITOR
                _playingChanged = EditorSettingsProvider.playingChanged;
#endif
                string json = null;
                //if(Application.isPlaying)
                //{
                //    if (!IsRuntime)
                //        throw new Exception($"can't  playing load runtime settings [{type.FullName}]");
                //}
                if (IsRuntime)
                {
                    filePath = GetResourcesPath();
                    TextAsset textAsset = Resources.Load<TextAsset>(filePath);
                    if (textAsset)
                    {
                        json = Encoding.GetString(textAsset.bytes);
                    }
                    if (!string.IsNullOrEmpty(json))
                    {
                        this.settings = JsonUtility.FromJson(json, type);
                    }
                }
                else
                {
                    filePath = GetFilePath();
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            json = File.ReadAllText(filePath, Encoding);
                            lastWriteTime = File.GetLastWriteTimeUtc(filePath);
                            EnableFileSystemWatcher();
                        }
                        if (!string.IsNullOrEmpty(json))
                        {
                            this.settings = JsonUtility.FromJson(json, type);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"load file <{filePath}>", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            if (this.settings == null)
            {
                Debug.Log("settings null, " + filePath);
                if (OnFirstCreateInstance != null)
                {
                    try
                    {
                        this.settings = OnFirstCreateInstance();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                    if (this.settings == null)
                        Debug.LogError("OnFirstCreateInstance return null");
                }

                if (this.settings == null)
                {
                    this.settings = Activator.CreateInstance(type);

                }
            }


            OnLoadAfter?.Invoke(this.settings);
        }

        public void Save()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning($"is playing, runtime settings can't save [{type.FullName}]");
                return;
            }

#if UNITY_EDITOR
            if (_playingChanged != EditorSettingsProvider.playingChanged)
            {
                _playingChanged = EditorSettingsProvider.playingChanged;
                settings = null;
                return;
            }
#endif

            string filePath = GetFilePath();
            string json = JsonUtility.ToJson(Settings, true);
            if (!Directory.Exists(Path.GetDirectoryName(filePath)))
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, json, Encoding);
            lastWriteTime = File.GetLastWriteTimeUtc(filePath);
            EnableFileSystemWatcher();
        }






        public void EnableFileSystemWatcher()
        {

            if (!Application.isEditor)
                return;
            if (fsw != null)
                return;
            try
            {
                string filePath = GetFilePath();
                string dir = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(dir))
                    return;
                string fileName = Path.GetFileName(filePath);
                fsw = new FileSystemWatcher();
                fsw.BeginInit();
                fsw.Path = dir;
                fsw.Filter = fileName;
                fsw.NotifyFilter = NotifyFilters.LastWrite;
                fsw.Changed += OnFileSystemWatcher;
                fsw.Deleted += OnFileSystemWatcher;
                fsw.Renamed += OnFileSystemWatcher;
                fsw.Created += OnFileSystemWatcher;
                fsw.IncludeSubdirectories = true;
                fsw.EnableRaisingEvents = true;
                fsw.EndInit();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }


        public void OnFileSystemWatcher(object sender, FileSystemEventArgs e)
        {
#if UNITY_EDITOR
            EditorApplication.delayCall += () =>
            {
                if (!Application.isPlaying)
                {
                    bool changed = true;
                    string filePath = GetFilePath();
                    if (File.Exists(filePath) && lastWriteTime == File.GetLastWriteTimeUtc(filePath))
                    {
                        changed = false;
                    }

                    if (changed)
                    {
                        settings = null;
                    }
                }
            };
#endif
        }

        public override string ToString()
        {
            return $"package: {packageName}, runtime: {IsRuntime}, project: {IsProject}";
        }

    }

#if UNITY_EDITOR
    internal class EditorSettingsProvider
    {

        public static int playingChanged;

        [InitializeOnLoadMethod]
        static void InitializeOnLoadMethod()
        {
            EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
        }

        private static void EditorApplication_playModeStateChanged(PlayModeStateChange state)
        {
            //丢弃运行时设置
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.ExitingEditMode)
            {
                playingChanged = (playingChanged + 1) % 1000;
            }
        }
    }
#endif


}
