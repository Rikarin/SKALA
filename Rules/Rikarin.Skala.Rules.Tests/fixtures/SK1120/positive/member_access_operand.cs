using System.IO;

class MemberOperand {
    readonly object source = new();

    public bool Test() => typeof(Stream).IsInstanceOfType(this.source);
}
