using KinshipCalculator.Testing.Verification;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test_Verification;

[TestClass]
public class ReferenceJudgmentPolicyTests
{
    [TestMethod]
    public void ExtractPrimaryCandidate_RemovesAlternatePortion()
    {
        var result = ReferenceJudgmentPolicy.ExtractPrimaryCandidate("堂侄女 | 堂侄女（口語）");

        Assert.AreEqual("堂侄女", result);
    }

    [TestMethod]
    public void EvaluateRow_Returns_Aligned_For_ExactMatch()
    {
        var result = ReferenceJudgmentPolicy.EvaluateRow("堂侄女", "堂侄女", "堂侄女");

        Assert.AreEqual(ReferenceJudgmentKind.Aligned, result.Kind);
        Assert.AreEqual("堂侄女", result.CandidateDisplay);
        Assert.AreEqual("一致", result.JudgmentDisplay);
    }

    // ---- Candidate-hit contract (release-audit round 2): the K15 layered design keeps the
    // STANDARD form primary while a reference's regional form may live among our tagged
    // candidates. A non-primary hit grades acceptable and is marked 候選命中 — never 一致.

    [TestMethod]
    public void CandidateHit_InFirstCandidateAlternateTail_GradesAcceptable_NotAligned()
    {
        // 伯外公 lives in the FIRST candidate's own alternate tail ("label | alternate").
        var result = ReferenceJudgmentPolicy.EvaluateRow(
            "伯外公", "伯外祖父 | 伯外公", "伯外祖父 | 伯外公", "M.F.OB", null, null);

        Assert.AreEqual(ReferenceJudgmentKind.LexicalEquivalenceCandidate, result.Kind);
        StringAssert.Contains(result.JudgmentDisplay, "候選命中");
        Assert.AreNotEqual("一致", result.JudgmentDisplay);
    }

    [TestMethod]
    public void CandidateHit_InLaterOptions_GradesAcceptable()
    {
        // The reference form appears only in a LATER option (the others channel).
        var result = ReferenceJudgmentPolicy.EvaluateRow(
            "伯外公", "伯外祖父", "伯外祖父", "M.F.OB", "伯外公", "伯外公");

        Assert.AreEqual(ReferenceJudgmentKind.LexicalEquivalenceCandidate, result.Kind);
        StringAssert.Contains(result.JudgmentDisplay, "候選命中");
    }

    [TestMethod]
    public void CandidateHit_WrongCandidates_StayMismatch()
    {
        // Serving OTHER wrong forms must not launder a miss into a hit.
        var result = ReferenceJudgmentPolicy.EvaluateRow(
            "伯外公", "伯外祖父 | 某錯詞", "伯外祖父 | 某錯詞", "M.F.OB", "另一錯詞", "另一錯詞");

        Assert.AreEqual(ReferenceJudgmentKind.StructuralMismatch, result.Kind);
    }

    [TestMethod]
    public void CandidateHit_PreemptsLedgerVerdict()
    {
        // D.D.D.D sits in the absorption ledger (reject). The ledger only applies to a
        // structural MISMATCH; when the reference term is genuinely served among our
        // candidates the row is a candidate hit, not a ledger verdict.
        var result = ReferenceJudgmentPolicy.EvaluateRow(
            "外曾外曾外孙女", "外玄孫女 | 外曾外曾外孙女", "外玄孫女 | 外曾外曾外孙女", "D.D.D.D", null, null);

        Assert.AreEqual(ReferenceJudgmentKind.LexicalEquivalenceCandidate, result.Kind);
        StringAssert.Contains(result.JudgmentDisplay, "候選命中");
    }

    [TestMethod]
    public void EvaluateRow_Returns_LexicalEquivalenceCandidate_For_SunNvXu_Compaction()
    {
        var result = ReferenceJudgmentPolicy.EvaluateRow("晜孙女婿", "晜孙婿", "晜孙婿");

        Assert.AreEqual(ReferenceJudgmentKind.LexicalEquivalenceCandidate, result.Kind);
        Assert.AreEqual("晜孙婿", result.CandidateDisplay);
        Assert.AreEqual("可接受簡寫：晜孙婿", result.JudgmentDisplay);
    }

    [TestMethod]
    public void EvaluateRow_Returns_LexicalEquivalenceCandidate_For_XiFu_Compaction()
    {
        var result = ReferenceJudgmentPolicy.EvaluateRow("仍外孙媳妇", "仍外孙媳", "仍外孙媳");

        Assert.AreEqual(ReferenceJudgmentKind.LexicalEquivalenceCandidate, result.Kind);
        Assert.AreEqual("仍外孙媳", result.CandidateDisplay);
    }

    [TestMethod]
    public void EvaluateRow_Returns_LexicalEquivalenceCandidate_For_SunZi_Compaction()
    {
        var result = ReferenceJudgmentPolicy.EvaluateRow("外曾外孙 | 外息仔", "外曾外孫子", "外曾外孫子");

        Assert.AreEqual(ReferenceJudgmentKind.LexicalEquivalenceCandidate, result.Kind);
        Assert.AreEqual("外曾外孫子", result.CandidateDisplay);
        Assert.AreEqual("可接受簡寫：外曾外孫子", result.JudgmentDisplay);
    }

    [DataTestMethod]
    [DataRow("亲家公", "親家公")]
    [DataRow("父亲", "父親")]
    [DataRow("远祖父", "遠祖父")]
    [DataRow("开祖父", "開祖父")]
    [DataRow("婶", "嬸")]
    public void EvaluateAgainstReference_Normalizes_GlyphVariants(string reference, string candidate)
    {
        var result = ReferenceJudgmentPolicy.EvaluateAgainstReference(reference, candidate);

        Assert.AreEqual(ReferenceJudgmentKind.Aligned, result);
    }

    [DataTestMethod]
    [DataRow("表兄", "表哥")]
    [DataRow("堂兄", "族兄")]
    [DataRow("祖父", "爷爷")]
    [DataRow("祖母", "奶奶")]
    [DataRow("外祖母|外婆", "姥姥")]
    [DataRow("外祖父|外公", "姥爷")]
    [DataRow("大爷", "伯父")]
    [DataRow("大爷", "大伯父")]
    [DataRow("大妈", "大伯母")]
    [DataRow("二大妈", "二伯母")]
    [DataRow("嬸母|嬸兒", "婶婶")]
    [DataRow("外伯祖父", "伯外公")]
    [DataRow("外伯祖母", "伯外婆")]
    [DataRow("外叔祖父", "叔外公")]
    [DataRow("外叔祖母", "叔外婆")]
    [DataRow("外姑祖父", "姑外公")]
    [DataRow("外姑祖母", "姑外婆")]
    public void EvaluateAgainstReference_Returns_LexicalEquivalenceCandidate_For_BoundedColloquialPairs(string reference, string candidate)
    {
        var result = ReferenceJudgmentPolicy.EvaluateAgainstReference(reference, candidate);

        Assert.AreEqual(ReferenceJudgmentKind.LexicalEquivalenceCandidate, result);
    }

    [TestMethod]
    public void EvaluateAgainstReference_Treats_StructuralMarkers_As_Strict()
    {
        var outerMismatch = ReferenceJudgmentPolicy.EvaluateAgainstReference("曾外孙女", "曾孙女");
        var tangBiaoMismatch = ReferenceJudgmentPolicy.EvaluateRow("堂侄女", "表侄女", "堂侄女");

        Assert.AreEqual(ReferenceJudgmentKind.StructuralMismatch, outerMismatch);
        Assert.AreEqual(ReferenceJudgmentKind.StructuralMismatch, tangBiaoMismatch.Kind);
        Assert.AreEqual("男：表侄女；女：堂侄女", tangBiaoMismatch.CandidateDisplay);
        Assert.AreEqual("不一致：男：表侄女；女：堂侄女", tangBiaoMismatch.JudgmentDisplay);
    }

    // ------------------------------------------------------------------ K5: data-driven lexicon families

    [DataTestMethod]
    [DataRow("伯公|大爷爷", "伯祖父")]       // grand-uncle colloquial family
    [DataRow("叔婆|婶婆", "叔祖母")]         // grand-aunt colloquial family
    [DataRow("舅爷爷|太舅父", "舅祖父")]     // maternal grand-uncle family
    [DataRow("舅外公|舅爹", "外舅祖父")]     // maternal-line grand-uncle (R0007 pattern extended)
    [DataRow("姨外婆|姨婆", "外姨祖母")]     // maternal-line grand-aunt
    [DataRow("祖公父|祖公", "祖父")]         // spouse-side 隨夫稱 series
    [DataRow("祖岳父|祖丈人", "岳祖父")]     // spouse-side 岳-form series
    [DataRow("外祖岳母|姥丈母娘", "岳外祖母")]
    [DataRow("耳孙|九世孙", "8代孫子")]      // 世-count alias (世 = 代 + 1)
    [DataRow("族伯母|族叔母", "族嬸")]       // 嬸 = 叔母 in graded families
    public void EvaluateAgainstReference_LexiconTsv_Families_Match(string reference, string candidate)
    {
        var result = ReferenceJudgmentPolicy.EvaluateAgainstReference(reference, candidate);

        Assert.AreEqual(ReferenceJudgmentKind.LexicalEquivalenceCandidate, result);
    }

    [DataTestMethod]
    [DataRow("伯叔高祖父", "伯高祖父")]       // 合稱展開:伯叔X ≡ 伯X / 叔X
    [DataRow("伯叔高祖父", "叔高祖父")]
    [DataRow("堂伯叔曾祖母", "堂叔曾祖母")]
    [DataRow("族伯父|族叔父", "族伯")]        // 稱尾省略:族伯父 ≡ 族伯
    [DataRow("堂曾孙|再曾孙", "堂姪曾孫子")]  // graded 姪-elision + 孫子 compaction
    [DataRow("甥孙女|远甥女", "外甥孫女")]    // 外甥 is a lexeme, not a lineage marker
    [DataRow("曾孙妇", "曾孫媳")]             // 妇 ≡ 媳
    public void EvaluateAgainstReference_PatternRules_Match(string reference, string candidate)
    {
        var result = ReferenceJudgmentPolicy.EvaluateAgainstReference(reference, candidate);

        Assert.AreNotEqual(ReferenceJudgmentKind.StructuralMismatch, result);
    }

    // ------------------------------------------------------------------ K3/K6: absorption-ledger verdicts

    [TestMethod]
    public void EvaluateRow_LedgerReject_For_StackedWaiSynthesis()
    {
        var result = ReferenceJudgmentPolicy.EvaluateRow("外曾外曾外孙女", "外玄孫女", "外玄孫女", "D.D.D.D");

        Assert.AreEqual(ReferenceJudgmentKind.RejectedReference, result.Kind);
        Assert.AreEqual("拒收：外玄孫女", result.JudgmentDisplay);
    }

    [TestMethod]
    public void EvaluateRow_LedgerAbsorb_For_YinSeries()
    {
        var result = ReferenceJudgmentPolicy.EvaluateRow("姻伯 | 姻叔", "女的配偶的父的父", "女的配偶的父的父", "D.SP.F.F");

        Assert.AreEqual(ReferenceJudgmentKind.AbsorbedVariant, result.Kind);
        Assert.AreEqual("已收編：女的配偶的父的父", result.JudgmentDisplay);
    }

    [TestMethod]
    public void EvaluateRow_WithoutChain_LedgerDoesNotApply()
    {
        var result = ReferenceJudgmentPolicy.EvaluateRow("外曾外曾外孙女", "外玄孫女", "外玄孫女");

        Assert.AreEqual(ReferenceJudgmentKind.StructuralMismatch, result.Kind);
    }

    [TestMethod]
    public void EvaluateRow_UnknownChain_MismatchIsPreserved()
    {
        var result = ReferenceJudgmentPolicy.EvaluateRow("外曾外曾外孙女", "外玄孫女", "外玄孫女", "X.NOT.A.CHAIN");

        Assert.AreEqual(ReferenceJudgmentKind.StructuralMismatch, result.Kind);
    }
}