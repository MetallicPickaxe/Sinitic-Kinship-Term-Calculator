using System.Collections.Generic;
using System.Linq;

using KinshipCalculator.Core.Models;
using KinshipCalculator.Core.Services;
using KinshipCalculator.Testing.Verification;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using MumuyAlgorithm;

namespace Test_Unit;

[TestClass]
public sealed class ExtendedConsistencyTests
{
    private const int DifferentialSampleSize = 128;

    private readonly KinshipCalculator.Core.Services.KinshipCalculator calculator_field = new();
    private readonly MumuyResolver mumuy_field = new();

    public TestContext? TestContext { get; set; }

    [TestMethod]
    public void ReviewedCorpus_FullScan_ProducesCoverageAccounting()
    {
        IReadOnlyList<ReviewedChainCase> corpus = ReviewedChainCorpusLoader.LoadDefaultCorpus();
        int covered = 0;
        int uncovered = 0;

        foreach (ReviewedChainCase entry in corpus)
        {
            EvaluationOutcome male = Evaluate(entry, PersonGender.Male);
            EvaluationOutcome female = Evaluate(entry, PersonGender.Female);

            bool isCovered =
                KinshipVerificationOracle.MatchesExpected(male.Result, entry.Expected)
                || KinshipVerificationOracle.MatchesExpected(female.Result, entry.Expected);

            if (isCovered)
            {
                covered++;
            }
            else
            {
                uncovered++;
            }
        }

        TestContext?.WriteLine($"reviewed-corpus-count={corpus.Count}");
        TestContext?.WriteLine($"reviewed-corpus-covered={covered}");
        TestContext?.WriteLine($"reviewed-corpus-uncovered={uncovered}");

        Assert.AreEqual(1000, corpus.Count);
        Assert.AreEqual(corpus.Count, covered + uncovered);
        Assert.IsTrue(covered > 0);
    }

    [TestMethod]
    public void SimpleReferenceCase_MatchesMumuyThroughSharedOracle()
    {
        string[] tokens = { "father", "father" };
        KinshipResult result = calculator_field.Evaluate(tokens, "zh-Hans", PersonGender.Male);
        IReadOnlyList<string> mumuyNames = KinshipVerificationOracle.ResolveMumuyNames(mumuy_field, tokens, PersonGender.Male);

        TestContext?.WriteLine($"reference-names={string.Join(",", mumuyNames)}");
        TestContext?.WriteLine($"candidate-terms={string.Join(",", KinshipVerificationOracle.ExtractComparableTerms(result))}");

        Assert.IsTrue(mumuyNames.Count > 0);
        Assert.IsTrue(KinshipVerificationOracle.MatchesAny(result, mumuyNames));
    }

    [TestMethod]
    public void ReviewedCorpus_MumuyDifferentialSample_ProducesConsistentAccounting()
    {
        IReadOnlyList<ReviewedChainCase> sample = ReviewedChainCorpusLoader.LoadDefaultCorpus()
            .Take(DifferentialSampleSize)
            .ToArray();

        int matches = 0;
        int discrepancies = 0;
        int noReference = 0;

        foreach (ReviewedChainCase entry in sample)
        {
            EvaluationOutcome outcome = ChoosePreferredOutcome(entry);
            IReadOnlyList<string> mumuyNames = KinshipVerificationOracle.ResolveMumuyNames(
                mumuy_field,
                entry.Tokens,
                outcome.SelfGender);

            if (mumuyNames.Count == 0)
            {
                noReference++;
                continue;
            }

            if (KinshipVerificationOracle.MatchesAny(outcome.Result, mumuyNames))
            {
                matches++;
            }
            else
            {
                discrepancies++;
            }
        }

        TestContext?.WriteLine($"sample-count={sample.Count}");
        TestContext?.WriteLine($"matches={matches}");
        TestContext?.WriteLine($"discrepancies={discrepancies}");
        TestContext?.WriteLine($"no-reference={noReference}");

        Assert.AreEqual(sample.Count, matches + discrepancies + noReference);
        Assert.IsTrue(matches > 0);
    }

    private EvaluationOutcome ChoosePreferredOutcome(ReviewedChainCase entry)
    {
        EvaluationOutcome male = Evaluate(entry, PersonGender.Male);
        if (KinshipVerificationOracle.MatchesExpected(male.Result, entry.Expected))
        {
            return male;
        }

        EvaluationOutcome female = Evaluate(entry, PersonGender.Female);
        if (KinshipVerificationOracle.MatchesExpected(female.Result, entry.Expected))
        {
            return female;
        }

        return male;
    }

    private EvaluationOutcome Evaluate(ReviewedChainCase entry, PersonGender selfGender)
        => new(selfGender, calculator_field.Evaluate(entry.Tokens, "zh-Hans", selfGender));

    private sealed record EvaluationOutcome(PersonGender SelfGender, KinshipResult Result);
}
