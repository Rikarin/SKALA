class Candidate {
    public int start;
    public int end;
}

class C {
    bool M(Candidate candidate) => candidate.start == candidate.end;

    bool N(int a, int b) => a > b && b > 0;
}
