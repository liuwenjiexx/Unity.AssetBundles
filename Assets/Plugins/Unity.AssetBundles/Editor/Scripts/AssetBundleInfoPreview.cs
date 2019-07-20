using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace UnityEditor.Build.AssetBundle
{
    [CustomEditor(typeof(Object))]
    public class AssetBundleInfoPreview : ObjectPreview
    {
        public static bool active;

        public override bool HasPreviewGUI()
        {
            var assetPath = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(assetPath))
                return false;
            if (System.IO.Directory.Exists(assetPath))
                return false;
            var importer = AssetImporter.GetAtPath(assetPath);
            if (!importer)
                return false;
            if (string.IsNullOrEmpty(importer.assetBundleName))
                return false;
            return true;
        }
        public override GUIContent GetPreviewTitle()
        {
            return new GUIContent("AssetBundle Info");
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            var assetPath = AssetDatabase.GetAssetPath(target);
            var importer = AssetImporter.GetAtPath(assetPath);
            if (!importer)
                return;
            float margin = 2;
            r.xMin += margin;
            r.xMax -= margin;
            r.yMin += margin;

            r.height = EditorGUIUtility.singleLineHeight;
            float labelWidth = 80;
            float labelSpace = 0;
            Rect rect;
            rect = GUIDrawLabel(new Rect(r.x, r.y, labelWidth, r.height), new GUIContent("Asset Path: "));
            string value;
            value = assetPath;
            GUIDrawValue(new Rect(rect.xMax + labelSpace, r.y, r.width - rect.width - labelSpace, r.height), new GUIContent(value, value));

            value = BuildAssetBundles.GetAddressableName(assetPath).ToLower();
            r.y += EditorGUIUtility.singleLineHeight;
            rect = GUIDrawLabel(new Rect(r.x, r.y, labelWidth, r.height), new GUIContent("Asset Name: "));
            GUIDrawValue(new Rect(rect.xMax + labelSpace, r.y, r.width - rect.width - labelSpace, r.height), new GUIContent(value, value));

            value = target.GetType().Name;
            r.y += EditorGUIUtility.singleLineHeight;
            rect = GUIDrawLabel(new Rect(r.x, r.y, labelWidth, r.height), new GUIContent("Asset Type: "));
            GUIDrawValue(new Rect(rect.xMax + labelSpace, r.y, r.width - rect.width - labelSpace, r.height), new GUIContent(value, value));

            value = BuildAssetBundles.IsPreloadedAssetBundleByAssetpath(assetPath).ToString();
            r.y += EditorGUIUtility.singleLineHeight;
            rect = GUIDrawLabel(new Rect(r.x, r.y, labelWidth, r.height), new GUIContent("Preloaded: "));
            GUIDrawValue(new Rect(rect.xMax + labelSpace, r.y, r.width - rect.width - labelSpace, r.height), new GUIContent(value, value));
        }

        Rect GUIDrawLabel(Rect rect, GUIContent label)
        {
            GUIStyle labelStyle = "label";
            Vector2 size = labelStyle.CalcSize(label);
            rect.width = size.x;
            GUI.Label(rect, label, labelStyle);
            return rect;
        }
        void GUIDrawValue(Rect rect, GUIContent label)
        {
            GUIStyle labelStyle = new GUIStyle("label");
            Vector2 size = labelStyle.CalcSize(label);
            if (size.x < rect.width)
                labelStyle.alignment = TextAnchor.MiddleLeft;
            else
            {
                labelStyle.alignment = TextAnchor.MiddleRight;
                var size2 = labelStyle.CalcSize(new GUIContent("..."));
                if (size2.x > 0 && size2.x < rect.width)
                {
                    while (label.text.Length > 0 && size.x + size2.x > rect.width)
                    {
                        label.text = label.text.Substring(1);
                        size = labelStyle.CalcSize(label);
                    }
                    label.text = "..." + label.text;
                }
            }
            GUI.Label(rect, label, labelStyle);
        }


        public override string GetInfoString()
        {
            return "";
        }



        [CustomPreview(typeof(GameObject))]
        class GameObjectAssetBundleInfo : AssetBundleInfoPreview
        {

        }

        [CustomPreview(typeof(AssetImporter))]
        class AssetImporterAssetBundleInfo : AssetBundleInfoPreview { }

        [CustomPreview(typeof(VideoClip))]
        class VideoClipAssetBundleInfo : AssetBundleInfoPreview { }

        [CustomPreview(typeof(AudioClip))]
        class AudioClipAssetBundleInfo : AssetBundleInfoPreview { }

        [CustomPreview(typeof(Texture2D))]
        class Texture2DAssetBundleInfo : AssetBundleInfoPreview { }

        [CustomPreview(typeof(Sprite))]
        class SpriteAssetBundleInfo : AssetBundleInfoPreview { }


    }
}