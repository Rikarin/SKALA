using System;

class C {
    public static void Run(bool flag) {
        if (flag) {
            return;
#if TRACE
            Console.WriteLine("still running");
#endif
        } else {
            Console.WriteLine("no");
        }
    }
}
