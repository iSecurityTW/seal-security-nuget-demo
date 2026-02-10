# Browser + CLI Demo (NuGet/C#)

## Overview

This demo application is a simple ASP.NET Core welcome page that uses **Newtonsoft.Json 12.0.2** to parse user input. The app has a name field — type your name, click Go, and it displays **"Welcome, alice!"**. Under the hood it passes the input through Newtonsoft.Json's `JsonConvert.DeserializeObject()`. That's it — completely standard usage of a popular JSON library.

The problem is that Newtonsoft.Json 12.0.2 (and versions before 13.0.1) has **CVE-2024-21907** — a high severity Denial of Service vulnerability with a **CVSS score of 7.5 (HIGH)**. This demo shows how Seal Security patches the vulnerability in-place without requiring a major version upgrade.

---

## The Vulnerability: CVE-2024-21907

### What is the vulnerability?

Newtonsoft.Json's `JsonConvert.DeserializeObject()` method can be exploited by crafting deeply nested JSON payloads. When parsing these payloads, the library performs recursive descent parsing that can cause **stack overflow**, leading to application crash (Denial of Service).

### How the exploit works

The app takes user input and parses it directly through Newtonsoft.Json (same pattern as the Maven demo's `yaml.load(name)`):

```csharp
var parsed = JsonConvert.DeserializeObject<JToken>(name);
```

**Normal input:** Type `alice` → displays "Welcome, alice!"

**Exploit — send deeply nested JSON (thousands of levels):**
```
{"n":{"n":{"n":{"n":{"n":{"n":...5000+ levels...}}}}}}
```

The exploit payload is hosted in the companion **[json-payload](https://github.com/seal-sec-demo-2/json-payload)** repo (mirroring the Maven demo's [yaml-payload](https://github.com/seal-sec-demo-2/yaml-payload) pattern). The workflow auto-fetches it. To test manually:

```bash
# Download the payload
curl -sO https://raw.githubusercontent.com/seal-sec-demo-2/json-payload/main/payload.json

# Send it to the app
curl -s "http://localhost:5000/?name=$(python3 -c 'import urllib.parse; print(urllib.parse.quote(open("payload.json").read()))')"
```

The required depth depends on the platform's thread stack size (~2000–5000 on Windows x64, ~15000+ on macOS ARM64). When the recursion depth exceeds the stack, the application crashes with a `StackOverflowException` — the process dies instantly (no graceful error handling possible).

### Real-world impact

With this vulnerability, attackers can:
- **Crash the application** by sending malicious JSON payloads
- **Cause Denial of Service** affecting all users
- **Exhaust server resources** through repeated exploitation
- **Bypass rate limiting** since each request crashes the process

### Why not just upgrade to Newtonsoft.Json 13.0.1?

The publicly available fix requires upgrading to version 13.0.1. However, upgrading major versions often introduces:

- **Breaking API changes** in serialization behavior
- **Compatibility issues** with other libraries expecting specific versions
- **Extensive testing requirements** for all serialization/deserialization code paths
- **Risk of runtime behavior changes** in production

This makes the "just upgrade" fix a project that can easily take **weeks of developer time** — leaving the vulnerability open in the meantime.

### How Seal Security fixes it

Seal's patched version (`12.0.2-sp1`) adds **recursion depth protection** without changing any public API. The patch:

1. Adds default `MaxDepth` limits to prevent unbounded recursion
2. Gracefully handles deep nesting with proper exceptions instead of stack overflow
3. **Does not change any public API** — existing code continues to work without modification

This is the same mitigation strategy applied in Newtonsoft.Json 13.0.1, backported to 12.0.2 as a drop-in replacement.

---

## Other Vulnerable Dependencies

This demo also includes other vulnerable NuGet packages that Seal Security can patch:

### log4net 2.0.5 - CVE-2018-1285 (CVSS 9.8 CRITICAL)

**XML External Entity (XXE) vulnerability** in log4net's XML configuration parsing. An attacker who can control the log4net configuration file can:
- Read arbitrary files from the server
- Perform Server-Side Request Forgery (SSRF)
- Cause Denial of Service

### System.Net.Http 4.3.0 - CVE-2017-0249 (CVSS 7.3 HIGH)

**Security bypass vulnerabilities** in System.Net.Http that can lead to:
- Information disclosure
- Elevation of privilege
- Security feature bypass

---

## Prerequisites

- .NET 9.0 SDK (or 8.0+)
- Seal Security CLI ([download from onboarding](https://app.sealsecurity.io))
- Seal Security Token

---

## Quick Start

### 1. Set Environment Variables

```bash
export SEAL_TOKEN="your-seal-token-here"
export SEAL_PROJECT="nuget-demo"
```

### 2. Run the Vulnerable App (Before Seal)

```bash
cd seal-security-nuget-demo

# Restore from standard NuGet only
dotnet restore --source https://api.nuget.org/v3/index.json

# Build and run
dotnet build
dotnet run
```

Open [http://localhost:5000](http://localhost:5000) — the app is running with vulnerable dependencies.

### 3. Apply Seal Security Fix

```bash
# Restore dependencies first
dotnet restore

# Run Seal CLI to fix vulnerabilities
seal fix --mode all -vvvv seal-security-nuget-demo.csproj

# Restore again to pull sealed versions
dotnet restore

# Build and run the patched app
dotnet build
dotnet run
```

The app now uses patched versions (`12.0.2-sp1`, `2.0.5-sp1`, `4.3.0-sp1`).

### 4. Verify Patched Versions

```bash
dotnet list package
```

You should see packages with `-sp1` suffix indicating Seal Security patches.

---

## Seal Security CLI Integration

### The Golden Rule

The CLI step must be added **immediately after dependencies are installed** but **before the final build**.

```bash
# 1. Restore dependencies
dotnet restore

# 2. <--- Run Seal CLI Here
export SEAL_TOKEN=<your-token>
export SEAL_PROJECT="nuget-demo"
seal fix --mode all -vvvv seal-security-nuget-demo.csproj

# 3. Restore again (to get sealed versions)  
dotnet restore

# 4. Build
dotnet build
```

### Fix Modes

| Mode | Description |
|------|-------------|
| `all` | Apply all available fixes automatically |
| `remote` | Only apply fixes approved in the Seal UI |
| `local` | Only apply fixes defined in `.seal-actions.yml` |

---

## Configuring the Artifact Server

### nuget.config Setup

The `nuget.config` file is pre-configured to use Seal Security with environment variables:

```xml
<configuration>
  <packageSources>
    <add key="Seal" value="https://nuget.sealsecurity.io/v3/index.json" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <Seal>
      <add key="Username" value="%SEAL_PROJECT%" />
      <add key="ClearTextPassword" value="%SEAL_TOKEN%" />
    </Seal>
  </packageSourceCredentials>
</configuration>
```

### Required Environment Variables

| Variable | Description |
|----------|-------------|
| `SEAL_TOKEN` | Your Seal Security access token |
| `SEAL_PROJECT` | Project ID (e.g., `nuget-demo`) |

### Alternative: CLI Configuration

Add source with credentials stored in user-level config:

```bash
dotnet nuget add source https://nuget.sealsecurity.io/v3/index.json \
  --name Seal \
  --username $SEAL_PROJECT \
  --password $SEAL_TOKEN \
  --store-password-in-clear-text
```

---

## Demo Walkthrough

### GitHub Actions Demo Flow

This mirrors the Maven demo approach — two workflow runs to show before/after.

Unlike the Maven demo (where the exploit payload is a short YAML string you paste into the browser), CVE-2024-21907 requires a **deeply nested JSON payload** — thousands of levels deep — to overflow the stack. The payload is hosted in the companion **[json-payload](https://github.com/seal-sec-demo-2/json-payload)** repo (same pattern as the Maven demo's [yaml-payload](https://github.com/seal-sec-demo-2/yaml-payload) repo). The workflow **automatically fetches and sends the payload** after starting the app, so the crash (or safe handling) is visible right in the GitHub Actions logs.

#### Run 1: Without Seal (Vulnerable)

1. Make sure the remote rules in the Seal UI are **cleared** (no fixes selected)
2. Trigger the workflow: **Actions → Seal Security Remediation → Run workflow** (default mode: `remote`)
3. The workflow runs `seal fix --mode remote` — with no rules set, nothing gets patched
4. The app starts at https://sealdemo-nuget.ngrok.dev
5. The workflow automatically fetches the [5000-depth nested JSON payload](https://github.com/seal-sec-demo-2/json-payload) and sends it to the app
6. **In the logs you'll see:**
   ```
   RESULT: Server CRASHED - CVE-2024-21907 exploited!
   App is DOWN - process was killed by StackOverflowException - VULNERABLE
   ```
7. The browser at https://sealdemo-nuget.ngrok.dev shows an error page (process is dead)

#### Run 2: With Seal (Patched)

1. Go to the Seal UI and **approve remediation** for Newtonsoft.Json 12.0.2 → 12.0.2-sp1
2. Re-trigger the workflow (same settings)
3. This time `seal fix --mode remote` patches Newtonsoft.Json to 12.0.2-sp1
4. The app starts at https://sealdemo-nuget.ngrok.dev
5. The workflow sends the same [5000-depth payload](https://github.com/seal-sec-demo-2/json-payload)
6. **In the logs you'll see:**
   ```
   RESULT: App handled the payload safely (HTTP 200)
   Seal Security patch is ACTIVE - CVE-2024-21907 is mitigated!
   App is still running (HTTP 200) - PATCHED
   ```
7. The browser at https://sealdemo-nuget.ngrok.dev still works — app stays up for 20 minutes

### Test with Normal Input

1. Go to https://sealdemo-nuget.ngrok.dev (or http://localhost:5000 locally)
2. Type `alice` in the name field
3. Click **Go**
4. You should see: **"Welcome, alice!"**

---

## Getting Your Seal Token

1. Sign up at [sealsecurity.io](https://sealsecurity.io)
2. Complete the onboarding flow
3. Click **Generate token** to create your artifact server token
4. Copy and save the token securely
5. Download the Seal CLI for your platform

---

## GitHub Actions (CI/CD)

This repo also includes GitHub Actions workflows for running the demo in CI/CD:

- **build_and_run.yml** — Builds and runs the vulnerable demo app (triggered on PR or manually)
- **seal-security.yml** — Runs Seal Security CLI to remediate vulnerabilities, then builds and runs the patched app (manual trigger with fix mode selection)

### Required GitHub Secrets

Add these in your repo under **Settings → Secrets and variables → Actions**:

| Secret | Required | Description |
|--------|----------|-------------|
| `SEAL_TOKEN` | ✅ Yes | Seal Security access token (for CLI + NuGet feed auth) |
| `SNYK_TOKEN` | Optional | Snyk API token (for Snyk integration) |
| `NGROK_AUTHTOKEN` | Optional | ngrok auth token (to expose demo publicly) |

---

## Available Sealed NuGet Packages

| Package | Vulnerable Version | Sealed Version | CVE | CVSS |
|---------|-------------------|----------------|-----|------|
| Newtonsoft.Json | 12.0.2 | 12.0.2-sp1 | CVE-2024-21907 | 7.5 HIGH |
| log4net | 2.0.5 | 2.0.5-sp1 | CVE-2018-1285 | 9.8 CRITICAL |
| log4net | 2.0.0 | 2.0.0-sp1 | CVE-2018-1285 | 9.8 CRITICAL |
| System.Net.Http | 4.3.0 | 4.3.0-sp1 | CVE-2017-0249 | 7.3 HIGH |
| Snappier | 1.1.0 | 1.1.0-sp1 | CVE-2023-28638 | 7.0 HIGH |
| jQuery.Validation | 1.17.0 | 1.17.0-sp1 | CVE-2021-21252 | 7.5 HIGH |

---

## License

MIT License - See LICENSE file for details.
