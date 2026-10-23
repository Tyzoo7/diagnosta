namespace Diagnosta.Report;

/// <summary>Where reports go. Tests use an in-memory version instead of the real desktop.</summary>
internal interface IReportStore
{
    /// <summary>Saves the report and returns the full path, so the user knows where to find it.</summary>
    string Save(string fileName, string content);

    /// <summary>Opens the saved report in the default browser.</summary>
    void Open(string path);
}
