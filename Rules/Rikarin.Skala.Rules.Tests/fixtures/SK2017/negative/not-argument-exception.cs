using System;

public sealed class Rejected(string paramName) : Exception {
    public string ParamName => paramName;
}

public sealed class Caller {
    // Neither of these carries the `ArgumentException` contract, so neither is naming a parameter.
    public void Write(string value) {
        if (value is null) {
            throw new Rejected("vlaue");
        }

        if (value.Length == 0) {
            throw new InvalidOperationException("vlaue");
        }
    }
}
