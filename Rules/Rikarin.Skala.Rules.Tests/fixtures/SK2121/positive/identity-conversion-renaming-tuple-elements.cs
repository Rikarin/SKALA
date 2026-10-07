using System.Collections.Generic;

// ⚠ #412's audit: the conversion is an identity, but the tested type names the tuple's elements
// differently. Replaced by the operand alone, `renamed[0].x` was CS1061; the fix writes the cast, which
// keeps the names the rest of the method reads.
public static class Probe {
    public static int Run() {
        List<(int a, int b)> pairs = new() { (1, 2) };
        var renamed = pairs as List<(int x, int y)>;
        return renamed![0].x;
    }
}
