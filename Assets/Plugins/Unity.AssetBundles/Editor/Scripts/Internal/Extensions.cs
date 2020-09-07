using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace UnityEditor.Build.Internal
{

    internal static partial class Extension
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

        public static string ReplaceDirectorySeparatorChar(this string path)
        { 
            char separatorChar = Path.DirectorySeparatorChar;
            if (separatorChar == '/')
                path = path.Replace('\\', separatorChar);
            else
                path = path.Replace('/', separatorChar);

            return path;
        }


        public static bool PathStartsWithDirectory(this string path, string dir)
        {
            char separatorChar = Path.DirectorySeparatorChar;
            path = path.ReplaceDirectorySeparatorChar();
            dir = dir.ReplaceDirectorySeparatorChar();

            path = path.ToLower();
            dir = dir.ToLower();
            if (!dir.EndsWith(separatorChar.ToString()))
                dir += separatorChar;
            return path.StartsWith(dir);
        }

        public static void FileClearAttributes(this string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.SetAttributes(path, FileAttributes.Normal);
        }
        public static bool FileEqualContent(this string file1, string file2)
        {
            FileInfo fileInfo1 = new FileInfo(file1);
            FileInfo fileInfo2 = new FileInfo(file2);
            bool equal = true;
            if (fileInfo1.Exists && fileInfo2.Exists && fileInfo1.Length == fileInfo2.Length)
            {
                using (var fs1 = new FileStream(file1, FileMode.Open, FileAccess.Read))
                using (var fs2 = new FileStream(file2, FileMode.Open, FileAccess.Read))
                {
                    int buffSize = 1024 * 4;

                    buffSize = Math.Min(buffSize, (int)fileInfo1.Length);
                    buffSize = Math.Min(buffSize, (int)fileInfo2.Length);

                    byte[] buff1 = new byte[buffSize];
                    byte[] buff2 = new byte[buffSize];
                    int count1;
                    int count2;
                    while (equal)
                    {
                        count1 = fs1.Read(buff1, 0, buff1.Length);
                        if (count1 == 0)
                            break;
                        count2 = fs2.Read(buff2, 0, buff2.Length);
                        if (count1 != count2)
                        {
                            equal = false;
                            break;
                        }
                        for (int i = 0; i < count1; i++)
                        {
                            if (buff1[i] != buff2[i])
                            {
                                equal = false;
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                equal = false;
            }
            return equal;
        }
        public static bool FileCopyIfChanged(this string srcFile, string dstFile)
        {
            bool changed = false;

            if (!FileEqualContent(srcFile, dstFile))
            {
                changed = true;
            }
            if (changed)
            {
                string dstDir = Path.GetDirectoryName(dstFile);
                if (!string.IsNullOrEmpty(dstDir) && !Directory.Exists(dstDir))
                    Directory.CreateDirectory(dstDir);
                if (File.Exists(dstFile))
                    File.SetAttributes(dstFile, FileAttributes.Normal);
                File.Copy(srcFile, dstFile, true);
            }
            return changed;
        }
        public static string GetFileHashSHA256(this string filePath)
        {
            byte[] hash;
            using (SHA256 sha256 = new SHA256Managed())
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                hash = sha256.ComputeHash(fs);
            }
            return BitConverter.ToString(hash).Replace("-", "").ToLower();

        }

        public static IEnumerable<Assembly> Referenced(this IEnumerable<Assembly> assemblies, Assembly referenced)
        {
            string fullName = referenced.FullName;

            foreach (var ass in assemblies)
            {
                if (referenced == ass)
                {
                    yield return ass;
                }
                else
                {
                    foreach (var refAss in ass.GetReferencedAssemblies())
                    {
                        if (fullName == refAss.FullName)
                        {
                            yield return ass;
                            break;
                        }
                    }
                }
            }
        }

        public static void InsertSorted<T>(this IList<T> list, Comparison<T> comparison, T item, bool descending = false)
        {
            int index = -1;
            if (descending)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (comparison(item, list[i]) >= 0)
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
                    if (comparison(item, list[i]) <= 0)
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
        public static void InsertSorted<T>(this IList<T> list, IComparer<T> comparison, T item, bool descending = false)
        {
            int index = -1;
            if (descending)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (comparison.Compare(item, list[i]) >= 0)
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
                    if (comparison.Compare(item, list[i]) <= 0)
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
