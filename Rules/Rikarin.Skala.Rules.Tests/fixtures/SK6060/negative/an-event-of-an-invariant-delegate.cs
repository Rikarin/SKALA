// A delegate declared with no variance leaves the event type invariant, so nothing can be offered.
public delegate void Change<TArg>(TArg argument, TArg previous);

public interface IInvariantNotifier<T> {
    event Change<T> Changed;
}
