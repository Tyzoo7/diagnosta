using System.Runtime.Versioning;
using Diagnosta.SystemInfo;

namespace Diagnosta.Tests;

/// <summary>
/// Reads the real machine. Runs on Windows (CI and your own PC) and is skipped elsewhere.
/// The checks are deliberately loose: they prove the Windows calls work, not exact values.
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsSystemInfoSourceTests
{
    private static SystemOverview ReadRealMachine()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Needs Windows.");
        return new WindowsSystemInfoSource().Read();
    }

    [Fact]
    public void Cpu_IsDetected()
    {
        var cpu = ReadRealMachine().Cpu;

        Assert.False(string.IsNullOrWhiteSpace(cpu.Name));
        Assert.NotNull(cpu.PhysicalCores);
        Assert.InRange(cpu.PhysicalCores.Value, 1, cpu.LogicalProcessors);
    }

    [Fact]
    public void Memory_IsPlausible()
    {
        var memory = ReadRealMachine().Memory;

        Assert.NotNull(memory);
        Assert.InRange(memory.TotalBytes, 1UL * 1024 * 1024 * 1024, 64UL * 1024 * 1024 * 1024 * 1024);
        Assert.True(memory.AvailableBytes <= memory.TotalBytes);
    }

    [Fact]
    public void Windows_IsDetected()
    {
        var windows = ReadRealMachine().Windows;

        Assert.NotNull(windows);
        Assert.True(windows.Build >= 10240, $"Unexpected build {windows.Build}");
        Assert.NotNull(windows.UpdateRevision);
    }

    [Fact]
    public void Gpus_CanBeListed()
    {
        // Cloud machines may have no real GPU, so an empty list is fine; null (failure) is not.
        Assert.NotNull(ReadRealMachine().Gpus);
    }

    [Fact]
    public void Uptime_IsPositive()
    {
        Assert.True(ReadRealMachine().Uptime > TimeSpan.Zero);
    }
}
