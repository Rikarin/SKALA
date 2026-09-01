using System;

sealed class Importer {
    bool _committed;

    public void Simple() {
        try {
            Import();
        } finally {
            throw new InvalidOperationException("the import did not commit");
        }
    }

    public void Conditional() {
        try {
            Import();
        } finally {
            if (!_committed) {
                throw new InvalidOperationException("the import did not commit");
            }
        }
    }

    // The `finally` of a `try`/`catch`/`finally` replaces whatever the `catch` let through.
    public void AfterACatch() {
        try {
            Import();
        } catch (FormatException) {
            _committed = false;
        } finally {
            throw new InvalidOperationException("cleanup refused");
        }
    }

    // A `throw` in a nested handler inside the `finally` still leaves the `finally`.
    public void FromANestedCatch() {
        try {
            Import();
        } finally {
            try {
                Rollback();
            } catch (FormatException error) {
                throw new InvalidOperationException("the rollback failed", error);
            }
        }
    }

    static void Import() { }

    static void Rollback() { }
}
