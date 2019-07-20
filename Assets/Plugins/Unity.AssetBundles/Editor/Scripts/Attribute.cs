using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace UnityEditor
{

    class PreProcessBuildAttribute : CallbackOrderAttribute
    {
        public PreProcessBuildAttribute()
        {
        }

        public PreProcessBuildAttribute(int callbackOrder)
        {
            base.m_CallbackOrder = callbackOrder;
        }

        public int CallbackOrder
        {
            get { return m_CallbackOrder; }
        }

    }
}