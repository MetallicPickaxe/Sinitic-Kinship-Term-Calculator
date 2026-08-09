using System;
using System.Linq;

using KinshipCalculator.Core.Models;
using KinshipCalculator.Core.Services.Formatting;
using KinshipCalculator.WinUI.Options;
using KinshipCalculator.WinUI.ViewModels;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test_Unit;

/// <summary>
/// F3 of the 2026-08-02 user-feature acceptance contract — the 侄 / 姪 glyph policy.
///
/// Scope is the GLYPH LAYER ONLY. Nothing here may change kinship derivation, and the reverse
/// lookup that would consume the normalisation contract is deferred — what is required now is
/// that the contract exists, is tested, and that a Traditional reader can still reach 侄.
/// </summary>
[TestClass]
public class AcceptanceF3GlyphTests
{
    private static MainViewModel Build(String language, params String[] tokenIds)
    {
        MainViewModel vm = new(new KinshipCalculator.Core.Services.KinshipCalculator(), new ApplicationOptions())
        {
            SelectedLanguage = language
        };
        foreach (String id in tokenIds)
        {
            vm.AppendTokenCommand.Execute(vm.TokenButtons.First(t => t.Token.Id == id));
        }
        return vm;
    }

    private static String Primary(String language, params String[] tokenIds)
        => Build(language, tokenIds).ResultOptions.First().StandardLabel;

    // ---- one relation, two spellings — never two relations

    [TestMethod]
    public void TraditionalLeadsWith姪_SimplifiedWith侄()
    {
        Assert.AreEqual("姪子", Primary("zh-Hant", "older-brother", "son"));
        Assert.AreEqual("侄子", Primary("zh-Hans", "older-brother", "son"));
        Assert.AreEqual("姪女", Primary("zh-Hant", "older-brother", "daughter"));
        Assert.AreEqual("侄女", Primary("zh-Hans", "older-brother", "daughter"));
    }

    [TestMethod]
    public void TheSamePairingHoldsOnACompound()
    {
        // One compound, as the contract asks, so the policy is shown to survive composition
        // rather than being special-cased on the bare word.
        Assert.AreEqual("堂姪子", Primary("zh-Hant", "father", "older-brother", "son", "son"));
        Assert.AreEqual("堂侄子", Primary("zh-Hans", "father", "older-brother", "son", "son"));
    }

    [TestMethod]
    public void BothSpellingsAreTheSameWord()
    {
        Assert.IsTrue(KinshipGlyphVariants.AreSameWord("侄子", "姪子"));
        Assert.IsTrue(KinshipGlyphVariants.AreSameWord("侄女", "姪女"));
        Assert.IsTrue(KinshipGlyphVariants.AreSameWord("堂侄孫", "堂姪孫"));

        // And it must not over-reach: two genuinely different relations stay different.
        Assert.IsFalse(KinshipGlyphVariants.AreSameWord("姪子", "姪女"));
        Assert.IsFalse(KinshipGlyphVariants.AreSameWord("姪子", "外甥"));
    }

    [TestMethod]
    public void EitherSpellingNormalisesToOneKey()
    {
        // The reusable half the deferred reverse lookup will need: whichever way the user
        // writes it, the lookup key is the same.
        Assert.AreEqual(KinshipGlyphVariants.Normalize("侄女"), KinshipGlyphVariants.Normalize("姪女"));
        Assert.AreEqual("姪女", KinshipGlyphVariants.Normalize("侄女"));
        Assert.AreEqual("姪女", KinshipGlyphVariants.Normalize("姪女"));
        // A word with no settled variant character passes through untouched.
        Assert.AreEqual("外甥女", KinshipGlyphVariants.Normalize("外甥女"));
        Assert.IsNull(KinshipGlyphVariants.TryGetAlternateSpelling("外甥女"));
    }

    // ---- the Traditional reader must still be able to reach 侄

    [TestMethod]
    public void TraditionalUiStillOffers侄AsAVariantGlyph()
    {
        // Before this, the global ToHant rewrote every 侄 to 姪 on the way to the screen, so the
        // spelling simply did not exist in a Traditional session.
        ResultInterpretation nephew = Build("zh-Hant", "older-brother", "son").ResultOptions.First();

        VariantChip glyph = nephew.Variants.Single(v => v.IsGlyphVariant);
        Assert.AreEqual("侄子", glyph.Term);
        // Round-2 R6: the source tag is chrome, so it is English whatever the word beside it is.
        Assert.AreEqual("variant glyph", glyph.LayerName);
        Assert.AreEqual("侄子 · variant glyph", glyph.Display);
    }

    [TestMethod]
    public void SimplifiedUiOffers姪TheSameWay()
    {
        ResultInterpretation nephew = Build("zh-Hans", "older-brother", "daughter").ResultOptions.First();

        VariantChip glyph = nephew.Variants.Single(v => v.IsGlyphVariant);
        Assert.AreEqual("姪女", glyph.Term);
        Assert.AreEqual("variant glyph", glyph.LayerName);
    }

    [TestMethod]
    public void TheGlyphChipComesFirst()
    {
        // Ahead of the dialect words: it is the same word rather than another one, and last
        // place put it below the fold of the scrolling chip list — for the very question that
        // opened this item, invisible is the same as absent.
        ResultInterpretation nephew = Build("zh-Hant", "older-brother", "son").ResultOptions.First();
        Assert.IsTrue(nephew.Variants[0].IsGlyphVariant, $"first chip was {nephew.Variants[0].Display}");
        Assert.AreEqual("侄子", nephew.Variants[0].Term);
    }

    [TestMethod]
    public void TheGlyphChipIsNotMixedInWithDialectNames()
    {
        // Three concepts, three appearances (contract cross-cutting rule). A glyph chip must be
        // marked as such so it cannot read as a regional word.
        ResultInterpretation nephew = Build("zh-Hant", "older-brother", "son").ResultOptions.First();

        Assert.AreEqual(1, nephew.Variants.Count(v => v.IsGlyphVariant));
        Assert.IsTrue(nephew.Variants.Any(v => !v.IsGlyphVariant), "dialect and register names must still be there");
        foreach (VariantChip dialect in nephew.Variants.Where(v => !v.IsGlyphVariant))
        {
            Assert.AreNotEqual("異體字形", dialect.LayerName, $"{dialect.Term} is a word, not a spelling");
        }
    }

    [TestMethod]
    public void RelationsWithNoVariantGlyphGetNoGlyphChip()
    {
        // The chip appears only where a settled pair applies — it is not decoration.
        ResultInterpretation father = Build("zh-Hant", "father").ResultOptions.First();
        Assert.IsFalse(father.Variants.Any(v => v.IsGlyphVariant));
    }

    // ---- the 女 radical is not a gender marker

    [TestMethod]
    public void TheFemaleRadicalDoesNotMeanTheRelativeIsFemale()
    {
        // 姪子 is male and 姪女 female: the gender is carried by 子 / 女, not by the radical in
        // 姪. Pinning both sides keeps any future "tidy-up" from splitting them by gender.
        Assert.AreEqual("姪子", Primary("zh-Hant", "older-brother", "son"));
        Assert.AreEqual("姪女", Primary("zh-Hant", "older-brother", "daughter"));
        Assert.AreEqual("姪子", Primary("zh-Hant", "younger-brother", "son"));
        Assert.AreEqual("姪女", Primary("zh-Hant", "younger-brother", "daughter"));
    }

    [TestMethod]
    public void NoUiTextExplainsTheRadicalAsGender()
    {
        // The contract forbids UI copy that reads the 女 radical as "this person is female".
        // Nothing in the shipped view model text may say so.
        MainViewModel vm = Build("zh-Hant", "older-brother", "son");
        String allText = String.Join(
            " ",
            new[] { vm.ResultText, vm.PathText, vm.RawChainText }
                .Concat(vm.ResultOptions.SelectMany(o => new[] { o.ReadingLabel, o.Explanation, o.OfficialLabel, o.DescriptiveChain }))
                .Concat(vm.ResultOptions.SelectMany(o => o.Variants.Select(v => v.Display))));

        foreach (String forbidden in new[] { "女字旁", "女性", "female radical" })
        {
            StringAssert.DoesNotMatch(allText, new System.Text.RegularExpressions.Regex(forbidden),
                $"UI text must not explain the radical as gender: {allText}");
        }
    }
}
