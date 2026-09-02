using System;

// ⚠ Both shapes are on one `try`, and they are answered by one finding carrying one edit. Reported
// separately their two deletions compose into `try { Run(); }`, which is CS1524; reported one per
// pass, the fix's own output still carries the other finding. The clause that would survive is
// nothing, so the edit is the unwrap.
class C {
    public static void Save() {
        try {
            Run();
        } catch (InvalidOperationException) {
            throw;
        } finally {
        }
    }

    static void Run() { }
}
