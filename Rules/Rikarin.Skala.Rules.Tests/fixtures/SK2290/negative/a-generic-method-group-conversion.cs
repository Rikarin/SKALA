using System;

// ⚠ The arm an identifier-only sweep does not reach. A generic method's method-group conversion
// spells `TryFirst<int>`, which is a GenericNameSyntax — the direct call two lines down spells
// `TryFirst`, an IdentifierNameSyntax in callee position, and is correctly not counted as a
// reference. Watching only IdentifierName would leave this delegate invisible.
delegate bool Attempt(int[] items, out int first);

class Reader {
    static bool TryFirst<T>(T[] items, out T first) {
        if (items.Length == 0) {
            first = default!;
            return false;
        }

        first = items[0];
        return true;
    }

    public void Run(int[] items) {
        Attempt attempt = TryFirst<int>;
        if (attempt(items, out var head)) {
            Console.WriteLine(head);
        }

        if (TryFirst(items, out _)) {
            Console.WriteLine("direct");
        }
    }
}
