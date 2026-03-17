namespace OpenShock.Desktop.Modules.Interception.Server;

public sealed class ShareCodeMapping
{
    public List<Guid> ShockerIds { get; set; } = [];
    public byte MinIntensity { get; set; } = 1;
    public byte MaxIntensity { get; set; } = 100;
    public ushort MinDuration { get; set; } = 1;
    public ushort MaxDuration { get; set; } = 15;
}
