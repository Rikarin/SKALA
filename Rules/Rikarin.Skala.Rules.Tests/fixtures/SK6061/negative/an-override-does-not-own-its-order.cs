using System.Runtime.CompilerServices;

// The base declares no caller-info attribute, so it is not a candidate. The override adds one and
// is the only thing in this file the rule could report — and must not, because moving a parameter
// there produces a member that no longer overrides.
public abstract class Base {
    public abstract void Log(string message, string caller = "", int level = 0);
}

public sealed class Derived : Base {
    public override void Log(string message, [CallerMemberName] string caller = "", int level = 0) { }
}
