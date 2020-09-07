using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;


namespace UnityEngine
{
    internal static class InternalExtensions
    {
        private static readonly DateTime UtcInitializationTime = new DateTime(1970, 1, 1, 0, 0, 0);
        public static long ToUtcMilliseconds(this DateTime dt)
        {
            dt = dt.ToUniversalTime();
            return (long)dt.Subtract(UtcInitializationTime).TotalMilliseconds;
        }
        public static DateTime FromUtcMilliseconds(this long milliseconds)
        {
            return UtcInitializationTime.AddMilliseconds(milliseconds);
        }
        public static int ToUtcSeconds(this DateTime dt)
        {
            dt = dt.ToUniversalTime();
            return (int)dt.Subtract(UtcInitializationTime).TotalSeconds;
        }
        public static DateTime FromUtcSeconds(this int milliseconds)
        {
            return UtcInitializationTime.AddSeconds(milliseconds);
        }

        public static void IncludeExcludeWithRegex(this List<string> items, string includePattern, string excludePattern)
        {
            if (!string.IsNullOrEmpty(includePattern))
            {
                Regex regex = new Regex(includePattern, RegexOptions.IgnoreCase);
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    string item = items[i];
                    if (!regex.IsMatch(item))
                    {
                        items.RemoveAt(i);
                    }
                }
            }
            if (!string.IsNullOrEmpty(excludePattern))
            {
                Regex regex = new Regex(excludePattern, RegexOptions.IgnoreCase);
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    string item = items[i];
                    if (regex.IsMatch(item))
                    {
                        items.RemoveAt(i);
                    }
                }
            }
        }

        public static void InsertSorted<T>(this IList<T> list, T item, bool descending = false)
            where T : IComparable<T>
        {
            int index = -1;
            if (descending)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (item.CompareTo(list[i]) >= 0)
                    {
                        index = i;
                        break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (item.CompareTo(list[i]) <= 0)
                    {
                        index = i;
                        break;
                    }
                }
            }

            if (index == -1)
                list.Add(item);
            else
                list.Insert(index, item);
        }

    }
}