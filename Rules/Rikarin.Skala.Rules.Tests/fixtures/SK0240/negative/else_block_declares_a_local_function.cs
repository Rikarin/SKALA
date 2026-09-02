using System;

class C {
    public static void Run(bool flag) {
        if (flag) {
            return;
        } else {
            void Say() => Console.WriteLine("no");

            Say();
        }
    }
}
