// ⚠ The exclusion that is easiest to get wrong. Inside a generic method the conversion is
// classified against the constraint, not against the type the method is instantiated with, so a
// conversion that reads as certain here is not certain at run time.
class Base { }

sealed class Consumer {
    public Base? Widen<T>(T value) where T : Base => value as Base;

    public object? Box<T>(T value) => value as object;

    public T? Narrow<T>(object value) where T : class => value as T;
}
