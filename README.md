# Browser + CLI Demo (NuGet/C#)

## Overview

This demo application is a simple ASP.NET Core welcome page that uses **Newtonsoft.Json 12.0.2** to parse user input as a config object. The app has a name field — type your name, click Go, and it displays **"Welcome, alice!"**. Under the hood it passes the input through Newtonsoft.Json's `JsonConvert.DeserializeObject<NestedConfig>()`. That's it — completely standard usage of a popular JSON library.

The problem is that Newtonsoft.Json 12.0.2 (and versions before 13.0.1) has **CVE-2024-21907** — a high severity Denial of Service vulnerability with a **CVSS score of 7.5 (HIGH)**. This demo shows how Seal Security patches the vulnerability in-place without requiring a major version upgrade.

---

## The Vulnerability: CVE-2024-21907

### What is the vulnerability?

Newtonsoft.Json's `JsonConvert.DeserializeObject<T>()` method can be exploited by crafting deeply nested JSON payloads. When deserializing into a typed object (POCO), the library's `JsonSerializerInternalReader` performs truly recursive calls (`CreateValueInternal → CreateObject → PopulateObject → SetPropertyValue → CreateValueInternal`) that cause **stack overflow**, leading to application crash (Denial of Service).

### How the exploit works

The app takes user input and parses it through Newtonsoft.Json. If the input is a URL, the app fetches the content first — a realistic pattern used by config loaders, API testers, and webhook receivers:

```csharp
public class NestedConfig
{
    [JsonProperty("n")]
    public NestedConfig? N { get; set; }
}

var config = JsonConvert.DeserializeObject<NestedConfig>(name);
```

**Normal input:** Type `alice` → displays "Welcome, alice!"

**Exploit:** Paste this URL into the name field and click **Go**:

```
https://raw.githubusercontent.com/seal-sec-demo-2/json-payload/main/payload.json
```

The app detects it's a URL, fetches the [json-payload](https://github.com/seal-sec-demo-2/json-payload) (deeply nested JSON `{"n":{"n":{...}}}`), and deserializes it through Newtonsoft.Json into the recursive `NestedConfig` class — triggering the stack overflow.

The `JsonSerializerInternalReader` recurses through `CreateValueInternal → CreateObject → PopulateObject → SetPropertyValue` for every nesting level. At ~5,000 levels deep, this exhausts the thread stack and the application crashes with a `StackOverflowException` — the process dies instantly (no graceful error handling possible).

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

### Test with normal input

Go to [http://localhost:5000](http://localhost:5000), type `alice` in the name field, and click **Go**.
You should see: **"Welcome, alice!"**

### Test with exploit payload

Paste the following URL into the name field and click **Go**:

```
https://raw.githubusercontent.com/seal-sec-demo-2/json-payload/main/payload.json
```

**Unpatched result:** The browser shows an error / connection reset — the app crashed with a `StackOverflowException` in `JsonSerializerInternalReader.CreateValueInternal`. The process is dead.

**Patched result (after Seal):** The page shows **"Blocked by Seal patch: MaxDepth of 64 has been exceeded."** — Newtonsoft.Json's patched recursion limit rejected the deep payload. The server continues running normally.

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

## Preparations

### One-time setup

* Clone this repository
* Get owner access to the `seal-sec-demo-2` GitHub organization
* Set up GitHub secrets:
  * `SEAL_TOKEN` — Your Seal Security API token
  * `NGROK_AUTHTOKEN` — ngrok auth token (for exposing the app publicly)

### Before each demo

* Make sure the `nuget-demo` project exists in your Seal Security dashboard (not archived)
* Clean up the local state: `git clean -fxd && git checkout main && git reset --hard origin/main`
* Have the following tabs ready in Chrome:
  * [Seal Dashboard](https://app.sealsecurity.io/) — show the project and available patches
  * [GitHub - csproj](https://github.com/seal-sec-demo-2/seal-security-nuget-demo/blob/main/seal-security-nuget-demo.csproj) — show the vulnerable dependencies
  * [NuGet.org - Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json) — show version 12.0.2 popularity
  * [CVE-2024-21907 (NVD)](https://www.cve.org/CVERecord?id=CVE-2024-21907) — shows 7.5 HIGH severity
  * [Newtonsoft.Json 13.0.1 release notes](https://github.com/JamesNK/Newtonsoft.Json/releases/tag/13.0.1) — why upgrading is hard
* GitHub Actions tabs:
  * [Seal Security Remediation](https://github.com/seal-sec-demo-2/seal-security-nuget-demo/actions/workflows/seal-security.yml) — runs patched app

---

## Demo Flow

### Part 1: Show the vulnerability (unpatched)

1. **Show the project** — Open GitHub csproj tab, briefly explain the dependencies (ASP.NET Core, Newtonsoft.Json, log4net, etc.)
2. **Run the unpatched app** — Trigger the "Seal Security Remediation" workflow with remote rules **cleared** (nothing gets patched)
3. **Show version popularity** — Switch to NuGet.org tab, show Newtonsoft.Json download stats
4. **Normal usage** — Go to [https://sealdemo-nuget.ngrok.dev](https://sealdemo-nuget.ngrok.dev), type `alice`, click Go → shows **"Welcome, alice!"**
5. **Explain the vulnerability** — Newtonsoft.Json 12.0.2 has CVE-2024-21907 (7.5 HIGH) — Denial of Service via stack overflow
6. **Demonstrate the exploit** — Paste the payload URL (below), click Go → **connection reset, app crashes**

### Exploit payload

```
https://raw.githubusercontent.com/seal-sec-demo-2/json-payload/main/payload.json
```

### Part 2: Fix with Seal (patched)

1. **Show the problem with upgrading** — Open Newtonsoft.Json 13.0.1 release notes tab, explain it's not a simple version bump
2. **Show Seal's solution** — Open Seal Dashboard, show `Newtonsoft.Json 12.0.2-sp1` is available
3. **Approve remediation** — In the Seal UI, approve Newtonsoft.Json 12.0.2 → 12.0.2-sp1
4. **Run the patched app** — Re-trigger the "Seal Security Remediation" workflow (same settings)
5. **Test the exploit again** — Paste the same payload URL → **"Blocked by Seal patch: MaxDepth of 64 has been exceeded."** (server keeps running)

### What to expect

| Scenario | Result |
|----------|--------|
| **Unpatched** (Newtonsoft.Json 12.0.2) | Browser shows connection reset / error — app crashed with `StackOverflowException` (process dead) |
| **Seal-patched** (Newtonsoft.Json 12.0.2-sp1) | Page shows **"Blocked by Seal patch: MaxDepth of 64 has been exceeded."** — server keeps running |

### Key talking points

* **No code changes** — The application code is identical. Only the NuGet package was swapped.
* **Same API** — `12.0.2-sp1` is a drop-in replacement for `12.0.2`
* **Defense in depth** — Protects all code paths, including transitive dependencies
* **Public patches** — All patches are open source and auditable

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
