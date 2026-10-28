<div align="center">
  <img src="../Assets/Branding/logo.png" alt="LOKA" width="120" />
  <h1>LOKA</h1>
  <p><strong>High-Performance Local Web Development Environment for Windows</strong></p>

  <p>
    <a href="https://github.com/ariski254/LOKA/releases/latest"><img src="https://img.shields.io/github/v/release/ariski254/LOKA?style=flat-square&color=2ea44f&label=Release" alt="Latest Release" /></a>
    <a href="https://dotnet.microsoft.com/download/dotnet/8.0"><img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white" alt=".NET 8" /></a>
    <a href="https://www.microsoft.com/windows"><img src="https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20x64-0078D6?style=flat-square&logo=windows&logoColor=white" alt="Platform" /></a>
    <a href="../LICENSE"><img src="https://img.shields.io/badge/License-MIT-blue?style=flat-square" alt="License" /></a>
  </p>
</div>

---

## Overview

LOKA is an integrated local web development suite designed specifically for Microsoft Windows. It provides native execution performance and clean stack isolation without the overhead of heavy virtual machines or Docker containers.

LOKA manages web servers, isolated PHP runtime environments, relational databases, local DNS resolution, and SSL certificates through a modern desktop application and an integrated command-line tool.

---

## Key Features

- **Zero-Configuration Virtual Hosts:** Point LOKA to your projects folder. Every project folder is instantly accessible via `.test` local domains (e.g., `http://my-project.test` and `https://my-project.test`).
- **Automated Local SSL & HTTPS:** Built-in Local Root Certificate Authority (CA) automatically generates and trusts SSL certificates in Windows so browsers show secure HTTPS without warnings.
- **Isolated Multi-Version PHP:** Run projects on different PHP versions simultaneously. Switch between PHP 8.3, PHP 8.4, and PHP 8.5 per project with instant upstream routing.
- **Multi-Engine Databases:** Native process management for MySQL, MariaDB, and PostgreSQL.
- **Integrated phpMyAdmin:** Pre-configured phpMyAdmin dynamically connects to the active database engine with zero manual configuration.
- **Dual Management Interface:** Control your stack via a clean desktop GUI (WPF) or through PowerShell/CMD using the `loka` CLI.
- **Port Conflict Resolver:** Automatically detects and diagnoses background processes blocking standard ports (80, 443, 3306, 5432).

---

## System Requirements and .NET 8 Installation

> [!IMPORTANT]
> **.NET 8 INSTALLATION REQUIREMENT:**
> LOKA requires the **Microsoft .NET 8 Desktop Runtime** (or .NET 8 SDK) to run. If your computer does not have .NET 8 installed, please install it using one of the methods below before launching LOKA.

### How to Install .NET 8:

#### Option 1: Using Windows Package Manager (winget) — Recommended
Run the following command in PowerShell or Command Prompt:

```powershell
winget install Microsoft.DotNet.DesktopRuntime.8
```

*(Or install the full SDK if you plan to build or modify code: `winget install Microsoft.DotNet.SDK.8`)*

#### Option 2: Download from Microsoft Official Website
1. Visit the official Microsoft download page: [Download .NET 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Under **.NET Desktop Runtime 8.x**, download the **Windows x64** installer.
3. Run the installer and follow the on-screen instructions.

#### Verification
Verify the installation in terminal:
```powershell
dotnet --version
```
*Expected result: `8.0.xxx`*

### Other System Requirements:
- **Operating System:** Windows 10 (Version 1809 or higher) or Windows 11 (64-bit).
- **Permissions:** Administrator access is requested during setup to register local SSL certificates and synchronize Windows `hosts`.

---

## Download and Installation

Download the latest version from the [LOKA Releases Page](https://github.com/ariski254/LOKA/releases/latest):

| Package | Recommended For | Description |
|---|---|---|
| **`Loka-Setup-Online-x64.exe`** | **Most Users (Recommended)** | Lightweight (~1.8 MB) installer. Downloads and installs only the runtimes and components you choose. |
| **`Loka-Setup-Offline-x64.exe`** | Offline / Air-Gapped Machines | Full installer (~870 MB) with all runtimes (Nginx, Apache, PHP 8.3-8.5, MySQL, MariaDB, Postgres) pre-packaged. |


### Installation Steps:
1. Run `Loka-Setup-Online-x64.exe` (or the offline installer).
2. Choose your installation path and select the runtime packages you want to install.
3. Complete the setup wizard. LOKA will register the Local Root CA and start in the Windows System Tray.

---

## Quick Usage Guide

### 1. Starting Services
Open the LOKA Dashboard and click **Start All** to launch your configured web servers, PHP FastCGI workers, and database engines.

### 2. Adding Web Projects
- **Park Directory:** In the **Projects** view, add your main workspace folder (e.g. `C:\Users\Username\Projects`). Every subfolder immediately receives its own local domain:
  - Folder `C:\Users\Username\Projects\my-site` ➡️ `https://my-site.test`
- **Link Project:** Link individual folders located outside your parked directory with custom `.test` hostnames.

### 3. Selecting PHP Version per Project
Under the **Projects** tab, choose the desired PHP version (8.3, 8.4, or 8.5) from the dropdown next to each project. Upstream web server routing updates automatically without restarting the application.

### 4. Database Access & phpMyAdmin
Click **Open phpMyAdmin** from the Dashboard or Database view to launch database administration in your default browser.

---

## Command-Line Interface (`loka` CLI)

LOKA includes a command-line tool accessible from PowerShell, Windows Terminal, or Command Prompt:

| Command | Description |
|---|---|
| `loka status` | View operational status of all services and active PHP versions |
| `loka doctor` | Run full diagnostic checks (ports, hosts permissions, SSL trust) |
| `loka start` | Start all background services |
| `loka start <service>` | Start an individual service (`nginx`, `apache`, `mysql`, `mariadb`, `postgres`) |
| `loka stop` | Stop all active services |
| `loka restart` | Restart running services |
| `loka php list` | List installed PHP runtime versions |
| `loka php use <version>` | Set global fallback PHP version (e.g. `8.4`) |
| `loka projects` | List all registered parked directories and linked project domains |
| `loka sync-hosts` | Synchronize Windows `hosts` file with registered local domains |
| `loka trust` | Reinstall and trust the Local Root Certificate Authority |

---

## Database Connection Defaults

| Service | Port | Default User | Default Password |
|---|---|---|---|
| **MySQL** | `3306` | `root` | *(empty)* |
| **MariaDB** | `3307` | `root` | *(empty)* |
| **PostgreSQL** | `5432` | `postgres` | `postgres` or *(empty)* |

---

## License

LOKA is open-source software licensed under the [MIT License](../LICENSE).  
Copyright (c) 2026 LOKA Dev Team and Contributors.
