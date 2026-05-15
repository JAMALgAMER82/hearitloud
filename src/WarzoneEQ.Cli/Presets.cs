using System.Text.Json;
using System.Text.Json.Serialization;
using WarzoneEQ.ConfigGenerator.Models;

namespace WarzoneEQ.Cli;

// JSON serialization for WorkflowOptions. Used for:
//   - persistent settings (auto-saved on Apply, restored on startup)
//   - shareable .warzeq preset files (Save/Load Preset buttons + double-click open)
//
// The on-disk format is intentionally future-proof: extra unknown JSON
// properties are ignored, missing ones fall back to record defaults.
public static class Presets
{
    public const string FileExtension = ".warzeq";
    public const string FileExtensionDescription = "Hear It Loud preset";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static void Save(string path, WorkflowOptions options)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var payload = new PresetPayload(
            FormatVersion: 1,
            App: "Hear It Loud",
            Options: options);
        File.WriteAllText(path, JsonSerializer.Serialize(payload, JsonOpts));
    }

    public static WorkflowOptions Load(string path)
    {
        var json = File.ReadAllText(path);
        var payload = JsonSerializer.Deserialize<PresetPayload>(json, JsonOpts);
        if (payload?.Options is null)
            throw new InvalidDataException("Preset file is empty or invalid.");
        return payload.Options;
    }

    public static bool LooksLikePresetFile(string path) =>
        path.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase) && File.Exists(path);

    private sealed record PresetPayload(int FormatVersion, string App, WorkflowOptions? Options);
}

// Persistent settings file (~%APPDATA%\HearItLoud\settings.json). One slot —
// the last-applied Advanced-tab state. Loaded on form startup so the user
// doesn't have to redo their toggles.
public static class Settings
{
    public static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HearItLoud",
            "settings" + Presets.FileExtension);

    public static WorkflowOptions? TryLoad()
    {
        try { return File.Exists(SettingsPath) ? Presets.Load(SettingsPath) : null; }
        catch { return null; }
    }

    public static void Save(WorkflowOptions options)
    {
        try { Presets.Save(SettingsPath, options); }
        catch { /* swallow — settings are best-effort */ }
    }
}
