using System;
using System.IO;

using JetBrains.Annotations;

namespace Editor.ProjectTwiner
{
    public static class PathUtils
    {
        public static bool IsDirectoryPath(string path)
        {
            return path.EndsWith("/") || path.EndsWith("\\");
        }

        public static string FixPath(string path)
        {
            return path.Replace("\\", "/");
        }

        public static string FixDirPath(string path)
        {
            return FixPath(path).WithEnding("/");
        }

        public static bool IsSymbolic(string path)
        {
            if (File.Exists(path))
            {
                return IsSymbolic(new FileInfo(path));
            }

            if (Directory.Exists(path))
            {
                return IsSymbolic(new DirectoryInfo(path));
            }

            return false;
        }

        public static bool IsSymbolic(FileSystemInfo info)
        {
            return info.Attributes.HasFlag(FileAttributes.ReparsePoint);
        }

        /// <summary>
        ///     Returns true if <paramref name="path" /> starts with the path <paramref name="baseDirPath" />.
        ///     The comparison is case-insensitive, handles / and \ slashes as folder separators and
        ///     only matches if the base dir folder name is matched exactly ("c:\foobar\file.txt" is not a sub path of "c:\foo").
        /// </summary>
        public static bool IsSubPathOf(string path, string baseDirPath)
        {
            string normalizedPath = Path.GetFullPath(path.Replace('/', '\\').WithEnding("\\"));
            string normalizedBaseDirPath = Path.GetFullPath(baseDirPath.Replace('/', '\\').WithEnding("\\"));

            return normalizedPath.StartsWith(normalizedBaseDirPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        ///     Returns <paramref name="str" /> with the minimal concatenation of <paramref name="ending" /> (starting from end)
        ///     that
        ///     results in satisfying .EndsWith(ending).
        /// </summary>
        /// <example>"hel".WithEnding("llo") returns "hello", which is the result of "hel" + "lo".</example>
        public static string WithEnding([CanBeNull] this string str, string ending)
        {
            if (str == null)
            {
                return ending;
            }

            string result = str;

            // Right() is 1-indexed, so include these cases
            // * Append no characters
            // * Append up to N characters, where N is ending length
            for (var i = 0; i <= ending.Length; i++)
            {
                string tmp = result + ending.Right(i);
                if (tmp.EndsWith(ending))
                {
                    return tmp;
                }
            }

            return result;
        }

        /// <summary>Gets the rightmost <paramref name="length" /> characters from a string.</summary>
        /// <param name="value">The string to retrieve the substring from.</param>
        /// <param name="length">The number of characters to retrieve.</param>
        /// <returns>The substring.</returns>
        public static string Right([NotNull] this string value, int length)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, "Length is less than zero");
            }

            return length < value.Length ? value.Substring(value.Length - length) : value;
        }

        public static string MakeRelativePath(string basePath, string targetPath)
        {
            var result = string.Empty;
            int offset;

            // this is the easy case.  The file is inside of the working directory.
            if (targetPath.StartsWith(basePath))
            {
                return targetPath.Substring(basePath.Length + 1);
            }

            // the hard case has to back out of the working directory
            string[] baseDirs = basePath.Split(':', '\\', '/');
            string[] fileDirs = targetPath.Split(':', '\\', '/');

            // if we failed to split (empty strings?) or the drive letter does not match
            if (baseDirs.Length <= 0 || fileDirs.Length <= 0 || baseDirs[0] != fileDirs[0])
            {
                // can't create a relative path between separate harddrives/partitions.
                return targetPath;
            }

            // skip all leading directories that match
            for (offset = 1; offset < baseDirs.Length; offset++)
            {
                if (baseDirs[offset] != fileDirs[offset])
                {
                    break;
                }
            }

            // back out of the working directory
            for (var i = 0; i < baseDirs.Length - offset; i++)
            {
                result += "..\\";
            }

            // step into the file path
            for (int i = offset; i < fileDirs.Length - 1; i++)
            {
                result += fileDirs[i] + "\\";
            }

            // append the file
            result += fileDirs[fileDirs.Length - 1];

            return result;
        }
    }
}