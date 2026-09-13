# 犹格索托斯的庭院 外置修改器（IL2CPP 版）

《Yog-Sothoth's Yard / 犹格索托斯的庭院》的外置修改器，三件套架构：

- **`ScriptTrainer/`** — BepInEx 6 IL2CPP 插件：属性修改、物品添加、至高遗物创建、物品表/图标导出、全数据表导出（`EXPORT_TABLES` 命令，签名自动发现全部表管理器并让游戏自行解密，供剧情文本等数据挖掘）、游戏内热键
- **`ScriptTrainer.UI/`** — 外部 WinForms 界面（独立进程），通过文件 IPC（`BepInEx\ScriptTrainer.commands` / `.responses`）与插件通信；界面用游戏原画换肤，支持 20 张 CG 背景切换、透明物品表格、无边框拖拽/缩放（CG 由插件首次随游戏启动时自动提取到 `BepInEx\ui_backgrounds\`，仓库与发布包均不含游戏原画）
- **`tools/MetaDump/`** — interop 程序集成员表导出器，游戏更新打乱混淆名后重新对表用

衍生自 [GlossMod/UnityScriptTrainer](https://github.com/GlossMod/UnityScriptTrainer)（MIT）的犹格庭院内置修改器：
游戏本体转为 IL2CPP 构建后，原 BepInEx 5 (Mono) 内置方案不再适用，本仓库将其重构为上述外置方案。

## 下载

到 [Releases](../../releases) 页面下载打包好的 `ScriptTrainer_Package_*.zip`。包内含 **BepInEx 6 IL2CPP 本体
（6.0.0-be.697，含 dotnet 运行时）**、编译好的插件 DLL、外部 UI 和使用说明——整包解压到游戏根目录即可，
无需自行编译，也无需（更不容易装错版本地）单独安装 BepInEx。详细步骤见包内 `使用说明.txt`。

## 版本兼容性（重要）

**当前版本只适配 Steam build 24329824（游戏 2026-07-23 更新）。**

游戏是 IL2CPP + 混淆构建，每次更新都可能整体打乱内部混淆名。旧版修改器装在新版游戏上会每帧报
`MissingMethodException`，完全失效。

- 查游戏版本：`steamapps\appmanifest_2194530.acf` 里的 `"buildid"`
- 全部反射用混淆名集中在 [`ScriptTrainer/ObfNames.cs`](ScriptTrainer/ObfNames.cs)，硬绑定成员清单在该文件底部注释
- 插件内置熔断：名称失配时只停用受影响功能并写日志，不会每帧刷错误拖垮游戏

## 编译

前置条件：

1. 游戏已安装 [BepInEx 6 IL2CPP 版](https://builds.bepinex.dev/projects/bepinex_be)（本版在 6.0.0-be.697 上验证）
2. 游戏用 BepInEx 至少启动过一次，生成 `BepInEx\interop\`（插件直接引用这些 interop 程序集，无法用 NuGet 替代——这也是插件与游戏版本强绑定的原因）
3. .NET SDK 6.0 或更高

```
dotnet build ScriptTrainer -c Release -p:GameDir="<你的游戏目录>"
```

也可以直接改 `ScriptTrainer/ScriptTrainer.csproj` 里的 `GameDir` 属性。
产物 `ScriptTrainer.dll` 复制到 `游戏目录\BepInEx\plugins\`。

外部界面：

```
dotnet build ScriptTrainer.UI -c Release
```

产物 `ScriptTrainer.UI.exe`（net40 WinForms）放游戏根目录，游戏进存档后双击运行。

## 使用

详见 [使用说明.txt](使用说明.txt)。简述：启动游戏并进入存档 → 运行外部 UI → 常用功能页改属性，
获取物品页搜索/添加物品；至高遗物（22000+）走游戏原生创建逻辑。游戏内热键（Home/F1-F12 等）见说明。

普通神谕（E_Relic，ID 20000-21999，含[特]系列）**已被主动屏蔽**：这批物品没有安全的添加通道，
直接塞进背包会损坏存档，因此列表不显示、按 ID 添加（含游戏内热键）也会被插件拒绝。

「实验性功能」页（默认关闭，需手动开启）：

- **防回归**：白天/夜晚切换时若 SAN 为 0，不再触发回归结局，改为恢复 10 点 SAN 并用游戏原生剧情界面
  弹出一段耶芙娜的对话（Harmony 前缀 `ez.dnx`；对话剧本是运行时在内存里生成的 protobuf 剧本资产，
  经 `Plot.Level.nfa` 前缀喂给原生加载链）。开关持久化在 `BepInEx\ScriptTrainer.experimental.cfg`。
- **替换旧版神谕贴图**：把目录里的 `Relic*.png` 在运行时灌回游戏已加载的 `Texture2D`，仅内存、重启恢复。

## 游戏更新后的重适配流程

1. 导出新版成员表：
   `dotnet run --project tools/MetaDump -- "<游戏目录>\BepInEx\interop\Assembly-CSharp.dll" new.txt`
2. 按未混淆锚点重新对表：`E_ActionPiont`/`E_ExtraIncrease`/`E_SeniorRelic` 枚举成员、
   `RelicEffectSaveParam` 签名、`hl.Load()`/`GetItem(Int32)`、签名唯一性（如"全程序集唯一的 Example.nl 型属性"）
3. 改 `ObfNames.cs` 反射名 + `TrainerBehaviour.cs` 硬绑定成员（清单在 ObfNames.cs 底部注释）
4. 编译即校验（编译器会检查全部硬绑定成员的存在性），进游戏冒烟：0 异常、钩子全挂上、物品表导出、IPC 应答

两个坑：旧名字在新版里常被**别的成员**占用，字符串反射不改名会静默绑错而不是报错；
名称偏移量只是巧合不是规律，必须逐个锚点验证。

## 说明与许可

- `ScriptTrainer.UI/` 源码为反编译恢复版（原始源码遗失），此后的界面换肤等开发直接在其上进行，Release 包内 exe 即由本源码编译
- 本仓库按 MIT 发布；[LICENSE](LICENSE) 保留上游 Gloss Mod组 的原始版权声明，特此致谢
