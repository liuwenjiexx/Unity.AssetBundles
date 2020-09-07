using System;
using System.Collections;
using System.Collections.Generic;
using System.StringFormats;
using UnityEngine;

namespace UnityEngine
{
    public class AssetBundleLoadingGUI : MonoBehaviour
    {
        private float progress;
        private GUIStyle whiteStyle;
        private GUIStyle colorButtonStyle;
        private GUIStyle labelStyle;
        public Rect progressRect = new Rect(0, 0.9f, 1f, 0.1f);
        public Color progressForegroundColor = new Color(0.3f, 0.8f, 1f, 1);
        public Color progressBackgroundColor = new Color(0.95f, 0.95f, 0.95f, 1);
        public Color progressMsgColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        public string downloadMsgFormat = "Downloading...";
        public string downloadProgressFormat = "{$DownloadProgress}/{$DownloadTotal}  {$DownloadSpeed}";
        public string versionFormat = "v{$BundleVersion}";
        public string preloadMsgFormat = "Loading...";
        public string preloadProgressFormat = "{$PreloadProgress}/{$PreloadTotal}";
        public string downloadErrorMsg = "Download Error";
        public string retryButtonText = "Retry";
        public Color retryButtonBackgroundColor = new Color(0.3f, 0.3f, 0.7f, 1);
        public Action InitializedCallback;
        Dictionary<string, object> formatValues = new Dictionary<string, object>();

        static AssetBundleLoadingGUI instance;

        public static void Show()
        {
            if (!instance)
            {
                new GameObject($"[{nameof(AssetBundleLoadingGUI)}]").AddComponent<AssetBundleLoadingGUI>();
            }
        }

        public static void Hide()
        {
            if (instance)
                DestroyImmediate(instance.gameObject);
        }

        private void Awake()
        {
            instance = this;
            formatValues["AppVersion"] = AssetBundles.AppVersion;
            formatValues["BundleVersion"] = string.Empty;
            formatValues["DownloadTotal"] = 0;
            formatValues["DownloadProgress"] = 0;
            formatValues["DownloadSpeed"] = 0;
            formatValues["PreloadTotal"] = 0;
            formatValues["PreloadProgress"] = 0;


            AssetBundles.OnDownloadStarted += (url, bundleNames) =>
            {
                progress = 0f;
            };
            AssetBundles.OnDownloadProgress += (bundleName) =>
            {

            };
            AssetBundles.OnDownloadCompleted += () =>
            {

            };
            AssetBundles.OnPreloadStarted += (n) =>
            {
                progress = 0f;
            };
            AssetBundles.OnPreloadProgress += (a, b) =>
            {

            };
            AssetBundles.OnPreloadCompleted += () =>
            {

            };
        }
        // Update is called once per frame
        void Update()
        {

            formatValues["AppVersion"] = AssetBundles.AppVersion;
            if (AssetBundles.Version != null)
                formatValues["BundleVersion"] = AssetBundles.Version.bundleVersion;


            if (AssetBundles.Status == AssetBundleStatus.Downloading)
            {
                progress = AssetBundles.DownloadProgress / (float)AssetBundles.DownloadTotal;
                if (AssetBundles.DownloadItemTotalBytes > 0)
                {
                    progress += (1f / AssetBundles.DownloadTotal) * (AssetBundles.DownloadItemReceiveBytes / (float)AssetBundles.DownloadItemTotalBytes);
                }
                formatValues["DownloadTotal"] = AssetBundles.DownloadTotal;
                formatValues["DownloadProgress"] = AssetBundles.DownloadProgress;
                string spdUnit;
                formatValues["DownloadSpeed"] = string.Format("{0:0.#}{1}/s", AssetBundles.GetBytesUnit(AssetBundles.DownloadSpeed, out spdUnit), spdUnit);
            }
            else if (AssetBundles.Status == AssetBundleStatus.Preloading)
            {
                progress = AssetBundles.PreloadedProgress / (float)AssetBundles.PreloadedTotal;
                formatValues["PreloadTotal"] = AssetBundles.PreloadedTotal;
                formatValues["PreloadProgress"] = AssetBundles.PreloadedProgress;
            }

        }


        private void OnGUI()
        {
            if (whiteStyle == null)
            {
                whiteStyle = new GUIStyle();
                Texture2D img = new Texture2D(2, 2);
                Color[] colors = new Color[img.width * img.height];
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = Color.white;
                }
                img.SetPixels(colors);
                img.Apply();
                whiteStyle.normal.background = img;
            }

            if (colorButtonStyle == null)
            {
                colorButtonStyle = new GUIStyle("button");
                colorButtonStyle.normal.background = whiteStyle.normal.background;
                colorButtonStyle.active.background = whiteStyle.normal.background;
                colorButtonStyle.hover.background = whiteStyle.normal.background;
            }
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle("label");
                labelStyle.normal.textColor = progressMsgColor;
                labelStyle.alignment = TextAnchor.MiddleCenter;
            }

            Rect rect = new Rect(progressRect);
            rect.x *= Screen.width;
            rect.y *= Screen.height;
            rect.width *= Screen.width;
            rect.height *= Screen.height;

            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, whiteStyle);
            GUI.backgroundColor = progressBackgroundColor;
            GUI.Box(rect, GUIContent.none, whiteStyle);
            GUI.backgroundColor = progressForegroundColor;
            GUI.Box(new Rect(rect.x, rect.y, rect.width * progress, rect.height), GUIContent.none, whiteStyle);
            GUI.backgroundColor = Color.white;


            string msg = "", progressText = "";
            string versionStr = "";
            if (!string.IsNullOrEmpty(versionFormat) && AssetBundles.Version != null)
                versionStr = versionFormat.FormatStringWithKey(formatValues);

            switch (AssetBundles.Status)
            {
                case AssetBundleStatus.Downloading:
                    if (!string.IsNullOrEmpty(downloadMsgFormat) && AssetBundles.DownloadTotal > 0)
                    {
                        msg = downloadMsgFormat.FormatStringWithKey(formatValues);
                        progressText = downloadProgressFormat.FormatStringWithKey(formatValues);
                    }
                    break;
                case AssetBundleStatus.Preloading:
                    if (!string.IsNullOrEmpty(preloadMsgFormat) && AssetBundles.PreloadedTotal > 0)
                    {
                        msg = preloadMsgFormat.FormatStringWithKey(formatValues);
                        progressText = preloadProgressFormat.FormatStringWithKey(formatValues);
                    }
                    break;
                case AssetBundleStatus.Error:
                    msg = downloadErrorMsg;
                    break;
            }

            Vector2 size = labelStyle.CalcSize(new GUIContent(versionStr));
            GUI.Label(new Rect(rect.x, rect.y - size.y, size.x, size.y), versionStr, labelStyle);

            using (new GUILayout.AreaScope(rect, GUIContent.none))
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(msg, labelStyle, GUILayout.ExpandWidth(false), GUILayout.Height(rect.height));
                GUILayout.FlexibleSpace();

                if (AssetBundles.Status == AssetBundleStatus.None || AssetBundles.Status == AssetBundleStatus.Error)
                {
                    GUI.backgroundColor = retryButtonBackgroundColor;
                    if (GUILayout.Button(retryButtonText, colorButtonStyle, GUILayout.ExpandWidth(false), GUILayout.Height(rect.height)))
                    {
                        AssetBundles.InitializeAsync();
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUILayout.Label(progressText, labelStyle, GUILayout.ExpandWidth(false), GUILayout.Height(rect.height));
                }
            }
        }

    }
}