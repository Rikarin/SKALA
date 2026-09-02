public static class ArgumentPassedByName {
    static bool Accept(int? value) => value.HasValue;

    // The argument branch reads its parameter through the operation model, so a named argument at
    // the call site is matched to the parameter it names rather than to its position.
    public static bool Go(int value) => Accept(value: new int?(value));
}
