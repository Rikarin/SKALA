// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaCleanup generated=2026-08-30
namespace Skala.Corpus.Arrangement;

// resharper_csharp_braces_redundant = true, on its own file.
//
// ⚠ This fixture cannot agree with the oracle at the sweep's baseline and is not expected to. Removing
// a redundant brace pair is one of SK-DIV-0013's three rewrites: the export configures it, doc 06 asks
// for it, Skala performs it, and `jb cleanupcode` 2025.2.6 performs none of the three. The key is
// therefore unattributable by construction, and the point of giving it a file of its own is that it
// stops making *other* keys unattributable — it used to live in qualifiers-and-parentheses.cs and
// broke that file's baseline for two Roslyn keys that have nothing to do with braces.
public class BracesRedundant {
    public void RedundantBraces(int a) {
        {
            {
                Console.WriteLine(a);
            }
        }

        // ⚠ Refused: the inner block declares, and lifting it widens the declaration's scope.
        {
            var scoped = a;
            Console.WriteLine(scoped);
        }
    }
}
