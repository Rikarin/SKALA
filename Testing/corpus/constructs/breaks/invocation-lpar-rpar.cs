class InvocationParentheses {
    void M() {
        Call(
            firstArgument,
            secondArgument
        );

        Call(
            firstArgument,
            secondArgument
        );

        Call(firstArgument, secondArgument);
    }
}
