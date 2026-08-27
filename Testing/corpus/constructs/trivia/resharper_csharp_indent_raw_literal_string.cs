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
