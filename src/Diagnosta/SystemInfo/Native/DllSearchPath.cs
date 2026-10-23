using System.Runtime.InteropServices;

// Windows libraries are loaded from System32 only, never from the folder the exe sits in.
// People often run Diagnosta straight from Downloads; without this, a fake dxgi.dll saved
// there by a malicious website would be loaded and run ("DLL planting").
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
