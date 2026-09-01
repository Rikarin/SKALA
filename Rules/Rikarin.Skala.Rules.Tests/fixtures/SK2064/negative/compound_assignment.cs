// `&=` and `|=` are a different shape: there is no `&&=` to suggest.
class C {
    bool M(bool ready, bool loaded) {
        var all = true;
        all &= ready;
        all |= loaded;
        return all;
    }
}
