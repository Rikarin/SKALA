// ⚠ Wrong in spacing everywhere, and wrong in indentation and in wrapping as well.
//
// `disable_space_changes` preserves every inter-token run byte for byte — the two-space runs around
// the `+` included, which is the case a one-bit "was there a space" reading gets wrong — while the
// file is still reindented and the `if` body is still broken onto a line of its own.
class C {
    public int Alpha ;
    public void Method( int one,int two ) {
            var sum=one  +  two;
        if(sum>0){ Alpha=sum; }    // note
    }
}
