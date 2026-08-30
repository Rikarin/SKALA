// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-30
class EmbeddedStatementPlacement {
    void M(bool flag) {
        if (flag) OnTheSameLine();

        if (flag)
            OnItsOwnLine();

        while (flag) OnTheSameLine();

        for (var i = 0; i < 10; i++) OnTheSameLine();

        foreach (var item in items)
            OnItsOwnLine();
    }
}
