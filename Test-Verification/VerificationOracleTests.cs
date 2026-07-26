using System.Linq;

using KinshipCalculator.Core.Models;
using KinshipCalculator.Testing.Verification;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test_Verification;

[TestClass]
public sealed class VerificationOracleTests
{
    [TestMethod]
    public void BuildMumuySelector_MapsSpouseUsingCurrentGender()
    {
        Assert.AreEqual("w,f", KinshipVerificationOracle.BuildMumuySelector(new[] { "spouse", "father" }, PersonGender.Male));
        Assert.AreEqual("h,f", KinshipVerificationOracle.BuildMumuySelector(new[] { "spouse", "father" }, PersonGender.Female));
    }

    [TestMethod]
    public void AdvanceGender_FollowsTokenTransitions()
    {
        PersonGender spouseGender = KinshipVerificationOracle.AdvanceGender(PersonGender.Male, "spouse");
        PersonGender childGender = KinshipVerificationOracle.AdvanceGender(spouseGender, "daughter");

        Assert.AreEqual(PersonGender.Female, spouseGender);
        Assert.AreEqual(PersonGender.Female, childGender);
    }

    [TestMethod]
    public void ExtractComparableTerms_IncludesPrimaryAndAlternateVariants()
    {
        KinshipResult result = CreateResult("父亲", "爸爸|老爸", "爹");

        string[] terms = KinshipVerificationOracle.ExtractComparableTerms(result).ToArray();

        CollectionAssert.Contains(terms, "父亲");
        CollectionAssert.Contains(terms, "爸爸");
        CollectionAssert.Contains(terms, "老爸");
        CollectionAssert.Contains(terms, "爹");
    }

    [TestMethod]
    public void AreEquivalent_AcceptsCanonicalAliases()
    {
        Assert.IsTrue(KinshipVerificationOracle.AreEquivalent("爷爷", "祖父"));
        Assert.IsTrue(KinshipVerificationOracle.AreEquivalent("外婆", "外祖母"));
    }

    [TestMethod]
    public void AreEquivalent_AcceptsPipeSeparatedAlternates()
    {
        Assert.IsTrue(KinshipVerificationOracle.AreEquivalent("祖父", "爷爷|别称"));
        Assert.IsTrue(KinshipVerificationOracle.AreEquivalent("父", "父亲|老爸"));
    }

    [TestMethod]
    public void AreEquivalent_RejectsDifferentRelationships()
    {
        Assert.IsFalse(KinshipVerificationOracle.AreEquivalent("祖父", "祖母"));
        Assert.IsFalse(KinshipVerificationOracle.AreEquivalent("伯", "叔"));
    }

    [TestMethod]
    public void LoadDefaultCorpus_LoadsStructuredEntries()
    {
        ReviewedChainCase first = ReviewedChainCorpusLoader.LoadDefaultCorpus().First();

        Assert.IsTrue(first.Tokens.Length > 0);
        Assert.IsFalse(string.IsNullOrWhiteSpace(first.Expected));
        Assert.IsFalse(string.IsNullOrWhiteSpace(first.Path));
    }

    private static KinshipResult CreateResult(string primary, string alternate, string secondary)
    {
        KinshipResolutionOption option1 = new(
            new LocalizedText(primary, primary, primary),
            true,
            LocalizedText.Empty,
            LocalizedText.Empty,
            string.Empty,
            string.Empty,
            RelationVector.Empty,
            new LocalizedText(alternate, alternate, alternate));

        KinshipResolutionOption option2 = new(
            new LocalizedText(secondary, secondary, secondary),
            true,
            LocalizedText.Empty,
            LocalizedText.Empty,
            string.Empty,
            string.Empty,
            RelationVector.Empty);

        return new KinshipResult(new[] { option1, option2 }, LocalizedText.Empty);
    }
}
