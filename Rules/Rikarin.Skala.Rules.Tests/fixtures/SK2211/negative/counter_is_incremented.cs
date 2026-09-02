// The loop that works.
class C {
    void M(int count) {
        var i = 0;
        while (i < count) {
            System.Console.WriteLine(i);
            i++;
        }
    }

    void F(int count) {
        for (var i = 0; i < count; i++) {
            System.Console.WriteLine(i);
        }
    }
}
