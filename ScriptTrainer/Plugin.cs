using BepInEx;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;

namespace ScriptTrainer;

[BepInPlugin("com.github.3dmxm.ScriptTrainer", "ScriptTrainer", "1.0.0")]
public class Plugin : BasePlugin
{
	public override void Load()
	{
		TrackerPatches.Init();
		ClassInjector.RegisterTypeInIl2Cpp<TrainerBehaviour>();
		AddComponent<TrainerBehaviour>();
		TrainerLog.Write("ScriptTrainer loaded for IL2CPP.");
		base.Log.LogInfo("ScriptTrainer loaded for IL2CPP. Use the external UI. Results are written to BepInEx/ScriptTrainer.log.");
	}
}
