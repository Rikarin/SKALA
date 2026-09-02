interface IParent {
    string Log(string message);
}

interface IChild : IParent {
    new string Log(string message);
}

static class Call {
    // An identical signature is `CS0108`, and the compiler already asks for the `new` that is here.
    public static string Run(IChild child) => child.Log("started");
}
