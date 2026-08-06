using OpenShock.Desktop.Modules.Interception.Server;

namespace OpenShock.Desktop.Modules.Interception;

public sealed class InterceptionConfig
{
    public ushort Port { get; set; } = 443;
    public bool AutoStart { get; set; } = true;
    public Dictionary<string, ShareCodeMapping> ShareCodeMappings { get; set; } = new();
}
