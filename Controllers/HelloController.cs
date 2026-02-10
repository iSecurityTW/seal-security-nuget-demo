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
        return Content(Greeting("World"), "text/html");
    }

    [HttpPost("/")]
    [Produces("text/html")]
    public IActionResult Submit([FromForm] string name)
    {
        return Content(Greeting(name), "text/html");
    }

    private string Greeting(string name)
    {
        // Parse the name through Newtonsoft.Json — mirrors the Maven demo's SnakeYAML usage.
        // With unpatched 12.0.2, deeply nested JSON input triggers CVE-2024-21907
        // (StackOverflowException → process crash → browser shows error page,
        //  crash details visible in GH Action logs).
        // With Seal-patched 12.0.2-sp1, the same input is parsed safely.
        var json = $"{{\"name\": \"{EscapeJsonString(name)}\"}}";
        var parsed = JsonConvert.DeserializeObject<JToken>(json);
        var displayName = parsed?["name"]?.ToString() ?? name;

        Logger.Info($"Greeting: {displayName}");

        return "<html>" +
            "<head>" +
            "<title>Welcome</title>" +
            "<link href=\"https://cdn.jsdelivr.net/npm/bootstrap@5.0.2/dist/css/bootstrap.min.css\" rel=\"stylesheet\">" +
            "</head>" +
            "<body style=\"background:#f8f9fa;\">" +
            "<div style=\"max-width:500px; margin:100px auto; text-align:center;\">" +
            "<h1 class=\"mb-4\">Welcome, " + EscapeHtml(displayName) + "!</h1>" +
            "<form method=\"POST\" action=\"/\">" +
            "<div class=\"input-group mb-3\">" +
            "<input type=\"text\" name=\"name\" class=\"form-control\" placeholder=\"Enter your name\" value=\"\">" +
            "<button type=\"submit\" class=\"btn btn-primary\">Go</button>" +
            "</div>" +
            "</form>" +
            "</div>" +
            "</body>" +
            "</html>";
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
