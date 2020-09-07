using System.Globalization;
using System.Text.RegularExpressions;

namespace System.StringFormats
{

    /// <summary>
    /// format: /(?<result>regex expression)/
    /// 正则表达式提取字符串中的[result]匹配组
    /// </summary>
    /// <example>
    /// string.Format(formatProvider, "{0:/(?<result>h.*d)/}", "say hello world .") => "hello world"
    /// </example>
    public class RegexStringFormatProvider : IFormatProvider, ICustomFormatter
    {
        private static RegexStringFormatProvider instance;
        private static Regex regex = new Regex("^/(?<pattern>.*)/(?<options>[igm]*)$");

        private ICustomFormatter baseFormatter;

        public RegexStringFormatProvider()
        {
        }

        public RegexStringFormatProvider(ICustomFormatter baseFormatter)
        {
            this.baseFormatter = baseFormatter;
        }

        public string Format(string format, object arg, IFormatProvider formatProvider)
        {

            if (format != null && format.Length > 1)
            {
                var regexMatch = regex.Match(format);
                if (regexMatch.Success)
                {
                    string result = arg == null ? string.Empty : arg.ToString();
                    string pattern = regexMatch.Groups["pattern"].Value;
                    string vars = regexMatch.Groups["options"].Value;
                    RegexOptions optons = RegexOptions.None;
                    if (vars.IndexOf('i') >= 0 || vars.IndexOf('I') >= 0)
                        optons |= RegexOptions.IgnoreCase;
                    if (vars.IndexOf('m') >= 0 || vars.IndexOf('M') >= 0)
                        optons |= RegexOptions.Multiline;
                    bool global = false;
                    if (vars.IndexOf('g') >= 0 || vars.IndexOf('G') >= 0)
                        global = true;
                    Regex regex = new Regex(pattern, optons);
                    string matchResult = string.Empty;
                    if (global)
                    {
                        foreach (Match m in regex.Matches(result))
                        {
                            if (m.Success)
                            {
                                var g = m.Groups["result"];
                                if (g != null && g.Success)
                                    matchResult = matchResult + g.Value;
                            }
                        }
                    }
                    else
                    {
                        var m = regex.Match(result);
                        if (m != null && m.Success)
                        {
                            var g = m.Groups["result"];
                            if (g != null && g.Success)
                                matchResult = g.Value;
                        }
                    }

                    return matchResult;
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