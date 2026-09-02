interface IParent {
    string Log(string message);
}

interface IChild : IParent {
    string Log(object value);
}

static class Call {
    // A named argument is a decline rather than a guess: the whole rule rests on being able to say
    // the hidden overload would have taken this call.
    public static string Run(IChild child) => child.Log(value: "started");
}
