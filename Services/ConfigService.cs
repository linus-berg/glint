using System;
using System.IO;
using Tomlyn;
using Tomlyn.Model;

namespace Glint.Services;

public class GlintConfig
{
    public string SaveDir { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
    public string DefaultTool { get; set; } = "Freehand";
    public int DefaultColorIndex { get; set; } = 0;
    public int DefaultStrokeIndex { get; set; } = 1;
    public string[]? Palette { get; set; } = null;
}

public static class ConfigService
{
    public static GlintConfig Current { get; private set; } = new GlintConfig();

    public static void LoadConfig()
    {
        var configPath = GetConfigPath();
        if (!File.Exists(configPath))
        {
            CreateDefaultConfig(configPath);
        }

        try
        {
            var tomlString = File.ReadAllText(configPath);
            var loadedConfig = TomlSerializer.Deserialize<GlintConfig>(tomlString);
            if (loadedConfig != null)
            {
                if (loadedConfig.SaveDir?.StartsWith("~/") == true)
                {
                    loadedConfig.SaveDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), loadedConfig.SaveDir.Substring(2));
                }
                
                // Ensure array length is 8 if provided
                if (loadedConfig.Palette != null)
                {
                    var p = new string[8];
                    for (int i = 0; i < 8; i++)
                    {
                        if (i < loadedConfig.Palette.Length && loadedConfig.Palette[i] is string sCol)
                            p[i] = sCol;
                        else
                            p[i] = "#ffffff";
                    }
                    loadedConfig.Palette = p;
                }
                
                // Clamp indices
                loadedConfig.DefaultColorIndex = Math.Clamp(loadedConfig.DefaultColorIndex, 0, 7);
                loadedConfig.DefaultStrokeIndex = Math.Clamp(loadedConfig.DefaultStrokeIndex, 0, 3);
                
                Current = loadedConfig;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading config: {ex.Message}");
        }
    }

    private static string GetConfigPath()
    {
        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrEmpty(xdgConfigHome))
        {
            xdgConfigHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }
        
        var dir = Path.Combine(xdgConfigHome, "glint");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        
        return Path.Combine(dir, "config.toml");
    }

    private static void CreateDefaultConfig(string path)
    {
        var toml = @"# Glint Configuration File

# Directory to save quick screenshots
# Can use ~/ for home directory
save_dir = ""~/Desktop""

# Default selected tool on startup
# Options: Freehand, Arrow, Rectangle, Ellipse, Highlighter, Text, Step, Blur, Redaction, Loupe
default_tool = ""Freehand""

# Default color index (0-7)
default_color_index = 0

# Default stroke width index (0-3)
default_stroke_index = 1

# Optional custom 8-color palette (hex codes). Uncomment to use!
# palette = [""#FF3B30"", ""#FF9500"", ""#FFCC00"", ""#34C759"", ""#007AFF"", ""#AF52DE"", ""#FFFFFF"", ""#000000""]
";
        File.WriteAllText(path, toml);
    }
}
