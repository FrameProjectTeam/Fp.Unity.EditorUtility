using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Fp.ProjectTwiner
{
	public sealed class GitIgnoreRegex
	{
		private readonly RegExGroup _group;

		private GitIgnoreRegex(RegExGroup regExGroup)
		{
			_group = regExGroup;
		}

		public bool Includes(string path)
		{
			path = SliceRelative(FixPathSeparator(path));

			return _group.ForceInclude.Exact.IsMatch(path) || !_group.Exclude.Exact.IsMatch(path);
		}

		public bool Ignores(string path)
		{
			return !Includes(path);
		}

		public bool MaybeIncludes(string path)
		{
			path = SliceRelative(FixPathSeparator(path));

			return _group.ForceInclude.Partial.IsMatch(path) || !_group.Exclude.Partial.IsMatch(path);
		}

		public static GitIgnoreRegex ParseGitIgnoreRules(params string[] rules)
		{
			RegExPair[] pattern = rules
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
										  var regxPair = new RegExPair
										  {
											  Exact = lists[0].Count > 0 ? new Regex($"^(({string.Join(")|(", lists[0])}))") : new Regex("$^"),
											  Partial = lists[1].Count > 0 ? new Regex($"^(({string.Join(")|(", lists[1])}))") : new Regex("$^")
										  };
										  return regxPair;
									  }
								  )
								  .ToArray();
			
			var group = new RegExGroup
			{
				Exclude = pattern[0],
				ForceInclude = pattern[1]
			};

			return new GitIgnoreRegex(group);
		}

		public static GitIgnoreRegex ParseGitIgnore(FileInfo ignoreFile)
		{
			if(ignoreFile is not { Exists: true })
			{
				throw new ArgumentException(nameof(ignoreFile));
			}

			return ParseGitIgnoreRules(File.ReadAllLines(ignoreFile.FullName));
		}

		private static string FixPathSeparator(string path)
		{
			return path.Replace("\\", "/");
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

		private struct RegExGroup
		{
			public RegExPair Exclude;
			public RegExPair ForceInclude;
		}

		private struct RegExPair
		{
			public Regex Exact;
			public Regex Partial;
		}
	}
}