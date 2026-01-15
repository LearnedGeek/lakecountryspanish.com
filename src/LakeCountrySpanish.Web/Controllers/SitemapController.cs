using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Xml.Linq;

namespace LakeCountrySpanish.Web.Controllers;

/// <summary>
/// Controller for generating XML sitemap for search engines.
/// </summary>
public class SitemapController : Controller
{
    private readonly IConfiguration _configuration;

    public SitemapController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet("/sitemap.xml")]
    [ResponseCache(Duration = 3600)] // Cache for 1 hour
    public IActionResult Index()
    {
        var baseUrl = _configuration["App:BaseUrl"] ?? "https://lakecountryspanish.com";

        var pages = new List<SitemapUrl>
        {
            new(baseUrl, DateTime.UtcNow, ChangeFrequency.Weekly, 1.0),
            new($"{baseUrl}/Home/About", DateTime.UtcNow, ChangeFrequency.Monthly, 0.8),
            new($"{baseUrl}/Contact", DateTime.UtcNow, ChangeFrequency.Monthly, 0.7),
            new($"{baseUrl}/Subscription/Plans", DateTime.UtcNow, ChangeFrequency.Weekly, 0.9),
        };

        var sitemap = GenerateSitemapXml(pages);

        return Content(sitemap, "application/xml", Encoding.UTF8);
    }

    private static string GenerateSitemapXml(IEnumerable<SitemapUrl> urls)
    {
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

        var sitemap = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(ns + "urlset",
                urls.Select(url => new XElement(ns + "url",
                    new XElement(ns + "loc", url.Location),
                    new XElement(ns + "lastmod", url.LastModified.ToString("yyyy-MM-dd")),
                    new XElement(ns + "changefreq", url.ChangeFrequency.ToString().ToLowerInvariant()),
                    new XElement(ns + "priority", url.Priority.ToString("F1"))
                ))
            )
        );

        return sitemap.Declaration + Environment.NewLine + sitemap.ToString();
    }

    private record SitemapUrl(
        string Location,
        DateTime LastModified,
        ChangeFrequency ChangeFrequency,
        double Priority
    );

    private enum ChangeFrequency
    {
        Always,
        Hourly,
        Daily,
        Weekly,
        Monthly,
        Yearly,
        Never
    }
}
