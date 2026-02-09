# Seal Security — Windows Server 2022 Implementation Guide

## Target Environment

| Spec | Value |
|------|-------|
| **Edition** | Windows Server 2022 Standard |
| **Version** | 21H2 (OS Build 20348.4529) |
| **Architecture** | 64-bit, x64-based processor |
| **Processor** | Intel Xeon Gold 5215 @ 2.50GHz (4 cores) |
| **RAM** | 8 GB |
| **Device** | UMCT1259.umc.com |

---

## Prerequisites

### 1. Install .NET 9.0 SDK (x64)

Download and install from [https://dotnet.microsoft.com/download/dotnet/9.0](https://dotnet.microsoft.com/download/dotnet/9.0).

Choose: **Windows x64 SDK installer** (`.exe`)

```powershell
# Verify installation
dotnet --version
dotnet --list-sdks
```

### 2. Install Seal Security CLI (Windows x64)

Download the Windows x64 CLI from [https://app.sealsecurity.io](https://app.sealsecurity.io) (Onboarding → Download CLI).

```powershell
# Place seal.exe somewhere on your PATH, e.g.:
Move-Item seal.exe C:\Tools\seal.exe

# Add to PATH (run as Administrator, persists across sessions)
[Environment]::SetEnvironmentVariable("Path", $env:Path + ";C:\Tools", "Machine")

# Verify
seal version
```

### 3. Install Git (optional, for cloning)

Download from [https://git-scm.com/download/win](https://git-scm.com/download/win) or use `winget`:

```powershell
winget install --id Git.Git -e --source winget
```

---

## Step-by-Step Setup

### Step 1 — Clone the Repository

```powershell
cd C:\Projects
git clone https://github.com/seal-sec-demo-2/seal-security-nuget-demo.git
cd seal-security-nuget-demo
```

### Step 2 — Set Environment Variables

Set these for the current PowerShell session:

```powershell
$env:SEAL_TOKEN = "your-seal-token-here"
$env:SEAL_PROJECT = "nuget-demo"
```

To persist across sessions (run as Administrator):

```powershell
[Environment]::SetEnvironmentVariable("SEAL_TOKEN", "your-seal-token-here", "Machine")
[Environment]::SetEnvironmentVariable("SEAL_PROJECT", "nuget-demo", "Machine")
```

### Step 3 — Restore Dependencies (Vulnerable Versions)

```powershell
dotnet restore
```

This pulls packages from both nuget.org and the Seal NuGet feed (configured in `nuget.config`). The environment variables `SEAL_PROJECT` and `SEAL_TOKEN` authenticate against the Seal feed automatically.

### Step 4 — Build and Run the Vulnerable App

```powershell
dotnet build
dotnet run
```

The app starts on **http://localhost:5000**. Open a browser and verify the welcome page loads.

### Step 5 — Apply Seal Security Fixes

```powershell
seal fix . --mode remote -v
```

**Output should show:**
```
newtonsoft.json@12.0.2 replaced with newtonsoft.json@12.0.2-sp1 (remote config)
system.net.http@4.3.0 replaced with system.net.http@4.3.0-sp1 (remote config)
log4net@2.0.5 replaced with log4net@2.0.5-sp1 (remote config)

Fixed 3 packages
```

The CLI modifies `seal-security-nuget-demo.csproj` in-place, updating version references to the `-sp1` sealed variants.

### Step 6 — Restore and Build with Sealed Packages

```powershell
dotnet restore
dotnet build
```

### Step 7 — Run the Patched App

```powershell
dotnet run
```

The app is now running with patched dependencies. All existing functionality works identically — Seal's patches are binary-compatible drop-in replacements.

### Step 8 — Verify Sealed Versions

```powershell
dotnet list package
```

You should see `-sp1` suffixed versions:
```
Newtonsoft.Json     12.0.2-sp1
log4net             2.0.5-sp1
System.Net.Http     4.3.0-sp1
```

---

## Full Workflow (Copy & Paste)

```powershell
# Set credentials
$env:SEAL_TOKEN = "your-seal-token-here"
$env:SEAL_PROJECT = "nuget-demo"

# Clone and enter project
cd C:\Projects
git clone https://github.com/seal-sec-demo-2/seal-security-nuget-demo.git
cd seal-security-nuget-demo

# Restore vulnerable dependencies
dotnet restore

# Run Seal CLI to patch vulnerabilities
seal fix . --mode remote -v

# Restore sealed packages and build
dotnet restore
dotnet build

# Run the patched app
dotnet run
```

---

## Running as a Windows Service (Production)

To run the demo as a persistent Windows Service:

### 1. Publish the Application

```powershell
dotnet publish -c Release -r win-x64 -o C:\Apps\seal-nuget-demo
```

### 2. Create a Windows Service

```powershell
# Run as Administrator
New-Service -Name "SealNuGetDemo" `
  -BinaryPathName "C:\Apps\seal-nuget-demo\seal-security-nuget-demo.exe" `
  -DisplayName "Seal Security NuGet Demo" `
  -StartupType Automatic `
  -Description "Seal Security NuGet vulnerability remediation demo"

Start-Service -Name "SealNuGetDemo"
```

### 3. Configure Firewall (if needed)

```powershell
New-NetFirewallRule -DisplayName "Seal NuGet Demo" `
  -Direction Inbound `
  -Protocol TCP `
  -LocalPort 5000 `
  -Action Allow
```

---

## IIS Deployment (Alternative)

### 1. Install the ASP.NET Core Hosting Bundle

Download from [https://dotnet.microsoft.com/download/dotnet/9.0](https://dotnet.microsoft.com/download/dotnet/9.0) — choose **Hosting Bundle** (includes the ASP.NET Core runtime + IIS module).

### 2. Publish

```powershell
dotnet publish -c Release -r win-x64 -o C:\inetpub\seal-nuget-demo
```

### 3. Create IIS Site

```powershell
Import-Module WebAdministration

New-WebAppPool -Name "SealNuGetDemo"
Set-ItemProperty "IIS:\AppPools\SealNuGetDemo" -Name "managedRuntimeVersion" -Value ""

New-Website -Name "SealNuGetDemo" `
  -PhysicalPath "C:\inetpub\seal-nuget-demo" `
  -ApplicationPool "SealNuGetDemo" `
  -Port 5000
```

---

## Troubleshooting

### NuGet Restore Fails with 401/403

Ensure environment variables are set in the current session:
```powershell
echo $env:SEAL_TOKEN
echo $env:SEAL_PROJECT
```

If blank, re-set them. The `nuget.config` references `%SEAL_PROJECT%` and `%SEAL_TOKEN%` which NuGet resolves from environment variables.

### Seal CLI Not Found

Verify it's on PATH:
```powershell
Get-Command seal
# or
where.exe seal
```

If not found, add the directory containing `seal.exe` to your system PATH.

### Port 5000 Already in Use

```powershell
# Find what's using port 5000
netstat -ano | findstr :5000

# Kill the process (replace PID)
Stop-Process -Id <PID> -Force
```

### TLS/Certificate Errors

Windows Server 2022 ships with TLS 1.2 enabled by default. If you encounter certificate issues:
```powershell
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
```

### x64 Architecture Verification

```powershell
# Confirm x64
[Environment]::Is64BitOperatingSystem   # Should return True
[Environment]::Is64BitProcess           # Should return True
dotnet --info | Select-String "RID"     # Should show win-x64
```

---

## Seal CLI Reference (Windows)

| Command | Description |
|---------|-------------|
| `seal version` | Show CLI version |
| `seal fix . --mode remote -v` | Apply server-approved fixes (recommended) |
| `seal fix . --mode all -v` | Apply all available fixes |
| `seal scan .` | Scan without fixing |
| `seal fix . --mode remote --summarize` | Fix and write summary to log |

### Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `SEAL_TOKEN` | Yes | Seal Security JWT access token |
| `SEAL_PROJECT` | Yes | Project identifier (e.g., `nuget-demo`) |

---

## What Gets Patched

| Package | Vulnerable | Sealed | CVE | Severity |
|---------|-----------|--------|-----|----------|
| Newtonsoft.Json | 12.0.2 | 12.0.2-sp1 | CVE-2024-21907 | HIGH (7.5) |
| log4net | 2.0.5 | 2.0.5-sp1 | CVE-2018-1285 | CRITICAL (9.8) |
| System.Net.Http | 4.3.0 | 4.3.0-sp1 | CVE-2017-0249 | HIGH (7.3) |
