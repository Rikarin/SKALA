// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-29
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
