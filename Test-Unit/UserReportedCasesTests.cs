using KinshipCalculator.Core.Models;
using KinshipCalculator.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Test_Unit
{
    [TestClass]
    public class UserReportedCasesTests
    {
        private static KinshipCalculator.Core.Services.KinshipCalculator CreateCalculator() 
            => new KinshipCalculator.Core.Services.KinshipCalculator();

        [TestMethod]
        public void OlderBrotherDaughterSonSpouseYoungerBrother()
        {
            // older-brother -> daughter -> son -> spouse -> younger-brother
            // Expected colloquial reading: 侄外孙的小舅子 (the nephew's daughter's son's wife's
    // younger brother).
            var tokens = new[] { "older-brother", "daughter", "son", "spouse", "younger-brother" };
            var result = CreateCalculator().Evaluate(tokens, "zh-Hans", PersonGender.Male);

            Assert.AreEqual("侄外孙眷孙男", result.Term.ForLanguage("zh-Hans"));
            var colloquial = result.Options.First().AlternateLabel.ForLanguage("zh-Hans");
            Assert.AreEqual("侄外孙的小舅子", colloquial);
        }

        [TestMethod]
        public void FatherSpouseMotherFather()
        {
            // father -> spouse -> mother -> father
            // Formal: 外曾外祖父, Colloquial: 外曾祖父|曾祖父
            var tokens = new[] { "father", "spouse", "mother", "father" };
            var result = CreateCalculator().Evaluate(tokens, "zh-Hans", PersonGender.Male);

            Assert.AreEqual("外曾外祖父", result.Term.ForLanguage("zh-Hans"));
            var colloquial = result.Options.First().AlternateLabel.ForLanguage("zh-Hans");
            Assert.IsTrue(colloquial.Contains("外曾祖父") || colloquial.Contains("曾祖父"));
        }

        [TestMethod]
        public void YoungerBrotherDaughterSpouseFather()
        {
            // younger-brother -> daughter -> spouse -> father
            // Niece's Husband's Father.
            var tokens = new[] { "younger-brother", "daughter", "spouse", "father" };
            var result = CreateCalculator().Evaluate(tokens, "zh-Hans", PersonGender.Male);

            Assert.AreEqual("侄姻兄弟", result.Term.ForLanguage("zh-Hans"));
        }

        [TestMethod]
        public void YoungerSisterSpouseYoungerSister()
        {
            // younger-sister -> spouse -> younger-sister
            // Sister's Husband's Sister.
            var tokens = new[] { "younger-sister", "spouse", "younger-sister" };
            var result = CreateCalculator().Evaluate(tokens, "zh-Hans", PersonGender.Male);

            Assert.AreEqual("姊妹姻姊妹", result.Term.ForLanguage("zh-Hans"));
            var colloquial = result.Options.First().AlternateLabel.ForLanguage("zh-Hans");
            Assert.AreEqual("姻姊妹", colloquial);
        }

        [TestMethod]
        public void MotherOlderSisterSpouseFather()
        {
            // mother -> older-sister -> spouse -> father
            // Aunt (DaYi)'s Husband (YiFu)'s Father.
            var tokens = new[] { "mother", "older-sister", "spouse", "father" };
            var result = CreateCalculator().Evaluate(tokens, "zh-Hans", PersonGender.Male);

            Assert.AreEqual("姨姻祖父", result.Term.ForLanguage("zh-Hans"));
        }

        [TestMethod]
        public void SpouseOlderSisterSpouse()
        {
            // spouse -> older-sister -> spouse
            // Male Self: Wife's older sister's husband. (Lianjin)
            var tokens = new[] { "spouse", "older-sister", "spouse" };
            
            // Test Male Self
            var resultMale = CreateCalculator().Evaluate(tokens, "zh-Hans", PersonGender.Male);
            var maleTerm = resultMale.Term.ForLanguage("zh-Hans");
            var maleAlternate = resultMale.Options[0].AlternateLabel.ForLanguage("zh-Hans");
            Assert.IsTrue(maleTerm.Contains("连襟") || maleAlternate.Contains("连襟"), $"term: {maleTerm} | alt: {maleAlternate}");
            Assert.IsTrue(maleTerm.Contains("姐夫") || maleAlternate.Contains("姐夫"), $"term: {maleTerm} | alt: {maleAlternate}");

            // Test Female Self (Husband's sister's husband)
            // Husband's older sister (DaGu). Her husband (GuFu).
            // Usually just called Brother-in-law or GuFu.
            // Calculator returned "姐夫" (Older Sister's Husband).
            var resultFemale = CreateCalculator().Evaluate(tokens, "zh-Hans", PersonGender.Female);
            Assert.AreEqual("姐夫", resultFemale.Term.ForLanguage("zh-Hans"));
        }

        [TestMethod]
        public void FatherSpouseSpouse()
        {
            // father -> spouse -> spouse
            // Father.
            var tokens = new[] { "father", "spouse", "spouse" };
            var result = CreateCalculator().Evaluate(tokens, "zh-Hans", PersonGender.Male);

            Assert.AreEqual("父亲", result.Term.ForLanguage("zh-Hans"));
        }

        [TestMethod]
        public void SonSpouseOlderBrother()
        {
            // son -> spouse -> older-brother
            // Daughter-in-law's older brother.
            var tokens = new[] { "son", "spouse", "older-brother" };
            var result = CreateCalculator().Evaluate(tokens, "zh-Hans", PersonGender.Male);

            Assert.AreEqual("姻侄", result.Term.ForLanguage("zh-Hans"));
        }

        [TestMethod]
        public void YoungerBrotherSonSpouseYoungerSisterDaughter()
        {
            // younger-brother -> son -> spouse -> younger-sister -> daughter
            // Nephew's wife's sister's daughter.
            var tokens = new[] { "younger-brother", "son", "spouse", "younger-sister", "daughter" };
            var result = CreateCalculator().Evaluate(tokens, "zh-Hans", PersonGender.Male);

            Assert.AreEqual("侄眷外孙女", result.Term.ForLanguage("zh-Hans"));
        }

        [TestMethod]
        public void YoungerSisterSpouseFather()
        {
            // younger-sister -> spouse -> father
            // Sister's Husband's Father.
            var tokens = new[] { "younger-sister", "spouse", "father" };
            var result = CreateCalculator().Evaluate(tokens, "zh-Hans", PersonGender.Male);

            Assert.AreEqual("姊妹姻父", result.Term.ForLanguage("zh-Hans"));
            var colloquial = result.Options.First().AlternateLabel.ForLanguage("zh-Hans");
            Assert.AreEqual("姊妹的公公|伯伯|叔叔", colloquial);
        }
        [TestMethod]
        public void OlderSisterSonSpouseYoungerSisterDaughter()
        {
            // older-sister -> son -> spouse -> younger-sister -> daughter
            // Sister's son's wife's sister's daughter.
            var tokens = new[] { "older-sister", "son", "spouse", "younger-sister", "daughter" };
            var result = CreateCalculator().Evaluate(tokens, "zh-Hans", PersonGender.Male);

            Assert.AreEqual("甥眷外孙女", result.Term.ForLanguage("zh-Hans"));
        }
        [TestMethod]
        public void YoungerBrotherSpouseYoungerBrotherDaughter()
        {
            // younger-brother -> spouse -> younger-brother -> daughter
            // Brother's wife's brother's daughter.
            var tokens = new[] { "younger-brother", "spouse", "younger-brother", "daughter" };
            var result = CreateCalculator().Evaluate(tokens, "zh-Hans", PersonGender.Male);

            Assert.AreEqual("兄弟眷侄女", result.Term.ForLanguage("zh-Hans"));
            var colloquial = result.Options.First().AlternateLabel.ForLanguage("zh-Hans");
            Assert.AreEqual("姻侄女|弟媳的侄女", colloquial);
        }
    }
}
