using System.Collections.Generic;

public sealed class Registry {
    // ⚠ Taken from Serilog's MessageTemplate.GetElementsOfTypeToArray, read off the corpus sweep.
    // The obvious element name for `tokens` is `token`, and the body already declares a pattern
    // variable called `token` — so the first draft of the fix wrote `foreach (var token in tokens)`
    // around `if (token is string token)`, which is CS0136 and does not compile.
    //
    // ⚠ Neither half of the usual scoping guard sees it. `LookupSymbols` at the loop's own start
    // position cannot see a pattern variable scoped to an `if` *inside* the loop, and
    // `DeclaredElsewhereInMember` skips every node overlapping the span being moved — which is the
    // whole loop. The name has to be checked against the loop's own body as well.
    public static string[] OfType(object[] tokens) {
        var result = new List<string>(tokens.Length / 2);
        for (var i = 0; i < tokens.Length; i++) {
            if (tokens[i] is string token) {
                result.Add(token);
            }
        }

        return result.ToArray();
    }
}
