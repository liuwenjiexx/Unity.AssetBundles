using System;
using UnityEngine;

namespace UnityEngine.Coroutines
{
    internal class UnityRuntimeLifetime// : ILifetime
    {
        object value;

        public static bool IsUnityRuntime
        {
            get { return Application.isPlaying; }
        }

        public object GetValue()
        {
            if (!IsUnityRuntime)
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
