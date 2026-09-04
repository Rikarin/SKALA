// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
class EmbeddedStatements {
    void M(bool flag) {
        if (flag) First();

        if (flag) Second();

        while (flag) Third();

        foreach (var item in items) Use(item);
    }
}
