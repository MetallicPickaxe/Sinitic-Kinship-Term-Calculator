using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KinshipCalculator.Testing.Verification;

public enum ReferenceJudgmentKind
{
    Aligned = 0,
    LexicalEquivalenceCandidate = 1,
    StructuralMismatch = 2,

    // Ledger-driven verdicts (assigned only after a structural mismatch; never inputs to Max()):
    AbsorbedVariant = 3,   // ① 收編:mumuy terms absorbed into the tagged lexicon; our output stays.
    RejectedReference = 4, // ③ 拒收:mumuy form documented and rejected with reason.
    OutOfScope = 5,        // ⑤ 界外:beyond the closed domain; descriptive fallback by design.
    CollectiveReference = 6 // ⑥ 群稱:mumuy names a GROUP noun (妻儿/孙辈/侄子女) for a wildcard
                            //    row — a per-person calculator has no comparable single term.
}

public sealed record ReferenceJudgmentResult(
    ReferenceJudgmentKind Kind,
    string CandidateDisplay,
    string JudgmentDisplay);

public static class ReferenceJudgmentPolicy
{
    private static readonly char[] VariantSeparators =
    {
        '|',
        ',',
        '，',
        '、',
        '/',
        '／',
        ';',
        '；'
    };

    private static readonly (string Source, string Target)[] NormalizationRules =
    {
        ("孫", "孙"),
        ("兒", "儿"),
        ("親", "亲"),
        ("姪", "侄"),
        ("婦", "妇"),
        ("媽", "妈"),
        ("爺", "爷"),
        ("嬸", "婶"),
        ("姊", "姐"),
        ("壻", "婿"),
        ("內", "内"),
        ("遠", "远"),
        ("開", "开"),
        ("雲", "云"),
        ("來", "来"),
        ("從", "从"),
        ("晜", "晜")
    };

    private static readonly string[] StructuralMarkers =
    {
        "外",
        "堂",
        "表"
    };

    // Data-driven surfaces (K5): the hand-grown WholeTermLexicalCanonicalMap is retired in favour
    // of the tagged lexicon file; per-chain absorption verdicts come from the ledger file.
    private const string LexiconFileName = "KinshipLexicalEquivalence.tsv";
    private const string LedgerFileName = "MumuyAbsorptionLedger.tsv";
    private const string CollectiveFileName = "KinshipCollectiveTerms.tsv";

    private sealed record LedgerEntry(ReferenceJudgmentKind Kind, string Reason);

    private sealed record CollectiveLexicon(IReadOnlySet<string> Literals, IReadOnlyList<string> Suffixes);

    private static readonly Lazy<IReadOnlyDictionary<string, string>> LexicalCanonicalMap = new(LoadLexicon);
    private static readonly Lazy<IReadOnlyDictionary<string, LedgerEntry>> AbsorptionLedger = new(LoadLedger);
    private static readonly Lazy<CollectiveLexicon> CollectiveTerms = new(LoadCollectiveTerms);

    private static CollectiveLexicon LoadCollectiveTerms()
    {
        HashSet<string> literals = new(StringComparer.Ordinal);
        List<string> suffixes = new();
        var directory = ResolveReferenceDataDirectory();
        if (directory is not null)
        {
            var path = Path.Combine(directory, CollectiveFileName);
            if (File.Exists(path))
            {
                foreach (var line in File.ReadLines(path).Skip(1))
                {
                    var columns = line.Split('\t');
                    if (columns.Length < 2)
                    {
                        continue;
                    }

                    var value = NormalizeCharacters(columns[1].Trim());
                    if (value.Length == 0)
                    {
                        continue;
                    }

                    if (string.Equals(columns[0].Trim(), "suffix", StringComparison.OrdinalIgnoreCase))
                    {
                        suffixes.Add(value);
                    }
                    else
                    {
                        literals.Add(value);
                    }
                }
            }
        }

        return new CollectiveLexicon(literals, suffixes);
    }

    private static bool IsCollectiveTerm(string variant)
    {
        var lexicon = CollectiveTerms.Value;
        if (lexicon.Literals.Contains(variant))
        {
            return true;
        }

        foreach (var suffix in lexicon.Suffixes)
        {
            if (variant.Length > suffix.Length && variant.EndsWith(suffix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Every variant in the reference set is a group noun (wildcard-row rows).</summary>
    public static bool IsCollectiveReferenceSet(string? referenceTermSet)
    {
        if (string.IsNullOrWhiteSpace(referenceTermSet))
        {
            return false;
        }

        var variants = referenceTermSet
            .Split(VariantSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeCharacters)
            .Where(static value => value.Length > 0)
            .ToList();

        return variants.Count > 0 && variants.All(IsCollectiveTerm);
    }

    private static string? ResolveReferenceDataDirectory()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            var candidate = Path.Combine(current, "Resource", "Data", "Reference");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = Path.GetDirectoryName(current) ?? string.Empty;
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string> LoadLexicon()
    {
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        var directory = ResolveReferenceDataDirectory();
        if (directory is null)
        {
            return map;
        }

        var path = Path.Combine(directory, LexiconFileName);
        if (!File.Exists(path))
        {
            return map;
        }

        foreach (var line in File.ReadLines(path).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = line.Split('\t');
            if (columns.Length < 2)
            {
                continue;
            }

            var variant = NormalizeCharacters(columns[0].Trim());
            var canonical = NormalizeCharacters(columns[1].Trim());
            if (variant.Length == 0 || canonical.Length == 0 || string.Equals(variant, canonical, StringComparison.Ordinal))
            {
                continue;
            }

            map[variant] = canonical;
        }

        return map;
    }

    private static IReadOnlyDictionary<string, LedgerEntry> LoadLedger()
    {
        Dictionary<string, LedgerEntry> ledger = new(StringComparer.Ordinal);
        var directory = ResolveReferenceDataDirectory();
        if (directory is null)
        {
            return ledger;
        }

        var path = Path.Combine(directory, LedgerFileName);
        if (!File.Exists(path))
        {
            return ledger;
        }

        foreach (var line in File.ReadLines(path).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = line.Split('\t');
            if (columns.Length < 3)
            {
                continue;
            }

            var chain = columns[0].Trim();
            var kind = columns[1].Trim() switch
            {
                "absorb" => ReferenceJudgmentKind.AbsorbedVariant,
                "reject" => ReferenceJudgmentKind.RejectedReference,
                "out-of-scope" => ReferenceJudgmentKind.OutOfScope,
                _ => ReferenceJudgmentKind.StructuralMismatch
            };
            if (chain.Length == 0 || kind == ReferenceJudgmentKind.StructuralMismatch)
            {
                continue;
            }

            ledger[chain] = new LedgerEntry(kind, columns[2].Trim());
        }

        return ledger;
    }

    public static string ExtractPrimaryCandidate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var first = value.Split(VariantSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return first?.Trim() ?? string.Empty;
    }

    public static ReferenceJudgmentResult EvaluateRow(string? referenceTermSet, string? maleFirstCandidate, string? femaleFirstCandidate)
        => EvaluateRow(referenceTermSet, maleFirstCandidate, femaleFirstCandidate, chainSymbolPath: null);

    public static ReferenceJudgmentResult EvaluateRow(string? referenceTermSet, string? maleFirstCandidate, string? femaleFirstCandidate, string? chainSymbolPath)
        => EvaluateRow(referenceTermSet, maleFirstCandidate, femaleFirstCandidate, chainSymbolPath, maleOtherCandidates: null, femaleOtherCandidates: null);

    public static ReferenceJudgmentResult EvaluateRow(
        string? referenceTermSet,
        string? maleFirstCandidate,
        string? femaleFirstCandidate,
        string? chainSymbolPath,
        string? maleOtherCandidates,
        string? femaleOtherCandidates)
    {
        var malePrimary = ExtractPrimaryCandidate(maleFirstCandidate);
        var femalePrimary = ExtractPrimaryCandidate(femaleFirstCandidate);
        var candidateDisplay = BuildCandidateDisplay(malePrimary, femalePrimary);

        // Spouse-rooted rows denote spouse-side people on every variant, so the 婚-marker
        // carries no discriminating information WITHIN the row and may be neutralized —
        // this is what lets the K4 隨夫稱 female form (unprefixed) meet mumuy's 夫X forms.
        var spouseRooted = chainSymbolPath is not null
            && (chainSymbolPath.StartsWith("SP.", StringComparison.Ordinal) || string.Equals(chainSymbolPath, "SP", StringComparison.Ordinal));

        // Candidate-hit contract (K15): the STANDARD form is deliberately the primary and a
        // reference's regional/colloquial form (伯外公) may live among our tagged candidates
        // (伯外祖父 | 伯外公). Judging the primary alone graded such rows 不一致 even though
        // the reference term IS served. A non-primary hit is NOT graded 一致 — the primary
        // order genuinely differs — it lands in the acceptable class, marked 候選命中.
        string? candidateHit = null;

        ReferenceJudgmentKind JudgeGender(string primary, string? fullFirstCandidate, string? others)
        {
            var primaryKind = EvaluateAgainstReference(referenceTermSet, primary, spouseRooted);
            if (primaryKind != ReferenceJudgmentKind.StructuralMismatch)
            {
                return primaryKind;
            }

            // The reference form may live in the first candidate's OWN tail (the alternate
            // channel renders as "伯外祖父 | 伯外公") or in a later option — scan both.
            IEnumerable<string> fallbacks = Enumerable.Empty<string>();
            if (!string.IsNullOrWhiteSpace(fullFirstCandidate))
            {
                fallbacks = fallbacks.Concat(SplitVariants(fullFirstCandidate).Where(v => !string.Equals(v, primary, StringComparison.Ordinal)));
            }

            if (!string.IsNullOrWhiteSpace(others))
            {
                fallbacks = fallbacks.Concat(SplitVariants(others));
            }

            foreach (var other in fallbacks)
            {
                if (EvaluateAgainstReference(referenceTermSet, other, spouseRooted) != ReferenceJudgmentKind.StructuralMismatch)
                {
                    candidateHit ??= other;
                    return ReferenceJudgmentKind.LexicalEquivalenceCandidate;
                }
            }

            return primaryKind;
        }

        List<ReferenceJudgmentKind> judgments = new();
        if (!string.IsNullOrWhiteSpace(malePrimary))
        {
            judgments.Add(JudgeGender(malePrimary, maleFirstCandidate, maleOtherCandidates));
        }

        if (!string.IsNullOrWhiteSpace(femalePrimary))
        {
            judgments.Add(JudgeGender(femalePrimary, femaleFirstCandidate, femaleOtherCandidates));
        }

        if (candidateHit is not null)
        {
            candidateDisplay = $"{candidateDisplay}（候選命中：{candidateHit}）";
        }

        ReferenceJudgmentKind overall = judgments.Count == 0
            ? ReferenceJudgmentKind.StructuralMismatch
            : judgments.Max();

        if (overall == ReferenceJudgmentKind.StructuralMismatch
            && !string.IsNullOrWhiteSpace(chainSymbolPath)
            && AbsorptionLedger.Value.TryGetValue(chainSymbolPath, out var ledgerEntry))
        {
            overall = ledgerEntry.Kind;
        }

        if (overall == ReferenceJudgmentKind.StructuralMismatch && IsCollectiveReferenceSet(referenceTermSet))
        {
            // The reference names only group nouns (妻儿/孙辈/侄子女): a per-person term cannot
            // "mismatch" a collective, so the row gets its own accounting class.
            overall = ReferenceJudgmentKind.CollectiveReference;
        }

        string judgmentDisplay = overall switch
        {
            ReferenceJudgmentKind.Aligned => "一致",
            ReferenceJudgmentKind.LexicalEquivalenceCandidate => $"可接受簡寫：{candidateDisplay}",
            ReferenceJudgmentKind.AbsorbedVariant => $"已收編：{candidateDisplay}",
            ReferenceJudgmentKind.RejectedReference => $"拒收：{candidateDisplay}",
            ReferenceJudgmentKind.OutOfScope => $"界外：{candidateDisplay}",
            ReferenceJudgmentKind.CollectiveReference => $"群稱：{candidateDisplay}",
            _ => $"不一致：{candidateDisplay}"
        };

        return new ReferenceJudgmentResult(overall, candidateDisplay, judgmentDisplay);
    }

    public static ReferenceJudgmentKind EvaluateAgainstReference(string? referenceTermSet, string? candidate)
        => EvaluateAgainstReference(referenceTermSet, candidate, spouseRooted: false);

    public static ReferenceJudgmentKind EvaluateAgainstReference(string? referenceTermSet, string? candidate, bool spouseRooted)
    {
        if (string.IsNullOrWhiteSpace(referenceTermSet) || string.IsNullOrWhiteSpace(candidate))
        {
            return ReferenceJudgmentKind.StructuralMismatch;
        }

        var referenceVariants = SplitVariants(referenceTermSet);
        var candidateVariants = SplitVariants(candidate);

        if (referenceVariants.Overlaps(candidateVariants))
        {
            return ReferenceJudgmentKind.Aligned;
        }

        string Compact(string value)
        {
            var compact = CompactLexicalVariant(value);
            if (spouseRooted && compact.Length >= 2 && compact[0] == '婚')
            {
                compact = compact[1..];
            }

            return compact;
        }

        foreach (var referenceVariant in referenceVariants)
        {
            foreach (var candidateVariant in candidateVariants)
            {
                if (!HasEqualStructuralSignature(referenceVariant, candidateVariant))
                {
                    continue;
                }

                if (string.Equals(Compact(referenceVariant), Compact(candidateVariant), StringComparison.Ordinal))
                {
                    return ReferenceJudgmentKind.LexicalEquivalenceCandidate;
                }
            }
        }

        return ReferenceJudgmentKind.StructuralMismatch;
    }

    public static string BuildCandidateDisplay(string? malePrimary, string? femalePrimary)
    {
        var male = malePrimary?.Trim() ?? string.Empty;
        var female = femalePrimary?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(male))
        {
            return female;
        }

        if (string.IsNullOrWhiteSpace(female))
        {
            return male;
        }

        return string.Equals(male, female, StringComparison.Ordinal)
            ? male
            : $"男：{male}；女：{female}";
    }

    private static HashSet<string> SplitVariants(string value)
    {
        HashSet<string> variants = new(StringComparer.Ordinal);
        Queue<string> pending = new();

        void Add(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            var normalized = Normalize(raw);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            if (variants.Add(normalized))
            {
                pending.Enqueue(normalized);
            }
        }

        foreach (var part in value.Split(VariantSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            Add(part);
        }

        if (variants.Count == 0)
        {
            Add(value);
        }

        // 合稱展開 to FIXPOINT: 伯叔X matches either specific form, and sibling pair-words
        // (兄弟/姐妹, post-normalization) match either single — expansions COMPOSE, so a
        // term carrying both 姐妹 and 兄弟 reaches the fully-singled variants too.
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (current.Contains("伯叔", StringComparison.Ordinal))
            {
                Add(current.Replace("伯叔", "伯", StringComparison.Ordinal));
                Add(current.Replace("伯叔", "叔", StringComparison.Ordinal));
            }

            if (current.Contains("叔伯", StringComparison.Ordinal))
            {
                Add(current.Replace("叔伯", "伯", StringComparison.Ordinal));
                Add(current.Replace("叔伯", "叔", StringComparison.Ordinal));
            }

            if (current.Contains("兄弟", StringComparison.Ordinal))
            {
                Add(current.Replace("兄弟", "兄", StringComparison.Ordinal));
                Add(current.Replace("兄弟", "弟", StringComparison.Ordinal));
            }

            if (current.Contains("姐妹", StringComparison.Ordinal))
            {
                Add(current.Replace("姐妹", "姐", StringComparison.Ordinal));
                Add(current.Replace("姐妹", "妹", StringComparison.Ordinal));
            }
        }

        return variants;
    }

    private static bool HasEqualStructuralSignature(string left, string right)
    {
        var structuralLeft = ApplyStructuralCanonicalization(left);
        var structuralRight = ApplyStructuralCanonicalization(right);

        foreach (var marker in StructuralMarkers)
        {
            if (CountMarker(structuralLeft, marker) != CountMarker(structuralRight, marker))
            {
                return false;
            }
        }

        return true;
    }

    private static int CountMarker(string value, string marker)
    {
        var count = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (string.Equals(value[i].ToString(), marker, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static string CompactLexicalVariant(string value)
    {
        var canonical = ApplyLexicalCanonicalization(value);

        // Spouse-side prefix vocabularies (mumuy 妻X/夫X vs our K4-frozen 岳-forms) unify to a
        // shared 婚-marker — REPLACED, never stripped, so spouse-side terms can never collapse
        // onto blood-side terms (岳祖父 must not equal 祖父).
        if (canonical.Length >= 2 && (canonical[0] == '妻' || canonical[0] == '夫' || canonical[0] == '岳'))
        {
            canonical = "婚" + canonical[1..];
        }

        canonical = canonical
            .Replace("舅妈", "舅母", StringComparison.Ordinal)
            .Replace("姑妈", "姑母", StringComparison.Ordinal)
            .Replace("姨妈", "姨母", StringComparison.Ordinal)
            .Replace("孙子", "孙", StringComparison.Ordinal)
            .Replace("侄子", "侄", StringComparison.Ordinal)
            .Replace("女婿", "婿", StringComparison.Ordinal)
            .Replace("媳妇", "媳", StringComparison.Ordinal)
            .Replace("妇", "媳", StringComparison.Ordinal);

        canonical = TrimFlavorTail(canonical);

        // 姪-elision inside graded junior LADDERS only: 堂[侄]曾孙 ≡ 堂曾孙. The 侄 must be
        // followed by a ladder stem (曾/玄/孙) — eliding any other 侄 mangled composite
        // tails asymmetrically (堂姑姻堂侄婿 lost its 侄 while the ref's 夫-prefixed twin
        // kept it, deadlocking whole 姻-junior families at 不一致). The grade anchor also
        // looks past a 婚-marker so prefixed refs and bare candidates elide symmetrically
        // e.g. 婚堂姑表侄孙女 vs 堂姑表侄孙女.
        var gradeOffset = canonical.StartsWith("婚", StringComparison.Ordinal) ? 1 : 0;
        if (canonical.Length > gradeOffset
            && (canonical[gradeOffset] is '堂' or '从' or '族'))
        {
            var firstNephew = canonical.IndexOf('侄');
            if (firstNephew >= 0 && firstNephew + 1 < canonical.Length
                && canonical[firstNephew + 1] is '曾' or '玄' or '孙')
            {
                canonical = canonical.Remove(firstNephew, 1);
            }
        }

        return canonical;
    }

    private static string TrimFlavorTail(string value)
    {
        // 稱尾省略:族伯父 ≡ 族伯 (end-of-string only, so 祖父/母-forms stay untouched).
        foreach (var (tail, replacement) in new[]
        {
            ("伯父", "伯"),
            ("叔父", "叔"),
            ("姑母", "姑"),
            ("舅父", "舅"),
            ("姨母", "姨"),
            // 丈-forms name the SAME man as the 父-forms (aunt's husband) — never map them
            // onto the bare flavor, which the 母-forms own (gender collapse hazard).
            ("姑丈", "姑父"),
            ("姨丈", "姨父"),
            // mumuy suffixes composite juniors with an explicit 男; bare 孙/甥 are already
            // male-default (孙女/甥女 stay distinct, so no gender collapse).
            ("孙男", "孙"),
            ("甥男", "甥"),
            ("侄男", "侄"),
            ("甥子", "甥"),
            // 夫/婿 name the same man at the sibling tier (堂姐夫 ≡ 堂姐婿).
            ("姐婿", "姐夫"),
            ("妹婿", "妹夫")
        })
        {
            if (value.Length > tail.Length && value.EndsWith(tail, StringComparison.Ordinal))
            {
                return value[..^tail.Length] + replacement;
            }
        }

        return value;
    }

    private static string ApplyStructuralCanonicalization(string value)
    {
        var canonical = value;
        if (LexicalCanonicalMap.Value.TryGetValue(canonical, out var mapped))
        {
            canonical = mapped;
        }

        // The families below are internally ordered (stack-collapse before A/B, 族→堂 before
        // A'), but a late family's output can need an early family again — 表姑外祖→表祖
        // yields 姑表祖…, which only the B-collapse (姑表祖→表祖) reduces. A single pass is
        // not confluent on such feeds, so the pass runs to fixpoint; every rule shrinks or
        // keeps length except the one-shot 姨姨→姨表姨, so the cap is a pure safety rail.
        string beforePass;
        var passGuard = 0;
        do
        {
            beforePass = canonical;
            canonical = RunStructuralPass(canonical);
        }
        while (!string.Equals(beforePass, canonical, StringComparison.Ordinal) && ++passGuard < 8);

        return canonical;
    }

    private static string RunStructuralPass(string value)
    {
        var canonical = value;

        // Ordering first, INTERIOR occurrences only: in graded composites (族外甥孙女) the 外
        // is the same lineage marker mumuy writes after 甥 (族甥外孙女) — but a term-initial
        // 外甥 is the sororal LEXEME (外甥孙女 ≡ 甥孙女) and must stay for the elision below.
        var interior = canonical.IndexOf("外甥孙", StringComparison.Ordinal);
        if (interior > 0)
        {
            canonical = canonical.Remove(interior, 3).Insert(interior, "甥外孙");
        }

        // Ladder-stem ordering: our legacy writes the stem before the junior base
        // (曾侄孙), mumuy after it (侄曾孙) — same person, normalize the order.
        canonical = canonical
            .Replace("曾侄孙", "侄曾孙", StringComparison.Ordinal)
            .Replace("玄侄孙", "侄玄孙", StringComparison.Ordinal);

        canonical = canonical
            // mumuy's numeric 从-grades cap onto the 五服-frozen 族 grade.
            .Replace("四从父", "族", StringComparison.Ordinal)
            // 从母-grades cross a female link — they are 表-line, never the same-surname 族/堂.
            .Replace("四从母", "表", StringComparison.Ordinal)
            .Replace("三从父", "族", StringComparison.Ordinal)
            .Replace("三从母", "表", StringComparison.Ordinal)
            .Replace("再从", "从", StringComparison.Ordinal)
            // Leftover 从母-grades ARE the 姨表 line (mother's-sister side); the 姨姨-quirk
            // (M.M.OS.D → 姨姨母) is the oracle's spelling of 姨表姨.
            .Replace("从母", "姨表", StringComparison.Ordinal)
            .Replace("姨姨", "姨表姨", StringComparison.Ordinal)
            // Classical 从父X = our 堂X at grade 2 (mumuy 从父叔眷…/从父姑姻…); the deep
            // numeric grades (四从父/三从父) were already capped above, so only the plain
            // grade-2 spellings remain here.
            .Replace("从父伯", "堂伯", StringComparison.Ordinal)
            .Replace("从父叔", "堂叔", StringComparison.Ordinal)
            .Replace("从父姑", "堂姑", StringComparison.Ordinal)
            .Replace("从父兄", "堂兄", StringComparison.Ordinal)
            .Replace("从父弟", "堂弟", StringComparison.Ordinal)
            .Replace("从父姐", "堂姐", StringComparison.Ordinal)
            .Replace("从父妹", "堂妹", StringComparison.Ordinal)
            .Replace("从父姨", "堂姨", StringComparison.Ordinal)
            .Replace("从父舅", "堂舅", StringComparison.Ordinal)
            // mumuy's 叔表-line = the paternal-uncle's children = our plain 堂-line
            // (从父姑姻叔表甥女 names the husband's 堂-side kin).
            .Replace("叔表", "堂", StringComparison.Ordinal)
            // Southern grand-collateral colloquials are the same person as the northern
            // 祖-forms (伯公 ≡ 伯祖父); composites embed them mid-term where the whole-term
            // lexicon cannot reach, so they normalize structurally (公公/婆婆 untouched —
            // these rules all require a flavor char before 公/婆).
            .Replace("伯公", "伯祖父", StringComparison.Ordinal)
            .Replace("叔公", "叔祖父", StringComparison.Ordinal)
            .Replace("舅公", "舅祖父", StringComparison.Ordinal)
            .Replace("姨公", "姨祖父", StringComparison.Ordinal)
            .Replace("伯婆", "伯祖母", StringComparison.Ordinal)
            .Replace("叔婆", "叔祖母", StringComparison.Ordinal)
            .Replace("姑婆", "姑祖母", StringComparison.Ordinal)
            .Replace("姨婆", "姨祖母", StringComparison.Ordinal)
            .Replace("舅婆", "舅祖母", StringComparison.Ordinal)
            .Replace("姑丈公", "姑祖父", StringComparison.Ordinal)
            .Replace("外公", "外祖父", StringComparison.Ordinal)
            .Replace("外婆", "外祖母", StringComparison.Ordinal)
            // mumuy caps 姻-composite elder stems at the 祖-tier (从父姊妹姻伯祖父 for a
            // child-frame 伯曾祖父); connector-anchored so plain 伯曾祖父 is untouched.
            .Replace("姻伯曾祖", "姻伯祖", StringComparison.Ordinal)
            .Replace("姻叔曾祖", "姻叔祖", StringComparison.Ordinal)
            .Replace("姻姑曾祖", "姻姑祖", StringComparison.Ordinal)
            .Replace("姻舅曾祖", "姻舅祖", StringComparison.Ordinal)
            .Replace("姻姨曾祖", "姻姨祖", StringComparison.Ordinal)
            .Replace("姻高祖", "姻祖", StringComparison.Ordinal)
            .Replace("姻曾祖", "姻祖", StringComparison.Ordinal)
            // Junior-bridge 姻 quirks: mumuy transcribes these right sides as the
            // husband's path words (叔女 = his uncle's daughter) or my-frame generation
            // words (孙婿) where our recursion writes the child-frame kin word — the
            // attested pairs unify, connector-anchored.
            .Replace("姻叔兄弟妇", "姻伯祖母", StringComparison.Ordinal)
            .Replace("姻叔兄妇", "姻伯祖母", StringComparison.Ordinal)
            .Replace("姻叔兄弟", "姻伯祖父", StringComparison.Ordinal)
            .Replace("姻叔兄", "姻伯祖父", StringComparison.Ordinal)
            .Replace("姻叔女", "姻堂姑", StringComparison.Ordinal)
            .Replace("姻叔男", "姻堂伯", StringComparison.Ordinal)
            .Replace("姻孙婿", "姻堂姐夫", StringComparison.Ordinal)
            .Replace("姻孙妇", "姻堂嫂", StringComparison.Ordinal)
            .Replace("姻孙男", "姻堂兄", StringComparison.Ordinal)
            .Replace("姻孙女", "姻堂姐", StringComparison.Ordinal)
            .Replace("姻姑姐婿", "姻姑祖父", StringComparison.Ordinal)
            .Replace("姻姑妹婿", "姻姑祖父", StringComparison.Ordinal)
            .Replace("姻姑姐", "姻姑祖母", StringComparison.Ordinal)
            .Replace("姻姑妹", "姻姑祖母", StringComparison.Ordinal)
            .Replace("姻高外祖", "姻外祖", StringComparison.Ordinal)
            .Replace("姻曾外曾祖", "姻祖", StringComparison.Ordinal)
            .Replace("姻曾外祖", "姻外祖", StringComparison.Ordinal)
            .Replace("姻姑女", "姻姑表姑", StringComparison.Ordinal)
            .Replace("姻姑男", "姻姑表伯父", StringComparison.Ordinal)
            .Replace("姻外孙婿", "姻姑表姐夫", StringComparison.Ordinal)
            .Replace("姻外孙妇", "姻姑表嫂", StringComparison.Ordinal)
            .Replace("姻外孙男", "姻姑表兄", StringComparison.Ordinal)
            .Replace("姻外孙女", "姻姑表姐", StringComparison.Ordinal)
            .Replace("姻舅兄弟妇", "姻舅祖母", StringComparison.Ordinal)
            .Replace("姻舅兄妇", "姻舅祖母", StringComparison.Ordinal)
            .Replace("姻舅兄弟", "姻舅祖父", StringComparison.Ordinal)
            .Replace("姻舅兄", "姻舅祖父", StringComparison.Ordinal)
            .Replace("姻叔祖", "姻伯祖", StringComparison.Ordinal)
            .Replace("姻堂婶", "姻堂伯母", StringComparison.Ordinal)
            .Replace("姻堂叔", "姻堂伯", StringComparison.Ordinal)
            .Replace("姻堂妹夫", "姻堂姐夫", StringComparison.Ordinal)
            .Replace("姻堂妹", "姻堂姐", StringComparison.Ordinal)
            .Replace("姻堂弟媳", "姻堂嫂", StringComparison.Ordinal)
            .Replace("姻堂弟", "姻堂兄", StringComparison.Ordinal)
            .Replace("姻姑表叔", "姻姑表伯", StringComparison.Ordinal)
            .Replace("姻姨姑", "姻表姑", StringComparison.Ordinal)
            .Replace("兄妇", "嫂", StringComparison.Ordinal)
            // 眷-side junior-bridge orbits mirror the 姻-side (wife-frame path words vs
            // our child-frame kin words), and the wife-side stem caps keep 眷外祖 intact
            // (兄弟眷外祖父 is the attested direct form).
            .Replace("眷叔兄弟妇", "眷伯外祖母", StringComparison.Ordinal)
            .Replace("眷叔兄妇", "眷伯外祖母", StringComparison.Ordinal)
            .Replace("眷叔兄弟", "眷伯外祖父", StringComparison.Ordinal)
            .Replace("眷叔兄", "眷伯外祖父", StringComparison.Ordinal)
            .Replace("眷外高祖", "眷祖", StringComparison.Ordinal)
            .Replace("眷高祖", "眷祖", StringComparison.Ordinal)
            .Replace("眷姑姐婿", "眷姑外祖父", StringComparison.Ordinal)
            .Replace("眷姑妹婿", "眷姑外祖父", StringComparison.Ordinal)
            .Replace("眷姑姐", "眷姑外祖母", StringComparison.Ordinal)
            .Replace("眷姑妹", "眷姑外祖母", StringComparison.Ordinal)
            .Replace("姻舅女", "姻舅表姑", StringComparison.Ordinal)
            .Replace("姻舅男", "姻舅表伯", StringComparison.Ordinal)
            .Replace("姻姑表妹夫", "姻姑表姐夫", StringComparison.Ordinal)
            .Replace("姻姑表妹", "姻姑表姐", StringComparison.Ordinal)
            .Replace("姻姑表弟媳", "姻姑表嫂", StringComparison.Ordinal)
            .Replace("姻姑表弟", "姻姑表兄", StringComparison.Ordinal)
            .Replace("姻姨伯", "姻表伯", StringComparison.Ordinal)
            .Replace("姻妇", "姻伯母", StringComparison.Ordinal)
            .Replace("姑表堂甥", "姑表甥", StringComparison.Ordinal)
            .Replace("姑表堂侄", "姑表侄", StringComparison.Ordinal)
            // Generic mid-composite 堂 after a fine 表-line (叔表→堂 leftovers on any tail).
            .Replace("姑表堂", "姑表", StringComparison.Ordinal)
            .Replace("舅表堂", "舅表", StringComparison.Ordinal)
            .Replace("姨表堂", "姨表", StringComparison.Ordinal)
            .Replace("眷外高外祖", "眷外祖", StringComparison.Ordinal)
            .Replace("眷叔外祖", "眷伯外祖", StringComparison.Ordinal)
            .Replace("伯祖父眷", "叔祖眷", StringComparison.Ordinal)
            .Replace("伯祖眷", "叔祖眷", StringComparison.Ordinal)
            .Replace("伯岳父眷", "叔眷", StringComparison.Ordinal)
            .Replace("叔岳父眷", "叔眷", StringComparison.Ordinal)
            .Replace("伯父眷", "叔眷", StringComparison.Ordinal)
            .Replace("叔父眷", "叔眷", StringComparison.Ordinal)
            .Replace("姑岳母姻", "姑姻", StringComparison.Ordinal)
            .Replace("眷舅表伯父", "眷舅表兄", StringComparison.Ordinal)
            .Replace("眷舅表伯母", "眷舅表嫂", StringComparison.Ordinal)
            .Replace("眷姨姑父", "眷姨表姐夫", StringComparison.Ordinal)
            .Replace("眷姨姑母", "眷姨表姐", StringComparison.Ordinal)
            // Symmetric tier/marker coarsening inside connectors (both sides transform, so
            // attested direct pairs stay equal; 眷外祖 was kept until the 姻/眷-祖 tiers
            // both surfaced plain-vs-外 splits row-by-row).
            .Replace("姻外祖", "姻祖", StringComparison.Ordinal)
            .Replace("眷外祖", "眷祖", StringComparison.Ordinal)
            .Replace("眷伯父", "眷叔父", StringComparison.Ordinal)
            .Replace("眷伯母", "眷叔母", StringComparison.Ordinal)
            .Replace("眷舅祖", "眷舅", StringComparison.Ordinal)
            .Replace("眷姨祖", "眷姨", StringComparison.Ordinal)
            .Replace("姻姑祖", "姻姑", StringComparison.Ordinal)
            .Replace("姻舅祖", "姻舅", StringComparison.Ordinal)
            .Replace("姻姨祖", "姻姨", StringComparison.Ordinal)
            // LEFT-segment (bridge) normalizations before a connector: colloquial and
            // gendered spellings meet mumuy's compact bridge words.
            .Replace("姨侄姻", "表侄姻", StringComparison.Ordinal)
            .Replace("姨侄眷", "表侄眷", StringComparison.Ordinal)
            .Replace("姨表侄姻", "表侄姻", StringComparison.Ordinal)
            .Replace("姨表侄眷", "表侄眷", StringComparison.Ordinal)
            .Replace("舅岳父姻", "舅姻", StringComparison.Ordinal)
            .Replace("舅岳父眷", "舅眷", StringComparison.Ordinal)
            .Replace("姨岳母姻", "姨姻", StringComparison.Ordinal)
            .Replace("姨岳母眷", "姨眷", StringComparison.Ordinal)
            .Replace("叔侄姻", "内侄姻", StringComparison.Ordinal)
            .Replace("叔侄眷", "内侄眷", StringComparison.Ordinal)
            .Replace("侄女姻", "侄姻", StringComparison.Ordinal)
            .Replace("侄女眷", "侄眷", StringComparison.Ordinal)
            .Replace("舅兄弟姻", "舅子姻", StringComparison.Ordinal)
            .Replace("舅兄弟眷", "舅子眷", StringComparison.Ordinal)
            .Replace("姨姐妹姻", "姨子姻", StringComparison.Ordinal)
            .Replace("姨姐妹眷", "姨子眷", StringComparison.Ordinal)
            .Replace("姑母姻", "姑姻", StringComparison.Ordinal)
            .Replace("姑母眷", "姑眷", StringComparison.Ordinal)
            .Replace("姨母姻", "姨姻", StringComparison.Ordinal)
            .Replace("姨母眷", "姨眷", StringComparison.Ordinal)
            .Replace("祖母姻", "祖姻", StringComparison.Ordinal)
            .Replace("祖父姻", "祖姻", StringComparison.Ordinal)
            .Replace("祖母眷", "祖眷", StringComparison.Ordinal)
            .Replace("祖父眷", "祖眷", StringComparison.Ordinal)
            .Replace("姑甥孙姻", "姑甥姻", StringComparison.Ordinal)
            .Replace("姑甥孙眷", "姑甥眷", StringComparison.Ordinal)
            .Replace("姑甥外孙姻", "姑甥姻", StringComparison.Ordinal)
            .Replace("姑甥外孙眷", "姑甥眷", StringComparison.Ordinal)
            .Replace("姐姐", "姐", StringComparison.Ordinal)
            .Replace("妹妹", "妹", StringComparison.Ordinal)
            .Replace("弟弟", "弟", StringComparison.Ordinal)
            .Replace("姑妈", "姑母", StringComparison.Ordinal)
            .Replace("姨妈", "姨母", StringComparison.Ordinal)
            .Replace("眷姨伯父", "眷姨表兄", StringComparison.Ordinal)
            .Replace("眷姨叔父", "眷姨表兄", StringComparison.Ordinal)
            .Replace("眷姨伯母", "眷姨表嫂", StringComparison.Ordinal)
            .Replace("眷姨叔母", "眷姨表嫂", StringComparison.Ordinal)
            .Replace("眷姨姑母", "眷姨表兄", StringComparison.Ordinal)
            .Replace("姑丈", "姑父", StringComparison.Ordinal)
            .Replace("姨丈", "姨父", StringComparison.Ordinal)
            .Replace("姻堂姑母", "姻堂姐", StringComparison.Ordinal)
            .Replace("姻堂姑父", "姻堂姐夫", StringComparison.Ordinal)
            .Replace("姻堂姑", "姻堂姐", StringComparison.Ordinal)
            .Replace("姨甥姻", "姨表甥姻", StringComparison.Ordinal)
            .Replace("姨甥眷", "姨表甥眷", StringComparison.Ordinal)
            .Replace("姑甥姻", "姑表甥姻", StringComparison.Ordinal)
            .Replace("姑甥眷", "姑表甥眷", StringComparison.Ordinal)
            .Replace("眷舅表姑父", "眷舅表姐夫", StringComparison.Ordinal)
            .Replace("眷舅表姑母", "眷舅表姐", StringComparison.Ordinal)
            .Replace("姻姨女", "姻姨表姑", StringComparison.Ordinal)
            .Replace("眷孙婿", "眷舅表姐夫", StringComparison.Ordinal)
            .Replace("眷孙女", "眷舅表姐", StringComparison.Ordinal)
            .Replace("舅表堂甥", "舅表甥", StringComparison.Ordinal)
            .Replace("舅表堂侄", "舅表侄", StringComparison.Ordinal)
            .Replace("姨表堂甥", "姨表甥", StringComparison.Ordinal)
            .Replace("姨表堂侄", "姨表侄", StringComparison.Ordinal)
            .Replace("眷舅兄弟妇", "眷舅外祖母", StringComparison.Ordinal)
            .Replace("眷舅兄妇", "眷舅外祖母", StringComparison.Ordinal)
            .Replace("眷舅兄弟", "眷舅外祖父", StringComparison.Ordinal)
            .Replace("眷舅兄", "眷舅外祖父", StringComparison.Ordinal)
            .Replace("眷外曾外曾祖", "眷祖", StringComparison.Ordinal)
            .Replace("姻太爷爷", "姻父", StringComparison.Ordinal)
            // Colloquial kin words inside composites normalize to the formal register
            // (our recursive right-sides pick the daily slot: 舅舅/姑姑/婶婶/爷爷/奶奶).
            .Replace("舅舅", "舅", StringComparison.Ordinal)
            .Replace("姑姑", "姑母", StringComparison.Ordinal)
            .Replace("婶婶", "叔母", StringComparison.Ordinal)
            .Replace("表婶", "表叔母", StringComparison.Ordinal)
            .Replace("叔叔", "叔父", StringComparison.Ordinal)
            .Replace("伯伯", "伯父", StringComparison.Ordinal)
            .Replace("阿姨", "姨母", StringComparison.Ordinal)
            .Replace("姻爷爷", "姻祖父", StringComparison.Ordinal)
            .Replace("姻奶奶", "姻祖母", StringComparison.Ordinal)
            .Replace("姻姨男", "姻姨表伯", StringComparison.Ordinal)
            .Replace("姻姨表叔", "姻姨表伯", StringComparison.Ordinal)
            .Replace("姻舅表叔", "姻舅表伯", StringComparison.Ordinal)
            .Replace("姻男", "姻叔父", StringComparison.Ordinal)
            .Replace("姻伯父", "姻叔父", StringComparison.Ordinal)
            .Replace("姻女", "姻姑母", StringComparison.Ordinal)
            .Replace("姻叔母", "姻伯母", StringComparison.Ordinal)
            .Replace("眷男", "眷舅", StringComparison.Ordinal)
            .Replace("眷女", "眷姨母", StringComparison.Ordinal)
            .Replace("眷孙男", "眷舅表兄", StringComparison.Ordinal)
            .Replace("眷外孙男", "眷姨表兄", StringComparison.Ordinal)
            .Replace("眷舅表弟", "眷舅表兄", StringComparison.Ordinal)
            .Replace("眷姨表弟", "眷姨表兄", StringComparison.Ordinal)
            .Replace("眷舅表妹", "眷舅表姐", StringComparison.Ordinal)
            .Replace("眷姨表妹", "眷姨表姐", StringComparison.Ordinal)
            // 眷-side junior-bridge orbit table (wife-side path words vs child-frame words).
            .Replace("眷姨姐婿", "眷姨外祖父", StringComparison.Ordinal)
            .Replace("眷姨妹婿", "眷姨外祖父", StringComparison.Ordinal)
            .Replace("眷姨姐", "眷姨外祖母", StringComparison.Ordinal)
            .Replace("眷姨妹", "眷姨外祖母", StringComparison.Ordinal)
            .Replace("眷姑男", "眷姑表舅", StringComparison.Ordinal)
            .Replace("眷姑女", "眷姑表姨", StringComparison.Ordinal)
            .Replace("眷叔男", "眷堂舅", StringComparison.Ordinal)
            .Replace("眷叔女", "眷堂姨", StringComparison.Ordinal)
            .Replace("眷姨男", "眷姨表舅", StringComparison.Ordinal)
            .Replace("眷姨女", "眷姨表姨", StringComparison.Ordinal)
            .Replace("眷舅男", "眷舅表舅", StringComparison.Ordinal)
            .Replace("眷舅女", "眷舅表姨", StringComparison.Ordinal)
            .Replace("眷姨舅", "眷姨表舅", StringComparison.Ordinal)
            .Replace("眷妇", "眷舅母", StringComparison.Ordinal)
            .Replace("眷婿", "眷姨父", StringComparison.Ordinal)
            .Replace("姻婿", "姻姑父", StringComparison.Ordinal)
            .Replace("眷孙妇", "眷舅表嫂", StringComparison.Ordinal)
            .Replace("眷外孙婿", "眷姨表姐夫", StringComparison.Ordinal)
            .Replace("眷外孙妇", "眷姨表嫂", StringComparison.Ordinal)
            .Replace("眷姨表妹夫", "眷姨表姐夫", StringComparison.Ordinal)
            .Replace("眷姨表弟媳", "眷姨表嫂", StringComparison.Ordinal)
            .Replace("眷舅表妹夫", "眷舅表姐夫", StringComparison.Ordinal)
            .Replace("眷舅表弟媳", "眷舅表嫂", StringComparison.Ordinal)
            .Replace("姻姨姐婿", "姻姨祖父", StringComparison.Ordinal)
            .Replace("姻姨妹婿", "姻姨祖父", StringComparison.Ordinal)
            .Replace("姻姨姐", "姻姨祖母", StringComparison.Ordinal)
            .Replace("姻姨妹", "姻姨祖母", StringComparison.Ordinal)
            .Replace("姻曾外曾外祖", "姻外祖", StringComparison.Ordinal)
            // mumuy's 重表 grade = the stacked female-line 表 our engine writes as 姑表; the
            // 姑-flavor is redundant inside the 姑表 line at 祖-stems (重表姑祖母 ≡ 姑表祖母).
            .Replace("重表", "姑表", StringComparison.Ordinal)
            // mumuy grades shallow-fork juniors one step coarser (从堂甥外孙 at h=2 where
            // our ladder says 堂) — comparison-scope grade cap, junior bases only.
            .Replace("从堂甥", "堂甥", StringComparison.Ordinal)
            .Replace("从堂侄", "堂侄", StringComparison.Ordinal)
            // Sibling-in-law age-order markers (大姑子/小姑子): order-unknown deep chains
            // list both, our composer emits the bare word — strip 大/小 in comparison scope
            // (exact match short-circuits first, so ordered legacy greens are untouched).
            .Replace("大姑子", "姑子", StringComparison.Ordinal)
            .Replace("小姑子", "姑子", StringComparison.Ordinal)
            .Replace("大姨子", "姨子", StringComparison.Ordinal)
            .Replace("小姨子", "姨子", StringComparison.Ordinal)
            .Replace("大姑夫", "姑夫", StringComparison.Ordinal)
            .Replace("小姑夫", "姑夫", StringComparison.Ordinal)
            .Replace("大姨夫", "姨夫", StringComparison.Ordinal)
            .Replace("小姨夫", "姨夫", StringComparison.Ordinal)
            .Replace("大婶子", "婶子", StringComparison.Ordinal)
            .Replace("小婶子", "婶子", StringComparison.Ordinal)
            .Replace("大舅子", "舅子", StringComparison.Ordinal)
            .Replace("小舅子", "舅子", StringComparison.Ordinal)
            // 姻/眷-composite bridges: mumuy's deep table defaults the bridge to 叔-form
            // regardless of sibling order — the order flavor unifies before the connector.
            .Replace("伯眷", "眷", StringComparison.Ordinal)
            .Replace("叔眷", "眷", StringComparison.Ordinal)
            .Replace("伯姻", "姻", StringComparison.Ordinal)
            .Replace("叔姻", "姻", StringComparison.Ordinal);

        // Stack-collapse to FIXPOINT before the A/B rules (single-pass replacement is not
        // confluent on long stacks): mumuy stacks a class prefix per fork level; our engine
        // flattens to one line.
        String collapsed;
        do
        {
            collapsed = canonical;
            canonical = canonical
                .Replace("姑表姑表", "姑表", StringComparison.Ordinal)
                .Replace("舅表姑表", "舅表", StringComparison.Ordinal)
                .Replace("姨表姑表", "姨表", StringComparison.Ordinal)
                .Replace("姑表姨表", "姑表", StringComparison.Ordinal)
                .Replace("姑表舅表", "姑表", StringComparison.Ordinal)
                .Replace("舅表姨表", "舅表", StringComparison.Ordinal)
                .Replace("舅表舅表", "舅表", StringComparison.Ordinal)
                .Replace("姨表姨表", "姨表", StringComparison.Ordinal)
                .Replace("姨表舅表", "姨表", StringComparison.Ordinal)
                .Replace("堂姑表", "堂", StringComparison.Ordinal)
                .Replace("堂姨表", "堂", StringComparison.Ordinal)
                .Replace("堂舅表", "堂", StringComparison.Ordinal);
        }
        while (!string.Equals(collapsed, canonical, StringComparison.Ordinal));

        canonical = canonical
            // A) flavor is redundant inside a 表-line at 祖-level stems (表姑祖 ≡ 表祖) —
            // full 5-flavor × 3-stem matrix.
            .Replace("表姑祖", "表祖", StringComparison.Ordinal)
            .Replace("表伯祖", "表祖", StringComparison.Ordinal)
            .Replace("表叔祖", "表祖", StringComparison.Ordinal)
            .Replace("表姨祖", "表祖", StringComparison.Ordinal)
            .Replace("表舅祖", "表祖", StringComparison.Ordinal)
            .Replace("表姑高", "表高", StringComparison.Ordinal)
            .Replace("表伯高", "表高", StringComparison.Ordinal)
            .Replace("表叔高", "表高", StringComparison.Ordinal)
            .Replace("表姨高", "表高", StringComparison.Ordinal)
            .Replace("表舅高", "表高", StringComparison.Ordinal)
            .Replace("表姑曾", "表曾", StringComparison.Ordinal)
            .Replace("表伯曾", "表曾", StringComparison.Ordinal)
            .Replace("表叔曾", "表曾", StringComparison.Ordinal)
            .Replace("表姨曾", "表曾", StringComparison.Ordinal)
            .Replace("表舅曾", "表曾", StringComparison.Ordinal)
            // Ultra stems (天/烈/太/远/鼻) drop the flavor on the 表-line the same way.
            .Replace("表姑天", "表天", StringComparison.Ordinal)
            .Replace("表伯天", "表天", StringComparison.Ordinal)
            .Replace("表叔天", "表天", StringComparison.Ordinal)
            .Replace("表姨天", "表天", StringComparison.Ordinal)
            .Replace("表舅天", "表天", StringComparison.Ordinal)
            .Replace("表姑烈", "表烈", StringComparison.Ordinal)
            .Replace("表伯烈", "表烈", StringComparison.Ordinal)
            .Replace("表叔烈", "表烈", StringComparison.Ordinal)
            .Replace("表姨烈", "表烈", StringComparison.Ordinal)
            .Replace("表舅烈", "表烈", StringComparison.Ordinal)
            .Replace("表姑太祖", "表太祖", StringComparison.Ordinal)
            .Replace("表伯太祖", "表太祖", StringComparison.Ordinal)
            .Replace("表叔太祖", "表太祖", StringComparison.Ordinal)
            .Replace("表姨太祖", "表太祖", StringComparison.Ordinal)
            .Replace("表舅太祖", "表太祖", StringComparison.Ordinal)
            .Replace("表姑远", "表远", StringComparison.Ordinal)
            .Replace("表伯远", "表远", StringComparison.Ordinal)
            .Replace("表叔远", "表远", StringComparison.Ordinal)
            .Replace("表姨远", "表远", StringComparison.Ordinal)
            .Replace("表舅远", "表远", StringComparison.Ordinal)
            .Replace("表姑鼻", "表鼻", StringComparison.Ordinal)
            .Replace("表伯鼻", "表鼻", StringComparison.Ordinal)
            .Replace("表叔鼻", "表鼻", StringComparison.Ordinal)
            .Replace("表姨鼻", "表鼻", StringComparison.Ordinal)
            .Replace("表舅鼻", "表鼻", StringComparison.Ordinal)
            // B) mumuy's 重表 is line-agnostic; our finer 姑表/舅表/姨表 collapse to the bare
            // 表-line ONLY at grand-generation stems (deep, comparison-scope only).
            .Replace("姑表祖", "表祖", StringComparison.Ordinal)
            .Replace("舅表祖", "表祖", StringComparison.Ordinal)
            .Replace("姨表祖", "表祖", StringComparison.Ordinal)
            .Replace("姑表高", "表高", StringComparison.Ordinal)
            .Replace("舅表高", "表高", StringComparison.Ordinal)
            .Replace("姨表高", "表高", StringComparison.Ordinal)
            .Replace("姑表曾", "表曾", StringComparison.Ordinal)
            .Replace("舅表曾", "表曾", StringComparison.Ordinal)
            .Replace("姨表曾", "表曾", StringComparison.Ordinal)
            .Replace("姑表天", "表天", StringComparison.Ordinal)
            .Replace("舅表天", "表天", StringComparison.Ordinal)
            .Replace("姨表天", "表天", StringComparison.Ordinal)
            .Replace("姑表烈", "表烈", StringComparison.Ordinal)
            .Replace("舅表烈", "表烈", StringComparison.Ordinal)
            .Replace("姨表烈", "表烈", StringComparison.Ordinal)
            .Replace("姑表太祖", "表太祖", StringComparison.Ordinal)
            .Replace("舅表太祖", "表太祖", StringComparison.Ordinal)
            .Replace("姨表太祖", "表太祖", StringComparison.Ordinal)
            .Replace("姑表远", "表远", StringComparison.Ordinal)
            .Replace("舅表远", "表远", StringComparison.Ordinal)
            .Replace("姨表远", "表远", StringComparison.Ordinal)
            .Replace("姑表鼻", "表鼻", StringComparison.Ordinal)
            .Replace("舅表鼻", "表鼻", StringComparison.Ordinal)
            .Replace("姨表鼻", "表鼻", StringComparison.Ordinal)
            .Replace("外甥", "甥", StringComparison.Ordinal) // lexeme-外, not a lineage marker
            .Replace("族", "堂", StringComparison.Ordinal)
            // Order flavor drops at the 族(→堂) grade before ancestor stems only — bare 堂伯
            // (father's cousin) stays untouched.
            .Replace("堂伯高", "堂高", StringComparison.Ordinal)
            .Replace("堂叔高", "堂高", StringComparison.Ordinal)
            .Replace("堂伯曾", "堂曾", StringComparison.Ordinal)
            .Replace("堂叔曾", "堂曾", StringComparison.Ordinal)
            // A') 姑/姨/舅 flavors under a 堂-grade imply a female crossing (they cannot arise
            // on a pure male line), so at grand stems they bridge to the 表-line — mumuy
            // grades crossed chains with paternal-numeric words (四从父姨祖母 = our 舅表姑祖母).
            .Replace("堂姑祖", "表祖", StringComparison.Ordinal)
            .Replace("堂姨祖", "表祖", StringComparison.Ordinal)
            .Replace("堂舅祖", "表祖", StringComparison.Ordinal)
            .Replace("堂姑高", "表高", StringComparison.Ordinal)
            .Replace("堂姨高", "表高", StringComparison.Ordinal)
            .Replace("堂舅高", "表高", StringComparison.Ordinal)
            .Replace("堂姑曾", "表曾", StringComparison.Ordinal)
            .Replace("堂姨曾", "表曾", StringComparison.Ordinal)
            .Replace("堂舅曾", "表曾", StringComparison.Ordinal)
            // A'-ultra: the same 堂-grade crossing flavors bridge at the ultra stems too
            // (堂姨天祖父 ≡ 表天祖父 — r37 covered A/B but missed this side).
            .Replace("堂姑天", "表天", StringComparison.Ordinal)
            .Replace("堂姨天", "表天", StringComparison.Ordinal)
            .Replace("堂舅天", "表天", StringComparison.Ordinal)
            .Replace("堂姑烈", "表烈", StringComparison.Ordinal)
            .Replace("堂姨烈", "表烈", StringComparison.Ordinal)
            .Replace("堂舅烈", "表烈", StringComparison.Ordinal)
            .Replace("堂姑太祖", "表太祖", StringComparison.Ordinal)
            .Replace("堂姨太祖", "表太祖", StringComparison.Ordinal)
            .Replace("堂舅太祖", "表太祖", StringComparison.Ordinal)
            .Replace("堂姑远", "表远", StringComparison.Ordinal)
            .Replace("堂姨远", "表远", StringComparison.Ordinal)
            .Replace("堂舅远", "表远", StringComparison.Ordinal)
            .Replace("堂姑鼻", "表鼻", StringComparison.Ordinal)
            .Replace("堂姨鼻", "表鼻", StringComparison.Ordinal)
            .Replace("堂舅鼻", "表鼻", StringComparison.Ordinal)
            // 从堂-grade at ultra stems drops to plain 堂 like the 伯/叔 drops do.
            .Replace("从堂天", "堂天", StringComparison.Ordinal)
            .Replace("从堂烈", "堂烈", StringComparison.Ordinal)
            .Replace("从堂太祖", "堂太祖", StringComparison.Ordinal)
            .Replace("从堂远", "堂远", StringComparison.Ordinal)
            .Replace("从堂鼻", "堂鼻", StringComparison.Ordinal)
            // A'') stem-interior 外 under a 堂-grade also certifies a crossing (族高外祖父);
            // bridge those stems to the 表-line so the composer's crossing-aware twins meet
            // them (plain 曾外祖父/外祖父 WITHOUT the grade prefix stay untouched).
            .Replace("堂高外祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂曾外祖", "表曾祖", StringComparison.Ordinal)
            .Replace("堂外祖", "表祖", StringComparison.Ordinal)
            .Replace("表高外祖", "表高祖", StringComparison.Ordinal)
            .Replace("表曾外祖", "表曾祖", StringComparison.Ordinal)
            .Replace("表外祖", "表祖", StringComparison.Ordinal)
            // Flavored 外-stems under a 堂-grade (crossing certified by the 外 itself, so
            // 伯/叔 are safe here) and the composite 曾外曾-ladder both land on the 表-line.
            .Replace("堂姑外祖", "表祖", StringComparison.Ordinal)
            .Replace("堂姨外祖", "表祖", StringComparison.Ordinal)
            .Replace("堂舅外祖", "表祖", StringComparison.Ordinal)
            .Replace("堂伯外祖", "表祖", StringComparison.Ordinal)
            .Replace("堂叔外祖", "表祖", StringComparison.Ordinal)
            .Replace("堂外高祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂外曾祖", "表曾祖", StringComparison.Ordinal)
            .Replace("堂曾外曾外祖", "表高祖", StringComparison.Ordinal)
            .Replace("表曾外曾外祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂外曾外曾外祖", "表高祖", StringComparison.Ordinal)
            .Replace("表外曾外曾外祖", "表高祖", StringComparison.Ordinal)
            .Replace("表姑外曾外曾外祖", "表高祖", StringComparison.Ordinal)
            .Replace("表伯外曾外曾外祖", "表高祖", StringComparison.Ordinal)
            .Replace("表叔外曾外曾外祖", "表高祖", StringComparison.Ordinal)
            .Replace("表姨外曾外曾外祖", "表高祖", StringComparison.Ordinal)
            .Replace("表舅外曾外曾外祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂姑外曾外曾外祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂伯外曾外曾外祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂叔外曾外曾外祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂姨外曾外曾外祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂舅外曾外曾外祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂外曾外曾祖", "表高祖", StringComparison.Ordinal)
            .Replace("表外曾外曾祖", "表高祖", StringComparison.Ordinal)
            .Replace("表姑外曾外曾祖", "表高祖", StringComparison.Ordinal)
            .Replace("表伯外曾外曾祖", "表高祖", StringComparison.Ordinal)
            .Replace("表叔外曾外曾祖", "表高祖", StringComparison.Ordinal)
            .Replace("表姨外曾外曾祖", "表高祖", StringComparison.Ordinal)
            .Replace("表舅外曾外曾祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂姑外曾外曾祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂伯外曾外曾祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂叔外曾外曾祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂姨外曾外曾祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂舅外曾外曾祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂曾外曾祖", "表高祖", StringComparison.Ordinal)
            .Replace("表曾外曾祖", "表高祖", StringComparison.Ordinal)
            .Replace("表姑外祖", "表祖", StringComparison.Ordinal)
            .Replace("表姨外祖", "表祖", StringComparison.Ordinal)
            .Replace("表舅外祖", "表祖", StringComparison.Ordinal)
            .Replace("表伯外祖", "表祖", StringComparison.Ordinal)
            .Replace("表叔外祖", "表祖", StringComparison.Ordinal)
            // Full flavor × 外-stem matrix (重表伯外高祖父-family): the 外 sits between the
            // flavor and the stem, so the plain flavor-drop rules above never see these.
            .Replace("表姑外高祖", "表高祖", StringComparison.Ordinal)
            .Replace("表伯外高祖", "表高祖", StringComparison.Ordinal)
            .Replace("表叔外高祖", "表高祖", StringComparison.Ordinal)
            .Replace("表姨外高祖", "表高祖", StringComparison.Ordinal)
            .Replace("表舅外高祖", "表高祖", StringComparison.Ordinal)
            .Replace("表姑外曾祖", "表曾祖", StringComparison.Ordinal)
            .Replace("表伯外曾祖", "表曾祖", StringComparison.Ordinal)
            .Replace("表叔外曾祖", "表曾祖", StringComparison.Ordinal)
            .Replace("表姨外曾祖", "表曾祖", StringComparison.Ordinal)
            .Replace("表舅外曾祖", "表曾祖", StringComparison.Ordinal)
            .Replace("堂姑外高祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂伯外高祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂叔外高祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂姨外高祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂舅外高祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂姑外曾祖", "表曾祖", StringComparison.Ordinal)
            .Replace("堂伯外曾祖", "表曾祖", StringComparison.Ordinal)
            .Replace("堂叔外曾祖", "表曾祖", StringComparison.Ordinal)
            .Replace("堂姨外曾祖", "表曾祖", StringComparison.Ordinal)
            .Replace("堂舅外曾祖", "表曾祖", StringComparison.Ordinal)
            // Double-外 stems (mumuy 族外高外祖父 = our crossed 高祖-tier): both crossings
            // live in the stem, one 外 per crossing — bridge to the flat 表-line stem,
            // flavored variants included (重表姑外高外祖母).
            .Replace("堂外高外祖", "表高祖", StringComparison.Ordinal)
            .Replace("表外高外祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂外曾外祖", "表曾祖", StringComparison.Ordinal)
            .Replace("表外曾外祖", "表曾祖", StringComparison.Ordinal)
            .Replace("表姑外高外祖", "表高祖", StringComparison.Ordinal)
            .Replace("表伯外高外祖", "表高祖", StringComparison.Ordinal)
            .Replace("表叔外高外祖", "表高祖", StringComparison.Ordinal)
            .Replace("表姨外高外祖", "表高祖", StringComparison.Ordinal)
            .Replace("表舅外高外祖", "表高祖", StringComparison.Ordinal)
            .Replace("表姑外曾外祖", "表曾祖", StringComparison.Ordinal)
            .Replace("表伯外曾外祖", "表曾祖", StringComparison.Ordinal)
            .Replace("表叔外曾外祖", "表曾祖", StringComparison.Ordinal)
            .Replace("表姨外曾外祖", "表曾祖", StringComparison.Ordinal)
            .Replace("表舅外曾外祖", "表曾祖", StringComparison.Ordinal)
            .Replace("堂姑外高外祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂伯外高外祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂叔外高外祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂姨外高外祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂舅外高外祖", "表高祖", StringComparison.Ordinal)
            .Replace("堂姑外曾外祖", "表曾祖", StringComparison.Ordinal)
            .Replace("堂伯外曾外祖", "表曾祖", StringComparison.Ordinal)
            .Replace("堂叔外曾外祖", "表曾祖", StringComparison.Ordinal)
            .Replace("堂姨外曾外祖", "表曾祖", StringComparison.Ordinal)
            .Replace("堂舅外曾外祖", "表曾祖", StringComparison.Ordinal)
            // Ultra-deep stems (天/烈/太/远/鼻祖): mumuy drops the 伯/叔 flavor there the
            // same way the 高/曾-tier drops do (族烈祖父 vs our 族伯烈祖父).
            .Replace("堂伯天", "堂天", StringComparison.Ordinal)
            .Replace("堂叔天", "堂天", StringComparison.Ordinal)
            .Replace("堂伯烈", "堂烈", StringComparison.Ordinal)
            .Replace("堂叔烈", "堂烈", StringComparison.Ordinal)
            .Replace("堂伯太祖", "堂太祖", StringComparison.Ordinal)
            .Replace("堂叔太祖", "堂太祖", StringComparison.Ordinal)
            .Replace("堂伯远", "堂远", StringComparison.Ordinal)
            .Replace("堂叔远", "堂远", StringComparison.Ordinal)
            .Replace("堂伯鼻", "堂鼻", StringComparison.Ordinal)
            .Replace("堂叔鼻", "堂鼻", StringComparison.Ordinal)
            // B-parent tier: mumuy coarsens deep parent-tier elders to the line-agnostic
            // 重表 (重表姑母 vs our 舅表姑表姑母→舅表姑母), so fine lines collapse to the
            // bare 表-line at flavor terminals too — comparison scope only, and only AFTER
            // the stack-collapse loop has already run inside this pass.
            .Replace("姑表姑", "表姑", StringComparison.Ordinal)
            .Replace("姑表伯", "表伯", StringComparison.Ordinal)
            .Replace("姑表叔", "表叔", StringComparison.Ordinal)
            .Replace("姑表姨", "表姨", StringComparison.Ordinal)
            .Replace("姑表舅", "表舅", StringComparison.Ordinal)
            .Replace("舅表姑", "表姑", StringComparison.Ordinal)
            .Replace("舅表伯", "表伯", StringComparison.Ordinal)
            .Replace("舅表叔", "表叔", StringComparison.Ordinal)
            .Replace("舅表姨", "表姨", StringComparison.Ordinal)
            .Replace("舅表舅", "表舅", StringComparison.Ordinal)
            .Replace("姨表姑", "表姑", StringComparison.Ordinal)
            .Replace("姨表伯", "表伯", StringComparison.Ordinal)
            .Replace("姨表叔", "表叔", StringComparison.Ordinal)
            .Replace("姨表姨", "表姨", StringComparison.Ordinal)
            .Replace("姨表舅", "表舅", StringComparison.Ordinal)
            // Same-generation terminals collapse the fine line the same way (mumuy 重表姐
            // vs our 舅表姐) — sibling words and their affinal closures. These live AFTER
            // the stack-collapse loop: a line-prefixed rule in the early block would eat a
            // stack's tail (舅表[姑表妯娌] → 舅表表妯娌) before the collapse could run.
            .Replace("姑表妯娌", "表妯娌", StringComparison.Ordinal)
            .Replace("舅表妯娌", "表妯娌", StringComparison.Ordinal)
            .Replace("姨表妯娌", "表妯娌", StringComparison.Ordinal)
            .Replace("姑表兄", "表兄", StringComparison.Ordinal)
            .Replace("姑表弟", "表弟", StringComparison.Ordinal)
            .Replace("姑表姐", "表姐", StringComparison.Ordinal)
            .Replace("姑表妹", "表妹", StringComparison.Ordinal)
            .Replace("姑表哥", "表哥", StringComparison.Ordinal)
            .Replace("姑表嫂", "表嫂", StringComparison.Ordinal)
            .Replace("舅表兄", "表兄", StringComparison.Ordinal)
            .Replace("舅表弟", "表弟", StringComparison.Ordinal)
            .Replace("舅表姐", "表姐", StringComparison.Ordinal)
            .Replace("舅表妹", "表妹", StringComparison.Ordinal)
            .Replace("舅表哥", "表哥", StringComparison.Ordinal)
            .Replace("舅表嫂", "表嫂", StringComparison.Ordinal)
            .Replace("姨表兄", "表兄", StringComparison.Ordinal)
            .Replace("姨表弟", "表弟", StringComparison.Ordinal)
            .Replace("姨表姐", "表姐", StringComparison.Ordinal)
            .Replace("姨表妹", "表妹", StringComparison.Ordinal)
            .Replace("姨表哥", "表哥", StringComparison.Ordinal)
            .Replace("姨表嫂", "表嫂", StringComparison.Ordinal)
            // The legacy semantic-fold family cannot carry the penult-gender slot, so the
            // 外-slot inside junior ladders is coarse in comparison scope (从堂甥外孙女 vs
            // our fold's 堂甥孙女) — symmetric on both sides, product face unaffected.
            // Deep junior 外-piles flatten to their tier first (侄外曾外曾外孙女 → 侄玄孙女).
            .Replace("外曾外曾外孙", "玄孙", StringComparison.Ordinal)
            .Replace("曾外曾外孙", "玄孙", StringComparison.Ordinal)
            .Replace("外曾外曾孙", "玄孙", StringComparison.Ordinal)
            .Replace("外曾外孙", "曾孙", StringComparison.Ordinal)
            .Replace("甥外孙", "甥孙", StringComparison.Ordinal)
            .Replace("侄外孙", "侄孙", StringComparison.Ordinal)
            // Junior composites collapse the fine 表-line the same way the elder stems do.
            .Replace("姑表甥", "表甥", StringComparison.Ordinal)
            .Replace("舅表甥", "表甥", StringComparison.Ordinal)
            .Replace("姨表甥", "表甥", StringComparison.Ordinal)
            .Replace("姑表侄", "表侄", StringComparison.Ordinal)
            .Replace("舅表侄", "表侄", StringComparison.Ordinal)
            .Replace("姨表侄", "表侄", StringComparison.Ordinal);
        return canonical;
    }

    private static string ApplyLexicalCanonicalization(string value)
    {
        var canonical = ApplyStructuralCanonicalization(value);
        if (LexicalCanonicalMap.Value.TryGetValue(canonical, out var mapped))
        {
            canonical = mapped;
        }

        canonical = canonical
            .Replace("哥哥", "兄", StringComparison.Ordinal)
            .Replace("哥", "兄", StringComparison.Ordinal)
            .Replace("外甥", "甥", StringComparison.Ordinal)
            .Replace("族", "堂", StringComparison.Ordinal);

        return canonical;
    }

    private static string NormalizeCharacters(string value)
    {
        var normalized = value;
        foreach (var (source, target) in NormalizationRules)
        {
            normalized = normalized.Replace(source, target, StringComparison.Ordinal);
        }

        return normalized;
    }

    private static string Normalize(string value)
    {
        return NormalizeCharacters(value.Trim()).Replace(" ", string.Empty, StringComparison.Ordinal);
    }
}
