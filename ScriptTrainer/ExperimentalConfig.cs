using System;
using System.IO;

namespace ScriptTrainer;

// 实验性功能开关的共用存储：BepInEx\ScriptTrainer.experimental.cfg，一行一个 key=1/0。
// 外部 UI 也直接读写这个文件（游戏不在线时先落盘，插件下次启动读到）。
internal static class ExperimentalConfig
{
	public static string Path => System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "ScriptTrainer.experimental.cfg");

	public static bool Read(string key, string logTag)
	{
		try
		{
			if (!File.Exists(Path))
			{
				return false;
			}
			foreach (string raw in File.ReadAllLines(Path))
			{
				string line = raw.Trim();
				int eq = line.IndexOf('=');
				if (eq <= 0 || line.StartsWith("#"))
				{
					continue;
				}
				if (string.Equals(line.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
				{
					string v = line.Substring(eq + 1).Trim();
					return v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
				}
			}
		}
		catch (Exception ex)
		{
			TrainerLog.Write(logTag + " read config failed: " + ex.Message);
		}
		return false;
	}

	public static void Write(string key, bool value, string logTag)
	{
		try
		{
			System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();
			bool replaced = false;
			if (File.Exists(Path))
			{
				foreach (string raw in File.ReadAllLines(Path))
				{
					string line = raw.Trim();
					int eq = line.IndexOf('=');
					if (eq > 0 && string.Equals(line.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
					{
						if (!replaced)
						{
							lines.Add(key + "=" + (value ? "1" : "0"));
							replaced = true;
						}
						continue;
					}
					lines.Add(raw);
				}
			}
			if (!replaced)
			{
				if (lines.Count == 0)
				{
					lines.Add("# ScriptTrainer 实验性功能开关（外部 UI 与插件共用）");
				}
				lines.Add(key + "=" + (value ? "1" : "0"));
			}
			File.WriteAllLines(Path, lines.ToArray());
		}
		catch (Exception ex)
		{
			TrainerLog.Write(logTag + " write config failed: " + ex.Message);
		}
	}
}
