// skala-oracle: resharper=2025.2.6 config=sha256:bd9791d3a6e6a087 profile=SkalaFormatOnly generated=2026-08-27
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
        Use(
            delegate(
                int first
            ) {
                return first;
            }
        );
    }
}
