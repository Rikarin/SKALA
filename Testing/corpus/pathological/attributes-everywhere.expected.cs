// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
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
