using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Loka.Core.Interfaces;

namespace Loka.Infrastructure.Windows;

public class SslCertificateService : ISslCertificateService
{
    private readonly ILogService _logger;
    public const string CaCommonName = "Loka Development Root CA";
    public const string CaDistinguishedName = "CN=Loka Development Root CA, O=Loka, OU=Development";
    private const string CaPassword = "loka-development-ca-pfx";

    public SslCertificateService(ILogService logger)
    {
        _logger = logger;
    }

    public Task<string> EnsureLocalSslCertificateAsync(string domain, string outputDir)
    {
        return EnsureLocalSslCertificateAsync(domain, outputDir, trustInWindowsStore: true);
    }

    public Task<string> EnsureLocalSslCertificateAsync(string domain, string outputDir, bool trustInWindowsStore)
    {
        try
        {
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var certFile = Path.Combine(outputDir, $"{domain}.crt");
            var keyFile = Path.Combine(outputDir, $"{domain}.key");

            // 1. Ensure Root CA exists and is loaded
            var caDir = Path.Combine(outputDir, "ca");
            var caCert = GetOrCreateRootCa(caDir);

            // 2. Check if Root CA is trusted in Windows Certificate Store
            if (trustInWindowsStore)
            {
                _ = Task.Run(() => EnsureRootCaTrustedInternal(caCert, Path.Combine(caDir, "loka-ca.crt")));
            }

            // 3. Check if existing certificate is valid and issued by our Root CA
            if (File.Exists(certFile) && File.Exists(keyFile))
            {
                if (IsCertificateValidForDomain(certFile, domain))
                {
                    return Task.FromResult(certFile);
                }

                _logger.LogInfo("SSL", $"Existing certificate for '{domain}' is invalid, self-signed, or expired. Regenerating with Loka Root CA...");
            }

            // 4. Generate new Domain Certificate signed by Loka Root CA
            using var rsaDomain = RSA.Create(2048);
            var req = new CertificateRequest(
                $"CN={domain}, O=Loka Development, OU=Local Dev",
                rsaDomain,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // SAN (Subject Alternative Name) - Mandatory for modern Chrome/Edge
            var builder = new SubjectAlternativeNameBuilder();
            builder.AddDnsName(domain);
            builder.AddDnsName($"*.{domain}");
            builder.AddIpAddress(System.Net.IPAddress.Loopback);
            builder.AddIpAddress(System.Net.IPAddress.IPv6Loopback);
            req.CertificateExtensions.Add(builder.Build());

            // Basic constraints - End Entity (IsCA = false)
            req.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(certificateAuthority: false, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));

            // Key usage
            req.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                    critical: true));

            // Enhanced key usage (Server Authentication)
            req.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection
                    {
                        new Oid("1.3.6.1.5.5.7.3.1"), // Server Authentication
                        new Oid("1.3.6.1.5.5.7.3.2")  // Client Authentication
                    },
                    critical: false));

            // Subject Key Identifier
            req.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(req.PublicKey, critical: false));

            // Authority Key Identifier (matches CA's Subject Key Identifier)
            try
            {
                req.CertificateExtensions.Add(
                    X509AuthorityKeyIdentifierExtension.CreateFromCertificate(caCert, includeKeyIdentifier: true, includeIssuerAndSerial: false));
            }
            catch
            {
                // Fallback if not supported
            }

            // Random serial number
            var serialNumber = new byte[16];
            RandomNumberGenerator.Fill(serialNumber);

            // Chrome and CA/Browser Forum rule: Maximum validity 398 days for TLS leaf certs
            var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
            var notAfter = DateTimeOffset.UtcNow.AddDays(365);

            using var leafCert = req.Create(
                caCert,
                notBefore,
                notAfter,
                serialNumber);

            // Write fullchain: Leaf Cert + CA Cert
            var leafPem = leafCert.ExportCertificatePem();
            var caPem = caCert.ExportCertificatePem();
            var fullchainPem = leafPem.TrimEnd() + Environment.NewLine + caPem.TrimEnd() + Environment.NewLine;
            File.WriteAllText(certFile, fullchainPem, Encoding.ASCII);

            // Write private key (PKCS#8 PEM)
            var keyPem = rsaDomain.ExportPkcs8PrivateKeyPem();
            File.WriteAllText(keyFile, keyPem, Encoding.ASCII);

            _logger.LogInfo("SSL", $"Generated trusted SSL certificate for '{domain}' signed by '{CaCommonName}' at '{certFile}'.");
            return Task.FromResult(certFile);
        }
        catch (Exception ex)
        {
            _logger.LogError("SSL", $"Failed to generate local SSL certificate for '{domain}'", ex);
            throw;
        }
    }

    public Task<bool> EnsureRootCertificateAuthorityAsync(string sslDir)
    {
        try
        {
            var caDir = Path.Combine(sslDir, "ca");
            var caCert = GetOrCreateRootCa(caDir);
            return Task.FromResult(caCert != null);
        }
        catch (Exception ex)
        {
            _logger.LogError("SSL", "Failed ensuring Root Certificate Authority", ex);
            return Task.FromResult(false);
        }
    }

    public Task<bool> TrustRootCertificateAsync(string sslDir)
    {
        try
        {
            var caDir = Path.Combine(sslDir, "ca");
            var caCert = GetOrCreateRootCa(caDir);
            var caCertPath = Path.Combine(caDir, "loka-ca.crt");

            bool ok = EnsureRootCaTrustedInternal(caCert, caCertPath);
            return Task.FromResult(ok);
        }
        catch (Exception ex)
        {
            _logger.LogError("SSL", "Failed to trust Root Certificate Authority in Windows", ex);
            return Task.FromResult(false);
        }
    }

    public bool IsRootCertificateTrusted(string sslDir)
    {
        try
        {
            var caDir = Path.Combine(sslDir, "ca");
            var caCertPath = Path.Combine(caDir, "loka-ca.crt");
            if (!File.Exists(caCertPath))
            {
                return false;
            }

            using var caCert = new X509Certificate2(caCertPath);
            return IsRootCaInstalled(caCert);
        }
        catch
        {
            return false;
        }
    }

    public async Task<int> RegenerateAllCertificatesAsync(string sslDir)
    {
        int count = 0;
        try
        {
            if (!Directory.Exists(sslDir))
            {
                return 0;
            }

            var crtFiles = Directory.GetFiles(sslDir, "*.crt");
            foreach (var crt in crtFiles)
            {
                var fileName = Path.GetFileName(crt);
                if (fileName.Equals("loka-ca.crt", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var domain = fileName.Substring(0, fileName.Length - 4); // strip .crt
                if (!IsCertificateValidForDomain(crt, domain))
                {
                    _logger.LogInfo("SSL", $"Regenerating certificate for domain: {domain}");
                    var keyFile = Path.Combine(sslDir, $"{domain}.key");
                    try { if (File.Exists(crt)) File.Delete(crt); } catch { }
                    try { if (File.Exists(keyFile)) File.Delete(keyFile); } catch { }

                    await EnsureLocalSslCertificateAsync(domain, sslDir);
                    count++;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("SSL", "Failed while regenerating SSL certificates", ex);
        }

        return count;
    }

    private X509Certificate2 GetOrCreateRootCa(string caDir)
    {
        if (!Directory.Exists(caDir))
        {
            Directory.CreateDirectory(caDir);
        }

        var caCertFile = Path.Combine(caDir, "loka-ca.crt");
        var caKeyFile = Path.Combine(caDir, "loka-ca.key");
        var caPfxFile = Path.Combine(caDir, "loka-ca.pfx");

        if (File.Exists(caPfxFile) && File.Exists(caCertFile))
        {
            try
            {
                var loadedCert = new X509Certificate2(
                    caPfxFile,
                    CaPassword,
                    X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);

                if (loadedCert.HasPrivateKey && loadedCert.NotAfter > DateTime.UtcNow.AddDays(30))
                {
                    return loadedCert;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("SSL", $"Failed to load existing CA from '{caPfxFile}': {ex.Message}. Regenerating CA...");
            }
        }

        // Generate persistent Root CA (RSA 4096-bit, 10-year validity)
        using var rsaCa = RSA.Create(4096);
        var subject = new X500DistinguishedName(CaDistinguishedName);
        var caReq = new CertificateRequest(
            subject,
            rsaCa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Basic Constraints: IS CA
        caReq.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));

        // Key Usage: Cert Signing + CRL Signing
        caReq.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature,
                critical: true));

        // Subject Key Identifier
        caReq.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(caReq.PublicKey, critical: false));

        var caNotBefore = DateTimeOffset.UtcNow.AddDays(-1);
        var caNotAfter = DateTimeOffset.UtcNow.AddYears(10);

        using var selfSignedCa = caReq.CreateSelfSigned(caNotBefore, caNotAfter);

        // Export PFX with private key
        var pfxBytes = selfSignedCa.Export(X509ContentType.Pfx, CaPassword);
        File.WriteAllBytes(caPfxFile, pfxBytes);

        // Export PEM certificate
        var certPem = selfSignedCa.ExportCertificatePem();
        File.WriteAllText(caCertFile, certPem, Encoding.ASCII);

        // Export PEM private key
        var keyPem = rsaCa.ExportPkcs8PrivateKeyPem();
        File.WriteAllText(caKeyFile, keyPem, Encoding.ASCII);

        _logger.LogInfo("SSL", $"Generated new Loka Root CA at '{caCertFile}'.");

        return new X509Certificate2(
            caPfxFile,
            CaPassword,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
    }

    private bool IsCertificateValidForDomain(string certFile, string domain)
    {
        try
        {
            using var cert = new X509Certificate2(certFile);

            // Must be issued by our CA, not self-signed
            if (!cert.Issuer.Contains(CaCommonName))
            {
                return false;
            }

            // Expiry check (must have at least 14 days left)
            if (cert.NotAfter <= DateTime.UtcNow.AddDays(14) || cert.NotBefore > DateTime.UtcNow)
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsRootCaInstalled(X509Certificate2 caCert)
    {
        foreach (var location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
        {
            try
            {
                using var store = new X509Store(StoreName.Root, location);
                store.Open(OpenFlags.ReadOnly);
                var found = store.Certificates.Find(X509FindType.FindByThumbprint, caCert.Thumbprint, false);
                if (found.Count > 0)
                {
                    return true;
                }
            }
            catch
            {
                // Ignore read permission errors
            }
        }
        return false;
    }

    private bool EnsureRootCaTrustedInternal(X509Certificate2 caCert, string caCertPath)
    {
        if (IsRootCaInstalled(caCert))
        {
            return true;
        }

        try
        {
            // First attempt: CurrentUser Root store via X509Store API
            using var userStore = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            userStore.Open(OpenFlags.ReadWrite);
            userStore.Add(caCert);
            userStore.Close();
            _logger.LogInfo("SSL", "Loka Root CA installed successfully to Windows CurrentUser Root store.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("SSL", $"Direct X509Store addition to CurrentUser failed: {ex.Message}. Falling back to certutil...");
            try
            {
                // Fallback: certutil
                if (File.Exists(caCertPath))
                {
                    var psi = new ProcessStartInfo("certutil", $"-user -addstore Root \"{caCertPath}\"")
                    {
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using var p = System.Diagnostics.Process.Start(psi);
                    p?.WaitForExit(5000);
                    return IsRootCaInstalled(caCert);
                }
            }
            catch (Exception cEx)
            {
                _logger.LogError("SSL", "Failed to trust Loka Root CA via certutil", cEx);
            }
        }

        return false;
    }
}
