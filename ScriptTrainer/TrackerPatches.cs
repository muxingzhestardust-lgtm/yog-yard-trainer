using System;
using System.Reflection;
using HarmonyLib;
using SaveLoadSystem;

namespace ScriptTrainer;

public static class TrackerPatches
{
	public static void Init()
	{
		try
		{
			Harmony harmony = new Harmony("com.github.3dmxm.ScriptTrainer.Tracker");
			System.Type egType = System.Type.GetType(ObfNames.EgType + ObfNames.AssemblySuffix);
			System.Type qoType = System.Type.GetType(ObfNames.QoType + ObfNames.AssemblySuffix);
			Patch(harmony, egType, ObfNames.TrkRelicFlowA, "Prefix_RelicFlowA");
			Patch(harmony, egType, ObfNames.TrkRelicFlowB, "Prefix_RelicFlowB");
			Patch(harmony, egType, ObfNames.EgCreateRelic, "Prefix_CreateRelic");
			Patch(harmony, egType, ObfNames.TrkRelicSave, "Prefix_RelicSave");
			Patch(harmony, qoType, ObfNames.QoAddItem, "Prefix_AddItem");
			TrainerLog.Write("Tracker safe patches applied.");
		}
		catch (System.Exception ex)
		{
			TrainerLog.Write("Tracker patch failed: " + ex);
		}
	}

	private static void Patch(Harmony harmony, System.Type type, string methodName, string prefixName)
	{
		try
		{
			if (type == null)
			{
				TrainerLog.Write("[TRACKER] patch missing type for " + methodName);
				return;
			}
			MethodInfo methodInfo = AccessTools.Method(type, methodName);
			MethodInfo methodInfo2 = AccessTools.Method(typeof(TrackerPatches), prefixName);
			if (methodInfo == null || methodInfo2 == null)
			{
				TrainerLog.Write("[TRACKER] patch missing " + type.FullName + "." + methodName);
			}
			else
			{
				harmony.Patch(methodInfo, new HarmonyMethod(methodInfo2));
			}
		}
		catch (System.Exception ex)
		{
			TrainerLog.Write($"[TRACKER] patch failed {type?.FullName}.{methodName}: {ex.GetType().Name}: {ex.Message}");
		}
	}

	// 新 dew = 旧 dev: bool dew(int, bool, bool, Action, bool)
	public static void Prefix_RelicFlowA(int __0, bool __1, bool __2, Il2CppSystem.Action __3, bool __4)
	{
		try
		{
			TrainerLog.Write($"[TRACKER] eg.{ObfNames.TrkRelicFlowA}({__0}, {__1}, {__2}, {__4})");
		}
		catch
		{
		}
	}

	// 新 dev = 旧 deu: void dev(int, int, Action, bool, bool, bool)
	public static void Prefix_RelicFlowB(int __0, int __1, Il2CppSystem.Action __2, bool __3, bool __4, bool __5)
	{
		try
		{
			TrainerLog.Write($"[TRACKER] eg.{ObfNames.TrkRelicFlowB}({__0}, {__1}, {__3}, {__4}, {__5})");
		}
		catch
		{
		}
	}

	// 新 dez = 旧 dey: ec dez(int, int, RelicEffectSaveParam)
	public static void Prefix_CreateRelic(int __0, int __1, RelicEffectSaveParam __2)
	{
		try
		{
			TrainerLog.Write($"[TRACKER] eg.{ObfNames.EgCreateRelic}({__0}, {__1}, save.ID={__2?.ID ?? (-1)})");
		}
		catch
		{
		}
	}

	// 新 dfa = 旧 dez: void dfa(int, RelicEffectSaveParam)
	public static void Prefix_RelicSave(int __0, RelicEffectSaveParam __1)
	{
		try
		{
			TrainerLog.Write($"[TRACKER] eg.{ObfNames.TrkRelicSave}({__0}, save.ID={__1?.ID ?? (-1)})");
		}
		catch
		{
		}
	}

	// 新 hmz = 旧 hmy: void hmz(int, int, bool)
	public static void Prefix_AddItem(int __0, int __1, bool __2)
	{
		try
		{
			TrainerLog.Write($"[TRACKER] qo.{ObfNames.QoAddItem}({__0}, {__1}, {__2})");
		}
		catch
		{
		}
	}
}
