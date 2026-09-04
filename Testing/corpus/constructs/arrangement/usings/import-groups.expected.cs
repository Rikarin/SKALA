// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
using Zeta.Support;
using System.Text;

// dotnet_separate_import_directive_groups = false, so no blank line separates the two groups.
//
// ⚠ Both directions are the oracle's: at `true` it writes exactly one blank line between directives
// in different groups, and at the export's `false` it takes every blank line between two adjacent
// directives back out. The grouping is (kind, first segment) — `System` and `System.Text` would be
// one group, `System.Text` and `Zeta.Support` are two, and a plain directive, a `using static` and an
// alias are three kinds that never share a group whatever their segments.
//
// ⚠ The source carries a blank line inside the using block, and it is load-bearing. It used to be
// written deliberately *without* one, because the oracle strips such a line under CSReformatCode
// alone while Skala's formatter did not read the key at all — so a blank line here would have
// measured that gap rather than this option, and the fixture dodged it. That gap is closed
// (SK-DIV-0074): the formatter owns the key now, so this file measures both directions on both
// profiles and the dodge is what would hide a regression.
//
// ⚠ Two namespaces in the file, deliberately: it keeps csharp_style_namespace_declarations and
// csharp_using_directive_placement out of this fixture, so the only thing that moves here is the
// order and the separation.
namespace Skala.Corpus.Arrangement.Imports {
    public class ImportGroups {
        public string Use() {
            var builder = new StringBuilder();
            builder.Append(Helper.Twice(5));
            return builder.ToString();
        }
    }
}

namespace Zeta.Support {
    public static class Helper {
        public static int Twice(int value) {
            return value * 2;
        }
    }
}
