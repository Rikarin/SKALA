using System;

sealed class MarkerAttribute : Attribute { }

class C {
    public static void Run([MarkerAttribute] int value) { }
}
