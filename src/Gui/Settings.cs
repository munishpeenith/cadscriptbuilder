using System.IO;
using System.Text.Json;

namespace Gui;

// The user's choices from the ⚙ panel, kept in Documents\CADScriptBuilder\settings.json.
// Like a Python dataclass saved with json.dump: load once, change, save.
public class Settings
{
    // Blocks the user has hidden. We store the hidden ones (not the shown ones),
    // so any new block added in a later version shows up by default.
    public List<string> HiddenBlocks { get; set; } = [];

    // Clear the steps by itself once a script has run in Civil 3D
    public bool ClearAfterRun { get; set; } = false;

    // Open the window pinned on top of Civil 3D
    public bool StartPinned { get; set; } = true;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "CADScriptBuilder", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // A missing or broken file just means "use the defaults"
    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
        }
        return new Settings();
    }

    // Saving never crashes the app; at worst the choice isn't remembered next time
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
