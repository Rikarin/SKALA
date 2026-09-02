using System;

class C {
    public static void Check(bool flag) {
        if (flag) {
            throw new InvalidOperationException();
        } else {
            Console.WriteLine("ok");
        }
    }
}
