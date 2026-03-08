# OpenShock Interception

A desktop module for [OpenShock Desktop](https://github.com/openshock/desktop) that intercepts PiShock API calls and redirects them through OpenShock.

## How It Works

This module runs a local HTTPS server that impersonates `do.pishock.com`. Applications that use the PiShock API will have their requests intercepted and translated into OpenShock control commands.

1. **Hosts file redirect** — Adds a `127.0.0.1 do.pishock.com` entry to the Windows hosts file so PiShock API traffic is routed to localhost.
2. **Self-signed certificates** — Generates a local CA and server certificate for `do.pishock.com` to serve HTTPS. The CA can be installed into the user's trust store.
3. **API translation** — Incoming PiShock `apioperate` and `GetShockerInfo` requests are parsed and forwarded to OpenShock using share code-to-shocker mappings.

## Configuration

| Setting | Default | Description |
|---|---|---|
| `Port` | `443` | HTTPS port for the local server |
| `AutoStart` | `true` | Start the interception server automatically on module load |
| `ShareCodeMappings` | `{}` | Map of PiShock share codes to OpenShock shocker GUIDs |

## Requirements

- Windows (hosts file and certificate store management are Windows-specific)
- .NET 10.0
- [OpenShock Desktop](https://github.com/OpenShock) with module support
- Administrator privileges for hosts file modification

## Building

```bash
dotnet build
```

## Project Structure

```
Interception/
├── InterceptionMain.cs        # Module entry point and DI setup
├── InterceptionService.cs     # HTTPS server lifecycle management
├── InterceptionConfig.cs      # Module configuration model
├── Certificates/
│   └── CertificateManager.cs  # CA and server certificate generation
├── HostsFile/
│   └── HostsFileManager.cs    # Windows hosts file manipulation
├── Server/
│   ├── DoWebApiController.cs  # PiShock API endpoint handlers
│   └── PiShockRequest.cs      # PiShock request model
├── SwanToMicrosoft.cs         # EmbedIO (Swan) to Microsoft.Extensions.Logging bridge
└── Ui/                        # Blazor UI components (MudBlazor)
```

## License

See [LICENSE](LICENSE) for details.
