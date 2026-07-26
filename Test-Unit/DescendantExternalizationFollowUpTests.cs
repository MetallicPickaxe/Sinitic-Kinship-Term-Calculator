using KinshipCalculator.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test_Unit;

[TestClass]
public class DescendantExternalizationFollowUpTests
{
    private static KinshipCalculator.Core.Services.KinshipCalculator CreateCalculator() => new();

    [TestMethod]
    public void DaughterSonDaughter_Should_Return_WaiZengSunNv_In_Traditional()
    {
        var result = CreateCalculator().Evaluate(new[] { "daughter", "son", "daughter" }, "zh-Hant", PersonGender.Male);

        Assert.AreEqual("外曾孫女", result.Term.ForLanguage("zh-Hant"));
    }

    [TestMethod]
    public void DaughterSonDaughterSpouse_Should_Return_WaiZengSunXu_In_Traditional()
    {
        var result = CreateCalculator().Evaluate(new[] { "daughter", "son", "daughter", "spouse" }, "zh-Hant", PersonGender.Male);

        Assert.AreEqual("外曾孫婿", result.Term.ForLanguage("zh-Hant"));
    }
}
