// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-29
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
