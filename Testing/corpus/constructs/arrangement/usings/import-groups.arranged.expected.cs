// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaCleanup generated=2026-08-28
using System.Text;
using Zeta.Support;

// dotnet_separate_import_directive_groups = false, so no blank line separates the two groups.
//
// ⚠ Both directions are the oracle's, measured under the cleanup profile: at `true` it writes
// exactly one blank line between directives whose *first* namespace segment differs, and at the
// export's `false` it takes every blank line inside the block back out. The grouping is by first
// segment and nothing finer, so `System` and `System.Text` would be one group and `System.Text` and
// `Zeta.Support` are two.
//
// ⚠ The removal direction is not written into this file, and the reason is a *formatter* gap rather
// than an arrangement one: the oracle strips a blank line inside the using block under
// CSReformatCode alone, and Skala's formatter does not read this key at all. A source blank line
// here would measure that gap instead of this option. SK-DIV-0074, pinned by ArrangementRuleTests.
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
        public static int Twice(int value) => value * 2;
    }
}
