using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;


namespace System.StringFormats
{
    public class NameStringFormatProvider : IFormatProvider, ICustomFormatter
    {
        private ICustomFormatter baseFormatter;

        private static Dictionary<string, INameFormatter> nameFormatter;

        static Dictionary<string, INameFormatter> NameFormatter
        {
            get
            {
                if (nameFormatter == null)
                {
                    nameFormatter = new Dictionary<string, INameFormatter>();
                    INameFormatter formatValue, old;

                    foreach (var type in AppDomain.CurrentDomain.GetAssemblies()
                             .Referenced(typeof(INameFormatter).Assembly)
                             .SelectMany(o => o.GetTypes())
                             .Where(o => !o.IsAbstract && !o.IsGenericType && typeof(INameFormatter).IsAssignableFrom(o)))
                    {
                        formatValue = Activator.CreateInstance(type) as INameFormatter;
                        if (string.IsNullOrEmpty(formatValue.Name))
                            continue;
                        if (nameFormatter.TryGetValue(formatValue.Name, out old))
                        {
                            if (formatValue.Priority < old.Priority)
                                continue;
                        }
                        nameFormatter[formatValue.Name] = formatValue;
                    }

                }
                return nameFormatter;
            }
        }

        public NameStringFormatProvider()
        {
        }

        public NameStringFormatProvider(ICustomFormatter baseFormatter)
        {
            this.baseFormatter = baseFormatter;
        }

        bool IsLetterChar(char ch)
        {
            if (('0' <= ch && ch <= '9') ||
                ('a' <= ch && ch <= 'z') ||
                ('A' <= ch && ch <= 'Z') ||
                ch == '_')
                return true;
            return false;
        }

        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            if (!string.IsNullOrEmpty(format) && format.Length > 1)
            {
                if ((format[0] == '$' || format[0] == '#') && IsLetterChar(format[1]))
                {
                    string key;
                    string parameter = null;
                    int index = format.IndexOf(',');
                    if (index < 0)
                    {
                        key = format.Substring(1);
                    }
                    else
                    {
                        key = format.Substring(1, index - 1);
                        parameter = format.Substring(index + 1);
                    }
                    INameFormatter formatValue;
                    if (!NameFormatter.TryGetValue(key, out formatValue))
                    {
                        throw new Exception("not key: " + key + ", format:" + format);
                    }
                    string result = formatValue.Format(arg, parameter);
                    return result;
                }
            }

            if (baseFormatter != null)
                return baseFormatter.Format(format, arg, formatProvider);

            if (arg is IFormattable)
                return ((IFormattable)arg).ToString(format, CultureInfo.CurrentCulture);
            if (arg != null)
            {
                if (arg is string)
                    return (string)arg;
                else
                    return arg.ToString();
            }

            return string.Empty;
        }

        public object GetFormat(Type formatType)
        {
            if (formatType == typeof(ICustomFormatter))
                return this;
            return null;
        }

    }


}
