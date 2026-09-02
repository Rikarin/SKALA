interface IParent {
    string Log(string message);
}

interface IChild : IParent {
    string Log(object value);
}

static class Call {
    // Through `IParent` the better overload is the one that binds, which is the point.
    public static string Run(IParent parent) => parent.Log("started");
}
