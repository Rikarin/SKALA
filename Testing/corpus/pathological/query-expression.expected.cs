// skala-oracle: resharper=2025.2.6 config=sha256:bd9791d3a6e6a087 profile=SkalaFormatOnly generated=2026-08-26
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
