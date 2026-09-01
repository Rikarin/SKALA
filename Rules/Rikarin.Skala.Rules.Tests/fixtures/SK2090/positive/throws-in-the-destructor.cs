using System;

sealed class Direct {
    ~Direct() {
        throw new InvalidOperationException("the buffer was never released");
    }
}

sealed class Rethrows {
    ~Rethrows() {
        try {
            Release();
        } catch (InvalidOperationException) {
            throw;
        }
    }

    static void Release() { }
}

sealed class InsideAFinally {
    ~InsideAFinally() {
        try {
            Release();
        } finally {
            throw new InvalidOperationException("cleanup failed");
        }
    }

    static void Release() { }
}

sealed class ThrowExpression {
    string? _name;

    ~ThrowExpression() {
        Record(_name ?? throw new InvalidOperationException("no name"));
    }

    static void Record(string name) { }
}
