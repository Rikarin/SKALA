namespace Contoso.Design;

// A visitor base whose every method is an empty `virtual` is the canonical extension point. Nothing
// is required of a derived type and everything is offered to it.
public abstract class Visitor {
    public virtual void VisitOpen(int node) { }

    public virtual void VisitClose(int node) { }
}
