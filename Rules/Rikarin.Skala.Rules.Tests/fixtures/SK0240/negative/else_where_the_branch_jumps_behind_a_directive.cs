using System;

class C {
    public static void Run(bool flag) {
        if (flag) {
#if TRACE
            return;
#endif
        } else {
            Console.WriteLine("no");
        }
    }
}
