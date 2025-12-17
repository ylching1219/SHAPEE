using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SHAPEE.Controllers
{
    [Authorize]
    public class SearchController : Controller
    {
        public class QuickLink
        {
            public string Role { get; set; } = "";
            public string Title { get; set; } = "";
            public string Url { get; set; } = "";
            public string? Group { get; set; }

            // search terms used for keyword matching
            public string[] Keywords { get; set; } = Array.Empty<string>();
        }

        // list for quicklink load from json
        private static List<QuickLink>? _quickLinks;

        // lock object to ensure safe loading
        private static readonly object _initLock = new();

        public SearchController()
        {
            // load quick links only once for the whole session
            if (_quickLinks != null)
                return;

            lock (_initLock)
            {
                if (_quickLinks != null)
                    return;

                // locate quicklinks.json at root
                var appRoot = Directory.GetCurrentDirectory();
                var jsonPath = Path.Combine(appRoot, "quicklinks.json");

                if (System.IO.File.Exists(jsonPath))
                {
                    var jsonContent = System.IO.File.ReadAllText(jsonPath);

                    _quickLinks = JsonSerializer.Deserialize<List<QuickLink>>(
                        jsonContent,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        }
                    ) ?? new List<QuickLink>();
                }
                else
                {
                    // fallback to empty list if file is missing
                    _quickLinks = new List<QuickLink>();
                }
            }
        }

        [HttpGet]
        public IActionResult Quick(string term)
        {
            // normalize search input
            var searchTerm = (term ?? "").Trim();

            // return empty result if no search term provide
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return Json(new { results = Array.Empty<object>() });
            }

            // check user role
            var userRole =
                User.IsInRole("Admin") ? "Admin" :
                User.IsInRole("Staff") ? "Staff" :
                User.IsInRole("Student") ? "Student" :
                null;

            // deny search results if role is not found
            if (userRole == null)
            {
                return Json(new { results = Array.Empty<object>() });
            }

            // ensure quick links list is loaded
            var links = _quickLinks ?? new List<QuickLink>();

            // filter links by role and match keywords
            var matchedResults = links
                .Where(link =>
                    string.Equals(link.Role, userRole, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(link.Role, "All", StringComparison.OrdinalIgnoreCase))
                .Where(link =>
                    link.Keywords.Any(keyword =>
                        keyword.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)))
                .Take(10)
                .Select(link => new
                {
                    title = link.Title,
                    url = link.Url,
                    group = link.Group
                })
                .ToList();

            // return matched quick navigation result
            return Json(new { results = matchedResults });
        }
    }
}
