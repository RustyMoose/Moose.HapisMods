using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Moose.Hapis;

internal static class HapisLog
{
	private static readonly string Tag = "[" + typeof(HapisLog).Assembly.GetName().Name + "] ";
	private static readonly HashSet<string> Reported = new();

	public static void Info(string message)
	{
		Debug.LogWarning(Tag + Tidy(message));
	}

	public static void Shared(string message)
	{
		var key = "Moose.Hapis.Logged." + message;
		if (AppDomain.CurrentDomain.GetData(key) != null)
		{
			return;
		}

		AppDomain.CurrentDomain.SetData(key, true);
		Info(message);
	}

	public static void Once(string where, Exception e)
	{
		if (Reported.Add(where + "/" + e.GetType().Name))
		{
			Debug.LogWarning(Tag + Tidy("Problem in " + where) + ": " + e);
		}
	}

	private static string Tidy(string text)
	{
		var words = text.Split(' ');
		for (var i = 0; i < words.Length; i++)
		{
			var word = words[i];
			if (word.Length == 0 || word.IndexOf('/') >= 0 || word.IndexOf('\\') >= 0)
			{
				continue;
			}

			var built = new StringBuilder(word.Length);
			var first = true;
			foreach (var c in word)
			{
				if (char.IsLetter(c))
				{
					built.Append(first ? char.ToUpperInvariant(c) : c);
					first = false;
				}
				else
				{
					built.Append(c);
				}
			}

			words[i] = built.ToString();
		}

		return string.Join(" ", words);
	}
}
