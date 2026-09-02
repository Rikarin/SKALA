public class Parent {
    public virtual void Run() { }
}

public sealed class Child : Parent {
    public void Go() {
        // The same question for the `base.` half: the comment is on the line above, and the span
        // the fix deletes is the five characters `base.`.
        base.Run();
    }
}
