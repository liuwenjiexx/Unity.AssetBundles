using Coroutines;
using System.Collections;
using UnityEngine;

namespace UnityEditor.Coroutines
{
    class EditorWaitForCustomYieldInstruction : CustomYield
    {
        private CustomYieldInstruction yield;
        public EditorWaitForCustomYieldInstruction(CustomYieldInstruction yield)
        {
            this.yield = yield;
        }

        public override bool KeepWaiting
        {
            get { return yield.keepWaiting; }
        }

    }
}


