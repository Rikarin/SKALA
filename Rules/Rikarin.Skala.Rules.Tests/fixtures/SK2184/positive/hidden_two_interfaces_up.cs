interface IRoot {
    string Send(string payload);
}

interface IMiddle : IRoot { }

interface ILeaf : IMiddle {
    string Send(object payload);
}

static class Call {
    public static string Run(ILeaf leaf) => leaf.Send("body");
}
