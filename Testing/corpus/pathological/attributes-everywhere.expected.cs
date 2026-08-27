// skala-oracle: resharper=2025.2.6 config=sha256:98ff52570e019fac profile=SkalaFormatOnly generated=2026-08-26
using System;

[AttributeUsage(AttributeTargets.All)]
class MyAttribute : Attribute { }

[My]
class C<[My] T> {
    [My]
    int _a;

    [return: My]
    [My]
    int M([My] int a) => a;
}
