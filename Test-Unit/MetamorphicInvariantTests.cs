using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using KinshipCalculator.Core.Data;
using KinshipCalculator.Core.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Calc = KinshipCalculator.Core.Services.KinshipCalculator;

namespace Test_Unit;

/// <summary>
/// Oracle-FREE validation for the deep affinal tail, where no dictionary (not even mumuy)
/// records the answer. These are metamorphic / property tests: they never assert a specific
/// term (which would just re-encode the author's opinion) — they assert RELATIONS the engine
/// must satisfy no matter what term it emits. A generative engine that is internally
/// consistent AND agrees with mumuy on the attested overlap is as correct as an unattested
/// term can be, because for such a term "correct" *means* "what the composition rules yield".
///
/// The properties here are un-fakeable: they are derived from the chain's own arithmetic, so
/// tuning the engine's output to pass them is only possible by making the output actually
/// consistent. The batch runs let the machine check thousands of chains that no human could
/// cross-verify by hand.
/// </summary>
[TestClass]
public class MetamorphicInvariantTests
{
    private static readonly Dictionary<String, KinshipToken> ById =
        KinshipData.Tokens.ToDictionary(t => t.Id, t => t, StringComparer.Ordinal);

    private static readonly String[] BloodPool =
    {
        "father", "mother", "son", "daughter",
        "older-brother", "younger-brother", "older-sister", "younger-sister",
    };

    private static readonly String[] FullPool = BloodPool.Concat(new[] { "spouse" }).ToArray();

    private static readonly HashSet<String> MaleHop = new(StringComparer.Ordinal)
        { "father", "son", "older-brother", "younger-brother" };
    private static readonly HashSet<String> FemaleHop = new(StringComparer.Ordinal)
        { "mother", "daughter", "older-sister", "younger-sister" };

    // Terminal-morpheme gender of a resolved term. 女 as the final glyph always wins
    // (侄女/孫女/甥女), otherwise a curated male/female final-glyph set; anything else is
    // "unknown" and skipped so the invariant only fires where it is certain.
    private static readonly HashSet<Char> FemaleFinal = new("母女姐妹姑姨嬸婶媳婆奶娘嫂妻");
    private static readonly HashSet<Char> MaleFinal = new("父子兄弟夫舅叔伯甥侄孫孙公爺爷郎婿");

    private static String Term(String[] ids, PersonGender self)
        => new Calc().Evaluate(ids, "zh-Hant", self).Term.ForLanguage("zh-Hant");

    private static Boolean IsDirectTerm(String t)
        => !String.IsNullOrWhiteSpace(t)
        && !t.Contains('的') && !t.Contains('→')
        && !t.Contains('眷') && !t.Contains('姻')
        && !t.Contains('：') && !t.StartsWith("自己", StringComparison.Ordinal);

    private static String[] RandomChain(Random rng, String[] pool, Int32 min, Int32 max)
    {
        Int32 len = rng.Next(min, max);
        String[] c = new String[len];
        for (Int32 i = 0; i < len; i++)
        {
            c[i] = pool[rng.Next(pool.Length)];
        }
        return c;
    }

    // ---- M1: determinism -- same input, same output (guards ThreadStatic / ordering leaks).
    [TestMethod]
    public void M1_Determinism()
    {
        Random rng = new(0x1D0D_2A11);
        for (Int32 i = 0; i < 4000; i++)
        {
            String[] c = RandomChain(rng, FullPool, 4, 16);
            String a = Term(c, PersonGender.Male);
            String b = Term(c, PersonGender.Male);
            Assert.AreEqual(a, b, $"non-deterministic on [{String.Join('.', c)}]");
        }
    }

    // ---- M2: spouse-involution confluence -- inserting a self-cancelling 配偶→配偶 pair
    // (the normaliser removes consecutive spouses) must NOT change the result. A difference
    // is a genuine path-dependence bug, provable without any oracle.
    [TestMethod]
    public void M2_SpouseInvolutionConfluence()
    {
        Random rng = new(0x5EED_C0DE);
        KinshipToken sp = ById["spouse"];
        List<String> violations = new();
        Int32 checkedCount = 0;

        for (Int32 i = 0; i < 4000 && violations.Count < 10; i++)
        {
            String[] c = RandomChain(rng, BloodPool, 3, 14); // blood-only base for a clean insert
            // insert the cancelling pair between two blood tokens (clean, unambiguous identity)
            Int32 at = rng.Next(1, c.Length);
            List<String> withPair = new(c);
            withPair.Insert(at, "spouse");
            withPair.Insert(at, "spouse");

            String baseTerm = Term(c, PersonGender.Male);
            String pairTerm = Term(withPair.ToArray(), PersonGender.Male);
            checkedCount++;
            if (!String.Equals(baseTerm, pairTerm, StringComparison.Ordinal))
            {
                violations.Add($"[{String.Join('.', c)}] = {baseTerm}  vs  +配偶配偶@{at} = {pairTerm}");
            }
        }

        _ = sp;
        Assert.AreEqual(0, violations.Count,
            $"spouse-involution broke on {violations.Count}/{checkedCount}:\n  " + String.Join("\n  ", violations));
    }

    // Terminal-gender classification shared by the gauge. Returns (term-is-female?, last-hop-male?);
    // term-is-female is null when the chain is not a confidently-classifiable direct term.
    private static (Boolean? TermFemale, Boolean LastMale) ClassifyTerminal(String[] c)
    {
        Boolean lastMale = MaleHop.Contains(c[^1]);
        String t = Term(c, PersonGender.Male);
        if (!IsDirectTerm(t))
        {
            return (null, lastMale);
        }

        Char fin = t[^1];
        if (fin == '女')
        {
            return (true, lastMale); // 侄女 / 孫女 / 甥女
        }
        if (fin == '子')
        {
            // 子 is a noun suffix, not a gender. Only the morpheme immediately before 子
            // carries the gender — and NOT a 姑表/姨表 LINE prefix elsewhere in the word
            // (姑表姪子 is male: 姪 before 子). 小姨子/小姑子 female, 姪子/甥子/舅子/兒子 male.
            if (t.Length < 2)
            {
                return (null, lastMale);
            }
            Char pen = t[^2];
            Boolean? byPen = "姨姑姐妹嫂婆娘媳奶母女".Contains(pen) ? true
                : "姪侄甥舅叔伯兄弟兒孫孙子父".Contains(pen) ? false
                : (Boolean?) null;
            return (byPen, lastMale);
        }
        if (FemaleFinal.Contains(fin))
        {
            return (true, lastMale);
        }
        if (MaleFinal.Contains(fin))
        {
            return (false, lastMale);
        }

        return (null, lastMale); // unknown terminal glyph — do not classify
    }

    // ---- M3: terminal-gender consistency INVARIANT. The relative IS the last person in the
    // chain, so a resolved DIRECT term must end on a morpheme of the last hop's gender — an
    // un-fakeable expectation drawn from the chain, not a lookup. History: this started as a
    // GAUGE at 1.13% (net-generation collapse + overlapping-loop union pruning dropped the
    // terminal person), fell to 0.17% when those shortcuts were retired (residual: complex-
    // lineal's spouse leg rendering F.D.SP as 嫂嫂 for a male husband), and reached 0/7500
    // when that leg was scoped away and the sibling-co-parent identity landed. The seed is
    // fixed, so the run is deterministic — zero violations is now asserted exactly.
    [TestMethod]
    public void M3_TerminalGenderConsistencyGauge()
    {
        Random rng = new(unchecked((Int32) 0x0DEE_9111));
        List<String> samples = new();
        Int32 asserted = 0;
        Int32 violations = 0;

        for (Int32 i = 0; i < 12000; i++)
        {
            String[] c = RandomChain(rng, BloodPool, 2, 16); // full range, blood-only => last hop gendered
            (Boolean? termFemale, Boolean lastMale) = ClassifyTerminal(c);
            if (termFemale is null)
            {
                continue;
            }

            asserted++;
            if (!termFemale.Value != lastMale)
            {
                violations++;
                if (samples.Count < 15)
                {
                    samples.Add($"[{String.Join('.', c)}] last={c[^1]}({(lastMale ? "M" : "F")}) => {Term(c, PersonGender.Male)}");
                }
            }
        }

        Double rate = asserted == 0 ? 0 : (Double) violations / asserted;
        Console.WriteLine($"[terminal-gender consistency gauge] {violations}/{asserted} = {rate:P2} inconsistent (loop/sibling collapse drops terminal gender)");
        foreach (String s in samples)
        {
            Console.WriteLine("  suspect " + s);
        }

        // The seed is fixed, so the classified-sample count is deterministic: pinning it
        // exactly makes a silent coverage collapse (a formatter change that stops emitting
        // direct terms, shrinking what the invariant can see) fail loudly instead of
        // quietly weakening the check. Update DELIBERATELY when the engine legitimately
        // changes how many chains classify.
        Assert.AreEqual(7500, asserted, $"classified-sample count moved ({asserted}) — coverage changed, re-baseline consciously");
        Assert.AreEqual(0, violations,
            $"terminal-gender inconsistency reappeared at {rate:P2}: the retired collapse-shortcut class is back");
    }
}
