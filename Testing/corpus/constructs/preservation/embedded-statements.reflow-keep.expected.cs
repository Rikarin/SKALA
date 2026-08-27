// skala-oracle: resharper=2025.2.6 config=sha256:bd9791d3a6e6a087 profile=SkalaFormatOnly generated=2026-08-27
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
