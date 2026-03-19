using System.Text.Json.Serialization;

namespace OpenShock.Desktop.Modules.Interception.Server;

public sealed class ShareCodeMapping
{
    public List<Guid> ShockerIds { get; set; } = [];
    public byte MinIntensity { get; set; } = 1;
    public byte MaxIntensity { get; set; } = 100;
    public ushort MinDuration { get; set; } = 1;
    public ushort MaxDuration { get; set; } = 15;

    /// <summary>
    /// Effective min intensity, guaranteed &lt;= EffectiveMaxIntensity.
    /// </summary>
    [JsonIgnore]
    public byte EffectiveMinIntensity => Math.Min(MinIntensity, MaxIntensity);

    /// <summary>
    /// Effective max intensity, guaranteed &gt;= EffectiveMinIntensity.
    /// </summary>
    [JsonIgnore]
    public byte EffectiveMaxIntensity => Math.Max(MinIntensity, MaxIntensity);

    /// <summary>
    /// Effective min duration (seconds), guaranteed &lt;= EffectiveMaxDuration.
    /// </summary>
    [JsonIgnore]
    public ushort EffectiveMinDuration => Math.Min(MinDuration, MaxDuration);

    /// <summary>
    /// Effective max duration (seconds), guaranteed &gt;= EffectiveMinDuration.
    /// </summary>
    [JsonIgnore]
    public ushort EffectiveMaxDuration => Math.Max(MinDuration, MaxDuration);
}
