interface IParent {
    string Log(object value);
}

interface IChild : IParent {
    string Log(int count);
}

static class Call {
    // The bound overload really is the better match: `object` does not convert to `int`.
    public static string Run(IChild child) => child.Log(1);
}
