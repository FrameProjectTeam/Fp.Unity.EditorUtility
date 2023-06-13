#if UNITY_EDITOR_WIN

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Fp.ProjectTwiner.Utility.ReorderableList.Editor;

using UnityEditor;

using UnityEngine;

using Debug = UnityEngine.Debug;

namespace Fp.ProjectTwiner
{
	public class TwinProjectEditor : EditorWindow
	{
		private const int DisplayProgressDelay = 250;
		private const float DirectoryCopingProgressPct = 0.1f;
		private const float FileCopingProgressPct = 1 - DirectoryCopingProgressPct;
		private const string TwinEditorTitle = "Twin Editor";
		private const string GitIgnoreFileName = ".gitignore";
		private const string CancelButtonText = "Cancel";
		private const string SkipButtonText = "Skip";
		private const string OkButtonText = "Ok";

		private const string PrefixPrefs = "TE_";
		private const string StorePathKey = PrefixPrefs + "StorePath";
		private const string TwinCloneDirName = "ProjectTwins";

		private static readonly string[] DefaultRealCopyPath =
		{
			//Directories
			"Library/Artifacts/",
			//Files
			"Library/ArtifactDB",
			"Library/SourceAssetsDB",
		};

		private static readonly string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";

		private static readonly string[] DefaultSymlinkPath =
		{
			//Directories
			"Assets/",
			"Packages/",
			"GooglePackages/",
			//Files
			"ProjectSettings/"
		};

		private ReorderableList _additionalSymlinkProp;
		private TwinProjectCache _cache;

		private TwinProjectSettings _newProjectSettings;

		private ReorderableList _realCopyProp;
		private Vector2 _scrollPosition;

		private SerializedObject _serializedObject;

		private void OnEnable()
		{
			InitRuntimeObjects();
		}

		private void OnDisable()
		{
			DestroyImmediate(_newProjectSettings);
			EditorPrefs.SetString(StorePathKey, _newProjectSettings.StorePath);
		}

		private void OnGUI()
		{
			if(!_newProjectSettings)
			{
				InitRuntimeObjects();
			}

			try
			{
				_scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
				try
				{
					EditorGUILayout.BeginVertical("box", GUILayout.ExpandHeight(false));

					DrawActiveCopies();
				}
				finally
				{
					EditorGUILayout.EndVertical();
				}

				try
				{
					EditorGUILayout.BeginVertical("box");

					CreationToolDraw();
				}
				finally
				{
					EditorGUILayout.EndVertical();
				}
			}
			finally
			{
				EditorGUILayout.EndScrollView();
			}

			_serializedObject.ApplyModifiedProperties();
		}

		private static string GetCurrentProjectDirectory()
		{
			return Directory.GetCurrentDirectory();
		}

		private static bool CreateClone(TwinProjectSettings twinSettings, TwinProjectCache twinProjectCache)
		{
			var twinId = Guid.NewGuid();

			string projectPath = GetCurrentProjectDirectory();
			string twinPath = GetTwinPath(twinSettings.StorePath, twinId);

			if(!Directory.Exists(twinPath))
			{
				Directory.CreateDirectory(twinPath);
			}
			
			var symlinkCommand = new SymlinkCommandBuilder();

			GitIgnoreRegex realCopyIgnoreRegex = GitIgnoreRegex.ParseGitIgnoreRules(twinSettings.RealCopyPath.Where(p => p.Enabled).Select(p => $"!{p.Value}").Concat(new[] {"*"}).ToArray());
			GitIgnoreRegex symlinkIgnoreRegex;
			
			if(twinSettings.FilterByGitignore)
			{
				var ignoreFile = new FileInfo(Path.Combine(projectPath, GitIgnoreFileName));
				if(!ignoreFile.Exists)
				{
					Debug.LogError("Cant copy project to twin. Gitignore file not found");
					return false;
				}

				symlinkIgnoreRegex = GitIgnoreRegex.ParseGitIgnore(ignoreFile);

				if(symlinkIgnoreRegex == null)
				{
					Debug.LogError("Cant copy project to twin. Invalid parse process gitignore file");
					return false;
				}
			}
			else
			{
				symlinkIgnoreRegex = GitIgnoreRegex.ParseGitIgnoreRules(
					twinSettings.SymlinkPath.Where(p => p.Enabled).Select(p => $"!{p.Value}").Concat(new[] { "*" }).ToArray()
				);
			}
			
			var symlinkPath = new List<string>();
			var realCopyPath = new List<string>();
			
			var pathQueue = new Queue<string>();

			pathQueue.Enqueue(projectPath);
			while(pathQueue.TryDequeue(out string curPath))
			{
				//Symlinks directories
				foreach(string directoryInfo in Directory.GetDirectories(curPath))
				{
					string relativePath = PathUtils.MakeRelativePath(projectPath, directoryInfo) + Path.AltDirectorySeparatorChar;

					if(realCopyIgnoreRegex.Includes(relativePath))
					{
						realCopyPath.Add(relativePath);
						continue;
					}
					
					if(realCopyIgnoreRegex.MaybeIncludes(relativePath))
					{
						pathQueue.Enqueue(directoryInfo);
						continue;
					}
					
					if(symlinkIgnoreRegex.Ignores(relativePath))
					{
						continue;
					}

					symlinkPath.Add(relativePath);
				}

				//Symlinks files 
				foreach(string filePath in Directory.GetFiles(curPath))
				{
					string relativePath = PathUtils.MakeRelativePath(projectPath, filePath);

					if(realCopyIgnoreRegex.Includes(relativePath))
					{
						realCopyPath.Add(relativePath);
						continue;
					}
						
					if(symlinkIgnoreRegex.Ignores(relativePath))
					{
						continue;
					}

					symlinkPath.Add(relativePath);
				}
			}
			
			foreach(string path in symlinkPath)
			{
				if(PathUtils.IsDirectoryPath(path))
				{
					var targetDir = new DirectoryInfo(PathUtils.FixDirPath(Path.Combine(twinPath, path)));
					if(targetDir.Parent is { Exists: false })
					{
						targetDir.Parent.Create();
					}

					symlinkCommand.AddDirectory(PathUtils.FixDirPath(Path.Combine(projectPath, path)), targetDir.FullName);
				}
				else
				{
					var targetDir = new FileInfo(PathUtils.FixDirPath(Path.Combine(twinPath, path)));
					if(targetDir.Directory?.Parent is { Exists: false })
					{
						targetDir.Directory.Parent.Create();
					}

					symlinkCommand.AddFile(
						PathUtils.FixPath(Path.Combine(projectPath, path)),
						PathUtils.FixPath(Path.Combine(twinPath, path))
					);
				}
			}

			try
			{
				EditorUtility.DisplayProgressBar("Create symlinks", "MKLINK execution", 0);

				symlinkCommand.Execute();
			}
			catch(Exception e)
			{
				Debug.LogException(e);
				return false;
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}

			UpdateProjectSettingsFile(projectPath, twinPath, twinId);

			foreach(string subPath in realCopyPath)
			{
				if(PathUtils.IsDirectoryPath(subPath))
				{
					RealCopySubdirectory(projectPath, twinPath, subPath);
				}
				else
				{
					string sourcePath = Path.Combine(projectPath, subPath);
					string destinationPath = Path.Combine(twinPath, subPath);

					if(!File.Exists(sourcePath))
					{
						continue;
					}

					File.Copy(sourcePath, destinationPath, true);
				}
			}

			twinProjectCache.Records.Add(
				new TwinProjectCache.TwinProjectRecord
				{
					Guid = twinId.ToString("N"),
					FullPath = twinPath,
					RealCopyPaths = realCopyPath.ToArray(),
					SymlinksPaths = symlinkCommand.GetSymlinkSource()
				}
			);
			return true;
		}

		private static bool UpdateProjectSettingsFile(string projectPath, string twinPath, Guid twinId)
		{
			string sourcePath = Path.Combine(projectPath, ProjectSettingsPath);

			if(!File.Exists(sourcePath))
			{
				return false;
			}

			string destinationPath = Path.Combine(twinPath, ProjectSettingsPath);
			string sourceSettings = File.ReadAllText(sourcePath);

			string newSettings = Regex.Replace(sourceSettings, @"productName: (.+)$", $"productName: $1 [{twinId:N}]", RegexOptions.Multiline);
			if(File.Exists(destinationPath))
			{
				File.Delete(destinationPath);
			}

			File.WriteAllText(destinationPath, newSettings, Encoding.UTF8);
			return true;
		}

		private static string GetTwinPath(string basePath, Guid twinId)
		{
			var originalDirectoryInfo = new DirectoryInfo(Directory.GetCurrentDirectory());
			var originalFolderName = $"{originalDirectoryInfo.Name}/";

			return PathUtils.FixDirPath(Path.Combine(basePath, TwinCloneDirName, $"{twinId:N}", originalFolderName));
		}

		private string GetCacheKey()
		{
			string productName = PlayerSettings.productName;
			productName = productName.Replace(" ", "_");
			return $"{PrefixPrefs}{productName}";
		}

		private bool TryReadCache(out TwinProjectCache cache)
		{
			string cacheKey = GetCacheKey();
			string cacheValue = EditorPrefs.GetString(cacheKey, string.Empty);

			if(!string.IsNullOrWhiteSpace(cacheValue))
			{
				//Deserialize
				cache = JsonUtility.FromJson<TwinProjectCache>(cacheValue);
				return true;
			}

			cache = null;
			return false;
		}

		private void SaveCache(TwinProjectCache cache)
		{
			string cacheKey = GetCacheKey();
			string jsonValue = JsonUtility.ToJson(cache);
			EditorPrefs.SetString(cacheKey, jsonValue);
			Debug.Log($"save cache as: \n{jsonValue}");
		}

		[MenuItem("Tools/TwinEditor")]
		private static void ShowWindow()
		{
			var twinProjectWindow = GetWindow<TwinProjectEditor>(TwinEditorTitle);
			twinProjectWindow.Show();
		}

		private void InitRuntimeObjects()
		{
			_newProjectSettings = CreateInstance<TwinProjectSettings>();
			_serializedObject = new SerializedObject(_newProjectSettings);

			ResetSettings();

			_realCopyProp = CreateReorderableList(_serializedObject.FindProperty(nameof(TwinProjectSettings.RealCopyPath)));
			_additionalSymlinkProp = CreateReorderableList(_serializedObject.FindProperty(nameof(TwinProjectSettings.SymlinkPath)));

			_newProjectSettings.StorePath = EditorPrefs.GetString(StorePathKey, Path.GetTempPath());

			if(!TryReadCache(out _cache))
			{
				_cache = new TwinProjectCache();
			}
		}

		private void DrawActiveCopies()
		{
			EditorGUILayout.LabelField($"{TwinEditorTitle} (Existing Twins)", EditorStyles.centeredGreyMiniLabel);
			try
			{
				GUILayout.BeginVertical();

				for(var i = 0; i < _cache.Records.Count; i++)
				{
					TwinProjectCache.TwinProjectRecord twinProjectRecord = _cache.Records[i];
					try
					{
						GUILayout.BeginHorizontal(EditorStyles.helpBox);
						try
						{
							GUILayout.BeginVertical();

							EditorGUILayout.LabelField(nameof(Guid), twinProjectRecord.Guid);
							EditorGUILayout.LabelField("Full Path", twinProjectRecord.FullPath);
							EditorGUILayout.LabelField("Symlinks", twinProjectRecord.SymlinksPaths.Length.ToString());
							EditorGUILayout.LabelField("Paths", twinProjectRecord.RealCopyPaths.Length.ToString());
						}
						finally
						{
							GUILayout.EndVertical();
						}

						try
						{
							GUILayout.BeginVertical(GUILayout.MaxWidth(125));

							if(GUILayout.Button("Open Unity"))
							{
								OpenUnityEditor(new DirectoryInfo(twinProjectRecord.FullPath));
							}

							if(GUILayout.Button("Refresh settings"))
							{
								UpdateProjectSettingsFile(
									GetCurrentProjectDirectory(), twinProjectRecord.FullPath, Guid.Parse(twinProjectRecord.Guid)
								);
							}

							if(GUILayout.Button("Delete Twin"))
							{
								RemoveProject(twinProjectRecord.Guid);
							}

							if(GUILayout.Button("Open in Explorer"))
							{
								OpenInExplorer(new DirectoryInfo(twinProjectRecord.FullPath));
							}
						}
						finally
						{
							GUILayout.EndVertical();
						}
					}
					finally
					{
						GUILayout.EndHorizontal();
					}
				}
			}
			finally
			{
				GUILayout.EndVertical();
			}
		}

		private void OpenInExplorer(DirectoryInfo directoryInfo)
		{
			Process.Start("explorer.exe", $"/select,{directoryInfo.FullName}");
		}

		private void RemoveProject(string guid)
		{
			int removeIdx = -1;
			for(var index = 0; index < _cache.Records.Count; index++)
			{
				TwinProjectCache.TwinProjectRecord twinProjectRecord = _cache.Records[index];
				if(twinProjectRecord.Guid.Equals(guid))
				{
					removeIdx = index;
				}
			}

			if(removeIdx < 0)
			{
				return;
			}

			TwinProjectCache.TwinProjectRecord removedProject = _cache.Records[removeIdx];

			if(!DeleteDirectory(new DirectoryInfo(removedProject.FullPath).Parent))
			{
				return;
			}

			_cache.Records.RemoveAt(removeIdx);
			SaveCache(_cache);
		}

		private bool DeleteDirectory(DirectoryInfo dir)
		{
			if(!dir.Exists)
			{
				return true;
			}

			var files = new Stack<string>();
			var directories = new Stack<string>();

			var directoriesInQueue = new Queue<DirectoryInfo>();
			directoriesInQueue.Enqueue(dir);

			try
			{
				var sw = Stopwatch.StartNew();
				var title = $"Deleting directory [{dir.FullName}]";

				EditorUtility.DisplayProgressBar(title, "Marking files && directories", 0);

				while(directoriesInQueue.Count > 0)
				{
					dir = directoriesInQueue.Dequeue();
					if(sw.Elapsed.Milliseconds > DisplayProgressDelay)
					{
						sw.Restart();
						EditorUtility.DisplayProgressBar(title, $"Marking [{dir.FullName}]", 0);
					}

					DirectoryInfo[] allDirectories = dir.GetDirectories("*", SearchOption.TopDirectoryOnly);
					FileInfo[] allFiles = dir.GetFiles("*", SearchOption.TopDirectoryOnly);

					foreach(FileInfo fileInfo in allFiles)
					{
						string fileName = fileInfo.FullName;
						if(fileName.Length < 2 || !fileName.StartsWith(@"\\"))
						{
							//TODO: only for windows ?
							fileName = @"\\?\" + fileName;
						}

						files.Push(fileName);
					}

					directories.Push(dir.FullName);

					foreach(DirectoryInfo directoryInfo in allDirectories)
					{
						if(PathUtils.IsSymbolic(directoryInfo))
						{
							directories.Push(directoryInfo.FullName);
						}
						else
						{
							directoriesInQueue.Enqueue(directoryInfo);
						}
					}
				}

				int counter = files.Count;

				while(files.Count > 0)
				{
					string path = files.Pop();
					if(sw.Elapsed.Milliseconds > DisplayProgressDelay)
					{
						sw.Restart();
						if(EditorUtility.DisplayCancelableProgressBar(title, $"File: {path}", 1 - (float)files.Count / counter))
						{
							return false;
						}
					}

					try
					{
						//TODO: File.Delete not working with long path
						//File.Delete(path);
						new FileInfo(path).Delete();
					}
					catch(Exception e)
					{
						Debug.LogException(e);
					}
				}

				counter = directories.Count;
				while(directories.Count > 0)
				{
					string path = directories.Pop();

					if(sw.Elapsed.Milliseconds > DisplayProgressDelay)
					{
						sw.Restart();
						if(EditorUtility.DisplayCancelableProgressBar(title, $"Directory: [{path}]", 1 - (float)directories.Count / counter))
						{
							return false;
						}
					}

					var dirInfo = new DirectoryInfo(path);

					try
					{
						dirInfo.Delete();
					}
					catch(UnauthorizedAccessException ex)
					{
						Debug.LogWarning(ex.ToString());
						if((dirInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
						{
							Debug.Log($"Try to change attribute {dirInfo.Attributes} to {dirInfo.Attributes ^ FileAttributes.ReadOnly}");
							dirInfo.Attributes ^= FileAttributes.ReadOnly;
							//Repeating delete this directory
							dirInfo.Delete();
						}
						else
						{
							//User command
							using(var cli = ExecuteCommandLine.Instance)
							{
								cli.AddCommand($"takeown /r /f \"{dirInfo.FullName}\"");
								cli.AddCommand($"rmdir /s /q \"{dirInfo.FullName}\"");

								cli.Execute();
							}
						}
					}
				}

				sw.Stop();
			}
			catch(Exception ex)
			{
				Debug.LogException(ex);
				return false;
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}

			return true;
		}

		private void CreationToolDraw()
		{
			EditorGUILayout.LabelField($"{TwinEditorTitle} (Create New)", EditorStyles.centeredGreyMiniLabel);

			try
			{
				GUILayout.BeginVertical(EditorStyles.helpBox);

				_serializedObject.Update();

				EditorGUILayout.LabelField(nameof(Guid), _newProjectSettings.TwinGuid.ToString("N"));
				EditorGUILayout.PropertyField(_serializedObject.FindProperty(nameof(TwinProjectSettings.FilterByGitignore)));
				
				DrawPath(_serializedObject.FindProperty(nameof(TwinProjectSettings.StorePath)));

				//Draw real copy path list
				_realCopyProp.DoLayoutList();
				//Draw additional symlink path list
				if(!_newProjectSettings.FilterByGitignore)
				{
					_additionalSymlinkProp.DoLayoutList();
				}

				if(GUILayout.Button("Make Twin"))
				{
					if(CreateClone(_newProjectSettings, _cache))
					{
						//OpenUnityEditor(twinProjectDir);
						ResetSettings();
						SaveCache(_cache);
					}
					else
					{
						EditorUtility.DisplayDialog("Error", "Twin wasn't created", OkButtonText);
					}
				}
			}
			finally
			{
				GUILayout.EndVertical();
			}
		}

		private void DrawPath(SerializedProperty pathProperty)
		{
			EditorGUILayout.LabelField(pathProperty.displayName, pathProperty.stringValue);

			GUILayout.BeginHorizontal();
			if(GUILayout.Button($"Change {pathProperty.displayName}"))
			{
				string newPath = EditorUtility.OpenFolderPanel("Select new Path", pathProperty.stringValue, string.Empty);
				if(Directory.Exists(newPath))
				{
					pathProperty.stringValue = PathUtils.FixDirPath(newPath);
				}
			}

			if(GUILayout.Button("Temp", GUILayout.Width(50)))
			{
				pathProperty.stringValue = PathUtils.FixDirPath(Path.GetTempPath());
			}

			GUILayout.EndHorizontal();
		}

		private void ResetSettings()
		{
			_newProjectSettings.SymlinkPath.Clear();
			_newProjectSettings.RealCopyPath.Clear();

			_newProjectSettings.SymlinkPath.AddRange(DefaultSymlinkPath.Select(s => new LockedString { Enabled = true, Readonly = false, Value = s }));
			_newProjectSettings.RealCopyPath.AddRange(DefaultRealCopyPath.Select(s => new LockedString { Enabled = true, Readonly = false, Value = s }));
			_newProjectSettings.RealCopyPath.Add(new LockedString { Enabled = true, Readonly = true, Value = ProjectSettingsPath });

			_newProjectSettings.TwinGuid = Guid.NewGuid();
			_newProjectSettings.FilterByGitignore = true;
		}

		private static void OpenUnityEditor(DirectoryInfo projectDir)
		{
			string editorExePath = EditorApplication.applicationPath;
			BuildTarget activeBuildTarget = EditorUserBuildSettings.activeBuildTarget;

			var processStartInfo = new ProcessStartInfo
			{
				FileName = editorExePath,
				Arguments = $"-projectPath \"{projectDir.FullName}\" -buildTarget {activeBuildTarget}"
			};

			var process = new Process { StartInfo = processStartInfo };
			process.Start();
			process.Dispose();
		}

		private static bool RealCopySubdirectory(string path, string twinPath, string subDirectory)
		{
			try
			{
				//Copy library
				string sourcePath = Path.Combine(path, subDirectory);
				string destinationPath = Path.Combine(twinPath, subDirectory);

				if(!Directory.Exists(sourcePath) || Directory.Exists(destinationPath))
				{
					return false;
				}

				var progressTitle = $"Coping [{subDirectory}]";

				Directory.CreateDirectory(destinationPath);

				EditorUtility.DisplayProgressBar(progressTitle, "Marking directories for copping", 0f);
				string[] dictionaries = Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories);

				var sw = Stopwatch.StartNew();

				//Now Create all of the directories
				for(var i = 0; i < dictionaries.Length; i++)
				{
					string dirPath = dictionaries[i];

					if(sw.ElapsedMilliseconds > DisplayProgressDelay)
					{
						sw.Restart();
						if(EditorUtility.DisplayCancelableProgressBar(
							   progressTitle, $"Copping [{i} / {dictionaries.Length}]",
							   (float)i / dictionaries.Length * DirectoryCopingProgressPct
						   ))
						{
							return false;
						}
					}

					Directory.CreateDirectory(dirPath.Replace(sourcePath, destinationPath));
				}

				EditorUtility.DisplayProgressBar(progressTitle, "Marking files for copping", DirectoryCopingProgressPct);
				string[] files = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
				//Copy all the files & Replaces any files with the same name
				for(var i = 0; i < files.Length; i++)
				{
					string filePath = files[i];

					if(sw.ElapsedMilliseconds > DisplayProgressDelay)
					{
						sw.Restart();
						if(EditorUtility.DisplayCancelableProgressBar(
							   progressTitle, $"Copping [{i}/{files.Length}]: {filePath}",
							   DirectoryCopingProgressPct + (float)i / files.Length * FileCopingProgressPct
						   ))
						{
							return false;
						}
					}

					File.Copy(filePath, filePath.Replace(sourcePath, destinationPath), true);
				}
			}
			catch(Exception ex)
			{
				EditorUtility.DisplayDialog($"Error coping [{subDirectory}]", ex.Message, OkButtonText);
				Debug.LogException(ex);
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}

			return true;
		}

		[Serializable]
		internal class TwinProjectSettings : ScriptableObject
		{
			[SerializeField]
			internal Guid TwinGuid;

			[SerializeField]
			internal bool FilterByGitignore = false;

			[SerializeField]
			internal string StorePath;

			[SerializeField]
			internal List<LockedString> RealCopyPath = new List<LockedString>();

			[SerializeField]
			internal List<LockedString> SymlinkPath = new List<LockedString>();
		}

		[Serializable]
		internal struct LockedString
		{
			public bool Enabled;
			public bool Readonly;
			public string Value;
		}

		[Serializable]
		internal class TwinProjectCache
		{
			public List<TwinProjectRecord> Records = new List<TwinProjectRecord>();

			[Serializable]
			internal class TwinProjectRecord
			{
				public string Guid;
				public string FullPath;
				public string[] SymlinksPaths;
				public string[] RealCopyPaths;
			}
		}

#region ReordrableList Function

		private ReorderableList CreateReorderableList(SerializedProperty property)
		{
			var list = new ReorderableList(property)
			{
				expandable = false
			};

			list.getElementHeightCallback += OnCalculateElementHeight;
			list.drawElementCallback += OnDrawElementCallback;
			list.onAddCallback += OnAddCallback;
			list.onCanRemoveCallback += OnCanRemoveCallback;

			return list;
		}

		private bool OnCanRemoveCallback(ReorderableList list)
		{
			return list.Selected.Select(i => list.List.GetArrayElementAtIndex(i))
					   .Select(item => item.FindPropertyRelative(nameof(LockedString.Readonly)))
					   .All(locked => !locked.boolValue);
		}

		private void OnAddCallback(ReorderableList list)
		{
			SerializedProperty newItem = list.AddItem();
			SerializedProperty locked = newItem.FindPropertyRelative(nameof(LockedString.Readonly));
			SerializedProperty value = newItem.FindPropertyRelative(nameof(LockedString.Value));

			locked.boolValue = false;
			value.stringValue = string.Empty;
		}

		private float OnCalculateElementHeight(SerializedProperty element)
		{
			return EditorGUI.GetPropertyHeight(element.FindPropertyRelative(nameof(LockedString.Value)));
		}

		private void OnDrawElementCallback(
			Rect rect,
			SerializedProperty element,
			GUIContent label,
			bool selected,
			bool focused)
		{
			SerializedProperty isEnabled = element.FindPropertyRelative(nameof(LockedString.Enabled));
			SerializedProperty locked = element.FindPropertyRelative(nameof(LockedString.Readonly));
			SerializedProperty value = element.FindPropertyRelative(nameof(LockedString.Value));

			var tag = string.Empty;

			string strValue = value.stringValue;
			if(string.IsNullOrWhiteSpace(strValue))
			{
				tag = "None";
			}
			else if(PathUtils.IsDirectoryPath(strValue))
			{
				tag = "Directory";
			}
			else
			{
				tag = "File";
			}

			bool cachedEnabledStatus = GUI.enabled;

			GUI.enabled = !locked.boolValue;

			//Set witdh for checkbox
			Rect checkBoxWidth = rect;
			checkBoxWidth.width = EditorGUIUtility.singleLineHeight;
			
			EditorGUI.PropertyField(checkBoxWidth, isEnabled, GUIContent.none);

			rect.width -= checkBoxWidth.width;
			rect.x += checkBoxWidth.width;
			
			GUI.enabled = !locked.boolValue && isEnabled.boolValue;
			EditorGUI.PropertyField(rect, value, new GUIContent(tag));

			GUI.enabled = cachedEnabledStatus;
		}

#endregion
	}
}

#endif