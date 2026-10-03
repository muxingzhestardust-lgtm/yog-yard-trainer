using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

internal static class Program
{
    private const string Header = "ItemID,NameID,Name,Type,SubType,Icon,IconFile\r\n";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Type FormType = Assembly.Load("ScriptTrainer.UI").GetType("ScriptTrainerExternal.MainForm", true);
    private static int checks;

    [STAThread]
    private static int Main(string[] args)
    {
        // Run only in a fresh build output: this directory is an isolated fake game root.
        string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BepInEx");
        if (Directory.Exists(root))
        {
            Console.Error.WriteLine("Use a fresh build output directory; BepInEx already exists: " + root);
            return 1;
        }
        Directory.CreateDirectory(root);
        string csv = Path.Combine(root, "item_ids.csv");
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (Form form = (Form)Activator.CreateInstance(FormType, new object[] { true }))
            {
                Equal(0, Rows(form).Count, "missing file gives an empty list");
                Require(Status(form).Contains("未找到物品清单"), "missing file status");

                StringBuilder fixture = new StringBuilder(Header);
                for (int i = 0; i < 1500; i++)
                    fixture.AppendFormat("{0},{1},bulk-{2},E_MazeCommon,E_Package,,\r\n", 50000 + i, 60000 + i, i);
                fixture.Append("20000,200001,blocked,E_Relic,E_Package,,\r\n");
                fixture.Append("43001,430011,\"第一行,\"\"引号\"\"\r\n\r\n第二行\",E_MazeCommon,E_NotShow,,\r\n");
                fixture.Append("43002,430021,\"开头\n结尾\",E_MazeCommon,E_NotShow,,\r\n");
                File.WriteAllText(csv, fixture.ToString(), Encoding.UTF8);
                Reload(form);
                Equal(1503, Loaded(form).Count, "all CSV records loaded");
                Equal(1502, Rows(form).Count, "unfiltered results exceed 300 and 1000");
                Require(Status(form).Contains("1503") && Status(form).Contains("1502") && Status(form).Contains("屏蔽 1"), "separate loaded/displayed/blocked counts");
                Require(!Rows(form).Cast<object>().Any(x => Property(x, "ItemID") == "20000"), "ordinary oracle stays blocked");
                string multiline = Property(Rows(form).Cast<object>().Single(x => Property(x, "ItemID") == "43001"), "名称");
                Equal("第一行,\"引号\"\n\n第二行", multiline.Replace("\r\n", "\n"), "multiline name, blank line, comma and escaped quotes preserved");

                Search(form, "bulk-");
                Equal(1500, Rows(form).Count, "search results have no cap");
                Search(form, "51499");
                Equal("51499", Property(Rows(form)[0], "ItemID"), "last bulk item remains searchable");
                Search(form, "第二行");
                Equal(1, Rows(form).Count, "search finds continuation of multiline field");
                Search(form, "no-such-item");
                Equal(0, Rows(form).Count, "no search matches");
                Require(Status(form).Contains("当前显示: 0"), "search updates status");
                Search(form, "");
                Equal(1502, Rows(form).Count, "clearing search restores all rows");

                File.WriteAllText(csv, "ItemID,NameID,Type,SubType,Icon\r\n10001,100011,E_MazeCommon,E_Package,icon/path\r\n", Encoding.UTF8);
                Reload(form);
                Equal(1, Rows(form).Count, "legacy five-column CSV remains supported");
                Equal("未解析名称 100011", Property(Rows(form)[0], "名称"), "legacy name fallback");
                Equal("E_MazeCommon", Property(Rows(form)[0], "Type"), "legacy columns stay aligned");

                File.WriteAllText(csv, Header, Encoding.UTF8);
                Reload(form);
                Equal(0, Loaded(form).Count, "header-only file clears old items");
                Equal(0, Rows(form).Count, "header-only file clears grid");
                File.WriteAllText(csv, "", Encoding.UTF8);
                Reload(form);
                Equal(0, Rows(form).Count, "empty file");

                if (args.Length == 3)
                {
                    File.Copy(args[0], csv, true);
                    Reload(form);
                    Equal(int.Parse(args[1]), Loaded(form).Count, "real catalog loaded count");
                    Equal(int.Parse(args[2]), Rows(form).Count, "real catalog displayed count");
                    foreach (string id in new[] { "30120", "30121", "42049", "43001", "43009" })
                        Require(Rows(form).Cast<object>().Any(x => Property(x, "ItemID") == id), "real catalog contains " + id);
                    Search(form, "43009");
                    Equal(1, Rows(form).Count, "last real record is searchable");
                    Search(form, "");
                    Console.WriteLine(Status(form));
                }
                else if (args.Length != 0)
                    throw new ArgumentException("Optional arguments: <real CSV path> <expected loaded> <expected displayed>");
                Require(!File.Exists(Path.Combine(root, "ScriptTrainer.commands")), "tests send no game commands");
            }
            Console.WriteLine("PASS: " + checks + " checks against the compiled UI.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static object Field(Form form, string name) => FormType.GetField(name, PrivateInstance).GetValue(form);
    private static IList Loaded(Form form) => (IList)Field(form, "items");
    private static IList Rows(Form form) => (IList)((DataGridView)Field(form, "itemGrid")).DataSource;
    private static string Status(Form form) => ((Label)Field(form, "statusLabel")).Text;
    private static string Property(object row, string name) => (string)row.GetType().GetProperty(name).GetValue(row, null);
    private static void Reload(Form form) => FormType.GetMethod("LoadItems", PrivateInstance).Invoke(form, null);
    private static void Search(Form form, string keyword) => ((TextBox)Field(form, "searchBox")).Text = keyword;
    private static void Equal(object expected, object actual, string description) => Require(object.Equals(expected, actual), description + " (expected " + expected + ", got " + actual + ")");
    private static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        checks++;
    }
}
