using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

using UnityEngine;

using Debug = UnityEngine.Debug;

namespace Fp.ProjectTwiner
{
	public abstract class CommandLineBuilderBase
	{
		private readonly bool _runAsAdmin;

		public CommandLineBuilderBase(bool runAsAdmin = true)
		{
			_runAsAdmin = runAsAdmin;
		}

		public void Execute(bool debug = false)
		{
			string[] commands = GetCommands();

			string stringCommand = string.Format(CultureInfo.InvariantCulture, string.Join(Environment.NewLine, commands));
			string batchFilePath = PathUtils.FixPath(Path.Combine(Directory.GetCurrentDirectory(), "run.bat"));

			var stringBuilder = new StringBuilder();

			stringBuilder.AppendLine("@echo");
			stringBuilder.AppendLine(stringCommand);
			if(debug)
			{
				stringBuilder.AppendLine("pause");
			}

			File.WriteAllText(batchFilePath, stringBuilder.ToString());

			stringBuilder.Clear();
			
			if(debug)
			{
				Debug.Log("Batch file created at: " + batchFilePath);
			}

			var processStartInfo = new ProcessStartInfo
			{
				FileName = batchFilePath,
				UseShellExecute = true,
				CreateNoWindow = true
			};

			if(_runAsAdmin)
			{
				processStartInfo.Verb = "runas";
			}

			var process = new Process()
			{
				StartInfo = processStartInfo
			};

			process.Start();

			process.WaitForExit();

			int exitCode = process.ExitCode;

			if(exitCode != 0)
			{
				Debug.LogError($"Executing command failed with exit code {exitCode:X}!");
			}
			
			process.Close();

			if(!debug)
			{
				File.Delete(batchFilePath);
			}
		}

		protected abstract string[] GetCommands();
	}
}