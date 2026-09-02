interface IParent {
    string Log(string message);
}

interface IChild : IParent {
    string Log(object value);
}

static class Call {
    // Binds to `IChild.Log(object)`; `IParent.Log(string)` is the better match and unreachable here.
    public static string Run(IChild child) => child.Log("started");
}
