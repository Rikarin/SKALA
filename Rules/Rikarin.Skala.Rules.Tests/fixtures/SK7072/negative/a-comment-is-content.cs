// A comment between the directives is left alone. The rule deletes brackets, and a bracket that
// still explains itself to a reader is not the accident this rule is about.
public sealed class Work {
    public void Run() { }
}

#pragma warning disable CS0168 // Restored below.
// The unused local that lived here moved to Work.Run; keep the bracket until the port lands.
#pragma warning restore CS0168
