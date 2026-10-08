class InterpolatedRawLiteralAlignment {
    void Shifted(int x) {
        var interpolated = $"""
                        Hello {x}
                        World
                        """;
        var doubled = $$"""
                Hello {{x}}
                World
                """;
        var nested = $"""
                        Outer {$"""
                                    inner {x}
                                    """}
                        World
                        """;
        var flush = $"""
Hello {x}
""";
        var blank = $"""
                        Hello {x}

                        World
                        """;
        var verbatim = $@"
                        Hello {x}
                        ";
    }

    void AHoleWhoseLineStays(int x) {
        var hole = $"""
                    Hello {
                        x
                    } there
                    World
                    """;
    }

    void Chopped(int x) {
        Call($"""
                        Arg {x}
                        """);
    }

    void Call(string s) { }
}
