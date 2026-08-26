// skala-oracle: resharper=2025.2.6 config=sha256:98ff52570e019fac profile=SkalaFormatOnly generated=2026-08-26
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
