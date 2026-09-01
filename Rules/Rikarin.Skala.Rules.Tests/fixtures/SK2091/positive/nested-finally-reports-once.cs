using System;

sealed class Nested {
    // One `throw`, one finding: the innermost `finally` owns the keyword its author wrote. The
    // outer clause sees the same node and must not report it a second time.
    public void TwoDeep() {
        try {
            Work();
        } finally {
            try {
                Work();
            } finally {
                throw new InvalidOperationException("the inner cleanup refused");
            }
        }
    }

    public void ThrowExpression(string? name) {
        try {
            Work();
        } finally {
            Record(name ?? throw new InvalidOperationException("no name to record"));
        }
    }

    static void Work() { }

    static void Record(string name) { }
}
