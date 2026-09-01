using System;

// The finalizer handles its own failure, which is the repair the rule is asking for.
sealed class Handled {
    ~Handled() {
        try {
            Release();
        } catch (InvalidOperationException) {
            // Nothing on the finalizer thread can act on this.
        }
    }

    static void Release() => throw new InvalidOperationException();
}

// A throw a delegate holds is not on the finalizer's path: the delegate may never be invoked, and
// proving that it is would be an interprocedural analysis this rule declines to be.
sealed class InALambda {
    ~InALambda() {
        Func<int> f = () => throw new InvalidOperationException("not called here");
        Register(f);
    }

    static void Register(Func<int> f) { }
}

sealed class InALocalFunction {
    ~InALocalFunction() {
        Report();

        static void Fail() => throw new InvalidOperationException("never reached");
    }

    static void Report() { }
}

// The nested `catch` swallows it before it can reach the destructor's exit.
sealed class NestedButHandled {
    ~NestedButHandled() {
        try {
            try {
                throw new InvalidOperationException("inner");
            } catch (InvalidOperationException) {
            }
        } finally {
            Release();
        }
    }

    static void Release() { }
}
