using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Diagnosta.SystemInfo;

/// <summary>
/// Reads the same autostart locations Task Manager shows: the "Run" registry keys and the
/// Startup folders, plus Task Manager's own enabled/disabled switches.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WindowsStartupApps
{
    private const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Run32 = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string Approved = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

    public static IReadOnlyList<StartupEntry> Read()
    {
        var entries = new List<StartupEntry>();

        AddRunKey(entries, Registry.CurrentUser, Run, Approved + "Run", StartupScope.CurrentUser);
        AddRunKey(entries, Registry.LocalMachine, Run, Approved + "Run", StartupScope.AllUsers);
        AddRunKey(entries, Registry.LocalMachine, Run32, Approved + "Run32", StartupScope.AllUsers);
        AddFolder(entries, Environment.SpecialFolder.Startup, Registry.CurrentUser, StartupScope.CurrentUser);
        AddFolder(entries, Environment.SpecialFolder.CommonStartup, Registry.LocalMachine, StartupScope.AllUsers);

        return entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AddRunKey(List<StartupEntry> entries, RegistryKey hive, string runPath, string approvedPath, StartupScope scope)
    {
        using var run = hive.OpenSubKey(runPath);
        if (run is null)
        {
            return;
        }

        using var approved = hive.OpenSubKey(approvedPath);
        foreach (var name in run.GetValueNames().Where(n => n.Length > 0))
        {
            entries.Add(new StartupEntry(
                name,
                StartupParsing.FileNameFromCommand(run.GetValue(name) as string),
                scope,
                StartupParsing.IsEnabled(approved?.GetValue(name) as byte[])));
        }
    }

    private static void AddFolder(List<StartupEntry> entries, Environment.SpecialFolder folder, RegistryKey hive, StartupScope scope)
    {
        var path = Environment.GetFolderPath(folder);
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            return;
        }

        using var approved = hive.OpenSubKey(Approved + "StartupFolder");
        foreach (var file in Directory.EnumerateFiles(path).Where(f => !f.EndsWith("desktop.ini", StringComparison.OrdinalIgnoreCase)))
        {
            var fileName = Path.GetFileName(file);
            entries.Add(new StartupEntry(
                Path.GetFileNameWithoutExtension(file),
                fileName,
                scope,
                StartupParsing.IsEnabled(approved?.GetValue(fileName) as byte[])));
        }
    }
}
