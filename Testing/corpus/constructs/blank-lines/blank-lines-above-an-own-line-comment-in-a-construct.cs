// ⚠ Above an own-line comment inside a construct the keep keys do not reach, as above anything else there
// (SK-DIV-0196) — but a `//` keeps one of the author's blank lines and a `/* */` none. Not straight after an
// opening `(`, `[` or `<`, where neither keeps one. A braced list's items and the statements and members
// around them keep the cap's two above either kind. Issue #500.
class C {
    void M(int a,



        // c
        int b,

        /* c */
        int d) {
        Call(1,



            // c
            2);
        Call(1,

            /* c */
            2);
        Call(

            // c
            1);
        Call(1,
            // a


            // c
            2);
        Foo<int,



            // c
            string>();
        var e = a[1,


            // c
            2];
        var x =


            // c
            1;
        var t = (1,


            // c
            2);
        A()


            // c
            ;
        var arr = new[] {
            1,



            // c
            2,


            /* c */
            3
        };
        var obj = new P {
            X = 1,



            /* c */
            Y = 2
        };
        A();



        // c
        B();
    }
}
