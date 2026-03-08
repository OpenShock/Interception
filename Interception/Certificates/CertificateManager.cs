using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace OpenShock.Desktop.Modules.Interception.Certificates;

public sealed class CertificateManager : IDisposable
{
    private const string CaFileName = "interception-ca.pfx";
    private const string ServerFileName = "interception-server.pfx";
    private const string CertPassword = "";

    private readonly string _dataDir;
    private X509Certificate2? _caCert;
    private X509Certificate2? _serverCert;

    public CertificateManager(string dataDir)
    {
        _dataDir = dataDir;
        Directory.CreateDirectory(_dataDir);
    }

    public X509Certificate2 ServerCertificate => _serverCert
                                                 ?? throw new InvalidOperationException(
                                                     "Certificates not initialized. Call Initialize() first.");

    public bool IsCaTrusted { get; private set; }

    public void Dispose()
    {
        _caCert?.Dispose();
        _serverCert?.Dispose();
    }

    public async Task InitializeAsync()
    {
        var caPath = Path.Combine(_dataDir, CaFileName);
        var serverPath = Path.Combine(_dataDir, ServerFileName);

        _caCert = await LoadOrCreateCaCert(caPath);
        _serverCert = await LoadOrCreateServerCert(serverPath, _caCert);
        IsCaTrusted = CheckCaTrusted();
    }

    private static async Task<X509Certificate2> LoadOrCreateCaCert(string path)
    {
        if (File.Exists(path))
        {
            byte[] buffer;
            await using (var file = File.OpenRead(path))
            {
                buffer = new byte[file.Length];
                await file.ReadExactlyAsync(buffer);
            }

            var loaded = X509CertificateLoader.LoadPkcs12(buffer, CertPassword,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
            if (loaded.NotAfter > DateTime.UtcNow.AddDays(30))
                return loaded;

            loaded.Dispose();
        }

        using var rsa = RSA.Create(4096);
        var req = new CertificateRequest(
            "CN=OpenShock Interception CA, O=OpenShock",
            rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
        await File.WriteAllBytesAsync(path, cert.Export(X509ContentType.Pfx, CertPassword));
        return cert;
    }

    private static async Task<X509Certificate2> LoadOrCreateServerCert(string path, X509Certificate2 caCert)
    {
        if (File.Exists(path))
        {
            byte[] buffer;
            await using (var file = File.OpenRead(path))
            {
                buffer = new byte[file.Length];
                await file.ReadExactlyAsync(buffer);
            }

            var loaded = X509CertificateLoader.LoadPkcs12(buffer, CertPassword,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
            if (loaded.NotAfter > DateTime.UtcNow.AddDays(30))
                return loaded;
            loaded.Dispose();
        }

        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            "CN=do.pishock.com, O=OpenShock Interception",
            rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("do.pishock.com");
        sanBuilder.AddIpAddress(IPAddress.Loopback);
        req.CertificateExtensions.Add(sanBuilder.Build());

        var serial = new byte[16];
        RandomNumberGenerator.Fill(serial);

        using var cert = req.Create(caCert, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(2),
            serial);

        // Combine with private key
        using var certWithKey = cert.CopyWithPrivateKey(rsa);
        var exported = X509CertificateLoader.LoadPkcs12(certWithKey.Export(X509ContentType.Pfx, CertPassword),
            CertPassword,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);

        await File.WriteAllBytesAsync(path, exported.Export(X509ContentType.Pfx, CertPassword));

        return exported;
    }

    private bool CheckCaTrusted()
    {
        if (_caCert == null) return false;
        using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);
        var found = store.Certificates.Find(X509FindType.FindByThumbprint, _caCert.Thumbprint, false);
        return found.Count > 0;
    }

    public void InstallCaCertificate()
    {
        if (_caCert == null) throw new InvalidOperationException("CA certificate not initialized.");
        using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        store.Add(_caCert);
        store.Close();
        IsCaTrusted = true;
    }

    public void RemoveCaCertificate()
    {
        if (_caCert == null) return;
        using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        var found = store.Certificates.Find(X509FindType.FindByThumbprint, _caCert.Thumbprint, false);
        foreach (var cert in found)
            store.Remove(cert);
        store.Close();
        IsCaTrusted = false;
    }
}