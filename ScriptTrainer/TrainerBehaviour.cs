using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using BagSystem;
using BepInEx;
using Example;
using HotelModule;
using Il2CppSystem.Collections.Generic;
using SaveLoadSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScriptTrainer;

public class TrainerBehaviour : MonoBehaviour
{
	private bool showWindow = true;

	private bool uiFailed;

	private GameObject? canvas;

	private Text? statusText;

	private InputField? amountInput;

	private InputField? itemIdInput;

	private InputField? itemCountInput;

	private long amount = 10000L;

	private int itemId = 10013;

	private int itemCount = 1;

	private float nextExportAttemptTime = 5f;

	private float nextCommandPollTime;

	private bool itemListExported;

	private bool exportBroken;

	private int consecutiveUpdateErrors;

	private bool updateDisabled;

	private string lastMessage = "ScriptTrainer loaded. Use external UI.";

	public TrainerBehaviour(IntPtr ptr)
		: base(ptr)
	{
	}

	public void Update()
	{
		if (updateDisabled)
		{
			return;
		}
		try
		{
			PollExternalCommands();
			TryExportItemList(force: false);
			consecutiveUpdateErrors = 0;
		}
		catch (Exception ex)
		{
			consecutiveUpdateErrors++;
			if (consecutiveUpdateErrors == 1)
			{
				TrainerLog.Write("Update error: " + ex);
			}
			if (consecutiveUpdateErrors >= 120)
			{
				updateDisabled = true;
				TrainerLog.Write("Update disabled after repeated errors. 游戏可能又更新改了混淆名，请重新适配修改器。");
			}
		}
	}

	public void OnGUI()
	{
	}

	private void EnsureWindow()
	{
		if (canvas != null || uiFailed)
		{
			return;
		}
		try
		{
			CreateWindow();
			SetCanvasVisible(showWindow);
			SetMessage("UGUI trainer window created. If buttons do not respond, keep using hotkeys.", writeFile: true);
		}
		catch (Exception ex)
		{
			uiFailed = true;
			SetMessage("创建旧版风格窗口失败，已保留热键模式: " + ex, writeFile: true);
		}
	}

	private void CreateWindow()
	{
		EnsureEventSystem();
		canvas = new GameObject("ScriptTrainerCanvas");
		UnityEngine.Object.DontDestroyOnLoad(canvas);
		Canvas obj = canvas.AddComponent<Canvas>();
		obj.renderMode = RenderMode.ScreenSpaceOverlay;
		obj.overrideSorting = true;
		obj.sortingOrder = 32767;
		CanvasScaler canvasScaler = canvas.AddComponent<CanvasScaler>();
		canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		canvasScaler.referenceResolution = new Vector2(1280f, 720f);
		canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
		canvasScaler.matchWidthOrHeight = 0.5f;
		canvas.AddComponent<GraphicRaycaster>();
		GameObject gameObject = CreatePanel(canvas.transform, "ScriptTrainerPanel", new Vector2(760f, 430f), new Vector2(0f, 0f), new Color32(45, 45, 48, 235));
		CreatePanel(gameObject.transform, "TitleBar", new Vector2(740f, 34f), new Vector2(0f, 188f), new Color32(30, 30, 30, byte.MaxValue));
		CreateText(gameObject.transform, "【犹格索托斯的庭院】内置修改器 IL2CPP", new Vector2(0f, 188f), new Vector2(720f, 30f), 18, TextAnchor.MiddleCenter, Color.white);
		CreateButton(gameObject.transform, "X", new Vector2(365f, 188f), new Vector2(28f, 28f), new Color32(183, 28, 28, byte.MaxValue), delegate
		{
			showWindow = false;
			SetCanvasVisible(visible: false);
		});
		CreateText(gameObject.transform, "数量", new Vector2(-330f, 140f), new Vector2(60f, 28f), 15, TextAnchor.MiddleRight, Color.white);
		amountInput = CreateInput(gameObject.transform, amount.ToString(), new Vector2(-245f, 140f), new Vector2(110f, 30f), delegate(string text)
		{
			amount = Math.Max(1L, ParseLong(text, amount));
			RefreshInputs();
		});
		CreateButton(gameObject.transform, "/10", new Vector2(-160f, 140f), new Vector2(54f, 30f), new Color32(97, 97, 97, byte.MaxValue), delegate
		{
			amount = Math.Max(1L, amount / 10);
			RefreshInputs();
			SetMessage($"Amount={amount}", writeFile: true);
		});
		CreateButton(gameObject.transform, "x10", new Vector2(-98f, 140f), new Vector2(54f, 30f), new Color32(97, 97, 97, byte.MaxValue), delegate
		{
			amount *= 10L;
			RefreshInputs();
			SetMessage($"Amount={amount}", writeFile: true);
		});
		float num = -300f;
		float y = 82f;
		CreateButton(gameObject.transform, "添加现金", new Vector2(num, y), new Vector2(100f, 34f), new Color32(140, 158, byte.MaxValue, byte.MaxValue), delegate
		{
			ModifyAttribute("添加现金", lp.E_Money, ne.E_ExtraIncrease, amount);
		});
		CreateButton(gameObject.transform, "添加 San", new Vector2(num + 112f, y), new Vector2(100f, 34f), new Color32(140, 158, byte.MaxValue, byte.MaxValue), delegate
		{
			ModifyAttribute("添加 San", lp.E_San, ne.E_ExtraIncrease, amount);
		});
		CreateButton(gameObject.transform, "添加灵魂", new Vector2(num + 224f, y), new Vector2(100f, 34f), new Color32(140, 158, byte.MaxValue, byte.MaxValue), delegate
		{
			ModifyAttribute("添加灵魂", lp.E_Souls, ne.E_ExtraIncrease, amount);
		});
		CreateButton(gameObject.transform, "清洁度", new Vector2(num + 336f, y), new Vector2(100f, 34f), new Color32(140, 158, byte.MaxValue, byte.MaxValue), delegate
		{
			ModifyAttribute("添加清洁度", lp.E_Cleanliness, ne.E_ExtraIncrease, amount);
		});
		CreateButton(gameObject.transform, "降低恶值", new Vector2(num + 448f, y), new Vector2(100f, 34f), new Color32(140, 158, byte.MaxValue, byte.MaxValue), delegate
		{
			ModifyAttribute("降低恶值", lp.E_Evil, ne.E_Reduce, amount);
		});
		CreateButton(gameObject.transform, "行动力", new Vector2(num + 560f, y), new Vector2(100f, 34f), new Color32(140, 158, byte.MaxValue, byte.MaxValue), delegate
		{
			ModifyAttribute("添加行动力", lp.E_ActionPiont, ne.E_ExtraIncrease, amount);
		});
		y = 34f;
		CreateButton(gameObject.transform, "耶芙娜", new Vector2(num, y), new Vector2(100f, 34f), new Color32(140, 158, byte.MaxValue, byte.MaxValue), delegate
		{
			ModifyAttribute("耶芙娜好感度", lp.E_DragonFavor, ne.E_ExtraIncrease, amount);
		});
		CreateButton(gameObject.transform, "小叶子", new Vector2(num + 112f, y), new Vector2(100f, 34f), new Color32(140, 158, byte.MaxValue, byte.MaxValue), delegate
		{
			ModifyAttribute("小叶子好感度", lp.E_MaidFavor, ne.E_ExtraIncrease, amount);
		});
		CreateButton(gameObject.transform, "霞露零", new Vector2(num + 224f, y), new Vector2(100f, 34f), new Color32(140, 158, byte.MaxValue, byte.MaxValue), delegate
		{
			ModifyAttribute("霞露零好感度", lp.E_ElfFavor, ne.E_ExtraIncrease, amount);
		});
		CreateButton(gameObject.transform, "特莉波卡", new Vector2(num + 336f, y), new Vector2(100f, 34f), new Color32(140, 158, byte.MaxValue, byte.MaxValue), delegate
		{
			ModifyAttribute("特莉波卡好感度", lp.E_DeathFavor, ne.E_ExtraIncrease, amount);
		});
		CreateButton(gameObject.transform, "刷新数值", new Vector2(num + 448f, y), new Vector2(100f, 34f), new Color32(97, 97, 97, byte.MaxValue), LogCurrentValues);
		CreateButton(gameObject.transform, "导出物品", new Vector2(num + 560f, y), new Vector2(100f, 34f), new Color32(97, 97, 97, byte.MaxValue), delegate
		{
			TryExportItemList(force: true);
		});
		CreateText(gameObject.transform, "物品 ID", new Vector2(-323f, -42f), new Vector2(80f, 28f), 15, TextAnchor.MiddleRight, Color.white);
		itemIdInput = CreateInput(gameObject.transform, itemId.ToString(), new Vector2(-230f, -42f), new Vector2(120f, 30f), delegate(string text)
		{
			itemId = Math.Max(0, ParseInt(text, itemId));
			RefreshInputs();
		});
		CreateText(gameObject.transform, "个数", new Vector2(-112f, -42f), new Vector2(60f, 28f), 15, TextAnchor.MiddleRight, Color.white);
		itemCountInput = CreateInput(gameObject.transform, itemCount.ToString(), new Vector2(-45f, -42f), new Vector2(80f, 30f), delegate(string text)
		{
			itemCount = Math.Max(1, ParseInt(text, itemCount));
			RefreshInputs();
		});
		CreateButton(gameObject.transform, "检查物品", new Vector2(55f, -42f), new Vector2(94f, 30f), new Color32(97, 97, 97, byte.MaxValue), CheckItemById);
		CreateButton(gameObject.transform, "添加物品", new Vector2(160f, -42f), new Vector2(94f, 30f), new Color32(140, 158, byte.MaxValue, byte.MaxValue), AddItemById);
		CreateText(gameObject.transform, "Home 显示/隐藏。F1-F10 属性，F11/F12 数量，数字键输入物品ID，Insert 添加，End 检查。", new Vector2(0f, -118f), new Vector2(700f, 28f), 14, TextAnchor.MiddleCenter, new Color32(220, 220, 220, byte.MaxValue));
		statusText = CreateText(gameObject.transform, lastMessage, new Vector2(0f, -158f), new Vector2(700f, 54f), 14, TextAnchor.MiddleCenter, new Color32(180, byte.MaxValue, 180, byte.MaxValue));
		RefreshInputs();
	}

	private static void EnsureEventSystem()
	{
		if (!(UnityEngine.Object.FindObjectOfType<EventSystem>() != null))
		{
			GameObject obj = new GameObject("ScriptTrainerEventSystem");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			obj.AddComponent<EventSystem>();
			obj.AddComponent<StandaloneInputModule>();
		}
	}

	private static GameObject CreatePanel(Transform parent, string name, Vector2 size, Vector2 position, Color color)
	{
		GameObject obj = new GameObject(name);
		obj.transform.SetParent(parent, worldPositionStays: false);
		RectTransform rectTransform = obj.AddComponent<RectTransform>();
		rectTransform.sizeDelta = size;
		rectTransform.anchoredPosition = position;
		obj.AddComponent<Image>().color = color;
		return obj;
	}

	private static Text CreateText(Transform parent, string text, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, Color color)
	{
		GameObject obj = new GameObject("Text");
		obj.transform.SetParent(parent, worldPositionStays: false);
		RectTransform rectTransform = obj.AddComponent<RectTransform>();
		rectTransform.sizeDelta = size;
		rectTransform.anchoredPosition = position;
		Text text2 = obj.AddComponent<Text>();
		text2.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
		text2.text = text;
		text2.fontSize = fontSize;
		text2.alignment = alignment;
		text2.color = color;
		return text2;
	}

	private static Button CreateButton(Transform parent, string text, Vector2 position, Vector2 size, Color color, Action action)
	{
		GameObject gameObject = CreatePanel(parent, "Button", size, position, color);
		Button button = gameObject.AddComponent<Button>();
		button.targetGraphic = gameObject.GetComponent<Image>();
		button.onClick.AddListener(action);
		CreateText(gameObject.transform, text, Vector2.zero, size, 14, TextAnchor.MiddleCenter, Color.white);
		return button;
	}

	private static InputField CreateInput(Transform parent, string text, Vector2 position, Vector2 size, Action<string> onEndEdit)
	{
		GameObject obj = CreatePanel(parent, "InputField", size, position, new Color32(33, 33, 33, byte.MaxValue));
		InputField inputField = obj.AddComponent<InputField>();
		Text textComponent = CreateText(obj.transform, text, Vector2.zero, new Vector2(size.x - 12f, size.y), 15, TextAnchor.MiddleLeft, Color.white);
		inputField.textComponent = textComponent;
		inputField.text = text;
		inputField.onEndEdit.AddListener(onEndEdit);
		return inputField;
	}

	private void SetCanvasVisible(bool visible)
	{
		if (canvas != null)
		{
			canvas.SetActive(visible);
		}
	}

	private void RefreshInputs()
	{
		if (amountInput != null)
		{
			amountInput.text = amount.ToString();
		}
		if (itemIdInput != null)
		{
			itemIdInput.text = itemId.ToString();
		}
		if (itemCountInput != null)
		{
			itemCountInput.text = itemCount.ToString();
		}
		if (statusText != null)
		{
			statusText.text = lastMessage;
		}
	}

	private static long ParseLong(string text, long fallback)
	{
		if (!long.TryParse(text, out var result))
		{
			return fallback;
		}
		return result;
	}

	private static int ParseInt(string text, int fallback)
	{
		if (!int.TryParse(text, out var result))
		{
			return fallback;
		}
		return result;
	}

	// rm.bhfm(getter=hxi) 旧 hxh()；rm.hxn() 旧 hxm
	private long ReadAttribute(lp attr)
	{
		try
		{
			return rm.bhfm?.hxn(attr) ?? 0;
		}
		catch (Exception ex)
		{
			SetMessage($"读取 {attr} 失败: {ex.Message}");
			return 0L;
		}
	}

	// rm.hxp() 旧 hxo
	private void ModifyAttribute(string label, lp attr, ne effect, long amount)
	{
		try
		{
			rm mgr = rm.bhfm;
			if (mgr == null)
			{
				SetMessage("属性管理器尚未初始化，请进入存档后再试。", writeFile: true);
				return;
			}
			long value = mgr.hxn(attr);
			mgr.hxp(attr, effect, amount);
			long value2 = mgr.hxn(attr);
			SetMessage($"{label}: {value} -> {value2}", writeFile: true);
		}
		catch (Exception value3)
		{
			SetMessage($"{label} 失败: {value3}", writeFile: true);
		}
	}

	private void LogCurrentValues()
	{
		SetMessage($"Money={ReadAttribute(lp.E_Money)}, San={ReadAttribute(lp.E_San)}, Souls={ReadAttribute(lp.E_Souls)}, Clean={ReadAttribute(lp.E_Cleanliness)}, Evil={ReadAttribute(lp.E_Evil)}", writeFile: true);
	}

	// qo.bheq(getter=hmt) 旧 hms()；qo.hnj() 旧 hni；qo.hmz() 旧 hmy；Item.xlg 旧 xlb
	private void AddItemById()
	{
		try
		{
			Item itemFromTable = GetItemFromTable(itemId);
			if (itemFromTable != null && itemFromTable.xlg.ToString() == "E_SeniorRelic")
			{
				TryAddSeniorRelic(itemId);
				return;
			}
			qo bag = qo.bheq;
			if (bag == null)
			{
				SetMessage("背包管理器尚未初始化，请进入存档后再试。", writeFile: true);
				return;
			}
			Item item = bag.hnj(itemId);
			bag.hmz(itemId, itemCount, true);
			SetMessage((item == null) ? $"已尝试添加物品 ID={itemId} x{itemCount}。" : $"已添加物品 ID={itemId} x{itemCount}。", writeFile: true);
		}
		catch (Exception value)
		{
			SetMessage($"添加物品失败: {value}", writeFile: true);
		}
	}

	private void TryAddSeniorRelic(int relicId)
	{
		try
		{
			Type type = Type.GetType(ObfNames.EgType + ObfNames.AssemblySuffix);
			object obj = type?.GetMethod(ObfNames.EgGetInstance, BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
			if (obj == null || type == null)
			{
				SetMessage($"至高遗物管理器未初始化，ID={relicId}", writeFile: true);
				return;
			}
			string value = DescribeRelicManager(obj);
			bool value2 = AddSeniorRelicInstance(obj, relicId, 1);
			string value3 = DescribeRelicManager(obj);
			SetMessage($"至高遗物直接添加 ID={relicId}, added={value2}, before={value}, after={value3}", writeFile: true);
		}
		catch (Exception value4)
		{
			SetMessage($"至高遗物添加失败 ID={relicId}: {value4}", writeFile: true);
		}
	}

	private static bool AddSeniorRelicInstance(object manager, int relicId, int star)
	{
		try
		{
			object obj = TryAddSeniorRelicByNativeMethod(manager, relicId, star);
			if (obj != null)
			{
				TrainerLog.Write($"AddSeniorRelicInstance native-only ID={relicId}: {DescribeRelicManager(manager)}");
				return true;
			}
			Type type = Type.GetType(ObfNames.EcType + ObfNames.AssemblySuffix);
			obj = ((type == null) ? null : Activator.CreateInstance(type, relicId, star));
			object obj2 = manager.GetType().GetProperty(ObfNames.EgRelicDict)?.GetValue(manager);
			if (obj2 == null || obj == null)
			{
				return false;
			}
			_ = (bool)(obj2.GetType().GetMethod("ContainsKey")?.Invoke(obj2, new object[1] { relicId }) ?? ((object)false));
			SetDictionaryItem(obj2, relicId, obj);
			return true;
		}
		catch (Exception ex)
		{
			TrainerLog.Write("AddSeniorRelicInstance failed: " + ex);
			return false;
		}
	}

	private static void SetDictionaryItem(object dictionary, int key, object value)
	{
		if ((bool)(dictionary.GetType().GetMethod("ContainsKey")?.Invoke(dictionary, new object[1] { key }) ?? ((object)false)))
		{
			dictionary.GetType().GetMethod("set_Item")?.Invoke(dictionary, new object[2] { key, value });
		}
		else
		{
			dictionary.GetType().GetMethod("Add")?.Invoke(dictionary, new object[2] { key, value });
		}
	}

	// eg.dez() 旧 dey：ec dez(int relicId, int star, RelicEffectSaveParam save)
	private static object? TryAddSeniorRelicByNativeMethod(object manager, int relicId, int star)
	{
		try
		{
			RelicEffectSaveParam relicEffectSaveParam = new RelicEffectSaveParam();
			relicEffectSaveParam.ID = relicId;
			relicEffectSaveParam.lastExecuteTime = new Il2CppSystem.Collections.Generic.List<int>();
			relicEffectSaveParam.triggerCount = new Il2CppSystem.Collections.Generic.List<int>();
			for (int i = 0; i < 8; i++)
			{
				relicEffectSaveParam.lastExecuteTime.Add(0);
				relicEffectSaveParam.triggerCount.Add(0);
			}
			object? obj = manager.GetType().GetMethod(ObfNames.EgCreateRelic, BindingFlags.Instance | BindingFlags.Public)?.Invoke(manager, new object[3] { relicId, star, relicEffectSaveParam });
			if (obj != null)
			{
				TrainerLog.Write($"TryAddSeniorRelicByNativeMethod succeeded: eg.{ObfNames.EgCreateRelic}({relicId}, {star}, save.ID={relicEffectSaveParam.ID})");
			}
			return obj;
		}
		catch (Exception value)
		{
			TrainerLog.Write($"TryAddSeniorRelicByNativeMethod failed ID={relicId}: {value}");
			return null;
		}
	}

	// 属性名全部换新：vmh(旧vmc字典) vmi/vmj/vmr/vms/vmx(旧vmd/vme/vmm/vmn/vms列表)
	private static string DescribeRelicManager(object manager)
	{
		return $"{ObfNames.EgListA}={DescribeIntList(manager, ObfNames.EgListA)},{ObfNames.EgListB}={DescribeIntList(manager, ObfNames.EgListB)},{ObfNames.EgListC}={DescribeIntList(manager, ObfNames.EgListC)},{ObfNames.EgListD}={DescribeIntList(manager, ObfNames.EgListD)},{ObfNames.EgListE}={DescribeIntList(manager, ObfNames.EgListE)},{ObfNames.EgRelicDict}={GetDictionaryCount(manager, ObfNames.EgRelicDict)}";
	}

	private static string DescribeIntList(object instance, string propertyName)
	{
		try
		{
			object obj = instance.GetType().GetProperty(propertyName)?.GetValue(instance);
			int num2 = ((obj?.GetType().GetProperty("Count")?.GetValue(obj) is int num) ? num : (-1));
			if (obj == null || num2 <= 0)
			{
				return num2.ToString();
			}
			MethodInfo method = obj.GetType().GetMethod("get_Item");
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = Math.Max(0, num2 - 8); i < num2; i++)
			{
				object value = method?.Invoke(obj, new object[1] { i });
				if (stringBuilder.Length > 0)
				{
					stringBuilder.Append('/');
				}
				stringBuilder.Append(value);
			}
			return num2 + "[" + stringBuilder?.ToString() + "]";
		}
		catch
		{
			return "-1";
		}
	}

	private static int GetListCount(object instance, string propertyName)
	{
		try
		{
			object obj = instance.GetType().GetProperty(propertyName)?.GetValue(instance);
			return (obj?.GetType().GetProperty("Count")?.GetValue(obj) is int num) ? num : (-1);
		}
		catch
		{
			return -1;
		}
	}

	private static int GetDictionaryCount(object instance, string propertyName)
	{
		return GetListCount(instance, propertyName);
	}

	// hl.bgvr(getter=dzg) 旧 dzf()；hl.wal 旧 wag；hl.dzh() 旧 dzg；nl.xlu 旧 xlp；Item.xld 旧 xky
	// 主路走已实测验证的混淆表扫描；未混淆的 GetItem(int) 仅作将来改名时的兜底（其键语义未单独验证）。
	private static Item? GetItemFromTable(int id)
	{
		try
		{
			hl.Load();
			hl inst = hl.bgvr;
			nl table = hl.wal ?? inst?.dzh();
			if (table?.xlu != null)
			{
				for (int i = 0; i < table.xlu.Count; i++)
				{
					Item item = table.xlu[i];
					if (item != null && item.xld == id)
					{
						return item;
					}
				}
			}
			if (inst != null)
			{
				try
				{
					Item direct = inst.GetItem(id);
					if (direct != null)
					{
						return direct;
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
			return null;
		}
		return null;
	}

	// Item.xld/xle/xlq 旧 xky/xkz/xll
	private void CheckItemById()
	{
		try
		{
			Item item = qo.bheq?.hnj(itemId);
			SetMessage((item == null) ? $"未找到物品 ID={itemId}" : $"找到物品 ID={itemId}, tableId={item.xld}, nameId={item.xle}, icon={item.xlq}", writeFile: true);
		}
		catch (Exception value)
		{
			SetMessage($"检查物品失败: {value}", writeFile: true);
		}
	}

	private void PollExternalCommands()
	{
		if (Time.realtimeSinceStartup < nextCommandPollTime)
		{
			return;
		}
		nextCommandPollTime = Time.realtimeSinceStartup + 0.2f;
		string path = Path.Combine(Paths.BepInExRootPath, "ScriptTrainer.commands");
		if (!File.Exists(path))
		{
			return;
		}
		string[] array;
		try
		{
			array = File.ReadAllLines(path, Encoding.UTF8);
			File.WriteAllText(path, string.Empty, Encoding.UTF8);
		}
		catch
		{
			return;
		}
		string[] array2 = array;
		foreach (string text in array2)
		{
			if (!string.IsNullOrWhiteSpace(text))
			{
				string responseId = "0";
				string message;
				try
				{
					string[] array3 = text.Split('|');
					responseId = ((array3.Length != 0) ? array3[0] : "0");
					message = ExecuteExternalCommand(array3);
				}
				catch (Exception ex)
				{
					message = "命令失败: " + ex.Message;
				}
				WriteExternalResponse(responseId, message);
			}
		}
	}

	private string ExecuteExternalCommand(System.Collections.Generic.IReadOnlyList<string> parts)
	{
		if (parts.Count < 2)
		{
			return "命令格式错误";
		}
		switch (parts[1])
		{
		case "ATTR":
		{
			long num = ((parts.Count > 3) ? Math.Max(1L, ParseLong(parts[3], amount)) : amount);
			switch ((parts.Count > 2) ? parts[2] : string.Empty)
			{
			case "money":
				ModifyAttribute("添加现金", lp.E_Money, ne.E_ExtraIncrease, num);
				break;
			case "san":
				ModifyAttribute("添加 San", lp.E_San, ne.E_ExtraIncrease, num);
				break;
			case "souls":
				ModifyAttribute("添加灵魂", lp.E_Souls, ne.E_ExtraIncrease, num);
				break;
			case "clean":
				ModifyAttribute("添加清洁度", lp.E_Cleanliness, ne.E_ExtraIncrease, num);
				break;
			case "evil":
				ModifyAttribute("降低恶值", lp.E_Evil, ne.E_Reduce, num);
				break;
			case "action":
				ModifyAttribute("添加行动力", lp.E_ActionPiont, ne.E_ExtraIncrease, num);
				break;
			case "dragon":
				ModifyAttribute("耶芙娜好感度", lp.E_DragonFavor, ne.E_ExtraIncrease, num);
				break;
			case "maid":
				ModifyAttribute("小叶子好感度", lp.E_MaidFavor, ne.E_ExtraIncrease, num);
				break;
			case "elf":
				ModifyAttribute("霞露零好感度", lp.E_ElfFavor, ne.E_ExtraIncrease, num);
				break;
			case "death":
				ModifyAttribute("特莉波卡好感度", lp.E_DeathFavor, ne.E_ExtraIncrease, num);
				break;
			default:
				return "未知属性命令";
			}
			return lastMessage;
		}
		case "ADD_ITEM":
			if (parts.Count > 2)
			{
				itemId = Math.Max(0, ParseInt(parts[2], itemId));
			}
			if (parts.Count > 3)
			{
				itemCount = Math.Max(1, ParseInt(parts[3], itemCount));
			}
			AddItemById();
			return lastMessage;
		case "CHECK_ITEM":
			if (parts.Count > 2)
			{
				itemId = Math.Max(0, ParseInt(parts[2], itemId));
			}
			CheckItemById();
			return lastMessage;
		case "EXPORT_ITEMS":
			TryExportItemList(force: true);
			return lastMessage;
		case "VALUES":
			LogCurrentValues();
			return lastMessage;
		default:
			return "未知命令";
		}
	}

	private static void WriteExternalResponse(string responseId, string message)
	{
		try
		{
			string path = Path.Combine(Paths.BepInExRootPath, "ScriptTrainer.responses");
			string text = message.Replace("\r", " ").Replace("\n", " ").Replace("|", "/");
			File.AppendAllText(path, responseId + "|" + text + Environment.NewLine, Encoding.UTF8);
		}
		catch
		{
		}
	}

	// 熔断壳：本体在 ExportItemListCore 里。游戏更新改名时 JIT 会在"调用处"抛
	// MissingMethod/MissingField/TypeLoad，这里接住并停用导出，Update 不再被拖死。
	private void TryExportItemList(bool force)
	{
		if (!force && (itemListExported || exportBroken || Time.realtimeSinceStartup < nextExportAttemptTime))
		{
			return;
		}
		nextExportAttemptTime = Time.realtimeSinceStartup + 10f;
		TrainerLog.Write(force ? "Manual item export requested." : "Automatic item export attempt.");
		try
		{
			ExportItemListCore();
		}
		catch (MissingMethodException ex)
		{
			OnExportNamesBroken(ex);
		}
		catch (MissingFieldException ex2)
		{
			OnExportNamesBroken(ex2);
		}
		catch (TypeLoadException ex3)
		{
			OnExportNamesBroken(ex3);
		}
		catch (Exception ex4)
		{
			TrainerLog.Write("Export item list failed: " + ex4);
		}
	}

	private void OnExportNamesBroken(Exception ex)
	{
		exportBroken = true;
		SetMessage("物品导出已停用：游戏版本与修改器不匹配（混淆名已变化），其余功能继续可用。" + ex.Message, writeFile: true);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private void ExportItemListCore()
	{
		hl.Load();
		hl inst = hl.bgvr;
		nl table = hl.wal ?? inst?.dzh();
		if (table?.xlu == null || table.xlu.Count == 0)
		{
			TrainerLog.Write("Item table is not initialized yet; will retry.");
			return;
		}
		string text = Path.Combine(Paths.BepInExRootPath, "item_ids.csv");
		string text2 = Path.Combine(Paths.BepInExRootPath, "item_icons");
		Directory.CreateDirectory(text2);
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("ItemID,NameID,Name,Type,SubType,Icon,IconFile,Field_xlf,Field_xlh,Field_xlj,Field_xll,Field_xln,Field_xlr");
		for (int i = 0; i < table.xlu.Count; i++)
		{
			Item item = table.xlu[i];
			if (item != null)
			{
				string value = ExportItemIcon(item, text2);
				stringBuilder.Append(item.xld).Append(',').Append(item.xle)
					.Append(',')
					.Append(EscapeCsv(ResolveItemName(item)))
					.Append(',')
					.Append(item.xlg)
					.Append(',')
					.Append(item.xli)
					.Append(',')
					.Append(EscapeCsv(item.xlq))
					.Append(',')
					.Append(EscapeCsv(value))
					.Append(',')
					.Append(item.xlf)
					.Append(',')
					.Append(item.xlh)
					.Append(',')
					.Append(item.xlj)
					.Append(',')
					.Append(item.xll)
					.Append(',')
					.Append(item.xln)
					.Append(',')
					.Append(item.xlr)
					.AppendLine();
			}
		}
		File.WriteAllText(text, stringBuilder.ToString(), Encoding.UTF8);
		itemListExported = true;
		SetMessage($"物品ID清单已导出: {text} ({table.xlu.Count} items)", writeFile: true);
	}

	private static string EscapeCsv(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return "";
		}
		if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
		{
			return "\"" + value.Replace("\"", "\"\"") + "\"";
		}
		return value;
	}

	// cd.cpk 未变；cy.cun 旧 cum；cy.cus 旧 cur；Item.xle 旧 xkz
	private static string ResolveItemName(Item item)
	{
		string text = TryResolveItemNameByMethod(ObfNames.NameTypeA, ObfNames.NameByIdA, item.xle);
		if (!string.IsNullOrWhiteSpace(text) && text != item.xle.ToString())
		{
			return text;
		}
		text = TryResolveItemNameByMethod(ObfNames.NameTypeB, ObfNames.NameByIdB, item.xle);
		if (!string.IsNullOrWhiteSpace(text) && text != item.xle.ToString())
		{
			return text;
		}
		text = TryResolveItemNameByMethod(ObfNames.NameTypeB, ObfNames.NameByIdC, item.xle);
		if (!string.IsNullOrWhiteSpace(text) && !(text == item.xle.ToString()))
		{
			return text;
		}
		return string.Empty;
	}

	private static string TryResolveItemNameByMethod(string typeName, string methodName, int nameId)
	{
		try
		{
			return ((Type.GetType(typeName + ObfNames.AssemblySuffix)?.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public))?.Invoke(null, new object[1] { nameId }))?.ToString() ?? string.Empty;
		}
		catch
		{
			return string.Empty;
		}
	}

	// cy.cup 旧 cuo；cn.ctr 旧 ctq；cy.cuq 旧 cup；Item.xlq/xld 旧 xll/xky
	private static string ExportItemIcon(Item item, string iconDir)
	{
		try
		{
			Sprite sprite = TryGetSprite(ObfNames.SpriteTypeB, ObfNames.SpriteByPathB, item.xlq) ?? TryGetSprite(ObfNames.SpriteTypeCn, ObfNames.SpriteByPathCn, item.xlq) ?? TryGetSprite(ObfNames.SpriteTypeB, ObfNames.SpriteByIdB, item.xld);
			if (sprite == null || sprite.texture == null)
			{
				return string.Empty;
			}
			byte[] bytes = CopySpriteTexture(sprite).EncodeToPNG();
			string path = item.xld + ".png";
			File.WriteAllBytes(Path.Combine(iconDir, path), bytes);
			return Path.Combine("item_icons", path);
		}
		catch
		{
			return string.Empty;
		}
	}

	private static Sprite? TryGetSprite(string typeName, string methodName, object argument)
	{
		try
		{
			return (Type.GetType(typeName + ObfNames.AssemblySuffix)?.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public))?.Invoke(null, new object[1] { argument }) as Sprite;
		}
		catch
		{
			return null;
		}
	}

	private static Texture2D CopySpriteTexture(Sprite sprite)
	{
		Rect rect = sprite.rect;
		RenderTexture active = RenderTexture.active;
		RenderTexture temporary = RenderTexture.GetTemporary(sprite.texture.width, sprite.texture.height, 0, RenderTextureFormat.ARGB32);
		try
		{
			Graphics.Blit(sprite.texture, temporary);
			RenderTexture.active = temporary;
			Texture2D texture2D = new Texture2D(sprite.texture.width, sprite.texture.height, TextureFormat.RGBA32, mipChain: false);
			texture2D.ReadPixels(new Rect(0f, 0f, temporary.width, temporary.height), 0, 0);
			texture2D.Apply();
			Texture2D texture2D2 = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, mipChain: false);
			Color[] array = texture2D.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
			texture2D2.SetPixels(array);
			texture2D2.Apply();
			UnityEngine.Object.Destroy(texture2D);
			return texture2D2;
		}
		finally
		{
			RenderTexture.active = active;
			RenderTexture.ReleaseTemporary(temporary);
		}
	}

	private void SetMessage(string message, bool writeFile = false)
	{
		lastMessage = message;
		if (statusText != null)
		{
			statusText.text = message;
		}
		Debug.Log("ScriptTrainer: " + message);
		if (writeFile)
		{
			TrainerLog.Write(message);
		}
	}
}
