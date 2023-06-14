using System;
using System.Collections.Generic;

namespace Fp.ProjectTwiner.Utility
{
	public enum GreekAlphabet
	{
		Alpha,
		Beta,
		Gamma,
		Delta,
		Epsilon,
		Zeta,
		Eta,
		Theta,
		Iota,
		Kappa,
		Lambda,
		Mu,
		Nu,
		Xi,
		Omicron,
		Pi,
		Rho,
		Sigma,
		Tau,
		Upsilon,
		Phi,
		Chi,
		Psi,
		Omega
	}

	public static class TextUtility
	{
		private static readonly string[] s_greekAlphabet;

		static TextUtility()
		{
			s_greekAlphabet = Enum.GetNames(typeof(GreekAlphabet));
		}

		public static IReadOnlyList<string> GreekAlphabet => s_greekAlphabet;

		public static string GetGreekLetter(GreekAlphabet alphabet)
		{
			return s_greekAlphabet[(int) alphabet];
		}
		
		public static string GetGreekLetter(int alphabet)
		{
			return s_greekAlphabet[alphabet];
		}
		
		public static string GetRandomGreekLetter()
		{
			return GreekAlphabet[UnityEngine.Random.Range(0, s_greekAlphabet.Length)];
		}
		
		public static int GreekLetterCount() => s_greekAlphabet.Length;
	}
}