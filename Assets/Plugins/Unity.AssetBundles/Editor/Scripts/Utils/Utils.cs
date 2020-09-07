using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;


namespace UnityEditor.Build
{
    internal static class Utils
    {


        #region IsMatchPath

        private static Regex DirectorySeparatorCharRegex = new Regex("[/\\\\]");
        private static Dictionary<string, Regex> cachedPathRegexs;


        /// <summary>
        /// [/] 绝对路径, [!] 排除
        /// </summary>
        /// <param name="path"></param>
        /// <param name="patterns"></param>
        /// <returns></returns>
        public static bool IsMatchPath(string path, string[] patterns)
        {
            string pattern;
            bool include = false, exclude = false;
            for (int i = 0; i < patterns.Length; i++)
            {
                pattern = patterns[i];

                if (pattern.StartsWith("!"))
                {
                    if (!exclude)
                    {
                        exclude = IsMatchPath(path, pattern,false);
                        if (exclude)
                            break;
                    }
                }
                else
                {
                    if (!include)
                    {
                        include = IsMatchPath(path, pattern, false);
                    }
                }
            }

            return include && !exclude;
        }
        public static bool IsMatchPath(string path, string pattern)
        {
            return IsMatchPath(path, pattern.Split('|'));
        }
        public static bool IsMatchFileName(string path, string pattern)
        {
            return IsMatchPath(path, pattern, true);
        }
        static bool IsMatchPath(string path, string pattern, bool isFileName)
        {
            if (string.IsNullOrEmpty(pattern))
                return false;
            string newPattern = pattern;
            if (string.IsNullOrEmpty(newPattern))
                return false;
            Regex regex;

            if (cachedPathRegexs == null)
                cachedPathRegexs = new Dictionary<string, Regex>();
            if (!cachedPathRegexs.TryGetValue(pattern, out regex))
            {
                string[] parts;
                if (pattern.StartsWith("!"))
                {
                    parts = pattern.Substring(1).Split('|');
                }
                else
                {
                    parts = pattern.Split('|');
                }

                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i];

                    if (!isFileName && !(part.StartsWith("/") || part.StartsWith("\\")))
                    {
                        part = "*" + part;
                    }
                    part = DirectorySeparatorCharRegex.Replace(part, "[/\\\\]");
                    part = part.Replace(".", "\\.").Replace("*", ".*");
                    part = "(^" + part + "$)";
                    parts[i] = part;
                }

                regex = new Regex(string.Join("|", parts), RegexOptions.IgnoreCase | RegexOptions.Multiline);
            }
            bool result;
            result = regex.IsMatch(path);
            return result;
        }

        #endregion
    }

}