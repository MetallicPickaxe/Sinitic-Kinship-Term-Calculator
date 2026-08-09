using System;
using System.Collections.Generic;
using System.Linq;

using KinshipCalculator.Core.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test_Unit;

/// <summary>
/// M5 — IDENTITY DETOUR INVARIANCE. E3 of
/// the 2026-08-04 engine fixpoint contract.
///
/// The property: walking out to a relative and straight back reaches the same person, so the
/// answer must not change. 父 and 父→子→父 are the same man. This needs no external oracle — the
/// invariant is its own judge, which is why it can cover ground the two mumuy reference faces
/// cannot: neither face contains a single doubling-back chain, so this entire defect family sat
/// outside every gate the project had.
///
/// The audit swept it before the fix and found 1,422 of 6,038 detours changing the answer
/// (the 2026-08-04 identity-detour sweep). This gate is that sweep made permanent, at no
/// less coverage: every 1–3 token chain that resolves exactly, a detour at every boundary, both
/// child tokens, and the parent chosen by the GENDER of the node being detoured from.
///
/// That last part is the whole difficulty. 父→子 is not an identity — a man's son's father may be
/// him or may be someone else entirely, so 父→子→父 is only a round trip if the node really is
/// male. Gender is tracked along the chain and 配偶 flips it.
/// </summary>
[TestClass]
public class IdentityDetourInvarianceTests
{
    private static readonly String[] Tokens =
    {
        "father", "mother", "older-brother", "younger-brother",
        "older-sister", "younger-sister", "son", "daughter", "spouse"
    };

    /// <summary>Gender of the person standing at the end of a chain, ego assumed male.</summary>
    private static Boolean IsMaleAfter(IReadOnlyList<String> chain, Boolean egoMale)
    {
        Boolean male = egoMale;
        foreach (String token in chain)
        {
            male = token switch
            {
                "father" or "older-brother" or "younger-brother" or "son" => true,
                "mother" or "older-sister" or "younger-sister" or "daughter" => false,
                "spouse" => !male,
                _ => male
            };
        }

        return male;
    }

    /// <summary>
    /// What the engine said, reduced to the part that must not move: whether it resolved, and the
    /// unordered set of readings it offered. Order is not part of the property.
    /// </summary>
    private static String Signature(KinshipResult result)
        => $"{result.IsExactMatch}|" + String.Join(
            ";",
            result.Options
                .Select(o => o.Label.ZhHant + ":" + (o.IsExactMatch ? "E" : "d"))
                .OrderBy(x => x, StringComparer.Ordinal));

    private static IEnumerable<String[]> BaseChains()
    {
        foreach (String a in Tokens)
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

    [TestMethod]
    public void M5_IdentityDetoursDoNotChangeTheAnswer()
    {
        KinshipCalculator.Core.Services.KinshipCalculator calc = new();

        Int32 baseChains = 0;
        Int32 exactBases = 0;
        Int32 detours = 0;
        List<String> broken = new();

        foreach (String[] chain in BaseChains())
        {
            baseChains++;
            KinshipResult baseline = calc.Evaluate(chain, "zh-Hant", PersonGender.Male);
            if (!baseline.IsExactMatch)
            {
                continue;
            }

            exactBases++;
            String expected = Signature(baseline);

            for (Int32 position = 0; position <= chain.Length; position++)
            {
                String parent = IsMaleAfter(chain.Take(position).ToArray(), egoMale: true) ? "father" : "mother";
                foreach (String child in new[] { "son", "daughter" })
                {
                    String[] detoured = chain.Take(position)
                        .Concat(new[] { child, parent })
                        .Concat(chain.Skip(position))
                        .ToArray();

                    detours++;
                    String actual = Signature(calc.Evaluate(detoured, "zh-Hant", PersonGender.Male));
                    if (!String.Equals(actual, expected, StringComparison.Ordinal))
                    {
                        broken.Add($"{String.Join("→", chain)}  +[{child}→{parent}]@{position}\n"
                            + $"      want {expected}\n      got  {actual}");
                    }
                }
            }
        }

        Console.WriteLine($"M5 baseChains={baseChains} exactBases={exactBases} detours={detours} broken={broken.Count}");

        // Coverage floor, so a future refactor cannot quietly shrink the sweep and still pass.
        //
        // Ratchet: the audit measured 779 exact bases / 6,038 detours. Cancelling identity round
        // trips during normalisation made 10 more base chains resolve — 子→父 is 自己, 配偶→子→母 is
        // 配偶, 女→子→母 is 女兒, all 38 such chains verified by hand — and each newly-resolving base
        // brings its own detours, so the sweep itself grew to 6,114. Floors moved up to the achieved
        // numbers: coverage may grow again, but it may not fall back.
        Assert.AreEqual(819, baseChains, "the alphabet or the depth changed");
        Assert.IsTrue(exactBases >= 789, $"fewer chains resolve than when this gate was written: {exactBases}");
        Assert.IsTrue(detours >= 6114, $"detour coverage shrank: {detours}");

        Assert.AreEqual(
            0,
            broken.Count,
            $"identity detours changed the answer in {broken.Count} of {detours} cases:\n"
                + String.Join("\n", broken.Take(12)));
    }
}
