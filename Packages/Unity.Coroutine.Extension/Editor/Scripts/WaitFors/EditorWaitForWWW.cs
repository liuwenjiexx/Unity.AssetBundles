using Coroutines;
using System.Collections;
using UnityEngine;

namespace UnityEditor.Coroutines
{

    internal class EditorWaitForWWW : CustomYield
    {
        public WWW www;

        public EditorWaitForWWW(WWW www)
        {
            this.www = www;
        }


        public override bool KeepWaiting
        {
            get { return www != null || !www.isDone; }
        }

    }
}
