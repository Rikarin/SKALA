// skala-oracle: resharper=2025.2.6 config=sha256:bd9791d3a6e6a087 profile=SkalaFormatOnly generated=2026-08-27
class C {
    bool M(int[] xs) => xs is [1, .. var rest, 3] && rest.Length > 0;
}
