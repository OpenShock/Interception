using System.Text;
using System.Text.Json;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using Microsoft.Extensions.Logging;
using OpenShock.Desktop.ModuleBase.Api;
using OpenShock.Desktop.ModuleBase.Models;

namespace OpenShock.Desktop.Modules.Interception.Server;

public sealed class PsWebApiController : WebApiController
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<PsWebApiController> _logger;
    private readonly IOpenShockControl _openShockControl;
    private readonly IOpenShockData _openShockData;
    private readonly InterceptionService _service;

    public PsWebApiController(InterceptionService service, IOpenShockControl openShockControl,
        IOpenShockData openShockData, ILogger<PsWebApiController> logger)
    {
        _service = service;
        _openShockControl = openShockControl;
        _openShockData = openShockData;
        _logger = logger;
    }

    [Route(HttpVerbs.Post, "/Operate")]
    public async Task Operate()
    {
        using var reader = new StreamReader(HttpContext.Request.InputStream);
        var body = await reader.ReadToEndAsync();

        PiShockRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<PiShockRequest>(body, JsonOptions);
        }
        catch
        {
            _logger.LogError("Error parsing JSON body: {Body}", body);
            HttpContext.Response.StatusCode = 400;
            await HttpContext.SendStringAsync("Invalid JSON", "text/plain", Encoding.UTF8);
            return;
        }

        if (request?.Code == null)
        {
            _logger.LogError("Missing share code in request: {Body}", body);
            HttpContext.Response.StatusCode = 400;
            await HttpContext.SendStringAsync("Missing share code", "text/plain", Encoding.UTF8);
            return;
        }

        if (!_service.Config.ShareCodeMappings.TryGetValue(request.Code, out var mapping))
        {
            _logger.LogError("Share code not mapped to any shocker: {Code}", request.Code);
            await HttpContext.SendStringAsync("This code doesn't exist.", "text/plain", Encoding.UTF8);
            return;
        }

        if (mapping.ShockerIds.Count == 0)
        {
            _logger.LogError("Share code has no shockers configured: {Code}", request.Code);
            await HttpContext.SendStringAsync("This code doesn't exist.", "text/plain", Encoding.UTF8);
            return;
        }

        var controlType = request.Op switch
        {
            0 => ControlType.Shock,
            1 => ControlType.Vibrate,
            2 => ControlType.Sound,
            _ => ControlType.Vibrate
        };

        var durationMs = (ushort)Math.Clamp(request.Duration * 1000, mapping.MinDuration * 1000, mapping.MaxDuration * 1000);
        var intensity = (byte)Math.Clamp(request.Intensity, mapping.MinIntensity, mapping.MaxIntensity);

        if (request.Intensity <= 0) controlType = ControlType.Stop;

        var controls = mapping.ShockerIds.Select(id => new ShockerControl
        {
            Id = id,
            Type = controlType,
            Intensity = intensity,
            Duration = durationMs
        }).ToArray();

        var customName = request.Name ?? request.Username ?? "PiShock Interception";

        try
        {
            await _openShockControl.Control(controls, customName);
            _logger.LogInformation(
                "PiShock Ps API Operate: {ControlType} {Intensity}% for {Duration}s on {ShockerCount} shocker(s) by {Name}",
                controlType, intensity, durationMs / 1000.0, controls.Length, customName);
            await HttpContext.SendStringAsync("Operation Succeeded.", "text/plain", Encoding.UTF8);
        }
        catch (Exception ex)
        {
            HttpContext.Response.StatusCode = 500;
            await HttpContext.SendStringAsync(ex.Message, "text/plain", Encoding.UTF8);
        }
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
