using System.Diagnostics;

// ⚠ The exclusion that matters most. A dotted path needs the first member's type to answer, and a
// path rooted at a namespace has a root that is not a member of anything.
[DebuggerDisplay("{Owner.Name} at {System.DateTime.Now}")]
sealed class Basket {
    public string Name => "basket";
}
