using System;
using System.IO;
using BepInEx;

namespace ScriptTrainer;

internal static class TrainerLog
{
	private static readonly string Path = System.IO.Path.Combine(Paths.BepInExRootPath, "ScriptTrainer.log");

	public static void Write(string message)
	{
		try
		{
			File.AppendAllText(Path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
		}
		catch
		{
		}
	}
}
