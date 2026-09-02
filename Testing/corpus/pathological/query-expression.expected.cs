// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
using System.Linq;

class C {
    object M(int[] xs) =>
        from x in xs
        where x > 0
        orderby x descending
        group x by x % 2
        into g
        select g;
}
