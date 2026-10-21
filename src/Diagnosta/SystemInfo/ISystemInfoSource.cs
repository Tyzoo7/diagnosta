namespace Diagnosta.SystemInfo;

/// <summary>Where the system overview comes from. Tests use a fake implementation.</summary>
internal interface ISystemInfoSource
{
    SystemOverview Read();
}
