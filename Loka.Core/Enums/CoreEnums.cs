namespace Loka.Core.Enums;

public enum ServiceType
{
    Apache,
    Nginx,
    MySQL,
    MariaDB,
    PostgreSQL,
    PHP_FPM,
    Redis
}

public enum ServiceState
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Error,
    Unknown
}

public enum ProjectFramework
{
    PlainPhp,
    Laravel,
    Kyro,
    WordPress,
    Symfony,
    CodeIgniter,
    Yii2,
    CakePhp,
    Slim,
    NodeJs,
    NextJs,
    NuxtJs,
    Express,
    Vite,
    React,
    Vue,
    StaticHtml,
    Unknown
}

public enum LogSeverity
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}
