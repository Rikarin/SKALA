// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaCleanup generated=2026-09-02
using System.Globalization;
using System.Text;

namespace Skala.Corpus.Arrangement.Placement;

// csharp_using_directive_placement = outside_namespace, so the block written inside the namespace
// is hoisted above it — and the namespace itself becomes file-scoped on the way, which is
// csharp_style_namespace_declarations.
//
// ⚠ Measured against jb cleanupcode 2025.2.6 under the cleanup profile: at inside_namespace a
// block written above the declaration is pushed below it instead, so the key moves this file in
// both directions. An *alias* directive at nested scope is not hoisted — that half is pinned by
// ArrangementRuleTests rather than here, because the oracle also replaces a usable alias with the
// short name and Skala has no reference-shortening rule to match it with (SK-DIV-0073).
public class Placement {
    public string Use(int value) {
        var builder = new StringBuilder();
        builder.Append(value.ToString(CultureInfo.InvariantCulture));
        return builder.ToString();
    }
}
