// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
class CaseStatements {
    void M(int v) {
        switch (v) {
            case 1: OnTheSameLine(); break;
            case 2:
                OnItsOwnLine();
                break;
            case 3: {
                InABlock();
                break;
            }
        }
    }
}
