using System;
using System.Collections.Generic;
using System.Linq;

using KinshipCalculator.Core.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Calc = KinshipCalculator.Core.Services.KinshipCalculator;

namespace Test_Unit;

/// <summary>
/// E2 of the 2026-08-04 engine fixpoint contract — one person, one card.
///
/// A doubling-back chain used to reach the same relative by several routes and the engine listed
/// each route as its own "possible relation", so the reader saw one man twice and had no way to
/// tell whether that meant two men. E1's fixpoint closed the two cases the sweep named by hand;
/// these tests pin that they stay closed and that the backstop keeps genuinely different people
/// apart while doing it.
/// </summary>
[TestClass]
public class SamePersonGroupingTests
{
    private static readonly String[] Tokens =
    {
        "father", "mother", "older-brother", "younger-brother",
        "older-sister", "younger-sister", "son", "daughter", "spouse"
    };

    private static LocalizedText T(String s) => new(s, s, s);

    private static KinshipResolutionOption Option(String label, String descriptive, String simplifiedPath)
        => new(
            T(label),
            isExact: true,
            T(simplifiedPath),
            T(descriptive),
            explanation: "test",
            detailsKey: label + simplifiedPath,
            vector: RelationVector.Empty,
            alternateLabel: LocalizedText.Empty,
            descriptiveChain: T(descriptive));

    /// <summary>
    /// The two double images SWEEP_2026-08-04_ENGINE_DETOUR.md §三 named:
    /// 兄→配偶→子→母 gave 哥哥眷配偶 + 嫂嫂 (should be 嫂嫂 alone), and 姐→配偶→子→父 gave 姐夫 twice.
    /// </summary>
    [TestMethod]
    public void TheTwoDoubleImagesTheSweepNamedAreGone()
    {
        Calc calc = new();

        KinshipResult brotherWife = calc.Evaluate(
            new[] { "older-brother", "spouse", "son", "mother" }, "zh-Hant", PersonGender.Male);
        Assert.AreEqual(1, brotherWife.Options.Count,
            "兄→配偶→子→母 is one woman: " + String.Join(" / ", brotherWife.Options.Select(o => o.Label.ZhHant)));
        Assert.AreEqual("嫂嫂", brotherWife.Options[0].Label.ZhHant);

        KinshipResult sisterHusband = calc.Evaluate(
            new[] { "older-sister", "spouse", "son", "father" }, "zh-Hant", PersonGender.Male);
        Assert.AreEqual(1, sisterHusband.Options.Count,
            "姐→配偶→子→父 is one man: " + String.Join(" / ", sisterHusband.Options.Select(o => o.Label.ZhHant)));
        Assert.AreEqual("姐夫", sisterHusband.Options[0].Label.ZhHant);
    }

    /// <summary>
    /// The sweep the contract asks for: no path may put two readings on screen that the reader
    /// cannot tell apart, and no two may share a reduced form.
    /// </summary>
    [TestMethod]
    public void NoPathShowsOnePersonTwice()
    {
        Calc calc = new();
        List<String> offenders = new();
        Int32 paths = 0;

        foreach (String a in Tokens)
        {
            foreach (String[] chain in Chains(a))
            {
                paths++;
                KinshipResult r = calc.Evaluate(chain, "zh-Hant", PersonGender.Male);

                HashSet<String> shown = new(StringComparer.Ordinal);
                HashSet<String> keys = new(StringComparer.Ordinal);
                foreach (KinshipResolutionOption o in r.Options)
                {
                    if (!shown.Add(o.Label.ZhHant + " " + o.DescriptiveChain.ZhHant))
                    {
                        offenders.Add($"{String.Join("→", chain)} shows {o.Label.ZhHant} ({o.DescriptiveChain.ZhHant}) twice");
                    }

                    if (!keys.Add(o.DetailsKey))
                    {
                        offenders.Add($"{String.Join("→", chain)} repeats reduced form {o.DetailsKey}");
                    }
                }
            }
        }

        Assert.AreEqual(819, paths, "the sweep shrank");
        Assert.AreEqual(0, offenders.Count,
            $"{offenders.Count} paths show one person more than once:\n" + String.Join("\n", offenders.Take(10)));
    }

    /// <summary>
    /// The other half of E2, and the reason the vector cannot be the grouping key: these people
    /// are DIFFERENT and their vectors are IDENTICAL. Grouping must not touch them.
    /// </summary>
    [TestMethod]
    public void GenuinelyDifferentPeopleStayApart()
    {
        Calc calc = new();

        KinshipResult aunts = calc.Evaluate(
            new[] { "father", "father", "daughter" }, "zh-Hant", PersonGender.Male);
        Assert.AreEqual(2, aunts.Options.Count, "父→父→女 is two women, not one");
        CollectionAssert.AreEquivalent(
            new[] { "父的姐", "父的妹" },
            aunts.Options.Select(o => o.DescriptiveChain.ZhHant).ToArray(),
            "the 的-chain is what tells the two 姑母 apart");
        Assert.AreEqual(
            aunts.Options[0].Vector,
            aunts.Options[1].Vector,
            "if these vectors ever differ, revisit the grouping key — today they are equal, which is "
                + "precisely why the key is the 的-chain and not the vector");

        Assert.AreEqual("伯父", calc.Evaluate(new[] { "father", "older-brother" }, "zh-Hant", PersonGender.Male).Options[0].Label.ZhHant);
        Assert.AreEqual("叔父", calc.Evaluate(new[] { "father", "younger-brother" }, "zh-Hant", PersonGender.Male).Options[0].Label.ZhHant);
    }

    /// <summary>
    /// The backstop itself. It merges nothing on today's engine, so driving it through Evaluate
    /// would assert nothing at all — the earlier vacuous-test lesson. Fed a pair it IS meant to
    /// collapse, it must collapse them and keep the shorter spelling; fed a pair that only LOOKS
    /// alike, it must leave both.
    /// </summary>
    [TestMethod]
    public void TheBackstopMergesOnlyWhatTheReaderCannotTellApart()
    {
        List<KinshipResolutionOption> sameMan = new()
        {
            Option("姐夫", "姐的配偶", "姐→配偶→子→父"),
            Option("姐夫", "姐的配偶", "姐→配偶")
        };

        List<KinshipResolutionOption> merged = Calc.GroupSamePersonReadings(sameMan);
        Assert.AreEqual(1, merged.Count, "one man, one card");
        Assert.AreEqual("姐→配偶", merged[0].SimplifiedPath.ZhHant, "the shortest spelling represents the group");

        List<KinshipResolutionOption> twoAunts = new()
        {
            Option("姑母", "父的姐", "父→姐"),
            Option("姑母", "父的妹", "父→妹")
        };

        Assert.AreEqual(2, Calc.GroupSamePersonReadings(twoAunts).Count,
            "same word, different 的-chain — two different women, both must survive");
    }

    private static IEnumerable<String[]> Chains(String a)
    {
        yield return new[] { a };
        foreach (String b in Tokens)
        {
            yield return new[] { a, b };
            foreach (String c in Tokens)
            {
                yield return new[] { a, b, c };
            }
        }
    }
}
