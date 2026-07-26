using System.Text;
using System.Text.Json;
using KinshipCalculator.Core.Models;
using KinshipCalculator.Core.Services;
using KinshipCalculator.Testing.Verification;
using CoreKinshipCalculator = KinshipCalculator.Core.Services.KinshipCalculator;

namespace ReferenceAccuracyExporter;

internal static class Program
{
    private static readonly Dictionary<string, string[][]> TokenMap = new(StringComparer.Ordinal)
    {
        ["f"] = new[] { new[] { "F" } },
        ["m"] = new[] { new[] { "M" } },
        ["s"] = new[] { new[] { "S" } },
        ["d"] = new[] { new[] { "D" } },
        ["ob"] = new[] { new[] { "OB" } },
        ["lb"] = new[] { new[] { "YB" } },
        ["os"] = new[] { new[] { "OS" } },
        ["ls"] = new[] { new[] { "YS" } },
        ["xb"] = new[] { new[] { "OB" }, new[] { "YB" } },
        ["xs"] = new[] { new[] { "OS" }, new[] { "YS" } },
        ["w"] = new[] { new[] { "SP" } },
        ["h"] = new[] { new[] { "SP" } },
        ["sp"] = new[] { new[] { "SP" } }
    };

    private static readonly Dictionary<string, string> SymbolToTokenId = new(StringComparer.Ordinal)
    {
        ["F"] = "father",
        ["M"] = "mother",
        ["S"] = "son",
        ["D"] = "daughter",
        ["OB"] = "older-brother",
        ["YB"] = "younger-brother",
        ["OS"] = "older-sister",
        ["YS"] = "younger-sister",
        ["SP"] = "spouse",
        ["AF"] = "adoptive-father",
        ["AM"] = "adoptive-mother",
        ["AS"] = "adoptive-son",
        ["AD"] = "adoptive-daughter"
    };

    private static int Main(string[] args)
    {
        var workbookReviewInput = TryGetArg(args, "--workbook-review-input");
        if (!string.IsNullOrWhiteSpace(workbookReviewInput))
        {
            var workbookReviewOutput = TryGetArg(args, "--workbook-review-output");
            if (string.IsNullOrWhiteSpace(workbookReviewOutput))
            {
                throw new InvalidOperationException("--workbook-review-output is required when --workbook-review-input is provided.");
            }

            return ExportWorkbookReview(workbookReviewInput, workbookReviewOutput);
        }

        var config = ParseArgs(args);
        var repoRoot = ResolveRepoRoot();
        var dataDir = Path.Combine(repoRoot, "Utility", "MumuyAlgorithm", "Data");
        var outputDir = Path.Combine(repoRoot, "Resource", "Data", "Reference");
        Directory.CreateDirectory(outputDir);

        var compactPath = Path.Combine(outputDir, config.OutputPrefix + ".tsv");
        var unsupportedPath = Path.Combine(outputDir, config.OutputPrefix + ".Unsupported.tsv");

        Console.WriteLine($"Loading {config.SourceFileName} ...");
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dataDir, config.SourceFileName), Encoding.UTF8));
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(config.SourceFileName + " is not a JSON object.");
        }

        var aggregated = new Dictionary<string, AggregatedRow>(StringComparer.Ordinal);
        var unsupportedRows = new List<UnsupportedRow>();
        var sourceRowNumber = 2;

        foreach (var property in document.RootElement.EnumerateObject())
        {
            var terms = GetStringList(property.Value);
            foreach (var expanded in ExpandEntry(property.Name))
            {
                if (string.IsNullOrWhiteSpace(expanded.SymbolPath))
                {
                    if (property.Name.Length == 0 && string.IsNullOrWhiteSpace(expanded.Notes))
                    {
                        var selfRow = GetOrCreate(aggregated, string.Empty, Array.Empty<string>());
                        selfRow.AddSourceRowNumber(sourceRowNumber);
                        selfRow.AddRawKey(property.Name);
                        selfRow.AddSelector("(self)");
                        selfRow.AddPrimaryTerms(terms.Count > 0 ? new[] { terms[0] } : Array.Empty<string>());
                        selfRow.AddTerms(terms);
                        selfRow.AddNote("Self row");
                    }
                    else
                    {
                        unsupportedRows.Add(new UnsupportedRow(
                            sourceRowNumber,
                            property.Name,
                            string.IsNullOrWhiteSpace(expanded.Selector) ? "(unexpanded)" : expanded.Selector,
                            terms.Count > 0 ? terms[0] : string.Empty,
                            JoinUnique(terms),
                            expanded.Notes
                        ));
                    }
                }
                else
                {
                    var row = GetOrCreate(aggregated, expanded.SymbolPath, expanded.Symbols);
                    row.AddSourceRowNumber(sourceRowNumber);
                    row.AddRawKey(property.Name);
                    row.AddSelector(expanded.Selector);
                    row.AddPrimaryTerms(terms.Count > 0 ? new[] { terms[0] } : Array.Empty<string>());
                    row.AddTerms(terms);
                    if (!string.IsNullOrWhiteSpace(expanded.Notes))
                    {
                        row.AddNote(expanded.Notes);
                    }
                }

                if (sourceRowNumber % 10000 == 0)
                {
                    Console.WriteLine($"{config.SourceSheetName}: row {sourceRowNumber}");
                }

                sourceRowNumber++;
            }
        }

        var calculator = new CoreKinshipCalculator();
        var orderedRows = aggregated.Values
            .OrderByDescending(static row => row.IsSelf)
            .ThenBy(static row => row.SymbolPath, StringComparer.Ordinal)
            .ToList();

        Console.WriteLine($"Compact rows={orderedRows.Count}");
        Console.WriteLine($"Unsupported rows={unsupportedRows.Count}");

        for (var i = 0; i < orderedRows.Count; i++)
        {
            if (i > 0 && i % 10000 == 0)
            {
                Console.WriteLine($"Evaluated rows={i}");
            }

            var row = orderedRows[i];
            row.MaleOutput = Evaluate(calculator, row.Symbols, PersonGender.Male, row.IsSelf);
            row.FemaleOutput = Evaluate(calculator, row.Symbols, PersonGender.Female, row.IsSelf);
        }

        var judge = args.Any(static arg => string.Equals(arg, "--judge", StringComparison.OrdinalIgnoreCase));
        WriteCompactTsv(compactPath, orderedRows, judge);
        WriteUnsupportedTsv(unsupportedPath, unsupportedRows);

        Console.WriteLine($"Compact={compactPath}");
        Console.WriteLine($"Unsupported={unsupportedPath}");
        return 0;
    }

    private static string? TryGetArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static ExportConfig ParseArgs(string[] args)
    {
        var source = "main";
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--source", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                source = args[++i];
            }
        }

        return source.Trim().ToLowerInvariant() switch
        {
            "main" => new ExportConfig("main.json", "ExpandedMain", "MumuyMainAccuracyCompact"),
            "mode-map" or "modemap" or "mode_map" => new ExportConfig("mode-map.json", "ExpandedModeMap", "MumuyModeMapAccuracyCompact"),
            "multiple" => new ExportConfig("multiple.json", "ExpandedMultiple", "MumuyMultipleAccuracyCompact"),
            _ => throw new InvalidOperationException("Unsupported source. Use main, mode-map, or multiple.")
        };
    }

    private static int ExportWorkbookReview(string inputPath, string outputPath)
    {
        var json = File.ReadAllText(inputPath, Encoding.UTF8);
        var rows = JsonSerializer.Deserialize<List<WorkbookReviewInputRow>>(json) ?? new List<WorkbookReviewInputRow>();
        var calculator = new CoreKinshipCalculator();
        List<WorkbookReviewOutputRow> outputs = new(rows.Count);

        foreach (var row in rows.OrderBy(static row => row.TableRowNumber))
        {
            var symbols = ParseSymbolPath(row.ChainSymbolPath);
            var isSelf = symbols.Count == 0;

            var maleOutput = Evaluate(calculator, symbols, PersonGender.Male, isSelf);
            var femaleOutput = Evaluate(calculator, symbols, PersonGender.Female, isSelf);
            var judgment = ReferenceJudgmentPolicy.EvaluateRow(
                row.MumuyTermSet,
                maleOutput.FirstCandidate,
                femaleOutput.FirstCandidate,
                row.ChainSymbolPath,
                maleOutput.OtherCandidates,
                femaleOutput.OtherCandidates);

            outputs.Add(new WorkbookReviewOutputRow(
                row.TableRowNumber,
                maleOutput.OfficialOrFallback,
                maleOutput.FirstCandidate,
                maleOutput.OtherCandidates,
                maleOutput.IsExactMatch,
                femaleOutput.OfficialOrFallback,
                femaleOutput.FirstCandidate,
                femaleOutput.OtherCandidates,
                femaleOutput.IsExactMatch,
                judgment.CandidateDisplay,
                judgment.JudgmentDisplay,
                judgment.Kind.ToString()
            ));
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(outputPath, JsonSerializer.Serialize(outputs, options), new UTF8Encoding(false));
        Console.WriteLine($"Workbook review output rows={outputs.Count}");
        Console.WriteLine($"WorkbookReview={outputPath}");
        return 0;
    }

    private static AggregatedRow GetOrCreate(Dictionary<string, AggregatedRow> rows, string symbolPath, IReadOnlyList<string> symbols)
    {
        if (!rows.TryGetValue(symbolPath, out var row))
        {
            row = new AggregatedRow(symbolPath, symbols.ToArray());
            rows.Add(symbolPath, row);
        }
        return row;
    }

    private static void WriteCompactTsv(string path, IReadOnlyList<AggregatedRow> rows, bool judge = false)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        var header = new List<string>
        {
            "table_row_number",
            "source_sheet_row_numbers",
            "raw_key_set",
            "chain_selector_set",
            "chain_symbol_path",
            "mumuy_primary_term_candidates",
            "mumuy_term_set",
            "our_official_or_fallback_male",
            "our_daily_folk_1st_candidate_male",
            "our_daily_folk_others_male",
            "our_is_exact_match_male",
            "our_official_or_fallback_female",
            "our_daily_folk_1st_candidate_female",
            "our_daily_folk_others_female",
            "our_is_exact_match_female",
            "notes"
        };
        if (judge)
        {
            header.Add("our_candidate_display");
            header.Add("our_judgment");
        }

        writer.WriteLine(string.Join('\t', header));

        var index = 1;
        foreach (var row in rows)
        {
            var cells = new List<object?>
            {
                index++,
                JoinUnique(row.SourceRowNumbers.Select(static n => n.ToString())),
                JoinUnique(row.RawKeys),
                JoinUnique(row.Selectors),
                row.IsSelf ? "(self)" : row.SymbolPath,
                JoinUnique(row.PrimaryTerms),
                JoinUnique(row.Terms),
                row.MaleOutput.OfficialOrFallback,
                row.MaleOutput.FirstCandidate,
                row.MaleOutput.OtherCandidates,
                row.MaleOutput.IsExactMatch,
                row.FemaleOutput.OfficialOrFallback,
                row.FemaleOutput.FirstCandidate,
                row.FemaleOutput.OtherCandidates,
                row.FemaleOutput.IsExactMatch,
                JoinUnique(row.Notes)
            };

            if (judge)
            {
                var judgment = ReferenceJudgmentPolicy.EvaluateRow(
                    JoinUnique(row.Terms),
                    row.MaleOutput.FirstCandidate,
                    row.FemaleOutput.FirstCandidate,
                    row.IsSelf ? "(self)" : row.SymbolPath,
                    row.MaleOutput.OtherCandidates,
                    row.FemaleOutput.OtherCandidates);
                cells.Add(judgment.CandidateDisplay);
                cells.Add(judgment.JudgmentDisplay);
            }

            WriteRow(writer, cells.ToArray());
        }
    }

    private static void WriteUnsupportedTsv(string path, IReadOnlyList<UnsupportedRow> rows)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.WriteLine(string.Join('\t', new[]
        {
            "table_row_number",
            "source_sheet_row_number",
            "raw_key",
            "chain_selector",
            "mumuy_primary_term",
            "mumuy_term_set",
            "notes"
        }));

        var index = 1;
        foreach (var row in rows)
        {
            WriteRow(
                writer,
                index++,
                row.SourceRowNumber,
                row.RawKey,
                row.Selector,
                row.PrimaryTerm,
                row.TermSet,
                row.Notes
            );
        }
    }

    private static string ResolveRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (Directory.Exists(Path.Combine(current, "KinshipCalculator.Core")) && Directory.Exists(Path.Combine(current, "Utility")))
            {
                return current;
            }
            current = Path.GetDirectoryName(current) ?? string.Empty;
        }
        throw new InvalidOperationException("Repo root not found.");
    }

    private static IReadOnlyList<string> ParseSymbolPath(string? symbolPath)
    {
        if (string.IsNullOrWhiteSpace(symbolPath) || string.Equals(symbolPath, "(self)", StringComparison.Ordinal))
        {
            return Array.Empty<string>();
        }

        return symbolPath
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }

    private static void WriteRow(StreamWriter writer, params object?[] columns)
    {
        static string Sanitize(object? value)
        {
            var text = value?.ToString() ?? string.Empty;
            return text.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
        }

        writer.WriteLine(string.Join('\t', columns.Select(Sanitize)));
    }

    private static List<string> GetStringList(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => new List<string> { element.GetString() ?? string.Empty },
            JsonValueKind.Array => element.EnumerateArray()
                .Where(static item => item.ValueKind == JsonValueKind.String)
                .Select(static item => item.GetString() ?? string.Empty)
                .ToList(),
            _ => new List<string>()
        };
    }

    private static IEnumerable<ExpandedEntry> ExpandEntry(string rawKey)
    {
        foreach (var sequence in ParseSequenceExpression(rawKey))
        {
            var selectorParts = sequence
                .Select(NormalizeSelectorToken)
                .Where(static token => !string.IsNullOrWhiteSpace(token))
                .ToList();

            var unsupported = selectorParts.Where(static token => !TokenMap.ContainsKey(token)).Distinct().ToList();
            var selector = string.Join(",", selectorParts);
            if (unsupported.Count > 0)
            {
                yield return new ExpandedEntry(selector, string.Empty, Array.Empty<string>(), $"Unsupported token(s): {string.Join(',', unsupported)}");
                continue;
            }

            foreach (var symbolPath in ExpandToSymbolPaths(selectorParts))
            {
                yield return new ExpandedEntry(selector, string.Join('.', symbolPath), symbolPath, string.Empty);
            }
        }
    }

    private static List<List<string>> ParseSequenceExpression(string raw)
    {
        var parts = new List<object>();
        var i = 0;
        while (i < raw.Length)
        {
            var ch = raw[i];
            if (ch == '[')
            {
                var depth = 1;
                var j = i + 1;
                while (j < raw.Length && depth > 0)
                {
                    if (raw[j] == '[') depth++;
                    else if (raw[j] == ']') depth--;
                    j++;
                }

                var content = raw.Substring(i + 1, j - i - 2);
                var optionStrings = SplitTopLevelOptions(content);
                var optionSequences = new List<List<string>>();
                foreach (var option in optionStrings)
                {
                    var sequence = option.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                    optionSequences.Add(sequence);
                }
                parts.Add(optionSequences);
                i = j;
                continue;
            }

            if (ch is ',' or ' ')
            {
                i++;
                continue;
            }

            var start = i;
            while (i < raw.Length && raw[i] is not ',' and not '[' and not ']')
            {
                i++;
            }
            var token = raw[start..i].Trim();
            if (token.Length > 0)
            {
                parts.Add(token);
            }
        }

        var sequences = new List<List<string>> { new() };
        foreach (var part in parts)
        {
            if (part is string token)
            {
                sequences = sequences.Select(sequence =>
                {
                    var next = new List<string>(sequence) { token };
                    return next;
                }).ToList();
            }
            else if (part is List<List<string>> options)
            {
                var nextSequences = new List<List<string>>();
                foreach (var sequence in sequences)
                {
                    foreach (var option in options)
                    {
                        var next = new List<string>(sequence);
                        next.AddRange(option);
                        nextSequences.Add(next);
                    }
                }
                sequences = nextSequences;
            }
        }

        return sequences;
    }

    private static List<string> SplitTopLevelOptions(string content)
    {
        var options = new List<string>();
        var depth = 0;
        var buffer = new StringBuilder();
        foreach (var ch in content)
        {
            if (ch == '[') depth++;
            else if (ch == ']') depth--;

            if (ch == '|' && depth == 0)
            {
                var option = buffer.ToString().Trim();
                if (option.Length > 0) options.Add(option);
                buffer.Clear();
                continue;
            }

            buffer.Append(ch);
        }

        var finalOption = buffer.ToString().Trim();
        if (finalOption.Length > 0) options.Add(finalOption);
        return options;
    }

    private static string NormalizeSelectorToken(string token)
    {
        var normalized = token.Trim().Replace("\u200b", string.Empty);
        if (normalized.Length == 0) return string.Empty;
        var ampersand = normalized.IndexOf('&');
        if (ampersand >= 0) normalized = normalized[..ampersand];
        return normalized.Replace("'", string.Empty).ToLowerInvariant();
    }

    private static IEnumerable<string[]> ExpandToSymbolPaths(IReadOnlyList<string> selectorTokens)
    {
        var paths = new List<List<string>> { new() };
        foreach (var token in selectorTokens)
        {
            var nextPaths = new List<List<string>>();
            foreach (var path in paths)
            {
                foreach (var option in TokenMap[token])
                {
                    var next = new List<string>(path);
                    next.AddRange(option);
                    nextPaths.Add(next);
                }
            }
            paths = nextPaths;
        }

        return paths.Select(static path => path.ToArray());
    }

    private static EvaluationOutput Evaluate(CoreKinshipCalculator calculator, IReadOnlyList<string> symbols, PersonGender selfGender, bool isSelf)
    {
        if (isSelf)
        {
            return new EvaluationOutput("自己", "自己", string.Empty, true, "Self row");
        }

        if (symbols.Count == 0)
        {
            return new EvaluationOutput(string.Empty, string.Empty, string.Empty, false, "No symbol path");
        }

        if (symbols.Any(static symbol => !SymbolToTokenId.ContainsKey(symbol)))
        {
            var unsupported = string.Join(',', symbols.Where(static symbol => !SymbolToTokenId.ContainsKey(symbol)).Distinct());
            return new EvaluationOutput(string.Empty, string.Empty, string.Empty, false, $"Unsupported symbol(s): {unsupported}");
        }

        var tokenIds = symbols.Select(static symbol => SymbolToTokenId[symbol]).ToArray();
        var originalOut = Console.Out;
        KinshipResult result;
        try
        {
            Console.SetOut(TextWriter.Null);
            result = calculator.Evaluate(tokenIds, "zh-Hant", selfGender);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var first = result.Options.First();
        var official = first.OfficialDescription.ForLanguage("zh-Hant");
        if (string.IsNullOrWhiteSpace(official))
        {
            official = first.Label.ForLanguage("zh-Hant");
        }

        var firstCandidate = FormatCandidate(first);
        var others = result.Options.Skip(1)
            .Select(FormatCandidate)
            .Where(static text => !string.IsNullOrWhiteSpace(text))
            .Distinct()
            .ToArray();

        return new EvaluationOutput(
            official,
            firstCandidate,
            string.Join(" || ", others),
            result.IsExactMatch,
            string.Empty
        );
    }

    private static string FormatCandidate(KinshipResolutionOption option)
    {
        var label = option.Label.ForLanguage("zh-Hant");
        var alternate = option.HasAlternateLabel ? option.AlternateLabel.ForLanguage("zh-Hant") : string.Empty;
        if (string.IsNullOrWhiteSpace(alternate) || string.Equals(label, alternate, StringComparison.Ordinal))
        {
            return label;
        }
        return $"{label} | {alternate}";
    }

    private static string JoinUnique(IEnumerable<string> values)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<string>();
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (seen.Add(value))
            {
                ordered.Add(value);
            }
        }
        return string.Join(" | ", ordered);
    }
}

internal sealed class AggregatedRow
{
    public AggregatedRow(string symbolPath, IReadOnlyList<string> symbols)
    {
        SymbolPath = symbolPath;
        Symbols = symbols.ToArray();
        IsSelf = symbols.Count == 0 && symbolPath.Length == 0;
    }

    public string SymbolPath { get; }
    public IReadOnlyList<string> Symbols { get; }
    public bool IsSelf { get; }
    public List<int> SourceRowNumbers { get; } = new();
    public List<string> RawKeys { get; } = new();
    public List<string> Selectors { get; } = new();
    public List<string> PrimaryTerms { get; } = new();
    public List<string> Terms { get; } = new();
    public List<string> Notes { get; } = new();
    public EvaluationOutput MaleOutput { get; set; } = new(string.Empty, string.Empty, string.Empty, false, string.Empty);
    public EvaluationOutput FemaleOutput { get; set; } = new(string.Empty, string.Empty, string.Empty, false, string.Empty);

    public void AddSourceRowNumber(int value)
    {
        if (!SourceRowNumbers.Contains(value))
        {
            SourceRowNumbers.Add(value);
        }
    }

    public void AddRawKey(string value) => AddUnique(RawKeys, value);
    public void AddSelector(string value) => AddUnique(Selectors, value);
    public void AddPrimaryTerms(IEnumerable<string> values) => AddUnique(PrimaryTerms, values);
    public void AddTerms(IEnumerable<string> values) => AddUnique(Terms, values);
    public void AddNote(string value) => AddUnique(Notes, value);

    private static void AddUnique(List<string> target, IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            AddUnique(target, value);
        }
    }

    private static void AddUnique(List<string> target, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }
        if (!target.Contains(value, StringComparer.Ordinal))
        {
            target.Add(value);
        }
    }
}

internal sealed record ExportConfig(string SourceFileName, string SourceSheetName, string OutputPrefix);
internal sealed record UnsupportedRow(int SourceRowNumber, string RawKey, string Selector, string PrimaryTerm, string TermSet, string Notes);
internal sealed record ExpandedEntry(string Selector, string SymbolPath, IReadOnlyList<string> Symbols, string Notes);
internal sealed record EvaluationOutput(string OfficialOrFallback, string FirstCandidate, string OtherCandidates, bool IsExactMatch, string Notes);
internal sealed record WorkbookReviewInputRow(int TableRowNumber, string ChainSymbolPath, string MumuyTermSet);
internal sealed record WorkbookReviewOutputRow(
    int TableRowNumber,
    string OurOfficialOrFallbackMale,
    string OurDailyFolk1stCandidateMale,
    string OurDailyFolkOthersMale,
    bool OurIsExactMatchMale,
    string OurOfficialOrFallbackFemale,
    string OurDailyFolk1stCandidateFemale,
    string OurDailyFolkOthersFemale,
    bool OurIsExactMatchFemale,
    string CandidateDisplay,
    string JudgmentDisplay,
    string JudgmentKind);
