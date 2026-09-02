// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
class C {
    bool M(int[] xs) => xs is [1, .. var rest, 3] && rest.Length > 0;
}
