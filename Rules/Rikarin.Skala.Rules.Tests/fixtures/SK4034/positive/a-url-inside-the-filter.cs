// ⚠ The span overload was a raw TEXT scan — `IndexOf("//")`, `IndexOf("/*")`, `IndexOf('#')` over
// the span's characters — so it could not tell a comment from a string literal that contains one.
// `"https://example.com"` inside the filter answered "a person wrote a comment here", and SK4034
// returned without reporting.
//
// ⚠ So #302 traded one silence for another. The node overload was dead on documented code; the
// span overload it moved ten call sites onto is dead on code holding a URL, a `#` fragment or a
// `/*` in a literal — and that failure has the same shape: the negatives all still pass, because a
// rule that never fires declines everything it is supposed to decline. The guard is now a trivia
// walk restricted to the span, which asks about comments and not about characters.
using System.Collections.Generic;
using System.Linq;

public sealed class Feed {
    public static IEnumerable<string> Live(List<string> entries) =>
        entries.OrderBy(entry => entry).Where(entry => entry != "https://example.com");
}
