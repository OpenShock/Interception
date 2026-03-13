using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenShock.Desktop.ModuleBase;
using OpenShock.Desktop.ModuleBase.Navigation;
using OpenShock.Desktop.Modules.Interception;
using OpenShock.Desktop.Modules.Interception.Certificates;
using OpenShock.Desktop.Modules.Interception.HostsFile;
using OpenShock.Desktop.Modules.Interception.Ui;
using Swan.Logging;

[assembly: DesktopModule(typeof(InterceptionMain), "openshock.desktop.modules.interception", "Interception")]

namespace OpenShock.Desktop.Modules.Interception;

public class InterceptionMain(ILogger<InterceptionMain> logger, ILoggerFactory loggerFactory) : DesktopModuleBase
{
    private InterceptionService? _service;

    public override Type RootComponent => typeof(InterceptionPage);

    public override IReadOnlyCollection<NavigationItem> NavigationComponents { get; } = [];
    
    public override IconOneOf Icon => IconOneOf.FromPath("/OpenShock/Desktop/Modules/Interception/interception.svg");

    public override async Task Setup()
    {
        Logger.RegisterLogger(new SwanToMicrosoft(loggerFactory));

        var openShock = ModuleInstanceManager.OpenShock;
        var moduleConfig = await ModuleInstanceManager.GetModuleConfig<InterceptionConfig>();

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenShock", "Desktop", "Modules", "Interception", "Certificates");

        var services = new ServiceCollection();
        services.AddSingleton(loggerFactory);
        services.AddLogging();

        services.AddSingleton(openShock.Control);
        services.AddSingleton(openShock.Data);
        services.AddSingleton(moduleConfig);
        services.AddSingleton(new CertificateManager(dataDir));
        services.AddSingleton<HostsFileManager>();
        services.AddSingleton<InterceptionService>();

        ModuleServiceProvider = services.BuildServiceProvider();
        _service = ModuleServiceProvider.GetRequiredService<InterceptionService>();

        try
        {
            await _service.HostsManager.DetectCurrentState();
        }
        catch (Exception ex)
        {
            logger.LogWarning("Failed to detect hosts file state: {Message}", ex.Message);
        }
    }

    public override async Task Start()
    {
        if (_service is not { Config.AutoStart: true }) return;

        try
        {
            await _service.StartAsync();
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to auto-start: {ExMessage}", ex.Message);
        }
    }
}