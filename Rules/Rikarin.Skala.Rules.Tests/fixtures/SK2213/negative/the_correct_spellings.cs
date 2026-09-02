// The tests for presence and absence, none of which loses the match at position 0.
class C {
    bool Present(string path) => path.IndexOf(':') >= 0;

    bool AlsoPresent(string path) => path.IndexOf(':') != -1;

    bool Absent(string path) => path.IndexOf(':') == -1;

    bool AlsoAbsent(string path) => path.IndexOf(':') < 0;
}
