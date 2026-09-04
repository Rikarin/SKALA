// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
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
