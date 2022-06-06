using System;
using System.Collections.Generic;

namespace Fp.ProjectTwiner
{
	public class ExecuteCommandLine : CommandLineBuilderBase, IDisposable
	{
		private readonly List<string> _commands = new List<string>();

		static ExecuteCommandLine()
		{
			Instance = new ExecuteCommandLine();
		}

		public ExecuteCommandLine(bool runAsAdmin = false) : base(runAsAdmin) { }

		public static ExecuteCommandLine Instance { get; }

#region IDisposable Implementation

		public void Dispose()
		{
			Reset();
		}

#endregion

		public void Reset()
		{
			_commands.Clear();
		}

		public void AddCommand(string command)
		{
			if(string.IsNullOrWhiteSpace(command))
			{
				throw new ArgumentException(nameof(command));
			}

			_commands.Add(command);
		}

		protected override string[] GetCommands()
		{
			return _commands.ToArray();
		}
	}
}