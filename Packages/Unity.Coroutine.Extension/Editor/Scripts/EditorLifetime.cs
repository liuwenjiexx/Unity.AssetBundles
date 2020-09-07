using System;
using UnityEngine;

namespace UnityEditor.Coroutines
{
    internal class EditorLifetime //: ILifetime
    {
        private object value;

        public static bool IsUnityEditor
        {
            get
            {
                return Application.isEditor && !Application.isPlaying;
            }
        }

        public object GetValue()
        {
            if (!IsUnityEditor)
            {
                if (value != null)
                    RemoveValue();
                return null;
            }

            return value;
        }

        public void RemoveValue()
        {
            if (value != null)
            {
                if (value is IDisposable)
                    ((IDisposable)value).Dispose();
                value = null;
            }
        }

        public void SetValue(object value)
        {
            this.value = value;
        }
    }


}
