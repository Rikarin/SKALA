using System;

[AttributeUsage(AttributeTargets.All)]
class MyAttribute : Attribute { }

[My]
class C< [My] T> {
    [My]
    int _a;

    [return: My]
    [My]
    int M([My] int a) => a;
}
