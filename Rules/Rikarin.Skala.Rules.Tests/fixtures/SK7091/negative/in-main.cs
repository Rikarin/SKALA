using System;

// ⚠ The line the rule draws. Ending the process is the process's own decision here, and there is
// nothing above this frame to unwind into.
public static class Program {
    public static void Main(string[] args) {
        if (args.Length == 0) {
            Environment.Exit(64);
        }
    }
}
