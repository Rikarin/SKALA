using System;

class C {
    public static int Save() {
        try {
            int Total() => 1;
            return Total();
        } catch (InvalidOperationException) {
            throw;
        }
    }
}
