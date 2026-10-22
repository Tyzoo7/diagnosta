using Diagnosta.SystemInfo;

namespace Diagnosta.Tests;

public class StartupParsingTests
{
    [Theory]
    [InlineData(null, true)]                                  // never touched in Task Manager
    [InlineData(new byte[0], true)]
    [InlineData(new byte[] { 0x02, 0, 0, 0 }, true)]
    [InlineData(new byte[] { 0x06, 0, 0, 0 }, true)]
    [InlineData(new byte[] { 0x03, 0, 0, 0, 0x5A, 0x1F }, false)] // disabled, with timestamp
    public void TaskManagerSwitch_IsRead(byte[]? approved, bool expected)
    {
        Assert.Equal(expected, StartupParsing.IsEnabled(approved));
    }

    [Theory]
    [InlineData(@"""C:\Program Files\Microsoft OneDrive\OneDrive.exe"" /background", "OneDrive.exe")]
    [InlineData(@"C:\Program Files\Some App\app.exe --minimized", "app.exe")]
    [InlineData(@"C:\Windows\system32\SecurityHealthSystray.exe", "SecurityHealthSystray.exe")]
    [InlineData(@"rundll32.exe C:\Windows\thing.dll,Start", "rundll32.exe")]
    [InlineData(@"""C:\Users\max\AppData\Local\Discord\Update.exe"" --processStart Discord.exe", "Update.exe")]
    public void ProgramFile_IsExtractedWithoutThePath(string command, string expected)
    {
        Assert.Equal(expected, StartupParsing.FileNameFromCommand(command));
    }

    [Fact]
    public void UserNameInThePath_IsNotKept()
    {
        var name = StartupParsing.FileNameFromCommand(@"""C:\Users\max.mustermann\AppData\Roaming\App\app.exe""");

        Assert.Equal("app.exe", name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyCommand_IsNull(string? command)
    {
        Assert.Null(StartupParsing.FileNameFromCommand(command));
    }
}
