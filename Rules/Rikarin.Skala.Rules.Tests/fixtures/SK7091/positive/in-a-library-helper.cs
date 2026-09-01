using System;

public sealed class ConfigurationLoader {
    public void Require(string? path) {
        if (path is null) {
            Environment.Exit(1);
        }
    }
}
