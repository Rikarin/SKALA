import sys

C = "Rules/Rikarin.Skala.Rules/Correctness/"
GROUPS = {
    "1": [
        (C + "MisleadingBodyIndentationAnalyzer.cs",
         "            || !string.Equals(nextIndent, bodyIndent, StringComparison.Ordinal)) {",
         "            || nextIndent.Length < bodyIndent.Length) {"),
        (C + "VariableLengthHexEscapeAnalyzer.cs",
         "            if (digits is >= 1 and <= 3) {",
         "            if (digits >= 1) {"),
        (C + "ForgivenIsOperandAnalyzer.cs",
         "        if ((context.SemanticModel.GetNullableContext(suppression.SpanStart) & NullableContext.WarningsEnabled) == 0) {\n            return;\n        }\n\n",
         ""),
        (C + "NegatedEmptyPatternAnalyzer.cs",
         "                Designation: null,\n",
         ""),
        (C + "UnparenthesisedPrecedenceMixAnalyzer.cs",
         "        if (innerFamily == PrecedenceFamily.Shift && parentFamily != PrecedenceFamily.Shift) {\n            return;\n        }\n\n",
         ""),
    ],
    "2": [
        (C + "MisleadingBodyIndentationAnalyzer.cs",
         "        if (body is EmptyStatementSyntax) {\n            return;\n        }\n\n",
         ""),
        (C + "VariableLengthHexEscapeAnalyzer.cs",
         "            if (text[i + 1] != 'x') {\n                i++;\n                continue;\n            }",
         "            if (text[i + 1] != 'x') {\n                continue;\n            }"),
        (C + "ForgivenIsOperandAnalyzer.cs",
         "        if (type is null || type.IsValueType && type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T) {\n            return;\n        }\n\n",
         ""),
        (C + "NegatedEmptyPatternAnalyzer.cs",
         "        if (RewriteGuards.ContainsCommentOrDirective(not)) {\n            return;\n        }\n\n",
         ""),
        (C + "UnparenthesisedPrecedenceMixAnalyzer.cs",
         "            || innerFamily == parentFamily) {",
         "            || false) {"),
    ],
    "3": [
        (C + "MisleadingBodyIndentationAnalyzer.cs",
         "        if (RewriteGuards.ContainsCommentOrDirective(\n                header.SyntaxTree,\n                TextSpan.FromBounds(body.Span.End, next.SpanStart)\n            )) {\n            return;\n        }\n\n",
         ""),
    ],
}

for path, old, new in GROUPS[sys.argv[1]]:
    s = open(path, encoding="utf-8").read()
    if s.count(old) != 1:
        raise SystemExit("count %d in %s for %r" % (s.count(old), path, old[:60]))
    open(path, "w", encoding="utf-8").write(s.replace(old, new))
    print("sabotaged", path)
