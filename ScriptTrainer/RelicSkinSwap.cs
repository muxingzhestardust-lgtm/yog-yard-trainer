using System;
using System.IO;
using System.Text;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace ScriptTrainer;

// ============================================================================
// 实验性功能：把旧版本游戏里提取出来的 Relic* 神谕贴图替换回当前游戏（仅内存，重启即恢复）。
//
// 思路：不动任何资源文件，而是在运行时用 ImageConversion.LoadImage 把 PNG 像素
// 原地灌进游戏已加载的 Texture2D 对象里——所有引用这张贴图的 Sprite/Image/预制体
// 全部自动跟着变，不需要去追每一处引用。
//
// 两类目标（按 Sprite.texture 分组处理）：
//   1) 独立贴图：Sprite 的 rect 铺满整张贴图（神谕卡图 Arts/UI/Card/Relic_XX 全是这种，
//      512x512）——直接 LoadImage 覆盖整张贴图。
//   2) 图集子图：Sprite 只是 UI_Main/UI_Main2 这类 1024 图集里的一块（RelicBg/RelicFrame/
//      RelicButton/RelicIcon）——先把图集 Blit->ReadPixels 解出可读副本，把 PNG 贴进
//      对应 rect（尺寸不同时双线性重采样），再整张 EncodeToPNG -> LoadImage 写回原对象。
//
// 替换过的贴图打上 DontUnloadUnusedAsset 并常驻引用，避免 Resources.UnloadUnusedAssets
// 把它回收后又从磁盘加载回新版原图。
// ============================================================================
internal static class RelicSkinSwap
{
	// Resources 路径，查自 globalgamemanagers 的 ResourceManager 容器表（大小写不敏感）；
	// 先 Resources.Load 一遍把还没进内存的贴图强制加载，这样在主菜单也能一次换完。
	private static readonly string[] CardSuffixes = new string[22]
	{
		"DG", "DG2", "FGE", "FGE2", "HST", "HST2", "HSY", "HSY2", "HZL", "HZL2",
		"KSL", "KSL2", "LS", "LS2", "Long", "Long2", "NYGD", "NYGD2", "UGSTS", "UGSTS2",
		"YOD", "YOD2"
	};

	private static readonly string[] IconPaths = new string[4]
	{
		"UIAtlas/Icon/UIIcon/Relic", "UIAtlas/Icon/UIIcon/RelicBG", "UIAtlas/Icon/UIIcon/RelicChoose", "UIAtlas/Icon/UIIcon/RelicIcon"
	};

	private sealed class PngFile
	{
		public string Path = string.Empty;

		public string SpriteName = string.Empty;

		public int Width;

		public int Height;
	}

	// 常驻引用：防止被 GC/UnloadUnusedAssets 回收
	private static readonly System.Collections.Generic.List<Texture2D> pinned = new System.Collections.Generic.List<Texture2D>();

	public static string Apply(string dir)
	{
		if (string.IsNullOrWhiteSpace(dir))
		{
			return "未指定旧版贴图目录";
		}
		dir = dir.Trim().Trim('"');
		if (!Directory.Exists(dir))
		{
			return "旧版贴图目录不存在: " + dir;
		}
		System.Collections.Generic.List<PngFile> pngs = ScanPngs(dir);
		if (pngs.Count == 0)
		{
			return "目录里没有 Relic 开头的 PNG: " + dir;
		}
		ForceLoadKnownSprites();
		// 收集所有已加载的 Relic* Sprite，按所属贴图分组
		System.Collections.Generic.Dictionary<IntPtr, Texture2D> textures = new System.Collections.Generic.Dictionary<IntPtr, Texture2D>();
		System.Collections.Generic.Dictionary<IntPtr, System.Collections.Generic.List<Sprite>> groups = new System.Collections.Generic.Dictionary<IntPtr, System.Collections.Generic.List<Sprite>>();
		int spriteTotal = 0;
		foreach (UnityEngine.Object obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Sprite>()))
		{
			Sprite sprite = obj?.TryCast<Sprite>();
			if (sprite == null || sprite.name == null || !sprite.name.StartsWith("Relic", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			Texture2D texture = sprite.texture;
			if (texture == null)
			{
				continue;
			}
			spriteTotal++;
			IntPtr key = texture.Pointer;
			if (!groups.TryGetValue(key, out var list))
			{
				list = new System.Collections.Generic.List<Sprite>();
				groups[key] = list;
				textures[key] = texture;
			}
			list.Add(sprite);
		}
		if (spriteTotal == 0)
		{
			return "游戏里没找到任何 Relic 开头的 Sprite（请确认已进入游戏）";
		}
		int direct = 0;
		int atlas = 0;
		int skipped = 0;
		System.Collections.Generic.List<string> missing = new System.Collections.Generic.List<string>();
		System.Collections.Generic.List<string> failed = new System.Collections.Generic.List<string>();
		foreach (System.Collections.Generic.KeyValuePair<IntPtr, System.Collections.Generic.List<Sprite>> pair in groups)
		{
			Texture2D texture = textures[pair.Key];
			try
			{
				int result = ReplaceTexture(texture, pair.Value, pngs, missing);
				if (result == 1)
				{
					direct++;
				}
				else if (result == 2)
				{
					atlas++;
				}
				else
				{
					skipped++;
				}
			}
			catch (Exception ex)
			{
				failed.Add(texture.name + ":" + ex.GetType().Name);
				TrainerLog.Write("[RELIC] replace " + texture.name + " failed: " + ex);
			}
		}
		StringBuilder sb = new StringBuilder();
		sb.Append($"旧版神谕贴图替换完成：找到 {spriteTotal} 个 Relic 精灵 / {groups.Count} 张贴图，整图覆盖 {direct}，图集贴入 {atlas}");
		if (skipped > 0)
		{
			sb.Append($"，无匹配 {skipped}");
		}
		if (missing.Count > 0)
		{
			sb.Append("；缺 PNG: ").Append(string.Join(",", missing.ToArray()));
		}
		if (failed.Count > 0)
		{
			sb.Append("；失败: ").Append(string.Join(",", failed.ToArray()));
		}
		sb.Append("。仅内存生效，重启游戏恢复。");
		string text = sb.ToString();
		TrainerLog.Write("[RELIC] " + text);
		return text;
	}

	// 目录里 Relic 开头的 PNG；文件名去掉提取工具加的 "_<pathId>" 后缀即 Sprite 名
	// （同名 Sprite 可能有多个候选，后面按 rect 尺寸挑）；宽高直接读 PNG IHDR。
	private static System.Collections.Generic.List<PngFile> ScanPngs(string dir)
	{
		System.Collections.Generic.List<PngFile> list = new System.Collections.Generic.List<PngFile>();
		foreach (string path in Directory.GetFiles(dir, "*.png"))
		{
			string stem = Path.GetFileNameWithoutExtension(path);
			if (!stem.StartsWith("Relic", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			int underscore = stem.LastIndexOf('_');
			if (underscore > 0 && underscore < stem.Length - 1 && IsAllDigits(stem, underscore + 1))
			{
				stem = stem.Substring(0, underscore);
			}
			if (!TryReadPngSize(path, out int w, out int h))
			{
				continue;
			}
			list.Add(new PngFile
			{
				Path = path,
				SpriteName = stem,
				Width = w,
				Height = h
			});
		}
		return list;
	}

	private static bool IsAllDigits(string s, int from)
	{
		for (int i = from; i < s.Length; i++)
		{
			if (!char.IsDigit(s[i]))
			{
				return false;
			}
		}
		return true;
	}

	private static bool TryReadPngSize(string path, out int width, out int height)
	{
		width = 0;
		height = 0;
		try
		{
			using FileStream fs = File.OpenRead(path);
			byte[] header = new byte[24];
			if (fs.Read(header, 0, 24) != 24 || header[0] != 0x89 || header[1] != (byte)'P' || header[2] != (byte)'N' || header[3] != (byte)'G')
			{
				return false;
			}
			width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
			height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
			return width > 0 && height > 0;
		}
		catch
		{
			return false;
		}
	}

	private static void ForceLoadKnownSprites()
	{
		foreach (string suffix in CardSuffixes)
		{
			SafeLoadSprite("Arts/UI/Card/Relic_" + suffix);
		}
		foreach (string path in IconPaths)
		{
			SafeLoadSprite(path);
		}
	}

	private static void SafeLoadSprite(string path)
	{
		try
		{
			Resources.Load<Sprite>(path);
		}
		catch (Exception ex)
		{
			TrainerLog.Write("[RELIC] Resources.Load " + path + " failed: " + ex.Message);
		}
	}

	// 同名候选里优先 rect 尺寸完全一致的；否则取第一个（贴图集时会重采样）
	private static PngFile? PickPng(System.Collections.Generic.List<PngFile> pngs, string spriteName, int w, int h)
	{
		PngFile? fallback = null;
		foreach (PngFile png in pngs)
		{
			if (!string.Equals(png.SpriteName, spriteName, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			if (png.Width == w && png.Height == h)
			{
				return png;
			}
			fallback ??= png;
		}
		return fallback;
	}

	// 返回 0=没有任何可替换项 1=整图覆盖 2=图集贴入
	private static int ReplaceTexture(Texture2D texture, System.Collections.Generic.List<Sprite> sprites, System.Collections.Generic.List<PngFile> pngs, System.Collections.Generic.List<string> missing)
	{
		int texW = texture.width;
		int texH = texture.height;
		// 1) 独立贴图：某个 Sprite 铺满整张贴图，且有同尺寸 PNG -> 直接整图覆盖
		foreach (Sprite sprite in sprites)
		{
			Rect rect = sprite.rect;
			if ((int)rect.x != 0 || (int)rect.y != 0 || (int)rect.width != texW || (int)rect.height != texH)
			{
				continue;
			}
			PngFile? png = PickPng(pngs, sprite.name, texW, texH);
			if (png == null)
			{
				continue;
			}
			if (png.Width != texW || png.Height != texH)
			{
				// 尺寸不一致就走图集路线贴进去（会重采样），不要整图覆盖以免改变贴图尺寸破坏 rect
				break;
			}
			byte[] bytes = File.ReadAllBytes(png.Path);
			if (!ImageConversion.LoadImage(texture, bytes, false))
			{
				throw new InvalidOperationException("LoadImage returned false");
			}
			Pin(texture);
			TrainerLog.Write($"[RELIC] direct {texture.name} <- {Path.GetFileName(png.Path)} ({texW}x{texH})");
			return 1;
		}
		// 2) 图集：把每个有候选的子图贴进可读副本，再整张写回
		System.Collections.Generic.List<Sprite> targets = new System.Collections.Generic.List<Sprite>();
		System.Collections.Generic.List<PngFile> chosen = new System.Collections.Generic.List<PngFile>();
		foreach (Sprite sprite in sprites)
		{
			Rect rect = sprite.rect;
			PngFile? png = PickPng(pngs, sprite.name, (int)rect.width, (int)rect.height);
			if (png == null)
			{
				missing.Add(sprite.name);
				continue;
			}
			targets.Add(sprite);
			chosen.Add(png);
		}
		if (targets.Count == 0)
		{
			return 0;
		}
		Texture2D copy = CopyReadable(texture);
		try
		{
			for (int i = 0; i < targets.Count; i++)
			{
				Rect rect = targets[i].rect;
				int rx = (int)rect.x;
				int ry = (int)rect.y;
				int rw = (int)rect.width;
				int rh = (int)rect.height;
				if (rx < 0 || ry < 0 || rw <= 0 || rh <= 0 || rx + rw > texW || ry + rh > texH)
				{
					continue;
				}
				Texture2D src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
				try
				{
					if (!ImageConversion.LoadImage(src, File.ReadAllBytes(chosen[i].Path), false))
					{
						continue;
					}
					Color[] pixels;
					if (src.width == rw && src.height == rh)
					{
						pixels = src.GetPixels();
					}
					else
					{
						pixels = new Color[rw * rh];
						for (int y = 0; y < rh; y++)
						{
							for (int x = 0; x < rw; x++)
							{
								pixels[y * rw + x] = src.GetPixelBilinear(((float)x + 0.5f) / (float)rw, ((float)y + 0.5f) / (float)rh);
							}
						}
					}
					copy.SetPixels(rx, ry, rw, rh, pixels);
					TrainerLog.Write($"[RELIC] atlas {texture.name}[{targets[i].name} @{rx},{ry} {rw}x{rh}] <- {Path.GetFileName(chosen[i].Path)} ({src.width}x{src.height})");
				}
				finally
				{
					UnityEngine.Object.Destroy(src);
				}
			}
			copy.Apply();
			byte[] merged = ImageConversion.EncodeToPNG(copy);
			if (merged == null || merged.Length == 0 || !ImageConversion.LoadImage(texture, merged, false))
			{
				throw new InvalidOperationException("atlas write-back failed");
			}
			Pin(texture);
			return 2;
		}
		finally
		{
			UnityEngine.Object.Destroy(copy);
		}
	}

	private static void Pin(Texture2D texture)
	{
		texture.hideFlags |= HideFlags.DontUnloadUnusedAsset;
		foreach (Texture2D t in pinned)
		{
			if (t.Pointer == texture.Pointer)
			{
				return;
			}
		}
		pinned.Add(texture);
	}

	// 压缩纹理不可直接读像素：Blit -> ReadPixels 解成 RGBA32 可读副本
	private static Texture2D CopyReadable(Texture2D texture)
	{
		RenderTexture active = RenderTexture.active;
		RenderTexture temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
		try
		{
			Graphics.Blit(texture, temporary);
			RenderTexture.active = temporary;
			Texture2D copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
			copy.ReadPixels(new Rect(0f, 0f, temporary.width, temporary.height), 0, 0);
			copy.Apply();
			return copy;
		}
		finally
		{
			RenderTexture.active = active;
			RenderTexture.ReleaseTemporary(temporary);
		}
	}
}
