using System.Text;
using System.Text.Json;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using Microsoft.Extensions.Logging;
using OpenShock.Desktop.ModuleBase.Api;
using OpenShock.Desktop.ModuleBase.Models;

namespace OpenShock.Desktop.Modules.Interception.Server;

public sealed class DoWebApiController : WebApiController
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<DoWebApiController> _logger;
    private readonly IOpenShockControl _openShockControl;

    private readonly InterceptionService _service;

    public DoWebApiController(InterceptionService service, IOpenShockControl openShockControl,
        ILogger<DoWebApiController> logger)
    {
        _service = service;
        _openShockControl = openShockControl;
        _logger = logger;
    }

    [Route(HttpVerbs.Post, "/apioperate")]
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

        if (!_service.Config.ShareCodeMappings.TryGetValue(request.Code, out var shockerId))
        {
            _logger.LogError("Share code not mapped to any shocker: {Code}", request.Code);
            HttpContext.Response.StatusCode = 404;
            await HttpContext.SendStringAsync("Share code not mapped to any shocker", "text/plain",
                Encoding.UTF8);
            return;
        }

        var controlType = request.Op switch
        {
            0 => ControlType.Shock,
            1 => ControlType.Vibrate,
            2 => ControlType.Sound,
            _ => ControlType.Vibrate
        };

        var durationMs = (ushort)Math.Clamp(request.Duration * 1000, 300, 30000);
        var intensity = (byte)Math.Clamp(request.Intensity, 1, 100);

        if (request.Intensity <= 0) controlType = ControlType.Stop;

        var controls = new[]
        {
            new ShockerControl
            {
                Id = shockerId,
                Type = controlType,
                Intensity = intensity,
                Duration = durationMs
            }
        };

        var customName = request.Name ?? request.Username ?? "PiShock Interception";

        try
        {
            await _openShockControl.Control(controls, customName);
            _logger.LogInformation(
                "PiShock Do API: control command: {ControlType} {Intensity}% for {Duration}s on shocker {ShockerId} by {Name}",
                controlType, intensity, durationMs / 1000.0, shockerId, customName);
            await HttpContext.SendStringAsync(
                JsonSerializer.Serialize(new { success = true, message = "Operation Succeeded." }),
                "application/json", Encoding.UTF8);
        }
        catch (Exception ex)
        {
            HttpContext.Response.StatusCode = 500;
            await HttpContext.SendStringAsync(
                JsonSerializer.Serialize(new { success = false, message = ex.Message }),
                "application/json", Encoding.UTF8);
        }
    }

    [Route(HttpVerbs.Post, "/GetShockerInfo")]
    public async Task GetShockerInfo()
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

        if (!_service.Config.ShareCodeMappings.TryGetValue(request.Code, out var shockerId))
        {
            _logger.LogError("Share code not mapped to any shocker: {Code}", request.Code);
            HttpContext.Response.StatusCode = 404;
            await HttpContext.SendStringAsync("Share code not found", "text/plain", Encoding.UTF8);
            return;
        }

        var info = new
        {
            clientId = shockerId,
            name = $"Shocker ({request.Code})",
            maxIntensity = 100,
            maxDuration = 15,
            online = true
        };

        await HttpContext.SendStringAsync(
            JsonSerializer.Serialize(info),
            "application/json", Encoding.UTF8);
    }
}