using Coroutines;
using System;
using System.Collections;
using UnityEngine;

namespace UnityEditor.Coroutines
{


    internal class EditorRoutineConverter : IRoutineConverter
    {

        public bool CanConvertToRoutine(Type type)
        {

            if (typeof(CustomYieldInstruction).IsAssignableFrom(type))
                return true;

            if (typeof(WWW).IsAssignableFrom(type))
                return true;
            if (typeof(WaitForSeconds).IsAssignableFrom(type))
                return true;
            if (typeof(WaitForEndOfFrame).IsAssignableFrom(type))
                return true;

            if (typeof(WaitForFixedUpdate).IsAssignableFrom(type))
                return true;

            return false;
        }

        public IEnumerator ConvertToRoutine(object obj)
        {


            if (obj is CustomYieldInstruction)
                return new EditorWaitForCustomYieldInstruction((CustomYieldInstruction)obj);

            if (obj is WWW)
                return new EditorWaitForWWW((WWW)obj);

            if (obj is WaitForSeconds)
                return new EditorWaitForSeconds((WaitForSeconds)obj).GetEnumerator();

            if (obj is WaitForEndOfFrame)
                return null;

            if (obj is WaitForFixedUpdate)
                return null;

            throw new InvalidOperationException();
        }
    }



}
