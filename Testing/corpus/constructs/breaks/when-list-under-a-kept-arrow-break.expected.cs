// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// #564: under a break the author kept after an arm's arrow, a `when` clause's argument list nests from the arm's continuation line.

class C564 {
    int A(object owner) =>
        owner switch {
            ArgumentListSyntax a when Compute(
                a,
                owner,
                owner,
                owner,
                owner,
                owner,
                owner,
                owner,
                owner,
                owner,
                owner,
                owner
            ) => SomeVeryLongMethodNameThatIsLong(owner, owner, owner, owner),
            _ => 0
        };

    int B(object owner) =>
        owner switch {
            ArgumentListSyntax a when Compute(
                    a,
                    owner,
                    owner,
                    owner,
                    owner,
                    owner,
                    owner,
                    owner,
                    owner,
                    owner,
                    owner,
                    owner
                ) =>
                1,
            _ => 0
        };

    int C(object owner) =>
        owner switch {
            ArgumentListSyntax a when Compute(a, owner) =>
                1,
            _ => 0
        };

    int E(object owner) =>
        owner switch {
            ArgumentListSyntax a when Compute(
                a,
                owner,
                owner,
                owner,
                owner,
                owner,
                owner,
                owner,
                owner,
                owner,
                owner,
                owner
            ) => "a string literal that is long enough to overflow",
            _ => 0
        };

    int F(object owner) =>
        owner switch {
            ArgumentListSyntax a when Compute(
                    a,
                    owner
                ) =>
                1,
            _ => 0
        };

    int G(object owner) =>
        owner switch {
            ArgumentListSyntax a when Compute(
                    a,
                    owner
                ) =>
                1,
            _ => 0
        };
}
