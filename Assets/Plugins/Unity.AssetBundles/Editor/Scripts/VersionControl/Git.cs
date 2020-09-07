using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Debug = UnityEngine.Debug;


namespace System.VersionControl
{
    public class Git
    {
        private static string workingDirectory;

        public static string WorkingDirectory
        {
            get
            {
                if (string.IsNullOrEmpty(workingDirectory))
                {
                    workingDirectory = GetWorkingDirectory(".");
                }
                return workingDirectory;
            }
            set => workingDirectory = value;
        }

        public static string GetWorkingDirectory(string dir)
        {
            string parentDir = Path.GetFullPath(dir);

            while (true)
            {
                if (Directory.Exists(Path.Combine(parentDir, ".git")))
                    return parentDir;

                int index1 = parentDir.LastIndexOf('/');
                int index2 = parentDir.LastIndexOf('\\');
                if (index1 == -1 || index2 > index1)
                    index1 = index2;

                if (index1 == -1)
                    break;
                parentDir = parentDir.Substring(0, index1);
                if (!Directory.Exists(parentDir))
                    break;
            }
            return null;
        }



        /// <summary>
        /// 获取完整 commit id
        /// </summary>
        /// <returns></returns>
        public static string GetCommitId()
        {
            return ExeReadLine("rev-parse HEAD");
        }

        /// <summary>
        /// 获取短 commit id
        /// </summary>
        /// <returns></returns>
        public static string GetShortCommitId()
        {
            return ExeReadLine("rev-parse --short HEAD");
        }

        public static string GetTag()
        {
            return ExeReadLine("describe --abbrev=0 --tags");
        }
        public static string TagToVersion()
        {
            string str = GetTag();
            if (string.IsNullOrEmpty(str))
                throw new Exception("not tag");
            Version version;
            if (!Version.TryParse(str, out version))
                throw new Exception("error version :" + str);
            return version.ToString();
        }
         
        public static string ParseTag(string pattern)
        {
            string str = GetTag();
            if (string.IsNullOrEmpty( str))
                throw new Exception("null tag");
            Match m = new Regex(pattern).Match(str);
            if (!m.Success)
                throw new Exception($"parse git tag error. pattern <{pattern}> not match <{str}>");
            return m.Groups["result"].Value;
        }

        public static void GetCommitInfo(out string commitId, out string author, out DateTime datetime)
        {
            GetCommitInfo(WorkingDirectory, out commitId, out author, out datetime);
        }

        public static void GetCommitInfo(string path, out string commitId, out string author, out DateTime datetime)
        {
            commitId = null;
            author = null;
            datetime = DateTime.MinValue;
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "git";
            startInfo.Arguments = "log --graph";
            startInfo.WorkingDirectory = path;
            //startInfo.UseShellExecute = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.StandardOutputEncoding = Encoding.UTF8;
            startInfo.StandardErrorEncoding = Encoding.UTF8;
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;

            using (var proc = Process.Start(startInfo))
            {
                var output = proc.StandardOutput;
                string line;
                line = output.ReadLine();
                Debug.Log(line);
                if (string.IsNullOrEmpty(line))
                {
                    return;
                }
                int index = line.IndexOf("commit ", StringComparison.InvariantCultureIgnoreCase);
                if (index >= 0)
                {
                    index = index + 7;
                    commitId = line.Substring(index).Trim();
                }
                line = output.ReadLine();
                Debug.Log(line);
                index = line.IndexOf("author: ", StringComparison.InvariantCultureIgnoreCase);
                if (index >= 0)
                {
                    index = index + 8;
                    author = line.Substring(index).Trim();
                }
                line = output.ReadLine();
                Debug.Log(line);
                index = line.IndexOf("date: ", StringComparison.InvariantCultureIgnoreCase);
                if (index >= 0)
                {
                    index = index + 8;
                    string dtFormat = "ddd MMM dd HH:mm:ss yyyy zzz";
                    //日期：Mon Mar 16 17:25:52 2020 +0800
                    //格式：ddd MMM dd HH:mm:ss yyyy zzz
                    DateTime.TryParseExact(line.Substring(index), dtFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out datetime);
                }
            }
        }

        static string ExeReadLine(string args)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "git";
            startInfo.Arguments = args;
            startInfo.WorkingDirectory = WorkingDirectory;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.StandardOutputEncoding = Encoding.UTF8;
            startInfo.StandardErrorEncoding = Encoding.UTF8;
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;

            using (var proc = Process.Start(startInfo))
            {
                var output = proc.StandardOutput;
                string line;
                line = output.ReadLine();
                return line;
            }
        }

    }
}