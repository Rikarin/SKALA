interface ILeft {
    string Take(int n);
}

interface IRight {
    string Take(string s);
}

interface IBoth : ILeft, IRight { }

static class Call {
    // Neither declares the other's member, so nothing is hidden: ordinary overload resolution over
    // the union picks `ILeft.Take(int)` and it is the only applicable candidate.
    public static string Run(IBoth both) => both.Take(1);
}
