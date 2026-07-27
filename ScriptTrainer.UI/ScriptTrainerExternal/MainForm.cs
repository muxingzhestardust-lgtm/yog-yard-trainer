using System;
using System.Collections.Generic;
using System.Drawing;
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

	private readonly Label statusLabel = new Label();

	private readonly DataGridView itemGrid = new DataGridView();

	private readonly List<ItemRow> items = new List<ItemRow>();

	private readonly Dictionary<string, Image> iconCache = new Dictionary<string, Image>();

	private readonly Image emptyIcon = new Bitmap(32, 32);

	public MainForm()
	{
		gameRoot = FindGameRoot();
		bepinexRoot = Path.Combine(gameRoot, "BepInEx");
		commandPath = Path.Combine(bepinexRoot, "ScriptTrainer.commands");
		responsePath = Path.Combine(bepinexRoot, "ScriptTrainer.responses");
		itemCsvPath = Path.Combine(bepinexRoot, "item_ids.csv");
		Text = "犹格索托斯的庭院 修改器";
		base.StartPosition = FormStartPosition.CenterScreen;
		MinimumSize = new Size(820, 560);
		base.Size = new Size(920, 620);
		BackColor = Color.FromArgb(45, 45, 48);
		ForeColor = Color.White;
		Font = new Font("Microsoft YaHei UI", 9f);
		BuildUi();
		LoadItems();
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
		TabControl tabControl = new TabControl();
		tabControl.Dock = DockStyle.Fill;
		TabControl tabControl2 = tabControl;
		TabPage tabPage = new TabPage("常用功能");
		tabPage.BackColor = Color.FromArgb(66, 66, 66);
		tabPage.ForeColor = Color.White;
		TabPage tabPage2 = tabPage;
		TabPage tabPage3 = new TabPage("获取物品");
		tabPage3.BackColor = Color.FromArgb(66, 66, 66);
		tabPage3.ForeColor = Color.White;
		TabPage tabPage4 = tabPage3;
		tabControl2.TabPages.Add(tabPage2);
		tabControl2.TabPages.Add(tabPage4);
		base.Controls.Add(tabControl2);
		statusLabel.Dock = DockStyle.Bottom;
		statusLabel.Height = 34;
		statusLabel.TextAlign = ContentAlignment.MiddleLeft;
		statusLabel.BackColor = Color.FromArgb(30, 30, 30);
		statusLabel.ForeColor = Color.FromArgb(180, 255, 180);
		statusLabel.Text = "启动游戏并进入存档后使用。";
		base.Controls.Add(statusLabel);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
		tableLayoutPanel.Dock = DockStyle.Fill;
		tableLayoutPanel.ColumnCount = 1;
		tableLayoutPanel.RowCount = 2;
		tableLayoutPanel.BackColor = Color.FromArgb(66, 66, 66);
		TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		tabPage2.Controls.Add(tableLayoutPanel2);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
		flowLayoutPanel.Dock = DockStyle.Fill;
		flowLayoutPanel.Padding = new Padding(12, 10, 12, 6);
		flowLayoutPanel.BackColor = Color.FromArgb(66, 66, 66);
		FlowLayoutPanel flowLayoutPanel2 = flowLayoutPanel;
		flowLayoutPanel2.Controls.Add(Label("数量"));
		amountBox.Width = 110;
		amountBox.Text = "10000";
		flowLayoutPanel2.Controls.Add(amountBox);
		flowLayoutPanel2.Controls.Add(Button("/10", delegate
		{
			ChangeAmount(multiply: false);
		}, 54));
		flowLayoutPanel2.Controls.Add(Button("x10", delegate
		{
			ChangeAmount(multiply: true);
		}, 54));
		flowLayoutPanel2.Controls.Add(Button("刷新数值", delegate
		{
			SendCommand("VALUES");
		}, 90));
		flowLayoutPanel2.Controls.Add(Button("导出物品", delegate
		{
			SendCommand("EXPORT_ITEMS");
			LoadItems();
		}, 90));
		tableLayoutPanel2.Controls.Add(flowLayoutPanel2, 0, 0);
		TableLayoutPanel tableLayoutPanel3 = new TableLayoutPanel();
		tableLayoutPanel3.Dock = DockStyle.Fill;
		tableLayoutPanel3.Padding = new Padding(18, 18, 18, 18);
		tableLayoutPanel3.ColumnCount = 5;
		tableLayoutPanel3.RowCount = 3;
		TableLayoutPanel tableLayoutPanel4 = tableLayoutPanel3;
		for (int i = 0; i < 5; i++)
		{
			tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
		}
		for (int j = 0; j < 3; j++)
		{
			tableLayoutPanel4.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));
		}
		tableLayoutPanel2.Controls.Add(tableLayoutPanel4, 0, 1);
		AddAttrButton(tableLayoutPanel4, "添加现金", "money", 0, 0);
		AddAttrButton(tableLayoutPanel4, "添加 San", "san", 1, 0);
		AddAttrButton(tableLayoutPanel4, "添加灵魂", "souls", 2, 0);
		AddAttrButton(tableLayoutPanel4, "清洁度", "clean", 3, 0);
		AddAttrButton(tableLayoutPanel4, "降低恶值", "evil", 4, 0);
		AddAttrButton(tableLayoutPanel4, "行动力", "action", 0, 1);
		AddAttrButton(tableLayoutPanel4, "耶芙娜", "dragon", 1, 1);
		AddAttrButton(tableLayoutPanel4, "小叶子", "maid", 2, 1);
		AddAttrButton(tableLayoutPanel4, "霞露零", "elf", 3, 1);
		AddAttrButton(tableLayoutPanel4, "特莉波卡", "death", 4, 1);
		TableLayoutPanel tableLayoutPanel5 = new TableLayoutPanel();
		tableLayoutPanel5.Dock = DockStyle.Fill;
		tableLayoutPanel5.ColumnCount = 1;
		tableLayoutPanel5.RowCount = 2;
		tableLayoutPanel5.BackColor = Color.FromArgb(66, 66, 66);
		TableLayoutPanel tableLayoutPanel6 = tableLayoutPanel5;
		tableLayoutPanel6.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));
		tableLayoutPanel6.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		tabPage4.Controls.Add(tableLayoutPanel6);
		FlowLayoutPanel flowLayoutPanel3 = new FlowLayoutPanel();
		flowLayoutPanel3.Dock = DockStyle.Fill;
		flowLayoutPanel3.Padding = new Padding(12, 10, 12, 6);
		flowLayoutPanel3.BackColor = Color.FromArgb(66, 66, 66);
		FlowLayoutPanel flowLayoutPanel4 = flowLayoutPanel3;
		flowLayoutPanel4.Controls.Add(Label("搜索"));
		searchBox.Width = 210;
		searchBox.TextChanged += delegate
		{
			FilterItems();
		};
		flowLayoutPanel4.Controls.Add(searchBox);
		flowLayoutPanel4.Controls.Add(Label("物品ID"));
		itemIdBox.Width = 90;
		itemIdBox.Text = "10013";
		flowLayoutPanel4.Controls.Add(itemIdBox);
		flowLayoutPanel4.Controls.Add(Label("个数"));
		itemCountBox.Width = 70;
		itemCountBox.Text = "1";
		flowLayoutPanel4.Controls.Add(itemCountBox);
		flowLayoutPanel4.Controls.Add(Button("检查", delegate
		{
			SendCommand("CHECK_ITEM|" + itemIdBox.Text.Trim());
		}, 70));
		flowLayoutPanel4.Controls.Add(Button("添加", AddSelectedItem, 70));
		flowLayoutPanel4.Controls.Add(Button("重载清单", LoadItems, 90));
		tableLayoutPanel6.Controls.Add(flowLayoutPanel4, 0, 0);
		itemGrid.Dock = DockStyle.Fill;
		itemGrid.ReadOnly = true;
		itemGrid.AllowUserToAddRows = false;
		itemGrid.AllowUserToDeleteRows = false;
		itemGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
		itemGrid.MultiSelect = false;
		itemGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
		itemGrid.AutoGenerateColumns = false;
		itemGrid.RowTemplate.Height = 42;
		itemGrid.BackgroundColor = Color.FromArgb(45, 45, 48);
		itemGrid.DefaultCellStyle.BackColor = Color.FromArgb(55, 55, 58);
		itemGrid.DefaultCellStyle.ForeColor = Color.White;
		itemGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(83, 109, 254);
		itemGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 30, 30);
		itemGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
		itemGrid.EnableHeadersVisualStyles = false;
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
		tableLayoutPanel6.Controls.Add(itemGrid, 0, 1);
	}

	private Label Label(string text)
	{
		Label label = new Label();
		label.Text = text;
		label.AutoSize = true;
		label.ForeColor = Color.White;
		label.TextAlign = ContentAlignment.MiddleCenter;
		label.Padding = new Padding(0, 6, 0, 0);
		return label;
	}

	private Button Button(string text, Action action, int width)
	{
		Button button = new Button();
		button.Text = text;
		button.Width = width;
		button.Height = 30;
		button.BackColor = Color.FromArgb(140, 158, 255);
		button.ForeColor = Color.Black;
		button.FlatStyle = FlatStyle.Flat;
		Button button2 = button;
		button2.FlatAppearance.BorderSize = 0;
		button2.Click += delegate
		{
			action();
		};
		return button2;
	}

	private void AddAttrButton(TableLayoutPanel panel, string text, string attr, int col, int row)
	{
		Button button = Button(text, delegate
		{
			SendCommand("ATTR|" + attr + "|" + Amount());
		}, 120);
		button.Dock = DockStyle.Fill;
		button.Margin = new Padding(8);
		panel.Controls.Add(button, col, row);
	}

	private long Amount()
	{
		if (!long.TryParse(amountBox.Text.Trim(), out var result) || result <= 0)
		{
			return 1L;
		}
		return result;
	}

	private void ChangeAmount(bool multiply)
	{
		long num = Amount();
		num = (multiply ? (num * 10) : Math.Max(1L, num / 10));
		amountBox.Text = num.ToString();
	}

	private void AddSelectedItem()
	{
		if (itemGrid.CurrentRow != null && itemGrid.CurrentRow.Cells["ItemID"].Value != null)
		{
			itemIdBox.Text = itemGrid.CurrentRow.Cells["ItemID"].Value.ToString();
		}
		SendCommand("ADD_ITEM|" + itemIdBox.Text.Trim() + "|" + itemCountBox.Text.Trim());
	}

	private void SendCommand(string command)
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
		DateTime dateTime = DateTime.Now.AddSeconds(3.0);
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
		List<ItemRow> dataSource = items.Where((ItemRow i) => keyword.Length == 0 || i.ItemID.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 || i.NameID.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 || i.名称.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 || i.Type.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 || i.IconPath.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0).Take(300).ToList();
		itemGrid.DataSource = dataSource;
	}

	private void FormatItemGrid()
	{
		itemGrid.ClearSelection();
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
