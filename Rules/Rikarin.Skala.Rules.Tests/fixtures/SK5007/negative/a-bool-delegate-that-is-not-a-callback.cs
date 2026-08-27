using System;

// Returns `true` unconditionally and has nothing to do with TLS. The rule is keyed on the delegate
// carrying an `SslPolicyErrors`, so an ordinary predicate is outside it.
public static class Filters {
    public static Func<string, bool> AcceptEverything() => _ => true;
}
