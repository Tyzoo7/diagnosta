using System.Diagnostics;
using System.Text;

namespace Diagnosta.Report;

/// <summary>
/// Saves reports on the desktop, where people find them again and can drag them into an
/// e-mail or ticket. Falls back to Documents, then to the current folder.
/// </summary>
internal sealed class DesktopReportStore : IReportStore
{
    public string Save(string fileName, string content)
    {
        var folder = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.CurrentDirectory,
            }
            .First(f => !string.IsNullOrEmpty(f) && Directory.Exists(f));

        var path = Path.Combine(folder, fileName);
        // CreateNew: never overwrite an existing file.
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
        return path;
    }

    public void Open(string path) =>
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
}
