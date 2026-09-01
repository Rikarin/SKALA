using System;

public sealed class Parser {
    public void Parse(string input) {
        if (input.Length == 0) {
            // `paramName` is the *second* argument here, which is most of why it goes wrong.
            throw new ArgumentException("must not be empty", "inptu");
        }
    }
}
