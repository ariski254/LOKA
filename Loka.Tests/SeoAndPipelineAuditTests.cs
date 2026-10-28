using System.IO;
using System.Text.Json;
using Loka.Core.Enums;
using Loka.Core.Models;
using Loka.Infrastructure.Configuration;
using Loka.Infrastructure.Logging;
using Loka.Project;
using Xunit;

namespace Loka.Tests;

public class LocalizationAndPipelineAuditTests : IDisposable
{
    private readonly string _sandbox;
    private readonly LogService _logger;
    private readonly ConfigurationService _configService;
    public LocalizationAndPipelineAuditTests()
    {
        _sandbox = Path.Combine(Path.GetTempPath(), "loka_pipeline_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_sandbox);

        _logger = new LogService();
        _configService = new ConfigurationService(_logger);
        _configService.Config.Paths.RootPath = _sandbox;
        _configService.Config.Paths.ProjectsPath = Path.Combine(_sandbox, "projects");
        _configService.Config.Paths.ConfigPath = Path.Combine(_sandbox, "config");
        _configService.Config.Paths.LogsPath = Path.Combine(_sandbox, "logs");
        Directory.CreateDirectory(_configService.Config.Paths.ProjectsPath);
        Directory.CreateDirectory(_configService.Config.Paths.ConfigPath);
        Directory.CreateDirectory(_configService.Config.Paths.LogsPath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_sandbox))
            {
                Directory.Delete(_sandbox, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task LocalizationScaffolder_ScaffoldsIndonesianResources_ForLaravel()
    {
        var projPath = Path.Combine(_configService.Config.Paths.ProjectsPath, "laravel-app");
        Directory.CreateDirectory(projPath);

        await LocalizationScaffolder.ScaffoldLaravelIndonesianLocaleAsync(projPath);

        var langIdDir = Path.Combine(projPath, "lang", "id");
        Assert.True(Directory.Exists(langIdDir));
        Assert.True(File.Exists(Path.Combine(langIdDir, "validation.php")));
        Assert.True(File.Exists(Path.Combine(langIdDir, "auth.php")));
        Assert.True(File.Exists(Path.Combine(langIdDir, "pagination.php")));
        Assert.True(File.Exists(Path.Combine(langIdDir, "passwords.php")));

        var valContent = await File.ReadAllTextAsync(Path.Combine(langIdDir, "validation.php"));
        Assert.Contains("Kolom :attribute wajib diisi.", valContent);
    }

    [Fact]
    public async Task LocalizationScaffolder_ScaffoldsIndonesianResources_ForCodeIgniter()
    {
        var projPath = Path.Combine(_configService.Config.Paths.ProjectsPath, "ci4-app");
        Directory.CreateDirectory(projPath);

        await LocalizationScaffolder.ScaffoldCodeIgniterIndonesianLocaleAsync(projPath);

        var langIdDir = Path.Combine(projPath, "app", "Language", "id");
        Assert.True(Directory.Exists(langIdDir));
        Assert.True(File.Exists(Path.Combine(langIdDir, "Validation.php")));

        var valContent = await File.ReadAllTextAsync(Path.Combine(langIdDir, "Validation.php"));
        Assert.Contains("Kolom {field} wajib diisi.", valContent);
    }

    [Fact]
    public void ProjectPipeline_Has15Stages_AndCorrectInitialStates()
    {
        var stages = ProjectCreationPipelineResult.CreateInitialStages();
        Assert.Equal(15, stages.Count);
        Assert.Equal(1, stages[0].StageNumber);
        Assert.Equal(15, stages[14].StageNumber);
        Assert.All(stages, s => Assert.Equal(PipelineStageStatus.Pending, s.Status));
    }
}
