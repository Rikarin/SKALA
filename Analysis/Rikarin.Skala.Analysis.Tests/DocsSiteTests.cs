using System.Reflection;
using Rikarin.Skala.Options;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     <c>docs/site/</c> is generated and never hand-edited, and it is byte-identical every time.
/// </summary>
/// <remarks>
///     The sibling of <c>RuleCatalogTests.DocsPages_AreUpToDate</c>, and stricter than it in the one
///     way that matters here. That test asserts <em>containment</em> after normalising whitespace,
///     because <c>Rikarin.Skala.Rules.Tests</c> may not reference <c>Analysis</c> and so cannot call
///     the renderer at all. This assembly can, so the comparison is byte for byte — which is the only
///     comparison that catches the failure a committed site actually has, namely a regeneration on
///     another machine that produces a different file for a reason that has nothing to do with
///     <c>rules.json</c>.
/// </remarks>
public sealed class DocsSiteTests {
    static string RepositoryRoot { get; } =
        Assembly.GetExecutingAssembly()
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .First(static attribute => attribute.Key == "SkalaRepositoryRoot")
        .Value!;

    static string SiteDirectory { get; } = Path.Combine(RepositoryRoot, "docs", "site");

    /// <summary>
    ///     ⚠ The test that makes the site worth committing: a <c>rules.json</c> or <c>options.json</c>
    ///     edit with no <c>skala docs site</c> after it is a red build, not a page that quietly
    ///     describes the previous behaviour.
    /// </summary>
    /// <remarks>
    ///     It asserts both directions. A missing or stale page is the obvious half; a file in
    ///     <c>docs/site/</c> that the renderer no longer produces is the half that rots silently, because
    ///     nothing links to it and every other assertion here only ever looks at pages that exist on
    ///     both sides.
    /// </remarks>
    [Fact]
    public void Site_IsUpToDateWithTheSources() {
        Assert.True(Directory.Exists(SiteDirectory), $"{SiteDirectory} does not exist. Run `skala docs site`.");

        var rendered = DocsSite.Render();
        foreach (var page in rendered) {
            var path = Path.Combine(SiteDirectory, page.Path.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"docs/site/{page.Path} is missing. Run `skala docs site`.");
            Assert.True(
                string.Equals(File.ReadAllText(path), page.Content, StringComparison.Ordinal),
                $"docs/site/{page.Path} is not what the sources render today. Run `skala docs site`."
            );
        }

        var expected = rendered.Select(static page => page.Path).ToHashSet(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(SiteDirectory, "*", SearchOption.AllDirectories)) {
            var relative = DocsSite.Relative(SiteDirectory, path);
            Assert.True(
                expected.Contains(relative),
                $"docs/site/{relative} is not produced by the renderer any more. Run `skala docs site`, "
                + "which deletes it."
            );
        }
    }

    /// <summary>
    ///     ⚠ The committed-artefact condition, the way <c>CloneIndex</c> states it: two runs over the
    ///     same sources produce byte-identical files.
    /// </summary>
    /// <remarks>
    ///     Cheap and worth having anyway, because the failure it catches is a dictionary or a
    ///     <c>GroupBy</c> whose enumeration order is stable within a process and not across processes —
    ///     which shows up as a several-hundred-line diff on a colleague's machine and as nothing at all
    ///     on the author's.
    /// </remarks>
    [Fact]
    public void TwoRenders_ProduceByteIdenticalPages() {
        var first = DocsSite.Render();
        var second = DocsSite.Render();

        Assert.Equal(first.Count, second.Count);
        for (var i = 0; i < first.Count; i++) {
            Assert.Equal(first[i].Path, second[i].Path);
            Assert.True(
                string.Equals(first[i].Content, second[i].Content, StringComparison.Ordinal),
                $"{first[i].Path} differs between two renders in the same process."
            );
        }

        var paths = first.Select(static page => page.Path).ToArray();
        Assert.Equal(paths.OrderBy(static path => path, StringComparer.Ordinal).ToArray(), paths);
    }

    /// <summary>
    ///     ⚠ Every newline is <c>\n</c> and no carriage return survives.
    /// </summary>
    /// <remarks>
    ///     The determinism above is per-process; this is the cross-platform half of it, and it is the
    ///     defect <c>ExplainCommand</c> has today. That renderer is written with
    ///     <see cref="System.Text.StringBuilder.AppendLine()" />, which emits
    ///     <see cref="Environment.NewLine" />, so <c>skala rules docs</c> run on Windows rewrites all 33
    ///     committed markdown pages to CRLF — and <c>DocsPages_AreUpToDate</c> cannot see it, because it
    ///     normalises whitespace before comparing. A whole-file diff produced by the operating system
    ///     rather than by a change is how a generated artefact stops being reviewed.
    /// </remarks>
    [Fact]
    public void NoPage_CarriesACarriageReturn() {
        foreach (var page in DocsSite.Render()) {
            Assert.DoesNotContain('\r', page.Content);
        }
    }

    /// <summary>Every rule has a page and every option key has an entry, and both are reachable.</summary>
    [Fact]
    public void EveryRuleAndEveryOptionKey_IsOnAPage() {
        var pages = DocsSite.Render()
            .ToDictionary(
                static page => page.Path,
                static page => page.Content,
                StringComparer.Ordinal
            );

        foreach (var rule in RuleCatalog.All) {
            var path = "rules/" + rule.Id + ".html";
            Assert.True(pages.ContainsKey(path), $"{rule.Id} has no page in the site.");
            Assert.Contains(rule.Id, pages["rules/index.html"], StringComparison.Ordinal);
        }

        var anchors = pages
            .Where(static page => page.Key.StartsWith("options/", StringComparison.Ordinal))
            .SelectMany(static page => Anchors(page.Value))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var option in OptionRegistry.All) {
            Assert.True(
                anchors.Contains(option.Key),
                $"'{option.Key}' is in options.json and has no entry anywhere under docs/site/options/."
            );
        }
    }

    /// <summary>
    ///     ⚠ Every internal link resolves — to a page the renderer produces, and to an anchor that page
    ///     actually carries.
    /// </summary>
    /// <remarks>
    ///     The whole argument for a generated site over a directory of markdown is that it is
    ///     cross-linked, and a cross-link is worth nothing the moment one of them 404s. The two ways
    ///     that happens here are both mechanical and both silent: a construct renamed in options.json
    ///     moves every key's anchor to a new page, and a rule id referenced in another rule's
    ///     <c>configuration</c> prose need not be a rule that exists.
    ///     <para>
    ///         ⚠ Fragments are checked as well as paths. A link to the right page and a dead anchor lands
    ///         the reader at the top of a page holding 27 keys, which is worse than an error.
    ///     </para>
    /// </remarks>
    [Fact]
    public void EveryInternalLink_ResolvesToAPageAndAnAnchor() {
        var pages = DocsSite.Render()
            .ToDictionary(
                static page => page.Path,
                static page => page.Content,
                StringComparer.Ordinal
            );

        var anchorsOf = pages.ToDictionary(
            static page => page.Key,
            static page => Anchors(page.Value).ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal
        );

        var checkedLinks = 0;
        foreach (var (path, content) in pages.OrderBy(static page => page.Key, StringComparer.Ordinal)) {
            foreach (var href in Hrefs(content)) {
                if (href.StartsWith("http://", StringComparison.Ordinal)
                    || href.StartsWith("https://", StringComparison.Ordinal)) {
                    continue;
                }

                var hash = href.IndexOf('#', StringComparison.Ordinal);
                var target = hash < 0 ? href : href[..hash];
                var fragment = hash < 0 ? null : href[(hash + 1)..];
                var resolved = target.Length == 0 ? path : Resolve(path, target);

                Assert.True(
                    pages.ContainsKey(resolved),
                    $"docs/site/{path} links to '{href}', which resolves to '{resolved}' and is not a page."
                );

                if (fragment is { Length: > 0 }) {
                    Assert.True(
                        anchorsOf[resolved].Contains(fragment),
                        $"docs/site/{path} links to '{href}' and '{resolved}' has no id '{fragment}'."
                    );
                }

                checkedLinks++;
            }
        }

        // ⚠ A link checker that finds no links passes. The site has hundreds; the floor is only
        // there so that a renderer which stopped emitting anchors fails here rather than silently.
        Assert.True(checkedLinks > 500, $"only {checkedLinks} internal links were checked; the site has hundreds.");
    }

    /// <summary>
    ///     The 107 constructs must produce 107 pages, because a slug collision loses one of them whole.
    /// </summary>
    [Fact]
    public void EveryConstruct_GetsItsOwnPage() {
        var constructs = OptionRegistry.All
            .Select(static option => option.Construct)
            .Distinct(StringComparer.Ordinal)
            .Count();

        var generated = DocsSite.Render()
            .Count(static page =>
                page.Path.StartsWith("options/", StringComparison.Ordinal)
                && !string.Equals(page.Path, "options/index.html", StringComparison.Ordinal)
            );

        Assert.Equal(constructs, generated);
    }

    /// <summary>⚠ No external asset: the site renders from a checkout with no network.</summary>
    /// <remarks>
    ///     A <c>&lt;script&gt;</c>, a webfont or a CDN stylesheet is one commit away at any time and
    ///     nothing else in the tree would notice. Outbound <c>&lt;a href&gt;</c> links are fine and
    ///     there are several — options.json carries a <c>docs</c> URL per key — so the assertion is
    ///     about what the page <em>loads</em>, not about what it points at.
    /// </remarks>
    [Fact]
    public void NoPage_LoadsAnythingFromTheNetwork() {
        foreach (var page in DocsSite.Render()) {
            Assert.DoesNotContain("<script", page.Content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<img", page.Content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("@import", page.Content, StringComparison.Ordinal);
            foreach (var href in Attributes(page.Content, "src=\"")) {
                Assert.Fail($"docs/site/{page.Path} loads '{href}'.");
            }

            foreach (var href in Attributes(page.Content, "<link rel=\"stylesheet\" href=\"")) {
                Assert.EndsWith("style.css", href, StringComparison.Ordinal);
            }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    static IEnumerable<string> Hrefs(string content) => Attributes(content, "href=\"");

    static IEnumerable<string> Anchors(string content) => Attributes(content, "id=\"");

    /// <summary>
    ///     Every value of one attribute spelling, by scanning rather than by regular expression.
    /// </summary>
    /// <remarks>
    ///     The renderer writes the attributes; it never quotes them differently and never puts a
    ///     <c>"</c> inside one, because every value it writes has been through <c>Esc</c>. So a scan is
    ///     exact here in a way it would not be over arbitrary HTML.
    /// </remarks>
    static IEnumerable<string> Attributes(string content, string prefix) {
        var i = content.IndexOf(prefix, StringComparison.Ordinal);
        while (i >= 0) {
            var start = i + prefix.Length;
            var end = content.IndexOf('"', start);
            if (end < 0) {
                yield break;
            }

            yield return content[start..end];
            i = content.IndexOf(prefix, end, StringComparison.Ordinal);
        }
    }

    /// <summary><c>rules/SK0001.html</c> + <c>../options/general.html</c> = <c>options/general.html</c>.</summary>
    static string Resolve(string page, string href) {
        var segments = new List<string>();
        var directory = page.Contains('/', StringComparison.Ordinal)
            ? page[..page.LastIndexOf('/')]
            : string.Empty;

        if (directory.Length > 0) {
            segments.AddRange(directory.Split('/'));
        }

        foreach (var segment in href.Split('/')) {
            switch (segment) {
                case ".":
                case "":
                    break;
                case "..":
                    if (segments.Count > 0) {
                        segments.RemoveAt(segments.Count - 1);
                    }

                    break;
                default:
                    segments.Add(segment);
                    break;
            }
        }

        return string.Join('/', segments);
    }
}
