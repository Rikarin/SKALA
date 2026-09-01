using System;

// The enclosing-method walk is by symbol, so a local function is judged by the method that holds it.
public sealed class Validator {
    public void Validate(int value) {
        Fail();
        return;

        void Fail() {
            if (value < 0) {
                Environment.Exit(3);
            }
        }
    }
}
