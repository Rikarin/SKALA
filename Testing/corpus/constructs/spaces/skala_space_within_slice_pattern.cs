class C {
    bool M(int[] xs) => xs is [1, .. var rest, 3] && rest.Length > 0;
}
