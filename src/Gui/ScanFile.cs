using System.IO;
using Core;

namespace Gui;

// The scan script comes from Core (the same code every generated script runs at the end).
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
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, ScanLisp.Script);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Couldn't write it (e.g. open elsewhere): the copy from last time still works
        }
    }
}
