using System.Runtime.CompilerServices;

public partial class Split {
    partial void Record(string message, string caller = "", int level = 0);
}

public partial class Split {
    partial void Record(string message, [CallerMemberName] string caller = "", int level = 0) { }
}
