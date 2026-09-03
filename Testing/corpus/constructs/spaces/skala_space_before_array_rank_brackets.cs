class C {
    int[] a;
    int[,] b;
    int[][] c;

    int[] M() => new int[4];
    int[,] N() => new int[2, 2];
    int[] O() => new int[] { 1 };

    void P(int[] x) {
        int[] y = x;
    }
}
