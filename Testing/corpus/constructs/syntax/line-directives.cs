// LineSpanDirectiveTrivia and LineDirectivePosition — C# 10's `#line (a, b) - (c, d) e "file"` —
// occurred nowhere, while the plain LineDirectiveTrivia did. A directive is a DirectiveNode, so
// `resharper_csharp_indent_preprocessor_other` decides its column, and the span form is the only
// directive in the language that carries a parenthesised position rather than a bare number.
class LineDirectives {
    static int Plain() {
#line 42
        return 0;
#line default
    }

    static int Named() {
#line 42 "Generated.g.cs"
        return 1;
#line default
    }

#line (7, 2) - (9, 40) 6 "Component.razor"
    static int Spanned() => 2;
#line default

    static int Nested(bool flag) {
        if (flag) {
#line (11, 5) - (11, 44) "Component.razor"
            return 3;
#line hidden
        }

        return 4;
#line default
    }

#line (100, 1) - (140, 120) 24 "AVeryLongGeneratedFileNameThatPushesTheDirectivePastTheMargin.razor"
    static int Overflowing() => 5;
#line default
}
