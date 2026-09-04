// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
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
