// A `dynamic` operand has no static type to classify from; whatever the binder decides at run time
// is not visible here.
class Base { }

sealed class Consumer {
    public Base? Widen(dynamic value) => value as Base;

    public object? ToObject(dynamic value) => value as object;
}
