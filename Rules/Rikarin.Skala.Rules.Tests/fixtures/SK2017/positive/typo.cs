using System;

public sealed class Writer {
    public void Write(string value) {
        if (value is null) {
            // The message, the `ParamName` and the log line all name a parameter that does not exist.
            throw new ArgumentNullException("vlaue");
        }
    }
}
