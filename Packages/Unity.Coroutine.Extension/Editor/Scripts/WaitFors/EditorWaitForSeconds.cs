using Coroutines;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace UnityEditor.Coroutines
{
    internal class EditorWaitForSeconds : IEnumerable
    {
        private float seconds;

        private static FieldInfo FieldSeconds;


        static EditorWaitForSeconds()
        {
            FieldSeconds = typeof(WaitForSeconds).GetField("m_Seconds", BindingFlags.NonPublic | BindingFlags.Instance);
            if (FieldSeconds == null)
                Debug.LogErrorFormat("{0} not found field m_Seconds", typeof(WaitForSeconds).Name);
        }

        public EditorWaitForSeconds(WaitForSeconds waitForSeconds)
        {

            if (FieldSeconds != null)
            {
                seconds = (float)FieldSeconds.GetValue(waitForSeconds);
            }
            else
            {
                seconds = 0;
            }

        }

        public EditorWaitForSeconds(float seconds)
        {
            this.seconds = seconds;
        }

        public IEnumerator GetEnumerator()
        {
            var scheduer = CoroutineScheduler.Current;
            double waitForTime = scheduer.Time + seconds;
            while (scheduer.Time < waitForTime)
                yield return null;
        }
    }
}




