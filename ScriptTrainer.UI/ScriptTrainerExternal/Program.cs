using System;
using System.Windows.Forms;

namespace ScriptTrainerExternal;

internal static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		Application.Run(new MainForm(args.Length > 0 && args[0] == "--items"));
	}
}
