using System;

// ⚠ The case the rule declines, and it declines by construction rather than by an exemption
// somebody has to maintain. Nothing in the tree can say whether this compilation is an application
// or a library — LooseLoader builds every loose compilation as a library — so the console write of a
// console application's own entry point is left alone because there is no logger to contradict it.
public static class Program {
    public static void Main(string[] args) {
        if (args.Length == 0) {
            Console.Error.WriteLine("usage: import <path>");
            return;
        }

        Console.WriteLine($"importing {args[0]}");
    }
}
