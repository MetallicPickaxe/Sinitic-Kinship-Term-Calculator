using KinshipCalculator.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test_Unit;

[TestClass]
public class DeepAncestorNamingStrategyTests
{
    private static KinshipCalculator.Core.Services.KinshipCalculator CreateCalculator() => new();

    [DataTestMethod]
    [DataRow("father,father,father,father,father,father,father,father,father", "鼻祖父")]
    [DataRow("father,father,father,father,father,father,father,father,father,father", "開祖父")]
    [DataRow("father,father,father,father,father,father,father,father,father,father,father", "始祖父")]
    [DataRow("father,father,father,father,father,father,father,father,father,father,father,father", "先祖父")]
    public void DeepMaleAncestors_Should_Use_Reviewed_Traditional_Stems(string chain, string expected)
    {
        var result = CreateCalculator().Evaluate(chain.Split(','), "zh-Hant", PersonGender.Male);

        Assert.AreEqual(expected, result.Term.ForLanguage("zh-Hant"));
    }

    [DataTestMethod]
    [DataRow("father,father,father,father,father,father,father,father,mother", "鼻祖母")]
    [DataRow("father,father,father,father,father,father,father,father,father,mother", "開祖母")]
    [DataRow("father,father,father,father,father,father,father,father,father,father,mother", "始祖母")]
    [DataRow("father,father,father,father,father,father,father,father,father,father,father,mother", "先祖母")]
    public void DeepFemaleAncestors_Should_Use_Reviewed_Traditional_Stems(string chain, string expected)
    {
        var result = CreateCalculator().Evaluate(chain.Split(','), "zh-Hant", PersonGender.Male);

        Assert.AreEqual(expected, result.Term.ForLanguage("zh-Hant"));
    }
}
