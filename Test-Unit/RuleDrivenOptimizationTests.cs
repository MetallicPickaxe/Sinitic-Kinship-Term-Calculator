using KinshipCalculator.Core.Models;
using KinshipCalculator.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Test_Unit;

[TestClass]
public class RuleDrivenOptimizationTests
{
    private static KinshipCalculator.Core.Services.KinshipCalculator CreateCalculator() => new();

    [TestMethod]
    public void Test_Grandnephew_Wife_Brother_Dynamic_Resolution()
    {
        // Case: OB -> D -> S -> SP -> YB (Brother -> Daughter -> Son -> Spouse -> Younger Brother)
        // This was previously hardcoded, now dynamic.
        var calculator = CreateCalculator();
        var result = calculator.Evaluate(new[] { "older-brother", "daughter", "son", "spouse", "younger-brother" }, "zh-Hans", PersonGender.Male);
        
        Assert.IsTrue(result.IsExactMatch, "Should be exact match via rule engine");
        Assert.AreEqual("侄外孙眷孙男", result.Term.ForLanguage("zh-Hans"));
        
        // Colloquial check
        var option = result.Options.First();
        Assert.AreEqual("侄外孙的小舅子", option.AlternateLabel?.ForLanguage("zh-Hans"));
    }

    [TestMethod]
    public void Test_Grandnephew_Variant_Dynamic_Resolution()
    {
        // Case: OB -> S -> S -> SP -> YB (Brother -> Son -> Son -> Spouse -> Younger Brother)
        // This was NEVER in the dictionary, tests the power of the new rule.
        var calculator = CreateCalculator();
        var result = calculator.Evaluate(new[] { "older-brother", "son", "son", "spouse", "younger-brother" }, "zh-Hans", PersonGender.Male);
        
        Assert.IsTrue(result.IsExactMatch);
        Assert.AreEqual("侄孙眷孙男", result.Term.ForLanguage("zh-Hans"));
        Assert.AreEqual("侄孙的小舅子", result.Options.First().AlternateLabel?.ForLanguage("zh-Hans"));
    }

    [TestMethod]
    public void Test_Sibling_CoParent_In_Law_Dynamic()
    {
        var calculator = CreateCalculator();

        // Brother side
        var res1 = calculator.Evaluate(new[] { "younger-brother", "daughter", "spouse", "father" }, "zh-Hans", PersonGender.Male);
        Assert.AreEqual("侄姻兄弟", res1.Term.ForLanguage("zh-Hans"));

        // Sister side (Extends support beyond previous hardcoding)
        var res2 = calculator.Evaluate(new[] { "older-sister", "son", "spouse", "father" }, "zh-Hans", PersonGender.Male);
        Assert.AreEqual("甥姻兄弟", res2.Term.ForLanguage("zh-Hans"));

        // Female parent
        var res3 = calculator.Evaluate(new[] { "older-brother", "son", "spouse", "mother" }, "zh-Hans", PersonGender.Male);
        Assert.AreEqual("侄姻姊妹", res3.Term.ForLanguage("zh-Hans"));
    }

    [TestMethod]
    public void Test_Collateral_Spouse_Parent_Dynamic()
    {
        var calculator = CreateCalculator();

        // Maternal Aunt side (Previously hardcoded)
        var res1 = calculator.Evaluate(new[] { "mother", "older-sister", "spouse", "father" }, "zh-Hans", PersonGender.Male);
        Assert.AreEqual("姨姻祖父", res1.Term.ForLanguage("zh-Hans"));

        // Paternal Uncle side (New support)
        var res2 = calculator.Evaluate(new[] { "father", "younger-brother", "spouse", "father" }, "zh-Hans", PersonGender.Male);
        Assert.AreEqual("叔姻祖父", res2.Term.ForLanguage("zh-Hans"));

        // Maternal Uncle side + Female parent (New support)
        var res3 = calculator.Evaluate(new[] { "mother", "older-brother", "spouse", "mother" }, "zh-Hans", PersonGender.Male);
        Assert.AreEqual("舅姻祖母", res3.Term.ForLanguage("zh-Hans"));
    }

    [TestMethod]
    public void Test_Adoptive_Relations_Dynamic()
    {
        var calculator = CreateCalculator();

        // Basic Adoptive (Previously hardcoded)
        var res1 = calculator.Evaluate(new[] { "adoptive-father" }, "zh-Hans", PersonGender.Male);
        Assert.AreEqual("养父", res1.Term.ForLanguage("zh-Hans"));

        // Extended Adoptive (New support)
        var res2 = calculator.Evaluate(new[] { "adoptive-father", "father" }, "zh-Hans", PersonGender.Male);
        Assert.AreEqual("养祖父", res2.Term.ForLanguage("zh-Hans"));

        var res3 = calculator.Evaluate(new[] { "adoptive-son", "son" }, "zh-Hans", PersonGender.Male);
        Assert.AreEqual("养孙子", res3.Term.ForLanguage("zh-Hans"));
    }

    [TestMethod]
    public void Test_Step_Relations_Dynamic()
    {
        var calculator = CreateCalculator();

        // Step Mother
        var res1 = calculator.Evaluate(new[] { "father", "spouse" }, "zh-Hans", PersonGender.Male);
        Assert.AreEqual("继母", res1.Term.ForLanguage("zh-Hans"));

        // Step Grandfather (Extended)
        var res2 = calculator.Evaluate(new[] { "father", "father", "spouse" }, "zh-Hans", PersonGender.Male);
        Assert.AreEqual("继祖母", res2.Term.ForLanguage("zh-Hans"));
    }

    [TestMethod]
    public void Test_Deep_Cousin_Relations_Dynamic()
    {
        var calculator = CreateCalculator();

        // Tang Shu (Father's Father's Younger Brother's Son)
        // F.F.YB.S -> Gen Change = 2 - 1 = +1. Strictly Paternal = True. Sibling = YB (Younger)
        var res1 = calculator.Evaluate(new[] { "father", "father", "younger-brother", "son" }, "zh-Hans", PersonGender.Male);
        Assert.AreEqual("堂叔", res1.Term.ForLanguage("zh-Hans"));

        // Tang Zhi (Father's Older Brother's Son's Son)
        // F.OB.S.S -> Gen Change = 1 - 2 = -1. Strictly Paternal = True.
        var res2 = calculator.Evaluate(new[] { "father", "older-brother", "son", "son" }, "zh-Hans", PersonGender.Male);
        Assert.AreEqual("堂侄子", res2.Term.ForLanguage("zh-Hans"));

        // Tang Jiu (Mother's Father's Older Brother's Son)
        // M.F.OB.S -> split point is Father + Brother, so the reviewed slice remains Tang.
        var res3 = calculator.Evaluate(new[] { "mother", "father", "older-brother", "son" }, "zh-Hans", PersonGender.Male);
        Assert.AreEqual("堂舅", res3.Term.ForLanguage("zh-Hans"));

        // Jiu-Biao Sheng (Maternal Uncle's Daughter's Son)
        // M.OB.D.S -> the bounded redesign keeps the refined 舅表 branch instead of collapsing to generic 表.
        var res4 = calculator.Evaluate(new[] { "mother", "older-brother", "daughter", "son" }, "zh-Hans", PersonGender.Male);
        Assert.AreEqual("舅表甥子", res4.Term.ForLanguage("zh-Hans"));
    }

    [TestMethod]
    public void Test_Traditional_Chinese_Support_For_Optimized_Rules()
    {
        var calculator = CreateCalculator();
        var result = calculator.Evaluate(new[] { "older-brother", "daughter", "son", "spouse", "younger-brother" }, "zh-Hant", PersonGender.Male);
        
        Assert.AreEqual("姪外孫眷孫男", result.Term.ForLanguage("zh-Hant"));
        Assert.AreEqual("姪外孫的小舅子", result.Options.First().AlternateLabel?.ForLanguage("zh-Hant"));
    }

    [TestMethod]
    public void Test_English_Output()
    {
        var calculator = CreateCalculator();

        Assert.AreEqual("Father", calculator.Evaluate(new[] { "father" }, "en", PersonGender.Male).Term.ForLanguage("en"));
        // K16: standard register is primary in every language — Grandpa moved to alternates.
        Assert.AreEqual("Grandfather", calculator.Evaluate(new[] { "father", "father" }, "en", PersonGender.Male).Term.ForLanguage("en"));
        Assert.AreEqual("Uncle", calculator.Evaluate(new[] { "father", "older-brother" }, "en", PersonGender.Male).Term.ForLanguage("en"));
        Assert.AreEqual("Nephew", calculator.Evaluate(new[] { "older-brother", "son" }, "en", PersonGender.Male).Term.ForLanguage("en"));
        Assert.AreEqual("Cousin", calculator.Evaluate(new[] { "father", "older-brother", "son" }, "en", PersonGender.Male).Term.ForLanguage("en"));
        Assert.AreEqual("Father-in-law", calculator.Evaluate(new[] { "spouse", "father" }, "en", PersonGender.Male).Term.ForLanguage("en"));
    }
}
