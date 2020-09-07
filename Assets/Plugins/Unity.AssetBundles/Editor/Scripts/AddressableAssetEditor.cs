using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEditor.GUIExtensions;

namespace UnityEditor.Build
{

    [CustomEditor(typeof(AssetBundleAddressableAsset))]
    class AddressableAssetEditor : Editor
    {
        AssetBundleAddressableAsset Asset
        {
            get => target as AssetBundleAddressableAsset;
        }

        private AssetBundleAddressableAsset.AssetInfo[] list;

        private void OnEnable()
        {
            list = Asset.assets.OrderBy(o => o.assetName).ToArray();
        }

        class ToggleState
        {
            public bool visible;
            public bool initialized;
        }

        public override void OnInspectorGUI()
        {
            var ctrlId = EditorGUIUtility.GetControlID(FocusType.Passive);
            var state = (ToggleState)GUIUtility.GetStateObject(typeof(ToggleState), ctrlId);
            if (!state.initialized)
            {
                state.visible = true;
                state.initialized = true;
            }
            state.visible = EditorGUILayout.Foldout(state.visible, "assets (" + list.Length+")", true);
            if (state.visible)
            {
                using (new EditorGUILayoutx.Scopes.IndentLevelVerticalScope())
                {
                    foreach (var item in list)
                    {
                          ctrlId = EditorGUIUtility.GetControlID(FocusType.Passive);
                          state = (ToggleState)GUIUtility.GetStateObject(typeof(ToggleState), ctrlId);
                        state.visible = EditorGUILayout.Foldout(state.visible, item.assetName, true);
                        if (state.visible)
                        {
                            using (new EditorGUILayoutx.Scopes.IndentLevelVerticalScope())
                            {
                                EditorGUILayout.LabelField("assetName", item.assetName);
                                EditorGUILayout.LabelField("bundleName", item.bundleName);
                                EditorGUILayout.LabelField("guid", item.guid);
                            }
                        }
                    }
                }
            }
        }
    }
}