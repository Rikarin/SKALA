namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     ⚠ #446 round 11 (SK-DIV-0211, SK-DIV-0210): the list limit as one integer rule at scale 209, the typed gate in
///     integers, a gate for one argument, the narrow head's dot cap, and a break after <c>yield return</c>. Every expected
///     string is <c>jb cleanupcode</c>'s, read off the grid and cell each test names; each must also format to itself.
/// </summary>
public sealed class HeldLimitExactIssue446Tests {
    static string Wrap(string body) => "class C {\n    void M() {\n" + body + "    }\n}\n";

    static void Agrees(string source, string expected) {
        var once = Format.Text(Wrap(source)).ReplaceLineEndings("\n");
        Assert.Equal(Wrap(expected), once);
        Assert.Equal(once, Format.Text(once).ReplaceLineEndings("\n"));
    }

    /// <summary>g16 C333: the oracle's cell, indent 8, a var head.</summary>
    [Fact]
    public void Limit_g16_C333() =>
        Agrees(
            "        var nmcitrmcxvkazktm = rvcpaeilsladmyadjzjjfcahdzbuerwpgpmcs.Mblkxc(annlqhoigc, bwwszunkqtfeermszvmbalpnfyzvqinjeree);\n",
            "        var nmcitrmcxvkazktm =\n"
            + "            rvcpaeilsladmyadjzjjfcahdzbuerwpgpmcs.Mblkxc(annlqhoigc, bwwszunkqtfeermszvmbalpnfyzvqinjeree);\n"
        );

    /// <summary>g16 C334: the oracle's cell, indent 8, a var head.</summary>
    [Fact]
    public void Limit_g16_C334() =>
        Agrees(
            "        var nmcitrmcxvkazktm = rxncngmjqdyfohtueeujemcuchixhdthuwxnr.Mkvvvv(arazttzthy, brdcyalywjtdksttykfmuclpsyynkxoforged);\n",
            "        var nmcitrmcxvkazktm = rxncngmjqdyfohtueeujemcuchixhdthuwxnr.Mkvvvv(\n"
            + "            arazttzthy,\n"
            + "            brdcyalywjtdksttykfmuclpsyynkxoforged\n"
            + "        );\n"
        );

    /// <summary>g5 C532: the oracle's cell, indent 8, a var head.</summary>
    [Fact]
    public void Limit_g5_C532() =>
        Agrees(
            "        var nbrjtbjtkj = ratlbepmsuriprpttppbmucfzaspgirjstihutbqkgogcrfbcpyvodkbk.Mhffth(asjpxlwanbacnrnolcjtesjnvhsnukhdciqeabbnbosmhrdldipfquxifmjghrda);\n",
            "        var nbrjtbjtkj =\n"
            + "            ratlbepmsuriprpttppbmucfzaspgirjstihutbqkgogcrfbcpyvodkbk.Mhffth(\n"
            + "                asjpxlwanbacnrnolcjtesjnvhsnukhdciqeabbnbosmhrdldipfquxifmjghrda\n"
            + "            );\n"
        );

    /// <summary>g5 C533: the oracle's cell, indent 8, a var head.</summary>
    [Fact]
    public void Limit_g5_C533() =>
        Agrees(
            "        var nvaeclfzbf = rdkfoblhlneblszpvxqzloykczmvuhviacorsyuqgbqybpiujfosrgngo.Mjxegm(axvutyxquagmnqckmckfjvdlbwqcgrzrfrymptizyrhlzpllrobshszlhgsssvytp);\n",
            "        var nvaeclfzbf = rdkfoblhlneblszpvxqzloykczmvuhviacorsyuqgbqybpiujfosrgngo.Mjxegm(\n"
            + "            axvutyxquagmnqckmckfjvdlbwqcgrzrfrymptizyrhlzpllrobshszlhgsssvytp\n"
            + "        );\n"
        );

    /// <summary>g11 C5154: the oracle's cell, indent 8, a typed head.</summary>
    [Fact]
    public void Gate_g11_C5154() =>
        Agrees(
            "        Ttecbxwbrmqpqiayvrudzbdgprclanmmrlcpxptcnf nfr = rrayixnofpjptaxjanfnllzqzdkhfmfwv.Mfdbee(akrihhwvdw, apipbgosce);\n",
            "        Ttecbxwbrmqpqiayvrudzbdgprclanmmrlcpxptcnf nfr =\n"
            + "            rrayixnofpjptaxjanfnllzqzdkhfmfwv.Mfdbee(akrihhwvdw, apipbgosce);\n"
        );

    /// <summary>g32 C1748: the oracle's cell, indent 8, a typed head.</summary>
    [Fact]
    public void Gate_g32_C1748() =>
        Agrees(
            "        Tfnigbsvcvypsnizuoberbsjlpovhmlrczrhpupnxg nnzj = rrxkfqnbc.Majerd(antcqpbjpukuyhwmvrdvicutzvfbhtffdopgbornahcyp);\n",
            "        Tfnigbsvcvypsnizuoberbsjlpovhmlrczrhpupnxg nnzj =\n"
            + "            rrxkfqnbc.Majerd(antcqpbjpukuyhwmvrdvicutzvfbhtffdopgbornahcyp);\n"
        );

    /// <summary>g32 C1750: the oracle's cell, indent 8, a typed head.</summary>
    [Fact]
    public void Gate_g32_C1750() =>
        Agrees(
            "        Tdncgwoietvkhfeucixvmvljhofknlpioewawffqutg nqru = rbcemvebj.Mvuiiw(asdvcjofgfzegakhmtkdwkjxkintfswjbltuxdxmjucd);\n",
            "        Tdncgwoietvkhfeucixvmvljhofknlpioewawffqutg nqru = rbcemvebj.Mvuiiw(\n"
            + "            asdvcjofgfzegakhmtkdwkjxkintfswjbltuxdxmjucd\n"
            + "        );\n"
        );

    /// <summary>g32 C0: the oracle's cell, indent 8, a typed head.</summary>
    [Fact]
    public void Gate_g32_C0() =>
        Agrees(
            "        Tjifivrwjfprvpcjqimfexwbaznegs n = rmeop.Mlnied(aaijfdfsfamngugcghhuvjfksjchigzoqsyxhdnlscaywqozqcnqkinkmsrmeqei);\n",
            "        Tjifivrwjfprvpcjqimfexwbaznegs n = rmeop.Mlnied(\n"
            + "            aaijfdfsfamngugcghhuvjfksjchigzoqsyxhdnlscaywqozqcnqkinkmsrmeqei\n"
            + "        );\n"
        );

    /// <summary>g29 C2903: the oracle's cell, indent 8, a var head.</summary>
    [Fact]
    public void NarrowCap_g29_C2903() =>
        Agrees(
            "        var nluh = rhfrzklizmfphhvjpjoqkomlugukbdkgbwpsnadytcuxygpndlvcyhzigatintoybjvwghzpejfrixblmchyxahbb.Mzbqkxxkhbixiltj(acztmxwrwtlpfcsmdchoklrrfndrherjtuqwtpveeagcatpesknvdzsaoykgfjcpiozffspkjzab);\n",
            "        var nluh = rhfrzklizmfphhvjpjoqkomlugukbdkgbwpsnadytcuxygpndlvcyhzigatintoybjvwghzpejfrixblmchyxahbb\n"
            + "            .Mzbqkxxkhbixiltj(acztmxwrwtlpfcsmdchoklrrfndrherjtuqwtpveeagcatpesknvdzsaoykgfjcpiozffspkjzab);\n"
        );

    /// <summary>g29 C2904: the oracle's cell, indent 8, a var head.</summary>
    [Fact]
    public void NarrowCap_g29_C2904() =>
        Agrees(
            "        var nluh = rdmcndqspdnceqfztfprkckfndkomemsdyocnjnhuarvahsoiyjumxfhfuhxtnpljdralcqosqlklynptqbctlvcc.Mhqskyduzcldthbm(augdphdbdpdzqfuknlveceyatocrluiptthxkfrjzhdzuphzitjhznsvuakekcqcpiiqreckagkvc);\n",
            "        var nluh =\n"
            + "            rdmcndqspdnceqfztfprkckfndkomemsdyocnjnhuarvahsoiyjumxfhfuhxtnpljdralcqosqlklynptqbctlvcc.Mhqskyduzcldthbm(\n"
            + "                augdphdbdpdzqfuknlveceyatocrluiptthxkfrjzhdzuphzitjhznsvuakekcqcpiiqreckagkvc\n"
            + "            );\n"
        );

    /// <summary>g29 C1273: the oracle's cell, indent 8, a var head.</summary>
    [Fact]
    public void NarrowCap_g29_C1273() =>
        Agrees(
            "        var np = rkzxunxfoxkkzfpercgekyfcqebzyeaitxspdbspwwlwjbmfaozlkwjerdmzftwpdplarlnxfoshxbbundxwwntcpjefigthglrci.Meuo(asclldligiilhndhhdjlcsyqeftjqdonvqihyrhtcvqsiexihmhrhszfqquhjvgvxmbteqkdpwvofhcfvmjliedryacod);\n",
            "        var np =\n"
            + "            rkzxunxfoxkkzfpercgekyfcqebzyeaitxspdbspwwlwjbmfaozlkwjerdmzftwpdplarlnxfoshxbbundxwwntcpjefigthglrci.Meuo(\n"
            + "                asclldligiilhndhhdjlcsyqeftjqdonvqihyrhtcvqsiexihmhrhszfqquhjvgvxmbteqkdpwvofhcfvmjliedryacod\n"
            + "            );\n"
        );

    /// <summary>g40 C385: the oracle's cell, indent 8, a yield head.</summary>
    [Fact]
    public void YieldReturn_g40_C385() =>
        Agrees(
            "        yield return rrwyqseaa.Pcyfovhbsmqfgaqassblccepabkjhzujgsqkjpysgwiafuekqqvxueuivgvutzyrmqyhqngynjtebwvmg as Tyoz;\n",
            "        yield return\n"
            + "            rrwyqseaa.Pcyfovhbsmqfgaqassblccepabkjhzujgsqkjpysgwiafuekqqvxueuivgvutzyrmqyhqngynjtebwvmg as Tyoz;\n"
        );

    /// <summary>g40 C388: the oracle's cell, indent 8, a yield head.</summary>
    [Fact]
    public void YieldReturn_g40_C388() =>
        Agrees(
            "        yield return ribzcmupwt.Ptxydaahvonkfxpyogfmqpzhduwssdyyxebylqopwqqpqrfufcnpluoecnezworrhjkjdacuqkpdryss as Tcan;\n",
            "        yield return ribzcmupwt\n"
            + "            .Ptxydaahvonkfxpyogfmqpzhduwssdyyxebylqopwqqpqrfufcnpluoecnezworrhjkjdacuqkpdryss as Tcan;\n"
        );

    /// <summary>g40 C1249: the oracle's cell, indent 8, a yield head.</summary>
    [Fact]
    public void YieldReturn_g40_C1249() =>
        Agrees(
            "        yield return r.Pmbffnsxnjpjppyjgzihtvydbeqwxjortbogfxqzalvegijlenchojobjmvmwbvaawgrxubmgioedgwamurwwgbegfd is Td;\n",
            "        yield return r\n"
            + "            .Pmbffnsxnjpjppyjgzihtvydbeqwxjortbogfxqzalvegijlenchojobjmvmwbvaawgrxubmgioedgwamurwwgbegfd is Td;\n"
        );

    /// <summary>g40 C2086: the oracle's cell, indent 8, a yield head.</summary>
    [Fact]
    public void YieldReturn_g40_C2086() =>
        Agrees(
            "        yield return ryqwquugnnkvvyhnqlbehfgfoewsoenbaztsdrio.Pulkatwelsmgqgcqxhiotoaekussnymjaslanainezenvf is Tnxhtfaf;\n",
            "        yield return\n"
            + "            ryqwquugnnkvvyhnqlbehfgfoewsoenbaztsdrio.Pulkatwelsmgqgcqxhiotoaekussnymjaslanainezenvf is Tnxhtfaf;\n"
        );
}
