using System;
using System.IO;

// An exception type that never took a cause. The advice would be to redesign a public surface the
// handler's author may not own, so there is no finding.
sealed class Unchainable : Exception {
    public Unchainable(string message) : base(message) { }
}

sealed class Chainable : Exception {
    public Chainable(string message) : base(message) { }

    public Chainable(string message, Exception inner) : base(message, inner) { }
}

sealed class Silent {
    public void NoChainingConstructor(string path) {
        try {
            File.ReadAllText(path);
        } catch (IOException error) {
            throw new Unchainable("the import failed");
        }
    }

    // ⚠ No variable is bound, so nothing was discarded that the author had named. A translation
    // where the caught type was the whole of the information is deliberately out of scope.
    public void NoVariable(string path) {
        try {
            File.ReadAllText(path);
        } catch (FileNotFoundException) {
            throw new Chainable("the configuration file is missing");
        }
    }

    // The nested handler catches it, so this `throw` never leaves the clause.
    public void CaughtAgain(string path) {
        try {
            File.ReadAllText(path);
        } catch (IOException error) {
            try {
                throw new Chainable("retrying");
            } catch (Chainable) {
                Console.WriteLine(error);
            }
        }
    }

    // A delegate may never be invoked here.
    public void InALambda(string path) {
        try {
            File.ReadAllText(path);
        } catch (IOException error) {
            Func<int> f = () => throw new Chainable("not thrown here");
            Console.WriteLine(error);
            Register(f);
        }
    }

    static void Register(Func<int> f) { }
}
