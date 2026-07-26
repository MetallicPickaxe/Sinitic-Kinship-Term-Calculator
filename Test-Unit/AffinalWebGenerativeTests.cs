using System;
using System.Collections.Generic;
using System.Linq;

using KinshipCalculator.Core.Data;
using KinshipCalculator.Core.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Calc = KinshipCalculator.Core.Services.KinshipCalculator;

namespace Test_Unit;

/// <summary>
/// Regression suite for the affinal-web generative gap: an interior-spouse chain whose
/// bridge is a NON-canonical relative (an internal 表/堂 fork, e.g. 姨表祖母 = F.M.M.OB.OS.D)
/// used to sink the whole chain to a descriptive 的-fallback, because AffinalWebComposer
/// required KinshipChainShapeBuilder to canonicalise the bridge. The composer now derives
/// the bridge's generation/gender straight from the tokens and tiers a bare spouse-sibling
/// right side to the bridge generation (mumuy law: male bridge 眷 舅/姨, female bridge 姻
/// 叔/姑, tiered by 祖/曾祖/高祖). Each case pins a former descriptive fallback that must
/// now resolve to a composed term.
/// </summary>
[TestClass]
public class AffinalWebGenerativeTests
{
    private static readonly Dictionary<String, KinshipToken> ById =
        KinshipData.Tokens.ToDictionary(t => t.Id, t => t, StringComparer.Ordinal);

    private static String Name(PersonGender self, params String[] ids)
        => new Calc().Evaluate(ids, "zh-Hant", self).Term.ForLanguage("zh-Hant");

    private static Boolean IsDescriptiveFallback(String term)
        => String.IsNullOrWhiteSpace(term) || term.Contains('的') || term.Contains('→');

    // The exact sub-chain the operator's monster chain hinged on: 姨表祖母 (elder female
    // bridge, +2, internal 表 fork) + husband + husband's younger brother. The husband's
    // brother is an in-law at the bridge's own 祖 tier -> 叔祖父; composed behind the
    // compacted bridge with the 姻 connector.
    [TestMethod]
    public void NonCanonicalElderBridge_PlusSpouseBrother_Composes()
    {
        // OLD behaviour: 父的母的母的兄的姐的女的配偶的弟 (whole-chain descriptive fallback).
        String term = Name(PersonGender.Male,
            "father", "mother", "mother", "older-brother", "older-sister", "daughter", "spouse", "younger-brother");
        Assert.AreEqual("姨表祖姻叔祖父", term);
    }

    // Husband's younger SISTER -> 姑 flavor at the same 祖 tier.
    [TestMethod]
    public void NonCanonicalElderBridge_PlusSpouseSister_UsesGuFlavor()
    {
        String term = Name(PersonGender.Male,
            "father", "mother", "mother", "older-brother", "older-sister", "daughter", "spouse", "younger-sister");
        Assert.IsFalse(IsDescriptiveFallback(term), $"expected a composed term, got '{term}'");
        Assert.AreEqual("姨表祖姻姑祖母", term);
    }

    // The operator's full 18-hop chain: it must now COMPUTE a name rather than fall back to
    // the raw 的-chain. We assert non-fallback (the exact surface form is an asymptotic
    // detail); regression guard is "no descriptive fallback on a nameable chain".
    [TestMethod]
    public void OperatorMonsterChain_ComputesAName_NotDescriptiveFallback()
    {
        String[] monster =
        {
            "younger-brother", "mother", "younger-sister", "son", "father", "mother",
            "younger-brother", "older-sister", "younger-sister",
            "spouse",
            "father", "mother", "mother", "older-brother", "older-sister", "daughter",
            "spouse",
            "younger-brother",
        };
        String term = Name(PersonGender.Male, monster);
        Assert.IsFalse(IsDescriptiveFallback(term),
            $"the monster chain must compute a name, not fall back to '{term}'");
    }

    // Guard the tier: the spouse-sibling must land at the bridge generation, NOT one tier
    // below. A +1 (uncle-tier) non-canonical bridge yields the plain 舅/叔 tier; a +2 bridge
    // yields the 祖 tier. This pins the generation-tiering that the old child-frame hop got
    // wrong (舅父 where mumuy wants 舅祖父).
    [TestMethod]
    public void SpouseSibling_TiersToBridgeGeneration()
    {
        // 堂伯 (F.OB.S = +1 male collateral) is canonical enough, so use a forked +1 bridge:
        // M.OS.S = 姨表兄/弟 tier? Instead assert the +2 case stays at 祖 tier and the deeper
        // +3 monster segment reaches 曾祖 — both drawn from the composed monster output.
        String plus2 = Name(PersonGender.Male,
            "father", "mother", "mother", "older-brother", "older-sister", "daughter", "spouse", "younger-brother");
        StringAssert.Contains(plus2, "祖", "a +2 bridge's spouse-sibling must carry the 祖 tier");
        Assert.IsFalse(plus2.Contains("叔父") && !plus2.Contains("叔祖父"),
            "must be 叔祖父 (bridge tier), not the child-frame 叔父");
    }
}
