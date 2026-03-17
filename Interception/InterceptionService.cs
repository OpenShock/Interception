using EmbedIO;
using EmbedIO.WebApi;
using Microsoft.Extensions.DependencyInjection;
using OpenShock.Desktop.ModuleBase.Config;
using OpenShock.Desktop.Modules.Interception.Certificates;
using OpenShock.Desktop.Modules.Interception.HostsFile;
using OpenShock.Desktop.Modules.Interception.Server;

namespace OpenShock.Desktop.Modules.Interception;

public sealed class InterceptionService(
    IModuleConfig<InterceptionConfig> moduleConfig,
    CertificateManager certManager,
    HostsFileManager hostsManager,
    IServiceProvider serviceProvider) : IAsyncDisposable
{
    private WebServer? _server;

    public InterceptionConfig Config => moduleConfig.Config;
    public bool IsRunning => _server != null;
    public HostsFileManager HostsManager => hostsManager;

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    public async Task StartAsync()
    {
        if (_server != null) return;

        await certManager.InitializeAsync();

        var port = Config.Port;
        var cert = certManager.ServerCertificate;

        var operateController = ActivatorUtilities.CreateInstance<DoWebApiController>(serviceProvider);
        var psController = ActivatorUtilities.CreateInstance<PsWebApiController>(serviceProvider);

        _server = new WebServer(o => o
                    .WithUrlPrefix($"https://*:{port}/")
                    .WithMode(HttpListenerMode.EmbedIO)
                    .WithCertificate(cert))
                .WithWebApi("/api", m => m.WithController(() => operateController))
                .WithWebApi("/PiShock", m => m.WithController(() => psController))
            ;

        _ = _server.RunAsync();
    }

    public Task StopAsync()
    {
        if (_server is null) return Task.CompletedTask;
        _server.Dispose();
        _server = null;

        return Task.CompletedTask;
    }

    public async Task UpdateConfig(Action<InterceptionConfig> update)
    {
        update(Config);
        await moduleConfig.Save();
    }
}
