// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
class EmbeddedStatements {
    void M(bool flag) {
        if (flag)
            First();

        if (flag) Second();

        while (flag)
            Third();

        foreach (var item in items)
            Use(item);
    }
}
