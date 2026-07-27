namespace ScriptTrainer;

// ============================================================================
// 混淆名总表 —— 对应游戏 Steam build 24329824（2026-07-23 更新）。
// 游戏更新导致改名时：用本目录 tools/MetaDump 重新导出
// BepInEx\interop\Assembly-CSharp.dll 的成员表，按锚点重新对表：
//   锚点: E_ActionPiont/E_ExtraIncrease/E_SeniorRelic 枚举成员、
//         RelicEffectSaveParam 签名、hl.Load()/GetItem、qo 的 (int,int,bool)。
// 本文件只放"反射字符串"用名；TrainerBehaviour.cs 里还有一批硬绑定成员，
// 清单见文件底部注释。
// ============================================================================
internal static class ObfNames
{
	public const string AssemblySuffix = ", Assembly-CSharp";

	// —— 至高遗物管理器 eg ——（类名本次未变）
	public const string EgType = "eg";
	public const string EgGetInstance = "dek";  // 旧 dej: static eg dek()
	public const string EgCreateRelic = "dez";  // 旧 dey: ec dez(int, int, RelicEffectSaveParam)
	public const string EcType = "ec";          // 遗物实例类；ctor(int,int) 未变
	public const string EgRelicDict = "vmh";    // 旧 vmc: Dictionary<int, ec>
	public const string EgListA = "vmi";        // 旧 vmd: List<int>
	public const string EgListB = "vmj";        // 旧 vme: List<int>
	public const string EgListC = "vmr";        // 旧 vmm: List<int>
	public const string EgListD = "vms";        // 旧 vmn: List<int>
	public const string EgListE = "vmx";        // 旧 vms: List<int>

	// —— Tracker 钩子（都在 eg / qo 上）——
	public const string TrkRelicFlowA = "dew";  // 旧 dev: bool dew(int, bool, bool, Action, bool)
	public const string TrkRelicFlowB = "dev";  // 旧 deu: void dev(int, int, Action, bool, bool, bool)
	public const string TrkRelicSave = "dfa";   // 旧 dez: void dfa(int, RelicEffectSaveParam)

	// —— 背包管理器 BagSystem.qo ——（类名本次未变）
	public const string QoType = "BagSystem.qo";
	public const string QoAddItem = "hmz";      // 旧 hmy: void hmz(int id, int count, bool notify)

	// —— 物品名称查找（导出 CSV 用，全部软失败）——
	public const string NameTypeA = "cd";
	public const string NameByIdA = "cpk";      // 本次未变: static string cpk(int)
	public const string NameTypeB = "cy";
	public const string NameByIdB = "cun";      // 旧 cum: static string cun(int)
	public const string NameByIdC = "cus";      // 旧 cur: static string cus(int)

	// —— 物品图标查找（导出 PNG 用，全部软失败）——
	public const string SpriteTypeB = "cy";
	public const string SpriteByPathB = "cup";  // 旧 cuo: static Sprite cup(string)
	public const string SpriteTypeCn = "cn";
	public const string SpriteByPathCn = "ctr"; // 旧 ctq: static Sprite ctr(string)
	public const string SpriteByIdB = "cuq";    // 旧 cup: static Sprite cuq(int)
}

// ============================================================================
// TrainerBehaviour.cs 中的硬绑定成员（编译期锁死，更新后要在代码里改）：
//   HotelModule.rm : bhfm静态属性=单例(getter方法hxi, 旧hxh()) / hxn(lp)读值(旧hxm)
//                    / hxp(lp,ne,long)改值(旧hxo)
//   BagSystem.qo   : bheq静态属性=单例(getter方法hmt, 旧hms()) / hnj(int)查物品(旧hni)
//                    / hmz(int,int,bool)加物品(旧hmy)
//   hl             : Load()未变 / GetItem(int)未变 / bgvr静态属性=单例(getter方法dzg, 旧dzf())
//                    / dzh()取表(旧dzg) / wal静态表属性(旧wag)
//   Example.nl     : xlu = List<Item>（旧 xlp）
//   Example.Item   : xld=ID(旧xky) xle=NameID(旧xkz) xlg=类型枚举(旧xlb)
//                    xli=子类型(旧xld) xlq=图标串(旧xll)
//                    杂项列: xlf(旧xla) xlh(旧xlc) xlj(旧xle) xll(旧xlg) xln(旧xli) xlr(旧xlm)
//   Example.lp / Example.ne 枚举类型与成员（E_Money 等）本次全部未变。
//   注意：本次碰巧呈现"方法≈+1、字段≈+5"的位移，但 cd.cpk 未移、eg 序列有空洞，
//   偏移量只是结果不是规律；未来重对表必须按签名/类型锚点逐个验证，
//   还要提防旧名字在新版里被"别的成员"占用（如新 hmy/hni/hxm 都另有其人）。
// ============================================================================
