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

> **Note:** The latest Windows CLI release is **v0.3.238** (Sep 11, 2025). Windows binaries were discontinued after this version.
> Newer releases only ship Linux and macOS binaries. v0.3.238 is fully functional for NuGet remediation on Windows.

Download from GitHub Releases:
- **EXE:** [seal-windows-amd64-v0.3.238.exe](https://github.com/seal-community/cli/releases/download/v0.3.238/seal-windows-amd64-v0.3.238.exe) (24.5 MB)
- **ZIP:** [seal-windows-amd64-v0.3.238.zip](https://github.com/seal-community/cli/releases/download/v0.3.238/seal-windows-amd64-v0.3.238.zip)

Or download via PowerShell:

```powershell
# Download the CLI
$cliUrl = "https://github.com/seal-community/cli/releases/download/v0.3.238/seal-windows-amd64-v0.3.238.exe"
Invoke-WebRequest -Uri $cliUrl -OutFile C:\Tools\seal.exe

# Add to PATH (run as Administrator, persists across sessions)
[Environment]::SetEnvironmentVariable("Path", $env:Path + ";C:\Tools", "Machine")

# Reload PATH in current session
$env:Path = [Environment]::GetEnvironmentVariable("Path", "Machine")

# Verify
seal version
# Expected output: v0.3.238
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
# Download and install Seal CLI (v0.3.238 — latest Windows release)
$cliUrl = "https://github.com/seal-community/cli/releases/download/v0.3.238/seal-windows-amd64-v0.3.238.exe"
New-Item -ItemType Directory -Force -Path C:\Tools | Out-Null
Invoke-WebRequest -Uri $cliUrl -OutFile C:\Tools\seal.exe
$env:Path += ";C:\Tools"

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

## CLI Version Comparison: v0.3.238 (Windows) vs v0.3.296 (Latest)

Windows Server 2022 uses **v0.3.238** — the last release with a Windows x64 binary. The latest CLI is **v0.3.296** (Linux/macOS only). Below is a comprehensive breakdown of what differs between these versions and the practical impact for Windows deployments.

### NuGet/.NET Functionality — No Impact

The NuGet remediation code is **virtually identical** between v0.3.238 and v0.3.296. The only change is an internal interface signature (`Prepare()` method receives an additional parameter that is ignored by the NuGet fixer). All core NuGet behavior is unchanged:

| NuGet Capability | v0.3.238 | v0.3.296 |
|-----------------|----------|----------|
| SDK-style `.csproj` support | ✅ | ✅ |
| Legacy `packages.config` support | ✅ | ✅ |
| `dotnet list package --format json` parsing | ✅ Identical | ✅ Identical |
| `dotnet add package --source` fix command | ✅ Identical | ✅ Identical |
| `.nupkg` download from Seal artifact server | ✅ Identical | ✅ Identical |
| Package name normalization (case-insensitive) | ✅ Identical | ✅ Identical |
| NuGet global cache path resolution | ✅ Identical | ✅ Identical |
| Signature verification on sealed packages | ✅ | ✅ |

**Bottom line: NuGet remediation on Windows v0.3.238 is functionally equivalent to v0.3.296.**

### Missing Ecosystem: Ruby/Bundler

v0.3.296 adds **Ruby/Bundler** as a 10th supported ecosystem. This is not available in v0.3.238.

| Ecosystem | v0.3.238 | v0.3.296 |
|-----------|----------|----------|
| NPM (Node.js) | ✅ | ✅ |
| PyPI (Python) | ✅ | ✅ |
| NuGet (.NET) | ✅ | ✅ |
| Maven (Java) | ✅ | ✅ |
| Go Modules | ✅ | ✅ |
| Composer (PHP) | ✅ | ✅ |
| RPM (Linux) | ✅ | ✅ |
| DEB (Linux) | ✅ | ✅ |
| APK (Alpine) | ✅ | ✅ |
| **RubyGems (Ruby)** | ❌ | ✅ |

**Impact:** Only relevant if scanning Ruby projects on the Windows server. Does **not** affect NuGet workflows.

### Missing Feature: Remote Diagnostic Log Upload

v0.3.296 adds the ability to upload CLI diagnostic logs to Seal's S3-backed storage for remote troubleshooting.

| Feature | v0.3.238 | v0.3.296 |
|---------|----------|----------|
| Local log file (`/tmp/seal-cli-*.log`) | ✅ | ✅ |
| Remote log upload to S3 | ❌ | ✅ |
| `--no-remote-log` opt-out flag | N/A | ✅ |

**Impact:** Seal support cannot pull diagnostic logs remotely from Windows deployments. Logs must be collected manually from the local machine. Log files are written to `%APPDATA%\local\seal-security\` on Windows.

### Missing Feature: Image Layer Squashing

v0.3.296 adds a `--squash` flag to the `seal image` command for container image layer squashing.

| Feature | v0.3.238 | v0.3.296 |
|---------|----------|----------|
| `seal image fix` command | ✅ | ✅ |
| `--squash` layer squashing | ❌ | ✅ |

**Impact:** Only relevant for container image remediation workflows. Does **not** affect NuGet package remediation.

### Missing Feature: Silence Rule Ecosystem Filtering

v0.3.296 adds per-ecosystem filtering of silence rules in `--mode remote`. In v0.3.238, silence rules from the backend are applied globally without ecosystem filtering.

| Feature | v0.3.238 | v0.3.296 |
|---------|----------|----------|
| Silence rules (remote mode) | ✅ All rules applied | ✅ Filtered per ecosystem |
| Silence rule validation | Basic | Enhanced (empty-part checks) |

**Impact:** If silence rules are configured for multiple ecosystems on the same project, v0.3.238 may attempt to apply rules from other ecosystems. In practice, this is a minor edge case — silence rules that don't match installed packages are harmlessly ignored.

### Missing Feature: Embedded AWS CA Bundle

v0.3.296 embeds an AWS CA certificate bundle for resilient TLS connectivity to Seal's backend, even on systems with outdated root certificates.

| Feature | v0.3.238 | v0.3.296 |
|---------|----------|----------|
| TLS with system CA store | ✅ | ✅ |
| Embedded AWS CA fallback | ❌ | ✅ |

**Impact:** Windows Server 2022 ships with current root certificates and receives updates via Windows Update. This is **not a concern** as long as the server's certificate store is up to date.

### Configuration Changes (Not Backward-Compatible)

v0.3.296 introduces new config sections and renames some fields. These only matter if using `.seal-config.yml`.

| Config Section | v0.3.238 | v0.3.296 |
|----------------|----------|----------|
| `java-files` (skip directory changes) | ❌ | ✅ |
| `rpm` (no GPM install) | ❌ | ✅ |
| `bundler` (prod-only deps) | ❌ | ✅ |
| `golang` (don't change go.mod) | ❌ | ✅ |
| `maven.copy-entire-m2-cache` | ❌ | ✅ |
| `snyk.project-ids` (multi-project) | ❌ (single `project-id`) | ✅ (array of IDs) |
| BlackDuck config key names | `blackduck-url`, `blackduck-token` | Renamed to `url`, `token` |

**Impact:** None of these affect NuGet workflows. The BlackDuck key rename is only relevant if integrating with BlackDuck.

### Integration Callbacks — Identical

Both versions support the same set of third-party integrations:

| Integration | v0.3.238 | v0.3.296 |
|-------------|----------|----------|
| Snyk | ✅ | ✅ |
| Dependabot | ✅ | ✅ |
| Checkmarx | ✅ | ✅ |
| JFrog Xray | ✅ | ✅ |
| BlackDuck | ✅ | ✅ |
| SentinelOne | ✅ | ✅ |
| Ox Security | ✅ | ✅ |

### Fix Orchestration Improvements (Minor)

v0.3.296 has minor improvements to the fix workflow that are cosmetic or affect edge cases:

| Improvement | Impact |
|-------------|--------|
| Progress bar no longer advances for skipped callbacks | Cosmetic — more accurate progress display |
| Progress bar shows "completed" after each callback | Cosmetic — better feedback |
| Shaded dependencies included in descriptors but skipped at fix time | Java-only — does not affect NuGet |

### Summary: What Windows Server 2022 Users Are Missing

| Category | What's Missing | Severity | NuGet Impact |
|----------|---------------|----------|--------------|
| **Ecosystem** | Ruby/Bundler support | Low | None |
| **Diagnostics** | Remote log upload | Low | Manual log collection still works |
| **Containers** | Image `--squash` flag | Low | None |
| **Rules** | Silence rule ecosystem filtering | Very Low | Harmless in practice |
| **TLS** | Embedded AWS CA bundle | Very Low | Not needed on patched Windows Server |
| **Config** | New config sections (Java, RPM, Go, Bundler) | None | N/A for NuGet |
| **NuGet** | — | **None** | **Fully equivalent** |

> **The v0.3.238 CLI is fully capable for NuGet/.NET vulnerability remediation on Windows Server 2022.** All differences between v0.3.238 and v0.3.296 are in areas outside of NuGet — primarily Ruby ecosystem support, remote log upload, and minor UX improvements. There are no NuGet-specific bug fixes, features, or behavioral changes that Windows users are missing.

---

## Seal CLI Reference (Windows — v0.3.238)

> This is the latest CLI version with Windows x64 support. All releases from [GitHub](https://github.com/seal-community/cli/releases) at v0.3.239+ ship Linux/macOS only.
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
