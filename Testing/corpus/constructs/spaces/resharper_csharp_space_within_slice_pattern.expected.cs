// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
class C {
    bool M(int[] xs) => xs is [1, .. var rest, 3] && rest.Length > 0;
}
