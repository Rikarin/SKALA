// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-17
namespace Constructs.Breaks;

// SK-DIV-0114 (issue #371). The two cref parameter lists live inside a documentation comment,
// which the break planner's walk never enters: the comment is the xmldoc formatter's, and a break
// the author wrote inside `cref="M(int,↵int)"` is kept by it, by the pinned oracle profile and by
// the profile that formats doc comments (SK-DIV-0006) alike. Pinned so that the kinds are
// accounted for rather than planned.
public class CrefParameterList {
    /// <summary>
    ///     See <see cref="M(int,
    ///     int)" /> after a comma.
    /// </summary>
    void AfterComma() { }

    /// <summary>
    ///     See <see cref="M(
    ///     int, int)" /> after the open.
    /// </summary>
    void AfterOpen() { }

    /// <summary>
    ///     See <see cref="this[int,
    ///     int]" /> bracketed.
    /// </summary>
    void Bracketed() { }

    void M(int a, int b) { }

    int this[int a, int b] => a;
}
