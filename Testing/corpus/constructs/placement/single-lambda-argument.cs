class SingleLambdaArgument {
    void M() {
        Run(() => Body());

        Run(() => {
                FirstStatement();
                SecondStatement();
            }
        );

        Run(
            state,
            () => {
                FirstStatement();
                SecondStatement();
            }
        );
    }
}
