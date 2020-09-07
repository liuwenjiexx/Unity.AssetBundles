using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace System.StringFormats
{
    class HashCodeMode : INameFormatter
    {
        public int Priority => 0;

        public string Name => "HashCodeMode";

        public string Format(object arg, string formatArg)
        {
            int mode= int.Parse(formatArg);
            return (Mathf.Abs( arg.GetHashCode()) % mode).ToString();
        }
    }
}
