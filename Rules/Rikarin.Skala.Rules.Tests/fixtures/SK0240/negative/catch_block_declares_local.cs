using System;

class C {
    public static int Save() {
        try {
            var total = Count();
            return total + 1;
        } catch (InvalidOperationException) {
            throw;
        }
    }

    static int Count() => 0;
}
