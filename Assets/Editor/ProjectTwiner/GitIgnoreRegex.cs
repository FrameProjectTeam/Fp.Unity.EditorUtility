using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Fp.ProjectTwiner
{
	public class GitIgnoreRegex
	{
		private readonly Regex[] _positives;
		private readonly Regex[] _negatives;

		private GitIgnoreRegex(Regex[] positives, Regex[] negatives)
		{
			_positives = positives;
			_negatives = negatives;
		}

		public bool Accepts(string path)
		{
			path = SliceRelative(path);

			return _negatives[0].IsMatch(path) || !_positives[0].IsMatch(path);
		}

		public bool Denies(string path)
		{
			path = SliceRelative(path);

			return !(_negatives[0].IsMatch(path) || !_positives[0].IsMatch(path));
		}

		public bool Maybe(string path)
		{
			path = SliceRelative(path);

			return _negatives[1].IsMatch(path) || !_positives[1].IsMatch(path);
		}

		public static GitIgnoreRegex Parse(FileInfo ignoreFile)
		{
			if(ignoreFile == null || !ignoreFile.Exists)
			{
				throw new ArgumentException(nameof(ignoreFile));
			}

			Regex[][] pattern = File.ReadAllLines(ignoreFile.FullName)
									.Select(l => l.Trim())
									.Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#"))
									.Aggregate(
										new[]
										{
											new List<string>(),
											new List<string>()
										}, (lists, s) =>
										{
											bool isNegative = s[0] == '!';
											if(isNegative)
											{
												s = s.Substring(1, s.Length - 1);
											}

											if(s[0] == '/')
											{
												s = s.Substring(1, s.Length - 1);
											}

											if(isNegative)
											{
												lists[1].Add(s);
											}
											else
											{
												lists[0].Add(s);
											}

											return lists;
										}
									)
									.Select(
										list =>
										{
											return list
												   .OrderBy(s => s)
												   .Select(PrepareRegexes)
												   .Aggregate(
													   new[]
													   {
														   new List<string>(), 
														   new List<string>()
													   }, (lists, strings) =>
													   {
														   lists[0].Add(strings[0]);
														   lists[1].Add(strings[1]);

														   return lists;
													   }
												   );
										}
									)
									.Select(
										lists =>
										{
											var regx = new Regex[2];
											regx[0] = lists[0].Count > 0 ? new Regex($"^(({string.Join(")|(", lists[0])}))") : new Regex("$^");
											regx[1] = lists[1].Count > 0 ? new Regex($"^(({string.Join(")|(", lists[1])}))") : new Regex("$^");
											return regx;
										}
									)
									.ToArray();

			Regex[] positives = pattern[0];
			Regex[] negatives = pattern[1];

			return new GitIgnoreRegex(positives, negatives);
		}

		private static string SliceRelative(string path)
		{
			if(path.StartsWith("/"))
			{
				path = path.Substring(1, path.Length - 1);
			}

			return path;
		}

		private static string[] PrepareRegexes(string pattern)
		{
			return new[]
			{
				// exact regex
				PrepareRegexPattern(pattern),
				// partial regex
				PreparePartialRegex(pattern)
			};
		}

		private static string PrepareRegexPattern(string pattern)
		{
			return EscapeRegex(pattern).Replace("**", "(.+)").Replace("*", "([^\\/]+)").Replace("?", "([^\\/])");
		}

		private static string PreparePartialRegex(string pattern)
		{
			return string.Join(
				"", pattern.Split('/')
						   .Select((s, i) => i > 0 ? $"([\\/]?({PrepareRegexPattern(s)}\\b|$))" : $"({PrepareRegexPattern(s)}\\b)")
			);
		}

		private static string EscapeRegex(string pattern)
		{
			return Regex.Replace(pattern, @"[\-\/\{\}\(\)\+\?\.\\\^\$\|]", "\\$&");
		}
	}
}