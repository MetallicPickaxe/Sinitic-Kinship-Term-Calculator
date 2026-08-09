using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using KinshipCalculator.Core.Data;
using KinshipCalculator.Core.Models;
using KinshipCalculator.Core.Services.Formatting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test_Verification;

/// <summary>
/// A lexicon layer answers through <see cref="KinshipLexiconLayers.GetVariants"/>, keyed by the
/// STANDARD form the engine computes. A layer keyed on anything else is dead data: the file
/// parses, the entries load, the reverse lookup reports them — and no query ever reaches them.
/// Nothing else in the suite notices, because a missing variant looks exactly like a relation
/// that legitimately has no everyday word.
///
/// This test closes that hole. It drives the calculator over a fixed chain corpus, collects
/// every standard form the engine actually emits, and requires every shipped variant key to be
/// in that set. It also writes the emitted forms to <c>lexicon-reachable-standards.tsv</c> in
/// the test output directory, which is the table a lexicon-authoring script should key against.
/// </summary>
[TestClass]
public sealed class LexiconKeyReachabilityTests
{
    private const string ProbeFileName = "lexicon-reachable-standards.tsv";

    /// <summary>
    /// Blood/marriage tokens only — the adoptive tokens produce 养-prefixed computed titles that
    /// carry no looked-up vocabulary by design (see FormatAncestor).
    /// </summary>
    private static readonly string[] BaseTokens =
    [
        "father", "mother",
        "older-brother", "younger-brother", "older-sister", "younger-sister",
        "son", "daughter", "spouse"
    ];

    /// <summary>
    /// Depth 4 is needed for the 曾祖-generation collaterals (伯曾祖父 = F.F.F.OB) but a full
    /// 9^4 sweep is 6561 chains of mostly-unreachable shapes. Every four-token chain that names
    /// a lexicalised relation starts with two ascent/marriage hops, so the fourth level is
    /// restricted to those openings. The restriction is part of the corpus definition, not a
    /// sampling shortcut: a key outside it is reported, never silently passed.
    /// </summary>
    private static readonly string[] DeepOpeners = ["father", "mother", "spouse"];

    /// <summary>
    /// The lexicalised deep shapes are all "k ascents, one sibling branch, optional marriage",
    /// optionally entered through the spouse (伯曾祖母 = F.F.F.OB.SP is five tokens, 伯岳母 =
    /// SP.F.OB.SP four). Enumerating that family directly reaches them without a 9^5 sweep.
    /// </summary>
    private static readonly string[] Ascents = ["father", "mother"];

    private static readonly string[] Branches = ["older-brother", "younger-brother", "older-sister", "younger-sister"];

    private static readonly string[] Descents = ["son", "daughter"];

    private const int MaxAscent = 4;

    /// <summary>
    /// The 堂 / 表 family: k ascents, one sibling branch, then m descents, optionally married in
    /// (堂姪孫, 表外孫女, 堂姪媳). Together with the pure descent lines (玄孫 and below) this is
    /// where the deep half of the reference face lives, and none of it is reachable from the
    /// depth-limited sweeps above.
    /// </summary>
    private const int MaxCollateralAscent = 3;

    private const int MaxDescent = 3;

    private const int MaxPureDescent = 6;

    public TestContext? TestContext { get; set; }

    [TestMethod]
    public void EveryShippedVariantKey_ActuallySurfacesInBothScripts()
    {
        SweepResult sweep = Sweep();

        File.WriteAllText(
            Path.Combine(AppContext.BaseDirectory, ProbeFileName),
            string.Join('\n', sweep.ProbeRows) + '\n',
            new UTF8Encoding(false));

        // Serving, not spelling. Asking "is the key a form the engine emits" would still pass a
        // key written in the wrong script (孫子 when the query site builds 孙子). Asking whether
        // any of its VARIANTS ever came out of the calculator proves the entry is wired end to
        // end — and per script, because a Hans-only key leaves Hant users with a blank column.
        HashSet<string> standardsHans = sweep.Standards.Select(KinshipScriptConverter.ToHans).ToHashSet(StringComparer.Ordinal);

        List<string> dead = new();
        foreach (string key in KinshipLexiconLayers.VariantKeys.OrderBy(static k => k, StringComparer.Ordinal))
        {
            // Ego-scoped entries (variants_male / variants_female) count too: 配偶 carries its
            // whole vocabulary there and nothing in the neutral block, so checking only the
            // neutral set would report the most-used relation in the app as having no data.
            string[] variants = KinshipLexiconLayers.GetVariants(key, PersonGender.Male)
                .Concat(KinshipLexiconLayers.GetVariants(key, PersonGender.Female))
                .Select(static v => v.Term)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (variants.Length == 0)
            {
                continue;
            }

            // Serving alone is not enough: a word shared with a live key (伯婆 sits under both
            // 伯祖母 and 伯岳母) would vouch for a key the engine never emits. Require the key
            // itself to be a standard form too, so a dead key cannot hide behind a live neighbour.
            if (!standardsHans.Contains(KinshipScriptConverter.ToHans(key)))
            {
                dead.Add($"{key} (key never emitted)");
                continue;
            }

            // Layer values are authored in one script and script-converted on the way out
            // (BuildLayerVariants → ToHant), so 后爹 leaves as 後爹. Compare on the Hans
            // normal form or every mixed-script variant reads as a false negative.
            string[] normalized = variants.Select(KinshipScriptConverter.ToHans).ToArray();
            bool inHans = normalized.Any(sweep.EmittedHans.Contains);
            bool inHant = normalized.Any(sweep.EmittedHant.Contains);
            if (!inHans && !inHant)
            {
                dead.Add($"{key} (never)");
            }
            else if (!inHans)
            {
                dead.Add($"{key} (zh-Hant only)");
            }
            else if (!inHant)
            {
                dead.Add($"{key} (zh-Hans only)");
            }
        }

        TestContext?.WriteLine($"chains-swept={sweep.ChainCount}");
        TestContext?.WriteLine($"standard-forms-emitted={sweep.Standards.Count}");
        TestContext?.WriteLine($"shipped-variant-keys={KinshipLexiconLayers.VariantKeys.Count}");
        TestContext?.WriteLine($"probe-table={Path.Combine(AppContext.BaseDirectory, ProbeFileName)}");

        Assert.AreEqual(
            0,
            dead.Count,
            $"These lexicon entries never reach a user: {string.Join(", ", dead)}");
    }

    private sealed record SweepResult(
        HashSet<string> Standards,
        HashSet<string> EmittedHans,
        HashSet<string> EmittedHant,
        List<string> ProbeRows,
        int ChainCount);

    private static SweepResult Sweep()
    {
        KinshipCalculator.Core.Services.KinshipCalculator calculator = new();
        HashSet<string> standards = new(StringComparer.Ordinal);
        HashSet<string> emittedHans = new(StringComparer.Ordinal);
        HashSet<string> emittedHant = new(StringComparer.Ordinal);
        List<string> rows = new(8192) { "chain\tego\tstandard_forms\tvariants" };
        int chainCount = 0;

        foreach (string[] chain in EnumerateChains())
        {
            chainCount++;
            foreach (PersonGender ego in new[] { PersonGender.Male, PersonGender.Female })
            {
                List<string> forms = new();
                List<string> variants = new();
                foreach (string language in new[] { "zh-Hans", "zh-Hant" })
                {
                    HashSet<string> emitted = language == "zh-Hans" ? emittedHans : emittedHant;
                    KinshipResult result = calculator.Evaluate(chain, language, ego);
                    foreach (KinshipResolutionOption option in result.Options)
                    {
                        // Label carries the standard form (NameSlotAssembler K16 contract),
                        // AlternateLabel the layer variants; a few paths hand back a '|' set.
                        foreach (string form in Split(option.Label.ForLanguage(language)))
                        {
                            standards.Add(form);
                            emitted.Add(KinshipScriptConverter.ToHans(form));
                            Record(forms, form);
                        }

                        if (!option.HasAlternateLabel)
                        {
                            continue;
                        }

                        foreach (string variant in Split(option.AlternateLabel.ForLanguage(language)))
                        {
                            emitted.Add(KinshipScriptConverter.ToHans(variant));
                            Record(variants, variant);
                        }
                    }
                }

                rows.Add($"{string.Join('.', chain)}\t{ego}\t{string.Join('|', forms)}\t{string.Join('|', variants)}");
            }
        }

        return new SweepResult(standards, emittedHans, emittedHant, rows, chainCount);

        static string[] Split(string set)
            => set.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        static void Record(List<string> target, string value)
        {
            if (!target.Contains(value, StringComparer.Ordinal))
            {
                target.Add(value);
            }
        }
    }

    private static IEnumerable<string[]> EnumerateChains()
    {
        foreach (string a in BaseTokens)
        {
            yield return [a];
            foreach (string b in BaseTokens)
            {
                yield return [a, b];
                foreach (string c in BaseTokens)
                {
                    yield return [a, b, c];
                }
            }
        }

        foreach (string a in DeepOpeners)
        {
            foreach (string b in DeepOpeners)
            {
                foreach (string c in BaseTokens)
                {
                    foreach (string d in BaseTokens)
                    {
                        yield return [a, b, c, d];
                    }
                }
            }
        }

        foreach (string[] ascent in EnumerateAscentRuns())
        {
            foreach (string branch in Branches)
            {
                foreach (bool viaSpouse in new[] { false, true })
                {
                    foreach (bool married in new[] { false, true })
                    {
                        List<string> chain = new(ascent.Length + 3);
                        if (viaSpouse)
                        {
                            chain.Add("spouse");
                        }

                        chain.AddRange(ascent);
                        chain.Add(branch);
                        if (married)
                        {
                            chain.Add("spouse");
                        }

                        yield return chain.ToArray();
                    }
                }
            }
        }

        // 堂 / 表 lines: ascents, one branch, then descents, optionally married in.
        foreach (string[] ascent in EnumerateRuns(Ascents, MaxCollateralAscent))
        {
            foreach (string branch in Branches)
            {
                foreach (string[] descent in EnumerateRuns(Descents, MaxDescent))
                {
                    foreach (bool married in new[] { false, true })
                    {
                        List<string> chain = new(ascent.Length + descent.Length + 2);
                        chain.AddRange(ascent);
                        chain.Add(branch);
                        chain.AddRange(descent);
                        if (married)
                        {
                            chain.Add("spouse");
                        }

                        yield return chain.ToArray();
                    }
                }
            }
        }

        // Straight descent (孫 / 曾孫 / 玄孫 …), optionally married in.
        foreach (string[] descent in EnumerateRuns(Descents, MaxPureDescent))
        {
            yield return descent;
            yield return [.. descent, "spouse"];
        }
    }

    private static IEnumerable<string[]> EnumerateAscentRuns()
        => EnumerateRuns(Ascents, MaxAscent);

    private static IEnumerable<string[]> EnumerateRuns(string[] steps, int maxDepth)
    {
        List<string[]> level = [[]];
        for (int depth = 0; depth < maxDepth; depth++)
        {
            List<string[]> next = new(level.Count * steps.Length);
            foreach (string[] run in level)
            {
                foreach (string step in steps)
                {
                    string[] extended = [.. run, step];
                    next.Add(extended);
                    yield return extended;
                }
            }

            level = next;
        }
    }
}
