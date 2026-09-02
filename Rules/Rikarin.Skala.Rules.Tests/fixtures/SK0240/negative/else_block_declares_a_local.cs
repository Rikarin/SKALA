using System;

class C {
    public static void Run(bool flag) {
        if (flag) {
            return;
        } else {
            var message = "no";
            Console.WriteLine(message);
        }
    }
}
