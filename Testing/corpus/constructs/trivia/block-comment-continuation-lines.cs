// Issue #428, SK-DIV-0094 and SK-DIV-0193. A multi-line block comment moves as a unit with the line
// its first line is on, clamped at column 0, and every line of it loses its trailing whitespace. The
// one exception is a starred /* */, which skala_align_multiline_comments aligns instead.
class Plain {
      /*
         moved left two
       */
    void A() { }

  /*
     moved right two
   */
    void B() { }

            /*
  left of the shift
       still left of it

                kept apart
            */
    void D() { }
}

class Doc {
      /**
       * doc starred
       */
    void A() { }

      /**
         * ragged doc
     * still ragged
       */
    void B() { }

      /*
         * ragged plain
     * aligned instead
       */
    void D() { }

      /** doc plain
            second
       */
    void E() { }
}

class Line {
    void A(int a,
              /* c
                 d */ int b) { }

    void B() {
          M(); /* trailing
                  more */
          var x = 1 + /* expr
                       expr2 */ 2;
        var q = new int[] { 1, /* c
                                  d */ 2 };
        if (true)
        { /* brace
             x */
            M();
        }
    }

    void M() { }
}

class Trim {
    /* first   
       second   
    
       */
    int F;

      /* moved   
         second	 
      */
    int G;

    /**
     * doc   
     *   
     */
    int H;

    /*
   * One.
   
        * Two.
     */
    int I;
}
