using System;

class C {
    public static void Run(bool flag, bool other) {
        if (flag) {
            if (other) {
                return;
            }
        } else {
            Console.WriteLine("no");
        }
    }
}
