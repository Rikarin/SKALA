using System;

class C {
    // ⚠ A directive is not a comment. Deleting a clause that a `#if` half owns leaves the directive
    // pair straddling nothing, and under the other symbol set the text it guarded is gone — so a
    // directive in the clause's own trivia withdraws the finding whatever the deleted span is.
    public static void Save() {
        try {
            Run();
        } catch (InvalidOperationException e) {
            Log(e);
        }
#if DEBUG
        finally {
        }
#endif
    }

    static void Run() { }

    static void Log(Exception e) { }
}
