class C {
    int? M(C? c) => c?.M(c)?.GetHashCode();
}
