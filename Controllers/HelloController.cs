using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using log4net;

namespace SealSecurityNuGetDemo.Controllers;

/// <summary>
/// Simple welcome page with JSON import. Takes a name or loads JSON from a URL.
/// Newtonsoft.Json 12.0.2 is vulnerable to CVE-2024-21907 (DoS via deep recursion).
/// </summary>
[ApiController]
public class HelloController : ControllerBase
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(HelloController));
    private static readonly HttpClient _http = new();

    [HttpGet("/")]
    [Produces("text/html")]
    public async Task<IActionResult> Home([FromQuery] string? name, [FromQuery] string? url)
    {
        string input = "World";

        if (!string.IsNullOrEmpty(url))
        {
            // Fetch JSON from a URL — a common pattern in real apps
            // (API testing tools, JSON validators, webhook receivers, etc.)
            try
            {
                input = await _http.GetStringAsync(url);
            }
            catch (Exception ex)
            {
                input = $"Error fetching URL: {ex.Message}";
            }
        }
        else if (!string.IsNullOrEmpty(name))
        {
            input = name;
        }

        return Content(Greeting(input), "text/html");
    }

    [HttpPost("/")]
    [Produces("text/html")]
    public IActionResult Submit([FromForm] string? name, [FromForm] string? url)
    {
        string input = name ?? "World";

        if (!string.IsNullOrEmpty(url))
        {
            try
            {
                input = _http.GetStringAsync(url).Result;
            }
            catch (Exception ex)
            {
                input = $"Error fetching URL: {ex.Message}";
            }
        }

        return Content(Greeting(input), "text/html");
    }

    private string Greeting(string name)
    {
        // Parse input through Newtonsoft.Json — same pattern as Maven demo's yaml.load(name).
        // With unpatched 12.0.2: deeply nested JSON → StackOverflowException → process crash
        // With Seal-patched 12.0.2-sp1: same input parsed safely
        string displayName;
        try
        {
            var parsed = JsonConvert.DeserializeObject<JToken>(name);
            displayName = parsed?.ToString(Formatting.None) ?? name;
        }
        catch
        {
            displayName = name;
        }

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
            "<div class=\"input-group mb-3\">" +
            "<input type=\"text\" name=\"url\" class=\"form-control\" placeholder=\"Or load JSON from URL\" value=\"\">" +
            "<button type=\"submit\" class=\"btn btn-outline-secondary\">Load</button>" +
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
}
