using System.Globalization;
using System.IO;

namespace System.StringFormats
{

    /// <summary>
    /// format: Name[,Offset][,SeparatorChar]
    /// Name: FileName, FileNameWithoutExtension, FileExtension, DirectoryName, DirectoryPath, FilePath, FullPath, FullDirectoryPath
    /// Offset: 正数右边开始，负数左边开始
    /// SeparatorChar: /, \\
    /// </summary>
    abstract class PathStringFormatter : INameFormatter
    {
        private ICustomFormatter baseFormatter;

        public int Priority => 0;

        public abstract string Name { get; }

        public PathStringFormatter()
        {
        }

        public PathStringFormatter(ICustomFormatter baseFormatter)
        {
            this.baseFormatter = baseFormatter;
        }


        public string Format(object arg, string formatArg)
        {
            string[] formatArgs;
            string result;
            if (arg is string)
                result = (string)arg;
            else
                result = arg.ToString();
            if (formatArg == null)
                formatArgs = new string[0];
            else
                formatArgs = formatArg.Split(',');
            result = HandleDirectory(formatArgs, result);
            result = OnFormat(result, formatArgs);

            if (formatArgs.Length > 0)
            {
                for (int i = 0; i < formatArgs.Length; i++)
                {
                    if (formatArgs[i] == "/")
                    {
                        if (result.IndexOf('\\') >= 0)
                            result = result.Replace('\\', '/');
                        continue;
                    }
                    else if (formatArgs[i] == "\\")
                    {
                        if (result.IndexOf('/') >= 0)
                            result = result.Replace('/', '\\');
                        continue;
                    }
                }
            }
            return result;
        }

        protected abstract string OnFormat(string result, string[] formatArgs);



        public object GetFormat(Type formatType)
        {
            if (formatType == typeof(ICustomFormatter))
                return this;
            return null;
        }

        string HandleDirectory(string[] parts, string path)
        {
            if (parts.Length == 0)
                return path;
            for (int i = 0; i < parts.Length; i++)
            {
                string str = parts[i];
                if (str.Length == 0)
                    continue;
                int n;
                if (int.TryParse(str, out n))
                {
                    if (n > 0)
                    {
                        path = TrimLeft(path, n);
                    }
                    else if (n < 0)
                    {
                        path = TrimRight(path, -n);
                    }
                }
            }
            return path;
        }

        string TrimLeft(string path, int count)
        {
            while (count > 0)
            {
                int index = path.IndexOf('/');
                int index2 = path.IndexOf('\\');
                if (index2 >= 0 && (index2 < index || index < 0))
                {
                    index = index2;
                }
                if (index < 0)
                    break;
                path = path.Substring(index + 1);

                count--;
            }
            return path;
        }

        string TrimRight(string path, int count)
        {
            while (count > 0)
            {
                int index = path.LastIndexOf('/');
                int index2 = path.LastIndexOf('\\');
                if (index2 >= 0 && (index2 > index || index < 0))
                {
                    index = index2;
                }
                if (index < 0)
                    break;
                path = path.Substring(0, index);

                count--;
            }
            return path;
        }
        string GetDirectoryName(string path)
        {
            int index = path.LastIndexOf('/');
            int index2 = path.LastIndexOf('\\');
            if (index2 >= 0 && (index2 > index || index < 0))
            {
                index = index2;
            }
            if (index < 0)
                return path;
            return path.Substring(0, index);
        }


        class FileNameFormatter : PathStringFormatter, INameFormatter
        {
            public override string Name => "FileName";
            protected override string OnFormat(string result, string[] formatArgs)
            {
                result = Path.GetFileName(result);
                return result;
            }
        }
        class FileNameWithoutExtensionFormatter : PathStringFormatter, INameFormatter
        {
            public override string Name => "FileNameWithoutExtension";
            protected override string OnFormat(string result, string[] formatArgs)
            {
                result = Path.GetFileNameWithoutExtension(result);
                return result;
            }
        }
        class FileExtensionFormatter : PathStringFormatter, INameFormatter
        {
            public override string Name => "FileExtension";
            protected override string OnFormat(string result, string[] formatArgs)
            {
                result = Path.GetExtension(result);
                return result;
            }
        }
        class DirectoryNameFormatter : PathStringFormatter, INameFormatter
        {
            public override string Name => "DirectoryName";
            protected override string OnFormat(string result, string[] formatArgs)
            {
                result = Path.GetFileName(GetDirectoryName(result));
                return result;
            }
        }
        class FilePathFormatter : PathStringFormatter, INameFormatter
        {
            public override string Name => "FilePath";
            protected override string OnFormat(string result, string[] formatArgs)
            {
                return result;
            }
        }
        class DirectoryPathFormatter : PathStringFormatter, INameFormatter
        {
            public override string Name => "DirectoryPath";
            protected override string OnFormat(string result, string[] formatArgs)
            {
                result = GetDirectoryName(result);
                return result;
            }
        }
        class FullPathFormatter : PathStringFormatter, INameFormatter
        {
            public override string Name => "FullPath";
            protected override string OnFormat(string result, string[] formatArgs)
            {
                if (!Path.IsPathRooted(result))
                    result = Path.GetFullPath(result);
                return result;
            }
        }
        class FullDirectoryPathFormatter : PathStringFormatter, INameFormatter
        {
            public override string Name => "FullDirectoryPath";
            protected override string OnFormat(string result, string[] formatArgs)
            {
                if (!Path.IsPathRooted(result))
                    result = GetDirectoryName(Path.GetFullPath(result));
                return result;
            }
        }

    }
}