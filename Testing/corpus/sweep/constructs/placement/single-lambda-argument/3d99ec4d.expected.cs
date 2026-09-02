// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
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
