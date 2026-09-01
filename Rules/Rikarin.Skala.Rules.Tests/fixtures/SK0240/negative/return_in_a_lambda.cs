using System;

class C {
    public static Action Run() =>
        () => {
            Use(1);
            return;
        };

    static void Use(int value) { }
}
