// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
using Builder = System.Text.StringBuilder;
using Numbers = System.Collections.Generic.List<int>;
using StringBuilder = System.Text.StringBuilder;

namespace Skala.Corpus.Arrangement;

// resharper_csharp_keep_nontrivial_alias = false against resharper_remove_only_unused_aliases =
// true: the export's pair, and the only one of the four that removes an unused *non-trivial* alias.
//
// ⚠ Measured against jb cleanupcode 2025.2.6 under the cleanup profile over all four combinations.
// `Builder` is used and survives everywhere. `StringBuilder` is unused and *trivial* — the alias
// name is the aliased type's own name — and goes at all four. `Numbers` is unused and non-trivial
// and goes only here, which is what makes each of the two keys observable on its own.
//
// ⚠ The registry recorded keep_nontrivial_alias as inert, and the probe that established it had the
// aliases in use, where nothing can remove them at either value. See SK-DIV-0072.
public class Aliases {
    public string Use() {
        var builder = new Builder();
        builder.Append(1);
        return builder.ToString();
    }
}
