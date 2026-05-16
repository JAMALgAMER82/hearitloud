using FluentAssertions;
using WarzoneEQ.WindowsIntegration.AutoEq;
using Xunit;

namespace WarzoneEQ.WindowsIntegration.Tests.AutoEq;

public class AutoEqFetcherTests : IDisposable
{
    private readonly string _cacheDir;

    public AutoEqFetcherTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), "hearitloud-autoeq-test-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir)) Directory.Delete(_cacheDir, recursive: true);
    }

    [Fact]
    public void Ctor_creates_the_cache_directory()
    {
        _ = new AutoEqFetcher(_cacheDir);
        Directory.Exists(_cacheDir).Should().BeTrue();
    }

    [Fact]
    public void CachePathFor_returns_a_slug_dot_txt_under_cache_dir()
    {
        var f = new AutoEqFetcher(_cacheDir);
        f.CachePathFor("HD600").Should().Be(Path.Combine(_cacheDir, "HD600.txt"));
    }

    [Fact]
    public void IsCached_is_false_for_a_fresh_cache()
    {
        var f = new AutoEqFetcher(_cacheDir);
        f.IsCached("HD600").Should().BeFalse();
    }

    [Fact]
    public void IsCached_is_true_after_a_file_is_dropped_into_the_cache()
    {
        var f = new AutoEqFetcher(_cacheDir);
        File.WriteAllText(f.CachePathFor("HD600"), "Preamp: -2.0 dB\nFilter 1: ON PK Fc 100 Hz Gain +1.0 dB Q 0.7");
        f.IsCached("HD600").Should().BeTrue();
    }

    [Fact]
    public async Task FetchAsync_returns_cache_path_without_hitting_network_when_already_cached()
    {
        var f = new AutoEqFetcher(_cacheDir);
        var path = f.CachePathFor("PreCached");
        File.WriteAllText(path, "Preamp: 0\nFilter 1: ON PK Fc 1000 Hz Gain +0.5 dB Q 1.0");

        // No log lines mentioning network attempts should appear.
        var lines = new List<string>();
        var result = await f.FetchAsync("PreCached", lines.Add);
        result.Should().Be(path);
        lines.Should().ContainSingle(l => l.Contains("cached:"));
        lines.Should().NotContain(l => l.Contains("trying https://"));
    }
}
