using System;

class C {
    public static void Run(bool flag) {
        if (flag) {
            return;
        } else /* deliberate */ {
            Console.WriteLine("no");
        }
    }
}
