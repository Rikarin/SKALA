class Parent {
    public virtual string Log(string message) => message;
}

sealed class Child : Parent {
    public string Log(object value) => value.ToString() ?? string.Empty;
}

static class Call {
    // Only interfaces are inspected: a class hierarchy makes the same shape visible in its own
    // declarations, and the compiler warns about the hiding there.
    public static string Run(Child child) => child.Log("started");
}
