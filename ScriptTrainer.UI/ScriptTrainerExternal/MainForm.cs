using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ScriptTrainerExternal;

internal sealed class MainForm : Form
{
	private sealed class ItemRow
	{
		public string IconFile { get; set; }

		public string 名称 { get; set; }

		public string ItemID { get; set; }

		public string NameID { get; set; }

		public string Type { get; set; }

		public string SubType { get; set; }

		public string IconPath { get; set; }
	}

	private readonly string gameRoot;

	private readonly string bepinexRoot;

	private readonly string commandPath;

	private readonly string responsePath;

	private readonly string itemCsvPath;

	private readonly TextBox amountBox = new TextBox();

	private readonly TextBox itemIdBox = new TextBox();

	private readonly TextBox itemCountBox = new TextBox();

	private readonly TextBox searchBox = new TextBox();

	private readonly Label statusLabel = new ChromeLabel();

	private readonly GlassGrid itemGrid = new GlassGrid();

	private readonly List<ItemRow> items = new List<ItemRow>();

	private readonly Dictionary<string, Image> iconCache = new Dictionary<string, Image>();

	private readonly Image emptyIcon = new Bitmap(32, 32);

	private Control commonPage;

	private Control itemsPage;

	private Control experimentalPage;

	private SkinButton navCommon;

	private SkinButton navItems;

	private SkinButton navExperimental;

	// 实验性功能：旧版神谕贴图目录
	private readonly TextBox relicDirBox = new TextBox();

	private SkinButton antiRegressButton;

	private Image bgImage;

	private string bgName;

	private readonly string skinCfgPath;

	private Bitmap backdropCache;

	private Size backdropClient;

	private Rectangle backdropBounds;

	private int bgVersion;

	private int backdropVersion = -1;

	protected override CreateParams CreateParams
	{
		get
		{
			// 无边框窗口保住最小化动画与任务栏行为
			CreateParams createParams = base.CreateParams;
			createParams.Style |= 131072;
			return createParams;
		}
	}

	public MainForm()
		: this(showItems: false)
	{
	}

	public MainForm(bool showItems)
	{
		gameRoot = FindGameRoot();
		bepinexRoot = Path.Combine(gameRoot, "BepInEx");
		commandPath = Path.Combine(bepinexRoot, "ScriptTrainer.commands");
		responsePath = Path.Combine(bepinexRoot, "ScriptTrainer.responses");
		itemCsvPath = Path.Combine(bepinexRoot, "item_ids.csv");
		skinCfgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ScriptTrainer.UI.skin.cfg");
		Skin.InitBackgrounds(bepinexRoot);
		LoadBackground();
		Text = "犹格索托斯的庭院 修改器";
		try
		{
			Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
		}
		catch
		{
		}
		base.StartPosition = FormStartPosition.CenterScreen;
		base.FormBorderStyle = FormBorderStyle.None;
		MinimumSize = new Size(820, 560);
		base.Size = new Size(960, 640);
		BackColor = Color.FromArgb(20, 18, 24);
		ForeColor = Skin.Text;
		Font = new Font("Microsoft YaHei UI", 9f);
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, value: true);
		BuildUi();
		SelectPage(showItems ? 1 : 0);
		LoadItems();
		ActiveControl = navCommon;
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
		DrawCover(e.Graphics);
	}

	// 插件首启导出的游戏 CG（BepInEx\ui_backgrounds\），运行时烘焙后等比铺满客户区、
	// 居中裁切；背景尚未提取时退成纯色底
	private void DrawCover(Graphics g)
	{
		Rectangle clientRectangle = base.ClientRectangle;
		if (clientRectangle.Width <= 0 || clientRectangle.Height <= 0)
		{
			return;
		}
		if (bgImage == null)
		{
			using SolidBrush brush = new SolidBrush(BackColor);
			g.FillRectangle(brush, clientRectangle);
			return;
		}
		g.InterpolationMode = InterpolationMode.HighQualityBilinear;
		double num = (double)bgImage.Width / (double)bgImage.Height;
		double num2 = (double)clientRectangle.Width / (double)clientRectangle.Height;
		Rectangle srcRect;
		if (num > num2)
		{
			int num3 = Math.Max(1, (int)((double)bgImage.Height * num2));
			srcRect = new Rectangle((bgImage.Width - num3) / 2, 0, num3, bgImage.Height);
		}
		else
		{
			int num4 = Math.Max(1, (int)((double)bgImage.Width / num2));
			srcRect = new Rectangle(0, (bgImage.Height - num4) / 2, bgImage.Width, num4);
		}
		g.DrawImage(bgImage, clientRectangle, srcRect, GraphicsUnit.Pixel);
	}

	// 给子控件画「窗体背景的对应区域」：整幅 cover 只在尺寸/位置/背景变了时预渲染进缓存位图，
	// 平时（包括滚动重绘的每个单元格）都只是从缓存里拷一块，避免实时缩放大图
	private void PaintBackdrop(Graphics g, Control c, Rectangle clip)
	{
		Point point = PointToClient(c.PointToScreen(Point.Empty));
		Rectangle rectangle = new Rectangle(point, c.Size);
		if (backdropCache == null || backdropClient != base.ClientSize || backdropBounds != rectangle || backdropVersion != bgVersion)
		{
			if (backdropCache != null)
			{
				backdropCache.Dispose();
			}
			backdropCache = new Bitmap(Math.Max(1, rectangle.Width), Math.Max(1, rectangle.Height));
			using (Graphics graphics = Graphics.FromImage(backdropCache))
			{
				graphics.TranslateTransform(-rectangle.X, -rectangle.Y);
				DrawCover(graphics);
			}
			backdropClient = base.ClientSize;
			backdropBounds = rectangle;
			backdropVersion = bgVersion;
		}
		g.DrawImage(backdropCache, clip, clip, GraphicsUnit.Pixel);
	}

	private void LoadBackground()
	{
		string text = "CG_Dragon_8";
		try
		{
			if (File.Exists(skinCfgPath))
			{
				string text2 = File.ReadAllText(skinCfgPath, Encoding.UTF8).Trim();
				if (text2.Length > 0)
				{
					text = text2;
				}
			}
		}
		catch
		{
		}
		string[] array = Skin.Backgrounds();
		if (array.Length == 0)
		{
			SetBackground(null, save: false);
			return;
		}
		if (Array.IndexOf(array, text) < 0)
		{
			text = ((Array.IndexOf(array, "CG_Dragon_8") >= 0) ? "CG_Dragon_8" : array[0]);
		}
		SetBackground(text, save: false);
	}

	private void SetBackground(string name, bool save)
	{
		bgName = name;
		bgImage = ((name == null) ? null : Skin.Bg(name));
		bgVersion++;
		Invalidate(invalidateChildren: true);
		if (save && name != null)
		{
			try
			{
				File.WriteAllText(skinCfgPath, name, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			}
			catch
			{
			}
		}
	}

	protected override void WndProc(ref Message m)
	{
		if (m.Msg == 132)
		{
			base.WndProc(ref m);
			if ((int)m.Result == 1)
			{
				int lParam = m.LParam.ToInt32();
				Point point = PointToClient(new Point((short)(lParam & 0xFFFF), (short)((lParam >> 16) & 0xFFFF)));
				m.Result = (IntPtr)HitTest(point);
			}
			return;
		}
		base.WndProc(ref m);
	}

	private int HitTest(Point p)
	{
		bool flag = p.X < 7;
		bool flag2 = p.X >= base.ClientSize.Width - 7;
		bool flag3 = p.Y < 7;
		bool flag4 = p.Y >= base.ClientSize.Height - 7;
		if (flag3 && flag)
		{
			return 13;
		}
		if (flag3 && flag2)
		{
			return 14;
		}
		if (flag4 && flag)
		{
			return 16;
		}
		if (flag4 && flag2)
		{
			return 17;
		}
		if (flag)
		{
			return 10;
		}
		if (flag2)
		{
			return 11;
		}
		if (flag3)
		{
			return 12;
		}
		if (flag4)
		{
			return 15;
		}
		if (p.Y < 48)
		{
			return 2;
		}
		return 1;
	}

	private string FindGameRoot()
	{
		string text = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
		if (Directory.Exists(Path.Combine(text, "BepInEx")))
		{
			return text;
		}
		string text2 = ((Directory.GetParent(text) != null) ? Directory.GetParent(text).FullName : text);
		if (Directory.Exists(Path.Combine(text2, "BepInEx")))
		{
			return text2;
		}
		return "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Yog-Sothoth's Yard";
	}

	private void BuildUi()
	{
		// 顶栏：徽章 + 标题 + 页签 + 最小化/关闭（Chrome* 控件命中穿透，顶栏区域可拖动窗口）
		Panel panel = new ChromePanel
		{
			Dock = DockStyle.Top,
			Height = 48,
			BackColor = Color.Transparent
		};
		base.Controls.Add(panel);
		PictureBox pictureBox = new ChromePicture
		{
			Image = Skin.Img("emblem"),
			SizeMode = PictureBoxSizeMode.Zoom,
			BackColor = Color.Transparent,
			Bounds = new Rectangle(12, 5, 38, 38)
		};
		panel.Controls.Add(pictureBox);
		Label label = new ChromeLabel
		{
			Text = "犹格索托斯的庭院 · 修改器",
			AutoSize = true,
			BackColor = Color.Transparent,
			ForeColor = Skin.Text,
			Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
			Location = new Point(56, 14)
		};
		panel.Controls.Add(label);
		navCommon = new SkinButton("m", "常用功能");
		navCommon.Bounds = new Rectangle(300, 9, 118, 30);
		navCommon.Click += delegate
		{
			SelectPage(0);
		};
		panel.Controls.Add(navCommon);
		navItems = new SkinButton("m", "获取物品");
		navItems.Bounds = new Rectangle(426, 9, 118, 30);
		navItems.Click += delegate
		{
			SelectPage(1);
		};
		panel.Controls.Add(navItems);
		navExperimental = new SkinButton("m", "实验性功能");
		navExperimental.Bounds = new Rectangle(552, 9, 118, 30);
		navExperimental.Click += delegate
		{
			SelectPage(2);
		};
		panel.Controls.Add(navExperimental);
		IconButton iconButton = new IconButton("btn_close")
		{
			Anchor = AnchorStyles.Top | AnchorStyles.Right
		};
		iconButton.Location = new Point(panel.Width - 46, 6);
		iconButton.Click += delegate
		{
			Close();
		};
		panel.Controls.Add(iconButton);
		// 背景选择：每次点开时重扫 ui_backgrounds（游戏首启导出可能发生在 UI 开着的时候），
		// 勾选当前项，选择写入 skin.cfg 记忆；尚未提取时给出提示项
		SkinButton bgButton = new SkinButton("m", "背景")
		{
			Anchor = AnchorStyles.Top | AnchorStyles.Right,
			Bounds = new Rectangle(panel.Width - 132, 9, 76, 30)
		};
		ContextMenuStrip bgMenu = new ContextMenuStrip
		{
			Renderer = new DarkMenuRenderer(),
			BackColor = Skin.GridRow,
			ForeColor = Skin.Text
		};
		RebuildBgMenu(bgMenu);
		bgMenu.Opening += delegate
		{
			RebuildBgMenu(bgMenu);
		};
		bgButton.Click += delegate
		{
			bgMenu.Show(bgButton, new Point(0, bgButton.Height));
		};
		panel.Controls.Add(bgButton);
		statusLabel.Dock = DockStyle.Bottom;
		statusLabel.Height = 32;
		statusLabel.TextAlign = ContentAlignment.MiddleLeft;
		statusLabel.Padding = new Padding(14, 0, 0, 0);
		statusLabel.BackColor = Skin.StatusBack;
		statusLabel.ForeColor = Skin.StatusText;
		statusLabel.Text = "启动游戏并进入存档后使用。";
		base.Controls.Add(statusLabel);
		Panel panel2 = new ChromePanel
		{
			Dock = DockStyle.Fill,
			BackColor = Color.Transparent,
			Padding = new Padding(14, 6, 14, 6)
		};
		base.Controls.Add(panel2);
		panel2.BringToFront();
		commonPage = BuildCommonPage();
		itemsPage = BuildItemsPage();
		experimentalPage = BuildExperimentalPage();
		panel2.Controls.Add(commonPage);
		panel2.Controls.Add(itemsPage);
		panel2.Controls.Add(experimentalPage);
	}

	private void RebuildBgMenu(ContextMenuStrip menu)
	{
		menu.Items.Clear();
		string[] array = Skin.Backgrounds();
		if (array.Length == 0)
		{
			menu.Items.Add(new ToolStripMenuItem("背景图未提取：先用 BepInEx 启动一次游戏")
			{
				Enabled = false,
				ForeColor = Skin.TextDim
			});
			return;
		}
		foreach (string text in array)
		{
			string name = text;
			ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem(Skin.BgLabel(name))
			{
				Tag = name,
				ForeColor = Skin.Text,
				Checked = name == bgName
			};
			toolStripMenuItem.Click += delegate
			{
				SetBackground(name, save: true);
			};
			menu.Items.Add(toolStripMenuItem);
		}
	}

	private void SelectPage(int index)
	{
		commonPage.Visible = index == 0;
		itemsPage.Visible = index == 1;
		experimentalPage.Visible = index == 2;
		navCommon.Checked = index == 0;
		navItems.Checked = index == 1;
		navExperimental.Checked = index == 2;
	}

	// 实验性功能页：目前只有「替换旧版神谕贴图」。
	// 把目录里 Relic 开头的 PNG 在运行时灌回游戏已加载的贴图（仅内存，重启恢复）。
	private Control BuildExperimentalPage()
	{
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 2,
			BackColor = Color.Transparent
		};
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(12, 10, 12, 6),
			BackColor = Color.Transparent,
			WrapContents = false
		};
		flowLayoutPanel.Controls.Add(Label("旧版贴图目录"));
		relicDirBox.Width = 460;
		relicDirBox.Text = DefaultRelicDir();
		Skin.StyleInput(relicDirBox);
		flowLayoutPanel.Controls.Add(relicDirBox);
		flowLayoutPanel.Controls.Add(Button("浏览...", delegate
		{
			using (FolderBrowserDialog dialog = new FolderBrowserDialog())
			{
				dialog.Description = "选择存放旧版 Relic*.png 的目录";
				if (Directory.Exists(relicDirBox.Text))
				{
					dialog.SelectedPath = relicDirBox.Text;
				}
				if (dialog.ShowDialog(this) == DialogResult.OK)
				{
					relicDirBox.Text = dialog.SelectedPath;
				}
			}
		}, 70));
		tableLayoutPanel.Controls.Add(flowLayoutPanel, 0, 0);
		TableLayoutPanel tableLayoutPanel2 = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(18, 18, 18, 18),
			ColumnCount = 1,
			RowCount = 2,
			BackColor = Color.Transparent
		};
		tableLayoutPanel2.RowCount = 3;
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		SkinButton replaceButton = new SkinButton("l", "替换旧版神谕贴图")
		{
			Anchor = AnchorStyles.Left | AnchorStyles.Top,
			Margin = new Padding(8),
			Width = 220
		};
		replaceButton.Click += delegate
		{
			string dir = relicDirBox.Text.Trim();
			if (dir.Length == 0)
			{
				SetStatus("请先填写旧版贴图目录");
				return;
			}
			if (!Directory.Exists(dir))
			{
				SetStatus("目录不存在: " + dir);
				return;
			}
			// 命令协议用 '|' 分隔字段，目录里出现竖线会被截断，这里直接拒绝
			if (dir.IndexOf('|') >= 0)
			{
				SetStatus("目录路径不能包含 '|'");
				return;
			}
			// 二十多张 512 图 + 两张 1024 图集解码回写，比普通命令慢，等久一点
			SendCommand("REPLACE_RELIC|" + dir, 15.0);
		};
		tableLayoutPanel2.Controls.Add(replaceButton, 0, 0);
		// 防回归开关：按钮按下态 = 已开启；状态以插件回复为准，插件不在线时按本地配置显示
		antiRegressButton = new SkinButton("l", "防回归：关")
		{
			Anchor = AnchorStyles.Left | AnchorStyles.Top,
			Margin = new Padding(8),
			Width = 220
		};
		antiRegressButton.Click += delegate
		{
			bool turnOn = !antiRegressButton.Checked;
			string reply = SendCommandForResult("ANTI_REGRESS|" + (turnOn ? "1" : "0"), 3.0);
			if (reply == null)
			{
				// 游戏没响应：直接写配置文件，插件下次启动会读到
				WriteExperimentalConfig("anti_regress", turnOn);
				SetStatus("游戏未响应，已写入配置文件，下次启动游戏生效。防回归：" + (turnOn ? "开" : "关"));
			}
			ApplyAntiRegressState(turnOn);
		};
		ApplyAntiRegressState(ReadExperimentalConfig("anti_regress"));
		tableLayoutPanel2.Controls.Add(antiRegressButton, 0, 1);
		Label tip = new Label
		{
			AutoSize = false,
			Dock = DockStyle.Fill,
			BackColor = Color.Transparent,
			ForeColor = Skin.TextDim,
			Margin = new Padding(8, 4, 8, 8),
			Text = "说明：\r\n" +
				"· 读取目录里 Relic 开头的 PNG（文件名去掉 _数字 后缀即精灵名，例如 Relic_DG.png、RelicBg_3936.png），" +
				"在运行时直接写进游戏已加载的贴图，界面与卡牌会立即换成旧版神谕美术。\r\n" +
				"· 需要游戏已启动并进入存档；只改内存，不动游戏文件，重启游戏即恢复原版。\r\n" +
				"· 发布包已自带一套旧版贴图，放在 BepInEx\\relic_override（默认目录）；目录留空时插件也退回这里。\r\n" +
				"· 实验性功能，若出现贴图错位或花屏，重启游戏即可。\r\n" +
				"\r\n防回归：开启后，白天/夜晚切换时若 SAN 为 0，不再触发回归结局，而是恢复 10 点 SAN，" +
				"并用游戏原生剧情界面弹出一段耶芙娜的对话（台词随机）。开关保存在 BepInEx\\ScriptTrainer.experimental.cfg，重启游戏仍生效。"
		};
		tableLayoutPanel2.Controls.Add(tip, 0, 2);
		tableLayoutPanel.Controls.Add(tableLayoutPanel2, 0, 1);
		return tableLayoutPanel;
	}

	// 默认目录：BepInEx\relic_override（旧版贴图随发布包一起分发在这里）
	private string DefaultRelicDir()
	{
		return Path.Combine(bepinexRoot, "relic_override");
	}

	private void ApplyAntiRegressState(bool on)
	{
		if (antiRegressButton == null)
		{
			return;
		}
		antiRegressButton.Checked = on;
		antiRegressButton.Text = on ? "防回归：开" : "防回归：关";
	}

	private string ExperimentalConfigPath => Path.Combine(bepinexRoot, "ScriptTrainer.experimental.cfg");

	private bool ReadExperimentalConfig(string key)
	{
		try
		{
			if (!File.Exists(ExperimentalConfigPath))
			{
				return false;
			}
			foreach (string raw in File.ReadAllLines(ExperimentalConfigPath))
			{
				string line = raw.Trim();
				int eq = line.IndexOf('=');
				if (eq > 0 && !line.StartsWith("#") && string.Equals(line.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
				{
					string v = line.Substring(eq + 1).Trim();
					return v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
				}
			}
		}
		catch
		{
		}
		return false;
	}

	private void WriteExperimentalConfig(string key, bool value)
	{
		try
		{
			List<string> lines = new List<string>();
			bool replaced = false;
			if (File.Exists(ExperimentalConfigPath))
			{
				foreach (string raw in File.ReadAllLines(ExperimentalConfigPath))
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
				lines.Add(key + "=" + (value ? "1" : "0"));
			}
			File.WriteAllLines(ExperimentalConfigPath, lines.ToArray());
		}
		catch (Exception ex)
		{
			SetStatus("写配置失败: " + ex.Message);
		}
	}

	private Control BuildCommonPage()
	{
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 2,
			BackColor = Color.Transparent
		};
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(12, 10, 12, 6),
			BackColor = Color.Transparent
		};
		flowLayoutPanel.Controls.Add(Label("数量"));
		amountBox.Width = 110;
		amountBox.Text = "10000";
		Skin.StyleInput(amountBox);
		flowLayoutPanel.Controls.Add(amountBox);
		flowLayoutPanel.Controls.Add(Button("/10", delegate
		{
			ChangeAmount(multiply: false);
		}, 54));
		flowLayoutPanel.Controls.Add(Button("x10", delegate
		{
			ChangeAmount(multiply: true);
		}, 54));
		flowLayoutPanel.Controls.Add(Button("±", delegate
		{
			NegateAmount();
		}, 40));
		flowLayoutPanel.Controls.Add(Button("刷新数值", delegate
		{
			SendCommand("VALUES");
		}, 90));
		flowLayoutPanel.Controls.Add(Button("导出物品", delegate
		{
			SendCommand("EXPORT_ITEMS");
			LoadItems();
		}, 90));
		tableLayoutPanel.Controls.Add(flowLayoutPanel, 0, 0);
		TableLayoutPanel tableLayoutPanel2 = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(18, 18, 18, 18),
			ColumnCount = 5,
			RowCount = 3,
			BackColor = Color.Transparent
		};
		for (int i = 0; i < 5; i++)
		{
			tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
		}
		for (int j = 0; j < 3; j++)
		{
			tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));
		}
		tableLayoutPanel.Controls.Add(tableLayoutPanel2, 0, 1);
		AddAttrButton(tableLayoutPanel2, "添加现金", "money", 0, 0);
		AddAttrButton(tableLayoutPanel2, "添加 San", "san", 1, 0);
		AddAttrButton(tableLayoutPanel2, "添加灵魂", "souls", 2, 0);
		AddAttrButton(tableLayoutPanel2, "清洁度", "clean", 3, 0);
		AddAttrButton(tableLayoutPanel2, "降低恶值", "evil", 4, 0);
		AddAttrButton(tableLayoutPanel2, "行动力", "action", 0, 1);
		AddAttrButton(tableLayoutPanel2, "耶芙娜", "dragon", 1, 1);
		AddAttrButton(tableLayoutPanel2, "小叶子", "maid", 2, 1);
		AddAttrButton(tableLayoutPanel2, "霞露零", "elf", 3, 1);
		AddAttrButton(tableLayoutPanel2, "特莉波卡", "death", 4, 1);
		return tableLayoutPanel;
	}

	private Control BuildItemsPage()
	{
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 2,
			BackColor = Color.Transparent
		};
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(12, 10, 12, 6),
			BackColor = Color.Transparent
		};
		flowLayoutPanel.Controls.Add(Label("搜索"));
		searchBox.Width = 210;
		Skin.StyleInput(searchBox);
		searchBox.TextChanged += delegate
		{
			FilterItems();
		};
		flowLayoutPanel.Controls.Add(searchBox);
		flowLayoutPanel.Controls.Add(Label("物品ID"));
		itemIdBox.Width = 90;
		itemIdBox.Text = "10013";
		Skin.StyleInput(itemIdBox);
		flowLayoutPanel.Controls.Add(itemIdBox);
		flowLayoutPanel.Controls.Add(Label("个数"));
		itemCountBox.Width = 70;
		itemCountBox.Text = "1";
		Skin.StyleInput(itemCountBox);
		flowLayoutPanel.Controls.Add(itemCountBox);
		flowLayoutPanel.Controls.Add(Button("检查", delegate
		{
			SendCommand("CHECK_ITEM|" + itemIdBox.Text.Trim());
		}, 70));
		flowLayoutPanel.Controls.Add(Button("添加", AddSelectedItem, 70));
		flowLayoutPanel.Controls.Add(Button("重载清单", LoadItems, 90));
		tableLayoutPanel.Controls.Add(flowLayoutPanel, 0, 0);
		itemGrid.Dock = DockStyle.Fill;
		itemGrid.ReadOnly = true;
		itemGrid.AllowUserToAddRows = false;
		itemGrid.AllowUserToDeleteRows = false;
		itemGrid.AllowUserToResizeColumns = false;
		itemGrid.AllowUserToResizeRows = false;
		itemGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
		itemGrid.MultiSelect = false;
		itemGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
		itemGrid.AutoGenerateColumns = false;
		itemGrid.RowTemplate.Height = 42;
		itemGrid.BorderStyle = BorderStyle.None;
		itemGrid.RowHeadersVisible = false;
		itemGrid.BackgroundColor = Skin.GridBack;
		itemGrid.GridColor = Skin.GridLine;
		itemGrid.DefaultCellStyle.BackColor = Skin.GridRow;
		itemGrid.DefaultCellStyle.ForeColor = Skin.Text;
		itemGrid.DefaultCellStyle.SelectionBackColor = Skin.GridSel;
		itemGrid.DefaultCellStyle.SelectionForeColor = Color.White;
		itemGrid.AlternatingRowsDefaultCellStyle.BackColor = Skin.GridRowAlt;
		itemGrid.AlternatingRowsDefaultCellStyle.SelectionBackColor = Skin.GridSel;
		itemGrid.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;
		itemGrid.ColumnHeadersDefaultCellStyle.BackColor = Skin.StatusBack;
		itemGrid.ColumnHeadersDefaultCellStyle.ForeColor = Skin.Text;
		itemGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Skin.StatusBack;
		itemGrid.ColumnHeadersHeight = 34;
		itemGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
		itemGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
		itemGrid.EnableHeadersVisualStyles = false;
		itemGrid.Backdrop = delegate(Graphics g, Rectangle r)
		{
			PaintBackdrop(g, itemGrid, r);
		};
		itemGrid.CellPainting += PaintGlassCell;
		itemGrid.Columns.Add(new DataGridViewImageColumn
		{
			Name = "图标",
			HeaderText = "图标",
			Width = 54,
			ImageLayout = DataGridViewImageCellLayout.Zoom,
			DefaultCellStyle =
			{
				NullValue = null
			}
		});
		itemGrid.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = "名称",
			HeaderText = "名称",
			DataPropertyName = "名称",
			FillWeight = 180f
		});
		itemGrid.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = "ItemID",
			HeaderText = "ItemID",
			DataPropertyName = "ItemID",
			FillWeight = 70f
		});
		itemGrid.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = "NameID",
			HeaderText = "NameID",
			DataPropertyName = "NameID",
			FillWeight = 70f
		});
		itemGrid.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = "Type",
			HeaderText = "Type",
			DataPropertyName = "Type",
			FillWeight = 120f
		});
		itemGrid.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = "SubType",
			HeaderText = "SubType",
			DataPropertyName = "SubType",
			FillWeight = 110f
		});
		itemGrid.DataBindingComplete += delegate
		{
			FormatItemGrid();
		};
		itemGrid.CellFormatting += FormatIconCell;
		itemGrid.CellDoubleClick += delegate
		{
			AddSelectedItem();
		};
		tableLayoutPanel.Controls.Add(itemGrid, 0, 1);
		return tableLayoutPanel;
	}

	private Label Label(string text)
	{
		Label label = new Label();
		label.Text = text;
		label.AutoSize = true;
		label.BackColor = Color.Transparent;
		label.ForeColor = Skin.Text;
		label.TextAlign = ContentAlignment.MiddleCenter;
		label.Padding = new Padding(0, 6, 0, 0);
		return label;
	}

	private SkinButton Button(string text, Action action, int width)
	{
		SkinButton skinButton = new SkinButton("m", text)
		{
			Width = width,
			Height = 30
		};
		skinButton.Click += delegate
		{
			action();
		};
		return skinButton;
	}

	private void AddAttrButton(TableLayoutPanel panel, string text, string attr, int col, int row)
	{
		SkinButton skinButton = new SkinButton("l", text)
		{
			Dock = DockStyle.Fill,
			Margin = new Padding(8)
		};
		skinButton.Click += delegate
		{
			SendCommand("ATTR|" + attr + "|" + Amount());
		};
		panel.Controls.Add(skinButton, col, row);
	}

	// 数量允许负数：负数发给插件后走反向效果（加钱变扣钱、降恶值变加恶值）；0 或非法输入按 1 处理
	private long Amount()
	{
		if (!long.TryParse(amountBox.Text.Trim(), out var result) || result == 0)
		{
			return 1L;
		}
		return result;
	}

	private void ChangeAmount(bool multiply)
	{
		long num = Amount();
		long sign = (num < 0) ? -1L : 1L;
		long mag = Math.Abs(num);
		mag = (multiply ? (mag * 10) : Math.Max(1L, mag / 10));
		amountBox.Text = (sign * mag).ToString();
	}

	private void NegateAmount()
	{
		amountBox.Text = (-Amount()).ToString();
	}

	// 普通/[特]神谕（E_Relic，20000-21999）直接添加会损坏存档，插件端已硬拦，
	// UI 端同步屏蔽：不进列表、不发命令；[至高]系列（E_SeniorRelic，22000+）走原生创建不受影响
	private static bool IsBlockedOracle(ItemRow row)
	{
		return string.Equals(row.Type, "E_Relic", StringComparison.OrdinalIgnoreCase);
	}

	private void AddSelectedItem()
	{
		if (itemGrid.CurrentRow != null && itemGrid.CurrentRow.Cells["ItemID"].Value != null)
		{
			itemIdBox.Text = itemGrid.CurrentRow.Cells["ItemID"].Value.ToString();
		}
		string idText = itemIdBox.Text.Trim();
		if (int.TryParse(idText, out var id))
		{
			ItemRow itemRow = items.FirstOrDefault((ItemRow r) => r.ItemID == idText);
			if ((itemRow != null && IsBlockedOracle(itemRow)) || (itemRow == null && id >= 20000 && id < 22000))
			{
				SetStatus("已屏蔽：普通神谕（20000-21999）直接添加会损坏存档；[至高]系列（22000+）可正常添加。");
				return;
			}
		}
		SendCommand("ADD_ITEM|" + idText + "|" + itemCountBox.Text.Trim());
	}

	private void SendCommand(string command)
	{
		SendCommand(command, 3.0);
	}

	// 同 SendCommand，但把游戏回复返回给调用方；超时/发送失败返回 null
	private string SendCommandForResult(string command, double timeoutSeconds)
	{
		if (!Directory.Exists(bepinexRoot))
		{
			SetStatus("未找到 BepInEx 目录: " + bepinexRoot);
			return null;
		}
		string id = DateTime.Now.Ticks.ToString();
		try
		{
			File.AppendAllText(commandPath, id + "|" + command + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			SetStatus("已发送命令，等待游戏响应...");
		}
		catch (Exception ex)
		{
			SetStatus("发送失败: " + ex.Message);
			return null;
		}
		DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);
		while (DateTime.Now < deadline)
		{
			Application.DoEvents();
			Thread.Sleep(80);
			string reply = TryReadResponse(id);
			if (reply != null)
			{
				SetStatus(reply);
				return reply;
			}
		}
		return null;
	}

	private void SendCommand(string command, double timeoutSeconds)
	{
		if (!Directory.Exists(bepinexRoot))
		{
			SetStatus("未找到 BepInEx 目录: " + bepinexRoot);
			return;
		}
		string text = DateTime.Now.Ticks.ToString();
		try
		{
			File.AppendAllText(commandPath, text + "|" + command + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			SetStatus("已发送命令，等待游戏响应...");
		}
		catch (Exception ex)
		{
			SetStatus("发送失败: " + ex.Message);
			return;
		}
		DateTime dateTime = DateTime.Now.AddSeconds(timeoutSeconds);
		while (DateTime.Now < dateTime)
		{
			Application.DoEvents();
			Thread.Sleep(80);
			string text2 = TryReadResponse(text);
			if (text2 != null)
			{
				SetStatus(text2);
				return;
			}
		}
		SetStatus("命令已写入；如果未响应，请确认游戏已启动并进入存档。最近命令: " + command);
	}

	private string TryReadResponse(string id)
	{
		if (!File.Exists(responsePath))
		{
			return null;
		}
		try
		{
			foreach (string item in File.ReadAllLines(responsePath, Encoding.UTF8).Reverse())
			{
				if (item.StartsWith(id + "|", StringComparison.Ordinal))
				{
					return item.Substring(id.Length + 1);
				}
			}
		}
		catch
		{
			return null;
		}
		return null;
	}

	private void LoadItems()
	{
		items.Clear();
		if (!File.Exists(itemCsvPath))
		{
			FilterItems();
			SetStatus("未找到物品清单，请进游戏后按 Ctrl+F12 或点击导出物品。路径: " + itemCsvPath);
			return;
		}
		foreach (string item in File.ReadAllLines(itemCsvPath, Encoding.UTF8).Skip(1))
		{
			string[] array = SplitCsv(item);
			if (array.Length >= 5)
			{
				bool flag = array.Length >= 6 && !IsNumeric(array[2]);
				bool flag2 = array.Length >= 7 && flag;
				string itemID = array[0];
				string text = array[1];
				string text2 = (flag ? array[2] : string.Empty);
				string type = (flag ? array[3] : array[2]);
				string subType = (flag ? array[4] : array[3]);
				string iconPath = (flag ? array[5] : array[4]);
				string iconFile = (flag2 ? array[6] : string.Empty);
				items.Add(new ItemRow
				{
					IconFile = iconFile,
					名称 = (string.IsNullOrWhiteSpace(text2) ? ("未解析名称 " + text) : text2),
					ItemID = itemID,
					NameID = text,
					Type = type,
					SubType = subType,
					IconPath = iconPath
				});
			}
		}
		FilterItems();
		SetStatus("已加载物品清单: " + items.Count + " 个物品。双击物品可添加。");
	}

	private void FilterItems()
	{
		string keyword = searchBox.Text.Trim();
		List<ItemRow> dataSource = items.Where((ItemRow i) => !IsBlockedOracle(i) && (keyword.Length == 0 || i.ItemID.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 || i.NameID.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 || i.名称.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 || i.Type.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 || i.IconPath.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)).Take(300).ToList();
		itemGrid.DataSource = dataSource;
	}

	private void FormatItemGrid()
	{
		itemGrid.ClearSelection();
	}

	// 半透明单元格：先铺窗体 CG，再叠色，最后画内容和网格线
	private void PaintGlassCell(object sender, DataGridViewCellPaintingEventArgs e)
	{
		PaintBackdrop(e.Graphics, itemGrid, e.CellBounds);
		Color color = ((e.RowIndex < 0) ? Color.FromArgb(205, Skin.StatusBack) : (((e.State & DataGridViewElementStates.Selected) != 0) ? Color.FromArgb(205, Skin.GridSel) : Color.FromArgb((e.RowIndex % 2 == 0) ? 132 : 110, Skin.GridRow)));
		using (SolidBrush brush = new SolidBrush(color))
		{
			e.Graphics.FillRectangle(brush, e.CellBounds);
		}
		e.Paint(e.ClipBounds, DataGridViewPaintParts.Border | DataGridViewPaintParts.ContentForeground);
		e.Handled = true;
	}

	private void FormatIconCell(object sender, DataGridViewCellFormattingEventArgs e)
	{
		if (itemGrid.Columns[e.ColumnIndex].Name != "图标" || e.RowIndex < 0 || e.RowIndex >= itemGrid.Rows.Count)
		{
			return;
		}
		if (!(itemGrid.Rows[e.RowIndex].DataBoundItem is ItemRow itemRow) || string.IsNullOrWhiteSpace(itemRow.IconFile))
		{
			e.Value = emptyIcon;
			e.FormattingApplied = true;
			return;
		}
		string text = Path.Combine(bepinexRoot, itemRow.IconFile);
		if (!iconCache.TryGetValue(text, out var value))
		{
			if (!File.Exists(text))
			{
				e.Value = emptyIcon;
				e.FormattingApplied = true;
				return;
			}
			try
			{
				using Image original = Image.FromFile(text);
				value = new Bitmap(original);
			}
			catch
			{
				value = emptyIcon;
			}
			iconCache[text] = value;
		}
		e.Value = value;
		e.FormattingApplied = true;
	}

	private static bool IsNumeric(string value)
	{
		long result;
		return long.TryParse(value, out result);
	}

	private static string[] SplitCsv(string line)
	{
		List<string> list = new List<string>();
		StringBuilder stringBuilder = new StringBuilder();
		bool flag = false;
		for (int i = 0; i < line.Length; i++)
		{
			char c = line[i];
			switch (c)
			{
			case '"':
				if (flag && i + 1 < line.Length && line[i + 1] == '"')
				{
					stringBuilder.Append('"');
					i++;
				}
				else
				{
					flag = !flag;
				}
				continue;
			case ',':
				if (!flag)
				{
					list.Add(stringBuilder.ToString());
					stringBuilder.Length = 0;
					continue;
				}
				break;
			}
			stringBuilder.Append(c);
		}
		list.Add(stringBuilder.ToString());
		return list.ToArray();
	}

	private void SetStatus(string text)
	{
		statusLabel.Text = text;
	}
}
