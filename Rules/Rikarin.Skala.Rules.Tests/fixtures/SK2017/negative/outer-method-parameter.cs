using System;

public sealed class Nesting {
    // ⚠ A local function or a lambda that validates the argument of the method enclosing it names
    // *that* method's parameter, and it is right to. This is the rule's largest false-positive
    // class, so the names in scope are the C# ones and not "the enclosing method's".
    public void Load(string source) {
        Validate();
        Run(() => {
            if (source.Length == 0) {
                throw new ArgumentException("must not be empty", "source");
            }
        });

        void Validate() {
            if (source is null) {
                throw new ArgumentNullException("source");
            }
        }
    }

    static void Run(Action action) => action();
}
