<div align="center">
  <img src="../Assets/Branding/logo.png" alt="LOKA" width="120" />
  <h1>LOKA</h1>
  <p><strong>High-Performance Local Web Development Environment for Windows</strong></p>

  <p>
    <a href="https://dotnet.microsoft.com/download/dotnet/8.0"><img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white" alt=".NET 8" /></a>
    <a href="https://www.microsoft.com/windows"><img src="https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20x64-0078D6?style=flat-square&logo=windows&logoColor=white" alt="Platform" /></a>
    <a href="../LICENSE"><img src="https://img.shields.io/badge/License-MIT-2ea44f?style=flat-square" alt="License" /></a>
    <a href="https://github.com/ariski254/LOKA"><img src="https://img.shields.io/badge/Repository-ariski254%2FLOKA-1f425f?style=flat-square&logo=github&logoColor=white" alt="GitHub Repository" /></a>
  </p>
</div>

---

## Overview

LOKA is an all-in-one local web development suite designed specifically for Microsoft Windows. It delivers native performance and complete stack isolation without requiring heavy virtualization or Docker containers.

LOKA coordinates web servers, isolated PHP runtime environments, relational databases, local DNS resolution, and SSL certificates through both a modern desktop application and an integrated command-line tool.

---

## Key Features

### Zero-Configuration Virtual Hosts
Every folder placed inside a designated parked directory automatically receives its own local `.test` domain without manual edits to Nginx configuration files or the Windows `hosts` file.

### Local SSL and HTTPS Automation
LOKA initializes and installs a dedicated Local Root Certificate Authority (CA) into the Windows Certificate Store. Modern web browsers automatically recognize and trust local HTTPS connections without security warnings.

### Per-Project PHP Version Isolation
Run multiple web applications requiring different PHP versions simultaneously. Switch between PHP 8.3, PHP 8.4, and PHP 8.5 on a per-project basis with instant upstream routing.

### Multi-Engine Database Support
Integrated process management for MySQL, MariaDB, and PostgreSQL. A pre-configured phpMyAdmin instance dynamically attaches to the active database engine with zero manual setup.

### Dual Interface (Desktop GUI and CLI)
Manage your development stack via a modern Windows Presentation Foundation (WPF) dashboard or directly from PowerShell and Command Prompt using the `loka` command-line utility.

### Integrated Diagnostic and Terminal Tools
- **Embedded Console:** Terminal session preloaded with PATH variables for active PHP, Composer, Node.js, and Git executables.
- **Port Conflict Inspector:** Scans and reports conflicting processes occupying standard ports (80, 443, 3306, 5432).
- **Real-Time Logs Viewer:** Live streaming of web server error logs and PHP diagnostic logs.

---

## Prerequisites and .NET 8 Installation

> [!IMPORTANT]
> **.NET 8 REQUIREMENT:**
> To compile, run, or build this project from source, **.NET 8 SDK** (or .NET 8 Desktop Runtime) must be installed on your system. If .NET 8 is not installed yet, please install it before running the project.

### 1. Install .NET 8 SDK

#### Option A: Windows Package Manager (winget) — Recommended
Run the following command in PowerShell or Command Prompt:

```powershell
winget install Microsoft.DotNet.SDK.8
```

#### Option B: Official Microsoft Installer
1. Visit the official Microsoft download portal: [Download .NET 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Under **.NET SDK**, download the **Windows x64** installer.
3. Run the downloaded installer and complete the setup wizard.

#### Verification
Verify that .NET 8 is active in your terminal:

```powershell
dotnet --version
```
*Expected output: `8.0.xxx`*

### 2. Operating System Requirements
- Windows 10 (Version 1809 or higher) or Windows 11 (64-bit).
- Administrative privileges (required during initial local CA installation and Windows `hosts` file synchronization).

---

## Quickstart Guide

### 1. Clone the Repository
```powershell
git clone https://github.com/ariski254/LOKA.git
cd LOKA
```

### 2. Restore Dependencies
```powershell
dotnet restore
```

### 3. Build the Solution
```powershell
dotnet build --configuration Release
```

### 4. Run the Application
Launch the Desktop GUI:
```powershell
dotnet run --project Loka.App
```

Or run the CLI interface:
```powershell
dotnet run --project Loka.CLI -- status
```

---

## Building the Installer Suite

LOKA provides an automated PowerShell build script that compiles all components and generates self-contained Windows installer executables:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-installer.ps1 -Configuration Release
```

The script outputs compiled binaries to the `release/` folder:
- `Loka.exe`: Standalone single-file desktop application.
- `Loka-Setup-Online-x64.exe`: Lightweight online bootstrapper installer.
- `Loka-Setup-Offline-x64.exe`: Full offline installer package.

---

## Command-Line Interface (`loka` CLI)

The CLI executable provides full stack control from any Windows terminal:

| Command | Description |
|---|---|
| `loka status` | Displays the operational status of all services and runtimes |
| `loka doctor` | Performs a comprehensive health and configuration diagnostic |
| `loka start` | Starts all configured background services |
| `loka start <service>` | Starts a specific service (`nginx`, `apache`, `mysql`, `mariadb`, `postgres`) |
| `loka stop` | Stops all active services cleanly |
| `loka restart` | Restarts running services |
| `loka php list` | Lists all installed PHP runtime versions |
| `loka php use <version>` | Sets the global fallback PHP version (e.g., `8.4`) |
| `loka projects` | Displays all registered parked directories and linked sites |
| `loka sync-hosts` | Synchronizes local project hostnames with `%SystemRoot%\System32\drivers\etc\hosts` |
| `loka trust` | Reinstalls and trusts the Local Root Certificate Authority |

---

## Database Connection Defaults

| Service | Port | Username | Password |
|---|---|---|---|
| **MySQL** | `3306` | `root` | *(empty)* |
| **MariaDB** | `3307` | `root` | *(empty)* |
| **PostgreSQL** | `5432` | `postgres` | `postgres` or *(empty)* |

---

## Repository and Deployment Guidelines

- **Binary Releases:** Compiled installer executables (such as `Loka-Setup-Offline-x64.exe`) exceed GitHub's 100 MB file size limit and are distributed exclusively through GitHub Releases.
- **Git Ignore Rules:** Build directories (`bin/`, `obj/`, `dist/`), cache folders (`artifacts/cache/`), and test environments (`pgsql/`, `laravel/`) are managed by [`.gitignore`](../.gitignore).

---

## License

This project is open-source software licensed under the [MIT License](../LICENSE).  
Copyright (c) 2026 LOKA Dev Team and Contributors.
