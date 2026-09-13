using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Par30GamesApi.Models;

namespace Par30GamesApi.Services;

public sealed class Par30GamesScraper
{
    private readonly HttpClient _http;
    public Par30GamesScraper(HttpClient http) => _http = http;

    public Task<List<Game>> GetGamesAsync(int page = 1) =>
        GetListAsync(page <= 1 ? "pc/" : $"pc/page/{page}/");

    public async Task<Game?> GetGameAsync(int id)
    {
        var html = await GetAsync(id.ToString());
        var doc = Load(html);
        var canonical = doc.DocumentNode.SelectSingleNode("//link[@rel='canonical']")?.GetAttributeValue("href", "");
        var url = !string.IsNullOrWhiteSpace(canonical) ? canonical : FindGameUrl(doc, id);
        if (string.IsNullOrWhiteSpace(url)) return null;

        var game = ParseGame(doc, url);
        return game.Id == 0 ? null : game;
    }

    public Task<List<Game>> SearchGamesAsync(string query, int page = 1)
    {
        var q = Uri.EscapeDataString(query);
        return GetListAsync($"?s={q}" + (page > 1 ? $"&paged={page}" : ""));
    }

    public async Task<List<GameCategory>> GetPcCategoriesAsync()
    {
        var doc = Load(await GetAsync("pc/"));
        var result = new Dictionary<string, GameCategory>(StringComparer.OrdinalIgnoreCase);

        foreach (var a in doc.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>())
        {
            var name = Clean(a.InnerText);
            var href = Abs(a.GetAttributeValue("href", ""));
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(href)) continue;
            if (href.Contains("/category/", StringComparison.OrdinalIgnoreCase) &&
                (name.Contains("بازی") || href.Contains("/pc", StringComparison.OrdinalIgnoreCase)))
                result[href] = new GameCategory { Name = name, Url = href };
        }
        return result.Values.OrderBy(x => x.Name).ToList();
    }

    public Task<List<Game>> GetGamesByCategoryAsync(string categoryUrl, int page = 1)
    {
        var uri = new Uri(categoryUrl);
        var path = uri.AbsolutePath.TrimEnd('/');
        var target = page <= 1 ? path.TrimStart('/') + "/" : $"{path}/page/{page}/";
        return GetListAsync(target);
    }

    private async Task<List<Game>> GetListAsync(string path)
    {
        var doc = Load(await GetAsync(path));
        var result = new List<Game>();
        var seen = new HashSet<int>();

        foreach (var a in doc.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>())
        {
            var href = Abs(a.GetAttributeValue("href", ""));
            var title = Clean(a.InnerText);
            if (!IsLikelyGameUrl(href, title)) continue;

            var id = ExtractId(href);
            if (id <= 0 || !seen.Add(id)) continue;

            var img = a.SelectSingleNode(".//img")?.GetAttributeValue("data-src", "") ??
                      a.SelectSingleNode(".//img")?.GetAttributeValue("src", "") ?? "";

            result.Add(new Game
            {
                Id = id,
                Name = title,
                Url = href,
                Image = Abs(img)
            });
        }
        return result;
    }

    private Game ParseGame(HtmlDocument doc, string url)
    {
        var title = Clean(doc.DocumentNode.SelectSingleNode("//h1")?.InnerText ?? "");
        if (title.Length == 0)
            title = Clean(doc.DocumentNode.SelectSingleNode("//title")?.InnerText ?? "");

        var image = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']")
            ?.GetAttributeValue("content", "") ?? "";

        var text = Clean(doc.DocumentNode.InnerText);
        var game = new Game
        {
            Id = ExtractId(url),
            Name = StripSiteSuffix(title),
            Url = url,
            Image = Abs(image),
            Description = GetDescription(doc),
            Developer = FindLabel(text, "توسعه دهنده"),
            Publisher = FindLabel(text, "ناشر"),
            Genre = FindLabel(text, "ژانر"),
            ReleaseDate = FindLabel(text, "تاریخ انتشار"),
            Platforms = FindLabel(text, "پلتفرم")
        };

        // Download sections are intentionally parsed generically:
        // headings become versions; links containing part/file/download become parts.
        string current = "";
        var currentDownload = new GameDownload();
        var links = doc.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>();
        foreach (var node in links)
        {
            var label = Clean(node.InnerText);
            var href = Abs(node.GetAttributeValue("href", ""));
            if (label.Length == 0 || href.Length == 0) continue;

            if (IsVersionHeading(label))
            {
                if (currentDownload.Parts.Count > 0)
                    game.Downloads.Add(currentDownload);
                current = label;
                currentDownload = new GameDownload { Version = current };
                continue;
            }

            if (IsDownloadLink(label, href))
            {
                if (current.Length == 0)
                {
                    current = "Unknown";
                    currentDownload = new GameDownload { Version = current };
                }

                var part = ExtractPartNumber(label + " " + href, currentDownload.Parts.Count + 1);
                currentDownload.Parts.Add(new GamePart
                {
                    Part = part,
                    Name = label,
                    Size = ExtractSize(label),
                    Url = href
                });
            }
        }
        if (currentDownload.Parts.Count > 0) game.Downloads.Add(currentDownload);

        // Deduplicate/sort.
        game.Downloads = game.Downloads
            .Where(x => x.Parts.Count > 0)
            .GroupBy(x => x.Version, StringComparer.OrdinalIgnoreCase)
            .Select(x => {
                var d = x.First();
                d.Parts = d.Parts.GroupBy(p => p.Url).Select(g => g.First())
                    .OrderBy(p => p.Part).ToList();
                return d;
            }).ToList();

        return game;
    }

    private async Task<string> GetAsync(string path)
    {
        using var response = await _http.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private HtmlDocument Load(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }

    private string FindGameUrl(HtmlDocument doc, int id)
    {
        foreach (var a in doc.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>())
        {
            var href = Abs(a.GetAttributeValue("href", ""));
            if (ExtractId(href) == id) return href;
        }
        return "";
    }

    private static bool IsLikelyGameUrl(string url, string title)
    {
        if (!url.Contains("par30games.net", StringComparison.OrdinalIgnoreCase)) return false;
        if (ExtractId(url) <= 0) return false;
        if (url.Contains("/category/") || url.Contains("/tag/") || url.Contains("/author/")) return false;
        if (string.IsNullOrWhiteSpace(title)) return false;
        return title.Contains("دانلود", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("-game-pc", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("/pc/", StringComparison.OrdinalIgnoreCase);
    }

    private static int ExtractId(string url)
    {
        var m = Regex.Match(url, @"par30games\.net/(\d+)(?:/|$)", RegexOptions.IgnoreCase);
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : 0;
    }

    private string Abs(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        if (Uri.TryCreate(value, UriKind.Absolute, out var u)) return u.ToString();
        return new Uri(_http.BaseAddress!, value).ToString();
    }

    private static string Clean(string s) =>
        WebUtility.HtmlDecode(Regex.Replace(s ?? "", @"\s+", " ")).Trim();

    private static string StripSiteSuffix(string s) =>
        Regex.Replace(s, @"\s*[-|–]\s*پارسی گیم.*$", "", RegexOptions.IgnoreCase).Trim();

    private static string GetDescription(HtmlDocument doc) =>
        Clean(doc.DocumentNode.SelectSingleNode("//meta[@name='description']")?.GetAttributeValue("content", "") ?? "");

    private static string FindLabel(string text, string label)
    {
        var m = Regex.Match(text, Regex.Escape(label) + @"\s*[:：]?\s*(.{2,100})");
        return m.Success ? m.Groups[1].Value.Trim() : "";
    }

    private static bool IsVersionHeading(string s)
    {
        var x = s.ToLowerInvariant();
        return x.Contains("fitgirl") || x.Contains("elamigos") || x.Contains("p2p") ||
               x.Contains("dodi") || x.Contains("codex") || x.Contains("repack") ||
               x.Contains("نسخه") || x.Contains("فشرده");
    }

    private static bool IsDownloadLink(string label, string href)
    {
        var x = (label + " " + href).ToLowerInvariant();
        return x.Contains("part") || x.Contains("پارت") || x.Contains("دانلود") ||
               x.Contains("download") || x.Contains("mediafire") || x.Contains("mega") ||
               x.Contains("1fichier") || x.Contains("upload") || x.Contains("dl.");
    }

    private static int ExtractPartNumber(string s, int fallback)
    {
        var m = Regex.Match(s, @"(?:part|پارت)[\s_-]*(\d+)", RegexOptions.IgnoreCase);
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : fallback;
    }

    private static string ExtractSize(string s)
    {
        var m = Regex.Match(s, @"(\d+(?:[.,]\d+)?)\s*(GB|MB|TB|گیگ|مگ)", RegexOptions.IgnoreCase);
        return m.Success ? m.Value : "";
    }
}
