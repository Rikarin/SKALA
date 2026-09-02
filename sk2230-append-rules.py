#!/usr/bin/env python3
"""Append the SK2230-SK2233 entries to rules.json, preserving the file's exact formatting.

The file is read and written as text rather than round-tripped through json.dump, because
json.dump would reflow all 7 700 lines and bury four entries in a whole-file diff.
"""
import json
import pathlib

ROOT = pathlib.Path(__file__).resolve().parent
PATH = ROOT / "Rules" / "Rikarin.Skala.Rules.Metadata" / "rules.json"

ENTRIES = [
    {
        "id": "SK2230",
        "concept": "sql-fragments-run-together",
        "title": "The concatenated SQL fragments run together",
        "category": "Correctness",
        "defaultSeverity": "warning",
        "scope": "Syntax",
        "requiresSemantics": False,
        "hasFix": True,
        "fixIsSafe": False,
        "resharperId": None,
        "supersedes": ["S2857"],
        "since": "2.0",
        "languageVersion": None,
        "summary": "Two string literals are concatenated and the join fuses a word: `\"select * from users\" + \"where id = 1\"` is `... usersWHERE id = 1`, which no database will parse.",
        "rationale": "The statement was split across two lines to keep it readable and the space that separated the two halves went with the line break. Nothing catches it: it compiles, it is a perfectly good `string`, and the failure arrives at execution as a syntax error from the database naming a token the source file does not contain. ⚠ **The reason this is worth a rule rather than a test is where the failure lands.** A query built this way is usually on a branch — the paging clause, the optional filter, the retry path — so it is the one call the smoke test does not make, and the defect ships. ⚠ **`SK5001` reports the opposite half of the same subject and the two are disjoint by construction.** `SK5001` fires only when a value that crossed a trust boundary reaches the SQL; this rule fires only when every fragment is a written literal, and a literal is never tainted. A query can be injectable, or malformed, or both, and each finding comes from a different rule with a different fix.",
        "examples": {
            "bad": "command.CommandText = \"select id, name from users\"\n    + \"where active = 1\";",
            "good": "command.CommandText = \"select id, name from users \"\n    + \"where active = 1\";"
        },
        "falsePositives": "⚠ **The whole risk is the \"this is SQL\" test, so it is three conditions and not one.** The first literal in the concatenation must begin — after leading whitespace — with a statement keyword: `select`, `insert`, `update`, `delete`, `merge`, `with`, `create`, `alter`, `drop`, `truncate`, `from` or `where`. The character before the join and the character after it must both be word characters, so a fusion has actually occurred. And the word the right-hand literal *starts with* must itself be a SQL keyword, matched whole. ⚠ **Only the right-hand direction is tested, and the left-hand one was cut because it is wrong.** \"the left literal ends with a SQL keyword\" reports `\"select * from Order\" + \"Items\"` — a table name split across two lines, where `Order` is a keyword only by coincidence. There is no way to tell that apart from a real fusion, so the shape is not reported at all and the cost is a miss on `\"... where\" + \"id = 1\"`. ⚠ **A fusion inside a SQL string literal is declined by counting quotes.** `\"select * from t where m = 'err\" + \"ORDER'\"` fuses `err` and `ORDER`, and `ORDER` is a keyword, but the join is inside a `'…'` literal where a space would change the *value* rather than repair the *statement*. The apostrophes in the accumulated text before the join are counted, `''` included, and an odd count withdraws the finding. ⚠ **Both operands must be written literals.** `\"select * from \" + table + \"order by id\"` is the same defect and is not reported, because whether `table` ends in a space is not a fact in this file — asserting it would be guessing. Raw string literals are excluded outright: the fix inserts a space before a closing delimiter, and for a multi-line raw literal the delimiter's line is load-bearing. Interpolated strings are not literals and are never matched. ⚠ `fixIsSafe: false`. The edit changes the text the database receives, which is the entire finding, so it is not applied without a reader — and it is withheld where a comment or a directive sits between the two literals. Generated code is excluded.",
        "configuration": [
            "dotnet_diagnostic.SK2230.severity"
        ]
    },
    {
        "id": "SK2231",
        "concept": "command-parameter-not-supplied",
        "title": "The command's SQL names a parameter nothing supplies",
        "category": "Correctness",
        "defaultSeverity": "warning",
        "scope": "Semantic",
        "requiresSemantics": True,
        "hasFix": False,
        "fixIsSafe": False,
        "resharperId": None,
        "supersedes": [],
        "since": "2.0",
        "languageVersion": None,
        "summary": "A command's text is a constant naming `@a` and `@b`, the same method adds `@a`, and `@b` is never supplied — which the provider reports at execution and nothing reports at build.",
        "rationale": "The marker and the binding are two edits in two places and only one of them was made. It is the same join a format string and its arguments make, against a different grammar, and it has the same failure shape: the compiler sees a `string` on one side and a method call on the other and has no reason to relate them. ⚠ **Nothing upstream implements this** — the SonarSource issue it comes from is an open idea rather than a shipped rule — so the whole of the specification is the restriction list below. ⚠ **`SK5001` does not overlap it.** `SK5001` needs a tainted value reaching the SQL and says nothing about a constant; this rule requires the text to be *entirely* constant before it will read it, so no string can satisfy both.",
        "examples": {
            "bad": "var command = connection.CreateCommand();\ncommand.CommandText = \"select * from orders where id = @id and status = @status\";\ncommand.Parameters.AddWithValue(\"@id\", id);",
            "good": "var command = connection.CreateCommand();\ncommand.CommandText = \"select * from orders where id = @id and status = @status\";\ncommand.Parameters.AddWithValue(\"@id\", id);\ncommand.Parameters.AddWithValue(\"@status\", status);"
        },
        "falsePositives": "⚠ **The restrictions are the rule, and every one of them is a case where a report would be wrong rather than merely unwelcome.** The command must be a **local variable** — a field or a property is visible to every other method in the type, so what it has been given is not a fact this method holds. The command must not **escape**: passed as an argument, returned, assigned to anything, or used in any shape the rule does not recognise, and the whole method is declined. Every use of `Parameters` must be a recognised `Add`, `AddWithValue`, or `Add(new …Parameter(\"@name\", …))` with a **constant** name; an `AddRange`, a loop, an indexer write, or a single `Add` whose name is computed declines the method entirely, because after one unknown name every remaining marker is unknowable. `CommandText` must be assigned **exactly once** and its value must be a **compile-time constant**. If `CommandType` is assigned at all the method is declined, because a stored-procedure name is not SQL and its parameters are not in its text. ⚠ **At least one parameter must already have been added.** Zero is the shape where the binding most plausibly happens somewhere this rule cannot see, and #258's subject is getting the count wrong rather than forgetting entirely — so a command with markers and no `Add` at all is silent. ⚠ **Only markers the SQL really names.** `@@identity` and `@@rowcount` are T-SQL globals and are skipped; an `@` preceded by a word character is an address or a literal and is skipped; markers inside a `'…'` SQL string, a `--` comment or a `/* … */` comment are skipped. Comparison is ordinal-ignore-case with the leading `@` optional on the added name, because that is how the providers match. ⚠ **The other direction is not reported.** A parameter added and never named in the SQL is ignored by most providers rather than fatal, so it is a different and much weaker finding. ⚠ `hasFix: false`, for the reason `SK5001` carries: supplying the missing parameter means choosing a value, and there is no value in the file to substitute.",
        "configuration": [
            "dotnet_diagnostic.SK2231.severity"
        ]
    },
    {
        "id": "SK2232",
        "concept": "assembly-loaded-outside-its-context",
        "title": "The load context's own resolver loads outside it",
        "category": "Correctness",
        "defaultSeverity": "warning",
        "scope": "Semantic",
        "requiresSemantics": True,
        "hasFix": True,
        "fixIsSafe": False,
        "resharperId": None,
        "supersedes": [],
        "since": "2.0",
        "languageVersion": None,
        "summary": "An `AssemblyLoadContext.Load` override returns `Assembly.LoadFrom` or `Assembly.LoadFile`, which loads into some other context and leaves this one empty.",
        "rationale": "The override exists to place an assembly in *this* context. `Assembly.LoadFrom` places it in the default one and `Assembly.LoadFile` places it in a brand new anonymous one, so in both cases the context whose resolver was asked ends up not holding the assembly it resolved. The symptom is the one the isolation was bought to prevent: the same assembly is now present twice, and a type from one copy will not cast to the identically-named type from the other — `InvalidCastException` with a message that prints the same type name on both sides. ⚠ **This is the sound core of `S3885`, and the broad reading of that rule is deliberately not implemented.** \"`Assembly.Load` should be used\" reported everywhere would report every plugin host in existence: `LoadFrom` against a path is exactly right when the default context is where the assembly belongs, and which context an assembly belongs in is intent, not a fact in the file. Inside a `Load` override the intent *is* stated — by the override existing — so this is the one position where the question has an answer.",
        "examples": {
            "bad": "protected override Assembly? Load(AssemblyName name) {\n    var path = resolver.ResolveAssemblyToPath(name);\n    return path is null ? null : Assembly.LoadFrom(path);\n}",
            "good": "protected override Assembly? Load(AssemblyName name) {\n    var path = resolver.ResolveAssemblyToPath(name);\n    return path is null ? null : LoadFromAssemblyPath(path);\n}"
        },
        "falsePositives": "⚠ **`Assembly.Load` inside the override is not reported, and that exclusion is the point of the rule rather than a concession.** Returning `Assembly.Load(name)` from a `Load` override is the documented way to say \"this dependency is shared — take it from the default context\", which is how a plugin and its host agree on a contract assembly. It is a deliberate statement about context and the rule must not contradict it. Only `Assembly.LoadFrom` and `Assembly.LoadFile` are reported, because neither can put the assembly anywhere the override could have wanted it. ⚠ **Only the single-argument path overloads.** `LoadFrom` also has overloads taking a hash and a hash algorithm; they are matched by argument count so a future overload is a miss rather than a wrong fix. ⚠ **The enclosing member must be the override itself.** The call is looked up from the nearest enclosing method declaration and that declaration must override `AssemblyLoadContext.Load`; a lambda or a local function between the call and the override withdraws the finding, because a `static` lambda has no `this` to call the instance method on and the fix would not compile. ⚠ `fixIsSafe: false`, and the reason is a real behaviour difference rather than caution: `Assembly.LoadFrom` accepts a relative path and `LoadFromAssemblyPath` throws on one, so a call site that was resolving against the current directory needs a person to look at it. The fix is withheld where a comment or a directive sits in the span it rewrites. Generated code is excluded.",
        "configuration": [
            "dotnet_diagnostic.SK2232.severity"
        ]
    },
    {
        "id": "SK2233",
        "concept": "mistaken-type-argument",
        "title": "The `Type` passed cannot satisfy what the API asks for",
        "category": "Correctness",
        "defaultSeverity": "warning",
        "scope": "Semantic",
        "requiresSemantics": True,
        "hasFix": False,
        "fixIsSafe": False,
        "resharperId": "PossibleMistakenSystemTypeArgument",
        "supersedes": [],
        "since": "2.0",
        "languageVersion": None,
        "summary": "`Enum.GetValues(typeof(Widget))` on a type that is not an enum, `Attribute.GetCustomAttribute(m, typeof(Widget))` on a type that is not an attribute, `Activator.CreateInstance(typeof(IWidget))` on an interface — each throws on every input.",
        "rationale": "The API states which type it needs in its own documentation and enforces it with a run-time `ArgumentException`, and `typeof(…)` states which type it was given right there in the source. Both halves of the contradiction are visible at build time and the compiler relates neither to the other, because `Type` is `Type`. ⚠ **The failure is total, not conditional**: there is no input on which `Enum.GetValues(typeof(Widget))` succeeds, so this is not a risk being flagged but a line that cannot work. That is what makes it worth an `error`-adjacent severity on a rule that never guesses. ⚠ **`SK2181` is the neighbouring half and does not overlap.** `SK2181` reports `GetType()` called on something that is *already* a `Type` — the wrong *operation*. `SK2182` reports a type identified by comparing its name to a string — the wrong *test*. This one reports the wrong *type*, in a position where the API says what the right one would have to be.",
        "examples": {
            "bad": "foreach (var value in Enum.GetValues(typeof(Widget))) { }\nvar instance = Activator.CreateInstance(typeof(IWidget));",
            "good": "foreach (var value in Enum.GetValues(typeof(WidgetKind))) { }\nvar instance = Activator.CreateInstance(typeof(Widget));"
        },
        "falsePositives": "⚠ **A closed table of four contracts, matched by parameter *name* and never by index**, which is the same discipline `taint.json` uses and for the same reason: an overload that inserts a parameter shifts every index and changes nothing about a name. `System.Enum`'s `enumType` must be an enum; the `attributeType` of `Attribute`, `MemberInfo`, `ICustomAttributeProvider` and `CustomAttributeExtensions` must derive from `System.Attribute`; `Delegate.CreateDelegate`'s `type` must derive from `System.Delegate`; and `Activator.CreateInstance`'s `type` must not be an interface, an abstract class or a static class. ⚠ **The argument must be a written `typeof(…)`.** A `Type` arriving in a variable is whatever a caller put there and is invisible from here — the same reason `SK5001` refuses to treat a parameter as a source. ⚠ **An unresolved type, a type parameter and `dynamic` are all declined**, because the operand's kind is then not a fact: `typeof(T)` inside a generic method names a different type at every instantiation, and a `T` constrained `struct, Enum` is an enum at every one of them. ⚠ **`typeof(Attribute)` itself is not reported**, because `GetCustomAttribute(member, typeof(Attribute))` means \"any attribute\" and is exactly right; the test is derives-from-or-equals, not derives-from. ⚠ **`SK1035` and this rule cannot both fire.** `SK1035` offers `Enum.GetValues<T>()` and requires the argument to *be* an enum, since the generic overload is constrained `struct, Enum`; this rule requires it not to be. ⚠ `hasFix: false`. Which type was meant is the only thing that would repair the line and it is not written anywhere in the file — `SK5001` carries the same disposition for the same reason.",
        "configuration": [
            "dotnet_diagnostic.SK2233.severity"
        ]
    }
]


def main() -> None:
    text = PATH.read_text(encoding="utf-8")
    tail = "\n  ]\n}\n"
    assert text.endswith(tail), "rules.json does not end the way this script assumes"

    rendered = []
    for entry in ENTRIES:
        body = json.dumps(entry, indent=2, ensure_ascii=False)
        rendered.append("\n".join("    " + line for line in body.splitlines()))

    merged = text[: -len(tail)] + ",\n" + ",\n".join(rendered) + tail
    json.loads(merged)  # refuse to write anything that is not valid JSON
    PATH.write_text(merged, encoding="utf-8")
    print("appended", ", ".join(e["id"] for e in ENTRIES))


if __name__ == "__main__":
    main()
