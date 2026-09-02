interface IParent {
    string Read();
}

interface IChild : IParent {
    string Log(object value);
}

static class Call {
    public static string Run(IChild child) => child.Log("started");
}
