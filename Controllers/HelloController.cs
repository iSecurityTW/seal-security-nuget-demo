using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using log4net;
using System.Diagnostics;
using System.Reflection;

namespace SealSecurityNuGetDemo.Controllers;

/// <summary>
/// Demo page showing Seal Security patching Newtonsoft.Json CVE-2024-21907.
/// </summary>
[ApiController]
public class HelloController : ControllerBase
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(HelloController));

    [HttpGet("/")]
    [Produces("text/html")]
    public IActionResult Home()
    {
        return Content(GenerateDemoPage(), "text/html");
    }

    [HttpGet("/api/test-vuln")]
    public IActionResult TestVulnerability([FromQuery] int depth = 500)
    {
        // Cap depth to prevent abuse
        depth = Math.Clamp(depth, 10, 10000);

        // Build deeply nested JSON: {"nested":{"nested":{"nested": ... }}}
        var nested = new string('{') + string.Join("", Enumerable.Range(0, depth).Select(i => "\"n\":{")) + "\"v\":1" + new string('}', depth + 1);

        var sw = Stopwatch.StartNew();
        try
        {
            Logger.Info($"Testing CVE-2024-21907 with depth={depth}");
            var parsed = JsonConvert.DeserializeObject<JToken>(nested);
            sw.Stop();

            Logger.Info($"Parsed successfully in {sw.ElapsedMilliseconds}ms (Seal patch active)");

            return Ok(new
            {
                success = true,
                message = $"Deeply nested JSON (depth {depth}) parsed safely in {sw.ElapsedMilliseconds}ms",
                depth = depth,
                elapsedMs = sw.ElapsedMilliseconds,
                sealPatched = true
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            Logger.Error($"CVE-2024-21907 triggered: {ex.GetType().Name}");

            return StatusCode(500, new
            {
                success = false,
                message = $"VULNERABLE: {ex.GetType().Name} after {sw.ElapsedMilliseconds}ms",
                depth = depth,
                error = ex.GetType().Name,
                elapsedMs = sw.ElapsedMilliseconds,
                sealPatched = false
            });
        }
    }

    [HttpGet("/api/parse")]
    public IActionResult ParseJson([FromQuery] string json = "{\"name\": \"demo\"}")
    {
        try
        {
            Logger.Info($"Parsing JSON input: {json.Substring(0, Math.Min(100, json.Length))}...");
            var parsed = JsonConvert.DeserializeObject<JToken>(json);
            Logger.Info("JSON parsed successfully");
            return Ok(new { success = true, parsed = parsed });
        }
        catch (Exception ex)
        {
            Logger.Error($"Error parsing JSON: {ex.Message}");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpPost("/api/parse")]
    [Consumes("application/json")]
    public IActionResult ParseJsonPost([FromBody] object json)
    {
        try
        {
            var jsonString = JsonConvert.SerializeObject(json);
            Logger.Info($"Received JSON for parsing");
            var parsed = JsonConvert.DeserializeObject<JToken>(jsonString);
            return Ok(new { success = true, parsed = parsed });
        }
        catch (Exception ex)
        {
            Logger.Error($"Error parsing JSON: {ex.Message}");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    private string GetNewtonsoftVersion()
    {
        try
        {
            var asm = typeof(JsonConvert).Assembly;
            var ver = asm.GetName().Version;
            var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return infoVer ?? ver?.ToString() ?? "unknown";
        }
        catch { return "unknown"; }
    }

    private string GenerateDemoPage()
    {
        var newtonsoftVer = GetNewtonsoftVersion();
        var isPatched = newtonsoftVer.Contains("sp1", StringComparison.OrdinalIgnoreCase);

        return @"<!DOCTYPE html>
<html>
<head>
    <title>Seal Security NuGet Demo</title>
    <link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css"" rel=""stylesheet"">
    <style>
        body { background: #0d1117; color: #e6edf3; min-height: 100vh; }
        .seal-card { background: #161b22; border: 1px solid #30363d; border-radius: 12px; }
        .seal-brand { color: #58a6ff; }
        .vuln-badge { background: #da3633; padding: 4px 10px; border-radius: 20px; font-size: 0.8rem; font-weight: 600; }
        .fixed-badge { background: #238636; padding: 4px 10px; border-radius: 20px; font-size: 0.8rem; font-weight: 600; }
        .result-box { background: #0d1117; border: 1px solid #30363d; border-radius: 8px; padding: 20px; min-height: 80px; font-family: 'SF Mono', Monaco, monospace; font-size: 0.9rem; }
        .btn-test { background: #da3633; border: none; font-weight: 600; padding: 12px 32px; font-size: 1.1rem; }
        .btn-test:hover { background: #f85149; }
        .dep-table { font-size: 0.85rem; }
        .dep-table td, .dep-table th { padding: 8px 12px; border-color: #30363d; }
        .dep-table th { background: #161b22; color: #8b949e; font-weight: 500; }
        .spinner-border { width: 1rem; height: 1rem; }
        .cve-link { color: #f85149; text-decoration: none; }
        .cve-link:hover { text-decoration: underline; color: #f85149; }
    </style>
</head>
<body>
    <div class=""container"" style=""max-width: 720px; padding-top: 60px;"">
        
        <!-- Header -->
        <div class=""text-center mb-4"">
            <h1 class=""mb-2"">
                <span class=""seal-brand"">Seal Security</span> NuGet Demo
            </h1>
            <p class=""text-secondary"">Demonstrating transparent vulnerability remediation for .NET dependencies</p>
        </div>

        <!-- Dependency Table -->
        <div class=""seal-card p-3 mb-4"">
            <table class=""table table-dark dep-table mb-0"">
                <thead>
                    <tr>
                        <th>Package</th>
                        <th>Version</th>
                        <th>CVE</th>
                        <th>Severity</th>
                    </tr>
                </thead>
                <tbody>
                    <tr>
                        <td><strong>Newtonsoft.Json</strong></td>
                        <td><code>" + EscapeHtml(newtonsoftVer) + @"</code></td>
                        <td><a class=""cve-link"" href=""https://nvd.nist.gov/vuln/detail/CVE-2024-21907"" target=""_blank"">CVE-2024-21907</a></td>
                        <td><span class=""vuln-badge"">HIGH 7.5</span></td>
                    </tr>
                    <tr>
                        <td><strong>log4net</strong></td>
                        <td><code>2.0.5-sp1</code></td>
                        <td><a class=""cve-link"" href=""https://nvd.nist.gov/vuln/detail/CVE-2018-1285"" target=""_blank"">CVE-2018-1285</a></td>
                        <td><span class=""vuln-badge"">CRITICAL 9.8</span></td>
                    </tr>
                    <tr>
                        <td><strong>System.Net.Http</strong></td>
                        <td><code>4.3.0-sp1</code></td>
                        <td><a class=""cve-link"" href=""https://nvd.nist.gov/vuln/detail/CVE-2017-0249"" target=""_blank"">CVE-2017-0249</a></td>
                        <td><span class=""vuln-badge"">HIGH 7.5</span></td>
                    </tr>
                </tbody>
            </table>
        </div>

        <!-- CVE-2024-21907 Test -->
        <div class=""seal-card p-4 mb-4"">
            <h4 class=""mb-1"">CVE-2024-21907 &mdash; Deep Recursion DoS</h4>
            <p class=""text-secondary small mb-3"">
                Unpatched Newtonsoft.Json &le; 13.0.1 crashes with a <code>StackOverflowException</code> 
                when parsing deeply nested JSON, causing a Denial of Service.
            </p>

            <div class=""d-flex align-items-center gap-3 mb-3"">
                <label class=""text-secondary"" for=""depthSlider"">Nesting depth:</label>
                <input type=""range"" class=""form-range flex-grow-1"" id=""depthSlider"" min=""100"" max=""5000"" value=""1000"" step=""100"">
                <span id=""depthValue"" class=""text-white"" style=""min-width:50px"">1000</span>
            </div>

            <button id=""testBtn"" class=""btn btn-test btn-lg text-white w-100 mb-3"" onclick=""testVuln()"">
                Test Vulnerability
            </button>

            <div id=""resultBox"" class=""result-box text-secondary"">
                Click the button to send deeply nested JSON to the server...
            </div>
        </div>

        <!-- Footer -->
        <div class=""text-center text-secondary small pb-4"">
            <p>
                " + (isPatched ? @"<span class=""fixed-badge"">PATCHED BY SEAL SECURITY</span>" : @"<span class=""vuln-badge"">UNPATCHED &mdash; VULNERABLE</span>") + @"
            </p>
            <p class=""mt-2"">
                <a href=""/api/parse?json={%22test%22:%22hello%22}"" class=""text-secondary"">JSON API</a>
                &nbsp;&middot;&nbsp;
                <a href=""https://sealsecurity.io"" class=""text-secondary"" target=""_blank"">sealsecurity.io</a>
            </p>
        </div>
    </div>

    <script>
        const slider = document.getElementById('depthSlider');
        const depthLabel = document.getElementById('depthValue');
        slider.addEventListener('input', () => depthLabel.textContent = slider.value);

        async function testVuln() {
            const btn = document.getElementById('testBtn');
            const box = document.getElementById('resultBox');
            const depth = slider.value;

            btn.disabled = true;
            btn.innerHTML = '<span class=""spinner-border""></span> Sending nested JSON (depth ' + depth + ')...';
            box.innerHTML = 'Sending request...';
            box.style.borderColor = '#30363d';

            try {
                const start = Date.now();
                const resp = await fetch('/api/test-vuln?depth=' + depth);
                const elapsed = Date.now() - start;
                const data = await resp.json();

                if (data.success) {
                    box.style.borderColor = '#238636';
                    box.innerHTML = 
                        '<div style=""color:#3fb950;font-size:1.1rem;margin-bottom:8px"">&#10003; SAFE &mdash; Seal Security patch active</div>' +
                        '<div>Parsed ' + depth + ' levels of nested JSON in <strong>' + data.elapsedMs + 'ms</strong> (server) / ' + elapsed + 'ms (round-trip)</div>' +
                        '<div class=""mt-2 text-secondary"">Without the patch, this would crash the server with a StackOverflowException.</div>';
                } else {
                    box.style.borderColor = '#da3633';
                    box.innerHTML = 
                        '<div style=""color:#f85149;font-size:1.1rem;margin-bottom:8px"">&#10007; VULNERABLE &mdash; Server crashed!</div>' +
                        '<div>' + data.message + '</div>';
                }
            } catch (e) {
                box.style.borderColor = '#da3633';
                box.innerHTML = 
                    '<div style=""color:#f85149;font-size:1.1rem;margin-bottom:8px"">&#10007; VULNERABLE &mdash; Server crashed!</div>' +
                    '<div>The server became unreachable. This confirms CVE-2024-21907 &mdash; the deeply nested JSON caused a fatal StackOverflowException.</div>' +
                    '<div class=""mt-2 text-secondary"">With Seal Security, this would be handled safely.</div>';
            }

            btn.disabled = false;
            btn.innerHTML = 'Test Vulnerability';
        }
    </script>
</body>
</html>";
    }

    private static string EscapeHtml(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
    }
}
