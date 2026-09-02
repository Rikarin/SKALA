interface IParent {
    string Log(int count);
}

interface IChild : IParent {
    string Log(object value);
}

static class Call {
    // `IParent.Log(int)` is hidden but a `string` does not convert to `int`, so it would not have
    // taken this call either.
    public static string Run(IChild child) => child.Log("started");
}
