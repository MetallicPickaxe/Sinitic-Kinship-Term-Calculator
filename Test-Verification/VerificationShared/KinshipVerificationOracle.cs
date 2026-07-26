using System;
using System.Collections.Generic;
using System.Linq;

using KinshipCalculator.Core.Models;

using MumuyAlgorithm;

namespace KinshipCalculator.Testing.Verification;

public static class KinshipVerificationOracle
{
    private static readonly (string Source, string Canonical)[] CanonicalReplacementRules =
    {
        ("父亲", "父"),
        ("爸爸", "父"),
        ("母亲", "母"),
        ("妈妈", "母"),
        ("爷爷", "祖父"),
        ("太公", "祖父"),
        ("奶奶", "祖母"),
        ("姥爷", "外祖父"),
        ("外公", "外祖父"),
        ("姥姥", "外祖母"),
        ("外婆", "外祖母"),
        ("儿子", "子"),
        ("女儿", "女"),
        ("孙子", "孙"),
        ("大舅", "舅"),
        ("小舅", "舅"),
        ("舅舅", "舅"),
        ("大姨", "姨"),
        ("小姨", "姨"),
        ("姨妈", "姨"),
        ("阿姨", "姨"),
        ("大姑", "姑"),
        ("小姑", "姑"),
        ("姑妈", "姑"),
        ("姑姑", "姑"),
        ("大伯", "伯"),
        ("伯父", "伯"),
        ("大叔", "叔"),
        ("小叔", "叔"),
        ("叔叔", "叔"),
        ("丈夫", "夫"),
        ("老公", "夫"),
        ("妻子", "妻"),
        ("老婆", "妻"),
        ("嫂子", "嫂"),
        ("弟妹", "弟媳"),
        ("妻侄女", "内侄女"),
        ("亲家公", "亲家"),
        ("亲家母", "亲家"),
        ("连襟", "襟兄弟"),
        ("姻姊妹", "姊妹姻姊妹"),
        ("姻兄弟", "兄弟姻兄弟")
    };

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

    public static PersonGender AdvanceGender(PersonGender currentGender, string token)
        => token switch
        {
            "father" => PersonGender.Male,
            "mother" => PersonGender.Female,
            "son" => PersonGender.Male,
            "daughter" => PersonGender.Female,
            "older-brother" => PersonGender.Male,
            "younger-brother" => PersonGender.Male,
            "older-sister" => PersonGender.Female,
            "younger-sister" => PersonGender.Female,
            "spouse" => currentGender == PersonGender.Male ? PersonGender.Female : PersonGender.Male,
            _ => PersonGender.Unknown
        };

    public static string BuildMumuySelector(IReadOnlyList<string> tokens, PersonGender selfGender)
    {
        List<string> selectors = new(tokens.Count);
        PersonGender currentGender = selfGender;

        foreach (string token in tokens)
        {
            selectors.Add(MapToMumuySelector(token, currentGender));
            currentGender = AdvanceGender(currentGender, token);
        }

        return string.Join(",", selectors);
    }

    public static IReadOnlyList<string> ResolveMumuyNames(MumuyResolver resolver, IReadOnlyList<string> tokens, PersonGender selfGender)
        => resolver.ResolveNames(BuildMumuySelector(tokens, selfGender), GetSelfSex(selfGender));

    public static IReadOnlyList<string> ExtractComparableTerms(KinshipResult result, string language = "zh-Hans")
    {
        HashSet<string> terms = new(StringComparer.Ordinal);
        AddLocalizedVariants(terms, result.Term, language);

        foreach (KinshipResolutionOption option in result.Options)
        {
            AddLocalizedVariants(terms, option.Label, language);
            AddLocalizedVariants(terms, option.AlternateLabel, language);
        }

        return terms.ToArray();
    }

    public static bool MatchesExpected(KinshipResult result, string expected, string language = "zh-Hans")
        => ExtractComparableTerms(result, language).Any(candidate => AreEquivalent(candidate, expected));

    public static bool MatchesAny(KinshipResult result, IEnumerable<string> references, string language = "zh-Hans")
    {
        HashSet<string> referenceSet = references
            .Where(static reference => !string.IsNullOrWhiteSpace(reference))
            .SelectMany(ExpandComparableVariants)
            .ToHashSet(StringComparer.Ordinal);

        if (referenceSet.Count == 0)
        {
            return false;
        }

        return ExtractComparableTerms(result, language)
            .SelectMany(ExpandComparableVariants)
            .Any(referenceSet.Contains);
    }

    public static bool AreEquivalent(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return ExpandComparableVariants(left)
            .Overlaps(ExpandComparableVariants(right));
    }

    public static int GetSelfSex(PersonGender selfGender)
        => selfGender == PersonGender.Male ? 1 : 0;

    internal static HashSet<string> ExpandComparableVariants(string value)
    {
        HashSet<string> variants = new(StringComparer.Ordinal);
        foreach (string fragment in value.Split(VariantSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string canonical = Canonicalize(fragment);
            if (!string.IsNullOrWhiteSpace(canonical))
            {
                variants.Add(canonical);
            }
        }

        if (variants.Count == 0)
        {
            string canonical = Canonicalize(value);
            if (!string.IsNullOrWhiteSpace(canonical))
            {
                variants.Add(canonical);
            }
        }

        return variants;
    }

    private static void AddLocalizedVariants(HashSet<string> terms, LocalizedText localizedText, string language)
    {
        string value = localizedText.ForLanguage(language);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (string fragment in value.Split(VariantSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            terms.Add(fragment);
        }

        terms.Add(value);
    }

    private static string Canonicalize(string value)
    {
        string canonical = value.Trim();
        foreach ((string source, string replacement) in CanonicalReplacementRules)
        {
            canonical = canonical.Replace(source, replacement, StringComparison.Ordinal);
        }

        return canonical.Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    private static string MapToMumuySelector(string token, PersonGender currentGender)
        => token switch
        {
            "father" => "f",
            "mother" => "m",
            "son" => "s",
            "daughter" => "d",
            "older-brother" => "ob",
            "younger-brother" => "lb",
            "older-sister" => "os",
            "younger-sister" => "ls",
            "spouse" => currentGender == PersonGender.Male ? "w" : "h",
            _ => throw new ArgumentOutOfRangeException(nameof(token), token, "Unknown kinship token.")
        };
}
