using System;

public sealed class Copier {
    public void Copy(string source) {
        if (source is null) {
            throw new ArgumentNullException(paramName: "sorce");
        }
    }
}
