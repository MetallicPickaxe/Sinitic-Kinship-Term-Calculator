using KinshipCalculator.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test_Unit;

[TestClass]
public class SemanticAlignmentDescendantTests
{
    private static KinshipCalculator.Core.Services.KinshipCalculator CreateCalculator() => new();

    [TestMethod]
    public void DaughterDaughter_Should_Return_WaiSunNv()
    {
        var result = CreateCalculator().Evaluate(new[] { "daughter", "daughter" }, "zh-Hans", PersonGender.Male);

        Assert.AreEqual("外孙女", result.Term.ForLanguage("zh-Hans"));
    }

    [TestMethod]
    public void SonDaughterDaughter_Should_Return_ZengWaiSunNv()
    {
        var result = CreateCalculator().Evaluate(new[] { "son", "daughter", "daughter" }, "zh-Hans", PersonGender.Male);

        Assert.AreEqual("曾外孙女", result.Term.ForLanguage("zh-Hans"));
    }

    [TestMethod]
    public void DaughterDaughterDaughter_Should_Return_WaiZengWaiSunNv()
    {
        var result = CreateCalculator().Evaluate(new[] { "daughter", "daughter", "daughter" }, "zh-Hans", PersonGender.Male);

        Assert.AreEqual("外曾外孙女", result.Term.ForLanguage("zh-Hans"));
    }

    [TestMethod]
    public void SonDaughterDaughterSpouse_Should_Preserve_Wai_In_Spouse_Title()
    {
        var result = CreateCalculator().Evaluate(new[] { "son", "daughter", "daughter", "spouse" }, "zh-Hans", PersonGender.Male);

        Assert.AreEqual("曾外孙婿", result.Term.ForLanguage("zh-Hans"));
    }

    [TestMethod]
    public void DeepMixedDescendantSpouse_Should_Preserve_Late_Wai_Marker()
    {
        var result = CreateCalculator().Evaluate(
            new[] { "son", "son", "son", "son", "son", "daughter", "son", "spouse" },
            "zh-Hans",
            PersonGender.Male);

        Assert.AreEqual("仍外孙媳", result.Term.ForLanguage("zh-Hans"));
    }
}
