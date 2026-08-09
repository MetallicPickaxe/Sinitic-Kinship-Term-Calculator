using System;
using System.Linq;

using KinshipCalculator.Core.Models;
using KinshipCalculator.WinUI.Options;
using KinshipCalculator.WinUI.ViewModels;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test_Unit;

/// <summary>
/// Round 3 of the 2026-08-04 UI acceptance contract — V1 copy, V2 per-press origin,
/// V3 grouped other-names.
///
/// Deliberately thin, per §三 (inheriting round-2 §四.3): the visual half is the user's to judge
/// and no assertion here pretends otherwise. What IS pinned is the structural half — which words
/// the variant menu offers, which token sequence each choice lands, that nothing survives a press,
/// and that grouping moves the attribution without losing it.
/// </summary>
[TestClass]
public class AcceptanceRound3Tests
{
    private static MainViewModel New()
        => new(new KinshipCalculator.Core.Services.KinshipCalculator(), new ApplicationOptions());

    private static TokenDisplay Key(MainViewModel vm, String id)
        => vm.TokenButtons.First(t => t.Token.Id == id);

    // ---- V2: the three forms live on the keys, per press

    [TestMethod]
    public void OnlyTheFourKeysTheEngineCanVaryOfferVariants()
    {
        MainViewModel vm = New();

        foreach (String id in new[] { "father", "mother", "son", "daughter" })
        {
            Assert.IsTrue(Key(vm, id).HasVariants, $"{id} must offer its three forms");
            Assert.AreEqual(3, Key(vm, id).Variants.Count);
        }

        // The other five have no alternate form in the engine, so offering a menu would be a lie.
        foreach (String id in new[] { "older-brother", "younger-brother", "older-sister", "younger-sister", "spouse" })
        {
            Assert.IsFalse(Key(vm, id).HasVariants, $"{id} has no variants to offer");
        }
    }

    [TestMethod]
    public void TheMenuOffersRealWordsNotModeNames()
    {
        // V2: 「Adoption」 asked the reader to hold a category in their head and apply it to
        // whatever they pressed next. 養父 does not.
        MainViewModel vm = New();

        CollectionAssert.AreEqual(
            new[] { "父", "養父", "繼父" },
            Key(vm, "father").Variants.Select(v => v.Label).ToArray());
        CollectionAssert.AreEqual(
            new[] { "母", "養母", "繼母" },
            Key(vm, "mother").Variants.Select(v => v.Label).ToArray());
        CollectionAssert.AreEqual(
            new[] { "子", "養子", "繼子" },
            Key(vm, "son").Variants.Select(v => v.Label).ToArray());
        CollectionAssert.AreEqual(
            new[] { "女", "養女", "繼女" },
            Key(vm, "daughter").Variants.Select(v => v.Label).ToArray());
    }

    [TestMethod]
    public void EachVariantLandsTheSequenceItsWordPromises()
    {
        // What V2 controls is the SEQUENCE the press lands, so that is what this pins. The two
        // alternates reach the engine by completely different routes: 養X is its own token, 繼X is
        // a rewrite through the marriage chain.
        MainViewModel vm = New();

        vm.AppendVariantCommand.Execute(Key(vm, "father").Variants[1]);
        Assert.AreEqual("自己→養父", vm.PathText);

        vm.ClearCommand.Execute(null);
        vm.AppendVariantCommand.Execute(Key(vm, "father").Variants[2]);
        Assert.AreEqual("自己→母→配偶", vm.PathText, "繼父 is the mother's current husband");

        vm.ClearCommand.Execute(null);
        vm.AppendVariantCommand.Execute(Key(vm, "mother").Variants[2]);
        Assert.AreEqual("自己→父→配偶", vm.PathText);

        vm.ClearCommand.Execute(null);
        vm.AppendVariantCommand.Execute(Key(vm, "son").Variants[2]);
        Assert.AreEqual("自己→配偶→子", vm.PathText, "繼子 is the spouse's child");

        vm.ClearCommand.Execute(null);
        vm.AppendVariantCommand.Execute(Key(vm, "daughter").Variants[1]);
        Assert.AreEqual("自己→養女", vm.PathText);
    }

    [TestMethod]
    public void AllEightVariantFormsAreNamed()
    {
        // E4 of ACCEPTANCE_2026-08-04_ENGINE_FIXPOINT.md closed the asymmetry this test used to
        // RECORD. Round-3 inherited an engine freeze, so it could only pin the defect: upward the
        // three forms came back as three words, downward all four collapsed — pick 養子 or 繼子 and
        // the answer was 兒子 either way. It looked like the menu had not fired, though the path
        // line above it plainly read 自己→養子; the sequence was always right and the NAMING was
        // what had no such words.
        //
        // Both halves are composed, not looked up (`Resource/Data/Lexicon` stayed at zero change),
        // and they are opened for these four words only: 養子的子 and 配偶的子的配偶 keep the legacy
        // naming they have always had.
        MainViewModel vm = New();

        foreach ((String Key, Int32 Index, String Expected) probe in new[]
        {
            ("father", 1, "養父"), ("father", 2, "繼父"),
            ("mother", 1, "養母"), ("mother", 2, "繼母"),
            ("son", 1, "養子"), ("son", 2, "繼子"),
            ("daughter", 1, "養女"), ("daughter", 2, "繼女")
        })
        {
            vm.ClearCommand.Execute(null);
            vm.AppendVariantCommand.Execute(Key(vm, probe.Key).Variants[probe.Index]);
            Assert.AreEqual(
                probe.Expected,
                vm.ResultText,
                $"{probe.Key} variant {probe.Index} must name {probe.Expected}, and the path was {vm.PathText}");
        }
    }

    [TestMethod]
    public void APlainTapIsStillTheBirthRelation()
    {
        MainViewModel vm = New();
        vm.AppendTokenCommand.Execute(Key(vm, "father"));
        Assert.AreEqual("父親", vm.ResultText);
        Assert.AreEqual("自己→父", vm.PathText);
    }

    [TestMethod]
    public void NothingSurvivesTheKeyPress()
    {
        // THE POINT OF V2. The old radio row held a mode, spent it on the next press, and reset
        // itself — so the control could be showing something other than what the next press would
        // do. There is no mode now: a variant press affects that press and the next plain tap is
        // an ordinary one, with no toggle anywhere to disagree with.
        MainViewModel vm = New();

        vm.AppendVariantCommand.Execute(Key(vm, "father").Variants[1]);   // 養父
        vm.AppendTokenCommand.Execute(Key(vm, "father"));                  // plain 父
        Assert.AreEqual("自己→養父→父", vm.PathText);

        vm.ClearCommand.Execute(null);
        vm.AppendTokenCommand.Execute(Key(vm, "father"));
        vm.AppendVariantCommand.Execute(Key(vm, "mother").Variants[1]);   // 養母
        Assert.AreEqual("自己→父→養母", vm.PathText);
    }

    // ---- V3: sources become headings, printed once

    [TestMethod]
    public void OtherNamesAreGroupedByTheirSourceWithNoRepeatedLabel()
    {
        MainViewModel vm = New();
        vm.AppendTokenCommand.Execute(Key(vm, "father"));
        ResultInterpretation father = vm.ResultOptions.First();

        String[] headers = father.VariantGroups.Select(g => g.Header).ToArray();
        CollectionAssert.AllItemsAreUnique(headers, $"a label may head only one section: {String.Join(" | ", headers)}");
        Assert.IsTrue(headers.Length >= 5, $"父親 draws on several sources, got {headers.Length}");

        // Every name still sits under a heading that says where it came from: the attribution
        // moved off the chip, it did not go away.
        Assert.AreEqual(
            father.Variants.Count,
            father.VariantGroups.Sum(g => g.Chips.Count),
            "grouping must not drop a name");
        foreach (VariantGroup group in father.VariantGroups)
        {
            Assert.IsFalse(String.IsNullOrWhiteSpace(group.Header));
            Assert.IsTrue(group.Chips.Count > 0, "an empty section must not exist");
        }
    }

    [TestMethod]
    public void TheTwoNonLayerSourcesSortLast()
    {
        // "variant glyph" and "composed by rule" are statements about the entry rather than
        // places it is said, so they follow the regional and register layers.
        MainViewModel vm = New();
        foreach (String id in new[] { "older-brother", "son" })
        {
            vm.AppendTokenCommand.Execute(Key(vm, id));
        }

        String[] headers = vm.ResultOptions.First().VariantGroups.Select(g => g.Header).ToArray();
        Int32 glyph = Array.IndexOf(headers, "variant glyph");
        Assert.IsTrue(glyph >= 0, $"姪子 offers its other spelling: {String.Join(" | ", headers)}");
        Assert.IsTrue(
            headers.Take(glyph).All(h => h != "composed by rule"),
            $"composed by rule sorts after variant glyph: {String.Join(" | ", headers)}");
        Assert.AreEqual(headers.Length - 1, Array.IndexOf(headers, headers.Last()));
    }

    [TestMethod]
    public void NoOtherNamesMeansNoGroups()
    {
        // U9 still holds: nothing recorded renders nothing, sections included.
        MainViewModel vm = New();
        foreach (String id in new[] { "father", "father", "older-brother", "son", "daughter" })
        {
            vm.AppendTokenCommand.Execute(Key(vm, id));
        }

        ResultInterpretation deep = vm.ResultOptions.First();
        Assert.IsFalse(deep.HasVariants);
        Assert.AreEqual(0, deep.VariantGroups.Count);
    }
}
