using System.Collections.Generic;
using System.Linq;

namespace Editor.ProjectTwiner
{
    public sealed class SymlinkCommandBuilder : CommandLineBuilderBase
    {
        private static readonly List<string> StringBuffer = new List<string>();
        private readonly List<SymlinkData> _symlinkData = new List<SymlinkData>();

        public void AddDirectory(string originalDirPath, string symlinkDirPath)
        {
            if (SubPathCheck(originalDirPath))
            {
                return;
            }

            _symlinkData.Add(new SymlinkData {SymlinkPath = symlinkDirPath, SourcePath = originalDirPath, IsDirectory = true});
        }

        public void AddFile(string originalFilePath, string symlinkFilePath)
        {
            if (SubPathCheck(originalFilePath))
            {
                return;
            }

            _symlinkData.Add(new SymlinkData {SymlinkPath = symlinkFilePath, SourcePath = originalFilePath, IsDirectory = false});
        }

        public string[] GetSymlinkSourceDirectories()
        {
            try
            {
                foreach (SymlinkData symlinkData in _symlinkData)
                {
                    if (symlinkData.IsDirectory)
                    {
                        StringBuffer.Add(symlinkData.SourcePath);
                    }
                }

                return StringBuffer.ToArray();
            }
            finally
            {
                StringBuffer.Clear();
            }
        }

        public string[] GetSymlinkSourceFiles()
        {
            try
            {
                foreach (SymlinkData symlinkData in _symlinkData)
                {
                    if (!symlinkData.IsDirectory)
                    {
                        StringBuffer.Add(symlinkData.SourcePath);
                    }
                }

                return StringBuffer.ToArray();
            }
            finally
            {
                StringBuffer.Clear();
            }
        }

        public string[] GetSymlinkSource()
        {
            try
            {
                foreach (SymlinkData symlinkData in _symlinkData)
                {
                    StringBuffer.Add(symlinkData.SourcePath);
                }

                return StringBuffer.ToArray();
            }
            finally
            {
                StringBuffer.Clear();
            }
        }

        protected override string[] GetCommands()
        {
            // concatenates a pair of "", this is to make folders with spaces to work
            var typeLink = string.Empty; // "/H" - hard /J - junction

            var commands = new string[_symlinkData.Count];
            for (var i = 0; i < _symlinkData.Count; i++)
            {
                string directory = _symlinkData[i].IsDirectory ? "/D " : string.Empty;
                commands[i] = $"mklink {directory}{typeLink}\"{_symlinkData[i].SymlinkPath}\" \"{_symlinkData[i].SourcePath}\"";
            }

            return commands;
        }

        private bool SubPathCheck(string originalDirPath)
        {
            if (_symlinkData.Select(symlinkData => symlinkData.SourcePath).Any(path => PathUtils.IsSubPathOf(originalDirPath, path)))
            {
                return true;
            }

            for (var i = 0; i < _symlinkData.Count; i++)
            {
                string path = _symlinkData[i].SourcePath;

                if (!PathUtils.IsSubPathOf(path, originalDirPath))
                {
                    continue;
                }

                _symlinkData.RemoveAt(i);
                i--;
            }

            return false;
        }

        private struct SymlinkData
        {
            public string SymlinkPath;
            public string SourcePath;
            public bool IsDirectory;
        }
    }
}