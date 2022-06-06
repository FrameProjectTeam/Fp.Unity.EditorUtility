using System.Diagnostics;
using System.Globalization;

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

		public void Execute()
		{
			string[] commands = GetCommands();

			string stringCommand = string.Format(CultureInfo.InvariantCulture, $"/c {string.Join(" & ", commands)}");

			Debug.Log($"Command line execution: \ncmd.exe {stringCommand}");

			var processStartInfo = new ProcessStartInfo
			{
				FileName = "cmd",
				Arguments = stringCommand,
				UseShellExecute = true,
				CreateNoWindow = true
			};

			if(_runAsAdmin)
			{
				processStartInfo.Verb = "runas";
			}

			var process = new Process { StartInfo = processStartInfo, EnableRaisingEvents = true };

			process.Start();

			process.WaitForExit();
			process.Close();
			process.Dispose();
		}

		protected abstract string[] GetCommands();
	}
}