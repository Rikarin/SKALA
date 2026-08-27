class C {
    void M(int a) {
        if (a > 0) {
            goto Finish;
        }

        a++;
        Finish:
        System.Console.Write(a);
    }
}
