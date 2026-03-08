using System.Text.Json.Serialization;

namespace OpenShock.Desktop.Modules.Interception.Server;

public class PiShockRequest
{
    [JsonPropertyName("Username")] public string? Username { get; set; }

    [JsonPropertyName("Apikey")] public string? Apikey { get; set; }

    [JsonPropertyName("Code")] public string? Code { get; set; }

    [JsonPropertyName("Name")] public string? Name { get; set; }

    [JsonPropertyName("Op")] public int Op { get; set; }

    [JsonPropertyName("Duration")] public int Duration { get; set; }

    [JsonPropertyName("Intensity")] public int Intensity { get; set; }
}