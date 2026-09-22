using System;
using System.Reflection;
using AlchemySystem;
using Foundation.UI;
using HarmonyLib;

namespace ScriptTrainer;

// ============================================================================
// 实验性功能：全部炼成无视 50 次上限
//
// 炼金窗口（Foundation.UI.bek）的「全部炼成」开关写的是 AlchemySystem.vo.zar；
// 点开始炼金后 bek.ldm → vo.iny(type, items)：
//   E_Item 且 !zak(十连) 且 zar(全部) 时：
//       zas = iop(items)            // 按背包库存算出最多能重复几次
//       if (zas > 50) zas = 50;     // 硬上限（vo 的 const yzy/zab = 50，内联在 iny 里）
//       ioe(items, zas);            // 真正扣材料、出结果的批处理
// 行动力等消耗在 iny 里只扣一次，与次数无关；ioe 自己不再限次。
//
// 这里 Harmony 前缀 vo.ioe：走「全部炼成」路径且传进来的次数正好是 50 时，
// 重新调 iop 取真实上限并改写参数（iop 只算不改状态，重复调用安全）。
// 开关持久化在 BepInEx\ScriptTrainer.experimental.cfg（alchemy_nocap=1/0）。
// ============================================================================
internal static class AlchemyNoCap
{
	private const string ConfigKey = "alchemy_nocap";

	private const string LogTag = "[ALCHEMY]";

	// 游戏里的硬上限；只在传入次数恰好等于它时才可能是被夹过的
	private const int GameCap = 50;

	private static bool patched;

	public static bool Enabled { get; private set; }

	public static void Init()
	{
		Enabled = ExperimentalConfig.Read(ConfigKey, LogTag);
		try
		{
			Harmony harmony = new Harmony("com.github.3dmxm.ScriptTrainer.AlchemyNoCap");
			MethodInfo target = AccessTools.Method(typeof(vo), "ioe");
			MethodInfo prefix = AccessTools.Method(typeof(AlchemyNoCap), nameof(Prefix_BatchAlchemy));
			if (target == null || prefix == null)
			{
				throw new MissingMethodException("vo.ioe / Prefix_BatchAlchemy");
			}
			harmony.Patch(target, new HarmonyMethod(prefix));
			patched = true;
			TrainerLog.Write(LogTag + " patch applied on vo.ioe, enabled=" + Enabled);
		}
		catch (Exception ex)
		{
			TrainerLog.Write(LogTag + " patch failed: " + ex);
		}
	}

	// ALCHEMY_NOCAP|1 / 0 / ?
	public static string Command(string arg)
	{
		arg = (arg ?? string.Empty).Trim().ToLowerInvariant();
		if (arg == "1" || arg == "on" || arg == "true")
		{
			SetEnabled(true);
		}
		else if (arg == "0" || arg == "off" || arg == "false")
		{
			SetEnabled(false);
		}
		string state = Enabled ? "已开启" : "已关闭";
		if (!patched)
		{
			return "全部炼成无上限 " + state + "（但补丁未装上，功能无效，详见 ScriptTrainer.log）";
		}
		return "全部炼成无上限 " + state + "：勾选「全部炼成」后按背包材料能炼几次就炼几次，不再截到 50 次";
	}

	private static void SetEnabled(bool value)
	{
		Enabled = value;
		ExperimentalConfig.Write(ConfigKey, value, LogTag);
		TrainerLog.Write(LogTag + " enabled=" + value);
	}

	// Harmony 前缀：vo.ioe(List<zb> a, int b)
	public static void Prefix_BatchAlchemy(vo __instance, Il2CppSystem.Collections.Generic.List<zb> a, ref int b)
	{
		try
		{
			if (!Enabled || __instance == null || a == null || b != GameCap)
			{
				return;
			}
			// 只认「全部炼成」路径：zar 开且 zak（十连）关；其他路径传 50 纯属巧合，不动
			if (!__instance.inf() || __instance.ine())
			{
				return;
			}
			int real = __instance.iop(a);
			if (real <= b)
			{
				return;
			}
			b = real;
			__instance.zas = real;
			TrainerLog.Write(LogTag + " 全部炼成: 50 -> " + real + " 次");
		}
		catch (Exception ex)
		{
			TrainerLog.Write(LogTag + " Prefix_BatchAlchemy error: " + ex);
		}
	}
}
