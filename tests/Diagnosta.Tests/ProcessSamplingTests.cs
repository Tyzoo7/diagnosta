using Diagnosta.SystemInfo;

namespace Diagnosta.Tests;

public class ProcessSamplingTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(500);

    [Fact]
    public void ProcessesOfOneProgram_AreAddedUp()
    {
        ProcessSnapshot[] before =
        [
            new(10, "chrome", 100, TimeSpan.FromMilliseconds(1000)),
            new(11, "chrome", 200, TimeSpan.FromMilliseconds(2000)),
        ];
        ProcessSnapshot[] after =
        [
            new(10, "chrome", 150, TimeSpan.FromMilliseconds(1100)),
            new(11, "chrome", 250, TimeSpan.FromMilliseconds(2100)),
        ];

        var chrome = Assert.Single(ProcessSampling.Combine(before, after, Window, processorCount: 4));

        Assert.Equal(2, chrome.Instances);
        Assert.Equal(400UL, chrome.MemoryBytes);
        // 200 ms CPU in a 500 ms window on 4 cores = 200 / 2000 = 10 %
        Assert.Equal(10, chrome.CpuPercent!.Value, precision: 6);
    }

    [Fact]
    public void ProtectedProcess_HasUnknownCpu_ButStillCountsMemory()
    {
        ProcessSnapshot[] before = [new(4, "System", 0, null)];
        ProcessSnapshot[] after = [new(4, "System", 1000, null)];

        var system = Assert.Single(ProcessSampling.Combine(before, after, Window, 8));

        Assert.Null(system.CpuPercent);
        Assert.Equal(1000UL, system.MemoryBytes);
    }

    [Fact]
    public void ProcessStartedDuringTheWindow_HasUnknownCpu()
    {
        var result = Assert.Single(ProcessSampling.Combine([], [new(42, "new", 10, TimeSpan.FromSeconds(1))], Window, 4));

        Assert.Null(result.CpuPercent);
    }

    [Fact]
    public void CpuShare_NeverExceeds100Percent()
    {
        ProcessSnapshot[] before = [new(1, "busy", 0, TimeSpan.Zero)];
        ProcessSnapshot[] after = [new(1, "busy", 0, TimeSpan.FromSeconds(10))];

        Assert.Equal(100, ProcessSampling.Combine(before, after, Window, 1)[0].CpuPercent);
    }

    [Fact]
    public void SystemCpuUsage_TreatsIdleAsPartOfKernelTime()
    {
        // 1000 units passed in total (kernel incl. idle 800 + user 200), 600 of them idle → 40 % busy.
        var usage = ProcessSampling.CpuUsage((Idle: 0, Kernel: 0, User: 0), (Idle: 600, Kernel: 800, User: 200));

        Assert.Equal(40, usage!.Value, precision: 6);
    }

    [Fact]
    public void SystemCpuUsage_WithNoTimePassed_IsUnknown()
    {
        Assert.Null(ProcessSampling.CpuUsage((1, 1, 1), (1, 1, 1)));
    }
}
