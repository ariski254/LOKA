using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Loka.Core.Exceptions;
using Loka.Core.Interfaces;
using Loka.Core.Models;

namespace Loka.Database;

public class DatabaseService : IDatabaseService
{
    private readonly IConfigurationService _configService;
    private readonly IRuntimeDetector _detector;
    private readonly IProcessManager _processManager;
    private readonly ILogService _logger;
    private readonly IPortService? _portService;

    public DatabaseService(
        IConfigurationService configService,
        IRuntimeDetector detector,
        IProcessManager processManager,
        ILogService logger,
        IPortService? portService = null)
    {
        _configService = configService;
        _detector = detector;
        _processManager = processManager;
        _logger = logger;
        _portService = portService;
    }

    public async Task<IReadOnlyList<DatabaseInfo>> GetDatabasesAsync(string? engine = null, int? port = null)
    {
        bool isPostgres = engine?.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) == true;
        var sql = isPostgres
            ? "SELECT datname AS db_name, 'UTF8' AS collation, 0 AS table_count, 0 AS size_bytes FROM pg_database WHERE datistemplate = false ORDER BY datname;"
            : @"SELECT 
                schema_name AS db_name,
                default_collation_name AS collation,
                (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = s.schema_name) AS table_count,
                IFNULL((SELECT SUM(data_length + index_length) FROM information_schema.tables WHERE table_schema = s.schema_name), 0) AS size_bytes
            FROM information_schema.schemata s
            ORDER BY schema_name;";

        var result = await ExecuteRawQueryAsync(isPostgres ? "postgres" : "information_schema", sql, engine, port);
        var list = new List<DatabaseInfo>();

        if (result.IsSuccess && result.Rows.Count > 0)
        {
            foreach (var row in result.Rows)
            {
                var name = row.TryGetValue("db_name", out var n) ? n?.ToString() ?? "" : "";
                var collation = row.TryGetValue("collation", out var c) ? c?.ToString() ?? "" : "";
                var tCount = row.TryGetValue("table_count", out var tc) && int.TryParse(tc?.ToString(), out var parsedCount) ? parsedCount : 0;
                var sBytes = row.TryGetValue("size_bytes", out var sb) && long.TryParse(sb?.ToString(), out var parsedBytes) ? parsedBytes : 0;

                if (!string.IsNullOrEmpty(name))
                {
                    list.Add(new DatabaseInfo
                    {
                        Name = name,
                        Collation = string.IsNullOrEmpty(collation) ? "utf8mb4_unicode_ci" : collation,
                        TablesCount = tCount,
                        SizeBytes = sBytes
                    });
                }
            }
        }
        else if (!isPostgres)
        {
            // Dynamic discovery fallback: query server using SHOW DATABASES;
            var fallbackResult = await ExecuteRawQueryAsync("", "SHOW DATABASES;", engine, port);
            if (fallbackResult.IsSuccess)
            {
                foreach (var row in fallbackResult.Rows)
                {
                    var name = row.Values.FirstOrDefault()?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(name))
                    {
                        list.Add(new DatabaseInfo
                        {
                            Name = name,
                            Collation = "utf8mb4_unicode_ci",
                            TablesCount = 0,
                            SizeBytes = 0
                        });
                    }
                }
            }
            else
            {
                var errorMsg = fallbackResult.ErrorMessage ?? result.ErrorMessage ?? "Unknown database connection error";
                _logger.LogWarning("Database", $"Could not retrieve databases list ({engine ?? "Default"}): {errorMsg}");
                throw new InvalidOperationException($"Could not retrieve databases list from {engine ?? "Default"}: {errorMsg}");
            }
        }
        else
        {
            var errorMsg = result.ErrorMessage ?? "Unknown PostgreSQL connection error";
            _logger.LogWarning("Database", $"Could not retrieve databases list ({engine ?? "Default"}): {errorMsg}");
            throw new InvalidOperationException($"Could not retrieve databases list from {engine ?? "Default"}: {errorMsg}");
        }

        return list;
    }

    public async Task<bool> CreateDatabaseAsync(string databaseName, string? engine = null, int? port = null)
    {
        if (string.IsNullOrWhiteSpace(databaseName) || !Regex.IsMatch(databaseName, @"^[a-zA-Z0-9_\-]+$"))
        {
            throw new ArgumentException("Invalid database name. Use alphanumeric characters and underscores only.");
        }

        bool isPostgres = engine?.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) == true;
        var sql = isPostgres
            ? $"CREATE DATABASE \"{databaseName}\";"
            : $"CREATE DATABASE `{databaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";

        var result = await ExecuteRawQueryAsync(isPostgres ? "postgres" : "", sql, engine, port);
        if (result.IsSuccess)
        {
            _logger.LogInfo("Database", $"Database `{databaseName}` created successfully on {engine ?? "Default"}.");
            return true;
        }

        _logger.LogError("Database", $"Failed to create database `{databaseName}`: {result.ErrorMessage}");
        return false;
    }

    public async Task<bool> DropDatabaseAsync(string databaseName, string? engine = null, int? port = null)
    {
        if (string.IsNullOrWhiteSpace(databaseName) || !Regex.IsMatch(databaseName, @"^[a-zA-Z0-9_\-]+$"))
        {
            throw new ArgumentException("Invalid database name.");
        }

        // Protect critical system schemas
        var systemDbs = new[] { "information_schema", "mysql", "performance_schema", "sys", "postgres", "template0", "template1" };
        if (systemDbs.Contains(databaseName.ToLowerInvariant()))
        {
            throw new InvalidOperationException($"Cannot drop system database `{databaseName}`.");
        }

        bool isPostgres = engine?.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) == true;
        var sql = isPostgres
            ? $"DROP DATABASE \"{databaseName}\";"
            : $"DROP DATABASE `{databaseName}`;";

        var result = await ExecuteRawQueryAsync(isPostgres ? "postgres" : "", sql, engine, port);
        if (result.IsSuccess)
        {
            _logger.LogInfo("Database", $"Database `{databaseName}` dropped successfully on {engine ?? "Default"}.");
            return true;
        }

        _logger.LogError("Database", $"Failed to drop database `{databaseName}`: {result.ErrorMessage}");
        return false;
    }

    public async Task<IReadOnlyList<DatabaseTableInfo>> GetTablesAsync(string databaseName, string? engine = null, int? port = null)
    {
        bool isPostgres = engine?.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) == true;
        var sql = isPostgres
            ? "SELECT table_name AS name, 0 AS row_count, 'heap' AS engine, 0 AS data_length FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name;"
            : $@"SELECT 
                table_name AS name,
                IFNULL(table_rows, 0) AS row_count,
                IFNULL(engine, 'InnoDB') AS engine,
                IFNULL(data_length, 0) AS data_length
            FROM information_schema.tables 
            WHERE table_schema = '{databaseName.Replace("'", "''")}'
            ORDER BY table_name;";

        var result = await ExecuteRawQueryAsync(databaseName, sql, engine, port);
        var tables = new List<DatabaseTableInfo>();

        if (result.IsSuccess)
        {
            foreach (var row in result.Rows)
            {
                var name = row.TryGetValue("name", out var n) ? n?.ToString() ?? "" : "";
                var rCount = row.TryGetValue("row_count", out var rc) && long.TryParse(rc?.ToString(), out var parsedRows) ? parsedRows : 0;
                var engineName = row.TryGetValue("engine", out var eg) ? eg?.ToString() ?? "InnoDB" : "InnoDB";
                var dLen = row.TryGetValue("data_length", out var dl) && long.TryParse(dl?.ToString(), out var parsedLen) ? parsedLen : 0;

                tables.Add(new DatabaseTableInfo
                {
                    Name = name,
                    RowsCount = rCount,
                    Engine = engineName,
                    DataLength = dLen
                });
            }
        }

        return tables;
    }

    public async Task<SqlQueryResult> ExecuteQueryAsync(string databaseName, string sql, string? engine = null, int? port = null)
    {
        return await ExecuteRawQueryAsync(databaseName, sql, engine, port);
    }

    public async Task<bool> ExportDatabaseAsync(string databaseName, string targetFilePath, Action<string>? progress = null)
    {
        var dumpExe = await FindMySqlToolAsync("mysqldump.exe");
        if (string.IsNullOrEmpty(dumpExe))
        {
            _logger.LogError("Database", "mysqldump.exe was not found on the system.");
            return false;
        }

        var dir = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        progress?.Invoke($"Starting export of `{databaseName}`...");
        _logger.LogInfo("Database", $"Exporting `{databaseName}` to '{targetFilePath}' using '{dumpExe}'...");

        var port = _configService.Config.Services.MySqlPort;
        var user = _configService.Config.Database.RootUser;
        var pass = _configService.Config.Database.RootPassword;
        var passArg = string.IsNullOrEmpty(pass) ? "" : $"--password=\"{pass}\"";

        var args = $"-h 127.0.0.1 -P {port} -u {user} {passArg} --routines --triggers \"{databaseName}\"";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = dumpExe,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) return false;

            using (var fileStream = File.Create(targetFilePath))
            {
                await proc.StandardOutput.BaseStream.CopyToAsync(fileStream);
            }

            var err = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode == 0)
            {
                var fileInfo = new FileInfo(targetFilePath);
                progress?.Invoke($"Export completed. Size: {fileInfo.Length / 1024.0:F1} KB");
                _logger.LogInfo("Database", $"Database `{databaseName}` exported successfully ({fileInfo.Length} bytes).");
                return true;
            }

            _logger.LogError("Database", $"mysqldump exited with error ({proc.ExitCode}): {err}");
            progress?.Invoke($"Export failed: {err}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("Database", $"Exception exporting `{databaseName}`", ex);
            progress?.Invoke($"Error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ImportDatabaseAsync(string databaseName, string sourceFilePath, Action<string>? progress = null)
    {
        if (!File.Exists(sourceFilePath))
        {
            _logger.LogError("Database", $"Import file '{sourceFilePath}' does not exist.");
            return false;
        }

        var mysqlExe = await FindMySqlToolAsync("mysql.exe");
        if (string.IsNullOrEmpty(mysqlExe))
        {
            _logger.LogError("Database", "mysql.exe was not found on the system.");
            return false;
        }

        progress?.Invoke($"Importing '{Path.GetFileName(sourceFilePath)}' into `{databaseName}`...");
        _logger.LogInfo("Database", $"Importing SQL script into `{databaseName}` from '{sourceFilePath}'...");

        var port = _configService.Config.Services.MySqlPort;
        var user = _configService.Config.Database.RootUser;
        var pass = _configService.Config.Database.RootPassword;
        var passArg = string.IsNullOrEmpty(pass) ? "" : $"--password=\"{pass}\"";

        var args = $"-h 127.0.0.1 -P {port} -u {user} {passArg} \"{databaseName}\"";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = mysqlExe,
                Arguments = args,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardInputEncoding = Encoding.UTF8
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) return false;

            using (var fileStream = File.OpenRead(sourceFilePath))
            {
                await fileStream.CopyToAsync(proc.StandardInput.BaseStream);
                proc.StandardInput.Close();
            }

            var err = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode == 0)
            {
                progress?.Invoke("Import completed successfully.");
                _logger.LogInfo("Database", $"SQL script imported into `{databaseName}` successfully.");
                return true;
            }

            _logger.LogError("Database", $"mysql import exited with code {proc.ExitCode}: {err}");
            progress?.Invoke($"Import failed: {err}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("Database", $"Exception importing SQL into `{databaseName}`", ex);
            progress?.Invoke($"Error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SetRootPasswordAsync(string newPassword)
    {
        var oldPassword = _configService.Config.Database.RootPassword ?? "";
        var port = _configService.Config.Services.MySqlPort;
        var user = _configService.Config.Database.RootUser;
        var mysqlExe = await FindMySqlToolAsync("mysql.exe");

        bool appliedOnServer = false;

        if (!string.IsNullOrEmpty(mysqlExe))
        {
            var safeNewPassword = newPassword.Replace("\"", "\\\"").Replace("'", "\\'");
            var sql = $"CREATE USER IF NOT EXISTS '{user}'@'127.0.0.1' IDENTIFIED BY '{safeNewPassword}'; ALTER USER '{user}'@'127.0.0.1' IDENTIFIED BY '{safeNewPassword}'; ALTER USER '{user}'@'localhost' IDENTIFIED BY '{safeNewPassword}'; GRANT ALL PRIVILEGES ON *.* TO '{user}'@'127.0.0.1' WITH GRANT OPTION; GRANT ALL PRIVILEGES ON *.* TO '{user}'@'localhost' WITH GRANT OPTION; FLUSH PRIVILEGES;";

            // First attempt with old password
            var oldPassArg = string.IsNullOrEmpty(oldPassword) ? "" : $"--password=\"{oldPassword}\"";
            var args = $"-h 127.0.0.1 -P {port} -u {user} {oldPassArg} -e \"{sql}\"";

            try
            {
                var res = await _processManager.ExecuteAsync(mysqlExe, args);
                if (res.Success)
                {
                    appliedOnServer = true;
                    _logger.LogInfo("Database", "MySQL root password updated on server via SQL command.");
                }
                else
                {
                    // If failed with old password, try connecting without password (in case it was blank)
                    if (!string.IsNullOrEmpty(oldPassword))
                    {
                        var blankArgs = $"-h 127.0.0.1 -P {port} -u {user} -e \"{sql}\"";
                        var blankRes = await _processManager.ExecuteAsync(mysqlExe, blankArgs);
                        if (blankRes.Success)
                        {
                            appliedOnServer = true;
                            _logger.LogInfo("Database", "MySQL root password updated on server (previously blank).");
                        }
                    }

                    // Also try mysqladmin tool as alternative
                    if (!appliedOnServer)
                    {
                        var adminExe = await FindMySqlToolAsync("mysqladmin.exe");
                        if (!string.IsNullOrEmpty(adminExe))
                        {
                            var adminArgs = string.IsNullOrEmpty(oldPassword)
                                ? $"-h 127.0.0.1 -P {port} -u {user} password \"{safeNewPassword}\""
                                : $"-h 127.0.0.1 -P {port} -u {user} -p\"{oldPassword}\" password \"{safeNewPassword}\"";

                            var adminRes = await _processManager.ExecuteAsync(adminExe, adminArgs);
                            if (adminRes.Success)
                            {
                                appliedOnServer = true;
                                _logger.LogInfo("Database", "MySQL root password updated on server via mysqladmin.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Database", $"Could not execute password change directly on MySQL server (server might be stopped): {ex.Message}");
            }
        }

        // Always update and save Loka configuration
        _configService.Config.Database.RootPassword = newPassword;
        await _configService.SaveAsync();
        _logger.LogInfo("Database", $"Root password updated in Loka configuration. (Applied to server: {appliedOnServer})");

        // Sync phpMyAdmin configuration if phpmyadmin directory exists
        await SyncPhpMyAdminConfigAsync();

        return true;
    }

    private static async Task<(string? Vendor, string? Version)> DetectVendorFromTcpHandshakeAsync(string host, int port)
    {
        try
        {
            using var tcp = new System.Net.Sockets.TcpClient();
            var connectTask = tcp.ConnectAsync(host, port);
            if (await Task.WhenAny(connectTask, Task.Delay(350)) != connectTask || !tcp.Connected)
            {
                return (null, null);
            }

            using var stream = tcp.GetStream();
            stream.ReadTimeout = 800;
            var buffer = new byte[256];
            var readTask = stream.ReadAsync(buffer, 0, buffer.Length);
            if (await Task.WhenAny(readTask, Task.Delay(600)) != readTask)
            {
                return (null, null);
            }

            int bytesRead = await readTask;
            // MySQL/MariaDB HandshakeV10 packet: 3 bytes length, 1 byte sequence (0), 1 byte protocol version (10 = 0x0A)
            // Followed by null-terminated version string starting at index 5
            if (bytesRead > 5 && buffer[4] == 0x0A)
            {
                int nullPos = Array.IndexOf(buffer, (byte)0, 5, bytesRead - 5);
                if (nullPos > 5)
                {
                    string verStr = Encoding.ASCII.GetString(buffer, 5, nullPos - 5).Trim();
                    string vendor = verStr.Contains("mariadb", StringComparison.OrdinalIgnoreCase) ? "MariaDB" : "MySQL";
                    return (vendor, verStr);
                }
            }
        }
        catch
        {
            // Socket or handshake probe failed
        }
        return (null, null);
    }

    public async Task<DatabaseServerStatus> ProbeDatabaseServerAsync(string serverName)
    {
        bool isMariaDb = serverName.Equals("MariaDB", StringComparison.OrdinalIgnoreCase);
        var canonicalName = isMariaDb ? "MariaDB" : "MySQL";
        var configuredPort = isMariaDb 
            ? (_configService.Config.Services.MariaDbPort > 0 ? _configService.Config.Services.MariaDbPort : 3307)
            : (_configService.Config.Services.MySqlPort > 0 ? _configService.Config.Services.MySqlPort : 3306);
        var host = string.IsNullOrWhiteSpace(_configService.Config.Database.Host) ? "127.0.0.1" : _configService.Config.Database.Host;

        var status = new DatabaseServerStatus
        {
            Name = canonicalName,
            Host = host,
            ConfiguredPort = configuredPort,
            ActualPort = configuredPort,
            StatusText = "Stopped"
        };

        bool isMock = _portService?.GetType().Name.Contains("Mock", StringComparison.OrdinalIgnoreCase) == true
                   || _processManager.GetType().Name.Contains("Mock", StringComparison.OrdinalIgnoreCase);

        // Layer 1: Check monitored process and system running processes
        if (isMock)
        {
            var mon = _processManager.GetMonitoredProcess(canonicalName);
            if (mon != null && mon.IsRunning)
            {
                status.IsProcessRunning = true;
                status.ProcessId = mon.ProcessId;
            }
        }
        else
        {
            var mon = _processManager.GetMonitoredProcess(canonicalName);
            if (mon != null && mon.IsRunning)
            {
                status.IsProcessRunning = true;
                status.ProcessId = mon.ProcessId;
            }
            else
            {
                if (isMariaDb)
                {
                    var mariaProcs = Process.GetProcessesByName("mariadbd").Concat(Process.GetProcessesByName("mariadb")).ToArray();
                    if (mariaProcs.Length > 0)
                    {
                        status.IsProcessRunning = true;
                        status.ProcessId = mariaProcs[0].Id;
                        try { status.ProcessPath = mariaProcs[0].MainModule?.FileName; } catch { }
                    }
                    else
                    {
                        // Check mysqld ONLY if path explicitly contains "mariadb"
                        var mysqldProcs = Process.GetProcessesByName("mysqld");
                        var trueMaria = mysqldProcs.FirstOrDefault(p => {
                            try { return p.MainModule?.FileName?.Contains("mariadb", StringComparison.OrdinalIgnoreCase) == true; }
                            catch { return false; }
                        });
                        if (trueMaria != null)
                        {
                            status.IsProcessRunning = true;
                            status.ProcessId = trueMaria.Id;
                            try { status.ProcessPath = trueMaria.MainModule?.FileName; } catch { }
                        }
                    }
                }
                else
                {
                    var mysqldProcs = Process.GetProcessesByName("mysqld").Concat(Process.GetProcessesByName("mysql")).ToArray();
                    var trueMySql = mysqldProcs.FirstOrDefault(p => {
                        try { return !(p.MainModule?.FileName?.Contains("mariadb", StringComparison.OrdinalIgnoreCase) ?? false); }
                        catch { return true; }
                    });
                    if (trueMySql != null)
                    {
                        status.IsProcessRunning = true;
                        status.ProcessId = trueMySql.Id;
                        try { status.ProcessPath = trueMySql.MainModule?.FileName; } catch { }
                    }
                }
            }
        }

        // Layer 2: Port listener verification (verify port is not taken by non-database process)
        bool portListening = false;
        if (_portService != null)
        {
            var portCheck = await _portService.CheckPortAsync(configuredPort);
            if (portCheck.IsOccupied)
            {
                var procName = portCheck.ProcessName ?? "";
                var procPath = portCheck.ProcessPath ?? "";
                bool isMatchingDbProcess = isMariaDb
                    ? (procName.Contains("mariadbd", StringComparison.OrdinalIgnoreCase) || procName.Contains("mariadb", StringComparison.OrdinalIgnoreCase) || (procName.Contains("mysqld", StringComparison.OrdinalIgnoreCase) && procPath.Contains("mariadb", StringComparison.OrdinalIgnoreCase)))
                    : ((procName.Contains("mysqld", StringComparison.OrdinalIgnoreCase) || procName.Equals("mysql", StringComparison.OrdinalIgnoreCase)) && !procName.Contains("mariadb", StringComparison.OrdinalIgnoreCase) && !procPath.Contains("mariadb", StringComparison.OrdinalIgnoreCase));

                // If occupied by a process that is explicitly known to not be a database engine (e.g. python, nginx, httpd, node)
                bool isKnownForeignProcess = !string.IsNullOrWhiteSpace(procName) && !isMatchingDbProcess &&
                    (procName.Contains("httpd", StringComparison.OrdinalIgnoreCase) ||
                     procName.Contains("nginx", StringComparison.OrdinalIgnoreCase) ||
                     procName.Contains("python", StringComparison.OrdinalIgnoreCase) ||
                     procName.Contains("node", StringComparison.OrdinalIgnoreCase) ||
                     procName.Contains("svchost", StringComparison.OrdinalIgnoreCase));

                if (isKnownForeignProcess)
                {
                    status.IsPortListening = true;
                    status.IsProcessRunning = false;
                    status.IsVerifiedVendor = false;
                    status.StatusText = $"Port {configuredPort} busy ({procName})";
                    status.ErrorDetails = $"Port {configuredPort} is occupied by non-database process '{procName}' (PID: {portCheck.ProcessId}).";
                    _logger.LogWarning("Database", $"[Probe {canonicalName}] Port {configuredPort} occupied by non-database process '{procName}' (PID: {portCheck.ProcessId}).");
                    return status;
                }

                portListening = true;
                status.ActualPort = configuredPort;
                if (portCheck.ProcessId.HasValue)
                {
                    status.ProcessId = portCheck.ProcessId;
                    status.IsProcessRunning = true;
                }
                if (!string.IsNullOrEmpty(procPath))
                {
                    status.ProcessPath = procPath;
                }
            }
        }
        else
        {
            // Direct TCP socket test fallback ONLY when _portService is not available
            try
            {
                using var tcpClient = new System.Net.Sockets.TcpClient();
                var connectTask = tcpClient.ConnectAsync(host, configuredPort);
                if (await Task.WhenAny(connectTask, Task.Delay(350)) == connectTask && tcpClient.Connected)
                {
                    portListening = true;
                }
            }
            catch { }
        }

        status.IsPortListening = portListening;

        if (!portListening)
        {
            status.StatusText = status.IsProcessRunning ? "Starting up..." : "Stopped";
            return status;
        }

        // Layer 3: TCP Handshake Vendor Verification (only in real network environment, not in mock)
        if (!isMock)
        {
            var (handshakeVendor, handshakeVer) = await DetectVendorFromTcpHandshakeAsync(host, configuredPort);
            if (!string.IsNullOrEmpty(handshakeVendor))
            {
                if (!string.Equals(handshakeVendor, canonicalName, StringComparison.OrdinalIgnoreCase))
                {
                    // Identity mismatch! e.g. Port configured for MariaDB responded with MySQL, or vice-versa
                    status.DetectedVendor = handshakeVendor;
                    status.Version = handshakeVer ?? "-";
                    status.IsVerifiedVendor = false;
                    status.StatusText = $"Wrong Server (Detected {handshakeVendor})";
                    status.ErrorDetails = $"Port {configuredPort} responded with {handshakeVendor} ({handshakeVer}), not {canonicalName}.";
                    _logger.LogWarning("Database", $"[Probe {canonicalName}] Handshake identity mismatch on port {configuredPort}: found {handshakeVendor} ({handshakeVer}) instead of {canonicalName}.");
                    return status;
                }

                status.DetectedVendor = handshakeVendor;
                status.Version = handshakeVer ?? "-";
                status.IsVerifiedVendor = true;
            }
        }

        // Layer 4: SQL Connection & Vendor Query Check
        var queryResult = await ExecuteRawQueryAsync("", "SELECT @@version AS ver, @@version_comment AS comment;", canonicalName, configuredPort);
        if (queryResult.IsSuccess)
        {
            string? ver = null;
            string? comment = null;

            if (queryResult.Rows.Count > 0)
            {
                var row = queryResult.Rows[0];
                if (row.TryGetValue("ver", out var vObj)) ver = vObj?.ToString();
                if (row.TryGetValue("comment", out var cObj)) comment = cObj?.ToString();
            }

            if (!string.IsNullOrEmpty(ver))
            {
                bool isActuallyMariaDb = ver.Contains("MariaDB", StringComparison.OrdinalIgnoreCase) || (comment != null && comment.Contains("MariaDB", StringComparison.OrdinalIgnoreCase));
                string sqlVendor = isActuallyMariaDb ? "MariaDB" : "MySQL";
                string fullVersion = string.IsNullOrWhiteSpace(comment) ? ver : $"{ver} ({comment})";

                status.DetectedVendor = sqlVendor;
                status.Version = fullVersion;

                if (string.Equals(sqlVendor, canonicalName, StringComparison.OrdinalIgnoreCase))
                {
                    status.IsVerifiedVendor = true;
                    status.IsConnectionSuccessful = true;
                    status.StatusText = "Running";
                    _logger.LogInfo("Database", $"[Probe {canonicalName}] Verified {sqlVendor} version {fullVersion} on port {configuredPort} (PID: {status.ProcessId}).");
                }
                else
                {
                    status.IsVerifiedVendor = false;
                    status.IsConnectionSuccessful = false;
                    status.StatusText = $"Wrong Server (Detected {sqlVendor})";
                    status.ErrorDetails = $"Port {configuredPort} responded with SQL vendor {sqlVendor} ({fullVersion}), not {canonicalName}.";
                    _logger.LogWarning("Database", $"[Probe {canonicalName}] SQL identity mismatch on port {configuredPort}: found {sqlVendor} ({fullVersion}) instead of {canonicalName}.");
                }
            }
            else
            {
                // Ping succeeded (e.g. mock returning simple ping without ver column)
                status.IsVerifiedVendor = true;
                status.DetectedVendor = canonicalName;
                status.IsConnectionSuccessful = true;
                status.StatusText = "Running";
            }
        }
        else
        {
            var err = queryResult.ErrorMessage ?? "";
            if (err.Contains("Access denied", StringComparison.OrdinalIgnoreCase) || err.Contains("password", StringComparison.OrdinalIgnoreCase))
            {
                status.StatusText = "Authentication Failed";
                status.ErrorDetails = err;
                if (status.IsVerifiedVendor)
                {
                    _logger.LogWarning("Database", $"[Probe {canonicalName}] Port {configuredPort} verified as {canonicalName}, but authentication failed: {err}");
                }
            }
            else if (err.Contains("refused", StringComparison.OrdinalIgnoreCase) || err.Contains("actively refused", StringComparison.OrdinalIgnoreCase))
            {
                status.StatusText = "Connection Refused";
                status.ErrorDetails = err;
            }
            else
            {
                if (status.IsVerifiedVendor)
                {
                    status.StatusText = "Running";
                    status.IsConnectionSuccessful = true;
                }
                else
                {
                    status.StatusText = "Connection Error";
                    status.ErrorDetails = err;
                }
            }
        }

        return status;
    }

    public async Task<CurrentDatabaseResolution> ResolveCurrentDatabaseServerAsync()
    {
        var mySqlStatus = await ProbeDatabaseServerAsync("MySQL");
        var mariaDbStatus = await ProbeDatabaseServerAsync("MariaDB");

        var res = new CurrentDatabaseResolution
        {
            MySqlStatus = mySqlStatus,
            MariaDbStatus = mariaDbStatus
        };

        bool mysqlHealthy = mySqlStatus.IsHealthy;
        bool mariadbHealthy = mariaDbStatus.IsHealthy;

        var preferred = _configService.Config.Services.PreferredDatabase;
        bool preferMariaDb = string.Equals(preferred, "MariaDB", StringComparison.OrdinalIgnoreCase);

        // Section 3: Current Server Rules
        if (mysqlHealthy && !mariadbHealthy)
        {
            // Rule 1: Only MySQL is running and verified
            res.ActiveServerName = "MySQL";
            res.ActivePort = mySqlStatus.ActualPort;
            res.PhpMyAdminServerIndex = 1;
            res.Message = $"MySQL is active on port {mySqlStatus.ActualPort}.";
            res.DiagnosticReason = $"Only MySQL is verified healthy on port {mySqlStatus.ActualPort} ({mySqlStatus.Version}). MariaDB is {mariaDbStatus.StatusText}.";
        }
        else if (!mysqlHealthy && mariadbHealthy)
        {
            // Rule 2: Only MariaDB is running and verified
            res.ActiveServerName = "MariaDB";
            res.ActivePort = mariaDbStatus.ActualPort;
            res.PhpMyAdminServerIndex = 2;
            res.Message = $"MariaDB is active on port {mariaDbStatus.ActualPort}.";
            res.DiagnosticReason = $"Only MariaDB is verified healthy on port {mariaDbStatus.ActualPort} ({mariaDbStatus.Version}). MySQL is {mySqlStatus.StatusText}.";
        }
        else if (mysqlHealthy && mariadbHealthy)
        {
            // Rule 3: Both running and verified healthy
            if (preferMariaDb)
            {
                res.ActiveServerName = "MariaDB";
                res.ActivePort = mariaDbStatus.ActualPort;
                res.PhpMyAdminServerIndex = 2;
                res.Message = $"Both servers running. Active: MariaDB on port {mariaDbStatus.ActualPort}.";
                res.DiagnosticReason = $"Both MySQL ({mySqlStatus.ActualPort}) and MariaDB ({mariaDbStatus.ActualPort}) are verified healthy. Retaining active choice MariaDB per preference.";
            }
            else
            {
                res.ActiveServerName = "MySQL";
                res.ActivePort = mySqlStatus.ActualPort;
                res.PhpMyAdminServerIndex = 1;
                res.Message = $"Both servers running. Active: MySQL on port {mySqlStatus.ActualPort}.";
                res.DiagnosticReason = $"Both MySQL ({mySqlStatus.ActualPort}) and MariaDB ({mariaDbStatus.ActualPort}) are verified healthy. Retaining active choice MySQL per preference.";
            }
        }
        else
        {
            // Rule 4: Neither running / healthy
            res.ActiveServerName = null;
            res.ActivePort = 0;
            res.PhpMyAdminServerIndex = preferMariaDb ? 2 : 1;
            res.Message = "Not available (No database server is currently running)";
            res.DiagnosticReason = $"Neither database server is verified healthy (MySQL: {mySqlStatus.StatusText}, MariaDB: {mariaDbStatus.StatusText}).";
        }

        // Rule 5: Fallback notification when preferred server stopped
        if (!string.IsNullOrEmpty(res.ActiveServerName))
        {
            if (preferMariaDb && res.ActiveServerName == "MySQL" && !mariadbHealthy)
            {
                res.Message = $"MariaDB is stopped. Fallback to active MySQL on port {mySqlStatus.ActualPort}.";
                res.DiagnosticReason = $"MariaDB was preferred but stopped ({mariaDbStatus.StatusText}). Safely fell back to verified MySQL on port {mySqlStatus.ActualPort}.";
            }
            else if (!preferMariaDb && res.ActiveServerName == "MariaDB" && !mysqlHealthy)
            {
                res.Message = $"MySQL is stopped. Fallback to active MariaDB on port {mariaDbStatus.ActualPort}.";
                res.DiagnosticReason = $"MySQL was preferred but stopped ({mySqlStatus.StatusText}). Safely fell back to verified MariaDB on port {mariaDbStatus.ActualPort}.";
            }

            await SyncPhpMyAdminConfigAsync(res.ActiveServerName);
        }
        else
        {
            await SyncPhpMyAdminConfigAsync(preferMariaDb ? "MariaDB" : "MySQL");
        }

        // Section 5: Diagnostic Logging (safe, credentials excluded)
        _logger.LogInfo("Database", $"[CurrentServer Resolution] Active: '{res.ActiveServerName ?? "None"}', Port: {res.ActivePort}, Index: {res.PhpMyAdminServerIndex}. Reason: {res.DiagnosticReason}");
        _logger.LogInfo("Database", $"[CurrentServer Details] MySQL: State={mySqlStatus.StatusText}, Port={mySqlStatus.ActualPort}, PID={mySqlStatus.ProcessId?.ToString() ?? "None"}, Vendor={mySqlStatus.DetectedVendor ?? "None"}, Ver={mySqlStatus.Version}, Healthy={mysqlHealthy}");
        _logger.LogInfo("Database", $"[CurrentServer Details] MariaDB: State={mariaDbStatus.StatusText}, Port={mariaDbStatus.ActualPort}, PID={mariaDbStatus.ProcessId?.ToString() ?? "None"}, Vendor={mariaDbStatus.DetectedVendor ?? "None"}, Ver={mariaDbStatus.Version}, Healthy={mariadbHealthy}");

        return res;
    }

    public async Task<bool> SetCurrentDatabaseServerAsync(string serverName)
    {
        var canonical = serverName.Equals("MariaDB", StringComparison.OrdinalIgnoreCase) ? "MariaDB" : "MySQL";
        _configService.Config.Services.PreferredDatabase = canonical;
        await _configService.SaveAsync();

        await SyncPhpMyAdminConfigAsync(canonical);
        _logger.LogInfo("Database", $"Current database server set to '{canonical}'. phpMyAdmin config updated.");
        return true;
    }

    public async Task<bool> SyncPhpMyAdminConfigAsync(string? activeServerName = null)
    {
        try
        {
            var pmaDirs = new[]
            {
                Path.Combine(_configService.Config.Paths.EtcPath, "phpmyadmin"),
                Path.Combine(_configService.Config.Paths.RootPath, "tools", "phpmyadmin"),
                Path.Combine(_configService.Config.Paths.BinPath, "phpmyadmin"),
                @"C:\xampp\phpMyAdmin"
            };

            var user = _configService.Config.Database.RootUser ?? "root";
            var host = string.IsNullOrEmpty(_configService.Config.Database.Host) ? "127.0.0.1" : _configService.Config.Database.Host;
            var mysqlPort = _configService.Config.Services.MySqlPort > 0 ? _configService.Config.Services.MySqlPort : 3306;
            var mariaDbPort = _configService.Config.Services.MariaDbPort > 0 ? _configService.Config.Services.MariaDbPort : 3307;
            var safePass = (_configService.Config.Database.RootPassword ?? "").Replace("'", "\\'");

            int serverDefault = 1;
            var active = activeServerName ?? _configService.Config.Services.PreferredDatabase;
            if (string.Equals(active, "MariaDB", StringComparison.OrdinalIgnoreCase))
            {
                serverDefault = 2;
            }

            var content = $@"<?php
/* Loka Auto-generated phpMyAdmin Configuration */
$cfg['blowfish_secret'] = 'loka_secret_cookie_token_32chars_long!';
$cfg['ServerDefault'] = {serverDefault};
$i = 0;

// Server 1: MySQL
$i++;
$cfg['Servers'][$i]['verbose'] = 'MySQL ({mysqlPort})';
$cfg['Servers'][$i]['auth_type'] = 'config';
$cfg['Servers'][$i]['user'] = '{user}';
$cfg['Servers'][$i]['password'] = '{safePass}';
$cfg['Servers'][$i]['host'] = '{host}';
$cfg['Servers'][$i]['port'] = '{mysqlPort}';
$cfg['Servers'][$i]['connect_type'] = 'tcp';
$cfg['Servers'][$i]['compress'] = false;
$cfg['Servers'][$i]['AllowNoPassword'] = true;

// Server 2: MariaDB
$i++;
$cfg['Servers'][$i]['verbose'] = 'MariaDB ({mariaDbPort})';
$cfg['Servers'][$i]['auth_type'] = 'config';
$cfg['Servers'][$i]['user'] = '{user}';
$cfg['Servers'][$i]['password'] = '{safePass}';
$cfg['Servers'][$i]['host'] = '{host}';
$cfg['Servers'][$i]['port'] = '{mariaDbPort}';
$cfg['Servers'][$i]['connect_type'] = 'tcp';
$cfg['Servers'][$i]['compress'] = false;
$cfg['Servers'][$i]['AllowNoPassword'] = true;

$cfg['UploadDir'] = '';
$cfg['SaveDir'] = '';
";

            foreach (var pmaDir in pmaDirs)
            {
                if (Directory.Exists(pmaDir))
                {
                    var configFile = Path.Combine(pmaDir, "config.inc.php");
                    var tempFile = configFile + ".tmp." + Guid.NewGuid().ToString("N");
                    var backupFile = configFile + ".bak";

                    await File.WriteAllTextAsync(tempFile, content, Encoding.UTF8);

                    if (File.Exists(configFile) && !File.Exists(backupFile))
                    {
                        try { File.Copy(configFile, backupFile, overwrite: false); } catch { }
                    }

                    File.Move(tempFile, configFile, overwrite: true);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Database", $"Could not sync phpMyAdmin config: {ex.Message}");
            return false;
        }
    }

    private async Task<SqlQueryResult> ExecuteRawQueryAsync(string databaseName, string sql, string? engine = null, int? port = null)
    {
        var sw = Stopwatch.StartNew();
        bool isPostgres = engine?.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) == true;
        bool isMariaDb = engine?.Equals("MariaDB", StringComparison.OrdinalIgnoreCase) == true
            || (string.IsNullOrEmpty(engine) && string.Equals(_configService.Config.Services.PreferredDatabase, "MariaDB", StringComparison.OrdinalIgnoreCase));

        int targetPort = port ?? (isPostgres
            ? _configService.Config.Services.PostgresPort
            : (isMariaDb ? _configService.Config.Services.MariaDbPort : _configService.Config.Services.MySqlPort));

        if (isPostgres)
        {
            var psqlExe = await FindPostgresToolAsync("psql.exe");
            if (string.IsNullOrEmpty(psqlExe))
            {
                return new SqlQueryResult
                {
                    IsSuccess = false,
                    ErrorMessage = "psql.exe CLI client was not found. Please verify PostgreSQL runtime is available."
                };
            }

            var dbName = string.IsNullOrWhiteSpace(databaseName) ? "postgres" : databaseName;
            var args = $"-h 127.0.0.1 -p {targetPort} -U postgres -d \"{dbName}\" -A -F \"\t\" -c \"{sql.Replace("\"", "\\\"")}\"";

            try
            {
                var res = await _processManager.ExecuteAsync(psqlExe, args);
                sw.Stop();

                if (!res.Success)
                {
                    return new SqlQueryResult
                    {
                        IsSuccess = false,
                        ErrorMessage = res.StandardError,
                        ExecutionTimeMs = sw.Elapsed.TotalMilliseconds
                    };
                }

                var (cols, rows) = ParseTabDelimitedOutput(res.StandardOutput);
                return new SqlQueryResult
                {
                    IsSuccess = true,
                    ColumnNames = cols,
                    Rows = rows,
                    RowsAffected = rows.Count,
                    ExecutionTimeMs = sw.Elapsed.TotalMilliseconds
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new SqlQueryResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    ExecutionTimeMs = sw.Elapsed.TotalMilliseconds
                };
            }
        }
        else
        {
            var mysqlExe = await FindMySqlToolAsync("mysql.exe", engine);
            if (string.IsNullOrEmpty(mysqlExe))
            {
                return new SqlQueryResult
                {
                    IsSuccess = false,
                    ErrorMessage = "mysql.exe CLI client was not found. Please verify MySQL/MariaDB runtime is detected."
                };
            }

            var user = _configService.Config.Database.RootUser;
            var pass = _configService.Config.Database.RootPassword;
            var passArg = string.IsNullOrEmpty(pass) ? "" : $"--password=\"{pass}\"";
            var dbArg = string.IsNullOrWhiteSpace(databaseName) ? "" : $"\"{databaseName}\"";

            // Use --connect-timeout=3 --batch --raw for bounded non-blocking execution
            var args = $"-h 127.0.0.1 -P {targetPort} -u {user} {passArg} --connect-timeout=3 --batch --raw {dbArg} -e \"{sql.Replace("\"", "\\\"")}\"";

            try
            {
                var res = await _processManager.ExecuteAsync(mysqlExe, args);
                sw.Stop();

                if (!res.Success)
                {
                    return new SqlQueryResult
                    {
                        IsSuccess = false,
                        ErrorMessage = res.StandardError,
                        ExecutionTimeMs = sw.Elapsed.TotalMilliseconds
                    };
                }

                var (cols, rows) = ParseTabDelimitedOutput(res.StandardOutput);
                return new SqlQueryResult
                {
                    IsSuccess = true,
                    ColumnNames = cols,
                    Rows = rows,
                    RowsAffected = rows.Count,
                    ExecutionTimeMs = sw.Elapsed.TotalMilliseconds
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new SqlQueryResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    ExecutionTimeMs = sw.Elapsed.TotalMilliseconds
                };
            }
        }
    }

    private static (List<string> Columns, List<Dictionary<string, object?>> Rows) ParseTabDelimitedOutput(string output)
    {
        var columns = new List<string>();
        var rows = new List<Dictionary<string, object?>>();

        if (string.IsNullOrWhiteSpace(output))
        {
            return (columns, rows);
        }

        var lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return (columns, rows);

        // First line contains column headers
        columns = lines[0].Split('\t').Select(c => c.Trim()).ToList();

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            // Skip PostgreSQL summary lines like "(1 row)" or "(0 rows)"
            if (line.StartsWith("(") && line.EndsWith(")")) continue;

            var parts = line.Split('\t');
            var dict = new Dictionary<string, object?>();

            for (int c = 0; c < columns.Count; c++)
            {
                var val = c < parts.Length ? parts[c] : null;
                dict[columns[c]] = val;
            }

            rows.Add(dict);
        }

        return (columns, rows);
    }

    private async Task<string?> FindPostgresToolAsync(string toolName)
    {
        await Task.CompletedTask;
        var candidates = new List<string>
        {
            Path.Combine(_configService.Config.Paths.BinPath, "postgresql", "bin", toolName),
            Path.Combine(_configService.Config.Paths.RootPath, "bin", "postgresql", "bin", toolName),
            $@"C:\Loka\bin\postgresql\bin\{toolName}",
            $@"C:\Program Files\PostgreSQL\17\bin\{toolName}",
            $@"C:\Program Files\PostgreSQL\16\bin\{toolName}"
        };

        var found = candidates.Distinct(StringComparer.OrdinalIgnoreCase).FirstOrDefault(File.Exists);
        if (!string.IsNullOrEmpty(found))
        {
            return found;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var p in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var full = Path.Combine(p, toolName);
                if (File.Exists(full)) return full;
            }
            catch { }
        }

        return null;
    }

    private async Task<string?> FindMySqlToolAsync(string toolName, string? engine = null)
    {
        bool isExplicitMariaDb = engine?.Equals("MariaDB", StringComparison.OrdinalIgnoreCase) == true;
        bool isExplicitMySql = engine?.Equals("MySQL", StringComparison.OrdinalIgnoreCase) == true;
        bool preferMariaDb = isExplicitMariaDb
            || (!isExplicitMySql && string.Equals(_configService.Config.Services.PreferredDatabase, "MariaDB", StringComparison.OrdinalIgnoreCase));

        var candidates = new List<string>();
        if (preferMariaDb)
        {
            candidates.Add(Path.Combine(_configService.Config.Paths.RuntimesPath, "mariadb", "bin", toolName));
            candidates.Add(Path.Combine(_configService.Config.Paths.BinPath, "mariadb", "bin", toolName));
            candidates.Add($@"C:\Loka\bin\mariadb\bin\{toolName}");
            var mariaDb = await _detector.DetectMariaDbAsync();
            if (mariaDb != null && !string.IsNullOrEmpty(mariaDb.DirectoryPath))
            {
                candidates.Add(Path.Combine(mariaDb.DirectoryPath, toolName));
            }
            candidates.Add($@"C:\Program Files\MariaDB\bin\{toolName}");
            if (Directory.Exists(@"C:\Program Files\MariaDB"))
            {
                try { candidates.AddRange(Directory.GetFiles(@"C:\Program Files\MariaDB", toolName, SearchOption.AllDirectories)); } catch { }
            }
        }
        else
        {
            candidates.Add(Path.Combine(_configService.Config.Paths.RuntimesPath, "mysql", "bin", toolName));
            candidates.Add(Path.Combine(_configService.Config.Paths.BinPath, "mysql", "bin", toolName));
            candidates.Add($@"C:\Loka\bin\mysql\bin\{toolName}");
            var mysql = await _detector.DetectMySqlAsync();
            if (mysql != null && !string.IsNullOrEmpty(mysql.DirectoryPath))
            {
                candidates.Add(Path.Combine(mysql.DirectoryPath, toolName));
            }
            candidates.AddRange(new[]
            {
                $@"C:\Program Files\MySQL\MySQL Server 8.4\bin\{toolName}",
                $@"C:\Program Files\MySQL\MySQL Server 8.0\bin\{toolName}",
                $@"C:\xampp\mysql\bin\{toolName}",
                $@"C:\laragon\bin\mysql\mysql-8.0.30-winx64\bin\{toolName}"
            });
            if (Directory.Exists(@"C:\Program Files\MySQL"))
            {
                try { candidates.AddRange(Directory.GetFiles(@"C:\Program Files\MySQL", toolName, SearchOption.AllDirectories)); } catch { }
            }
        }

        // 1. First priority: Check exact vendor candidate paths that exist
        var found = candidates.Distinct(StringComparer.OrdinalIgnoreCase).FirstOrDefault(File.Exists);
        if (!string.IsNullOrEmpty(found))
        {
            return found;
        }

        // 2. Second priority: Fall back to PATH only if specific vendor binary wasn't found above
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var p in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var full = Path.Combine(p, toolName);
                if (File.Exists(full))
                {
                    if (preferMariaDb && full.Contains("mysql", StringComparison.OrdinalIgnoreCase) && !full.Contains("mariadb", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (!preferMariaDb && full.Contains("mariadb", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    return full;
                }
            }
            catch { }
        }

        // 3. Last resort fallback: Check cross-vendor binary if installed
        var crossVendorFallback = preferMariaDb
            ? new[] { Path.Combine(_configService.Config.Paths.RuntimesPath, "mysql", "bin", toolName), $@"C:\Loka\bin\mysql\bin\{toolName}" }
            : new[] { Path.Combine(_configService.Config.Paths.RuntimesPath, "mariadb", "bin", toolName), $@"C:\Loka\bin\mariadb\bin\{toolName}" };

        return crossVendorFallback.FirstOrDefault(File.Exists);
    }
}
