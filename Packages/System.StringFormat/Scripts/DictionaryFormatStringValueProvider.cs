using System.Collections.Generic;

namespace System.StringFormats
{
    public class DictionaryFormatStringValueProvider : IKeyFormatStringValueProvider
    {

        private Dictionary<string, object> values;
        private IKeyFormatStringValueProvider baseProvider;

        public DictionaryFormatStringValueProvider(Dictionary<string, object> values)
        {
            this.values = values;
        }

        public DictionaryFormatStringValueProvider(Dictionary<string, object> values,IKeyFormatStringValueProvider baseProvider)
        {
            this.values = values;
            this.baseProvider = baseProvider;
        }

        public object GetFormatValue(string key)
        {
            if(values.ContainsKey(key))
            return values[key];
            if (baseProvider != null)
                baseProvider.GetFormatValue(key);
            throw new Exception("not key:" + key);
        }
    }
}
