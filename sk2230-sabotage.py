#!/usr/bin/env python3
"""Break one guard at a time and record which fixtures notice.

⚠ A sabotage that turns nothing red is a finding: it means the guard is either unreachable or has
no fixture asserting it. Every row below names the fixture that is *expected* to go red, and the
run reports agreement or disagreement rather than just the failure list.
"""
import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parent
RULES = ROOT / "Rules" / "Rikarin.Skala.Rules" / "Correctness"
LOG = pathlib.Path(
    "/private/tmp/claude-501/-Users-jiu-Projects-Rikarin-Skala"
    "/47988376-61d9-4b69-88aa-67b899fc2755/scratchpad"
)

SQL = RULES / "SqlFragmentsRunTogetherAnalyzer.cs"
CMD = RULES / "CommandParameterNotSuppliedAnalyzer.cs"
ALC = RULES / "AssemblyLoadedOutsideItsContextAnalyzer.cs"
TYP = RULES / "MistakenTypeArgumentAnalyzer.cs"

# (name, file, find, replace, fixtures expected to go red)
SABOTAGES = [
    (
        "SK2230/drop-the-looks-like-sql-gate",
        SQL,
        "if (LiteralTextOf(operands[0]) is not { } opening || !OpensAStatement(opening)) {",
        "if (LiteralTextOf(operands[0]) is not { } opening) {",
        ["nothing_here_opens_a_statement"],
    ),
    (
        "SK2230/drop-the-apostrophe-parity-guard",
        SQL,
        "if (quotes % 2 == 0\n                && LiteralTextOf",
        "if (quotes % 2 >= 0\n                && LiteralTextOf",
        # ⚠ `the_word_after_the_join_is_not_a_keyword` was expected here too and stayed green. Its
        # right-hand word is `setting`, which the continuation list does not hold, so it is declined
        # by the keyword test before the parity test is reached -- it proves a different guard.
        ["the_fusion_is_inside_a_sql_string"],
    ),
    (
        "SK2230/drop-the-left-side-word-character-test",
        SQL,
        "&& IsWordCharacter(left[left.Length - 1])\n                && IsWordCharacter(right[0])",
        "&& IsWordCharacter(right[0])",
        ["the_left_fragment_ends_with_a_space", "a_star_and_a_keyword_do_not_fuse"],
    ),
    (
        "SK2230/also-test-the-left-hand-keyword-direction",
        SQL,
        "&& ContinuationKeywords.Contains(LeadingWord(right).ToUpperInvariant())",
        "&& (ContinuationKeywords.Contains(LeadingWord(right).ToUpperInvariant())"
        " || ContinuationKeywords.Contains(LastWord(left).ToUpperInvariant()))",
        ["a_table_name_split_over_two_lines"],
    ),
    (
        "SK2231/drop-the-at-least-one-add-gate",
        CMD,
        "if (supplied.Count == 0) {\n            return;\n        }",
        "if (supplied.Count == -1) {\n            return;\n        }",
        ["no_parameter_is_added_at_all"],
    ),
    (
        "SK2231/treat-an-unrecognised-use-as-harmless",
        CMD,
        "            return reference.Parent is VariableDeclaratorSyntax or UsingStatementSyntax;",
        "            return true;",
        ["the_command_escapes_the_method"],
    ),
    (
        "SK2231/treat-an-unrecognised-parameters-call-as-harmless",
        CMD,
        "                // `AddRange`, `Insert`, `RemoveAt`, `Clear` — every one of them changes the set in a\n"
        "                // way the rule cannot read, so the method is abandoned.\n"
        "                return false;",
        "                return true;",
        ["an_add_range_the_rule_cannot_read"],
    ),
    (
        "SK2231/accept-a-computed-parameter-name",
        CMD,
        "        if (model.GetConstantValue(expression, cancellation) is not { HasValue: true, Value: string name }) {\n"
        "            return false;\n        }",
        "        if (model.GetConstantValue(expression, cancellation) is not { HasValue: true, Value: string name }) {\n"
        "            return true;\n        }",
        ["a_computed_parameter_name"],
    ),
    (
        "SK2231/stop-skipping-a-sql-string-literal",
        CMD,
        "            if (c == '\\'') {",
        "            if (c == '\\u0001') {",
        # ⚠ Not `an_address_inside_a_sql_string`, which was the first guess and stayed green: the
        # `@` in `root@localhost` is preceded by a word character and is skipped before the quote
        # counting is consulted. The apostrophe guard had no fixture at all until this one.
        ["a_marker_inside_a_sql_string"],
    ),
    (
        "SK2231/stop-skipping-a-sql-comment",
        CMD,
        "            if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-') {",
        "            if (c == '\\u0001' && i + 1 < sql.Length && sql[i + 1] == '-') {",
        ["a_marker_inside_a_comment"],
    ),
    (
        "SK2231/allow-a-field-as-the-receiver",
        CMD,
        "        if (command.Kind != SymbolKind.Local) {",
        "        if (command.Kind is not (SymbolKind.Local or SymbolKind.Field)) {",
        ["the_command_is_a_field"],
    ),
    (
        "SK2231/allow-a-second-command-text-assignment",
        CMD,
        "                return access.Parent is not AssignmentExpressionSyntax assigned\n"
        "                    || assigned.Left != access\n                    || assigned == textAssignment;",
        "                return true;",
        ["the_text_is_assigned_twice"],
    ),
    (
        "SK2231/ignore-a-command-type-assignment",
        CMD,
        "                return access.Parent is not AssignmentExpressionSyntax type || type.Left != access;",
        "                return true;",
        ["a_stored_procedure_name"],
    ),
    (
        "SK2232/report-assembly-load-as-well",
        ALC,
        'is not ("LoadFrom" or "LoadFile")',
        'is not ("LoadFrom" or "LoadFile" or "Load")',
        ["assembly_load_shares_the_contract"],
    ),
    (
        "SK2232/drop-the-override-requirement",
        ALC,
        "        if (!SitsInsideTheContextsOwnResolver(invocation, model, loadContext, cancellation)) {\n"
        "            return;\n        }",
        "        _ = SitsInsideTheContextsOwnResolver(invocation, model, loadContext, cancellation);",
        [
            "load_from_outside_any_load_context",
            "load_from_in_another_member_of_the_context",
            "inside_a_lambda_in_the_override",
            "inside_a_local_function_in_the_override",
            "an_override_of_something_else",
        ],
    ),
    (
        "SK2232/walk-through-lambdas-and-local-functions",
        ALC,
        "                case AnonymousFunctionExpressionSyntax:\n"
        "                case LocalFunctionStatementSyntax:\n                    return false;\n\n",
        "",
        ["inside_a_lambda_in_the_override", "inside_a_local_function_in_the_override"],
    ),
    (
        "SK2233/stop-declining-a-type-parameter",
        TYP,
        "|| argument.TypeKind is TypeKind.Error or TypeKind.Dynamic or TypeKind.TypeParameter or TypeKind.Unknown) {",
        "|| argument.TypeKind is TypeKind.Error or TypeKind.Dynamic or TypeKind.Unknown) {",
        ["a_type_parameter_operand"],
    ),
    (
        "SK2233/require-strictly-derives-from-rather-than-or-equals",
        TYP,
        "        for (var current = candidate; current is not null; current = current.BaseType) {\n"
        "            if (SymbolEqualityComparer.Default.Equals(current, target)) {",
        "        for (var current = candidate.BaseType; current is not null; current = current.BaseType) {\n"
        "            if (SymbolEqualityComparer.Default.Equals(current, target)) {",
        ["typeof_attribute_itself_means_any"],
    ),
    (
        "SK2233/match-the-argument-by-index-instead-of-by-name",
        TYP,
        "            if (string.Equals(method.Parameters[i].Name, parameterName, StringComparison.Ordinal)) {",
        "            if (i == 0) {",
        ["get_custom_attribute_on_a_non_attribute"],
    ),
    (
        "SK2233/stop-declining-an-interface-for-activator",
        TYP,
        "            Contract.Instantiable => argument is not {\n                TypeKind: TypeKind.Interface\n"
        "            } and not { IsAbstract: true } and not { IsStatic: true },",
        "            Contract.Instantiable => true,",
        ["create_instance_of_an_interface", "create_instance_of_an_abstract_class"],
    ),
]


def run() -> tuple[bool, list[str]]:
    result = subprocess.run(
        [
            "dotnet", "test",
            "Rules/Rikarin.Skala.Rules.Tests/Rikarin.Skala.Rules.Tests.csproj",
            "--nologo", "--filter", "FullyQualifiedName~SqlAndReflectionBatchTests",
        ],
        cwd=ROOT, capture_output=True, text=True,
    )
    text = result.stdout + result.stderr
    if "error CS" in text:
        return False, ["DID NOT COMPILE: " + next(
            line.strip() for line in text.splitlines() if "error CS" in line
        )]

    # ⚠ The test display name truncates the fixture path, so the failing fixture's identity is only
    # ever in the assertion message. Parsing the display name found nothing and reported every
    # sabotage as "red: (none)" -- a parser that cannot fail loudly, which is the instrument bug
    # this whole pass exists to catch in the rules.
    red = sorted({m.group(1) + "/" + m.group(2) for m in re.finditer(r"(SK2\d{3})/[+−]/([a-z_0-9]+):", text)})

    # A batch test failing is red too, and its name is not a fixture path.
    red += sorted({
        m.group(1) for m in re.finditer(r"SqlAndReflectionBatchTests\.([A-Za-z_]+)", text)
        if "[FAIL]" in text[m.start(): m.end() + 120] and "EveryFixtureInTheBatch" not in m.group(1)
    })

    return result.returncode == 0, red


def main() -> None:
    only = sys.argv[1] if len(sys.argv) > 1 else None
    report = []
    for name, path, find, replace, expected in SABOTAGES:
        if only and only not in name:
            continue

        original = path.read_text(encoding="utf-8")
        if find not in original:
            report.append((name, "PATCH DID NOT APPLY", []))
            print(name, "-> PATCH DID NOT APPLY", flush=True)
            continue

        path.write_text(original.replace(find, replace, 1), encoding="utf-8")
        try:
            green, red = run()
        finally:
            path.write_text(original, encoding="utf-8")

        if green:
            verdict = "⚠ NOTHING TURNED RED"
        elif set(expected) <= {name.split("/")[-1] for name in red}:
            verdict = "red, as expected"
        else:
            verdict = "red, but NOT the expected fixture(s)"

        report.append((name, verdict, red))
        print(f"{name}\n    {verdict}\n    red: {', '.join(red) or '(none)'}", flush=True)

    (LOG / "sk2230-sabotage-report.txt").write_text(
        "\n".join(f"{n}\n    {v}\n    red: {', '.join(r) or '(none)'}" for n, v, r in report),
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
