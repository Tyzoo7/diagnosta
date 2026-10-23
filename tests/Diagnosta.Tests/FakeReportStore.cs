using Diagnosta.Report;

namespace Diagnosta.Tests;

/// <summary>Keeps reports in memory instead of writing to the desktop.</summary>
internal sealed class FakeReportStore(Exception? failWith = null) : IReportStore
{
    public Dictionary<string, string> Saved { get; } = [];

    public List<string> Opened { get; } = [];

    public string Save(string fileName, string content)
    {
        if (failWith is not null)
        {
            throw failWith;
        }

        var path = @"C:\Users\Someone\Desktop\" + fileName;
        Saved[path] = content;
        return path;
    }

    public void Open(string path) => Opened.Add(path);
}
