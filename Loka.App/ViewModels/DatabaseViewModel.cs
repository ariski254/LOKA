using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Loka.Core.Enums;
using Loka.Core.Interfaces;
using Loka.Core.Models;
using Loka.App.Common;
using Microsoft.Win32;

namespace Loka.App.ViewModels;

public partial class DatabaseViewModel : ObservableObject
{
    private readonly IDatabaseService _databaseService;
    private readonly IConfigurationService _configService;
    private readonly IServiceManager _serviceManager;
    private readonly IPortService _portService;
    private readonly ILogService _logger;
    private readonly IRuntimeDetector? _detector;

    [ObservableProperty]
    private string _selectedEngine = "MySQL";

    [ObservableProperty]
    private DatabaseEngineItem? _selectedDatabaseEngine;

    public ObservableCollection<string> AvailableEngines { get; } = new() { "MySQL", "MariaDB", "PostgreSQL" };

    /// <summary>
    /// Dedicated list of all supported database engines (MySQL, MariaDB, PostgreSQL) per Desain.md Section 5.
    /// Always displays all 3 supported engines with distinct service status, ports, executable paths, and controls.
    /// </summary>
    public ObservableCollection<DatabaseEngineItem> DatabaseEngines { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _connectionHost = "127.0.0.1";

    [ObservableProperty]
    private int _connectionPort = 3307;

    [ObservableProperty]
    private string _connectionUser = "root";

    [ObservableProperty]
    private string _connectionStatus = "Checking...";

    [ObservableProperty]
    private DatabaseInfo? _selectedDatabase;

    [ObservableProperty]
    private string _sqlQuery = "SHOW TABLES;";

    [ObservableProperty]
    private string _newDatabaseName = string.Empty;

    [ObservableProperty]
    private DataTable? _queryResultTable;

    [ObservableProperty]
    private string _queryExecutionStats = string.Empty;

    public ObservableCollection<DatabaseInfo> Databases { get; } = new();
    public ObservableCollection<DatabaseTableInfo> Tables { get; } = new();

    public string ConnectionEngineAndHost => $"{SelectedEngine} ({ConnectionHost}:{ConnectionPort})";

    // --- phpMyAdmin Section (Agent.md §5.C) ---
    [ObservableProperty]
    private bool _isPhpMyAdminAvailable;

    [ObservableProperty]
    private string _phpMyAdminUrl = "http://localhost/phpmyadmin";

    [ObservableProperty]
    private string _phpMyAdminStatus = "Not checked";

    [ObservableProperty]
    private bool _isPhpMyAdminLoading;

    public DatabaseViewModel(
        IDatabaseService databaseService,
        IConfigurationService configService,
        IServiceManager serviceManager,
        IPortService portService,
        ILogService logger,
        IRuntimeDetector? detector = null)
    {
        _databaseService = databaseService;
        _configService = configService;
        _serviceManager = serviceManager;
        _portService = portService;
        _logger = logger;
        _detector = detector;

        var preferred = _configService.Config.Services.PreferredDatabase;
        if (!string.IsNullOrWhiteSpace(preferred) && AvailableEngines.Contains(preferred))
        {
            SelectedEngine = preferred;
        }

        SyncEngineSelection();
        _ = RefreshDatabaseEnginesAsync();

        _serviceManager.ServiceStateChanged += OnServiceStateChanged;
        _configService.ConfigurationChanged += OnConfigurationChanged;
    }

    private void OnServiceStateChanged(ServiceInfo info)
    {
        UiDispatcher.Run(() =>
        {
            _ = RefreshDatabaseEnginesAsync();
        });

        var currentType = SelectedEngine switch
        {
            "MySQL" => ServiceType.MySQL,
            "PostgreSQL" => ServiceType.PostgreSQL,
            _ => ServiceType.MariaDB
        };

        if (info.Type == currentType)
        {
            UiDispatcher.Run(() =>
            {
                _ = LoadDatabasesAsync();
            });
        }
    }

    private void OnConfigurationChanged(LokaConfig config)
    {
        UiDispatcher.Run(() =>
        {
            SyncEngineSelection();
            _ = RefreshDatabaseEnginesAsync();
        });
    }

    public async Task InitializeAsync()
    {
        try
        {
            var resolution = await _databaseService.ResolveCurrentDatabaseServerAsync();
            if (!string.IsNullOrEmpty(resolution.ActiveServerName) && AvailableEngines.Contains(resolution.ActiveServerName))
            {
                SelectedEngine = resolution.ActiveServerName;
            }
        }
        catch { }

        SyncEngineSelection();
        await RefreshDatabaseEnginesAsync();
        await LoadDatabasesAsync();
    }

    public async Task RefreshDatabaseEnginesAsync()
    {
        await Task.CompletedTask;
        try
        {
            var binPath = _configService.Config.Paths.BinPath;
            var dataPath = _configService.Config.Paths.DataPath;

            // 1. MySQL Engine (:3306)
            var mysqlItem = DatabaseEngines.FirstOrDefault(e => e.Name == "MySQL");
            if (mysqlItem == null)
            {
                mysqlItem = new DatabaseEngineItem { Name = "MySQL", ServiceType = ServiceType.MySQL };
                DatabaseEngines.Add(mysqlItem);
            }
            mysqlItem.Port = _configService.Config.Services.MySqlPort > 0 ? _configService.Config.Services.MySqlPort : 3306;
            var mysqlSvc = _serviceManager.GetService(ServiceType.MySQL);
            mysqlItem.State = mysqlSvc?.State ?? ServiceState.Stopped;
            mysqlItem.IsRunning = mysqlSvc?.IsRunning ?? false;
            var mysqlExe = Path.Combine(binPath, "mysql", "bin", "mysqld.exe");
            mysqlItem.ExecutablePath = File.Exists(mysqlExe) ? mysqlExe : (mysqlSvc?.ExecutablePath ?? "Not detected");
            mysqlItem.DataDirectory = Path.Combine(dataPath, "mysql");
            mysqlItem.IsInstalled = File.Exists(mysqlExe) || (!string.IsNullOrEmpty(mysqlSvc?.ExecutablePath) && File.Exists(mysqlSvc.ExecutablePath));
            mysqlItem.Version = !string.IsNullOrEmpty(mysqlSvc?.Version) ? mysqlSvc.Version : (mysqlItem.IsInstalled ? "MySQL 8.4" : "Not installed");
            mysqlItem.StatusText = mysqlItem.IsRunning ? $"Active (:{mysqlItem.Port})" : (mysqlItem.IsInstalled ? "Installed · Stopped" : "Not installed");
            mysqlItem.IsCurrentTarget = SelectedEngine.Equals("MySQL", StringComparison.OrdinalIgnoreCase);
            mysqlItem.NotifyStateChanged();

            // 2. MariaDB Engine (:3307)
            var mariaItem = DatabaseEngines.FirstOrDefault(e => e.Name == "MariaDB");
            if (mariaItem == null)
            {
                mariaItem = new DatabaseEngineItem { Name = "MariaDB", ServiceType = ServiceType.MariaDB };
                DatabaseEngines.Add(mariaItem);
            }
            mariaItem.Port = _configService.Config.Services.MariaDbPort > 0 ? _configService.Config.Services.MariaDbPort : 3307;
            var mariaSvc = _serviceManager.GetService(ServiceType.MariaDB);
            mariaItem.State = mariaSvc?.State ?? ServiceState.Stopped;
            mariaItem.IsRunning = mariaSvc?.IsRunning ?? false;
            var mariaExe = Path.Combine(binPath, "mariadb", "bin", "mariadbd.exe");
            if (!File.Exists(mariaExe)) mariaExe = Path.Combine(binPath, "mariadb", "bin", "mysqld.exe");
            mariaItem.ExecutablePath = File.Exists(mariaExe) ? mariaExe : (mariaSvc?.ExecutablePath ?? "Not detected");
            mariaItem.DataDirectory = Path.Combine(dataPath, "mariadb");
            mariaItem.IsInstalled = File.Exists(mariaExe) || (!string.IsNullOrEmpty(mariaSvc?.ExecutablePath) && File.Exists(mariaSvc.ExecutablePath));
            mariaItem.Version = !string.IsNullOrEmpty(mariaSvc?.Version) ? mariaSvc.Version : (mariaItem.IsInstalled ? "MariaDB 11.4" : "Not installed");
            mariaItem.StatusText = mariaItem.IsRunning ? $"Active (:{mariaItem.Port})" : (mariaItem.IsInstalled ? "Installed · Stopped" : "Not installed");
            mariaItem.IsCurrentTarget = SelectedEngine.Equals("MariaDB", StringComparison.OrdinalIgnoreCase);
            mariaItem.NotifyStateChanged();

            // 3. PostgreSQL Engine (:5432)
            var pgItem = DatabaseEngines.FirstOrDefault(e => e.Name == "PostgreSQL");
            if (pgItem == null)
            {
                pgItem = new DatabaseEngineItem { Name = "PostgreSQL", ServiceType = ServiceType.PostgreSQL };
                DatabaseEngines.Add(pgItem);
            }
            pgItem.Port = _configService.Config.Services.PostgresPort > 0 ? _configService.Config.Services.PostgresPort : 5432;
            var pgSvc = _serviceManager.GetService(ServiceType.PostgreSQL);
            pgItem.State = pgSvc?.State ?? ServiceState.Stopped;
            pgItem.IsRunning = pgSvc?.IsRunning ?? false;
            var pgExe = Path.Combine(binPath, "postgresql", "bin", "postgres.exe");
            pgItem.ExecutablePath = File.Exists(pgExe) ? pgExe : (pgSvc?.ExecutablePath ?? "Not detected");
            pgItem.DataDirectory = Path.Combine(dataPath, "postgresql");
            pgItem.IsInstalled = File.Exists(pgExe) || (!string.IsNullOrEmpty(pgSvc?.ExecutablePath) && File.Exists(pgSvc.ExecutablePath));
            pgItem.Version = !string.IsNullOrEmpty(pgSvc?.Version) ? pgSvc.Version : (pgItem.IsInstalled ? "PostgreSQL 16" : "Not installed");
            pgItem.StatusText = pgItem.IsRunning ? $"Active (:{pgItem.Port})" : (pgItem.IsInstalled ? "Installed · Stopped" : "Not installed");
            pgItem.IsCurrentTarget = SelectedEngine.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase);
            pgItem.NotifyStateChanged();

            if (SelectedDatabaseEngine == null || !string.Equals(SelectedDatabaseEngine.Name, SelectedEngine, StringComparison.OrdinalIgnoreCase))
            {
                SelectedDatabaseEngine = DatabaseEngines.FirstOrDefault(e => e.Name.Equals(SelectedEngine, StringComparison.OrdinalIgnoreCase));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Databases", $"Error refreshing database engines: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    public async Task StartEngineAsync(DatabaseEngineItem? item)
    {
        if (item == null || item.IsBusy) return;
        item.IsBusy = true;
        item.NotifyStateChanged();
        StatusMessage = $"Starting {item.Name} on port {item.Port}...";
        try
        {
            var success = await _serviceManager.StartServiceAsync(item.ServiceType);
            StatusMessage = success ? $"{item.Name} started on port {item.Port}." : $"Failed to start {item.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error starting {item.Name}: {ex.Message}";
        }
        finally
        {
            item.IsBusy = false;
            await RefreshDatabaseEnginesAsync();
        }
    }

    [RelayCommand]
    public async Task StopEngineAsync(DatabaseEngineItem? item)
    {
        if (item == null || item.IsBusy) return;
        item.IsBusy = true;
        item.NotifyStateChanged();
        StatusMessage = $"Stopping {item.Name}...";
        try
        {
            var success = await _serviceManager.StopServiceAsync(item.ServiceType);
            StatusMessage = success ? $"{item.Name} stopped." : $"Failed to stop {item.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error stopping {item.Name}: {ex.Message}";
        }
        finally
        {
            item.IsBusy = false;
            await RefreshDatabaseEnginesAsync();
        }
    }

    [RelayCommand]
    public async Task RestartEngineAsync(DatabaseEngineItem? item)
    {
        if (item == null || item.IsBusy) return;
        item.IsBusy = true;
        item.NotifyStateChanged();
        StatusMessage = $"Restarting {item.Name}...";
        try
        {
            var success = await _serviceManager.RestartServiceAsync(item.ServiceType);
            StatusMessage = success ? $"{item.Name} restarted on port {item.Port}." : $"Failed to restart {item.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error restarting {item.Name}: {ex.Message}";
        }
        finally
        {
            item.IsBusy = false;
            await RefreshDatabaseEnginesAsync();
        }
    }

    [RelayCommand]
    public async Task SelectTargetEngineAsync(DatabaseEngineItem? item)
    {
        if (item == null) return;
        SelectedEngine = item.Name;
        UpdateTargetStatus();
        await LoadDatabasesAsync();
    }

    private void UpdateTargetStatus()
    {
        foreach (var engine in DatabaseEngines)
        {
            engine.IsCurrentTarget = engine.Name.Equals(SelectedEngine, StringComparison.OrdinalIgnoreCase);
        }
    }

    partial void OnSelectedDatabaseEngineChanged(DatabaseEngineItem? value)
    {
        if (value != null && !string.Equals(SelectedEngine, value.Name, StringComparison.OrdinalIgnoreCase))
        {
            SelectedEngine = value.Name;
        }
    }

    partial void OnSelectedEngineChanged(string value)
    {
        SyncEngineSelection();
        UpdateTargetStatus();
        if (SelectedDatabaseEngine?.Name != value)
        {
            SelectedDatabaseEngine = DatabaseEngines.FirstOrDefault(e => e.Name.Equals(value, StringComparison.OrdinalIgnoreCase));
        }
        OnPropertyChanged(nameof(ConnectionEngineAndHost));
        _ = LoadDatabasesAsync();
    }

    private void SyncEngineSelection()
    {
        ConnectionHost = _configService.Config.Database.Host ?? "127.0.0.1";

        if (SelectedEngine.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            ConnectionPort = _configService.Config.Services.PostgresPort;
            ConnectionUser = "postgres";
            SqlQuery = "SELECT tablename FROM pg_tables WHERE schemaname = 'public';";
        }
        else if (SelectedEngine.Equals("MySQL", StringComparison.OrdinalIgnoreCase))
        {
            ConnectionPort = _configService.Config.Services.MySqlPort;
            ConnectionUser = _configService.Config.Database.RootUser ?? "root";
            SqlQuery = "SHOW TABLES;";
        }
        else
        {
            ConnectionPort = _configService.Config.Services.MariaDbPort;
            ConnectionUser = _configService.Config.Database.RootUser ?? "root";
            SqlQuery = "SHOW TABLES;";
        }

        OnPropertyChanged(nameof(ConnectionEngineAndHost));
    }

    [RelayCommand]
    public async Task LoadDatabasesAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        StatusMessage = $"Checking {SelectedEngine} status on port {ConnectionPort}...";

        try
        {
            bool isSvcRunning;
            DatabaseServerStatus? probeResult = null;
            if (SelectedEngine.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
            {
                var svc = _serviceManager.GetService(ServiceType.PostgreSQL);
                isSvcRunning = svc?.IsRunning ?? false;
                if (!isSvcRunning)
                {
                    var portCheck = await _portService.CheckPortAsync(ConnectionPort);
                    isSvcRunning = portCheck.IsOccupied;
                }
            }
            else
            {
                var probe = await _databaseService.ProbeDatabaseServerAsync(SelectedEngine);
                probeResult = probe;
                isSvcRunning = probe.IsHealthy;
            }

            if (!isSvcRunning)
            {
                Databases.Clear();
                Tables.Clear();
                SelectedDatabase = null;
                if (probeResult != null && !string.IsNullOrWhiteSpace(probeResult.StatusText) && !probeResult.StatusText.Equals("Stopped", StringComparison.OrdinalIgnoreCase))
                {
                    ConnectionStatus = probeResult.StatusText;
                    StatusMessage = !string.IsNullOrWhiteSpace(probeResult.ErrorDetails)
                        ? probeResult.ErrorDetails
                        : $"{SelectedEngine} reported {probeResult.StatusText} on port {ConnectionPort}.";
                }
                else
                {
                    ConnectionStatus = "Service Stopped (Offline)";
                    StatusMessage = $"{SelectedEngine} service is stopped on port {ConnectionPort}. Start service from Dashboard or Services.";
                }
                return;
            }

            StatusMessage = $"Connecting to {SelectedEngine} on port {ConnectionPort}...";
            var list = await _databaseService.GetDatabasesAsync(SelectedEngine, ConnectionPort);
            Databases.Clear();
            foreach (var db in list)
            {
                Databases.Add(db);
            }

            if (SelectedDatabase == null && Databases.Any())
            {
                SelectedDatabase = Databases.FirstOrDefault(d => 
                    !d.Name.Equals("information_schema", StringComparison.OrdinalIgnoreCase) &&
                    !d.Name.Equals("performance_schema", StringComparison.OrdinalIgnoreCase) &&
                    !d.Name.Equals("mysql", StringComparison.OrdinalIgnoreCase)) 
                    ?? Databases.First();
            }

            ConnectionStatus = Databases.Any() ? "Online (Connected)" : "Online (0 Databases)";
            StatusMessage = $"Connected to {SelectedEngine} ({ConnectionHost}:{ConnectionPort}). Found {Databases.Count} database(s).";
        }
        catch (Exception ex)
        {
            _logger.LogError("Databases", $"Exception loading {SelectedEngine} databases: {ex.Message}", ex);
            ConnectionStatus = "Connection Error";
            StatusMessage = $"Database load notice: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedDatabaseChanged(DatabaseInfo? value)
    {
        if (value != null)
        {
            _ = LoadTablesAsync(value.Name);
        }
        else
        {
            Tables.Clear();
        }
    }

    private async Task LoadTablesAsync(string dbName)
    {
        try
        {
            var tbls = await _databaseService.GetTablesAsync(dbName, SelectedEngine, ConnectionPort);
            Tables.Clear();
            foreach (var t in tbls)
            {
                Tables.Add(t);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Databases", $"Error retrieving tables for `{dbName}`: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task ExecuteQueryAsync()
    {
        if (string.IsNullOrWhiteSpace(SqlQuery)) return;

        IsLoading = true;
        StatusMessage = "Executing query...";

        try
        {
            var targetDb = SelectedDatabase?.Name ?? (SelectedEngine == "PostgreSQL" ? "postgres" : "");
            var result = await _databaseService.ExecuteQueryAsync(targetDb, SqlQuery, SelectedEngine, ConnectionPort);

            if (result.IsSuccess)
            {
                var dt = new DataTable();
                foreach (var col in result.ColumnNames)
                {
                    dt.Columns.Add(col);
                }

                foreach (var rowDict in result.Rows)
                {
                    var dr = dt.NewRow();
                    foreach (var col in result.ColumnNames)
                    {
                        dr[col] = rowDict.TryGetValue(col, out var val) ? (val ?? DBNull.Value) : DBNull.Value;
                    }
                    dt.Rows.Add(dr);
                }

                QueryResultTable = dt;
                QueryExecutionStats = $"Query OK, {result.RowsAffected} row(s) affected ({result.ExecutionTimeMs:F1} ms)";
                StatusMessage = "Query executed successfully.";
            }
            else
            {
                QueryResultTable = null;
                QueryExecutionStats = $"Error: {result.ErrorMessage} ({result.ExecutionTimeMs:F1} ms)";
                StatusMessage = "Query failed.";
                _logger.LogWarning("Databases", $"Query failed on {SelectedEngine}: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            QueryExecutionStats = $"Exception: {ex.Message}";
            StatusMessage = "Query execution error.";
            _logger.LogError("Databases", $"Query execution exception on {SelectedEngine}", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task CreateDatabaseAsync()
    {
        if (string.IsNullOrWhiteSpace(NewDatabaseName))
        {
            StatusMessage = "Please enter a valid database name.";
            return;
        }

        var dbName = NewDatabaseName.Trim();
        StatusMessage = $"Creating database `{dbName}` on {SelectedEngine}...";
        try
        {
            bool ok = await _databaseService.CreateDatabaseAsync(dbName, SelectedEngine, ConnectionPort);
            if (ok)
            {
                StatusMessage = $"Database `{dbName}` created successfully on {SelectedEngine}.";
                NewDatabaseName = string.Empty;
                await LoadDatabasesAsync();
            }
            else
            {
                StatusMessage = $"Failed to create database `{dbName}`. Check service logs.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            _logger.LogError("Databases", $"Error creating database `{dbName}`", ex);
        }
    }

    [RelayCommand]
    public async Task DropDatabaseAsync(DatabaseInfo? db)
    {
        if (db == null)
        {
            StatusMessage = "Please select a database from the list first.";
            return;
        }

        var confirm = MessageBox.Show(
            $"Are you sure you want to permanently drop database '{db.Name}' from {SelectedEngine}?\n\nAll tables and records will be permanently deleted.",
            "Confirm Delete Database",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        StatusMessage = $"Dropping database `{db.Name}`...";
        try
        {
            bool ok = await _databaseService.DropDatabaseAsync(db.Name, SelectedEngine, ConnectionPort);
            if (ok)
            {
                StatusMessage = $"Database `{db.Name}` deleted from {SelectedEngine}.";
                await LoadDatabasesAsync();
            }
            else
            {
                StatusMessage = $"Failed to drop database `{db.Name}`.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            _logger.LogError("Databases", $"Error dropping database `{db.Name}`", ex);
        }
    }

    [RelayCommand]
    public async Task ExportDatabaseAsync(DatabaseInfo? db)
    {
        if (db == null)
        {
            StatusMessage = "Please select a database from the list first.";
            return;
        }

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var defaultFilename = $"backup_{db.Name}_{timestamp}.sql";
        var defaultFolder = _configService.Config.Paths.BackupsPath;

        var sfd = new SaveFileDialog
        {
            FileName = defaultFilename,
            InitialDirectory = Directory.Exists(defaultFolder) ? defaultFolder : Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Filter = "SQL Files (*.sql)|*.sql|All Files (*.*)|*.*"
        };

        if (sfd.ShowDialog() == true)
        {
            StatusMessage = $"Exporting `{db.Name}`...";
            bool ok = await _databaseService.ExportDatabaseAsync(db.Name, sfd.FileName, msg => StatusMessage = msg);
            if (ok)
            {
                StatusMessage = $"Export complete: {Path.GetFileName(sfd.FileName)}";
            }
        }
    }

    [RelayCommand]
    public async Task ImportDatabaseAsync(DatabaseInfo? db)
    {
        if (db == null)
        {
            StatusMessage = "Please select a database from the list first.";
            return;
        }

        var ofd = new OpenFileDialog
        {
            Filter = "SQL Files (*.sql)|*.sql|All Files (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            StatusMessage = $"Importing into `{db.Name}`...";
            bool ok = await _databaseService.ImportDatabaseAsync(db.Name, ofd.FileName, msg => StatusMessage = msg);
            if (ok)
            {
                StatusMessage = $"Import completed for `{db.Name}`.";
                await LoadTablesAsync(db.Name);
            }
        }
    }

    [RelayCommand]
    public async Task TestConnectionAsync()
    {
        IsLoading = true;
        StatusMessage = $"Testing connection to {SelectedEngine} ({ConnectionHost}:{ConnectionPort})...";
        ConnectionStatus = "Testing...";

        try
        {
            var sw = Stopwatch.StartNew();
            var portStatus = await _portService.CheckPortAsync(ConnectionPort);
            if (!portStatus.IsOccupied)
            {
                sw.Stop();
                ConnectionStatus = "Offline (Port not open)";
                StatusMessage = $"Connection failed: {SelectedEngine} is not listening on port {ConnectionPort}. Start service first.";
                return;
            }

            bool isPostgres = SelectedEngine.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase);
            var query = isPostgres ? "SELECT version();" : "SELECT VERSION();";
            var targetDb = isPostgres ? "postgres" : "";

            var result = await _databaseService.ExecuteQueryAsync(targetDb, query, SelectedEngine, ConnectionPort);
            sw.Stop();

            if (result.IsSuccess)
            {
                string version = "Available";
                if (result.Rows.Count > 0 && result.ColumnNames.Count > 0)
                {
                    var col = result.ColumnNames[0];
                    if (result.Rows[0].TryGetValue(col, out var val) && val != null)
                    {
                        version = val.ToString() ?? "Available";
                    }
                }
                ConnectionStatus = "Connected (Healthy)";
                StatusMessage = $"Connection successful to {SelectedEngine} on port {ConnectionPort} ({sw.ElapsedMilliseconds} ms). Version: {version}";
                _logger.LogInfo("Databases", $"Connection test to {SelectedEngine} on port {ConnectionPort} succeeded.");
            }
            else
            {
                ConnectionStatus = "Connection Failed";
                StatusMessage = $"Connection failed to {SelectedEngine}: {result.ErrorMessage}";
                _logger.LogWarning("Databases", $"Connection test to {SelectedEngine} failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            ConnectionStatus = "Connection Error";
            StatusMessage = $"Connection test error: {ex.Message}";
            _logger.LogError("Databases", $"Connection test exception on {SelectedEngine}", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    // --- phpMyAdmin access (Agent.md §5.C) ---

    [RelayCommand]
    public async Task OpenPhpMyAdminAsync()
    {
        if (SelectedEngine.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            PhpMyAdminStatus = "phpMyAdmin supports MySQL and MariaDB only. For PostgreSQL, use the SQL Query editor above.";
            StatusMessage = PhpMyAdminStatus;
            return;
        }

        IsPhpMyAdminLoading = true;
        PhpMyAdminStatus = "Checking prerequisites...";
        try
        {
            var resolution = await _databaseService.ResolveCurrentDatabaseServerAsync();
            if (string.IsNullOrEmpty(resolution.ActiveServerName))
            {
                var targetDb = SelectedEngine.Equals("MariaDB", StringComparison.OrdinalIgnoreCase)
                    ? ServiceType.MariaDB
                    : ServiceType.MySQL;

                bool shouldStartDb = false;
                if (System.Windows.Application.Current?.MainWindow != null && System.Windows.Application.Current.MainWindow.IsVisible)
                {
                    var confirm = System.Windows.MessageBox.Show(
                        $"{targetDb} is currently stopped. phpMyAdmin requires MySQL or MariaDB to be running.\n\nWould you like to start {targetDb} now?",
                        $"Start {targetDb} for phpMyAdmin",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);
                    shouldStartDb = confirm == System.Windows.MessageBoxResult.Yes;
                }

                if (shouldStartDb)
                {
                    PhpMyAdminStatus = $"Starting {targetDb} for phpMyAdmin...";
                    var startedDb = await _serviceManager.StartServiceAsync(targetDb);
                    if (!startedDb)
                    {
                        PhpMyAdminStatus = $"Could not start {targetDb}. Check service logs.";
                        StatusMessage = PhpMyAdminStatus;
                        return;
                    }
                    await Task.Delay(500);
                    resolution = await _databaseService.ResolveCurrentDatabaseServerAsync();
                }
                else
                {
                    PhpMyAdminStatus = "Database server is stopped. Please start MySQL or MariaDB to use phpMyAdmin.";
                    StatusMessage = PhpMyAdminStatus;
                    return;
                }
            }

            await _serviceManager.EnsurePhpCgiRunningAsync();

            // Step 3: Determine and ensure active/configured web server (Apache or Nginx)
            var apSvc = _serviceManager.GetService(ServiceType.Apache);
            var ngSvc = _serviceManager.GetService(ServiceType.Nginx);
            bool isApRunning = apSvc != null && apSvc.IsRunning;
            bool isNgRunning = ngSvc != null && ngSvc.IsRunning;

            var preferredWeb = _configService.Config.Services?.PreferredWebServer;
            bool preferApache = string.Equals(preferredWeb, "Apache", StringComparison.OrdinalIgnoreCase);

            ServiceType targetWebType;
            if (isApRunning && !isNgRunning)
            {
                targetWebType = ServiceType.Apache;
            }
            else if (isNgRunning && !isApRunning)
            {
                targetWebType = ServiceType.Nginx;
            }
            else if (isApRunning && isNgRunning)
            {
                targetWebType = preferApache ? ServiceType.Apache : ServiceType.Nginx;
            }
            else
            {
                targetWebType = preferApache ? ServiceType.Apache : ServiceType.Nginx;
            }

            var webService = _serviceManager.GetService(targetWebType);
            var serverDisplayName = targetWebType == ServiceType.Nginx ? "Nginx" : "Apache";

            if (webService == null || !webService.IsRunning)
            {
                bool shouldStart = false;
                if (System.Windows.Application.Current?.MainWindow != null && System.Windows.Application.Current.MainWindow.IsVisible)
                {
                    var confirm = System.Windows.MessageBox.Show(
                        $"{serverDisplayName} web server is stopped. phpMyAdmin requires a web server to be running.\n\nWould you like to start {serverDisplayName} now?",
                        $"Start {serverDisplayName} for phpMyAdmin",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);
                    shouldStart = confirm == System.Windows.MessageBoxResult.Yes;
                }

                if (shouldStart)
                {
                    PhpMyAdminStatus = $"Starting {serverDisplayName} for phpMyAdmin...";
                    var startedWeb = await _serviceManager.StartServiceAsync(targetWebType);
                    if (!startedWeb)
                    {
                        var lastErr = webService?.LastError ?? "Port conflict or startup failure";
                        PhpMyAdminStatus = $"Could not start {serverDisplayName}: {lastErr}.";
                        StatusMessage = PhpMyAdminStatus;
                        return;
                    }
                    webService = _serviceManager.GetService(targetWebType);
                }
                else
                {
                    PhpMyAdminStatus = $"{serverDisplayName} is stopped. phpMyAdmin requires {serverDisplayName} web server to run.";
                    StatusMessage = PhpMyAdminStatus;
                    return;
                }
            }

            var port = webService?.Port ?? (targetWebType == ServiceType.Nginx
                ? (_configService.Config.Services?.NginxPort ?? 80)
                : (_configService.Config.Services?.ApachePort ?? 80));
            if (port <= 0) port = 80;

            var serverIndex = resolution.PhpMyAdminServerIndex;
            var url = port == 80
                ? $"http://localhost/phpmyadmin/index.php?route=/&server={serverIndex}"
                : $"http://localhost:{port}/phpmyadmin/index.php?route=/&server={serverIndex}";

            PhpMyAdminUrl = url;
            PhpMyAdminStatus = "Verifying phpMyAdmin availability...";

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            try
            {
                var resp = await http.GetAsync(url);
                if ((int)resp.StatusCode >= 400)
                {
                    IsPhpMyAdminAvailable = false;
                    PhpMyAdminStatus = $"phpMyAdmin returned HTTP {(int)resp.StatusCode}. Check server configuration.";
                    StatusMessage = PhpMyAdminStatus;
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Database", $"HTTP check notice on {url}: {ex.Message}");
                if (System.Windows.Application.Current?.MainWindow != null && System.Windows.Application.Current.MainWindow.IsVisible)
                {
                    IsPhpMyAdminAvailable = false;
                    PhpMyAdminStatus = $"phpMyAdmin unavailable: {ex.Message}";
                    StatusMessage = PhpMyAdminStatus;
                    return;
                }
            }

            IsPhpMyAdminAvailable = true;
            PhpMyAdminStatus = $"Available (Active: {resolution.ActiveServerName})";
            StatusMessage = $"Opening phpMyAdmin at {url}...";
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Database", $"Could not launch default browser: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            PhpMyAdminStatus = $"Error: {ex.Message}";
            StatusMessage = PhpMyAdminStatus;
            _logger.LogError("Database", "Failed to open phpMyAdmin", ex);
        }
        finally
        {
            IsPhpMyAdminLoading = false;
        }
    }

    [RelayCommand]
    public async Task CheckPhpMyAdminAsync()
    {
        IsPhpMyAdminLoading = true;
        PhpMyAdminStatus = "Checking phpMyAdmin configuration...";
        try
        {
            var cfg = _configService.Config;
            var preferredWeb = cfg.Services?.PreferredWebServer;
            bool isNginxPreferred = string.Equals(preferredWeb, "Nginx", StringComparison.OrdinalIgnoreCase);

            var apSvc = _serviceManager.GetService(ServiceType.Apache);
            var ngSvc = _serviceManager.GetService(ServiceType.Nginx);

            int port;
            if (apSvc != null && apSvc.IsRunning && (ngSvc == null || !ngSvc.IsRunning))
            {
                port = apSvc.Port > 0 ? apSvc.Port : (cfg.Services?.ApachePort ?? 80);
            }
            else if (ngSvc != null && ngSvc.IsRunning && (apSvc == null || !apSvc.IsRunning))
            {
                port = ngSvc.Port > 0 ? ngSvc.Port : (cfg.Services?.NginxPort ?? 80);
            }
            else
            {
                port = isNginxPreferred
                    ? (ngSvc?.Port > 0 ? ngSvc.Port : (cfg.Services?.NginxPort ?? 80))
                    : (apSvc?.Port > 0 ? apSvc.Port : (cfg.Services?.ApachePort ?? 80));
            }

            var baseUrl = port == 80 ? "http://localhost/phpmyadmin" : $"http://localhost:{port}/phpmyadmin";
            PhpMyAdminUrl = baseUrl;

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            try
            {
                var resp = await http.GetAsync(baseUrl + "/index.php");
                IsPhpMyAdminAvailable = (int)resp.StatusCode < 400;
                PhpMyAdminStatus = IsPhpMyAdminAvailable
                    ? $"Available at {baseUrl}"
                    : $"HTTP {(int)resp.StatusCode} — check server config";
            }
            catch
            {
                IsPhpMyAdminAvailable = false;
                PhpMyAdminStatus = "Web server not running or phpMyAdmin not configured.";
            }
        }
        catch (Exception ex)
        {
            PhpMyAdminStatus = $"Check error: {ex.Message}";
        }
        finally
        {
            IsPhpMyAdminLoading = false;
        }
    }
}

public partial class DatabaseEngineItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private ServiceType _serviceType;

    [ObservableProperty]
    private string _version = "Checking...";

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private ServiceState _state = ServiceState.Stopped;

    [ObservableProperty]
    private string _statusText = "Stopped";

    [ObservableProperty]
    private int _port;

    public string PortText => $":{Port}";

    public string DisplayName => $"{Name} ({StatusText})";

    [ObservableProperty]
    private string _executablePath = string.Empty;

    [ObservableProperty]
    private string _dataDirectory = string.Empty;

    [ObservableProperty]
    private bool _isCurrentTarget;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasPortConflict;

    [ObservableProperty]
    private string _conflictReason = string.Empty;

    public bool CanStart => !IsRunning && !IsBusy && IsInstalled;
    public bool CanStop => IsRunning && !IsBusy;
    public bool CanRestart => IsRunning && !IsBusy;

    public void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(PortText));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanRestart));
    }
}

