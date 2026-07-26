using KinshipCalculator.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test_Unit;

[TestClass]
public class TangBiaoRuleFamilyTests
{
    private static KinshipCalculator.Core.Services.KinshipCalculator CreateCalculator() => new();

    [TestMethod]
    public void FatherYoungerBrotherSonDaughter_Should_Return_TangZhiNv()
    {
        var result = CreateCalculator().Evaluate(new[] { "father", "younger-brother", "son", "daughter" }, "zh-Hans", PersonGender.Male);

        Assert.AreEqual("堂侄女", result.Term.ForLanguage("zh-Hans"));
    }

    [TestMethod]
    public void MotherOlderBrotherDaughterSon_Should_Return_JiuBiaoShengZi()
    {
        var result = CreateCalculator().Evaluate(new[] { "mother", "older-brother", "daughter", "son" }, "zh-Hans", PersonGender.Male);

        Assert.AreEqual("舅表甥子", result.Term.ForLanguage("zh-Hans"));
    }

    [TestMethod]
    public void MotherFatherOlderBrotherDaughter_Should_Return_TangYi()
    {
        var result = CreateCalculator().Evaluate(new[] { "mother", "father", "older-brother", "daughter" }, "zh-Hans", PersonGender.Male);

        Assert.AreEqual("堂姨", result.Term.ForLanguage("zh-Hans"));
    }

    [TestMethod]
    public void MotherFatherOlderBrotherDaughterSpouse_Should_Return_TangYiZhang()
    {
        var result = CreateCalculator().Evaluate(new[] { "mother", "father", "older-brother", "daughter", "spouse" }, "zh-Hans", PersonGender.Male);

        // K16 contract: standard form is primary, the everyday 丈-spelling follows as a
        // layer variant carried onto the graded composite.
        Assert.AreEqual("堂姨父", result.Term.ForLanguage("zh-Hans"));
        StringAssert.Contains(result.Options[0].AlternateLabel?.ForLanguage("zh-Hans") ?? "", "堂姨丈");
    }

    [TestMethod]
    public void MotherFatherOlderBrotherSon_Should_Return_TangJiu()
    {
        var result = CreateCalculator().Evaluate(new[] { "mother", "father", "older-brother", "son" }, "zh-Hans", PersonGender.Male);

        Assert.AreEqual("堂舅", result.Term.ForLanguage("zh-Hans"));
    }

    [TestMethod]
    public void MotherFatherOlderBrotherSonSpouse_Should_Return_TangJiuMa()
    {
        var result = CreateCalculator().Evaluate(new[] { "mother", "father", "older-brother", "son", "spouse" }, "zh-Hans", PersonGender.Male);

        // K16 contract: standard form is primary, 舅妈 follows as a layer variant.
        Assert.AreEqual("堂舅母", result.Term.ForLanguage("zh-Hans"));
        StringAssert.Contains(result.Options[0].AlternateLabel?.ForLanguage("zh-Hans") ?? "", "堂舅妈");
    }
}
