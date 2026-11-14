namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     ⚠ #446 round 13 (SK-DIV-0210, SK-DIV-0211): the list limit exact at scale 1 140 and the typed gate's two V-shaped
///     lines, where round 12 deferred a band of each to the rule before it; pass two of a one-argument chop behind a
///     <c>var</c> or an assignment; <c>is N.T</c> under the <c>x</c> layout; a narrow head's <c>(</c> one column over;
///     the type/name gap one column over; and the name's own line after the gap breaks. Every expected string is
///     <c>jb cleanupcode</c>'s, read off the grid and cell each test names; each must also format to itself.
/// </summary>
public sealed class HeldExactIssue446Tests {
    static string Wrap(string body) => "class C {\n    void M() {\n" + body + "    }\n}\n";

    static void Agrees(string source, string expected) {
        var once = Format.Text(Wrap(source)).ReplaceLineEndings("\n");
        Assert.Equal(Wrap(expected), once);
        Assert.Equal(once, Format.Text(once).ReplaceLineEndings("\n"));
    }

    /// <summary>
    ///     b1 C330: one argument behind a <c>var</c>, inside the exact limit; the oracle breaks the <c>=</c>.
    /// </summary>
    [Fact]
    public void Limit_b1_C330() =>
        Agrees(
            "        var njymziqppwvhvlmdwikmjxtjeogxqyktf = rerpwvv.Mga(azyswwccklaxbvrvymwzwmyftvlaukcleonciuznztiymzuqhgkpboozyfjdzlprfahftwaajvrgks);\n",
            "        var njymziqppwvhvlmdwikmjxtjeogxqyktf =\n"
            + "            rerpwvv.Mga(azyswwccklaxbvrvymwzwmyftvlaukcleonciuznztiymzuqhgkpboozyfjdzlprfahftwaajvrgks);\n"
        );

    /// <summary>b1 C337: the same a column wider; the oracle chops, and pass two keeps the chop.</summary>
    [Fact]
    public void Limit_b1_C337() =>
        Agrees(
            "        var njymziqppwvhvlmdwikmjxtjeogxqyktf = rerpwvv.Mga(anuhbhonkfahnpeuvxwgnaehkwfvrhffslokvhfyurczdqtnimuiplnxwhoamkcrxpgyaphdzxoswef);\n",
            "        var njymziqppwvhvlmdwikmjxtjeogxqyktf = rerpwvv.Mga(\n"
            + "            anuhbhonkfahnpeuvxwgnaehkwfvrhffslokvhfyurczdqtnimuiplnxwhoamkcrxpgyaphdzxoswef\n"
            + "        );\n"
        );

    /// <summary>
    ///     b1 C14058: one argument behind an assignment, 9/209 of a column past round 11's limit; the oracle breaks.
    /// </summary>
    [Fact]
    public void Limit_b1_C14058() =>
        Agrees(
            "        nuqmkawpeichnqaptwqsrxnpectqoulfionw = rmxtsagfhhbdf.Msg(axenmghrtanezedgtanzwhnhuqbfdzshxxhgrhdfqrmtwgpaidgzbuiomzvggxqiezlyutspmgs);\n",
            "        nuqmkawpeichnqaptwqsrxnpectqoulfionw =\n"
            + "            rmxtsagfhhbdf.Msg(axenmghrtanezedgtanzwhnhuqbfdzshxxhgrhdfqrmtwgpaidgzbuiomzvggxqiezlyutspmgs);\n"
        );

    /// <summary>b1 C14065: the same a column wider; the oracle chops beside the <c>=</c>.</summary>
    [Fact]
    public void Limit_b1_C14065() =>
        Agrees(
            "        nuqmkawpeichnqaptwqsrxnpectqoulfionw = rmxtsagfhhbdf.Msg(audjqzdjalemujihpndrbwyecyxtjbgxvnipykvfdottjchqmtonrboosfetriitenftnqrtksoc);\n",
            "        nuqmkawpeichnqaptwqsrxnpectqoulfionw = rmxtsagfhhbdf.Msg(\n"
            + "            audjqzdjalemujihpndrbwyecyxtjbgxvnipykvfdottjchqmtonrboosfetriitenftnqrtksoc\n"
            + "        );\n"
        );

    /// <summary>
    ///     b1 C12722: two arguments behind an assignment, 3/209 of a column past round 11's limit; the oracle breaks.
    /// </summary>
    [Fact]
    public void Limit_b1_C12722() =>
        Agrees(
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                nyfkcfwuzclitnrcfoafqznynplilmhixbqp = rkhhouvwexdyxjpkaxh.Mxqx(aiqfoacxgjww, buyfpeltunzaykjdmpkxqysflcjk);\n"
            + "            }\n"
            + "        }\n",
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                nyfkcfwuzclitnrcfoafqznynplilmhixbqp =\n"
            + "                    rkhhouvwexdyxjpkaxh.Mxqx(aiqfoacxgjww, buyfpeltunzaykjdmpkxqysflcjk);\n"
            + "            }\n"
            + "        }\n"
        );

    /// <summary>b1 C12734: the same a column wider; the oracle chops beside the <c>=</c>.</summary>
    [Fact]
    public void Limit_b1_C12734() =>
        Agrees(
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                nyfkcfwuzclitnrcfoafqznynplilmhixbqp = rkhhouvwexdyxjpkaxh.Mxqx(adgbgcqpnnnd, bgkpqgvqinfiskniqefynjfvhfxwm);\n"
            + "            }\n"
            + "        }\n",
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                nyfkcfwuzclitnrcfoafqznynplilmhixbqp = rkhhouvwexdyxjpkaxh.Mxqx(\n"
            + "                    adgbgcqpnnnd,\n"
            + "                    bgkpqgvqinfiskniqefynjfvhfxwm\n"
            + "                );\n"
            + "            }\n"
            + "        }\n"
        );

    /// <summary>
    ///     b1 C2463: two arguments behind a typed head, on round 11's limit; the oracle chops beside the <c>=</c>.
    /// </summary>
    [Fact]
    public void Limit_b1_C2463() =>
        Agrees(
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                Ttknijskeskfgkb nssesbpjrmwpuaqazjvfoux = ryfrlxvurifcbylhepre.Mysrb(aqvbbyosdv, bgwhmfvxuiatwfvfcxofbemdetawlrqe);\n"
            + "            }\n"
            + "        }\n",
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                Ttknijskeskfgkb nssesbpjrmwpuaqazjvfoux = ryfrlxvurifcbylhepre.Mysrb(\n"
            + "                    aqvbbyosdv,\n"
            + "                    bgwhmfvxuiatwfvfcxofbemdetawlrqe\n"
            + "                );\n"
            + "            }\n"
            + "        }\n"
        );

    /// <summary>b1 C2474: the same a column narrower; the oracle breaks the <c>=</c>.</summary>
    [Fact]
    public void Limit_b1_C2474() =>
        Agrees(
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                Ttknijskeskfgkb nssesbpjrmwpuaqazjvfoux = ryfrlxvurifcbylhepre.Mysrb(adnphctkvg, bgmwwafoprqkhicvhyjzgajqntzmiut);\n"
            + "            }\n"
            + "        }\n",
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                Ttknijskeskfgkb nssesbpjrmwpuaqazjvfoux =\n"
            + "                    ryfrlxvurifcbylhepre.Mysrb(adnphctkvg, bgmwwafoprqkhicvhyjzgajqntzmiut);\n"
            + "            }\n"
            + "        }\n"
        );

    /// <summary>
    ///     gt1 C1725: callee 21, name 20, the <c>(</c> at 80, past round 12's left line; the oracle chops.
    /// </summary>
    [Fact]
    public void Gate_gt1_C1725() =>
        Agrees(
            "        Ttdckelgmicohuhdnswkxtojjk nhfqvivtpevycarprjwy = rjszcwnhjdghbm.Mzxiej(aeuhhkjuetkobbolypidrkqbkili, bwmrkyencjl);\n",
            "        Ttdckelgmicohuhdnswkxtojjk nhfqvivtpevycarprjwy = rjszcwnhjdghbm.Mzxiej(\n"
            + "            aeuhhkjuetkobbolypidrkqbkili,\n"
            + "            bwmrkyencjl\n"
            + "        );\n"
        );

    /// <summary>
    ///     gt1 C1858: callee 22, name 18, the <c>(</c> at 69, short of round 12's left line; the oracle breaks.
    /// </summary>
    [Fact]
    public void Gate_gt1_C1858() =>
        Agrees(
            "        Tbtkoxwggmvsumsh nnsnyfuywlehjxuykx = rswdotvxbsaloif.Miougj(alwtaqcboxtspfikkoyrcouxjafglu, bjxflwwywznesgrxskdm);\n",
            "        Tbtkoxwggmvsumsh nnsnyfuywlehjxuykx =\n"
            + "            rswdotvxbsaloif.Miougj(alwtaqcboxtspfikkoyrcouxjafglu, bjxflwwywznesgrxskdm);\n"
        );

    /// <summary>
    ///     gt1 C120: callee 9, name 24, the <c>(</c> at 98, the right side past a name of 22; the oracle breaks.
    /// </summary>
    [Fact]
    public void Gate_gt1_C120() =>
        Agrees(
            "        Teatbnjeuhszlcbchlmclvuzbxhblijydgwepszouoohgpdfkbxx ntwlydzviipgrmgodcmxilqw = ry.Mccvaz(avdxt, btlpbdogsmykcbem);\n",
            "        Teatbnjeuhszlcbchlmclvuzbxhblijydgwepszouoohgpdfkbxx ntwlydzviipgrmgodcmxilqw =\n"
            + "            ry.Mccvaz(avdxt, btlpbdogsmykcbem);\n"
        );

    /// <summary>
    ///     xq1 C1322: the operand past the margin, <c>… is N</c> fitting one level in; the pattern's dot breaks.
    /// </summary>
    [Fact]
    public void PatternDot_xq1_C1322() =>
        Agrees(
            "        return roiqdwptuqmflyjveehj.Pnuiifthghxbtiwmydvtzwlslwnmubkchjblxqnrzuslxriuobqerenmofyqtnyckkjdpdopqnxxdlsphzfoukkatlflpsl is Npo.Tnhbmn;\n",
            "        return roiqdwptuqmflyjveehj\n"
            + "            .Pnuiifthghxbtiwmydvtzwlslwnmubkchjblxqnrzuslxriuobqerenmofyqtnyckkjdpdopqnxxdlsphzfoukkatlflpsl is Npo\n"
            + "            .Tnhbmn;\n"
        );

    /// <summary>xq1 C1323: the same a column wider, past the bound; the x layout, the dot two levels in.</summary>
    [Fact]
    public void PatternDot_xq1_C1323() =>
        Agrees(
            "        return rguifpoevdrmwtmjncuq.Pkpfttoqbojlhdcpsbytylgccaxwdjddpejguczwcycfngcdittrqkonyqdtxwydwvbterdmpowrnftlhezouuqnvxleamzq is Nez.Tuktod;\n",
            "        return rguifpoevdrmwtmjncuq\n"
            + "                .Pkpfttoqbojlhdcpsbytylgccaxwdjddpejguczwcycfngcdittrqkonyqdtxwydwvbterdmpowrnftlhezouuqnvxleamzq is\n"
            + "            Nez.Tuktod;\n"
        );

    /// <summary>
    ///     n1 C670: a head of six, the <c>(</c> one column over, an argument of 11; the dot takes the line.
    /// </summary>
    [Fact]
    public void NarrowOneOver_n1_C670() =>
        Agrees(
            "        nirn = refknerdzwqyqtyxslkzyqcvrvldbbibveutmmibwfcfsozcsrhtngeyocsfwssihndopavsoyilqswyxwevkmghiwodwmlkik.Mbdzae(ahpaagarszp);\n",
            "        nirn = refknerdzwqyqtyxslkzyqcvrvldbbibveutmmibwfcfsozcsrhtngeyocsfwssihndopavsoyilqswyxwevkmghiwodwmlkik\n"
            + "            .Mbdzae(ahpaagarszp);\n"
        );

    /// <summary>n1 C671: the same with an argument of 12; the oracle breaks the <c>=</c> and chops below.</summary>
    [Fact]
    public void NarrowOneOver_n1_C671() =>
        Agrees(
            "        nkay = rrmzxvhuiblkjnujixonqtnutlvxivkiopcrnhlervynqrcuqoyqdosmymyiuqdyopczrsokaumrkkbzsxavxlodppxufsurad.Mubqqi(abxqdrvnfxrn);\n",
            "        nkay =\n"
            + "            rrmzxvhuiblkjnujixonqtnutlvxivkiopcrnhlervynqrcuqoyqdosmymyiuqdyopczrsokaumrkkbzsxavxlodppxufsurad.Mubqqi(\n"
            + "                abxqdrvnfxrn\n"
            + "            );\n"
        );

    /// <summary>n1 C893: a typed head of five, an argument of 32; the dot takes the line.</summary>
    [Fact]
    public void NarrowOneOver_n1_C893() =>
        Agrees(
            "        T n = rnjmvqevnqnufgaczvkdkonwqjhqmeobfjwlkddoqkfnfqzwugbnhxkyyrhbvuepdjqhwdsujecxeophgzisheyjpcrfzqlvpoz.Mutlju(awxjzirzqdoyzrzwjogqispbpqujhryk);\n",
            "        T n = rnjmvqevnqnufgaczvkdkonwqjhqmeobfjwlkddoqkfnfqzwugbnhxkyyrhbvuepdjqhwdsujecxeophgzisheyjpcrfzqlvpoz\n"
            + "            .Mutlju(awxjzirzqdoyzrzwjogqispbpqujhryk);\n"
        );

    /// <summary>n1 C894: the same with an argument of 33; the oracle breaks the <c>=</c>.</summary>
    [Fact]
    public void NarrowOneOver_n1_C894() =>
        Agrees(
            "        T n = rvjhywrwnrndfaxsfbuvduireniglyilhuhajggmrpeifvjtizjecbalgcunxsetmcrttmxuralkxfjsncjwivvmeqpqxgukqwo.Mdwguc(aixtpfvrrdscrvkliovrjdwrscfmtuqyi);\n",
            "        T n =\n"
            + "            rvjhywrwnrndfaxsfbuvduireniglyilhuhajggmrpeifvjtizjecbalgcunxsetmcrttmxuralkxfjsncjwivvmeqpqxgukqwo.Mdwguc(\n"
            + "                aixtpfvrrdscrvkliovrjdwrscfmtuqyi\n"
            + "            );\n"
        );

    /// <summary>n2 C141: an assignment head of four, an argument of 64; the dot takes the line.</summary>
    [Fact]
    public void NarrowOneOver_n2_C141() =>
        Agrees(
            "        no = rizkbjskuvxmwtwuxaikvlrmmocynibnrbgsnvprffdyrbyswumblelqmrnfkxboiqfsqyzoaoqrpxgsctfzvrzxdafexrfjuzxp.Meqcyt(auqzdgcawvncqmxasesymlnfiglbtpwlnrtcmnbgwqoqhkinzpuysvrfarapzgny);\n",
            "        no = rizkbjskuvxmwtwuxaikvlrmmocynibnrbgsnvprffdyrbyswumblelqmrnfkxboiqfsqyzoaoqrpxgsctfzvrzxdafexrfjuzxp\n"
            + "            .Meqcyt(auqzdgcawvncqmxasesymlnfiglbtpwlnrtcmnbgwqoqhkinzpuysvrfarapzgny);\n"
        );

    /// <summary>
    ///     n2 C142: the same with an argument of 65; the oracle breaks the <c>=</c>, its value a column past the
    ///     continuation.
    /// </summary>
    [Fact]
    public void NarrowOneOver_n2_C142() =>
        Agrees(
            "        nb = rcoijzcuhvyfrwmgbmxzijholqqsxwzbkqhyajhyoywuvyadzsfijuqwalolkfhdwtkbgtngtubsiqfuzcekarnktxndzoixqbpp.Mzidit(aonaflzczwhuospgfvuxmyptnlhwdzsdrvlralqgljutaahghahzcyzhxiqxhmvqh);\n",
            "        nb =\n"
            + "            rcoijzcuhvyfrwmgbmxzijholqqsxwzbkqhyajhyoywuvyadzsfijuqwalolkfhdwtkbgtngtubsiqfuzcekarnktxndzoixqbpp.Mzidit(\n"
            + "                aonaflzczwhuospgfvuxmyptnlhwdzsdrvlralqgljutaahghahzcyzhxiqxhmvqh\n"
            + "            );\n"
        );

    /// <summary>
    ///     fg1 C5021: a head of six, a wide argument, the <c>(</c> one column over; the oracle breaks the <c>=</c>
    ///     (wrong on master).
    /// </summary>
    [Fact]
    public void NarrowOneOver_fg1_C5021() =>
        Agrees(
            "        if (b) {\n"
            + "            nzyg = rxexbbdqrvnzgpgekiqejtykaxfmyraedrcgkakhtzeikdpbddtomsyzimqkvrxxdqkfihfogvxburxgn.Mxpfwkrkrmehswyfdmu(ahrueflianeiefepngcbazbwbfrsuvcvbxzlupmbnpqpgywqestrlgoyvbbims);\n"
            + "        }\n",
            "        if (b) {\n"
            + "            nzyg =\n"
            + "                rxexbbdqrvnzgpgekiqejtykaxfmyraedrcgkakhtzeikdpbddtomsyzimqkvrxxdqkfihfogvxburxgn.Mxpfwkrkrmehswyfdmu(\n"
            + "                    ahrueflianeiefepngcbazbwbfrsuvcvbxzlupmbnpqpgywqestrlgoyvbbims\n"
            + "                );\n"
            + "        }\n"
        );

    /// <summary>
    ///     o1 C75: two arguments one column over, a type of 41 at <c>8·p − 5·i = 640</c>; the oracle chops beside the
    ///     <c>=</c>.
    /// </summary>
    [Fact]
    public void OneOverGap_o1_C75() =>
        Agrees(
            "        Tobtsvgigqqiebwpmdgcsqenzptghpolxervgusaw nmcynqadgpcxgx = rceujxzjdh.Mxbnom(axcrktjihhldvm, btlkatmijldjtygzmg);\n",
            "        Tobtsvgigqqiebwpmdgcsqenzptghpolxervgusaw nmcynqadgpcxgx = rceujxzjdh.Mxbnom(\n"
            + "            axcrktjihhldvm,\n"
            + "            btlkatmijldjtygzmg\n"
            + "        );\n"
        );

    /// <summary>o1 C77: the same with a type of 42; the oracle breaks between the type and the name.</summary>
    [Fact]
    public void OneOverGap_o1_C77() =>
        Agrees(
            "        Tgjhhnsldmyxuuwaiokksjhfdmbpquodarjqgdfjoi ntosruwtkpibdyppie = rvslz.Muhvvr(apsgdapol, bznphyotqkgcunxlpmjfmuh);\n",
            "        Tgjhhnsldmyxuuwaiokksjhfdmbpquodarjqgdfjoi\n"
            + "            ntosruwtkpibdyppie = rvslz.Muhvvr(apsgdapol, bznphyotqkgcunxlpmjfmuh);\n"
        );

    /// <summary>
    ///     o1 C1022: one argument, a type of 24 and a name of 4 at indent 8; the oracle breaks between them.
    /// </summary>
    [Fact]
    public void OneOverGap_o1_C1022() =>
        Agrees(
            "        Tpdmuxqstdnnkyrllmxgsntm nlxg = rxbjvzfweanvh.Mnnctp(agsdahwxpwezyzkiaukpuxayvhzshvblcmlpsezeojqmwdmbcrjlcjnoxz);\n",
            "        Tpdmuxqstdnnkyrllmxgsntm\n"
            + "            nlxg = rxbjvzfweanvh.Mnnctp(agsdahwxpwezyzkiaukpuxayvhzshvblcmlpsezeojqmwdmbcrjlcjnoxz);\n"
        );

    /// <summary>
    ///     o1 C1912: a type of 24 and a name of 2 at indent 24, its <c>(</c> at 68; the oracle chops beside the
    ///     <c>=</c>.
    /// </summary>
    [Fact]
    public void OneOverGap_o1_C1912() =>
        Agrees(
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        Tnyqkirbkyhnxsatyfyuhwzo ns = riiupu.Mbtkjc(asiysfdnyvfenwnovyzjqxjaftymgnsuishxyxrccmvtzxqfugv);\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n",
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        Tnyqkirbkyhnxsatyfyuhwzo ns = riiupu.Mbtkjc(\n"
            + "                            asiysfdnyvfenwnovyzjqxjaftymgnsuishxyxrccmvtzxqfugv\n"
            + "                        );\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n"
        );

    /// <summary>
    ///     nl1 C211: the name's own line, one argument inside the assignment's limit; the oracle breaks the <c>=</c>.
    /// </summary>
    [Fact]
    public void NameLine_nl1_C211() =>
        Agrees(
            "        Tsigdjdfnrbqmdkgieyebvxeaoxknabgumzhnvojemuirfcogieajxvkeikjlperalrimglkaipqxdehetswccdnmzbkkseffhqdpz nttpvdizopy = rjbqlhzmhtjlmrtujranglsqvgpqpdqfqtlmesvfbpx.Mojhkjcz(arrertuoxghvqgmdbphnykhuwpgowscdrsemfycmwvwncxwdpmcnyulcmwnyd);\n",
            "        Tsigdjdfnrbqmdkgieyebvxeaoxknabgumzhnvojemuirfcogieajxvkeikjlperalrimglkaipqxdehetswccdnmzbkkseffhqdpz\n"
            + "            nttpvdizopy =\n"
            + "                rjbqlhzmhtjlmrtujranglsqvgpqpdqfqtlmesvfbpx.Mojhkjcz(\n"
            + "                    arrertuoxghvqgmdbphnykhuwpgowscdrsemfycmwvwncxwdpmcnyulcmwnyd\n"
            + "                );\n"
        );

    /// <summary>nl1 C212: the same a column wider; the oracle keeps the <c>=</c> and chops.</summary>
    [Fact]
    public void NameLine_nl1_C212() =>
        Agrees(
            "        Tsigdjdfnrbqmdkgieyebvxeaoxknabgumzhnvojemuirfcogieajxvkeikjlperalrimglkaipqxdehetswccdnmzbkkseffhqdpz nttpvdizopy = rjbqlhzmhtjlmrtujranglsqvgpqpdqfqtlmesvfbpx.Mojhkjcz(abgbtlhbkczkxyaendboriokkmeobmzmojurvpcaupugnumnekgthdgqzomlxo);\n",
            "        Tsigdjdfnrbqmdkgieyebvxeaoxknabgumzhnvojemuirfcogieajxvkeikjlperalrimglkaipqxdehetswccdnmzbkkseffhqdpz\n"
            + "            nttpvdizopy = rjbqlhzmhtjlmrtujranglsqvgpqpdqfqtlmesvfbpx.Mojhkjcz(\n"
            + "                abgbtlhbkczkxyaendboriokkmeobmzmojurvpcaupugnumnekgthdgqzomlxo\n"
            + "            );\n"
        );

    /// <summary>
    ///     nl1 C268: a name of three on its own line; as a narrow head the oracle keeps the <c>=</c> and chops.
    /// </summary>
    [Fact]
    public void NameLine_nl1_C268() =>
        Agrees(
            "        Tvktjkhnkpmwsrjdfcntwiudlrbhzyuelwzjilzwjnkarzgcwxgxivmatgnqahhipuptekrjotaplfuldjalvoghvvyyqdrngjrcqapqyxsmjyop nzd = rzfvyletqzzathfobxdjz.Mlhdt(agecrakuwlofybhesijxzoxwfiqudfxidpzxqfuzlesjexufuiczizzgijcaejzrxtswzhfpn);\n",
            "        Tvktjkhnkpmwsrjdfcntwiudlrbhzyuelwzjilzwjnkarzgcwxgxivmatgnqahhipuptekrjotaplfuldjalvoghvvyyqdrngjrcqapqyxsmjyop\n"
            + "            nzd = rzfvyletqzzathfobxdjz.Mlhdt(\n"
            + "                agecrakuwlofybhesijxzoxwfiqudfxidpzxqfuzlesjexufuiczizzgijcaejzrxtswzhfpn\n"
            + "            );\n"
        );
}
