interface IParent {
    string Write(string name, string value);
}

interface IChild : IParent {
    string Write(object name, object value);
}

static class Call {
    public static string Run(IChild child) => child.Write("key", "value");
}
