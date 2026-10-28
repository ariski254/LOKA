using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Loka.Tests;

public class ApachePhpMyAdminRoutingTests
{
    [Fact]
    public void ApacheHttpdConf_ContainsDefaultVirtualHost_ForLocalhost()
    {
        var confPath = @"C:\Loka\bin\apache\conf\httpd.conf";
        if (!File.Exists(confPath))
        {
            // Fallback to repository template or etc path
            confPath = @"C:\Loka\etc\apache\httpd.conf";
        }

        if (File.Exists(confPath))
        {
            var content = File.ReadAllText(confPath);

            Assert.Contains("ServerName localhost", content);
            Assert.Contains("ServerAlias 127.0.0.1", content);
            Assert.Contains("Alias /phpmyadmin", content);
            Assert.Contains("<VirtualHost", content);
        }
    }

    [Fact]
    public void ApacheHttpdConf_PhpMyAdmin_FastCgiUsesCorrectScriptFilenameRegex()
    {
        var confPath = @"C:\Loka\bin\apache\conf\httpd.conf";
        if (!File.Exists(confPath))
        {
            confPath = @"C:\Loka\etc\apache\httpd.conf";
        }

        if (File.Exists(confPath))
        {
            var content = File.ReadAllText(confPath);

            // FastCGI for phpMyAdmin must NOT use DOCUMENT_ROOT which points to projects directory
            // It must map SCRIPT_FILENAME to the actual phpmyadmin directory
            Assert.Matches(@"ProxyFCGISetEnvIf\s+""reqenv\('SCRIPT_NAME'\)\s*=~\s*m#\^/phpmyadmin/\(\.\*\)\$#""\s+SCRIPT_FILENAME", content);
        }
    }

    [Fact]
    public void ApacheHttpd_SyntaxCheck_ReturnsSyntaxOk()
    {
        var httpdExe = @"C:\Loka\bin\apache\bin\httpd.exe";
        var confPath = @"C:\Loka\bin\apache\conf\httpd.conf";

        if (!File.Exists(httpdExe) || !File.Exists(confPath)) return;

        var psi = new ProcessStartInfo
        {
            FileName = httpdExe,
            Arguments = "-d \"C:/Loka/bin/apache\" -t",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi)!;
        var output = (proc.StandardOutput.ReadToEnd() + " " + proc.StandardError.ReadToEnd()).Trim();
        proc.WaitForExit();

        Assert.Equal(0, proc.ExitCode);
        Assert.Contains("Syntax OK", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApacheHttpd_VirtualHostDump_DesignatesLocalhostAsDefaultServer()
    {
        var httpdExe = @"C:\Loka\bin\apache\bin\httpd.exe";
        var confPath = @"C:\Loka\bin\apache\conf\httpd.conf";

        if (!File.Exists(httpdExe) || !File.Exists(confPath)) return;

        var psi = new ProcessStartInfo
        {
            FileName = httpdExe,
            Arguments = "-d \"C:/Loka/bin/apache\" -S",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi)!;
        var output = (proc.StandardOutput.ReadToEnd() + " " + proc.StandardError.ReadToEnd()).Trim();
        proc.WaitForExit();

        Assert.Equal(0, proc.ExitCode);
        // Verify default server is localhost and not hijacked by any project virtual host
        Assert.Matches(@"(?:default server localhost|\*:80\s+localhost)", output);
    }
}
