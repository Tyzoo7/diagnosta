using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Diagnosta.SystemInfo;

/// <summary>
/// Connected network adapters from .NET's built-in network information: no internet access,
/// no extra permissions. Wi-Fi signal strength is left out on purpose: since Windows 11 24H2
/// it counts as location data and would pop up a location permission prompt.
/// </summary>
internal static class NetworkAdapters
{
    public static IReadOnlyList<NetworkAdapterInfo> Read() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && IsRelevant(n.NetworkInterfaceType, n.Description))
            .Select(ToInfo)
            .OrderBy(a => a.Kind == NetworkKind.Other ? 1 : 0)
            .ToList();

    /// <summary>
    /// Extra adapters a PC creates for its own VMs, VPN tools and Bluetooth. Matched precisely:
    /// inside a VM the real network card is called e.g. "Microsoft Hyper-V Network Adapter",
    /// and hiding that would wrongly report "no network".
    /// </summary>
    private static readonly string[] VirtualAdapterNames =
        ["Hyper-V Virtual Ethernet Adapter", "VirtualBox Host-Only", "VMware Virtual Ethernet Adapter", "WSL", "Loopback", "TAP-Windows", "Bluetooth"];

    /// <summary>Hides loopback, tunnels and the virtual adapters of VMs and VPN tools.</summary>
    internal static bool IsRelevant(NetworkInterfaceType type, string description) =>
        type is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
        && !VirtualAdapterNames.Any(v => description.Contains(v, StringComparison.OrdinalIgnoreCase));

    internal static NetworkKind KindOf(NetworkInterfaceType type) => type switch
    {
        NetworkInterfaceType.Wireless80211 => NetworkKind.WiFi,
        NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetT or NetworkInterfaceType.FastEthernetFx => NetworkKind.Ethernet,
        _ => NetworkKind.Other,
    };

    private static NetworkAdapterInfo ToInfo(NetworkInterface adapter)
    {
        var ip = adapter.GetIPProperties();
        var speed = adapter.Speed;
        return new NetworkAdapterInfo(
            Description: adapter.Description,
            Kind: KindOf(adapter.NetworkInterfaceType),
            SpeedBitsPerSecond: speed > 0 ? speed : null,
            IPv4Addresses: ip.UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork).Select(a => a.Address.ToString()).ToList(),
            Gateway: ip.GatewayAddresses.Select(g => g.Address).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?.ToString(),
            DnsServers: ip.DnsAddresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork).Select(a => a.ToString()).ToList());
    }
}
