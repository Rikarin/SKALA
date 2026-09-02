// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
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
