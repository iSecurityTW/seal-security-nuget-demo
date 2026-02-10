using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using log4net;

namespace SealSecurityNuGetDemo.Controllers;

/// <summary>
/// Simple welcome page. Takes a name, parses it through Newtonsoft.Json, displays a greeting.
/// If the input looks like a URL, fetches the content first — a realistic pattern
/// (config loaders, API testers, webhook receivers all do this).
/// Newtonsoft.Json 12.0.2 is vulnerable to CVE-2024-21907 (DoS via deep recursion).
/// </summary>
[ApiController]
public class HelloController : ControllerBase
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(HelloController));
    private static readonly HttpClient _http = new();

    [HttpGet("/")]
    [Produces("text/html")]
    public IActionResult Home()
    {
        return Content(Greeting("World"), "text/html");
    }

    [HttpPost("/")]
    [Produces("text/html")]
    public async Task<IActionResult> Submit([FromForm] string name)
    {
        string input = name ?? "World";

        // If input is a URL, fetch its content — realistic pattern
        if (input.StartsWith("http://") || input.StartsWith("https://"))
        {
            try
            {
                input = await _http.GetStringAsync(input);
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
