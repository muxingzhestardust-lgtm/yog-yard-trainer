using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ScriptTrainerExternal;

// 皮肤：小件贴图（按钮/徽章等）为嵌入资源 Assets\*.png；CG 背景由插件首启导出到
// BepInEx\ui_backgrounds\ 后在本机运行时烘焙（见 InitBackgrounds/Bg）
internal static class Skin
{
	public static readonly Color Text = Color.FromArgb(224, 210, 184);

	public static readonly Color TextDim = Color.FromArgb(150, 140, 128);

	public static readonly Color InputBack = Color.FromArgb(24, 21, 27);

	public static readonly Color GridBack = Color.FromArgb(20, 18, 24);

	public static readonly Color GridRow = Color.FromArgb(31, 28, 35);

	public static readonly Color GridRowAlt = Color.FromArgb(26, 23, 30);

	public static readonly Color GridSel = Color.FromArgb(122, 28, 34);

	public static readonly Color GridLine = Color.FromArgb(52, 46, 56);

	public static readonly Color StatusBack = Color.FromArgb(16, 14, 19);

	public static readonly Color StatusText = Color.FromArgb(186, 214, 170);

	private static readonly Dictionary<string, Image> cache = new Dictionary<string, Image>();

	private static string bgSourceDir;

	private static string bgBakedDir;

	public static Image Img(string name)
	{
		if (cache.TryGetValue(name, out var value))
		{
			return value;
		}
		Assembly assembly = typeof(Skin).Assembly;
		foreach (string manifestResourceName in assembly.GetManifestResourceNames())
		{
			if (!manifestResourceName.EndsWith(".Assets." + name + ".png", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			using (Stream stream = assembly.GetManifestResourceStream(manifestResourceName))
			{
				using Image original = Image.FromStream(stream);
				value = new Bitmap(original);
			}
			cache[name] = value;
			return value;
		}
		throw new InvalidOperationException("缺少皮肤资源: " + name);
	}

	// CG 背景不随包分发（游戏原画）：插件首次随游戏启动时把原图导出到
	// BepInEx\ui_backgrounds\，UI 在这里读取并运行时烘焙
	public static void InitBackgrounds(string bepinexRoot)
	{
		bgSourceDir = Path.Combine(bepinexRoot, "ui_backgrounds");
		bgBakedDir = Path.Combine(bgSourceDir, "baked");
	}

	// 可选背景清单：ui_backgrounds 下所有已导出的 CG 原图（jpg）；游戏还没
	// 用新版插件启动过时目录不存在，返回空表，由调用方降级处理
	public static string[] Backgrounds()
	{
		List<string> list = new List<string>();
		try
		{
			if (bgSourceDir != null && Directory.Exists(bgSourceDir))
			{
				foreach (string file in Directory.GetFiles(bgSourceDir, "*.jpg"))
				{
					list.Add(Path.GetFileNameWithoutExtension(file));
				}
			}
		}
		catch
		{
		}
		list.Sort((string a, string b) => string.CompareOrdinal(SortKey(a), SortKey(b)));
		return list.ToArray();
	}

	// 取烘焙好的背景：内存缓存 -> baked\ 磁盘缓存 -> 从原图现场烘焙；全都不可用返回 null
	public static Image Bg(string name)
	{
		string key = "bg:" + name;
		if (cache.TryGetValue(key, out var value))
		{
			return value;
		}
		string bakedPath = Path.Combine(bgBakedDir, name + ".jpg");
		Image image = null;
		try
		{
			if (File.Exists(bakedPath))
			{
				image = LoadBitmap(bakedPath);
			}
		}
		catch
		{
			image = null;
		}
		if (image == null)
		{
			image = BakeBackground(name, bakedPath);
		}
		if (image != null)
		{
			cache[key] = image;
		}
		return image;
	}

	// 运行时烘焙，与旧版打包脚本 make_bgs.py 同一套流程：等比 cover 裁到 1400x788
	// 铺黑底 -> 叠 DarkWindow 暗角 -> 整体压暗(150/144/156)；结果写进 baked\ 下次直读
	private static Image BakeBackground(string name, string bakedPath)
	{
		try
		{
			string text = Path.Combine(bgSourceDir, name + ".jpg");
			if (!File.Exists(text))
			{
				return null;
			}
			using Bitmap bitmap = new Bitmap(1400, 788);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
				graphics.PixelOffsetMode = PixelOffsetMode.Half;
				graphics.Clear(Color.Black);
				using (Image image = LoadBitmap(text))
				{
					double num = (double)image.Width / (double)image.Height;
					double num2 = 1400.0 / 788.0;
					Rectangle srcRect;
					if (num > num2)
					{
						int num3 = Math.Max(1, (int)((double)image.Height * num2));
						srcRect = new Rectangle((image.Width - num3) / 2, 0, num3, image.Height);
					}
					else
					{
						int num4 = Math.Max(1, (int)((double)image.Width / num2));
						srcRect = new Rectangle(0, (image.Height - num4) / 2, image.Width, num4);
					}
					graphics.DrawImage(image, new Rectangle(0, 0, 1400, 788), srcRect, GraphicsUnit.Pixel);
				}
				string text2 = Path.Combine(bgSourceDir, "DarkWindow.png");
				if (File.Exists(text2))
				{
					using Image image2 = LoadBitmap(text2);
					graphics.DrawImage(image2, new Rectangle(0, 0, 1400, 788));
				}
			}
			Bitmap bitmap2 = new Bitmap(1400, 788);
			using (Graphics graphics2 = Graphics.FromImage(bitmap2))
			{
				using ImageAttributes imageAttributes = new ImageAttributes();
				imageAttributes.SetColorMatrix(new ColorMatrix
				{
					Matrix00 = 150f / 255f,
					Matrix11 = 144f / 255f,
					Matrix22 = 156f / 255f
				});
				graphics2.DrawImage(bitmap, new Rectangle(0, 0, 1400, 788), 0, 0, 1400, 788, GraphicsUnit.Pixel, imageAttributes);
			}
			try
			{
				Directory.CreateDirectory(bgBakedDir);
				SaveJpeg(bitmap2, bakedPath, 90L);
			}
			catch
			{
			}
			return bitmap2;
		}
		catch
		{
			return null;
		}
	}

	// 经字节复制加载，避免 GDI+ 长期锁住磁盘文件
	private static Image LoadBitmap(string path)
	{
		using Image original = Image.FromFile(path);
		return new Bitmap(original);
	}

	private static void SaveJpeg(Bitmap bitmap, string path, long quality)
	{
		ImageCodecInfo imageCodecInfo = null;
		ImageCodecInfo[] imageEncoders = ImageCodecInfo.GetImageEncoders();
		foreach (ImageCodecInfo codec in imageEncoders)
		{
			if (codec.FormatID == ImageFormat.Jpeg.Guid)
			{
				imageCodecInfo = codec;
			}
		}
		if (imageCodecInfo == null)
		{
			bitmap.Save(path, ImageFormat.Jpeg);
			return;
		}
		using EncoderParameters encoderParameters = new EncoderParameters(1);
		encoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
		bitmap.Save(path, imageCodecInfo, encoderParameters);
	}

	// 数字感知排序键：数字段补零对齐，让「小叶子 3」排在「小叶子 10」前面
	private static string SortKey(string name)
	{
		StringBuilder stringBuilder = new StringBuilder();
		int num = 0;
		while (num < name.Length)
		{
			if (char.IsDigit(name[num]))
			{
				int i = num;
				while (i < name.Length && char.IsDigit(name[i]))
				{
					i++;
				}
				stringBuilder.Append(name.Substring(num, i - num).PadLeft(4, '0'));
				num = i;
			}
			else
			{
				stringBuilder.Append(char.ToUpperInvariant(name[num++]));
			}
		}
		return stringBuilder.ToString();
	}

	// 背景菜单显示名：CG 前缀翻成角色名
	public static string BgLabel(string name)
	{
		if (name.StartsWith("CG_Dragon_", StringComparison.Ordinal))
		{
			return "耶芙娜 " + name.Substring(10);
		}
		if (name.StartsWith("CG_Maid_", StringComparison.Ordinal))
		{
			return "小叶子 " + name.Substring(8);
		}
		if (name.StartsWith("CG_Elf_", StringComparison.Ordinal))
		{
			return "霞露零 " + name.Substring(7);
		}
		if (name.StartsWith("CG_Death_", StringComparison.Ordinal))
		{
			return "特莉波卡 " + name.Substring(9);
		}
		if (name.StartsWith("BG_", StringComparison.Ordinal))
		{
			return "场景 " + name.Substring(3);
		}
		return name;
	}

	public static void StyleInput(TextBox box)
	{
		box.BackColor = InputBack;
		box.ForeColor = Text;
		box.BorderStyle = BorderStyle.FixedSingle;
	}
}

// 游戏原生按钮贴图（灰铁条+红晕），四态：Normal/Hover/Select(按下或选中)/Useless(禁用)
internal sealed class SkinButton : Control
{
	private readonly Image normal;

	private readonly Image hover;

	private readonly Image select;

	private readonly Image useless;

	private bool over;

	private bool down;

	private bool isChecked;

	public bool Checked
	{
		get
		{
			return isChecked;
		}
		set
		{
			isChecked = value;
			Invalidate();
		}
	}

	public SkinButton(string size, string text)
	{
		normal = Skin.Img("btn_" + size + "_normal");
		hover = Skin.Img("btn_" + size + "_hover");
		select = Skin.Img("btn_" + size + "_select");
		useless = Skin.Img("btn_" + size + "_useless");
		Text = text;
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, value: true);
		BackColor = Color.Transparent;
		ForeColor = Skin.Text;
		Height = 30;
		Cursor = Cursors.Hand;
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		over = true;
		Invalidate();
		base.OnMouseEnter(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		over = false;
		down = false;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		down = true;
		Invalidate();
		base.OnMouseDown(e);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		down = false;
		Invalidate();
		base.OnMouseUp(e);
	}

	protected override void OnEnabledChanged(EventArgs e)
	{
		Invalidate();
		base.OnEnabledChanged(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Image img = (!Enabled) ? useless : ((down || isChecked) ? select : (over ? hover : normal));
		DrawThreeSlice(e.Graphics, img, ClientRectangle);
		TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Enabled ? ForeColor : Skin.TextDim, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
	}

	// 横向三切片拉伸：保住两端金属造型，中段自由伸缩
	private static void DrawThreeSlice(Graphics g, Image img, Rectangle r)
	{
		g.InterpolationMode = InterpolationMode.HighQualityBilinear;
		g.PixelOffsetMode = PixelOffsetMode.Half;
		int num = Math.Min(img.Width / 3, img.Height);
		int num2 = Math.Max(1, num * r.Height / img.Height);
		if (2 * num2 >= r.Width)
		{
			g.DrawImage(img, r);
			return;
		}
		g.DrawImage(img, new Rectangle(r.X, r.Y, num2, r.Height), new Rectangle(0, 0, num, img.Height), GraphicsUnit.Pixel);
		g.DrawImage(img, new Rectangle(r.X + num2, r.Y, r.Width - 2 * num2, r.Height), new Rectangle(num, 0, img.Width - 2 * num, img.Height), GraphicsUnit.Pixel);
		g.DrawImage(img, new Rectangle(r.Right - num2, r.Y, num2, r.Height), new Rectangle(img.Width - num, 0, num, img.Height), GraphicsUnit.Pixel);
	}
}

// 单贴图按钮（关闭钮用），悬停提亮
internal sealed class IconButton : Control
{
	private readonly Image img;

	private bool over;

	public IconButton(string name)
	{
		img = Skin.Img(name);
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, value: true);
		BackColor = Color.Transparent;
		Cursor = Cursors.Hand;
		Size = new Size(36, 36);
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		over = true;
		Invalidate();
		base.OnMouseEnter(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		over = false;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
		int num = Math.Min(base.Width, base.Height) - 4;
		Rectangle rect = new Rectangle((base.Width - num) / 2, (base.Height - num) / 2, num, num);
		if (over)
		{
			e.Graphics.DrawImage(img, rect);
			return;
		}
		using ImageAttributes imageAttributes = new ImageAttributes();
		imageAttributes.SetColorMatrix(new ColorMatrix
		{
			Matrix33 = 0.72f
		});
		e.Graphics.DrawImage(img, rect, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, imageAttributes);
	}
}

// 背景选择菜单的暗色渲染
internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
	protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
	{
		Rectangle rect = new Rectangle(Point.Empty, e.Item.Size);
		using SolidBrush brush = new SolidBrush(e.Item.Selected ? Skin.GridSel : Skin.GridRow);
		e.Graphics.FillRectangle(brush, rect);
	}

	protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
	{
		using SolidBrush brush = new SolidBrush(Skin.GridRow);
		e.Graphics.FillRectangle(brush, e.AffectedBounds);
	}

	protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
	{
		using Pen pen = new Pen(Skin.GridLine);
		Rectangle affectedBounds = e.AffectedBounds;
		e.Graphics.DrawRectangle(pen, affectedBounds.X, affectedBounds.Y, affectedBounds.Width - 1, affectedBounds.Height - 1);
	}

	protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
	{
		using SolidBrush brush = new SolidBrush(Skin.GridRow);
		e.Graphics.FillRectangle(brush, e.AffectedBounds);
	}
}

// 无边框窗体的命中穿透：这些控件把 WM_NCHITTEST 还给窗体，统一由 MainForm.HitTest 判定拖动/缩放
internal sealed class ChromePanel : Panel
{
	protected override void WndProc(ref Message m)
	{
		if (m.Msg == 132)
		{
			m.Result = (IntPtr)(-1);
			return;
		}
		base.WndProc(ref m);
	}
}

internal sealed class ChromeLabel : Label
{
	protected override void WndProc(ref Message m)
	{
		if (m.Msg == 132)
		{
			m.Result = (IntPtr)(-1);
			return;
		}
		base.WndProc(ref m);
	}
}

internal sealed class ChromePicture : PictureBox
{
	protected override void WndProc(ref Message m)
	{
		if (m.Msg == 132)
		{
			m.Result = (IntPtr)(-1);
			return;
		}
		base.WndProc(ref m);
	}
}

// 透明表格：背景由窗体 CG 透出（Backdrop 回调），行/表头在 CellPainting 里叠半透明色；
// 原生滚动条太出戏，藏掉后在右缘自绘半透明滑块（滚轮 + 拖拽 + 点轨道跳转）
internal sealed class GlassGrid : DataGridView
{
	public Action<Graphics, Rectangle> Backdrop;

	private bool dragging;

	private int dragOffset;

	[DllImport("user32.dll")]
	private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

	public GlassGrid()
	{
		DoubleBuffered = true;
		ScrollBars = ScrollBars.None;
	}

	protected override void PaintBackground(Graphics graphics, Rectangle clipBounds, Rectangle gridBounds)
	{
		if (Backdrop == null)
		{
			base.PaintBackground(graphics, clipBounds, gridBounds);
			return;
		}
		Backdrop(graphics, clipBounds);
		using SolidBrush brush = new SolidBrush(Color.FromArgb(90, Skin.GridBack));
		graphics.FillRectangle(brush, clipBounds);
	}

	private int MaxFirstRow()
	{
		return Math.Max(0, RowCount - Math.Max(1, DisplayedRowCount(includePartialRow: false)));
	}

	private void ScrollTo(int index)
	{
		if (RowCount == 0)
		{
			return;
		}
		index = Math.Max(0, Math.Min(MaxFirstRow(), index));
		if (FirstDisplayedScrollingRowIndex != index)
		{
			// DataGridView 滚动内部用 ScrollWindow 平移旧像素，而背景 CG 钉在窗体上，
			// 平移过的旧画面和补画的新条带接不上就撕裂；滚动期间关掉重绘（WM_SETREDRAW=11），
			// 滚完再整幅同步重画，平移的半成品永远不上屏
			bool flag = IsHandleCreated;
			if (flag)
			{
				SendMessage(Handle, 11, (IntPtr)0, IntPtr.Zero);
			}
			FirstDisplayedScrollingRowIndex = index;
			if (flag)
			{
				SendMessage(Handle, 11, (IntPtr)1, IntPtr.Zero);
			}
		}
		Invalidate();
		Update();
	}

	protected override void OnScroll(ScrollEventArgs e)
	{
		// 键盘导航等内部自发的滚动不经过 ScrollTo，也立即整幅补画
		base.OnScroll(e);
		Invalidate();
		Update();
	}

	private Rectangle ThumbRect(out Rectangle track)
	{
		track = Rectangle.Empty;
		int num = MaxFirstRow();
		if (RowCount == 0 || num <= 0)
		{
			return Rectangle.Empty;
		}
		int num2 = (ColumnHeadersVisible ? ColumnHeadersHeight : 0);
		track = new Rectangle(base.Width - 9, num2 + 2, 6, base.Height - num2 - 4);
		int num3 = Math.Max(24, track.Height * DisplayedRowCount(includePartialRow: false) / RowCount);
		int y = track.Y + (int)((long)(track.Height - num3) * (long)FirstDisplayedScrollingRowIndex / num);
		return new Rectangle(track.X, y, track.Width, num3);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		Rectangle track;
		Rectangle rectangle = ThumbRect(out track);
		if (!rectangle.IsEmpty)
		{
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(46, 255, 255, 255)))
			{
				e.Graphics.FillRectangle(brush, track);
			}
			using SolidBrush brush2 = new SolidBrush(Color.FromArgb(dragging ? 225 : 155, Skin.GridSel));
			e.Graphics.FillRectangle(brush2, rectangle);
		}
	}

	protected override void OnMouseWheel(MouseEventArgs e)
	{
		if (RowCount > 0)
		{
			ScrollTo(FirstDisplayedScrollingRowIndex - Math.Sign(e.Delta) * 3);
		}
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		Rectangle track;
		Rectangle rectangle = ThumbRect(out track);
		if (!rectangle.IsEmpty && e.X >= track.X - 2 && e.Y >= track.Y)
		{
			dragging = true;
			dragOffset = (rectangle.Contains(e.Location) ? (e.Y - rectangle.Y) : (rectangle.Height / 2));
			DragTo(e.Y, track, rectangle.Height);
			base.Capture = true;
		}
		else
		{
			base.OnMouseDown(e);
		}
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		if (dragging)
		{
			Rectangle track;
			Rectangle rectangle = ThumbRect(out track);
			if (!rectangle.IsEmpty)
			{
				DragTo(e.Y, track, rectangle.Height);
			}
		}
		else
		{
			base.OnMouseMove(e);
		}
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		if (dragging)
		{
			dragging = false;
			Invalidate();
		}
		else
		{
			base.OnMouseUp(e);
		}
	}

	private void DragTo(int mouseY, Rectangle track, int thumbHeight)
	{
		int num = track.Height - thumbHeight;
		if (num > 0)
		{
			double num2 = (double)(mouseY - dragOffset - track.Y) / (double)num;
			ScrollTo((int)Math.Round(num2 * (double)MaxFirstRow()));
		}
	}
}