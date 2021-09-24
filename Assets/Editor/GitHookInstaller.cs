using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using UnityEditor;

using UnityEngine;

using Debug = UnityEngine.Debug;

namespace Fp.EditorUtility
{
#if GIT_HOOK_AUTOINSTALL
    [InitializeOnLoad]
#endif
    public static class GitHookInstaller
    {
        private const string HookInstallerKey = "GitHookInstaller_VersionKey";

        private const string CurrentVersion = "v0.13";

        private const string CheckMetaHook =
            @"QVNTRVRTX0RJUj0iJChnaXQgY29uZmlnIC0tZ2V0IHVuaXR5M2QuYXNzZXRzLWRpciB8fCBlY2hvICJBc3NldHMiKSIKCmlmIGdpdCByZXYtcGFyc2UgLS12ZXJpZnkgSEVBRCA+L2Rldi9udWxsIDI+JjEKdGhlbgoJYWdhaW5zdD1IRUFECmVsc2UKCSMgSW5pdGlhbCBjb21taXQ6IGRpZmYgYWdhaW5zdCBhbiBlbXB0eSB0cmVlIG9iamVjdAoJYWdhaW5zdD00YjgyNWRjNjQyY2I2ZWI5YTA2MGU1NGJmOGQ2OTI4OGZiZWU0OTA0CmZpCgojIFJlZGlyZWN0IG91dHB1dCB0byBzdGRlcnIuCmV4ZWMgMT4mMgoKZ2l0IGRpZmYgLS1jYWNoZWQgLS1uYW1lLW9ubHkgLS1kaWZmLWZpbHRlcj1BIC16ICRhZ2FpbnN0IC0tICIkQVNTRVRTX0RJUiIgfCB3aGlsZSByZWFkIC1kICQnXDAnIGY7IGRvCglleHQ9IiR7ZiMjKi59IgoJYmFzZT0iJHtmJS4qfSIKCWZpbGVuYW1lPSIkKGJhc2VuYW1lICIkZiIpIgoKCWlmIFsgIiRleHQiID0gIm1ldGEiIF07IHRoZW4KCQlpZiBbICQoZ2l0IGxzLWZpbGVzIC0tY2FjaGVkIC0tICIkYmFzZSIgfCB3YyAtbCkgPSAwIF07IHRoZW4KCQkJY2F0IDw8RU9GCkVycm9yOiBSZWR1ZGFudCBtZXRhIGZpbGUuCk1ldGEgZmlsZSBcYCRmJyBpcyBhZGRlZCwgYnV0IFxgJGJhc2UnIGlzIG5vdCBpbiB0aGUgZ2l0IGluZGV4LgpQbGVhc2UgYWRkIFxgJGJhc2UnIHRvIGdpdCBhcyB3ZWxsLgpFT0YKCQkJZXhpdCAxCgkJZmkKCWVsaWYgWyAiJHtmaWxlbmFtZSMjLip9IiAhPSAnJyBdOyB0aGVuCgkJcD0iJGYiCgkJd2hpbGUgWyAiJHAiICE9ICIkQVNTRVRTX0RJUiIgXTsgZG8KCQkJaWYgWyAkKGdpdCBscy1maWxlcyAtLWNhY2hlZCAtLSAiJHAubWV0YSIgfCB3YyAtbCkgPSAwIF07IHRoZW4KCQkJCWNhdCA8PEVPRgpFcnJvcjogTWlzc2luZyBtZXRhIGZpbGUuCkFzc2V0IFxgJGYnIGlzIGFkZGVkLCBidXQgXGAkcC5tZXRhJyBpcyBub3QgaW4gdGhlIGdpdCBpbmRleC4KUGxlYXNlIGFkZCBcYCRwLm1ldGEnIHRvIGdpdCBhcyB3ZWxsLgpFT0YKCQkJCWV4aXQgMQoJCQlmaQoJCQlwPSIke3AlLyp9IgoJCWRvbmUKCWZpCmRvbmUKCnJldD0iJD8iCmlmIFsgIiRyZXQiICE9IDAgXTsgdGhlbgoJZXhpdCAiJHJldCIKZmkKCmdpdCBkaWZmIC0tY2FjaGVkIC0tbmFtZS1vbmx5IC0tZGlmZi1maWx0ZXI9RCAteiAkYWdhaW5zdCAtLSAiJEFTU0VUU19ESVIiIHwgd2hpbGUgcmVhZCAtZCAkJ1wwJyBmOyBkbwoJZXh0PSIke2YjIyoufSIKCWJhc2U9IiR7ZiUuKn0iCgoJaWYgWyAiJGV4dCIgPSAibWV0YSIgXTsgdGhlbgoJCWlmIFsgJChnaXQgbHMtZmlsZXMgLS1jYWNoZWQgLS0gIiRiYXNlIiB8IHdjIC1sKSAhPSAwIF07IHRoZW4KCQkJY2F0IDw8RU9GCkVycm9yOiBSZWR1ZGFudCBtZXRhIGZpbGUuCk1ldGEgZmlsZSBcYCRmJyBpcyByZW1vdmVkLCBidXQgXGAkYmFzZScgaXMgc3RpbGwgaW4gdGhlIGdpdCBpbmRleC4KUGxlYXNlIHJlbW92ZSBcYCRiYXNlJyBmcm9tIGdpdCBhcyB3ZWxsLgpFT0YKCQkJZXhpdCAxCgkJZmkKCWVsc2UKCQlwPSIkZiIKCQl3aGlsZSBbICIkcCIgIT0gIiRBU1NFVFNfRElSIiBdOyBkbwoJCQlpZiBbICQoZ2l0IGxzLWZpbGVzIC0tY2FjaGVkIC0tICIkcCIgfCB3YyAtbCkgPSAwIF0gJiYgWyAkKGdpdCBscy1maWxlcyAtLWNhY2hlZCAtLSAiJHAubWV0YSIgfCB3YyAtbCkgIT0gMCBdOyB0aGVuCgkJCQljYXQgPDxFT0YKRXJyb3I6IE1pc3NpbmcgbWV0YSBmaWxlLgpBc3NldCBcYCRmJyBpcyByZW1vdmVkLCBidXQgXGAkcC5tZXRhJyBpcyBzdGlsbCBpbiB0aGUgZ2l0IGluZGV4LgpQbGVhc2UgcmVtb3ZlIFxgJHAubWV0YScgZnJvbSBnaXQgYXMgd2VsbC4KRU9GCgkJCQlleGl0IDEKCQkJZmkKCQkJcD0iJHtwJS8qfSIKCQlkb25lCglmaQpkb25lCgpyZXQ9IiQ/IgppZiBbICIkcmV0IiAhPSAwIF07IHRoZW4KCWV4aXQgIiRyZXQiCmZp";

        private const string RemoveEmptyDirectoryHook =
            @"QVNTRVRTX0RJUj0iJChnaXQgY29uZmlnIC0tZ2V0IHVuaXR5M2QuYXNzZXRzLWRpciB8fCBlY2hvICJBc3NldHMiKSIKIyBSZW1vdmUgZW1wdHkgYXNzZXRzIGRpcmVjdG9yeQpmaW5kICIkQVNTRVRTX0RJUiIgLWRlcHRoIC10eXBlIGQgLWVtcHR5IC1kZWxldGU=";

#if GIT_HOOK_AUTOINSTALL
        static GitHookInstaller()
        {
            if (UnityEditorInternal.InternalEditorUtility.inBatchMode || EditorPrefs.HasKey(HookInstallerKey) && EditorPrefs.GetString(HookInstallerKey, "none") == CurrentVersion)
            {
                return;
            }

            InstallGitHooks();
        }
#endif

        [MenuItem("Tools/Git/HookInstall")]
        private static void InstallGitHooks()
        {
            string projectPath = Path.GetDirectoryName(Application.dataPath.Replace("Assets", ""));

            const string gitSubFolder = ".git";

            if (projectPath != null && FindRecursion(projectPath, gitSubFolder, out string gitRootPath))
            {
                string gitPath = Path.Combine(gitRootPath, gitSubFolder);
                if (Directory.Exists(gitPath))
                {
                    Debug.Log($"Installing hooks v.{CurrentVersion} in founded \".git\" path \"{gitPath}\"");

                    string gitPathHooks = Path.Combine(gitPath, "hooks");

                    if (!Directory.Exists(gitPathHooks))
                    {
                        Directory.CreateDirectory(gitPathHooks);
                    }

                    InjectHookFile(gitPathHooks, "post-checkout", RemoveEmptyDirectoryHook);
                    InjectHookFile(gitPathHooks, "post-merge", RemoveEmptyDirectoryHook);
                    InjectHookFile(gitPathHooks, "pre-commit", CheckMetaHook);

                    EditorPrefs.SetString(HookInstallerKey, CurrentVersion);
                    Debug.Log($"Git hooks {CurrentVersion} are been installed.");

                    Debug.Log("Try to change git-hook target directory. ");
                    string relativePath = GetRelativePath(Path.GetFullPath(Application.dataPath), Path.GetFullPath(gitRootPath), '/').TrimEnd('/', '\\');
                    string result = CommandOutput("git", $"config unity3d.assets-dir \"{relativePath}\"");
                    if (!string.IsNullOrWhiteSpace(result))
                    {
                        Debug.LogError($"Please use manual command for git: \ngit config unity3d.assets-dir \"{relativePath}\"\n{result}");
                    }
                    else
                    {
                        Debug.Log($"git config unity3d.assets-dir \"{relativePath}\"");
                    }
                }
                else
                {
                    Debug.LogWarning("Git hooks can't be installed, git directory not found.");
                }
            }
            else
            {
                Debug.LogWarning("Git hooks can't be installed, project path is invalid.");
            }
        }

        private static string GetRelativePath(string primary, string secondary, char separationChar = '\\')
        {
            if (!primary.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                primary += Path.DirectorySeparatorChar;
            }

            var pathUri = new Uri(primary);
            // Folders must end in a slash
            if (!secondary.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                secondary += Path.DirectorySeparatorChar;
            }

            var folderUri = new Uri(secondary);
            return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace(Path.DirectorySeparatorChar, separationChar));
        }

        private static string CommandOutput(string command, string arguments, string workingDirectory = null)
        {
            try
            {
                var procStartInfo = new ProcessStartInfo(command, arguments);

                procStartInfo.RedirectStandardError =
                    procStartInfo.RedirectStandardInput = procStartInfo.RedirectStandardOutput = true;
                procStartInfo.UseShellExecute = false;
                procStartInfo.CreateNoWindow = true;
                if (null != workingDirectory)
                {
                    procStartInfo.WorkingDirectory = workingDirectory;
                }

                var outputBuilder = new StringBuilder();

                using (var proc = new Process {StartInfo = procStartInfo})
                {
                    proc.Start();

                    proc.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { outputBuilder.AppendLine(e.Data); };
                    proc.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { outputBuilder.AppendLine(e.Data); };

                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    proc.WaitForExit();
                }

                return outputBuilder.ToString();
            }
            catch (Exception objException)
            {
                return $"Error in command: {command}, {objException.Message}";
            }
        }

        private static bool FindRecursion(string path, string subFolder, out string result)
        {
            var pathInfo = new DirectoryInfo(path);

            do
            {
                if (pathInfo != null && pathInfo.Exists)
                {
                    DirectoryInfo gitDir = pathInfo.GetDirectories()
                                                   .FirstOrDefault(d => d.Name.Equals(subFolder, StringComparison.OrdinalIgnoreCase));
                    if (gitDir == null)
                    {
                        pathInfo = pathInfo.Parent;
                        continue;
                    }

                    result = pathInfo.FullName;
                    return true;
                }

                result = string.Empty;
                return false;
            }
            while (true);
        }

        private static void InjectHookFile(string gitHooksPath, string hookName, string hookSourcesBase64)
        {
            string filePath = Path.Combine(gitHooksPath, hookName);

            var content = string.Empty;
            var insertionTagStart = $"#UnityAutoInstallHook_[{hookName}]_S";
            var insertionTagEnd = $"#UnityAutoInstallHook_[{hookName}]_E";

            if (File.Exists(filePath))
            {
                content = File.ReadAllText(filePath);
            }

            var sb = new StringBuilder(content.Length);

            //if new empty file
            if (string.IsNullOrWhiteSpace(content))
            {
                sb.AppendLine("#!/bin/sh");
            }

            int oldInsertionStart = content.IndexOf(insertionTagStart, StringComparison.OrdinalIgnoreCase);
            int oldInsertionEnd = content.IndexOf(insertionTagEnd, StringComparison.OrdinalIgnoreCase);

            byte[] result = Convert.FromBase64String(hookSourcesBase64);
            string insertionContent = Encoding.UTF8.GetString(result);

            if (oldInsertionStart >= 0 && oldInsertionEnd >= 0 && oldInsertionEnd > oldInsertionStart)
            { //Replace
                string beforeInject = content.Substring(0, oldInsertionStart);
                string afterInject = content.Substring(oldInsertionEnd, content.Length - oldInsertionEnd);

                sb.Append(beforeInject);
                sb.AppendLine(insertionTagStart);
                sb.AppendLine(insertionContent);
                sb.Append(afterInject);
            }
            else
            { //Append
                sb.AppendLine(content);
                sb.AppendLine(insertionTagStart);
                sb.AppendLine(insertionContent);
                sb.AppendLine(insertionTagEnd);
            }

            File.WriteAllText(filePath, sb.ToString());
        }
    }
}