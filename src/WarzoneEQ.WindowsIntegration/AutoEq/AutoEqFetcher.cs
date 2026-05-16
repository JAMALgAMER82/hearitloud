using System.Net;

namespace WarzoneEQ.WindowsIntegration.AutoEq;

// Fetches per-headphone correction curves from jaakkopasanen/AutoEq on GitHub.
// AutoEQ tracks ~5,000 headphone measurements as ParametricEQ text files (the
// exact format EQ APO consumes via `Include:`). We mirror them locally to
// %APPDATA%\HearItLoud\autoeq-cache\<slug>.txt so a flaky network during a
// game doesn't break audio.
//
// URL pattern as of 2026:
//   https://raw.githubusercontent.com/jaakkopasanen/AutoEq/master/
//     results/{measurement-rig}/{brand}/{model}/{model} ParametricEQ.txt
//
// AutoEQ doesn't expose a clean per-slug API, so we use a known-good
// pattern: results/oratory1990/harman_over-ear_2018/{slug}/{slug} ParametricEQ.txt
// which covers ~80% of popular headphones. Users with rarer ones can drop
// their own correction file in the cache dir manually.
public sealed class AutoEqFetcher
{
    private const string BaseUrl = "https://raw.githubusercontent.com/jaakkopasanen/AutoEq/master/results";

    public string CacheDir { get; }

    public AutoEqFetcher(string? cacheDir = null)
    {
        CacheDir = cacheDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HearItLoud", "autoeq-cache");
        Directory.CreateDirectory(CacheDir);
    }

    public string CachePathFor(string slug) => Path.Combine(CacheDir, $"{slug}.txt");

    public bool IsCached(string slug) => File.Exists(CachePathFor(slug));

    // Tries each known rig/target combo until one returns HTTP 200. Returns
    // the cached file path on success, null if nothing matched. Logs progress
    // via the optional callback (used by the GUI to stream status to the log).
    public async Task<string?> FetchAsync(string slug, Action<string>? log = null, CancellationToken ct = default)
    {
        var local = CachePathFor(slug);
        if (File.Exists(local))
        {
            log?.Invoke($"[autoeq] cached: {slug}");
            return local;
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("HearItLoud-AutoEq");

        foreach (var url in CandidateUrls(slug))
        {
            try
            {
                log?.Invoke($"[autoeq] trying {url}");
                var resp = await http.GetAsync(url, ct);
                if (resp.StatusCode == HttpStatusCode.NotFound) continue;
                resp.EnsureSuccessStatusCode();
                var content = await resp.Content.ReadAsStringAsync(ct);
                if (!LooksLikeParametricEq(content)) continue;
                await File.WriteAllTextAsync(local, content, ct);
                log?.Invoke($"[autoeq] cached {slug} ({content.Length} bytes)");
                return local;
            }
            catch (HttpRequestException ex)
            {
                log?.Invoke($"[autoeq] {url} -> {ex.Message}");
            }
        }

        log?.Invoke($"[autoeq] no curve found for '{slug}' on any known target.");
        log?.Invoke($"[autoeq] drop a custom ParametricEQ.txt at {local} to use one.");
        return null;
    }

    // AutoEQ's directory layout. Each tuple is (rig, target). The slug appears
    // twice in each path (folder + filename), which is how the repo is laid out.
    private static IEnumerable<string> CandidateUrls(string slug)
    {
        var safe = Uri.EscapeDataString(slug);
        var rigs = new[]
        {
            "oratory1990/harman_over-ear_2018",
            "oratory1990/harman_in-ear_2019v2",
            "rtings/harman_over-ear_2018",
            "crinacle/harman_in-ear_2019v2",
            "innerfidelity/harman_over-ear_2018",
        };
        foreach (var rig in rigs)
            yield return $"{BaseUrl}/{rig}/{safe}/{safe}%20ParametricEQ.txt";
    }

    // Sanity check on the downloaded file before we save it. EQ APO will
    // happily Include: any file path, but a 404 HTML body or empty response
    // would silently break the chain.
    private static bool LooksLikeParametricEq(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;
        if (content.Length < 32) return false;
        // ParametricEQ.txt files start with "Preamp:" and have "Filter X:" lines.
        return content.Contains("Preamp:", StringComparison.OrdinalIgnoreCase)
            && content.Contains("Filter", StringComparison.OrdinalIgnoreCase);
    }
}
