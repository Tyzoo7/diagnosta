using System.Net.NetworkInformation;
using Diagnosta.SystemInfo;

namespace Diagnosta.Tests;

public class BatteryAndNetworkTests
{
    [Theory]
    [InlineData(50_000u, 40_000u, 0.8)]
    [InlineData(50_000u, 52_000u, 1.0)] // new batteries can exceed their design: capped at 100 %
    public void BatteryHealth_IsFullChargeComparedToDesign(uint design, uint full, double expected)
    {
        var battery = new BatteryInfo(true, DesignedCapacity: design, FullChargeCapacity: full);

        Assert.Equal(expected, battery.Health!.Value, precision: 6);
    }

    [Theory]
    [InlineData(null, 40_000u)]
    [InlineData(0u, 40_000u)]
    [InlineData(50_000u, 0u)]
    public void BatteryHealth_WithoutBothCapacities_IsUnknown(uint? design, uint full)
    {
        Assert.Null(new BatteryInfo(true, DesignedCapacity: design, FullChargeCapacity: full).Health);
    }

    [Theory]
    [InlineData("192.168.1.23", false)]
    [InlineData("169.254.12.7", true)]
    public void SelfAssignedAddress_IsRecognised(string address, bool expected)
    {
        var adapter = new NetworkAdapterInfo("Realtek", NetworkKind.Ethernet, null, [address], null, []);

        Assert.Equal(expected, adapter.HasSelfAssignedAddress);
    }

    [Theory]
    [InlineData(NetworkInterfaceType.Ethernet, "Intel(R) Ethernet Connection I219-V", true)]
    [InlineData(NetworkInterfaceType.Wireless80211, "Intel(R) Wi-Fi 6 AX201", true)]
    [InlineData(NetworkInterfaceType.Loopback, "Software Loopback Interface 1", false)]
    [InlineData(NetworkInterfaceType.Ethernet, "Hyper-V Virtual Ethernet Adapter", false)]
    [InlineData(NetworkInterfaceType.Ethernet, "VirtualBox Host-Only Ethernet Adapter", false)]
    [InlineData(NetworkInterfaceType.Ethernet, "Bluetooth Device (Personal Area Network)", false)]
    [InlineData(NetworkInterfaceType.Ethernet, "VMware Virtual Ethernet Adapter for VMnet8", false)]
    // Inside a VM these are the real network cards and must stay visible.
    [InlineData(NetworkInterfaceType.Ethernet, "Microsoft Hyper-V Network Adapter", true)]
    [InlineData(NetworkInterfaceType.Ethernet, "vmxnet3 Ethernet Adapter", true)]
    [InlineData(NetworkInterfaceType.Ethernet, "Intel(R) PRO/1000 MT Desktop Adapter", true)] // VirtualBox guest
    public void VirtualAndInternalAdapters_AreHidden(NetworkInterfaceType type, string description, bool expected)
    {
        Assert.Equal(expected, NetworkAdapters.IsRelevant(type, description));
    }

    [Theory]
    [InlineData(NetworkInterfaceType.Wireless80211, "WiFi")]
    [InlineData(NetworkInterfaceType.Ethernet, "Ethernet")]
    [InlineData(NetworkInterfaceType.GigabitEthernet, "Ethernet")]
    [InlineData(NetworkInterfaceType.Ppp, "Other")]
    public void AdapterKind_IsMapped(NetworkInterfaceType type, string expected)
    {
        Assert.Equal(expected, NetworkAdapters.KindOf(type).ToString());
    }
}
