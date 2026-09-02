public static class NamedArgument {
    // `Nullable<T>`'s constructor parameter is called `value`, so the wrapper's own argument can be
    // written by name. The shape guard asks for a bare single argument and refuses this one: the
    // head span runs from `new` to the operand, and `value:` is inside it.
    public static int? Go(int value) {
        int? wrapped = new int?(value: value);
        return wrapped;
    }
}
