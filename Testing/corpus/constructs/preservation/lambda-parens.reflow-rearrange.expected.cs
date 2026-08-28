// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
class LambdaParens {
    void M() {
        Use((int first) => first);
        Use((int first, int second) => first);
        Use(delegate(int first) { return first; });
    }
}
