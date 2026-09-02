using System.IO;

// `flag ? left : right is Stream` does not mean what the call means.
class ConditionalOperand {
    public bool Test(bool flag, object left, object right) =>
        typeof(Stream).IsInstanceOfType(flag ? left : right);
}
