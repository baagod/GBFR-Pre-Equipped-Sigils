using System.IO;
using System.Windows;

namespace LoadoutTool;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // The mod launches us with --mod-dir <path>; default to our own folder.
        string modDir = AppContext.BaseDirectory;
        for (int i = 0; i < e.Args.Length - 1; i++)
        {
            if (e.Args[i] == "--mod-dir")
                modDir = Path.GetFullPath(e.Args[i + 1]);
        }

        TraitData.Load(Path.Combine(AppContext.BaseDirectory, "traits.json"));
        new MainWindow(modDir).Show();
    }
}
