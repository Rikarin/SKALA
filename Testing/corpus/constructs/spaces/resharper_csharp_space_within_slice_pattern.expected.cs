// skala-oracle: resharper=2025.2.6 config=sha256:98ff52570e019fac profile=SkalaFormatOnly generated=2026-08-26
class C {
    bool M(int[] xs) => xs is [1, .. var rest, 3] && rest.Length > 0;
}
