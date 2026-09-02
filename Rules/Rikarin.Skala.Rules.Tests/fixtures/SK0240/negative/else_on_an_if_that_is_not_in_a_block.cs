using System;

class C {
    public static void Run(bool[] flags) {
        foreach (var flag in flags)
            if (flag)
                return;
            else
                Console.WriteLine("no");
    }
}
