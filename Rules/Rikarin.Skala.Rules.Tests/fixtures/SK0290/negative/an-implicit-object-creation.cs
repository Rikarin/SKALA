public static class ImplicitCreation {
    // `new(5)` is an `ImplicitObjectCreationExpression`, a different syntax kind. The type is not
    // written here at all — it comes from the target — so there is nothing redundant to remove.
    public static int? Go(int value) {
        int? wrapped = new(value);
        return wrapped;
    }
}
