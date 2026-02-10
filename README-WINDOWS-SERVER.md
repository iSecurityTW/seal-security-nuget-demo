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

Windows Server 2022 uses **v0.3.238** — the last release with a Windows x64 binary. The latest CLI is **v0.3.296** (Linux/macOS only). There are **58 releases** between these versions, with **113 source files changed, 19 added, and 2 moved** (excluding integration test suites and test fixture data). Below is a comprehensive breakdown based on a full source-code diff.

---

### NuGet/.NET Functionality — No Impact

The NuGet remediation code is **virtually identical** between v0.3.238 and v0.3.296. Of the 22 files in the MSIL/NuGet module (`internal/ecosystem/msil/`), only 2 changed — both are trivial interface signature updates (`Prepare()` method receives an additional parameter marked as unused with `_`). The actual fix logic, parsing, downloading, and normalization code is **byte-for-byte identical**.

| NuGet Capability | v0.3.238 | v0.3.296 |
|-----------------|----------|----------|
| SDK-style `.csproj` support | ✅ | ✅ |
| Legacy `packages.config` support | ✅ | ✅ |
| `dotnet list package --format json` parsing | ✅ Identical | ✅ Identical |
| `dotnet add package --source` fix command | ✅ Identical | ✅ Identical |
| `.nupkg` download from `nuget.sealsecurity.io` | ✅ Identical | ✅ Identical |
| Package name normalization (case-insensitive) | ✅ Identical | ✅ Identical |
| NuGet global cache path resolution | ✅ Identical | ✅ Identical |
| Signature verification on sealed packages | ✅ | ✅ |

**Bottom line: NuGet remediation on Windows v0.3.238 is functionally equivalent to v0.3.296.**

---

### New Ecosystem: Ruby/Bundler

v0.3.296 adds **Ruby/Bundler** as a 10th supported ecosystem with full fix, rename, and silence support.

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

**Impact:** Only relevant if scanning Ruby/Bundler projects. Does **not** affect NuGet workflows.

---

### Signature Verification — Hardened in v0.3.296

v0.3.296 makes a notable security change: the ECDSA public key used to verify sealed packages is now **embedded directly in the binary** rather than fetched from the backend at runtime.

| Feature | v0.3.238 | v0.3.296 |
|---------|----------|----------|
| Public key source | Fetched from backend API (`GET /unauthenticated/v1/signature/public_key`) | **Hardcoded in binary** (Base64 ECDSA P-256 key) |
| Signature matching | Matched by filename only | **Matched by filename + libraryVersionId** (stricter) |
| `GetPublicKey()` API call | ✅ Used | ❌ Removed (no longer needed) |

**Impact:** v0.3.238 fetches the public key over the network each time, which means it depends on that API endpoint being available. If Seal ever rotates or deprecates this endpoint, v0.3.238 signature verification could break. However, `--skip-sign-checks` can bypass this. For the demo, this is not a concern.

---

### Remote Diagnostic Log Upload

v0.3.296 adds the ability to upload CLI diagnostic logs to Seal's S3-backed storage for remote troubleshooting via presigned POST URLs.

| Feature | v0.3.238 | v0.3.296 |
|---------|----------|----------|
| Local log file | ✅ Written to temp dir | ✅ Written to temp dir |
| Remote log upload to S3 | ❌ | ✅ (automatic, presigned POST) |
| `--no-remote-log` opt-out flag | N/A | ✅ |
| Dual log files (local + upload) | ❌ | ✅ |
| `GetLogUploadPresignedURL()` API | N/A | ✅ |

**Impact:** Seal support cannot pull diagnostic logs remotely from Windows deployments. Logs must be collected manually from `%APPDATA%\local\seal-security\`.

---

### Embedded AWS CA Certificate Bundle

v0.3.296 embeds an AWS CA certificate bundle (`aws_ca_bundle.pem`) into the binary and creates a custom TLS client that appends these certs to the system cert pool.

| Feature | v0.3.238 | v0.3.296 |
|---------|----------|----------|
| TLS with system CA store | ✅ `http.Client{}` (default) | ✅ Custom `http.Client` with merged pool |
| Embedded AWS CA fallback | ❌ | ✅ (handles outdated system certs) |
| Custom `createHttpClient()` function | ❌ | ✅ |

**Impact:** Windows Server 2022 ships with current root certificates and receives updates via Windows Update. This is **not a concern** as long as the server's certificate store is reasonably up to date. On air-gapped or unpatched servers, v0.3.238 could potentially fail TLS handshakes to `cli.sealsecurity.io` if system certs are stale.

---

### Checkmarx Integration — Major Overhaul

The Checkmarx client was **completely rewritten** in v0.3.296 to use OAuth2/OIDC token refresh instead of direct token auth.

| Feature | v0.3.238 | v0.3.296 |
|---------|----------|----------|
| Auth mechanism | Direct bearer token (`cfg.Token`) | **OAuth2 refresh token flow** (Keycloak OIDC) |
| Token refresh | ❌ Manual token management | ✅ Automatic access token refresh via Keycloak |
| Realm extraction | ❌ | ✅ Auto-extracted from JWT issuer claim |
| IAM URL derivation | ❌ | ✅ `ast.checkmarx.net` → `iam.checkmarx.net` |
| `jwt` dependency | ❌ | ✅ `github.com/golang-jwt/jwt/v5` |
| Token caching | ❌ | ✅ Cached per-process (`tokenExpiry` stored but not enforced for refresh) |
| Form-encoded requests | ❌ | ✅ New `sendFormRequest()` for token endpoint |

**Impact:** If using Checkmarx integration on Windows with v0.3.238, the old direct-token auth is used. If Checkmarx migrates to require OIDC-only auth, v0.3.238 would break. For NuGet-only workflows without Checkmarx integration, **no impact**.

---

### BlackDuck Integration — Improved CVE Resolution

v0.3.296 adds proper CVE extraction from BlackDuck's proprietary vulnerability identifiers and package manager mapping.

| Feature | v0.3.238 | v0.3.296 |
|---------|----------|----------|
| CVE extraction from BlackDuck IDs | ❌ Basic | ✅ `extractVulnIdentifier()` — parses `RelatedVulnerability` |
| Package manager name mapping | ❌ | ✅ `blackDuckToSealPackageManager()` (maps `npmjs` → `NPM`, `pypi` → `PyPI`, etc.) |
| Config key names | `blackduck-url`, `blackduck-token` | **Renamed to** `url`, `token` (breaking change) |

**Impact:** Only relevant if integrating with BlackDuck. Note: if you later upgrade to v0.3.296 config format, BlackDuck config keys need renaming.

---

### Snyk Integration — Multi-Project Support

v0.3.296 replaces single `project-id` with an array of `project-ids`, allowing the CLI to update multiple Snyk projects in one run.

| Feature | v0.3.238 | v0.3.296 |
|---------|----------|----------|
| Single Snyk project | ✅ `snyk.project-id` | ✅ (backward compat via `ProjectIds`) |
| Multiple Snyk projects | ❌ | ✅ `snyk.project-ids: [id1, id2, ...]` |
| Per-project error handling | N/A | ✅ Continues on individual project failures |

**Impact:** Only relevant if using Snyk. v0.3.238 is limited to one project per run.

---

### SentinelOne Integration — Minor Change

v0.3.296 removes the `tag` query parameter from SentinelOne policy queries.

| Feature | v0.3.238 | v0.3.296 |
|---------|----------|----------|
| Tag-based policy query filter | ✅ | ❌ Removed |

**Impact:** Only relevant if using SentinelOne. Behavioral change to how policies are queried.

---

### Go Module Improvements

The Go ecosystem fixer received significant changes in v0.3.296:

| Feature | v0.3.238 | v0.3.296 |
|---------|----------|----------|
| `go.mod` update after fix | ❌ (backed up for rollback only) | ✅ `updateVersionInModFile()` runs `go mod edit` with `require`/`dropreplace` |
| `vendor/modules.txt` update | ❌ | ✅ `updateVersionInModulesFile()` — regex-based version replacement |
| `renamePackage()` version reference | Uses `VulnerablePackage.Version` | Bug fix — uses `AvailableFix.Version` |
| `dont-change-go-mod` config option | N/A (go.mod never modified) | ✅ Skips go.mod/modules.txt updates while still sealing vendor code |

**Impact:** None for NuGet. Go Modules users on v0.3.238 will have a less complete fix (go.mod is never updated, modules.txt is not updated, and `renamePackage` may reference the wrong version).

---

### Maven Improvements

The Maven fixer was substantially reworked for cache management:

| Feature | v0.3.238 | v0.3.296 |
|---------|----------|----------|
| M2 cache preparation | Recursive link tree of entire cache | **Selective linking** — only links paths for app dependencies |
| `copy-entire-m2-cache` option | ❌ | ✅ Config flag to fall back to old behavior |
| Plugin dependency tracking | ❌ | ✅ `pluginDependencies` tracked separately |
| Selective linking tests | ❌ | ✅ New `selective_linking_test.go` |

**Impact:** None for NuGet. Maven users on v0.3.238 use the older full-cache symlink approach (slower but functional).

---

### NPM Improvements

The NPM fixer now generates unique rollback directory names using UUIDs to prevent collisions with scoped packages.

| Feature | v0.3.238 | v0.3.296 |
|---------|----------|----------|
| Rollback dir naming | Relative path suffix | **UUID-based** (`{uuid}_{sanitized_name}_{version}`) |
| Scoped package path handling | ❌ Could create nested dirs | ✅ Slashes replaced with underscores |

**Impact:** None for NuGet. NPM users on v0.3.238 may hit edge cases with scoped packages (`@org/pkg`) during rollback.

---

### Image Fix — Squash Support

v0.3.296 adds `--squash` flag for container image layer squashing during `seal image fix`.

| Feature | v0.3.238 | v0.3.296 |
|---------|----------|----------|
| `seal image fix` | ✅ | ✅ |
| `--squash` layer squash | ❌ | ✅ |
| Image metadata extraction | ❌ | ✅ `metadata_extractor.go` |

**Impact:** Only relevant for container image remediation.

---

### Silence Rule Ecosystem Filtering

v0.3.296 filters silence rules to match the current package manager's ecosystem. v0.3.238 applies all silence rules globally.

| Feature | v0.3.238 | v0.3.296 |
|---------|----------|----------|
| Per-ecosystem filtering | ❌ All rules applied globally | ✅ `filterSilenceRulesForEcosystem()` |
| Empty-part validation | ❌ | ✅ Rejects rules with empty manager or package parts |

**Impact:** Minor edge case — if silence rules are configured for multiple ecosystems on the same project, v0.3.238 may attempt to apply non-matching rules (which are harmlessly ignored).

---

### API Changes

| API Endpoint | v0.3.238 | v0.3.296 |
|-------------|----------|----------|
| `GET /unauthenticated/v1/signature/public_key` | ✅ Used | ❌ Removed (key embedded in binary) |
| `GET /authenticated/v1/logs/upload/generate-post-url` | N/A | ✅ New (log upload) |
| `GetClient()` accessor on `CliServer` | ❌ | ✅ New |
| Most API methods | Value receivers `(s CliServer)` | **Pointer receivers** `(s *CliServer)` |
| `SilenceRule.SealedVersion` field | ❌ | ✅ New optional field |

**Impact:** The API receiver change (value → pointer) is an internal optimization. The removed `GetPublicKey` endpoint is a potential future concern (see Signature Verification section above).

---

### Configuration Changes

v0.3.296 introduces new `.seal-config.yml` sections:

| Config Section | v0.3.238 | v0.3.296 | Purpose |
|----------------|----------|----------|---------|
| `java-files.skip-directory-changes` | ❌ | ✅ | Skip dir changes during Java files fix |
| `rpm.no-gpg-install` | ❌ | ✅ | Skip GPG key installation for RPM |
| `bundler.prod-only` | ❌ | ✅ | Only scan production Ruby deps (`prod-only` tag existed for other ecosystems; new for Bundler) |
| `golang.dont-change-go-mod` | ❌ | ✅ | Seal vendor without modifying go.mod |
| `maven.copy-entire-m2-cache` | ❌ | ✅ | Revert to recursive cache linking |
| `maven.skip-directory-changes` | ❌ | ✅ | Skip dir changes during Maven fix |
| `gradle.skip-directory-changes` | ❌ | ✅ | Skip dir changes during Gradle fix |
| `snyk.project-ids` (array) | ❌ | ✅ | Multi-project Snyk support |

**Impact:** None of these affect NuGet workflows. They are ecosystem-specific tuning options.

---

### Fix Orchestration Improvements

| Improvement | Details | Impact |
|-------------|---------|--------|
| Progress bar accuracy | Skipped callbacks no longer advance the step counter | Cosmetic |
| Progress bar feedback | Shows "completed" after each callback | Cosmetic |
| Callback logging | Includes step name in log messages | Better diagnostics |
| Shaded dep tracking | Shaded deps included in descriptors but skipped at fix time | Java-only (fix for reporting accuracy) |

---

### Summary: What Windows Server 2022 Users Are Missing

| Category | What's Missing | Severity | NuGet Impact |
|----------|---------------|----------|--------------|
| **NuGet/.NET** | — | **None** | **Fully equivalent** |
| **Ecosystem** | Ruby/Bundler support | Low | None |
| **Security** | Embedded public key (signature verification hardening) | Low | v0.3.238 fetches key from API (works today) |
| **Diagnostics** | Remote log upload to S3 | Low | Manual log collection still works |
| **TLS** | Embedded AWS CA bundle | Low | Not needed on patched Windows Server |
| **Checkmarx** | OAuth2/OIDC token refresh (major rewrite) | Medium* | None unless using Checkmarx |
| **BlackDuck** | CVE extraction, package manager mapping | Low* | None unless using BlackDuck |
| **Snyk** | Multi-project support | Low* | None unless using Snyk |
| **SentinelOne** | Tag filter removed from policy query | Low* | None unless using SentinelOne |
| **Go Modules** | go.mod + modules.txt updates, rename bug fix | Medium* | None — different ecosystem |
| **Maven** | Selective cache linking (performance) | Low* | None — different ecosystem |
| **NPM** | UUID rollback dirs for scoped packages | Low* | None — different ecosystem |
| **Containers** | Image `--squash` flag | Low | None |
| **Rules** | Silence rule ecosystem filtering | Very Low | Harmless in practice |
| **Config** | New tuning sections (7 ecosystems) | None | N/A for NuGet |

\* *Severity marked for the relevant ecosystem — not applicable to NuGet.*

> **The v0.3.238 CLI is fully capable for NuGet/.NET vulnerability remediation on Windows Server 2022.** The 58 releases between v0.3.238 and v0.3.296 contain 113 changed source files, but none of the changes affect NuGet fix logic. The most significant differences are: Ruby ecosystem support, the Checkmarx auth rewrite, Go Modules fix improvements, Maven cache optimization, embedded signature key/AWS CA bundle, and remote log upload. None of these impact the NuGet remediation workflow that will be used on this Windows server.

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
