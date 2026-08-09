using System;
using System.Linq;

using KinshipCalculator.Core.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Calc = KinshipCalculator.Core.Services.KinshipCalculator;

namespace Test_Unit;

/// <summary>
/// Regression suite for the lexicon-wiring repair round (2026-08-02). The unifying defect class
/// was a layer entry that loads, validates and reverse-looks-up correctly while being
/// unreachable — dead data that looks exactly like a relation with no everyday word. Three
/// distinct causes were found by the reachability sweep in Test-Verification, and each is pinned
/// here with the OLD behaviour recorded.
/// </summary>
[TestClass]
public class LexiconWiringRepairTests
{
    private static KinshipResolutionOption First(PersonGender self, params String[] ids)
        => new Calc().Evaluate(ids, "zh-Hant", self).Options.First();

    private static String Alternates(params String[] ids)
        => First(PersonGender.Male, ids).AlternateLabel.ForLanguage("zh-Hant");

    private static String Term(params String[] ids)
        => First(PersonGender.Male, ids).Label.ForLanguage("zh-Hant");

    // ---- cause 1: the calculator answered before the formatters, so the layers were never asked

    [TestMethod]
    public void AtomicRelations_CarryTheirLayerVariants()
    {
        // OLD: F/M/S/D/SP resolve from the static term table, which sets an EMPTY alternate —
        // so 父親 reached the UI with a blank "other names" column while register-colloquial
        // had 爸爸/老爸 registered against it all along.
        CollectionAssert.Contains(Alternates("father").Split('|'), "爸爸");
        CollectionAssert.Contains(Alternates("mother").Split('|'), "媽媽");
        // The layers are authored in Hans and script-converted on the way out. The converter
        // table only knew the engine's own vocabulary, so a regional word's characters passed
        // through untouched and a zh-Hant reader was shown 老汉.
        CollectionAssert.Contains(Alternates("father").Split('|'), "老漢");
        CollectionAssert.DoesNotContain(Alternates("father").Split('|'), "老汉");
        Assert.AreNotEqual(String.Empty, Alternates("son"), "兒子 must offer layer variants");
        Assert.AreNotEqual(String.Empty, Alternates("daughter"), "女兒 must offer layer variants");
    }

    [TestMethod]
    public void DescendantTitles_CarryTheirLayerVariants()
    {
        // OLD: the descendant formatter never queries the lexicon, so 孫子/外孫子 answered with
        // only whatever the formatter hard-coded. The merge must ADD the layer set, not replace.
        CollectionAssert.Contains(Alternates("daughter", "son").Split('|'), "外孫兒");
        Assert.AreNotEqual(String.Empty, Alternates("son", "son"), "孫子 must offer layer variants");
    }

    // ---- cause 2: a computed relation the engine refused to name

    [TestMethod]
    public void SpouseUncleSpouse_IsNamed_NotDescribed()
    {
        // OLD: TryAnalyzeSpouseCollateral required the sibling to be the LAST token, so the
        // marriage hop dropped the whole chain to a 的-chain: 配偶的父的兄的配偶.
        Assert.AreEqual("伯岳母", Term("spouse", "father", "older-brother", "spouse"));
        Assert.AreEqual("叔岳母", Term("spouse", "father", "younger-brother", "spouse"));
        Assert.AreEqual("姑岳父", Term("spouse", "father", "older-sister", "spouse"));
        Assert.AreEqual("舅岳母", Term("spouse", "mother", "older-brother", "spouse"));
        Assert.AreEqual("姨岳父", Term("spouse", "mother", "older-sister", "spouse"));
    }

    [TestMethod]
    public void SpouseUncle_KeepsItsOwnReading()
    {
        // The marriage hop must not disturb the un-married case that already worked.
        Assert.AreEqual("伯岳父", Term("spouse", "father", "older-brother"));
        Assert.AreEqual("姑岳母", Term("spouse", "father", "older-sister"));
        Assert.AreEqual("舅岳父", Term("spouse", "mother", "older-brother"));
    }

    // ---- cause 3: one relation, two standard spellings

    [TestMethod]
    public void MaternalGrandCollateral_HasOneStandardSpelling()
    {
        // OLD: the direct chain produced 伯外祖父 (morpheme machine: 伯 + 外祖 + 父) while the
        // folded chain produced 外伯祖父 (name formatter: 外 + 伯 + 祖父). Same relation, two
        // answers, depending on which path the user typed.
        Assert.AreEqual("伯外祖父", Term("mother", "father", "older-brother"));
        Assert.AreEqual("伯外祖父", Term("mother", "father", "older-brother", "older-brother"));
        Assert.AreEqual("姑外祖母", Term("mother", "father", "older-sister"));
        Assert.AreEqual("姑外祖母", Term("mother", "father", "older-brother", "older-sister"));
    }

    // ---- cause 6: a two-slot compound that wrote the same person into both slots

    [TestMethod]
    public void SiblingSpouseSibling_NamesTheRealBridge()
    {
        // OLD: the analyzer discarded the bridge sibling's gender and the formatter mirrored the
        // terminal's into both slots, so 兄之妻之姐 read 姊妹姻姊妹 -- my sister's sister, a
        // bridge I do not have. The connector follows the project's own 姻/眷 rule (male bridge
        // takes 眷), which this path ignored while AffinalWebComposer applied it.
        Assert.AreEqual("兄弟眷姊妹", Term("older-brother", "spouse", "older-sister"));
        Assert.AreEqual("兄弟眷兄弟", Term("older-brother", "spouse", "younger-brother"));
        Assert.AreEqual("姊妹姻兄弟", Term("older-sister", "spouse", "older-brother"));
        Assert.AreEqual("姊妹姻姊妹", Term("older-sister", "spouse", "younger-sister"));
    }

    [TestMethod]
    public void SiblingSpouseParent_UsesTheSameConnectorRule()
    {
        // 兄弟姻父 for a BROTHER bridge contradicted the 姻/眷 rule the rest of the engine follows.
        Assert.AreEqual("兄弟眷父", Term("older-brother", "spouse", "father"));
        Assert.AreEqual("兄弟眷母", Term("younger-brother", "spouse", "mother"));
        Assert.AreEqual("姊妹姻父", Term("older-sister", "spouse", "father"));
    }

    // ---- cause 7: a composite formatter that dropped the depth its analyzer had counted

    [TestMethod]
    public void SiblingSpouseAncestor_KeepsItsGeneration()
    {
        // OLD: 兄弟眷父 named my brother's wife's father AND her grandfather. The analyzer
        // counts the ascent (GenerationChange = parentCount); the formatter ignored it.
        Assert.AreEqual("兄弟眷父", Term("older-brother", "spouse", "father"));
        Assert.AreEqual("兄弟眷祖父", Term("older-brother", "spouse", "father", "father"));
        Assert.AreEqual("兄弟眷曾祖母", Term("older-brother", "spouse", "father", "father", "mother"));
        Assert.AreEqual("姊妹姻祖父", Term("older-sister", "spouse", "father", "father"));
    }

    [TestMethod]
    public void SiblingSpouseSiblingDescendant_KeepsItsGeneration()
    {
        // OLD: 姊妹眷姪子 named my sister's husband's brother's son AND that son's son. Third
        // instance of the same shape: the analyzer records GenerationChange = -descendantCount
        // and the formatter stopped at the first generation.
        Assert.AreEqual("姊妹眷姪子", Term("older-sister", "spouse", "younger-brother", "son"));
        Assert.AreEqual("姊妹眷姪孫", Term("younger-sister", "spouse", "younger-brother", "son", "son"));
        // Depth 1 keeps its existing wording (外甥子, not the ladder's bare 外甥) so the fix adds
        // generations without silently restyling the one that already worked.
        Assert.AreEqual("兄弟眷外甥子", Term("older-brother", "spouse", "older-sister", "son"));
        Assert.AreEqual("兄弟眷外甥孫女", Term("older-brother", "spouse", "older-sister", "son", "daughter"));
    }

    [TestMethod]
    public void SpouseSisterDescendant_KeepsItsGeneration()
    {
        // OLD: 姑甥 covered four descending generations at once — the brother-side branch beside
        // it has always carried the ladder, this one returned a flat word.
        Assert.AreEqual("姑甥", Term("spouse", "older-sister", "son"));
        Assert.AreEqual("姑甥孫", Term("spouse", "older-sister", "son", "son"));
        Assert.AreEqual("姑甥女", Term("spouse", "older-sister", "daughter"));
        Assert.AreEqual("姑甥孫女", Term("spouse", "older-sister", "son", "daughter"));
    }

    // ---- cause 5: one standard form, two audiences

    [TestMethod]
    public void Spouse_OffersOnlyTheTermsThisEgoWouldUse()
    {
        // 配偶 is a single gender-neutral standard form covering both spouses, so a flat variant
        // list hands 老公 and 老婆 to the same person. The layers carry these in ego-scoped
        // blocks; before the format extension all 32 of them were held back and the most-clicked
        // relation in the app offered nothing at all.
        string[] male = First(PersonGender.Male, "spouse").AlternateLabel.ForLanguage("zh-Hant").Split('|');
        string[] female = First(PersonGender.Female, "spouse").AlternateLabel.ForLanguage("zh-Hant").Split('|');

        CollectionAssert.Contains(male, "老婆");
        CollectionAssert.Contains(male, "內人");
        CollectionAssert.DoesNotContain(male, "老公");

        CollectionAssert.Contains(female, "老公");
        CollectionAssert.Contains(female, "郎君");
        CollectionAssert.DoesNotContain(female, "老婆");

        // An unknown ego gets neither rather than both.
        CollectionAssert.DoesNotContain(
            First(PersonGender.Unknown, "spouse").AlternateLabel.ForLanguage("zh-Hant").Split('|'), "老婆");

        // 漢子 sat in the ego-NEUTRAL block, so a man was offered it for his wife. A word whose
        // own note says 方言稱夫 belongs to one ego; the neutral block is for 愛人 / 老伴 / 那口子.
        CollectionAssert.Contains(female, "漢子");
        CollectionAssert.DoesNotContain(male, "漢子");
        CollectionAssert.Contains(male, "愛人");
        CollectionAssert.Contains(female, "愛人");
    }

    // ---- cause 4: a reciprocal term offered to the wrong party

    [TestMethod]
    public void OwnSistersHusband_IsNotOfferedLianJin()
    {
        // OLD: 姐夫|连襟. 连襟 is the reciprocal between the husbands of two sisters — he and I
        // are not that. The parallel female branch never offered 妯娌 next to 嫂嫂.
        Assert.AreEqual("姐夫", Term("older-sister", "spouse"));
        Assert.AreEqual("妹夫", Term("younger-sister", "spouse"));
        CollectionAssert.DoesNotContain(Alternates("older-sister", "spouse").Split('|'), "連襟");
        CollectionAssert.DoesNotContain(Alternates("older-sister", "spouse").Split('|'), "连襟");
        // The brother's-wife side must keep working untouched.
        Assert.AreEqual("嫂嫂", Term("older-brother", "spouse"));
    }

    [TestMethod]
    public void WifesSistersHusband_IsLianJin_NotHerTerm()
    {
        // OLD: 姐夫 as the primary — my wife's word for him, borrowed wholesale. The
        // male-sibling line beside it already avoided that (舅嫂, not her 嫂子).
        Assert.AreEqual("襟兄", Term("spouse", "older-sister", "spouse"));
        Assert.AreEqual("襟弟", Term("spouse", "younger-sister", "spouse"));
        CollectionAssert.Contains(Alternates("spouse", "older-sister", "spouse").Split('|'), "連襟");
        // Wife's brother's wife is unchanged.
        Assert.AreEqual("舅嫂", Term("spouse", "older-brother", "spouse"));
    }

    [TestMethod]
    public void GrandCollateral_LadderDoesNotStopAtGreatGrand()
    {
        // OLD: the ladder read `depth == 3 ? 曾祖 : 祖`, so a +4 relative — my great-great-
        // grandfather's brother — was named 伯祖父, the word for a +2. Everything above the
        // great-grand tier collapsed onto 祖.
        Assert.AreEqual("伯祖父", Term("father", "father", "older-brother"));
        Assert.AreEqual("伯曾祖父", Term("father", "father", "father", "older-brother"));
        Assert.AreEqual("伯高祖父", Term("father", "father", "father", "father", "older-brother"));
        Assert.AreEqual("姑高祖母", Term("father", "father", "father", "father", "older-sister"));
    }

    [TestMethod]
    public void PaternalGrandCollateral_IsUnchanged()
    {
        // The 外 slot only exists on the maternal line; the paternal spelling must not move.
        Assert.AreEqual("伯祖父", Term("father", "father", "older-brother"));
        Assert.AreEqual("姑祖母", Term("father", "father", "older-sister"));
    }
}
