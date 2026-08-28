// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
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
