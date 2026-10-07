// ⚠ CS8126 forbids naming an element `ToString`, `Equals` or `GetHashCode`, but not `GetType`. The
// element's field hides `object.GetType` in member lookup, so `t.GetType` binds to the element — which the
// rule proves by binding the renamed access, rather than assuming from the compiler's list.
public static class Hidden {
    public static int Kind((int GetType, string Label) t) => t.Item1;
}
