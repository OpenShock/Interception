using System.Diagnostics;
using System.Text;

namespace OpenShock.Desktop.Modules.Interception.HostsFile;

public sealed class HostsFileManager
{
    private const string HostsPath = @"C:\Windows\System32\drivers\etc\hosts";
    private const string Marker = "# OpenShock Interception";

    private static readonly string[] HostEntries =
    [
        $"127.0.0.1 do.pishock.com {Marker}",
        $"127.0.0.1 ps.pishock.com {Marker}"
    ];

    public bool IsEnabled { get; private set; }

    public async Task EnableAsync()
    {
        if (IsEnabled && !NeedsUpdate) return;
        await RunElevatedHostsCommand("add");
        IsEnabled = true;
        NeedsUpdate = false;
        await FlushDns();
    }

    public async Task DisableAsync()
    {
        if (!IsEnabled) return;
        await RunElevatedHostsCommand("remove");
        IsEnabled = false;
        await FlushDns();
    }

    public bool NeedsUpdate { get; private set; }

    public async Task DetectCurrentState()
    {
        try
        {
            var content = await File.ReadAllTextAsync(HostsPath);
            var hasAny = content.Contains(Marker);
            var hasAll = content.Contains("do.pishock.com") && content.Contains("ps.pishock.com");
            IsEnabled = hasAny;
            NeedsUpdate = hasAny && !hasAll;
        }
        catch
        {
            IsEnabled = false;
            NeedsUpdate = false;
        }
    }

    private static async Task RunElevatedHostsCommand(string action)
    {
        string script;
        if (action == "add")
        {
            // Check each host entry individually so upgrades from the old
            // single-entry format pick up the new ps.pishock.com line.
            var linesArray = string.Join(",", HostEntries.Select(e => $"'{e}'"));
            script = string.Join("\n",
                $"$lines = @({linesArray});",
                $"$hostsPath = '{HostsPath}';",
                "$content = Get-Content $hostsPath -Raw -ErrorAction SilentlyContinue;",
                "if (-not $content) { $content = '' }",
                "$added = $false;",
                "foreach ($line in $lines) {",
                "    $host = ($line -split '\\s+')[1];",
                "    if ($content -notmatch [regex]::Escape($host)) {",
                "        Add-Content -Path $hostsPath -Value \"`n$line\" -NoNewline:$false;",
                "        $added = $true",
                "    }",
                "}");
        }
        else
        {
            script = string.Join("\n",
                $"$hostsPath = '{HostsPath}';",
                "$content = [System.IO.File]::ReadAllText($hostsPath);",
                "$content = $content -replace '(?m)^[^\\r\\n]*OpenShock Interception[^\\r\\n]*(\\r?\\n)?', '';",
                "[System.IO.File]::WriteAllText($hostsPath, $content)");
        }

        var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -EncodedCommand {encodedCommand}",
            Verb = "runas",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        using var process = Process.Start(psi)
                            ?? throw new InvalidOperationException("Failed to start elevated process.");
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Hosts file modification failed with exit code {process.ExitCode}.");
    }

    private static async Task FlushDns()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "ipconfig",
                Arguments = "/flushdns",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null) return;

            var timeout = Task.Delay(TimeSpan.FromSeconds(5));
            if (await Task.WhenAny(timeout, process.WaitForExitAsync()) == timeout) process.Kill();
        }
        catch
        {
            // Best-effort DNS flush
        }
    }
}
