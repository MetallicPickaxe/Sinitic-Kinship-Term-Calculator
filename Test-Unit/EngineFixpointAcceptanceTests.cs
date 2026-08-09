using System;
using System.Collections.Generic;
using System.Linq;

using KinshipCalculator.Core.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Calc = KinshipCalculator.Core.Services.KinshipCalculator;

namespace Test_Unit;

/// <summary>
/// E1 of the 2026-08-04 engine fixpoint contract, the named cases, tested directly.
///
/// The reported defect: 父→父→子→母→女 came back as five undigested 的-chains labelled
/// "Possible relation 1–5", all of them the same two women. Ask 父→父→女 on its own and the engine
/// answers 姑母 without hesitation — the folding rules knew the relation perfectly well. They just
/// never saw the reduced form, because reduction ran after them and nothing went back in.
///
/// The property these cases assert is EQUALITY WITH THE SHORT QUESTION, not a particular word:
/// burying a relation inside a chain that doubles back must not change what the engine says about
/// it. Pinning the word alone would pass just as well if both answers were wrong together.
/// </summary>
[TestClass]
public class EngineFixpointAcceptanceTests
{
    private static String Signature(KinshipResult result)
        => $"{result.IsExactMatch}|" + String.Join(
            ";",
            result.Options
                .Select(o => $"{o.Label.ZhHant}:{(o.IsExactMatch ? "E" : "d")}:{o.DescriptiveChain.ZhHant}")
                .OrderBy(x => x, StringComparer.Ordinal));

    private static void AssertSameAnswer(String[] longWay, String[] shortWay)
    {
        Calc calc = new();
        KinshipResult a = calc.Evaluate(longWay, "zh-Hant", PersonGender.Male);
        KinshipResult b = calc.Evaluate(shortWay, "zh-Hant", PersonGender.Male);

        Assert.AreEqual(
            Signature(b),
            Signature(a),
            $"{String.Join("→", longWay)} must answer exactly as {String.Join("→", shortWay)}\n"
                + $"      short: {Signature(b)}\n      long : {Signature(a)}");
    }

    [TestMethod]
    public void TheReportedChainAnswersLikeTheShortQuestion()
    {
        String[] reported = { "father", "father", "son", "mother", "daughter" };

        AssertSameAnswer(reported, new[] { "father", "father", "daughter" });
        AssertSameAnswer(reported, new[] { "father", "mother", "daughter" });

        // And what that answer IS, so a future change cannot satisfy the equality by breaking both
        // sides together: two 姑母, told apart by the 的-chain exactly as the UI already renders it.
        KinshipResult r = new Calc().Evaluate(reported, "zh-Hant", PersonGender.Male);
        Assert.IsTrue(r.IsExactMatch, "the reported chain resolves exactly now");
        Assert.AreEqual(2, r.Options.Count, "父的姐 and 父的妹 are two different women");
        CollectionAssert.AreEquivalent(
            new[] { "姑母", "姑母" },
            r.Options.Select(o => o.Label.ZhHant).ToArray());
        CollectionAssert.AreEquivalent(
            new[] { "父的姐", "父的妹" },
            r.Options.Select(o => o.DescriptiveChain.ZhHant).ToArray());
        Assert.IsTrue(r.Options.All(o => o.IsExactMatch), "both readings are exact");
    }

    [TestMethod]
    public void AnIdentityDetourAnswersLikeTheDirectRelation()
    {
        // 母→子→父: my mother's son's father. For a male ego the son may be me, and then his
        // father is my father — the case the contract names.
        AssertSameAnswer(new[] { "mother", "son", "father" }, new[] { "father" });
        Assert.AreEqual(
            "父親",
            new Calc().Evaluate(new[] { "mother", "son", "father" }, "zh-Hant", PersonGender.Male).Term.ForLanguage("zh-Hant"));
    }

    /// <summary>
    /// The cap is mandatory ("必須有迭代/深度上限"), and hitting it must DEGRADE, not throw. A chain
    /// long enough to exhaust every bound still has to come back with something a reader can use.
    /// </summary>
    [TestMethod]
    public void ExhaustingTheIterationCapDegradesInsteadOfThrowing()
    {
        Calc calc = new();
        String[] deep = Enumerable.Range(0, 40)
            .Select(i => (i % 4) switch
            {
                0 => "father",
                1 => "spouse",
                2 => "son",
                _ => "older-sister"
            })
            .ToArray();

        KinshipResult r = calc.Evaluate(deep, "zh-Hant", PersonGender.Male);

        Assert.IsTrue(r.Options.Count > 0, "a capped search still answers");
        Assert.IsFalse(
            String.IsNullOrWhiteSpace(r.Options[0].Label.ZhHant),
            "the degraded answer is a readable descriptive reading, not an empty string");
    }

    /// <summary>
    /// Cancelling one round trip exposes the next: in 子→子→父→父 the outer pair only becomes
    /// adjacent once the inner 子→父 is gone, so the rule has to repeat rather than sweep once.
    ///
    /// To be precise about what this covers — it is the loop INSIDE CancelIdentityDetours, not the
    /// outer NormalizeTokens pass loop. That outer loop is real too and 328 of the 6,114 detours
    /// depend on it, but M5 is what proves that, not this case.
    /// </summary>
    [TestMethod]
    public void ReductionRepeatsUntilNothingMoreCancels()
    {
        AssertSameAnswer(new[] { "son", "son", "father", "father" }, new[] { "son", "father" });
        Assert.AreEqual(
            "自己",
            new Calc().Evaluate(new[] { "son", "son", "father", "father" }, "zh-Hant", PersonGender.Male)
                .Term.ForLanguage("zh-Hant"),
            "two nested round trips return to me");
    }

    /// <summary>
    /// The asymmetry that makes this a sex-aware rule rather than a two-token rewrite: a woman's
    /// child's father is her husband, not her. 女→子→父 must NOT cancel.
    /// </summary>
    [TestMethod]
    public void ARoundTripThroughTheWrongSexDoesNotCancel()
    {
        Calc calc = new();

        KinshipResult throughDaughter = calc.Evaluate(
            new[] { "daughter", "son", "father" }, "zh-Hant", PersonGender.Male);
        Assert.AreNotEqual(
            "自己",
            throughDaughter.Term.ForLanguage("zh-Hant"),
            "女→子→父 is my daughter's husband, not me — cancelling it would be a sex-blind rewrite");

        // The mirror case does cancel, which is what makes the one above a real distinction.
        Assert.AreEqual(
            "自己",
            calc.Evaluate(new[] { "son", "father" }, "zh-Hant", PersonGender.Male).Term.ForLanguage("zh-Hant"));
    }
}
