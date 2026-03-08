namespace OpenShock.Desktop.Modules.Interception;

public sealed class InterceptionConfig
{
    public ushort Port { get; set; } = 443;
    public bool AutoStart { get; set; } = true;
    public Dictionary<string, Guid> ShareCodeMappings { get; set; } = new();
}