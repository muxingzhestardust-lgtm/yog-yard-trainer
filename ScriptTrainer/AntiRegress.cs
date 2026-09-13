using System;
using System.IO;
using System.Reflection;
using Example;
using HarmonyLib;
using HotelModule;
using HotelModule.Event;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace ScriptTrainer;

// ============================================================================
// 实验性功能：防回归
//
// 游戏里"回归"= SAN 归零结局（NpcEvent "San0Ending"）。它唯一的触发点是
//   ez.dnx()  ——  在回合状态机 rv.iaw（时段切换：白天→夜晚 / 过夜→白天）里被调用；
//                 逻辑：若 vqg 标记为真（SAN 曾归零）→ 清标记 → 若当前 SAN <= 0
//                 → 入队 San0Ending 事件并返回 true（调用方随即跳过本时段其余处理）。
//
// 本功能用 Harmony 前缀替换 dnx：条件满足时不入队结局，而是
//   1) 给玩家 +10 SAN；
//   2) 往 Conversation / LanguageTalk / NpcEvent 三张运行时表里注入自定义剧情行，
//      通过游戏自己的事件队列 (sr.ifp) 播放一段耶芙娜对话——用的是原生剧情 UI。
//      注意：E_PlayStory 播放的不是表行，而是 Resources 里 Story/<id> 的 protobuf
//      剧本资产（Plot.Level），Conversation 只被剧本里的 Say 节点引用。所以这里再
//      Harmony 前缀 Plot.Level.nfa：路径落在我们的 id 段时，直接用内存里拼好的
//      protobuf（Entry > Sequence > ParallelComplete > [Say, SetImage]，与官方
//      Story/1021105 字节级同构）构造 Level，立绘走 SetImage 节点里的 UnitBase id
//      （2103 系列 ExpressionAnim）；
//   3) 返回 false，让时段切换照常继续。
//
// 台词与立绘取自 sprite_lines_san0.md，每次随机一条（不与上次重复）。
// 开关持久化在 BepInEx\ScriptTrainer.experimental.cfg（anti_regress=1/0）。
// ============================================================================
internal static class AntiRegress
{
	private const string ConfigKey = "anti_regress";

	private const long SanRestore = 10;

	// 自定义表行 ID（游戏表里这些段位都是空的，见 tables/*.tsv）
	private const int EventIdBase = 6990001;       // NpcEvent
	private const int ConvIdBase = 9990001;        // Conversation / LanguageTalk（同号）
	private const int SpeakerTalkId = 21031;       // LanguageTalk 21031 = 耶芙娜（名字行）

	// 立绘对应的 UnitBase id（UnitBase.xxa = ExpressionAnim/2103/ExpressionAnim2103_x）| 台词
	// 2103→2103, 2103_2→2121, 2103_8→2237, 2103_10→2244, 2103_13→2267, 2103_19→2273, 2103_21→2279
	private static readonly (int unit, string sprite, string text)[] Lines = new (int, string, string)[]
	{
		(2103, "2103", "哼，这副脸色还想一个人熬到天亮？过来，伟大的红龙女王亲自抱你，算你走运哟~"),
		(2121, "2103_2", "哇，老板你的眼神都空掉了！别走别走，先被我抱一下，抱完再说！"),
		(2237, "2103_8", "啊？！你、你刚才是不是想直接从我旁边走过去？！站住，亲一口再走！"),
		(2244, "2103_10", "呵呵~想溜？我可是龙哦，你迈一步我就扑一步，还是乖乖让我亲一下省事啦~"),
		(2267, "2103_13", "你现在这个样子，走出这扇门就回不来了。过来，我抱着你，一直到你睡着。"),
		(2273, "2103_19", "……别一个人走。我不会尖叫着逃跑的……所以，让我抱一下，就一下。"),
		(2279, "2103_21", "唔……我就知道你会这样。哼，站着别动，亲一下，然后跟我回去。")
	};

	private static readonly System.Random rng = new System.Random();

	private static int lastLine = -1;

	private static bool patched;

	private static bool tablesInjected;

	public static bool Enabled { get; private set; }

	private static string ConfigPath => Path.Combine(BepInEx.Paths.BepInExRootPath, "ScriptTrainer.experimental.cfg");

	// ------------------------------------------------------------------
	// 初始化：读开关 + 装补丁（Plugin.Load 时调用）
	// ------------------------------------------------------------------
	public static void Init()
	{
		Enabled = ReadConfig();
		try
		{
			Harmony harmony = new Harmony("com.github.3dmxm.ScriptTrainer.AntiRegress");
			MethodInfo target = AccessTools.Method(typeof(ez), "dnx");
			MethodInfo prefix = AccessTools.Method(typeof(AntiRegress), nameof(Prefix_SanEnding));
			if (target == null || prefix == null)
			{
				throw new MissingMethodException("ez.dnx / Prefix_SanEnding");
			}
			harmony.Patch(target, new HarmonyMethod(prefix));
			MethodInfo loadTarget = AccessTools.Method(typeof(Plot.Level), "nfa");
			MethodInfo loadPrefix = AccessTools.Method(typeof(AntiRegress), nameof(Prefix_LevelLoad));
			if (loadTarget == null || loadPrefix == null)
			{
				throw new MissingMethodException("Plot.Level.nfa / Prefix_LevelLoad");
			}
			harmony.Patch(loadTarget, new HarmonyMethod(loadPrefix));
			patched = true;
			TrainerLog.Write("[ANTIREG] patches applied on ez.dnx + Plot.Level.nfa, enabled=" + Enabled);
		}
		catch (Exception ex)
		{
			TrainerLog.Write("[ANTIREG] patch failed: " + ex);
		}
	}

	// ------------------------------------------------------------------
	// 开关：ANTI_REGRESS|1 / 0 / ? 命令入口
	// ------------------------------------------------------------------
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
			return "防回归 " + state + "（但补丁未装上，功能无效，详见 ScriptTrainer.log）";
		}
		return "防回归 " + state + "：时段切换时若 SAN 为 0，改为恢复 10 点 SAN 并触发耶芙娜对话" + StatusSuffix();
	}

	// 调试：ANTI_REGRESS|test 直接注入并播放一条对话（不改 SAN）
	public static string ShowTestDialog()
	{
		try
		{
			if (!InjectTables())
			{
				return "表注入失败，见 ScriptTrainer.log";
			}
			int idx = PickLine();
			sr queue = sr.bhfx;
			if (queue == null)
			{
				return "事件队列未就绪（需进入存档）";
			}
			queue.ifp(EventIdBase + idx);
			queue.ifn();
			return "已入队测试对话 #" + idx + " (" + Lines[idx].sprite + ")" + StatusSuffix();
		}
		catch (Exception ex)
		{
			TrainerLog.Write("[ANTIREG] test failed: " + ex);
			return "测试失败: " + ex.Message;
		}
	}

	private static string StatusSuffix()
	{
		try
		{
			rp hotel = rp.bhfn;
			rm attrs = rm.bhfm;
			ez san = ez.bgto;
			if (hotel == null || attrs == null)
			{
				return string.Empty;
			}
			GameTime time = hotel.hzr();
			string flag = (san != null) ? san.vqg.ToString() : "?";
			return " [day=" + time.Day + " round=" + time.RoundState + " time=" + time.TimeListETime + " san=" + attrs.hxn(lp.E_San) + " zeroFlag=" + flag + "]";
		}
		catch (Exception ex)
		{
			return " [状态读取失败: " + ex.Message + "]";
		}
	}

	private static void SetEnabled(bool value)
	{
		Enabled = value;
		WriteConfig(value);
		TrainerLog.Write("[ANTIREG] enabled=" + value);
	}

	private static bool ReadConfig()
	{
		try
		{
			if (!File.Exists(ConfigPath))
			{
				return false;
			}
			foreach (string raw in File.ReadAllLines(ConfigPath))
			{
				string line = raw.Trim();
				int eq = line.IndexOf('=');
				if (eq <= 0 || line.StartsWith("#"))
				{
					continue;
				}
				if (string.Equals(line.Substring(0, eq).Trim(), ConfigKey, StringComparison.OrdinalIgnoreCase))
				{
					string v = line.Substring(eq + 1).Trim();
					return v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
				}
			}
		}
		catch (Exception ex)
		{
			TrainerLog.Write("[ANTIREG] read config failed: " + ex.Message);
		}
		return false;
	}

	private static void WriteConfig(bool value)
	{
		try
		{
			System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();
			bool replaced = false;
			if (File.Exists(ConfigPath))
			{
				foreach (string raw in File.ReadAllLines(ConfigPath))
				{
					string line = raw.Trim();
					int eq = line.IndexOf('=');
					if (eq > 0 && string.Equals(line.Substring(0, eq).Trim(), ConfigKey, StringComparison.OrdinalIgnoreCase))
					{
						if (!replaced)
						{
							lines.Add(ConfigKey + "=" + (value ? "1" : "0"));
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
				lines.Add(ConfigKey + "=" + (value ? "1" : "0"));
			}
			File.WriteAllLines(ConfigPath, lines.ToArray());
		}
		catch (Exception ex)
		{
			TrainerLog.Write("[ANTIREG] write config failed: " + ex.Message);
		}
	}

	// ------------------------------------------------------------------
	// Harmony 前缀：ez.dnx()  bool
	//   原逻辑：if (!vqg) return false; vqg=false; eg.dek().dfi();
	//           if (rm.hxn(E_San) > 0) return false;
	//           sr.bhfx.ifp(GlobalParamString["San0Ending"]); return true;
	//   替换：条件成立时 +10 SAN、入队自定义对话、返回 false（时段切换照常）。
	// ------------------------------------------------------------------
	public static bool Prefix_SanEnding(ez __instance, ref bool __result)
	{
		try
		{
			if (!Enabled || __instance == null || !__instance.vqg)
			{
				return true;
			}
			rm attrs = rm.bhfm;
			if (attrs == null)
			{
				return true;
			}
			long san = attrs.hxn(lp.E_San);
			if (san > 0)
			{
				return true;
			}
			// 与原方法一致：消费掉"曾归零"标记
			__instance.vqg = false;

			attrs.hxp(lp.E_San, ne.E_ExtraIncrease, SanRestore);
			long after = attrs.hxn(lp.E_San);

			string where = string.Empty;
			try
			{
				GameTime time = rp.bhfn.hzr();
				where = "day=" + time.Day + " round=" + time.RoundState + " time=" + time.TimeListETime;
			}
			catch
			{
			}

			int idx = -1;
			if (InjectTables())
			{
				idx = PickLine();
				sr queue = sr.bhfx;
				if (queue != null)
				{
					queue.ifp(EventIdBase + idx);
				}
			}
			TrainerLog.Write("[ANTIREG] blocked San0Ending (" + where + ") san " + san + "->" + after + ", dialog #" + idx);
			__result = false;
			return false;
		}
		catch (Exception ex)
		{
			TrainerLog.Write("[ANTIREG] Prefix_SanEnding error: " + ex);
			return true;
		}
	}

	private static int PickLine()
	{
		int idx;
		do
		{
			idx = rng.Next(Lines.Length);
		}
		while (Lines.Length > 1 && idx == lastLine);
		lastLine = idx;
		return idx;
	}

	// ------------------------------------------------------------------
	// 运行时表注入（只做一次；表管理器 gr/hn/hq 的静态数据 + id→索引字典）
	// ------------------------------------------------------------------
	private static bool InjectTables()
	{
		if (tablesInjected)
		{
			return true;
		}
		try
		{
			mi convTable = gr.vyd;
			ns talkTable = hn.war;
			nw eventTable = hq.wba;
			Il2CppSystem.Collections.Generic.Dictionary<long, int> convIndex = gr.vye;
			Il2CppSystem.Collections.Generic.Dictionary<long, int> talkIndex = hn.was;
			Il2CppSystem.Collections.Generic.Dictionary<long, int> eventIndex = hq.wbb;
			if (convTable == null || talkTable == null || eventTable == null || convIndex == null || talkIndex == null || eventIndex == null)
			{
				TrainerLog.Write("[ANTIREG] tables not loaded yet");
				return false;
			}
			// 已存在（例如热重载）就直接复用
			if (eventIndex.ContainsKey(EventIdBase))
			{
				tablesInjected = true;
				return true;
			}
			for (int i = 0; i < Lines.Length; i++)
			{
				int convId = ConvIdBase + i;
				int eventId = EventIdBase + i;
				string text = Lines[i].text;

				LanguageTalk talk = new LanguageTalk();
				talk.xmm = convId;
				talk.xmn = text;
				talk.xmo = 0;
				talk.xmp = text;
				talk.xmq = text;
				talk.xmr = text;
				talk.xms = text;
				talkIndex[convId] = talkTable.xmu.Count;
				talkTable.xmu.Add(talk);

				Conversation conv = new Conversation();
				conv.xeq = convId;
				conv.xer = SpeakerTalkId;
				conv.xes = 1;
				conv.xet = 0;
				conv.xeu = new Il2CppSystem.Collections.Generic.List<int>();
				conv.xev = new Il2CppSystem.Collections.Generic.List<int>();
				conv.xew = Lines[i].unit;
				conv.xex = new Il2CppSystem.Collections.Generic.List<int>();
				conv.xey = 0;
				conv.xez = new Il2CppSystem.Collections.Generic.List<int>();
				conv.xfa = mj.E_CharacterB;
				convIndex[convId] = convTable.xfc.Count;
				convTable.xfc.Add(conv);

				NpcEvent evt = new NpcEvent();
				evt.xng = eventId;
				evt.xnh = nx.E_PlayStory;
				evt.xni = string.Empty;
				evt.xnj = convId;
				evt.xnk = 0;
				evt.xnl = 0;
				evt.xnm = string.Empty;
				evt.xnn = string.Empty;
				evt.xno = new Il2CppSystem.Collections.Generic.List<int>();
				evt.xnp = new Il2CppSystem.Collections.Generic.List<int>();
				evt.xnq = 0;
				evt.xnr = 0;
				evt.xns = 0;
				eventIndex[eventId] = eventTable.xnu.Count;
				eventTable.xnu.Add(evt);
			}
			// 自检：走游戏自己的 GetItem 查一遍
			Conversation check = gr.bgux?.GetItem(ConvIdBase);
			NpcEvent checkEvt = hq.bgvw?.GetItem(EventIdBase);
			LanguageTalk checkTalk = hn.bgvt?.GetItem(ConvIdBase);
			if (check == null || checkEvt == null || checkTalk == null)
			{
				TrainerLog.Write("[ANTIREG] inject self-check failed conv=" + (check != null) + " evt=" + (checkEvt != null) + " talk=" + (checkTalk != null));
				return false;
			}
			tablesInjected = true;
			TrainerLog.Write("[ANTIREG] injected " + Lines.Length + " lines: conv " + ConvIdBase + ".., event " + EventIdBase + "..");
			return true;
		}
		catch (Exception ex)
		{
			TrainerLog.Write("[ANTIREG] inject tables failed: " + ex);
			return false;
		}
	}

	// ------------------------------------------------------------------
	// Harmony 前缀：Plot.Level.nfa(string path, Action<Level> cb)
	//   官方流程：bmo.nge(storyId) → bi.cnd(id 字符串 → storyMap 路径，未登记则原样返回)
	//             → bmp.ngw(path, cb) → new Level().nfa(path, cb)
	//             → dg.Load<TextAsset>(path).bytes → Level.bmi.nes(bytes)：
	//                bjs data = bhi.lrx<bjs>(bytes); Task t = bmp.ngv(data.bdnk);
	//                level.bdzg = t as EntryTask; 拷贝 bdnj/bdnn/bdno/bdnp/bdnq; bdzm = true; cb(level)
	//   我们的 id 没有资产，dg.Load 返回 null → 静默什么都不播。这里在 nfa 入口拦下
	//   自己的 id，用内存 protobuf 复刻 nes 的全部工作后直接回调。
	// ------------------------------------------------------------------
	public static bool Prefix_LevelLoad(Plot.Level __instance, string a, Il2CppSystem.Action<Plot.Level> b)
	{
		int idx;
		if (!TryGetLineIndex(a, out idx))
		{
			return true;
		}
		try
		{
			int storyId = ConvIdBase + idx;
			byte[] bytes = StoryProto.BuildOneLiner(storyId, Lines[idx].unit.ToString());
			Plot.bjs data = Plot.bjs.mkv((Il2CppStructArray<byte>)bytes);
			if (data == null || data.bdnk == null)
			{
				TrainerLog.Write("[ANTIREG] story deserialize failed for " + a);
				return true;
			}
			Plot.Task task = Plot.bmp.ngv(data.bdnk);
			Plot.EntryTask entry = task?.TryCast<Plot.EntryTask>();
			if (entry == null)
			{
				TrainerLog.Write("[ANTIREG] story root is not EntryTask for " + a + " (task=" + (task != null) + ")");
				return true;
			}
			__instance.bdzg = entry;
			__instance.bdzh = data.bdnj;
			__instance.bdzi = data.bdnn;
			__instance.bdzj = data.bdno;
			__instance.bdzk = data.bdnp;
			__instance.bdzl = data.bdnq;
			__instance.bdzm = true;
			TrainerLog.Write("[ANTIREG] served in-memory story " + storyId + " (" + Lines[idx].sprite + ", " + bytes.Length + " bytes)");
			b?.Invoke(__instance);
			return false;
		}
		catch (Exception ex)
		{
			TrainerLog.Write("[ANTIREG] Prefix_LevelLoad error: " + ex);
			return true;
		}
	}

	private static bool TryGetLineIndex(string path, out int idx)
	{
		idx = -1;
		if (string.IsNullOrEmpty(path))
		{
			return false;
		}
		string s = path;
		int slash = s.LastIndexOf('/');
		if (slash >= 0)
		{
			s = s.Substring(slash + 1);
		}
		int id;
		if (!int.TryParse(s, out id))
		{
			return false;
		}
		id -= ConvIdBase;
		if (id < 0 || id >= Lines.Length)
		{
			return false;
		}
		idx = id;
		return true;
	}
}

// ============================================================================
// 手写 protobuf 编码：生成一段"一句话 + 一张立绘"的剧本（Plot.bjs 的线格式）。
// 结构与官方 Story/1021105 完全一致（已在 Python 里逐字节比对通过）：
//   bjs { 1:0, 2:bix(Entry), 4:16, 5:0, 6:1, 7:0, 8:1 }
//   bix { 1:id, 2:type(blr), 3:payload, 4:delay(float) }
//   Entry payload (bjl)      { 1:child bix..., 2:0, 3:0f, 4:0, 5:1, 6:0, 7:0 }
//   Sequence/ParallelComplete payload { 1:child bix..., 2:0f }
//   BackGround payload (bje) { 4:"Bg_Alchemy", 其余零值 }   （官方 Story/1021201 同款）
//   Say payload (bki)        { 4:conversationId, 5:20f, 8:1, 11:1, 其余零值 }
//   SetImage payload (bkm)   { 3:slot0, 6:0.5f, 7:6, 10:"<UnitBase id>", 其余零值 }
// ============================================================================
internal static class StoryProto
{
	private const int TypeEntry = 0;
	private const int TypeParallelComplete = 4;
	private const int TypeSequence = 6;
	private const int TypeSay = 13;
	private const int TypeBackGround = 16;
	private const int TypeSetImage = 21;

	// 对话背景图（游戏内置资源名，与官方炼金房剧情一致）
	public const string Background = "Bg_Alchemy";

	private static readonly byte[] Vec2Zero = Hex("0d000000001500000000");
	private static readonly byte[] Vec3Zero = Hex("0d0000000015000000001d00000000");
	private static readonly byte[] Vec4Zero = Hex("0d0000000015000000001d000000002500000000");

	public static byte[] BuildOneLiner(int conversationId, string unitId)
	{
		byte[] bg = Bix(8, TypeBackGround, BackGroundPayload(Background));
		byte[] bgPar = Bix(9, TypeParallelComplete, ParentPayload(bg));
		byte[] say = Bix(4, TypeSay, SayPayload(conversationId));
		byte[] img = Bix(15, TypeSetImage, SetImagePayload(unitId));
		byte[] par = Bix(3, TypeParallelComplete, ParentPayload(say, img));
		byte[] seq = Bix(2, TypeSequence, ParentPayload(bgPar, par));
		byte[] entry = Bix(1, TypeEntry, EntryPayload(seq));
		Pb p = new Pb();
		p.V(1, 0).B(2, entry).V(4, 16).V(5, 0).V(6, 1).V(7, 0).V(8, 1);
		return p.ToArray();
	}

	private static byte[] BackGroundPayload(string name)
	{
		Pb p = new Pb();
		p.V(1, 0).F(2, 0f).V(3, 0).S(4, name).F(5, 0f).V(6, 0).V(7, 0).V(8, 0);
		return p.ToArray();
	}

	private static byte[] SayPayload(int conversationId)
	{
		Pb p = new Pb();
		p.V(1, 0).F(2, 0f).V(3, 0).V(4, conversationId).F(5, 20f).V(8, 1).V(9, 0).V(10, 0).V(11, 1)
			.V(13, 0).V(14, 0).V(15, 0).F(16, 0f).V(17, 0).V(18, 0).V(19, 0).B(20, Vec4Zero)
			.V(21, 0).V(22, 0).V(23, 0).V(24, 0).V(25, 0).B(26, Vec2Zero).B(27, Vec2Zero).V(28, 0);
		return p.ToArray();
	}

	private static byte[] SetImagePayload(string unitId)
	{
		Pb p = new Pb();
		p.V(1, 0).F(2, 0f).V(3, 0).V(4, 0).V(5, 0).F(6, 0.5f).V(7, 6).V(8, 0).B(9, Vec3Zero).S(10, unitId)
			.V(11, 0).V(12, 0).V(13, 0).B(14, Vec4Zero).B(15, Vec4Zero).F(16, 0f).V(17, 0).V(18, 0).V(19, 0)
			.B(20, Vec4Zero).V(21, 0).B(22, Vec4Zero);
		return p.ToArray();
	}

	private static byte[] Bix(int id, int type, byte[] payload)
	{
		Pb p = new Pb();
		p.V(1, id).V(2, type).B(3, payload).F(4, 0f);
		return p.ToArray();
	}

	private static byte[] ParentPayload(params byte[][] children)
	{
		Pb p = new Pb();
		foreach (byte[] c in children)
		{
			p.B(1, c);
		}
		p.F(2, 0f);
		return p.ToArray();
	}

	private static byte[] EntryPayload(params byte[][] children)
	{
		Pb p = new Pb();
		foreach (byte[] c in children)
		{
			p.B(1, c);
		}
		p.V(2, 0).F(3, 0f).V(4, 0).V(5, 1).V(6, 0).V(7, 0);
		return p.ToArray();
	}

	private static byte[] Hex(string hex)
	{
		byte[] r = new byte[hex.Length / 2];
		for (int i = 0; i < r.Length; i++)
		{
			r[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
		}
		return r;
	}

	private sealed class Pb
	{
		private readonly MemoryStream ms = new MemoryStream();

		public byte[] ToArray()
		{
			return ms.ToArray();
		}

		private void Varint(ulong v)
		{
			while (v >= 0x80)
			{
				ms.WriteByte((byte)(v | 0x80));
				v >>= 7;
			}
			ms.WriteByte((byte)v);
		}

		private void Tag(int field, int wire)
		{
			Varint((ulong)((field << 3) | wire));
		}

		public Pb V(int field, long value)
		{
			Tag(field, 0);
			Varint((ulong)value);
			return this;
		}

		public Pb F(int field, float value)
		{
			Tag(field, 5);
			byte[] b = BitConverter.GetBytes(value);
			ms.Write(b, 0, 4);
			return this;
		}

		public Pb B(int field, byte[] data)
		{
			Tag(field, 2);
			Varint((ulong)data.Length);
			ms.Write(data, 0, data.Length);
			return this;
		}

		public Pb S(int field, string text)
		{
			return B(field, System.Text.Encoding.UTF8.GetBytes(text));
		}
	}
}
