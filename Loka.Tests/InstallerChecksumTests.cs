using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Loka.Installer.Models;
using Loka.Installer.Services;
using Xunit;

namespace Loka.Tests;

public class InstallerChecksumTests
{
    private static (byte[] bytes, string hash) CreateSamplePackage(string content = "Sample Package Binary Content")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(bytes);
        var hash = Convert.ToHexString(hashBytes);
        return (bytes, hash);
    }

    [Fact]
    public void Test1_VerifyFileSha256_ValidFileAndMatchingHash_ReturnsTrue()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"loka_valid_{Guid.NewGuid():N}.dat");
        var (bytes, expectedHash) = CreateSamplePackage("Sample Loka Valid Binary 1234567890");
        File.WriteAllBytes(tempFile, bytes);

        try
        {
            bool ok = StackProvisioningService.VerifyFileSha256(tempFile, expectedHash, out var actualHash, out var failureReason);

            Assert.True(ok);
            Assert.Equal(expectedHash, actualHash);
            Assert.Null(failureReason);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Test2_VerifyFileSha256_HashMismatch_ReturnsFalseAndSpecificFailureReason()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"loka_mismatch_{Guid.NewGuid():N}.dat");
        var (bytes, _) = CreateSamplePackage("Original Binary");
        File.WriteAllBytes(tempFile, bytes);
        var fakeHash = new string('F', 64);

        try
        {
            bool ok = StackProvisioningService.VerifyFileSha256(tempFile, fakeHash, out var actualHash, out var failureReason);

            Assert.False(ok);
            Assert.NotEmpty(actualHash);
            Assert.NotEqual(fakeHash, actualHash);
            Assert.NotNull(failureReason);
            Assert.Contains("checksum verification", failureReason, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Test3_VerifyFileSha256_EmptyOrZeroByteFile_FailsWithDescriptiveMessage()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"loka_empty_{Guid.NewGuid():N}.dat");
        File.WriteAllBytes(tempFile, Array.Empty<byte>());
        var expectedHash = new string('A', 64);

        try
        {
            bool ok = StackProvisioningService.VerifyFileSha256(tempFile, expectedHash, out _, out var failureReason);

            Assert.False(ok);
            Assert.NotNull(failureReason);
            Assert.Contains("0 bytes", failureReason);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Test4_VerifyFileSha256_HtmlErrorResponse_DetectedAsHtml()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"loka_html_{Guid.NewGuid():N}.dat");
        var htmlError = "<!DOCTYPE html><html><head><title>404 Not Found</title></head><body><h1>404 Not Found</h1></body></html>";
        File.WriteAllText(tempFile, htmlError);
        var expectedHash = new string('B', 64);

        try
        {
            bool ok = StackProvisioningService.VerifyFileSha256(tempFile, expectedHash, out _, out var failureReason);

            Assert.False(ok);
            Assert.NotNull(failureReason);
            Assert.Contains("HTML error", failureReason, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Test5_NormalizeHash_HandlesPrefixesHyphensAndCasing()
    {
        var raw1 = "sha256:aad7bf75d669ece7671688bfdf35f1093d6a30d2e62469405f4d55d8d82d5fd3";
        var raw2 = "AAD7-BF75-D669-ECE7-6716-88BF-DF35-F109-3D6A-30D2-E624-6940-5F4D-55D8-D82D-5FD3";
        var raw3 = "   aad7bf75d669ece7671688bfdf35f1093d6a30d2e62469405f4d55d8d82d5fd3   ";

        var expected = "AAD7BF75D669ECE7671688BFDF35F1093D6A30D2E62469405F4D55D8D82D5FD3";

        Assert.Equal(expected, StackProvisioningService.NormalizeHash(raw1));
        Assert.Equal(expected, StackProvisioningService.NormalizeHash(raw2));
        Assert.Equal(expected, StackProvisioningService.NormalizeHash(raw3));
    }

    [Fact]
    public async Task Test6_DownloadPackage_ValidDownload_SucceedsAndMatchesChecksum()
    {
        var (bytes, hash) = CreateSamplePackage("Mock Valid Zip Content For Unit Test");
        var testDir = Path.Combine(Path.GetTempPath(), $"loka_test_prov_{Guid.NewGuid():N}");
        var envDir = Path.Combine(testDir, "env");
        var cacheDir = Path.Combine(testDir, "cache");
        Directory.CreateDirectory(testDir);
        Directory.CreateDirectory(envDir);
        Directory.CreateDirectory(cacheDir);

        var mockHandler = new MockHttpMessageHandler((req) =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            };
            res.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
            return Task.FromResult(res);
        });

        using var client = new HttpClient(mockHandler);
        var service = new StackProvisioningService(client);

        try
        {
            var comp = new StackProvisioningService.StackComponent(
                "TestComp",
                "1.0.0",
                "testcomp.bin",
                "https://test.loka.local/testcomp.bin",
                null,
                Path.Combine(envDir, "bin", "testcomp"),
                IsZip: false,
                TargetBinaryRelative: "testcomp.bin",
                ExpectedSha256: hash,
                ExpectedSizeBytes: bytes.Length);

            var targetCacheFile = Path.Combine(cacheDir, comp.PackageFileName);

            // Use reflection to invoke internal/private ProvisionComponentAsync
            var method = typeof(StackProvisioningService).GetMethod("ProvisionComponentAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);

            var task = (Task)method.Invoke(service, new object[]
            {
                comp,
                cacheDir,
                50,
                new Action<int, string>((_, _) => { }),
                false,
                CancellationToken.None
            })!;
            await task;

            // Verify file downloaded to cache
            Assert.True(File.Exists(targetCacheFile));
            Assert.Equal(bytes.Length, new FileInfo(targetCacheFile).Length);

            // Verify installed file in destination
            var installedFile = Path.Combine(comp.DestinationDirectory, comp.TargetBinaryRelative);
            Assert.True(File.Exists(installedFile));
        }
        finally
        {
            try { Directory.Delete(testDir, true); } catch { }
        }
    }

    [Fact]
    public async Task Test7_DownloadPackage_ChecksumMismatch_ThrowsPackageInstallationExceptionAndCleansTempFile()
    {
        var (bytes, _) = CreateSamplePackage("Mock Corrupted Binary Content");
        var expectedWrongHash = new string('E', 64);

        var testDir = Path.Combine(Path.GetTempPath(), $"loka_test_fail_{Guid.NewGuid():N}");
        var envDir = Path.Combine(testDir, "env");
        var cacheDir = Path.Combine(testDir, "cache");
        Directory.CreateDirectory(testDir);
        Directory.CreateDirectory(envDir);
        Directory.CreateDirectory(cacheDir);

        var mockHandler = new MockHttpMessageHandler((req) =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            };
            res.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
            return Task.FromResult(res);
        });

        using var client = new HttpClient(mockHandler);
        var service = new StackProvisioningService(client);

        try
        {
            var comp = new StackProvisioningService.StackComponent(
                "Nginx",
                "1.28.3",
                "nginx.zip",
                "https://test.loka.local/nginx.zip",
                null,
                Path.Combine(envDir, "bin", "nginx"),
                IsZip: true,
                TargetBinaryRelative: "nginx.exe",
                ExpectedSha256: expectedWrongHash,
                ExpectedSizeBytes: 1000);

            var method = typeof(StackProvisioningService).GetMethod("ProvisionComponentAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);

            var ex = await Assert.ThrowsAsync<PackageInstallationException>(async () =>
            {
                var task = (Task)method.Invoke(service, new object[]
                {
                    comp,
                    cacheDir,
                    50,
                    new Action<int, string>((_, _) => { }),
                    false,
                    CancellationToken.None
                })!;
                await task;
            });

            Assert.Equal("Nginx", ex.PackageName);
            Assert.Equal("1.28.3", ex.PackageVersion);
            Assert.Equal("Checksum Verification", ex.Phase);
            Assert.Contains("checksum verification", ex.Details, StringComparison.OrdinalIgnoreCase);

            // Verify no temp file left behind
            var tempFiles = Directory.GetFiles(cacheDir, "*.tmp*");
            Assert.Empty(tempFiles);

            // Verify corrupted file was not saved to destination
            var checkBinary = Path.Combine(comp.DestinationDirectory, comp.TargetBinaryRelative);
            Assert.False(File.Exists(checkBinary));
        }
        finally
        {
            try { Directory.Delete(testDir, true); } catch { }
        }
    }

    [Fact]
    public async Task Test8_DownloadPackage_HtmlErrorPage_ThrowsPackageInstallationException()
    {
        var htmlContent = Encoding.UTF8.GetBytes("<html><body>Error 404: Not Found</body></html>");
        var testDir = Path.Combine(Path.GetTempPath(), $"loka_test_html_{Guid.NewGuid():N}");
        var envDir = Path.Combine(testDir, "env");
        var cacheDir = Path.Combine(testDir, "cache");
        Directory.CreateDirectory(testDir);
        Directory.CreateDirectory(envDir);
        Directory.CreateDirectory(cacheDir);

        var mockHandler = new MockHttpMessageHandler((req) =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(htmlContent)
            };
            res.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
            return Task.FromResult(res);
        });

        using var client = new HttpClient(mockHandler);
        var service = new StackProvisioningService(client);

        try
        {
            var comp = new StackProvisioningService.StackComponent(
                "PHP 8.4",
                "8.4.26",
                "php-8.4.zip",
                "https://test.loka.local/php-8.4.zip",
                null,
                Path.Combine(envDir, "bin", "php"),
                IsZip: true,
                TargetBinaryRelative: "php.exe",
                ExpectedSha256: new string('A', 64),
                ExpectedSizeBytes: 1000);

            var method = typeof(StackProvisioningService).GetMethod("ProvisionComponentAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);

            var ex = await Assert.ThrowsAsync<PackageInstallationException>(async () =>
            {
                var task = (Task)method.Invoke(service, new object[]
                {
                    comp,
                    cacheDir,
                    50,
                    new Action<int, string>((_, _) => { }),
                    false,
                    CancellationToken.None
                })!;
                await task;
            });

            Assert.Equal("PHP 8.4", ex.PackageName);
            Assert.Contains("HTML", ex.Details, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(testDir, true); } catch { }
        }
    }

    [Fact]
    public async Task Test9_DownloadPackage_TransientNetworkFailureWithRetry_RecoversSuccessfully()
    {
        var (bytes, hash) = CreateSamplePackage("Mock Binary With Transient Network Glitch");
        var testDir = Path.Combine(Path.GetTempPath(), $"loka_test_retry_{Guid.NewGuid():N}");
        var envDir = Path.Combine(testDir, "env");
        var cacheDir = Path.Combine(testDir, "cache");
        Directory.CreateDirectory(testDir);
        Directory.CreateDirectory(envDir);
        Directory.CreateDirectory(cacheDir);

        int callCount = 0;
        var mockHandler = new MockHttpMessageHandler((req) =>
        {
            callCount++;
            if (callCount == 1)
            {
                // Fail on first attempt
                throw new HttpRequestException("Simulated transient network disconnect");
            }

            // Succeed on second attempt
            var res = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            };
            res.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
            return Task.FromResult(res);
        });

        using var client = new HttpClient(mockHandler);
        var service = new StackProvisioningService(client);

        try
        {
            var comp = new StackProvisioningService.StackComponent(
                "Apache",
                "2.4.66",
                "apache.bin",
                "https://test.loka.local/apache.bin",
                null,
                Path.Combine(envDir, "bin", "apache"),
                IsZip: false,
                TargetBinaryRelative: "apache.bin",
                ExpectedSha256: hash,
                ExpectedSizeBytes: bytes.Length);

            var method = typeof(StackProvisioningService).GetMethod("ProvisionComponentAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);

            var task = (Task)method.Invoke(service, new object[]
            {
                comp,
                cacheDir,
                50,
                new Action<int, string>((_, _) => { }),
                false,
                CancellationToken.None
            })!;
            await task;

            Assert.True(callCount >= 2, "Downloader should have retried after initial transient failure.");
            var targetFile = Path.Combine(cacheDir, comp.PackageFileName);
            Assert.True(File.Exists(targetFile));
        }
        finally
        {
            try { Directory.Delete(testDir, true); } catch { }
        }
    }

    [Fact]
    public async Task Test10_CorruptCache_IsDiscardedAndRedownloaded()
    {
        var (bytes, hash) = CreateSamplePackage("Fresh Valid Binary Content");
        var testDir = Path.Combine(Path.GetTempPath(), $"loka_test_cache_{Guid.NewGuid():N}");
        var envDir = Path.Combine(testDir, "env");
        var cacheDir = Path.Combine(testDir, "cache");
        Directory.CreateDirectory(testDir);
        Directory.CreateDirectory(envDir);
        Directory.CreateDirectory(cacheDir);

        // Pre-seed corrupt cache file
        var targetFile = Path.Combine(cacheDir, "pkg.bin");
        File.WriteAllBytes(targetFile, Encoding.UTF8.GetBytes("Corrupt Old Cached Content That Should Be Replaced"));

        int downloadCalls = 0;
        var mockHandler = new MockHttpMessageHandler((req) =>
        {
            downloadCalls++;
            var res = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            };
            res.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            return Task.FromResult(res);
        });

        using var client = new HttpClient(mockHandler);
        var service = new StackProvisioningService(client);

        try
        {
            var comp = new StackProvisioningService.StackComponent(
                "CustomPkg",
                "1.0.0",
                "pkg.bin",
                "https://test.loka.local/pkg.bin",
                null,
                Path.Combine(envDir, "bin", "pkg"),
                IsZip: false,
                TargetBinaryRelative: "pkg.bin",
                ExpectedSha256: hash,
                ExpectedSizeBytes: bytes.Length);

            var method = typeof(StackProvisioningService).GetMethod("ProvisionComponentAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);

            var task = (Task)method.Invoke(service, new object[]
            {
                comp,
                cacheDir,
                50,
                new Action<int, string>((_, _) => { }),
                false,
                CancellationToken.None
            })!;
            await task;

            Assert.Equal(1, downloadCalls);
            // Verify cache file now matches the new genuine content
            Assert.Equal(bytes.Length, new FileInfo(targetFile).Length);
            bool verified = StackProvisioningService.VerifyFileSha256(targetFile, hash, out _, out _);
            Assert.True(verified);
        }
        finally
        {
            try { Directory.Delete(testDir, true); } catch { }
        }
    }

    [Fact]
    public void Test11_NginxGenuinePackage_ChecksumMatchesOfficialRelease()
    {
        var localNginxZip = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "artifacts", "cache", "nginx.zip");
        if (File.Exists(localNginxZip))
        {
            var officialExpectedSha256 = "AAD7BF75D669ECE7671688BFDF35F1093D6A30D2E62469405F4D55D8D82D5FD3";
            bool matches = StackProvisioningService.VerifyFileSha256(localNginxZip, officialExpectedSha256, out var actualHash, out var failure);

            Assert.True(matches, $"Nginx package in cache must match official SHA-256 {officialExpectedSha256}. Details: {failure}");
            Assert.Equal(officialExpectedSha256, actualHash);
        }
    }

    [Fact]
    public async Task Test12_FallbackUrl_UsedWhenPrimaryUrlFailsWith404()
    {
        var (bytes, hash) = CreateSamplePackage("Package Content From Fallback URL");
        var testDir = Path.Combine(Path.GetTempPath(), $"loka_test_fallback_{Guid.NewGuid():N}");
        var envDir = Path.Combine(testDir, "env");
        var cacheDir = Path.Combine(testDir, "cache");
        Directory.CreateDirectory(testDir);
        Directory.CreateDirectory(envDir);
        Directory.CreateDirectory(cacheDir);

        var requestedUrls = new List<string>();
        var mockHandler = new MockHttpMessageHandler((req) =>
        {
            var url = req.RequestUri!.ToString();
            requestedUrls.Add(url);

            if (url.Contains("primary-404"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            var res = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            };
            res.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            return Task.FromResult(res);
        });

        using var client = new HttpClient(mockHandler);
        var service = new StackProvisioningService(client);

        try
        {
            var comp = new StackProvisioningService.StackComponent(
                "FallbackPkg",
                "1.0.0",
                "fallback.bin",
                "https://test.loka.local/primary-404.bin",
                new[] { "https://test.loka.local/fallback-archive.bin" },
                Path.Combine(envDir, "bin", "fallback"),
                IsZip: false,
                TargetBinaryRelative: "fallback.bin",
                ExpectedSha256: hash,
                ExpectedSizeBytes: bytes.Length);

            var method = typeof(StackProvisioningService).GetMethod("ProvisionComponentAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);

            var task = (Task)method.Invoke(service, new object[]
            {
                comp,
                cacheDir,
                50,
                new Action<int, string>((_, _) => { }),
                false,
                CancellationToken.None
            })!;
            await task;

            Assert.Contains(requestedUrls, u => u.Contains("primary-404"));
            Assert.Contains(requestedUrls, u => u.Contains("fallback-archive"));
            var targetFile = Path.Combine(cacheDir, comp.PackageFileName);
            Assert.True(File.Exists(targetFile));
        }
        finally
        {
            try { Directory.Delete(testDir, true); } catch { }
        }
    }

    [Fact]
    public async Task Test13_CleanInstallation_NginxPackageExtractsAndExecutesSuccessfully()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"loka_nginx_clean_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        try
        {
            var service = new StackProvisioningService();
            await service.ProvisionAsync(
                testDir,
                (_, _) => { },
                selectedComponents: new[] { "Nginx" },
                cancellationToken: CancellationToken.None);

            var nginxExe = Path.Combine(testDir, "bin", "nginx", "nginx.exe");
            Assert.True(File.Exists(nginxExe), $"Nginx binary must exist at '{nginxExe}' after provisioning.");

            // Execute nginx -v to verify binary integrity and execution
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = nginxExe,
                Arguments = "-v",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            Assert.NotNull(proc);
            await proc.WaitForExitAsync();

            var output = (await proc.StandardError.ReadToEndAsync() + " " + await proc.StandardOutput.ReadToEndAsync()).Trim();
            Assert.Equal(0, proc.ExitCode);
            Assert.Contains("nginx/1.28.3", output);
        }
        finally
        {
            try { Directory.Delete(testDir, true); } catch { }
        }
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request);
        }
    }
}
