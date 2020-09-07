using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.StringFormats
{
    public interface IKeyFormatStringValueProvider
    {
        object GetFormatValue(string key);
    }
}
