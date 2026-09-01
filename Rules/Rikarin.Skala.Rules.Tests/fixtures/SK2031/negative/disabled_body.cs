// Statements the preprocessor disabled are not in the body, so this reads as the empty exemption
// rather than as a setter that does work and ignores the write.
class C {
    int retries;

    public int Retries {
        get => retries;
        set {
#if TRACING
            retries = 0;
#endif
        }
    }
}
