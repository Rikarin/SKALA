// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
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
