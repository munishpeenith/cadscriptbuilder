using System.IO;
using System.Reflection;

namespace Gui;

// The scan script lives inside the .exe (an embedded resource, like a file baked into a Python zipapp).
// Each time the app starts, it writes a fresh copy to Documents\CADScriptBuilder\csb_scan.lsp,
// and the SCAN tiles drag that copy. So the .exe works on its own, and the copy is never stale.
public static class ScanFile
{
    public static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "CADScriptBuilder", "csb_scan.lsp");

    public static void Write()
    {
        try
        {
            using var script = Assembly.GetExecutingAssembly().GetManifestResourceStream("csb_scan.lsp");
            if (script is null)
            {
                return;
            }

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            using var file = File.Create(Path);
            script.CopyTo(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Couldn't write it (e.g. open elsewhere): the copy from last time still works
        }
    }
}
