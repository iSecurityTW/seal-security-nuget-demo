using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using log4net;

namespace SealSecurityNuGetDemo.Controllers;

/// <summary>
/// Simple welcome page. Takes a name, parses it through Newtonsoft.Json, displays a greeting.
/// Newtonsoft.Json 12.0.2 is vulnerable to CVE-2024-21907 (DoS via deep recursion).
/// </summary>
[ApiController]
public class HelloController : ControllerBase
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(HelloController));

    [HttpGet("/")]
    [Produces("text/html")]
    public IActionResult Home()
    {
        return Content(GenerateGreetingPage("World"), "text/html");
    }

    [HttpPost("/")]
    [Produces("text/html")]
    public IActionResult Submit([FromForm] string name)
    {
        return Content(GenerateGreetingPage(name), "text/html");
    }

    [HttpGet("/api/parse")]
    public IActionResult ParseJson([FromQuery] string json = "{\"name\": \"demo\"}")
    {
        try
        {
            Logger.Info($"Parsing JSON input: {json.Substring(0, Math.Min(100, json.Length))}...");
            
            // This uses Newtonsoft.Json vulnerable to CVE-2024-21907
            // Deep recursion in JSON can cause stack overflow (DoS)
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
            
            // Re-parse to demonstrate the vulnerability
            var parsed = JsonConvert.DeserializeObject<JToken>(jsonString);
            
            return Ok(new { success = true, parsed = parsed });
        }
        catch (Exception ex)
        {
            Logger.Error($"Error parsing JSON: {ex.Message}");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    private string GenerateGreetingPage(string name)
    {
        // Parse the name through JSON to demonstrate JSON handling
        string displayName;
        try
        {
            // Try to parse as JSON, otherwise use as-is
            var jsonName = $"\"{EscapeJsonString(name)}\"";
            displayName = JsonConvert.DeserializeObject<string>(jsonName) ?? name;
        }
        catch
        {
            displayName = name;
        }

        Logger.Info($"Generated greeting for: {displayName}");

        return $@"<!DOCTYPE html>
<html>
<head>
    <title>Welcome - Seal Security NuGet Demo</title>
    <link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.0.2/dist/css/bootstrap.min.css"" rel=""stylesheet"">
</head>
<body style=""background:#f8f9fa;"">
    <div style=""max-width:500px; margin:100px auto; text-align:center;"">
        <h1 class=""mb-4"">Welcome, {EscapeHtml(displayName)}!</h1>
        <form method=""POST"" action=""/"">
            <div class=""input-group mb-3"">
                <input type=""text"" name=""name"" class=""form-control"" placeholder=""Enter your name"" value="""">
                <button type=""submit"" class=""btn btn-primary"">Go</button>
            </div>
        </form>
        <hr class=""my-4"">
        <p class=""text-muted small"">
            This demo uses <strong>Newtonsoft.Json 12.0.2</strong> which has 
            <a href=""https://nvd.nist.gov/vuln/detail/CVE-2024-21907"" target=""_blank"">CVE-2024-21907</a> 
            (CVSS 7.5 HIGH).
        </p>
        <p class=""text-muted small"">
            <a href=""/api/parse?json={{%22test%22:%22value%22}}"">Try the JSON API</a>
        </p>
    </div>
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

    private static string EscapeJsonString(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
    }
}
