using System.Text;
using System.Text.Json;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using Microsoft.Extensions.Logging;
using OpenShock.Desktop.ModuleBase.Api;

namespace OpenShock.Desktop.Modules.Interception.Server;

public sealed class PsWebApiController : WebApiController
{
    private readonly ILogger<PsWebApiController> _logger;
    private readonly IOpenShockData _openShockData;
    private readonly InterceptionService _service;

    public PsWebApiController(InterceptionService service, IOpenShockData openShockData,
        ILogger<PsWebApiController> logger)
    {
        _service = service;
        _openShockData = openShockData;
        _logger = logger;
    }

    [Route(HttpVerbs.Get, "/GetUserDevices")]
    public async Task GetUserDevices()
    {
        _logger.LogInformation("PiShock Ps API: GetUserDevices request");

        var shareCodeIndex = 0;
        var devices = new List<object>();

        // Build a synthetic device containing all mapped shockers
        var shockers = new List<object>();
        foreach (var (shareCode, mapping) in _service.Config.ShareCodeMappings)
        {
            foreach (var shockerId in mapping.ShockerIds)
            {
                var shockerInfo = FindShockerInfo(shockerId);
                shockers.Add(new
                {
                    shockerId = shareCodeIndex++,
                    name = shockerInfo?.Name ?? $"Shocker ({shareCode})",
                    shareCode,
                    isPaused = false,
                    maxIntensity = (int)mapping.MaxIntensity,
                    maxDuration = (int)mapping.MaxDuration
                });
            }
        }

        if (shockers.Count > 0)
        {
            devices.Add(new
            {
                deviceId = 0,
                name = "OpenShock Interception",
                shockers
            });
        }

        await HttpContext.SendStringAsync(
            JsonSerializer.Serialize(devices),
            "application/json", Encoding.UTF8);
    }

    [Route(HttpVerbs.Get, "/GetShareCodesByOwner")]
    public async Task GetShareCodesByOwner()
    {
        _logger.LogInformation("PiShock Ps API: GetShareCodesByOwner request");

        var result = new List<object>();
        var shareId = 0;

        foreach (var (shareCode, mapping) in _service.Config.ShareCodeMappings)
        {
            var shockerInfo = mapping.ShockerIds.Count > 0 ? FindShockerInfo(mapping.ShockerIds[0]) : null;
            result.Add(new
            {
                shareCodeId = shareId++,
                code = shareCode,
                shockerName = shockerInfo?.Name ?? $"Shocker ({shareCode})",
                isPaused = false,
                maxIntensity = (int)mapping.MaxIntensity,
                maxDuration = (int)mapping.MaxDuration,
                permissions = new
                {
                    shock = true,
                    vibrate = true,
                    sound = true
                }
            });
        }

        await HttpContext.SendStringAsync(
            JsonSerializer.Serialize(result),
            "application/json", Encoding.UTF8);
    }

    [Route(HttpVerbs.Get, "/GetShockersByShareIds")]
    public async Task GetShockersByShareIds()
    {
        _logger.LogInformation("PiShock Ps API: GetShockersByShareIds request");

        var shareIdParams = HttpContext.GetRequestQueryData().GetValues("shareIds") ?? [];

        var result = new List<object>();
        var shareId = 0;

        foreach (var (shareCode, mapping) in _service.Config.ShareCodeMappings)
        {
            var currentId = shareId++;
            if (shareIdParams.Length > 0 && !shareIdParams.Contains(currentId.ToString())) continue;

            var shockerInfo = mapping.ShockerIds.Count > 0 ? FindShockerInfo(mapping.ShockerIds[0]) : null;
            result.Add(new
            {
                shareCodeId = currentId,
                code = shareCode,
                shockerName = shockerInfo?.Name ?? $"Shocker ({shareCode})",
                isPaused = false,
                maxIntensity = (int)mapping.MaxIntensity,
                maxDuration = (int)mapping.MaxDuration,
                online = true,
                permissions = new
                {
                    shock = true,
                    vibrate = true,
                    sound = true
                }
            });
        }

        await HttpContext.SendStringAsync(
            JsonSerializer.Serialize(result),
            "application/json", Encoding.UTF8);
    }

    private ShockerInfo? FindShockerInfo(Guid shockerId)
    {
        foreach (var hub in _openShockData.Hubs.Value)
        {
            foreach (var shocker in hub.Shockers)
            {
                if (shocker.Id == shockerId)
                    return new ShockerInfo(shocker.Name, hub.Name);
            }
        }

        return null;
    }

    private sealed record ShockerInfo(string Name, string HubName);
}
