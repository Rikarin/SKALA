// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-29
class C {
    void AlreadyAligned() {
        var a = """
                line one
                  line two
                """;
    }

    void FlushLeft() {
        var b = """
                line one
                  line two
                """;
    }

    void OverIndented() {
        var c = """
                line one
                  line two
                """;
    }

    void WithABlankLine() {
        var d = """
                line one

                line three
                """;
    }
}
