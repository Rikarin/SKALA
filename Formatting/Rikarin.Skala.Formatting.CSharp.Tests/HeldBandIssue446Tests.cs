namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     ⚠ #446 round 12 (SK-DIV-0211): the list limit's band of ±2/209 of a column where the rule before it decides, the
///     narrow head's integer form with a head of five, and the typed gate's corrected left line. Every expected string is
///     <c>jb cleanupcode</c>'s, read off the grid and cell each test names; each must also format to itself.
///     ⚠ Round 13 removed both of round 12's bands: these cells now pass by the exact limit and gate
///     (<see cref="HeldExactIssue446Tests" />).
/// </summary>
public sealed class HeldBandIssue446Tests {
    static string Wrap(string body) => "class C {\n    void M() {\n" + body + "    }\n}\n";

    static void Agrees(string source, string expected) {
        var once = Format.Text(Wrap(source)).ReplaceLineEndings("\n");
        Assert.Equal(Wrap(expected), once);
        Assert.Equal(once, Format.Text(once).ReplaceLineEndings("\n"));
    }

    /// <summary>g11 C1825: a typed list 1/209 of a column past the planar limit; the oracle breaks the <c>=</c>.</summary>
    [Fact]
    public void Band_g11_C1825() =>
        Agrees(
            "        Tqsswbky nonxfencdsbxjjmsojiiqrpaldxhqa = rwqva.Mkzdja(alopuwpgmu, aixcavvaqsqzbkujlgntmstyegmksihuzwqwvdvndmire);\n",
            "        Tqsswbky nonxfencdsbxjjmsojiiqrpaldxhqa =\n"
            + "            rwqva.Mkzdja(alopuwpgmu, aixcavvaqsqzbkujlgntmstyegmksihuzwqwvdvndmire);\n"
        );

    /// <summary>f3a C3965: a <c>var</c> list exactly on the planar limit; the oracle chops beside the <c>=</c>.</summary>
    [Fact]
    public void Band_f3a_C3965() =>
        Agrees(
            "        if (b) {\n            if (b) {\n"
            + "                var nskfgqwupdrwjg = rqtbijudlrlzdgvrdheeoknujauwvhipkvyohurhlqlcmciz.Mzptj(x.Ybebkxcl(zvlguk), agczpzslrbgx, akbaj);\n"
            + "            }\n        }\n",
            "        if (b) {\n            if (b) {\n"
            + "                var nskfgqwupdrwjg = rqtbijudlrlzdgvrdheeoknujauwvhipkvyohurhlqlcmciz.Mzptj(\n"
            + "                    x.Ybebkxcl(zvlguk),\n"
            + "                    agczpzslrbgx,\n"
            + "                    akbaj\n"
            + "                );\n"
            + "            }\n        }\n"
        );

    /// <summary>
    ///     g42 C8961: one column over behind a narrow head, the <c>(</c> at 94 from a value at column 23; the oracle
    ///     chops beside the <c>=</c> rather than breaking the dot.
    /// </summary>
    [Fact]
    public void OneOver_g42_C8961() =>
        Agrees(
            "        if (b) {\n"
            + "            var ndgp = riodyuypijpfjtdvvmdzyxclqzabbktwqnkfejjmahferdiwzyhglytksgqksyj.Mddzbq(x.Yzridjaifpsjo(zugdrpmc));\n"
            + "        }\n",
            "        if (b) {\n"
            + "            var ndgp = riodyuypijpfjtdvvmdzyxclqzabbktwqnkfejjmahferdiwzyhglytksgqksyj.Mddzbq(\n"
            + "                x.Yzridjaifpsjo(zugdrpmc)\n"
            + "            );\n"
            + "        }\n"
        );

    /// <summary>fg1 C928: one typed argument 2/209 of a column past the limit; the oracle breaks and chops below.</summary>
    [Fact]
    public void Band_fg1_C928() =>
        Agrees(
            "        Tgkdfnkyxiydhpwkrgs nngdpu = rztthceuvgtggtbvdathfueelflbvzih.Mfyhteqptqy(axrszeaufnhdibzzwjzfbwtybgvurjbkrlzylfhxvghbodegdeaajfgvodjpmxjbmzj);\n",
            "        Tgkdfnkyxiydhpwkrgs nngdpu =\n"
            + "            rztthceuvgtggtbvdathfueelflbvzih.Mfyhteqptqy(\n"
            + "                axrszeaufnhdibzzwjzfbwtybgvurjbkrlzylfhxvghbodegdeaajfgvodjpmxjbmzj\n"
            + "            );\n"
        );

    /// <summary>g41 C568: a head of five before its <c>=</c>, an argument of five; the oracle breaks the <c>=</c>.</summary>
    [Fact]
    public void Narrow_g41_C568() =>
        Agrees(
            "        Tk nu = rkhjeasuzpgtpmpckocnpbfypxnliikimccbkvubjrudcrkvwhtzsarcfuwaimqwkazteryrvxwjyfk.Mtwzgpunuestdybnhf(amltz);\n",
            "        Tk nu =\n"
            + "            rkhjeasuzpgtpmpckocnpbfypxnliikimccbkvubjrudcrkvwhtzsarcfuwaimqwkazteryrvxwjyfk.Mtwzgpunuestdybnhf(amltz);\n"
        );

    /// <summary>g41 C569: the same, an argument of six; the oracle chops beside the <c>=</c>.</summary>
    [Fact]
    public void Narrow_g41_C569() =>
        Agrees(
            "        Tk nu = rdxmhdloyagcqtrcdlwhyclykmzzejwnrlqydottfwdssbojsmdnjwrcsgwicmirkewjmwmulxzojju.Mxvdorwjkunuscgzks(abxmji);\n",
            "        Tk nu = rdxmhdloyagcqtrcdlwhyclykmzzejwnrlqydottfwdssbojsmdnjwrcsgwicmirkewjmwmulxzojju.Mxvdorwjkunuscgzks(\n"
            + "            abxmji\n"
            + "        );\n"
        );

    /// <summary>f1a C2441: a head of five, a callee of 98, an argument of six; the oracle chops beside the <c>=</c>.</summary>
    [Fact]
    public void Narrow_f1a_C2441() =>
        Agrees(
            "        Tdo n = rpbsbuswbnfzghbgkaafhxdmmnzfbjmpsabszgiewztdkvvkxynctzuckgyhenupeqjexcubjaebti.Mgecxymvzjlmbpiwsap(avcvip);\n",
            "        Tdo n = rpbsbuswbnfzghbgkaafhxdmmnzfbjmpsabszgiewztdkvvkxynctzuckgyhenupeqjexcubjaebti.Mgecxymvzjlmbpiwsap(\n"
            + "            avcvip\n"
            + "        );\n"
        );

    /// <summary>g23 C3320: an assignment head of nine at indent 16, an argument of 23; the oracle breaks the <c>=</c>.</summary>
    [Fact]
    public void Narrow_g23_C3320() =>
        Agrees(
            "        if (b) {\n            if (b) {\n"
            + "                njskjnxmj = rxznpgowiggrteettoutsiguweravjlgmbxcwytililbmkvumyaquxvwrcauwsjhgxteghuknucmajw.Mfulis(ayuuagneievuayejwhesexn);\n"
            + "            }\n        }\n",
            "        if (b) {\n            if (b) {\n"
            + "                njskjnxmj =\n"
            + "                    rxznpgowiggrteettoutsiguweravjlgmbxcwytililbmkvumyaquxvwrcauwsjhgxteghuknucmajw.Mfulis(\n"
            + "                        ayuuagneievuayejwhesexn\n"
            + "                    );\n"
            + "            }\n        }\n"
        );

    /// <summary>f1a C2011: a <c>List&lt;…&gt;</c> head at a callee of 37, the <c>(</c> on the gate's left line; breaks.</summary>
    [Fact]
    public void Gate_f1a_C2011() =>
        Agrees(
            "        if (b) {\n            if (b) {\n"
            + "                List<Tkewxmgrne> nsnyrilugg = rqrivoilympprxfrqaep.Mdeecvtetzkitnrb(x.Yeangdzh(zvvybv), x.Yngzpww(zevsxx));\n"
            + "            }\n        }\n",
            "        if (b) {\n            if (b) {\n"
            + "                List<Tkewxmgrne> nsnyrilugg =\n"
            + "                    rqrivoilympprxfrqaep.Mdeecvtetzkitnrb(x.Yeangdzh(zvvybv), x.Yngzpww(zevsxx));\n"
            + "            }\n        }\n"
        );

    /// <summary>
    ///     g43 C18482: one column over behind a narrow typed head, the <c>(</c> at 96 from a value at column 30; the
    ///     oracle breaks the dot (round 7's <c>10·paren − 3·column ≥ 871</c> chopped).
    /// </summary>
    [Fact]
    public void OneOver_g43_C18482() =>
        Agrees(
            "        if (b) {\n            if (b) {\n                if (b) {\n"
            + "                    Tvaw ng = rfgrohxamapuwsuyzlhpdpskmrklixpgfjifhfaejxtnfomkhcjuqxbawnts.Mase(atumpardacryvsbqvusyjhw);\n"
            + "                }\n            }\n        }\n",
            "        if (b) {\n            if (b) {\n                if (b) {\n"
            + "                    Tvaw ng = rfgrohxamapuwsuyzlhpdpskmrklixpgfjifhfaejxtnfomkhcjuqxbawnts\n"
            + "                        .Mase(atumpardacryvsbqvusyjhw);\n"
            + "                }\n            }\n        }\n"
        );

    /// <summary>fz1 C3150: a <c>Dictionary&lt;…&gt;</c> head over a callee of 10, a name of 23; closed, the list chops.</summary>
    [Fact]
    public void Gate_fz1_C3150() =>
        Agrees(
            "        if (b) {\n"
            + "            Dictionary<string, Tgmmvptqhvpqqininyjrwxuifqbc> nmggzvennnwpdthoidhtmlz = rbelr.Mdhe(x.Yrjasst(zzgvjb), aguwvtbgzqztimuup);\n"
            + "        }\n",
            "        if (b) {\n"
            + "            Dictionary<string, Tgmmvptqhvpqqininyjrwxuifqbc> nmggzvennnwpdthoidhtmlz = rbelr.Mdhe(\n"
            + "                x.Yrjasst(zzgvjb),\n"
            + "                aguwvtbgzqztimuup\n"
            + "            );\n"
            + "        }\n"
        );

    /// <summary>fz6 C2274: a callee of 22, a name of 16, the <c>(</c> at 95 on round 11's right gate line; breaks.</summary>
    [Fact]
    public void Gate_fz6_C2274() =>
        Agrees(
            "        if (b) {\n"
            + "            Tzcpxfddrfgafrnxktvzmpwdgvolwxwbawcurbaj nobxohsddodbdbpg = rosvdobnagdftqf.Mfmsza(azzripzbiuvmfy, ajhy, x.Yolqgwtluezoiqh(zyyblzrrup));\n"
            + "        }\n",
            "        if (b) {\n"
            + "            Tzcpxfddrfgafrnxktvzmpwdgvolwxwbawcurbaj nobxohsddodbdbpg =\n"
            + "                rosvdobnagdftqf.Mfmsza(azzripzbiuvmfy, ajhy, x.Yolqgwtluezoiqh(zyyblzrrup));\n"
            + "        }\n"
        );

    /// <summary>
    ///     fz8 C2703: an array head over a callee of 13, a name of 26, the <c>(</c> at 88 exactly on the gate's second right
    ///     line; inside the gate's band the rule before it breaks the <c>=</c>, as the oracle does.
    /// </summary>
    [Fact]
    public void GateBand_fz8_C2703() =>
        Agrees(
            "        if (b) {\n            if (b) {\n                if (b) {\n                    if (b) {\n"
            + "                        Ttjnzjmtqjagbxitro[] nqfhzyucyfszdfgpwczdbkgfso = rppf.Mybgzmep(azanwpuuoprkfgackziricm, aofmxokmu);\n"
            + "                    }\n                }\n            }\n        }\n",
            "        if (b) {\n            if (b) {\n                if (b) {\n                    if (b) {\n"
            + "                        Ttjnzjmtqjagbxitro[] nqfhzyucyfszdfgpwczdbkgfso =\n"
            + "                            rppf.Mybgzmep(azanwpuuoprkfgackziricm, aofmxokmu);\n"
            + "                    }\n                }\n            }\n        }\n"
        );
}
