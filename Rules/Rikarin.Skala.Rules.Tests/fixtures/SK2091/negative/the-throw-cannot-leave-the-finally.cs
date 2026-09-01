using System;

sealed class Contained {
    // The nested handler catches it, so nothing leaves the `finally` and the in-flight exception
    // survives — which is exactly the repair the rule asks for.
    public void Handled() {
        try {
            Work();
        } finally {
            try {
                throw new InvalidOperationException("swallowed on purpose");
            } catch (InvalidOperationException) {
                // The rollback is best-effort and must not replace the original failure.
            }
        }
    }

    // A delegate declared in the `finally` may never be invoked there, and proving that it is would
    // be an interprocedural analysis this rule declines to be.
    public void InALambda() {
        try {
            Work();
        } finally {
            Func<int> f = () => throw new InvalidOperationException("not thrown here");
            Register(f);
        }
    }

    public void InALocalFunction() {
        try {
            Work();
        } finally {
            Work();

            static void Fail() => throw new InvalidOperationException("never reached");
        }
    }

    static void Work() { }

    static void Register(Func<int> f) { }
}
