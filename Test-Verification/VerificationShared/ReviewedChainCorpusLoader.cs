using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace KinshipCalculator.Testing.Verification;

public static class ReviewedChainCorpusLoader
{
    private const string DefaultRelativePath = "Data/LongChainsReviewed.json";
    private static IReadOnlyList<ReviewedChainCase>? cache_field;

    public static IReadOnlyList<ReviewedChainCase> LoadDefaultCorpus()
    {
        cache_field ??= LoadFromFile(ResolveDefaultPath());
        return cache_field;
    }

    public static IReadOnlyList<ReviewedChainCase> LoadFromFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        IReadOnlyList<ReviewedChainCase>? records = JsonSerializer.Deserialize<List<ReviewedChainCase>>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (records is null || records.Count == 0)
        {
            throw new InvalidOperationException($"Reviewed corpus is empty: {path}");
        }

        if (records.Any(static record =>
                record.Tokens is null
                || record.Tokens.Length == 0
                || string.IsNullOrWhiteSpace(record.Expected)
                || string.IsNullOrWhiteSpace(record.Path)))
        {
            throw new InvalidOperationException($"Reviewed corpus contains invalid entries: {path}");
        }

        return records;
    }

    public static string ResolveDefaultPath()
    {
        string outputPath = Path.Combine(AppContext.BaseDirectory, DefaultRelativePath);
        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        // Output layouts differ in depth (with/without platform and RID segments),
        // so walk upward until the repository-level Test-Unit copy appears.
        DirectoryInfo? probe = new(AppContext.BaseDirectory);
        while (probe is not null)
        {
            string repoFallback = Path.Combine(probe.FullName, "Test-Unit", "Data", "LongChainsReviewed.json");
            if (File.Exists(repoFallback))
            {
                return repoFallback;
            }

            probe = probe.Parent;
        }

        throw new FileNotFoundException($"Unable to locate reviewed corpus at {DefaultRelativePath}.", outputPath);
    }
}
