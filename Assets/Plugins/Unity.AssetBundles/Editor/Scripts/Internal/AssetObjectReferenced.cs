using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor
{
    [Serializable]
    public struct AssetObjectReferenced : ISerializationCallbackReceiver
    {
        [SerializeField]
        private string guid;
        private UnityEngine.Object asset;
        private bool isMissing;

        public AssetObjectReferenced(Object asset)
        {
            this.guid = null;
            this.asset = null;
            this.isMissing = false;
            this.Asset = asset;
        }



        public Object Asset
        {
            get
            {
                return asset;
            }
            set
            {
                asset = value;
                isMissing = false;
                if (asset)
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid, out long localId);
                }
                else
                {
                    guid = null;
                }
            }
        }
        public bool IsMissing
        {
            get => isMissing;
        }
        public void OnAfterDeserialize()
        {
            isMissing = false;
            asset = null;
            if (!string.IsNullOrEmpty(guid))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(assetPath))
                    asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                if (!asset)
                {
                    isMissing = true;
                    Debug.LogError("missing asset. guid: " + guid);
                }
            }
        }
        public void OnBeforeSerialize()
        {
        }


        public static implicit operator bool(AssetObjectReferenced exists)
        {
            return exists.Asset;
        }

    }


    //[Serializable]
    //public class AssetObjectReferenced<T> : AssetObjectReferenced
    //    where T : UnityEngine.Object
    //{  

    //  public new T Asset
    //    {
    //        get => (T)base.Asset;
    //        set => base.Asset = value;
    //    }


    //}

}