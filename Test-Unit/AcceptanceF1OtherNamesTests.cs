using System;
using System.Collections.Generic;
using System.Linq;

using KinshipCalculator.Core.Models;
using KinshipCalculator.WinUI.Options;
using KinshipCalculator.WinUI.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test_Unit;

/// <summary>
/// F1 of the 2026-08-02 user-feature acceptance contract — "other names" and "possible
/// relations" must both be visible, and must not read as one undifferentiated list.
///
/// These pin the state the view binds. They are NOT the whole of F1 acceptance: the contract
/// requires the real UI to be walked as well, because a bound property proves nothing about a
/// column the user never sees. What they DO cover is every case the UI walk cannot exhaust —
/// the empty state, the multi-reading labelling, and the pruning policy.
/// </summary>
[TestClass]
public class AcceptanceF1OtherNamesTests
{
    private static MainViewModel CreateViewModel()
        => new(new KinshipCalculator.Core.Services.KinshipCalculator(), new ApplicationOptions());

    private static MainViewModel Build(String language, params String[] tokenIds)
    {
        MainViewModel vm = CreateViewModel();
        vm.SelectedLanguage = language;
        foreach (String id in tokenIds)
        {
            vm.AppendTokenCommand.Execute(vm.TokenButtons.First(t => t.Token.Id == id));
        }
        return vm;
    }

    // ---- other NAMES for one relation

    [TestMethod]
    public void OtherNames_AreShownWithTheirSourceLayer()
    {
        MainViewModel vm = Build("zh-Hant", "father");
        ResultInterpretation father = vm.ResultOptions.Single();

        Assert.AreEqual("父親", father.StandardLabel);
        Assert.IsTrue(father.HasVariants, "父親 must offer other names");
        Assert.AreEqual(Visibility.Visible, father.VariantsVisibility);

        // The WORD is content and stays Han; the SOURCE is chrome and is English (round-2 R6).
        VariantChip everyday = father.Variants.First(v => v.Term == "爸爸");
        Assert.IsTrue(everyday.HasLayer, "every chip must name where it came from");
        Assert.AreEqual("爸爸 · everyday speech", everyday.Display);
    }

    [TestMethod]
    public void NoOtherNames_RendersNothingAtAll()
    {
        // ROUND-2 U9 EXPRESSLY REPLACES the 2026-08-02 clause this test used to enforce. That
        // clause required the words 「目前沒有收錄其他稱呼」 where the chips would be, because the
        // defect of the day was a heading standing over an empty area and a reader could not tell
        // "none exist" from "the app lost them".
        //
        // The user's ruling: a sign saying nothing is here IS the clutter. The whole block —
        // heading, disclaimer, chips — is bound to one visibility, so an empty set renders no
        // block, and a heading that does not exist cannot stand over anything.
        //
        // Later audits may not read this as a regression of the old clause. The replacement is
        // written into the round-2 contract, §U9.
        MainViewModel vm = Build("zh-Hant", "father", "father", "older-brother", "son", "daughter");
        ResultInterpretation deep = vm.ResultOptions.First();

        Assert.IsFalse(deep.HasVariants, $"expected a variant-free title, got {deep.StandardLabel}");
        Assert.AreEqual(Visibility.Collapsed, deep.VariantsVisibility, "the whole block, heading included");
    }

    [TestMethod]
    public void TheSourceTagIsEnglishWhateverTheWordIs()
    {
        // R6: source and attribution tags carry settled English names for the varieties rather
        // than a literal rendering, and the visible UI does not use the word "Chinese" at all.
        ResultInterpretation father = Build("zh-Hant", "father").ResultOptions.First();
        String all = String.Join(" | ", father.Variants.Select(v => v.Display));

        Assert.AreEqual("Northern", father.Variants.First(v => v.Term == "爹").LayerName, all);
        Assert.AreEqual("Min", father.Variants.First(v => v.Term == "阿爸").LayerName, all);
        Assert.AreEqual("Yue (Cantonese)", father.Variants.First(v => v.Term == "老竇").LayerName, all);
        Assert.AreEqual("literary", father.Variants.First(v => v.Term == "阿父").LayerName, all);

        foreach (VariantChip chip in father.Variants)
        {
            Assert.IsFalse(
                chip.LayerName.Any(c => c >= 0x4E00 && c <= 0x9FFF),
                $"source tags are chrome and chrome is English: {chip.Display}");
            StringAssert.DoesNotMatch(chip.LayerName, new System.Text.RegularExpressions.Regex("Chinese"),
                $"the visible UI does not say 'Chinese': {chip.Display}");
        }
    }

    [TestMethod]
    public void TheAuditsNamedExample_嫂子_NoLongerRendersBare()
    {
        // 兄→配偶 is the case AUDIT_2026-08-02 named. 嫂子 is composed by the naming rules and
        // carried on the term table, so no layer file claims it and the lookup returns null.
        // That is a real answer — "the rules made this" — not an absence, and it now says so.
        ResultInterpretation sisterInLaw = Build("zh-Hant", "older-brother", "spouse").ResultOptions.First();
        VariantChip composed = sisterInLaw.Variants.Single(v => v.Term == "嫂子");

        Assert.IsTrue(composed.HasLayer);
        Assert.AreEqual("composed by rule", composed.LayerName);
        Assert.AreEqual("嫂子 · composed by rule", composed.Display);
        Assert.IsFalse(composed.IsGlyphVariant, "a rule-composed word is not a spelling variant");
    }

    [TestMethod]
    public void EveryOtherName_CarriesASource_SweptNotSpotChecked()
    {
        // AUDIT_2026-08-02_USER_FEATURE_ACCEPTANCE.md §三: 208 of 904 alternates reached the
        // screen with NO source label, because a term the lexicon does not register fell through
        // to String.Empty and VariantChip.Display then printed the bare word. The contract says
        // every name must say where it came from, and an unattributed chip is precisely the one
        // a reader cannot check.
        //
        // Swept, not spot-checked. The first round tested 父親 and passed while a fifth of the
        // corpus was bare; the defect lived in the fall-through, which only a sweep reaches.
        MainViewModel seed = CreateViewModel();
        String[] tokens = seed.TokenButtons.Select(t => t.Token.Id).ToArray();

        Int32 chips = 0;
        Int32 ruleComposed = 0;
        List<String> bare = new();
        foreach (String language in new[] { "zh-Hant" })
        {
            const String expectedFallback = "composed by rule";
            foreach (String first in tokens)
            {
                foreach (String second in tokens)
                {
                    MainViewModel vm = Build(language, first, second);
                    foreach (ResultInterpretation option in vm.ResultOptions)
                    {
                        foreach (VariantChip chip in option.Variants)
                        {
                            chips++;
                            if (!chip.HasLayer)
                            {
                                bare.Add($"{language} {first}->{second}: {chip.Term}");
                            }
                            else if (chip.LayerName == expectedFallback)
                            {
                                ruleComposed++;
                            }
                        }
                    }
                }
            }
        }

        Assert.IsTrue(chips > 200, $"the sweep must actually surface chips, got {chips}");
        Assert.AreEqual(0, bare.Count, $"chips with no source at all: {String.Join(" | ", bare.Take(10))}");
        // Without this the test would pass vacuously if the fall-through were ever removed:
        // these are the very chips that used to render bare.
        Assert.IsTrue(ruleComposed > 0, "the rule-composed branch must be exercised by this sweep");
    }

    [TestMethod]
    public void AnEnglishSessionNeverPrintsHanWhereTheNamesGo()
    {
        // The rule this pins was learned the hard way. The English column WAS empty for every
        // relation while the notice claimed nothing was recorded, so an attempt to fill it copied
        // the Traditional set across — and an English interface that prints 爸爸 · 老爸 · 爹 is
        // not an English interface. Reverted. This is the guard against doing it again.
        MainViewModel seed = CreateViewModel();
        String[] tokens = seed.TokenButtons.Select(t => t.Token.Id).ToArray();

        List<String> han = new();
        Int32 chips = 0;
        foreach (String first in tokens)
        {
            foreach (String second in tokens)
            {
                foreach (ResultInterpretation option in Build("en", first, second).ResultOptions)
                {
                    foreach (VariantChip chip in option.Variants)
                    {
                        chips++;
                        if (chip.Display.Any(c => c >= 0x4E00 && c <= 0x9FFF))
                        {
                            han.Add($"{first}->{second}: {chip.Display}");
                        }
                    }
                }
            }
        }

        Assert.AreEqual(0, han.Count, $"Han in an English session: {String.Join(" | ", han.Take(8))} (of {chips} chips)");
    }

    [TestMethod]
    public void TheReadingLabelIsChromeAndReadsEnglish()
    {
        // R5 boundary: the numbered reading label is chrome, so it is English, while the 的-chain
        // beside it is a Chinese description of the relation and stays Han. Both live on the same
        // card, which is exactly why the line has to be drawn deliberately.
        MainViewModel twoAunts = Build("zh-Hant", "father", "father", "daughter");
        Assert.AreEqual(2, twoAunts.ResultOptions.Count);

        Assert.AreEqual("Possible relation 1 of 2", twoAunts.ResultOptions[0].ReadingLabel);
        Assert.AreEqual("Possible relation 2 of 2", twoAunts.ResultOptions[1].ReadingLabel);
        CollectionAssert.AreEquivalent(
            new[] { "父的姐", "父的妹" },
            twoAunts.ResultOptions.Select(o => o.DescriptiveChain).ToArray());
    }

    [TestMethod]
    public void TheChineseSessionsKeepEveryOtherName()
    {
        // Guards the restructuring that came out of the English investigation. Merging the layer
        // variants used to bail early for a relation the lexicon does not key, which dropped the
        // names the FORMATTER had composed for it — 嫂子, 弟妹, 外孫. The two early returns became
        // one path; both scripts must carry the same set, name for name.
        MainViewModel seed = CreateViewModel();
        String[] tokens = seed.TokenButtons.Select(t => t.Token.Id).ToArray();

        List<String> lost = new();
        Int32 namesChecked = 0;
        foreach (String first in tokens)
        {
            foreach (String second in tokens)
            {
                String[] hant = Build("zh-Hant", first, second).ResultOptions
                    .SelectMany(o => o.Variants.Select(v => v.Term)).ToArray();
                String[] hans = Build("zh-Hans", first, second).ResultOptions
                    .SelectMany(o => o.Variants.Select(v => v.Term)).ToArray();

                namesChecked += hant.Length;
                if (hant.Length != hans.Length)
                {
                    lost.Add($"{first}->{second}: zh-Hant {hant.Length}, zh-Hans {hans.Length}");
                }
            }
        }

        Assert.IsTrue(namesChecked > 200, $"the sweep must actually see names, got {namesChecked}");
        Assert.AreEqual(0, lost.Count, $"the two scripts disagree: {String.Join(" | ", lost.Take(8))}");
    }

    [TestMethod]
    public void TheSourceTagDoesNotFollowTheContentScript()
    {
        // What this replaces: a test that pinned per-script layer labels, from the round where the
        // tag followed the interface language. Round-2 R5/R6 withdrew the choice — one content
        // script, one chrome language — so the tag is the same string no matter what, and the
        // thing worth pinning is that it does NOT vary.
        //
        // The old test earned its keep first: writing it is what surfaced a Simplified session
        // reading Traditional layer labels. That defect cannot recur, because there is now exactly
        // one label to be wrong about.
        VariantChip FromScript(String script)
            => Build(script, "father").ResultOptions.First().Variants.First(v => v.Term == "爸爸");

        Assert.AreEqual("爸爸 · everyday speech", FromScript("zh-Hant").Display);
        Assert.AreEqual(FromScript("zh-Hant").LayerName, FromScript("zh-Hans").LayerName);
    }

    // ---- different possible RELATIONS

    [TestMethod]
    public void SeveralRelationReadings_AreLabelledAndTellableApart()
    {
        // 父→父→子 is my father's brother, and the engine cannot know whether he is older or
        // younger: two genuine readings, 伯父 and 叔父.
        MainViewModel vm = Build("zh-Hant", "father", "father", "son");
        Assert.AreEqual(2, vm.ResultOptions.Count);

        Assert.AreEqual("Possible relation 1 of 2", vm.ResultOptions[0].ReadingLabel);
        Assert.AreEqual("Possible relation 2 of 2", vm.ResultOptions[1].ReadingLabel);
        Assert.AreEqual(Visibility.Visible, vm.ResultOptions[0].ReadingLabelVisibility);

        CollectionAssert.AreEquivalent(
            new[] { "伯父", "叔父" },
            vm.ResultOptions.Select(o => o.StandardLabel).ToArray());
    }

    [TestMethod]
    public void TheLineUnderTheTermIsAName_NotEngineCoordinates()
    {
        // It used to read OfficialDescription, the engine's own structural coordinates, and it
        // showed: 姑祖母 was captioned "Self → ancestor +2 sibling line (female)" and 父親 was
        // captioned nothing at all. Swept over every one- and two-token path here; the original
        // three-token sweep measured 266 of 955 empty and 244 more machine-shaped.
        MainViewModel seed = CreateViewModel();
        String[] tokens = seed.TokenButtons.Select(t => t.Token.Id).ToArray();

        List<String> bad = new();
        Int32 checkedNames = 0;
        foreach (String first in tokens)
        {
            foreach (String second in tokens)
            {
                foreach (ResultInterpretation o in Build("zh-Hant", first, second).ResultOptions)
                {
                    checkedNames++;
                    if (String.IsNullOrWhiteSpace(o.OfficialLabel))
                    {
                        bad.Add($"{first}->{second} ({o.StandardLabel}): empty");
                    }
                    else if (o.OfficialLabel.Contains('→') || o.OfficialLabel.Contains('+')
                        || o.OfficialLabel.Contains("ancestor") || o.OfficialLabel.Contains("descendant"))
                    {
                        bad.Add($"{first}->{second} ({o.StandardLabel}): '{o.OfficialLabel}'");
                    }
                }
            }
        }

        Assert.IsTrue(checkedNames >= 81, $"the sweep must cover every two-token path, got {checkedNames}");
        Assert.AreEqual(0, bad.Count, $"engine coordinates where a name belongs: {String.Join(" | ", bad.Take(8))}");

        // The named cases the old field got wrong.
        Assert.AreEqual("Grandaunt", Build("zh-Hant", "father", "father", "older-sister").ResultOptions.First().OfficialLabel);
        Assert.AreEqual("Grandfather", Build("zh-Hant", "father", "father").ResultOptions.First().OfficialLabel);
        Assert.AreEqual("Step-mother", Build("zh-Hant", "father", "spouse").ResultOptions.First().OfficialLabel);
        Assert.AreEqual("Father", Build("zh-Hant", "father").ResultOptions.First().OfficialLabel);

        // And it hides rather than repeat the term it sits under.
        Assert.AreEqual(Visibility.Collapsed, Build("en", "father").ResultOptions.First().EnglishNameVisibility);
        Assert.AreEqual(Visibility.Visible, Build("zh-Hant", "father").ResultOptions.First().EnglishNameVisibility);
    }

    [TestMethod]
    public void TheChainIsShownExactlyWhereItSeparatesTwoReadings()
    {
        // The 的-chain stopped printing under every result — it was restating the path line — and
        // now appears only where it is the one thing telling two readings apart. Which means the
        // FLAG deciding that is now load-bearing, and it shipped broken: the constructor took the
        // parameter and never assigned it, so it read false everywhere, the chain never rendered,
        // and 姑母 appeared twice with nothing to separate them. The compiler is happy to accept
        // an unused parameter, so this is the thing that has to catch it.
        MainViewModel twoAunts = Build("zh-Hant", "father", "father", "daughter");
        Assert.AreEqual(2, twoAunts.ResultOptions.Count);
        Assert.IsTrue(
            twoAunts.ResultOptions.All(o => o.ChainDisambiguates),
            "both readings are 姑母, so both must show the chain that separates them");
        Assert.IsTrue(twoAunts.ResultOptions.All(o => o.DisambiguationVisibility == Visibility.Visible));
        CollectionAssert.AreEquivalent(
            new[] { "父的姐", "父的妹" },
            twoAunts.ResultOptions.Select(o => o.DescriptiveChain).ToArray());

        // Two readings with DIFFERENT words separate themselves; the chain would be noise.
        MainViewModel twoUncles = Build("zh-Hant", "father", "father", "son");
        Assert.AreEqual(2, twoUncles.ResultOptions.Count);
        Assert.IsTrue(twoUncles.ResultOptions.All(o => !o.ChainDisambiguates), "伯父 and 叔父 are already distinct");

        // And a single reading never shows it.
        Assert.IsFalse(Build("zh-Hant", "father").ResultOptions.Single().ChainDisambiguates);
    }

    [TestMethod]
    public void ReadingsSharingOneTerm_AreStillTellableApart()
    {
        // 父→父→女 gives 姑母 twice — the father's elder sister and his younger sister are two
        // different people wearing one word. The reading label alone would not separate them,
        // so the documentary chain must, and it is what the third column shows.
        MainViewModel vm = Build("zh-Hant", "father", "father", "daughter");
        Assert.AreEqual(2, vm.ResultOptions.Count);
        Assert.AreEqual(vm.ResultOptions[0].StandardLabel, vm.ResultOptions[1].StandardLabel);

        CollectionAssert.AreEquivalent(
            new[] { "父的姐", "父的妹" },
            vm.ResultOptions.Select(o => o.DescriptiveChain).ToArray());
    }

    [TestMethod]
    public void SingleReading_CarriesNoReadingLabel()
    {
        // The label is noise when there is nothing to choose between.
        ResultInterpretation only = Build("zh-Hant", "father").ResultOptions.Single();
        Assert.AreEqual(String.Empty, only.ReadingLabel);
        Assert.AreEqual(Visibility.Collapsed, only.ReadingLabelVisibility);
    }

    // ---- the pruning policy, stated and pinned

    private static KinshipResolutionOption Option(String label, Boolean isExact)
        => new(
            new LocalizedText(label, label, label),
            isExact,
            LocalizedText.Empty,
            LocalizedText.Empty,
            "probe",
            label,
            RelationVector.Empty);

    [TestMethod]
    public void ExactPruning_DropsNonExactReadingsWhenAnExactOneIsPresent()
    {
        // Asserted on the POLICY ITSELF, not on a chain that happens to come out right.
        //
        // The previous version of this test was vacuous, as the review of 2026-08-02 showed: it
        // checked that the exact case ends up all-exact — true whether or not anything was
        // pruned — and that the "nothing resolves" case returns at least one option, while the
        // long chain it picked for that case actually resolves EXACTLY, so the second branch was
        // never reached. Both assertions survived unconditional pruning, which is the mistake
        // the policy exists to prevent.
        List<KinshipResolutionOption> mixed = KinshipCalculator.Core.Services.KinshipCalculator
            .ApplyExactPruningPolicy(new[] { Option("raw echo", false), Option("伯父", true), Option("another echo", false) });

        CollectionAssert.AreEqual(
            new[] { "伯父" },
            mixed.Select(o => o.Label.ZhHant).ToArray(),
            "a non-exact reading beside an exact one is the same person's unreduced chain");
    }

    [TestMethod]
    public void ExactPruning_KeepsDescriptiveReadingsWhenNothingResolves()
    {
        // The half the old test never reached. Remove the "only when an exact exists" guard and
        // this goes red — the user would be handed an empty result for every relation the engine
        // cannot name.
        List<KinshipResolutionOption> noneExact = KinshipCalculator.Core.Services.KinshipCalculator
            .ApplyExactPruningPolicy(new[] { Option("父的兄的女的夫", false), Option("父的兄的女婿", false) });

        Assert.AreEqual(2, noneExact.Count, "with nothing exact, every descriptive reading must survive");
        CollectionAssert.AreEquivalent(
            new[] { "父的兄的女的夫", "父的兄的女婿" },
            noneExact.Select(o => o.Label.ZhHant).ToArray());
    }

    [TestMethod]
    public void ExactPruning_LeavesSingletonsAndAllExactSetsAlone()
    {
        Assert.AreEqual(1, KinshipCalculator.Core.Services.KinshipCalculator
            .ApplyExactPruningPolicy(new[] { Option("only", false) }).Count, "a lone descriptive reading is the answer");
        Assert.AreEqual(1, KinshipCalculator.Core.Services.KinshipCalculator
            .ApplyExactPruningPolicy(new[] { Option("only", true) }).Count);
        Assert.AreEqual(2, KinshipCalculator.Core.Services.KinshipCalculator
            .ApplyExactPruningPolicy(new[] { Option("伯父", true), Option("叔父", true) }).Count,
            "two genuine exact readings are two people, not an echo");
    }

    [TestMethod]
    public void ExactPruning_IsWhatTheEngineActuallyRuns()
    {
        // Ties the pure function back to production, so extracting it cannot drift from Evaluate.
        KinshipCalculator.Core.Services.KinshipCalculator calc = new();
        KinshipResult twoUncles = calc.Evaluate(new[] { "father", "father", "son" }, "zh-Hant", PersonGender.Male);
        Assert.IsTrue(twoUncles.Options.All(o => o.IsExactMatch));
        Assert.AreEqual(2, twoUncles.Options.Count, "伯父 and 叔父 are two readings, and pruning must not merge them");
    }
}
