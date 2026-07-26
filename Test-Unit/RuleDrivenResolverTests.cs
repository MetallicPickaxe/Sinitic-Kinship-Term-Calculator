using KinshipCalculator.Core.Models;
using KinshipCalculator.Core.Services;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test_Unit;

[TestClass]
public sealed class RuleDrivenResolverTests
{
	private static KinshipCalculator.Core.Services.KinshipCalculator CreateCalculator ()
		=> new ();

	[TestMethod]
	public void DirectSiblingUsesRule ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = CreateCalculator ();
		KinshipResult result = calculator.Evaluate ( [ "older-brother" ] , "zh-Hans" , PersonGender.Male );

		Assert.IsTrue ( result.IsExactMatch );
		Assert.AreEqual ( "哥哥" , result.Term.ForLanguage ( "zh-Hans" ) );
	}

	[TestMethod]
	public void SiblingSpouseUsesRule ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = CreateCalculator ();
		KinshipResult result = calculator.Evaluate ( [ "older-brother" , "spouse" ] , "zh-Hans" , PersonGender.Male );

		Assert.IsTrue ( result.IsExactMatch );
		Assert.AreEqual ( "嫂嫂" , result.Term.ForLanguage ( "zh-Hans" ) );
	}

	[TestMethod]
	public void SpouseChainOddEven ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = CreateCalculator ();

		KinshipResult odd = calculator.Evaluate ( [ "spouse" ] , "zh-Hans" , PersonGender.Male );
		Assert.AreEqual ( "配偶" , odd.Term.ForLanguage ( "zh-Hans" ) );

		KinshipResult even = calculator.Evaluate ( [ "spouse" , "spouse" ] , "zh-Hans" , PersonGender.Male );
		Assert.AreEqual ( "自己" , even.Term.ForLanguage ( "zh-Hans" ) );
	}

	[TestMethod]
	public void SpouseChainQuadCollapsesToSelf ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = CreateCalculator ();

		KinshipResult quad = calculator.Evaluate ( [ "spouse" , "spouse" , "spouse" , "spouse" ] , "zh-Hans" , PersonGender.Female );
		Assert.IsTrue ( quad.IsExactMatch );
		Assert.AreEqual ( "自己" , quad.Term.ForLanguage ( "zh-Hans" ) );

		KinshipResult triple = calculator.Evaluate ( [ "spouse" , "spouse" , "spouse" ] , "zh-Hans" , PersonGender.Female );
		Assert.AreEqual ( "配偶" , triple.Term.ForLanguage ( "zh-Hans" ) );
	}

	[TestMethod]
	public void AffinalParentsDependOnSelfGender ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = CreateCalculator ();

		KinshipResult male = calculator.Evaluate ( [ "spouse" , "father" ] , "zh-Hans" , PersonGender.Male );
		Assert.AreEqual ( "岳父" , male.Term.ForLanguage ( "zh-Hans" ) );

		KinshipResult female = calculator.Evaluate ( [ "spouse" , "father" ] , "zh-Hans" , PersonGender.Female );
		Assert.AreEqual ( "公公" , female.Term.ForLanguage ( "zh-Hans" ) );
	}

	[TestMethod]
	public void AffinalSiblingResolves ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = CreateCalculator ();
		KinshipResult result = calculator.Evaluate ( [ "spouse" , "older-brother" ] , "zh-Hans" , PersonGender.Male );

		Assert.AreEqual ( "大舅子" , result.Term.ForLanguage ( "zh-Hans" ) );
	}

	[TestMethod]
	public void StepParentsAndInLaws ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = CreateCalculator ();

		KinshipResult stepMother = calculator.Evaluate ( [ "father" , "spouse" ] , "zh-Hans" , PersonGender.Male );
		Assert.AreEqual ( "继母" , stepMother.Term.ForLanguage ( "zh-Hans" ) );

		KinshipResult stepFather = calculator.Evaluate ( [ "mother" , "spouse" ] , "zh-Hans" , PersonGender.Female );
		Assert.AreEqual ( "继父" , stepFather.Term.ForLanguage ( "zh-Hans" ) );
	}

	[TestMethod]
	public void ChildSpouseNames ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = CreateCalculator ();

		KinshipResult daughterInLaw = calculator.Evaluate ( [ "son" , "spouse" ] , "zh-Hans" , PersonGender.Male );
		Assert.AreEqual ( "儿媳" , daughterInLaw.Term.ForLanguage ( "zh-Hans" ) );

		KinshipResult sonInLaw = calculator.Evaluate ( [ "daughter" , "spouse" ] , "zh-Hans" , PersonGender.Female );
		Assert.AreEqual ( "女婿" , sonInLaw.Term.ForLanguage ( "zh-Hans" ) );
	}

	[TestMethod]
	public void AdoptiveAncestorPrefixes ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = CreateCalculator ();
		KinshipResult result = calculator.Evaluate ( [ "adoptive-father" , "father" ] , "zh-Hans" , PersonGender.Male );

		Assert.AreEqual ( "养祖父" , result.Term.ForLanguage ( "zh-Hans" ) );
	}

	[TestMethod]
	public void AdoptiveDescendantPrefixes ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = CreateCalculator ();
		KinshipResult result = calculator.Evaluate ( [ "adoptive-son" , "son" ] , "zh-Hans" , PersonGender.Male );
		// Daily slot keeps the natural 子 tail (养孙子), consistent with 侄子/孙子 policy.

		Assert.AreEqual ( "养孙子" , result.Term.ForLanguage ( "zh-Hans" ) );
	}

}
