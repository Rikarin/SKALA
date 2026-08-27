class LambdaParens {
    void M() {
        Use((
                int first
            ) => first
        );
        Use((
                int first,
                int second
            ) => first
        );
        Use(delegate(
                int first
            ) { return first; }
        );
    }
}
