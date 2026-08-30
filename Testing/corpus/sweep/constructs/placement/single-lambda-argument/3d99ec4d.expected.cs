// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-30
class SingleLambdaArgument {
    void M() {
        Run(() => Body());

        Run(
            () => {
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
