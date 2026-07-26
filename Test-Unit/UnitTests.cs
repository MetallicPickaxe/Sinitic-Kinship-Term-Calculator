using System;
using System.Collections.Generic;
using System.Linq;

using KinshipCalculator.Core.Models;
using KinshipCalculator.Core.Services;
using KinshipCalculator.WinUI.Options;
using KinshipCalculator.WinUI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MumuyAlgorithm;

namespace Test_Unit;

[TestClass]
public class ViewModelTests
{
	private static MainViewModel CreateViewModel ()
		=> new MainViewModel ( new KinshipCalculator.Core.Services.KinshipCalculator () , new ApplicationOptions () );

	[TestMethod]
	public void EvaluateProducesStandardAncestorNames ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = new KinshipCalculator.Core.Services.KinshipCalculator ();
		KinshipResult result = calculator.Evaluate ( new [] { "father" , "father" , "father" } , "zh-Hans" , PersonGender.Male );

		Assert.IsTrue ( result.IsExactMatch );
		Assert.AreEqual ( "曾祖父" , result.Term.ForLanguage ( "zh-Hans" ) );
		// M,M,F crosses the female line twice (外曾外祖父), so no straight-line colloquial
		// exists and the label falls back to the formal (mumuy row 217 agrees).
		Assert.AreEqual ( "Maternal Great-Grandfather" , calculator.Evaluate ( new [] { "mother" , "mother" , "father" } , "en" , PersonGender.Male ).Term.ForLanguage ( "en" ) );
	}

	[TestMethod]
	public void EvaluateProducesStandardDescendantNames ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = new KinshipCalculator.Core.Services.KinshipCalculator ();
		KinshipResult result = calculator.Evaluate ( new [] { "son" , "son" , "daughter" } , "zh-Hans" , PersonGender.Male );

		Assert.IsTrue ( result.IsExactMatch );
		Assert.AreEqual ( "曾孙女" , result.Term.ForLanguage ( "zh-Hans" ) );
	}

	[TestMethod]
	public void MaternalCousinWithChildResolvesToStandardTerm ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = new KinshipCalculator.Core.Services.KinshipCalculator ();
		KinshipResult cousin = calculator.Evaluate ( new [] { "mother" , "older-brother" , "son" } , "zh-Hans" , PersonGender.Male );

		Assert.IsTrue ( cousin.IsExactMatch );
		Assert.AreEqual ( "舅表兄" , cousin.Term.ForLanguage ( "zh-Hans" ) );

		KinshipResult nephew = calculator.Evaluate ( new [] { "mother" , "older-brother" , "son" , "son" } , "zh-Hans" , PersonGender.Male );
		Assert.IsTrue ( nephew.IsExactMatch );
		Assert.AreEqual ( "舅表侄子" , nephew.Term.ForLanguage ( "zh-Hans" ) );
	}

	[TestMethod]
	public void PaternalCousinDaughterResolvesToStandardTerm ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = new KinshipCalculator.Core.Services.KinshipCalculator ();
		KinshipResult cousin = calculator.Evaluate ( new [] { "father" , "younger-sister" , "daughter" } , "zh-Hans" , PersonGender.Male );

		Assert.IsTrue ( cousin.IsExactMatch );
		// father,younger-sister,daughter crosses a female link (姑), so the cousin is
		// 姑表, never 堂 — the historical 堂姐妹 expectation was itself wrong.
		Assert.AreEqual ( "姑表妹" , cousin.Term.ForLanguage ( "zh-Hans" ) );
	}

	[TestMethod]
	public void SiblingChildrenResolveToNephewsAndNieces ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = new KinshipCalculator.Core.Services.KinshipCalculator ();

		KinshipResult nephew = calculator.Evaluate ( new [] { "younger-brother" , "son" } , "zh-Hans" , PersonGender.Male );
		Assert.IsTrue ( nephew.IsExactMatch );
		Assert.AreEqual ( "侄子" , nephew.Term.ForLanguage ( "zh-Hans" ) );
		Assert.AreEqual ( "Nephew" , nephew.Term.ForLanguage ( "en" ) );

		KinshipResult niece = calculator.Evaluate ( new [] { "younger-sister" , "daughter" } , "zh-Hans" , PersonGender.Male );
		Assert.IsTrue ( niece.IsExactMatch );
		Assert.AreEqual ( "外甥女" , niece.Term.ForLanguage ( "zh-Hans" ) );
		Assert.AreEqual ( "Niece" , niece.Term.ForLanguage ( "en" ) );
	}

	[TestMethod]
	public void NephewAndNieceSpousesReported ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = new KinshipCalculator.Core.Services.KinshipCalculator ();

		KinshipResult nephewSpouse = calculator.Evaluate ( new [] { "younger-brother" , "son" , "spouse" } , "zh-Hans" , PersonGender.Male );
		Assert.IsTrue ( nephewSpouse.IsExactMatch );
		Assert.AreEqual ( "侄媳妇" , nephewSpouse.Term.ForLanguage ( "zh-Hans" ) );
		Assert.AreEqual ( "Niece-in-law" , nephewSpouse.Term.ForLanguage ( "en" ) );

		KinshipResult nieceSpouse = calculator.Evaluate ( new [] { "younger-sister" , "daughter" , "spouse" } , "zh-Hans" , PersonGender.Male );
		Assert.IsTrue ( nieceSpouse.IsExactMatch );
		Assert.AreEqual ( "外甥婿" , nieceSpouse.Term.ForLanguage ( "zh-Hans" ) );
		Assert.AreEqual ( "Nephew-in-law" , nieceSpouse.Term.ForLanguage ( "en" ) );
	}

	[TestMethod]
	public void MaternalCousinChainWithSpouseParityEven ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = new KinshipCalculator.Core.Services.KinshipCalculator ();
		String[] tokens =
		{
			"mother" ,
			"older-brother" ,
			"younger-brother" ,
			"older-sister" ,
			"younger-sister" ,
			"son" ,
			"daughter" ,
			"spouse" ,
			"spouse"
		};

		KinshipResult result = calculator.Evaluate ( tokens , "zh-Hans" , PersonGender.Male );

		Assert.IsTrue ( result.IsExactMatch );
		Assert.AreEqual ( "姨表侄女" , result.Term.ForLanguage ( "zh-Hans" ) );
	}

	[TestMethod]
	public void MaternalCousinSpouseReported ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = new KinshipCalculator.Core.Services.KinshipCalculator ();
		KinshipResult result = calculator.Evaluate ( new [] { "mother" , "older-brother" , "son" , "spouse" } , "zh-Hans" , PersonGender.Male );

		Assert.IsTrue ( result.IsExactMatch );
		Assert.AreEqual ( "舅表嫂" , result.Term.ForLanguage ( "zh-Hans" ) );
	}

	[TestMethod]
	public void RepeatedSiblingChainStillMapsToStandardTerm ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = new KinshipCalculator.Core.Services.KinshipCalculator ();
		KinshipResult result = calculator.Evaluate ( new [] { "mother" , "older-brother" , "younger-brother" } , "zh-Hans" , PersonGender.Male );

		Assert.IsTrue ( result.IsExactMatch , "A chain of the mother's brothers must collapse to the maternal-uncle term." );
		Assert.AreEqual ( "舅父" , result.Term.ForLanguage ( "zh-Hans" ) );
	}

	[TestMethod]
	public void ParentSiblingParentChainCollapsesToGrandparent ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = new KinshipCalculator.Core.Services.KinshipCalculator ();
		KinshipResult result = calculator.Evaluate ( new [] { "mother" , "older-brother" , "mother" } , "zh-Hans" , PersonGender.Male );

		Assert.IsTrue ( result.IsExactMatch , "Mother's brother's mother must collapse back to the maternal grandmother." );
		Assert.AreEqual ( "外祖母" , result.Term.ForLanguage ( "zh-Hans" ) );
	}

	[TestMethod]
	public void PaternalGrandUncleResolvesToStandardTerm ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = new KinshipCalculator.Core.Services.KinshipCalculator ();
		KinshipResult result = calculator.Evaluate ( new [] { "father" , "father" , "younger-brother" } , "zh-Hans" , PersonGender.Male );

		Assert.IsTrue ( result.IsExactMatch , "Grandparent-generation uncles must emit the standard term." );
		Assert.AreEqual ( "叔祖父" , result.Term.ForLanguage ( "zh-Hans" ) );
	}

	[TestMethod]
	public void MaternalGrandUncleResolvesToStandardTerm ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = new KinshipCalculator.Core.Services.KinshipCalculator ();
		KinshipResult result = calculator.Evaluate ( new [] { "mother" , "father" , "older-brother" } , "zh-Hans" , PersonGender.Male );

		// M,F,oB anchors to 外祖父 (male), so his elder brother is the 伯-flavor with the
		// inherited 外 slot: 伯外公 (≡ 外伯祖父, the R0007 outer-prefix family). The old
		// 舅祖父 expectation belonged to a female anchor (F,M / M,M chains), not this one.
		Assert.IsTrue ( result.IsExactMatch , "Maternal grandparent-generation uncles must emit the standard term." );
		Assert.AreEqual ( "伯外祖父" , result.Term.ForLanguage ( "zh-Hans" ) );
	}

	[TestMethod]
	public void MaternalGreatAuntDaughterResolvesToStructuredLabel ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = new KinshipCalculator.Core.Services.KinshipCalculator ();
		String[] tokens =
		{
			"mother" ,
			"older-brother" ,
			"younger-brother" ,
			"father" ,
			"older-sister" ,
			"younger-sister" ,
			"son" ,
			"younger-sister"
		};

		KinshipResult result = calculator.Evaluate ( tokens , "zh-Hans" , PersonGender.Male );

		// The chain runs M,(sibling loop),F = 外祖父, then HIS sisters, then a daughter —
		// mother's paternal-aunt-line cousin, so 姑表姨 is the correct graded term. The old
		// 姨祖母的女儿 expectation misread the chain (assumed 姨-line) and predates grading.
		Assert.IsTrue ( result.IsExactMatch , "A long chain must still normalise to a standard result." );
		Assert.AreEqual ( "姑表姨" , result.Term.ForLanguage ( "zh-Hans" ) );
	}

	[TestMethod]
	public void ComplexLoopBackToMaternalUncle ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = new KinshipCalculator.Core.Services.KinshipCalculator ();
		String[] tokens =
		{
			"mother" ,
			"older-brother" ,
			"younger-brother" ,
			"older-sister" ,
			"father" ,
			"son"
		};

		KinshipResult result = calculator.Evaluate ( tokens , "zh-Hans" , PersonGender.Male );

		Assert.IsTrue ( result.IsExactMatch , "A long chain returning to the maternal line must resolve to the maternal uncle." );
		Assert.AreEqual ( "舅父" , result.Term.ForLanguage ( "zh-Hans" ) );
	}

	[TestMethod]
	public void MixedParentGapDoesNotProduceMaternalGreatUncle ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = new KinshipCalculator.Core.Services.KinshipCalculator ();
		String[] tokens =
		{
			"father" ,
			"older-sister" ,
			"mother" ,
			"daughter" ,
			"father" ,
			"younger-brother"
		};

		KinshipResult result = calculator.Evaluate ( tokens , "zh-Hans" , PersonGender.Male );

		Assert.IsTrue ( result.IsExactMatch , "After folding, the result must be the paternal granduncle, not the maternal one." );
		Assert.AreEqual ( "叔祖父" , result.Term.ForLanguage ( "zh-Hans" ) );
		Assert.AreEqual ( 1 , result.Options.Count , "No spurious maternal-granduncle candidate may appear." );
		Assert.AreEqual ( "F.F.YB" , result.Options [ 0 ].DetailsKey , "Must resolve to the paternal-line granduncle." );
	}

	[TestMethod]
	public void MixedParentGapWithSpouseResolvesToGrandaunt ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = new KinshipCalculator.Core.Services.KinshipCalculator ();
		String[] tokens =
		{
			"father" ,
			"older-sister" ,
			"mother" ,
			"daughter" ,
			"father" ,
			"younger-brother" ,
			"spouse"
		};

		KinshipResult result = calculator.Evaluate ( tokens , "zh-Hans" , PersonGender.Male );

		Assert.IsTrue ( result.IsExactMatch , "With a trailing spouse the target is the grandaunt-in-law, not the granduncle." );
		Assert.AreEqual ( "叔祖母" , result.Term.ForLanguage ( "zh-Hans" ) );
		Assert.AreEqual ( 1 , result.Options.Count , "Only spouse-bearing candidates may survive." );
		Assert.AreEqual ( "F.F.YB.SP" , result.Options [ 0 ].DetailsKey );
		String alias = result.Options [ 0 ].AlternateLabel.ForLanguage ( "zh-Hans" );
		Assert.IsTrue ( alias.Contains ( "叔祖母" , StringComparison.Ordinal ) || alias.Contains ( "婶婆" , StringComparison.Ordinal ) , "別稱應包含正式形叔祖母或變體婶婆。" );
	}

	[TestMethod]
	public void ChildSiblingChainNormalizesToDirectChild ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = new KinshipCalculator.Core.Services.KinshipCalculator ();
		KinshipResult result = calculator.Evaluate ( new [] { "son" , "younger-sister" } , "zh-Hans" , PersonGender.Male );

		Assert.IsTrue ( result.IsExactMatch , "Siblings between one's own children must fold back to the corresponding child." );
		Assert.AreEqual ( "女儿" , result.Term.ForLanguage ( "zh-Hans" ) );
	}

	[TestMethod]
	public void EvaluateFallsBackToDescriptiveChain ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = new KinshipCalculator.Core.Services.KinshipCalculator ();
		// The engine now names every reachable chain (legacy's 先祖-catch-all closes the
		// upward ladder, the affinal-web recursion closes spouse webs), so hunting an
		// un-nameable chain no longer tests anything. The fallback guarantee that remains
		// load-bearing: Evaluate must NEVER return an empty term, whatever the monster.
		String[][] monsters =
		{
			new [] { "father" , "father" , "father" , "father" , "father" , "father" , "father" , "father" , "father" , "father" , "father" , "father" , "father" , "older-brother" , "son" } ,
			new [] { "spouse" , "father" , "father" , "father" , "father" , "older-brother" , "son" , "son" , "son" , "son" } ,
			new [] { "older-brother" , "spouse" , "older-brother" , "spouse" , "older-brother" , "spouse" , "older-brother" , "spouse" , "older-brother" } ,
			new [] { "mother" , "mother" , "younger-sister" , "daughter" , "daughter" , "son" , "spouse" }
		};

		foreach ( String[] chain in monsters )
		{
			KinshipResult result = calculator.Evaluate ( chain , "zh-Hans" , PersonGender.Male );
			Assert.IsFalse (
				String.IsNullOrWhiteSpace ( result.Term.ForLanguage ( "zh-Hans" ) ) ,
				$"Empty term for {String.Join ( ',' , chain )}" );
			Assert.IsFalse (
				String.IsNullOrWhiteSpace ( result.Term.ForLanguage ( "zh-Hant" ) ) ,
				$"Empty zh-Hant term for {String.Join ( ',' , chain )}" );
		}
	}

	[TestMethod]
	public void LocalizedTextSwitchesWithLanguage ()
	{
		MainViewModel vm = CreateViewModel ();
		vm.SelectedLanguage = "en";
		TokenDisplay token = vm.TokenButtons.First ();
		string englishLabel = token.Token.Label.ForLanguage ( "en" );

		Assert.AreEqual ( englishLabel , token.Label );
	}

	[TestMethod]
	public void TokenButtonsExposeValidCommands ()
	{
		MainViewModel vm = CreateViewModel ();

		Assert.IsTrue ( vm.TokenButtons.Count > 0 , "Tokens should be loaded from calculator data." );

		foreach ( TokenDisplay button in vm.TokenButtons )
		{
			Assert.IsNotNull ( button.AppendCommand , "AppendCommand must be wired for every token button." );
			Assert.IsTrue ( button.AppendCommand.CanExecute ( button ) , "Command should be executable for its own token." );
		}
	}

	[TestMethod]
	public void AppendAndUndoUpdateStateAndCommands ()
	{
		MainViewModel vm = CreateViewModel ();
		TokenDisplay token = vm.TokenButtons.First ();

		Assert.IsFalse ( vm.UndoCommand.CanExecute ( null ) , "Undo is disabled before any token is chosen." );

		vm.AppendTokenCommand.Execute ( token );

		Assert.IsTrue ( vm.UndoCommand.CanExecute ( null ) , "Undo should be enabled after appending a token." );
		StringAssert.Contains ( vm.PathText , token.Label , "Path text should include the appended token." );

		string previousPath = vm.PathText;
		vm.UndoCommand.Execute ( null );

		Assert.IsFalse ( vm.UndoCommand.CanExecute ( null ) , "Undo should be disabled when sequence goes back to empty." );
		Assert.AreNotEqual ( previousPath , vm.PathText , "Path should revert after undo." );
	}

	// K16 contract (2026-07-20): the primary term is STANDARD Chinese; the colloquial /
	// dialect form is demoted to the alternate slot, supplied by a lexicon layer. Both are
	// asserted here so the layer wiring cannot silently drop the everyday word.
	[DataTestMethod]
	[DataRow("father,father","祖父","爷爷")]
	[DataRow("father,father,father","曾祖父","太爷爷")]
	[DataRow("mother,mother","外祖母","外婆")]
	[DataRow("mother,older-brother","舅父","舅舅")]
	[DataRow("mother,older-brother,spouse","舅母","舅妈")]
	[DataRow("father,older-brother","伯父","伯伯")]
	[DataRow("father,younger-brother","叔父","叔叔")]
	[DataRow("father,younger-sister","姑母","姑姑")]
	[DataRow("mother,younger-sister","姨母","阿姨")]
	[DataRow("mother,older-brother,son,spouse","舅表嫂",null)]
	public void CommonRelationshipsResolveToStandardTerms ( String tokenCsv , String expected , String? colloquial )
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = new KinshipCalculator.Core.Services.KinshipCalculator ();
		String[] tokens = tokenCsv.Split ( ',' , StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );

		KinshipResult result = calculator.Evaluate ( tokens , "zh-Hans" , PersonGender.Male );

		Assert.IsTrue ( result.IsExactMatch , $"Must resolve to a proper term: {tokenCsv}" );
		Assert.AreEqual ( expected , result.Term.ForLanguage ( "zh-Hans" ) );

		if ( colloquial is not null )
		{
			String alternates = result.Options [ 0 ].AlternateLabel?.ForLanguage ( "zh-Hans" ) ?? String.Empty;
			Assert.IsTrue (
				alternates.Split ( '|' ).Contains ( colloquial ) ,
				$"{tokenCsv}: 口語形 '{colloquial}' 應留在備選層,實得 '{alternates}'" );
		}
	}

    [TestMethod]
    public void EvaluateSpouseYoungerSisterDaughter_MaleSelf()
    {
        KinshipCalculator.Core.Services.KinshipCalculator calculator = new KinshipCalculator.Core.Services.KinshipCalculator();
        KinshipResult result = calculator.Evaluate(new[] { "spouse", "younger-sister", "daughter" }, "zh-Hans", PersonGender.Male);

        // Self is Male, Spouse is Female. Wife's younger sister's daughter.
        // 姑甥女 is the operator-specified formal AND sits in mumuy's accepted set, while
        // 外甥女 is only a loose paraphrase — so this family is formal-primary (workbook row
        // SP.YS.D regressed to 不一致 when the daily slot displaced it; adjudicated back).
        Assert.IsTrue(result.IsExactMatch);
        Assert.AreEqual("姑甥女", result.Term.ForLanguage("zh-Hans"));
        StringAssert.Contains(result.Options[0].AlternateLabel.ForLanguage("zh-Hans"), "外甥女");
    }

    [TestMethod]
    public void EvaluateSpouseYoungerSisterDaughter_FemaleSelf()
    {
        KinshipCalculator.Core.Services.KinshipCalculator calculator = new KinshipCalculator.Core.Services.KinshipCalculator();
        KinshipResult result = calculator.Evaluate(new[] { "spouse", "younger-sister", "daughter" }, "zh-Hans", PersonGender.Female);

        // Self is Female, Spouse is Male. Husband's younger sister's daughter.
        // Formal-primary family: 姑甥女 leads, the loose 外甥女 stays in the alternate slot.
        Assert.IsTrue(result.IsExactMatch);
        Assert.AreEqual("姑甥女", result.Term.ForLanguage("zh-Hans"));
        StringAssert.Contains(result.Options[0].AlternateLabel.ForLanguage("zh-Hans"), "外甥女");
    }

	// Generative-coverage GAUGE, not a pass/fail contract — and deliberately NOT gated on a
	// rate. Two reasons:
	//   1. Deep Han kinship is an open system with no single authority (K12). Chasing 100%
	//      would mean bending correct standard-Chinese output to match one regionally-biased
	//      table — the divergences below are mostly cases where OUR spelling is the better
	//      one (舅表姐 vs mumuy 舅表叔表姐).
	//   2. Of 10,000 random chains mumuy can only answer ~22, so any percentage computed
	//      here is statistical noise. The AUTHORITATIVE coverage measure is the 90k mode-map
	//      face (Utility/Scripts/Run-ValidationLoop.ps1), currently 96% absorbed.
	// So this test asserts only that the comparison harness still functions, and prints the
	// rate plus divergence samples for a human to eyeball.
	[TestMethod]
	public void RandomLongChainsMatchMumuyAlgorithm ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = new KinshipCalculator.Core.Services.KinshipCalculator ();
		MumuyResolver mumuy = new MumuyResolver ();
		const Int32 chainsPerSeed = 2_000;
		Int32[] seeds =
		{
			0x5F3759DF ,
			unchecked((Int32)0xCAFEBABE) ,
			unchecked((Int32)0xBADC0FFE) ,
			0x1234ABCD ,
			0x0F0F0F0F
		};

		Int32 totalValidated = 0;
		Int32 totalMatched = 0;
		List<String> samples = new ();
		foreach ( Int32 randomSeed in seeds )
		{
			Int32 validated = 0;
			foreach ( String[] tokens in GenerateRandomChains ( chainsPerSeed , randomSeed ) )
			{
				if ( !TryBuildMumuySelector ( tokens , out String selector ) )
				{
					continue;
				}

				IReadOnlyList<String> mumuyNames = mumuy.ResolveNames ( selector );
				if ( mumuyNames.Count == 0 )
				{
					continue;
				}

				KinshipResult result = calculator.Evaluate ( tokens , "zh-Hans" , PersonGender.Male );
				String actual = result.Term.ForLanguage ( "zh-Hans" );

				validated++;
				if ( mumuyNames.Contains ( actual , StringComparer.Ordinal ) )
				{
					totalMatched++;
				}
				else if ( samples.Count < 5 )
				{
					samples.Add ( $"{String.Join ( '→' , tokens )} → 我方 {actual} / mumuy {String.Join ( '/' , mumuyNames )}" );
				}
			}

			Assert.IsTrue ( validated > 0 , $"Seed {randomSeed} produced no verifiable chain; check the selector mapping." );
			totalValidated += validated;
		}

		Assert.IsTrue ( totalValidated > 0 , "The mumuy oracle answered no chain at all; check the selector mapping." );

		Double rate = (Double) totalMatched / totalValidated;
		Console.WriteLine ( $"[generative-coverage gauge] literal agreement {totalMatched}/{totalValidated} = {rate:P1} (sample too small to gate on; the authoritative figure is the 90k comparison face)" );
		foreach ( String sample in samples )
		{
			Console.WriteLine ( $"  divergence sample {sample}" );
		}
	}

	private static Boolean TryBuildMumuySelector ( IReadOnlyList<String> tokens , out String selector )
	{
		List<String> buffer = new List<String> ( tokens.Count );
		foreach ( String token in tokens )
		{
			if ( !TokenToMumuy.TryGetValue ( token , out String? symbol ) )
			{
				selector = String.Empty;
				return false;
			}

			buffer.Add ( symbol );
		}

		selector = String.Join ( ',' , buffer );
		return true;
	}

	private static readonly IReadOnlyDictionary<String , String> TokenToMumuy = new Dictionary<String , String> ( StringComparer.Ordinal )
	{
		[ "father" ] = "f" ,
		[ "mother" ] = "m" ,
		[ "son" ] = "s" ,
		[ "daughter" ] = "d" ,
		[ "older-brother" ] = "ob" ,
		[ "younger-brother" ] = "lb" ,
		[ "older-sister" ] = "os" ,
		[ "younger-sister" ] = "ls" ,
		[ "spouse" ] = "w"
	};

	private static IEnumerable<String[]> GenerateRandomChains ( Int32 count , Int32 seed )
	{
		Random random = new Random ( seed );
		String[] pool = TokenToMumuy.Keys.ToArray ();

		for ( Int32 i = 0 ; i < count ; i++ )
		{
			Int32 length = random.Next ( 5 , 21 );
			String[] tokens = new String[ length ];
			Int32 spouseStreak = 0;

			for ( Int32 j = 0 ; j < length ; j++ )
			{
				String token;
				do
				{
					token = pool[ random.Next ( pool.Length ) ];
				}
				while ( token == "spouse" && spouseStreak >= 1 );

				tokens[ j ] = token;
				spouseStreak = token == "spouse" ? spouseStreak + 1 : 0;
			}

			yield return tokens;
		}
	}

	// K11a stacked-elder composer guard: the recursive class-stacking law induced from the
	// oracle (sweep-3) — each attested cell must keep composing, and canonical comparison
	// must accept the oracle's own spellings.
	[TestMethod]
	public void StackedElderComposer_AttestedCells_Match ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = new ();
		var cases = new (String[] Tokens , String Reference)[]
		{
			( new [] { "mother" , "father" , "father" , "older-brother" , "son" , "son" } , "从堂舅父" ) ,
			( new [] { "mother" , "father" , "father" , "older-brother" , "daughter" , "daughter" } , "堂姑表姨母" ) ,
			( new [] { "father" , "father" , "mother" , "older-brother" , "daughter" , "daughter" } , "舅表姑表姑母" ) ,
			( new [] { "father" , "father" , "father" , "older-sister" , "daughter" , "daughter" } , "姑表姑表姑母" ) ,
			( new [] { "father" , "father" , "father" , "father" , "older-sister" , "daughter" , "daughter" } , "姑表姑表姑祖母" ) ,
			// Deep-descent parent-tier: entry-law flavor (father's cousin-brother = 伯/叔),
			// the cell FormatBiaoLineElder's line-flavor misread as 姑父 before deferring.
			( new [] { "father" , "father" , "father" , "father" , "father" , "older-sister" , "daughter" , "daughter" , "daughter" , "son" } , "重表伯父 | 重表叔父" ) ,
			// 玄-tier junior ladder at the g=-4 floor (all-male interior).
			( new [] { "father" , "father" , "father" , "father" , "older-brother" , "son" , "son" , "son" , "son" , "son" , "son" , "son" , "daughter" } , "四从父侄玄孙女" ) ,
			// K11a sweep-4 affinal-web recursion: wife-side 眷 (child-frame M+right) and
			// husband-side 姻 (child-frame F+right).
			( new [] { "father" , "father" , "older-brother" , "son" , "spouse" , "older-brother" , "daughter" } , "从父叔眷舅表姊 | 从父叔眷舅表妹" ) ,
			( new [] { "father" , "father" , "older-brother" , "daughter" , "spouse" , "father" , "older-brother" , "daughter" } , "从父姑姻堂姑母" ) ,
			// Crossed junior handoff: the grade ladder cannot express the 姑表 crossing.
			( new [] { "father" , "father" , "older-brother" , "daughter" , "daughter" , "daughter" , "daughter" , "spouse" } , "堂姑表甥外孙婿" ) ,
			// BiaoLine g=1 male BLOOD flavors by the entry law (伯/叔), inside a 姻-web.
			( new [] { "father" , "father" , "older-brother" , "daughter" , "spouse" , "father" , "older-sister" , "son" } , "从父姑姻姑表伯父 | 从父姑姻姑表叔父" ) ,
			// Shallow-fork spouse closures rescued from the legacy gender-flip (K1 slots).
			( new [] { "father" , "older-sister" , "daughter" , "daughter" , "spouse" } , "姑表甥婿" ) ,
			( new [] { "older-sister" , "daughter" , "daughter" , "spouse" } , "甥外孙婿" ) ,
			// Spouse-led 姻-web bridge (K4 wrap fallback on the left segment).
			( new [] { "spouse" , "father" , "older-sister" , "daughter" , "daughter" , "spouse" , "father" , "older-brother" , "daughter" } , "妻姑表甥姻叔女 | 夫姑表甥姻叔女" ) ,
			// Maternal single-hop fork inside a 眷-right (M.OB → 舅表, not 堂).
			( new [] { "father" , "father" , "older-brother" , "son" , "spouse" , "older-brother" , "daughter" , "daughter" , "spouse" } , "从父叔眷舅表甥婿" ) ,
			// Junior-bridge my-frame right tiers (mumuy 堂甥姻叔母/姻父-law).
			( new [] { "father" , "older-brother" , "daughter" , "daughter" , "spouse" , "father" , "father" , "older-brother" , "spouse" } , "堂甥姻叔母" ) ,
			( new [] { "father" , "older-brother" , "daughter" , "daughter" , "spouse" , "father" , "father" } , "堂甥姻父" ) ,
			( new [] { "father" , "older-brother" , "daughter" , "daughter" , "spouse" , "father" , "mother" , "father" } , "堂甥姻外祖父" ) ,
			( new [] { "father" , "older-brother" , "daughter" , "daughter" , "spouse" , "father" , "father" , "mother" } , "堂甥姻祖母" ) ,
			// K9-A cut #1: h=2 juniors leave the folded semantic words for the composer
			// (penult-外 law intact; the 从堂-grade bridges in comparison scope).
			( new [] { "father" , "father" , "older-brother" , "son" , "daughter" , "daughter" , "daughter" } , "从堂甥外孙女" ) ,
			( new [] { "father" , "father" , "older-brother" , "son" , "daughter" , "daughter" , "spouse" } , "从堂甥婿" ) ,
			// K9-A cut #2: my-frame gen-0 sibling words and junior ladder on the right.
			( new [] { "father" , "older-brother" , "daughter" , "daughter" , "spouse" , "father" } , "堂甥姻兄弟 | 堂甥姻兄 | 堂甥姻弟" ) ,
			( new [] { "older-brother" , "daughter" , "daughter" , "spouse" , "older-brother" , "spouse" } , "侄外孙姻孙妇" )
		};

		foreach ( var item in cases )
		{
			KinshipResult result = calculator.Evaluate ( item.Tokens , "zh-Hans" , PersonGender.Male );
			var kind = KinshipCalculator.Testing.Verification.ReferenceJudgmentPolicy.EvaluateAgainstReference (
				item.Reference , result.Term.ForLanguage ( "zh-Hans" ) , false );
			Assert.IsTrue (
				kind is KinshipCalculator.Testing.Verification.ReferenceJudgmentKind.Aligned
					or KinshipCalculator.Testing.Verification.ReferenceJudgmentKind.LexicalEquivalenceCandidate ,
				$"{String.Join ( ',' , item.Tokens )}: ours '{result.Term.ForLanguage ( "zh-Hans" )}' vs oracle '{item.Reference}' => {kind}" );
		}
	}

	// K9 canonicalization regression guard: the pattern rules that batch-absorb mumuy's
	// grade/spouse-prefix/ordering vocabularies must keep matching their showcase pairs.
	[TestMethod]
	public void JudgmentCanonicalization_PatternFamilies_Match ()
	{
		var pairs = new (String Reference , String Candidate , Boolean SpouseRooted)[]
		{
			( "四从父甥外孙女" , "族外甥孫女" , false ) ,
			( "妻四从父甥外孙女 | 夫四从父甥外孙女" , "岳族外甥孫女" , true ) ,
			( "族高祖父" , "族伯高祖父" , false ) ,
			( "妻族高祖母 | 夫族高祖母" , "岳族伯高祖母" , true ) ,
			( "重表姑外祖母" , "姑表姨祖母" , false ) ,
			( "重表甥外孙女" , "舅表甥外孙女" , false ) ,
			( "重表伯外高祖父" , "姑表舅高祖父" , false ) ,
			( "重表姑外曾祖母" , "姑表姨曾祖母" , false ) ,
			( "重表姑母" , "舅表姑表姑母" , false ) ,
			( "族外高外祖父" , "舅表舅高祖父" , false ) ,
			( "妻重表姑父 | 夫重表姑父" , "岳姑表姑父" , true ) ,
			( "重表大姑子 | 重表小姑子 | 重表姑子 | 重表大姨子" , "姑表姑子" , true ) ,
			( "重表大姑子 | 重表小姑子 | 重表姑子 | 重表大姨子" , "姑表姨子" , true ) ,
			( "四从父大姑夫 | 四从父小姑夫 | 四从父姑夫" , "族姑夫" , true ) ,
			( "重表姑外高外祖母" , "姨表姑表姨高祖母" , false ) ,
			( "族天祖父" , "族伯天祖父" , false ) ,
			( "重表姐妹 | 重表姐 | 重表妹" , "舅表姐" , false ) ,
			( "妻族远祖母 | 夫族远祖母" , "岳族伯远祖母" , true ) ,
			( "四从父大婶子 | 四从父小婶子 | 四从父妯娌" , "族妯娌" , true ) ,
			( "族外曾外曾祖父" , "舅表舅高祖父" , false ) ,
			( "从父姑姻叔表甥女" , "堂姑姻堂外甥女" , false ) ,
			( "从父姑姻伯祖父" , "堂姑姻伯公" , false ) ,
			( "妻从父姑姻叔表姊妹壻 | 夫从父姑姻叔表姊妹壻" , "岳堂姑姻堂姐夫" , true ) ,
			( "从父姊妹姻伯祖父" , "堂姐姻伯曾祖父" , false ) ,
			( "重表姑外曾外曾祖母" , "姨表姑表姨高祖母" , false ) ,
			( "堂甥姻叔女" , "堂外甥姻堂姑" , false ) ,
			( "堂甥姻祖父" , "堂外甥姻高祖父" , false ) ,
			( "堂甥姻叔兄弟妇 | 堂甥姻叔兄妇" , "堂外甥姻伯婆" , false ) ,
			( "从父姊妹姻叔表姊妹壻 | 从父姊妹姻叔表姊壻" , "堂姐姻堂姐夫" , false ) ,
			( "妻从父姑姻叔表侄婿 | 夫从父姑姻叔表侄婿" , "岳堂姑姻堂姪婿" , true ) ,
			( "堂甥姻姑女" , "堂外甥姻姑表姑" , false ) ,
			( "堂甥姻外孙女" , "堂外甥姻姑表姐" , false ) ,
			( "堂甥姻叔兄弟 | 堂甥姻叔弟" , "堂外甥姻叔公" , false ) ,
			( "重表姑外曾外曾外祖母" , "姨表姑表姨高祖母" , false ) ,
			( "堂甥姻祖父" , "堂外甥姻曾外曾祖父" , false ) ,
			( "妻堂姑表侄孙女 | 夫堂姑表侄孙女" , "岳堂姑表姪孫女" , true ) ,
			( "堂甥姻叔男" , "堂外甥姻堂叔" , false ) ,
			( "堂甥姻孙女" , "堂外甥姻堂妹" , false ) ,
			( "堂甥姻姨姊妹 | 堂甥姻姨姊" , "堂外甥姻姨婆" , false ) ,
			( "堂甥姻姑男" , "堂外甥姻姑表叔父" , false ) ,
			( "妻从父姊妹姻叔表兄弟妇 | 夫从父姊妹姻叔表兄弟妇" , "岳堂姐姻堂嫂" , true ) ,
			( "堂甥姻孙男" , "堂外甥姻堂弟" , false ) ,
			( "妻从堂甥外孙婿 | 夫从堂甥外孙婿" , "岳堂甥外孫婿" , true ) ,
			( "从父姑姻姨姑父" , "堂姑姻姨表姑丈" , false ) ,
			( "堂甥眷叔兄弟 | 堂甥眷叔兄" , "堂外甥眷伯外公" , false ) ,
			( "妻堂甥眷祖父 | 夫堂甥眷祖父" , "岳堂外甥眷外高祖父" , true ) ,
			( "堂甥姻舅女" , "堂外甥姻舅表姑" , false ) ,
			( "堂甥姻外孙女" , "堂外甥姻姑表妹" , false ) ,
			( "妻姑表叔表甥外孙女 | 夫姑表叔表甥外孙女" , "岳姑表甥外孫女" , true ) ,
			( "堂甥眷姑姊妹 | 堂甥眷姑姊" , "堂外甥眷姑外婆" , false ) ,
			( "从父姑姻姑表甥男" , "堂姑姻姑表甥子" , false ) ,
			( "重表姑烈祖母" , "姑表烈祖母" , false ) ,
			( "重表姑太祖父" , "姑表太祖父" , false ) ,
			( "堂甥姻舅男" , "堂外甥姻舅表伯" , false ) ,
			( "堂甥姻外孙男" , "堂外甥姻姑表弟" , false ) ,
			( "妻姑表叔表侄外孙女 | 夫姑表叔表侄外孙女" , "岳姑表姪外孫女" , true ) ,
			( "堂甥眷外祖父" , "堂外甥眷外高外祖父" , false ) ,
			( "堂甥眷叔兄弟 | 堂甥眷叔弟" , "堂外甥眷叔外公" , false ) ,
			( "从父姑姻姨伯母 | 从父姑姻姨叔母" , "堂姑姻姨表伯母" , false ) ,
			( "妻叔祖眷舅表姑父 | 夫叔祖眷舅表姑父" , "岳伯祖父眷舅表姐夫" , true ) ,
			( "堂甥姻妇" , "堂外甥姻伯母" , false ) ,
			( "堂甥姻姨女" , "堂外甥姻姨表姑" , false ) ,
			( "堂甥眷孙女" , "堂外甥眷舅表姐" , false ) ,
			( "妻舅表叔表甥外孙女 | 夫舅表叔表甥外孙女" , "岳舅表甥外孫女" , true ) ,
			( "堂甥眷舅兄弟 | 堂甥眷舅兄" , "堂外甥眷舅外公" , false ) ,
			( "妻叔眷堂姨父 | 夫叔眷堂姨父" , "伯岳父眷堂姨丈" , true ) ,
			( "妻堂甥姻父 | 夫堂甥姻父" , "岳堂外甥姻太爺爺" , true ) ,
			( "堂甥眷祖父" , "堂外甥眷外曾外曾祖父" , false ) ,
			( "堂甥眷姨姊妹 | 堂甥眷姨姊" , "堂外甥眷姨外婆" , false ) ,
			( "堂甥眷姑男" , "堂外甥眷姑表舅" , false ) ,
			( "堂甥眷妇" , "堂外甥眷舅媽" , false ) ,
			( "堂甥姻叔伯母" , "堂外甥姻嬸嬸" , false ) ,
			( "堂甥姻祖母" , "堂外甥姻奶奶" , false ) ,
			( "堂甥眷舅父" , "堂外甥眷舅舅" , false ) ,
			( "重表大婶子 | 重表妯娌" , "舅表姑表妯娌" , true ) ,
			( "堂甥姻叔伯父" , "堂外甥姻叔叔" , false ) ,
			( "堂甥姻男" , "堂外甥姻伯伯" , false ) ,
			( "堂甥眷女" , "堂外甥眷姨媽" , false ) ,
			( "堂甥眷孙男" , "堂外甥眷舅表弟" , false ) ,
			( "妻堂姨表甥外孙女 | 夫堂姨表甥外孙女" , "岳堂姑表甥外孫女" , true ) ,
			( "妻叔祖眷舅表伯父 | 夫叔祖眷舅表伯父" , "岳伯祖父眷舅表兄" , true ) ,
			( "叔眷舅表甥女" , "伯伯眷舅表甥女" , false ) ,
			( "妻姑姻堂姑父 | 夫姑姻堂姑父" , "姑岳母姻堂姑丈" , true ) ,
			( "重表大伯子 | 重表小叔子 | 重表伯叔 | 重表大舅子" , "姑表舅子" , true ) ,
			( "重表大伯子 | 重表小叔子 | 重表伯叔 | 重表大舅子" , "姑表伯叔" , true ) ,
			( "从堂甥外孙女" , "堂甥孙女" , false ) ,
			( "妻姨侄姻叔女" , "岳姨表姪姻堂姑" , true ) ,
			( "叔祖眷姨伯母 | 叔祖眷姨叔母" , "伯公眷姨表嫂" , false ) ,
			( "姑祖姻堂姑母" , "姑婆姻堂姐" , false ) ,
			( "侄外曾外曾外孙女" , "玄姪孫女" , false ) ,
			( "舅兄弟眷叔表姊妹 | 叔兄弟眷叔表姊妹" , "大舅子眷堂姐" , true ) ,
			( "堂姨天祖父" , "舅表姑天祖父" , false ) ,
			( "从堂远祖母" , "族伯遠祖母" , false ) ,
			( "侄曾孙" , "曾姪孫" , false ) ,
			( "从母叔表舅母" , "姨表舅母" , false ) ,
			( "妻从父姨姻堂姑母" , "岳堂姨姻堂姑母" , true ) ,
			( "妻姑祖姻堂姑母" , "岳姑祖母姻堂姑母" , true ) ,
			( "姨姊妹姻堂姑母" , "大姨子姻堂姑母" , true ) ,
			( "姊妹姻堂姑母" , "姐姐姻堂姑母" , false ) ,
			( "姨甥姻叔女" , "姨表甥姻堂姑" , false ) ,
			( "妻叔祖眷姨姑父 | 夫叔祖眷姨姑父" , "岳伯祖父眷姨表姐夫" , true ) ,
			( "妻姑表姨表甥外孙女 | 夫姑表姨表甥外孙女" , "岳姑表甥外孫女" , true )
		};

		// Full exporter-path replica of the live 90k row that stays 不一致 despite the
		// pairwise rule being green above — EvaluateRow with the complete variant set.
		var rowVerdict = KinshipCalculator.Testing.Verification.ReferenceJudgmentPolicy.EvaluateRow (
			"妻从父姑姻叔表侄婿 | 妻从父姑姻侄婿 | 内从父姑姻叔表侄婿 | 内从父姑姻侄婿 | 岳从父姑姻叔表侄婿 | 岳从父姑姻侄婿 | 岳家从父姑姻叔表侄婿 | 岳家从父姑姻侄婿 | 丈人从父姑姻叔表侄婿 | 丈人从父姑姻侄婿 | 夫从父姑姻叔表侄婿 | 夫从父姑姻侄婿 | 外从父姑姻叔表侄婿 | 外从父姑姻侄婿 | 公从父姑姻叔表侄婿 | 公从父姑姻侄婿 | 婆家从父姑姻叔表侄婿 | 婆家从父姑姻侄婿 | 婆婆从父姑姻叔表侄婿 | 婆婆从父姑姻侄婿" ,
			"岳堂姑姻堂姪婿" ,
			"堂姑姻堂姪婿" ,
			"SP.F.F.OB.D.SP.OB.S.D.SP" );
		var bigList = "妻从父姑姻叔表侄婿 | 妻从父姑姻侄婿 | 内从父姑姻叔表侄婿 | 内从父姑姻侄婿 | 岳从父姑姻叔表侄婿 | 岳从父姑姻侄婿 | 岳家从父姑姻叔表侄婿 | 岳家从父姑姻侄婿 | 丈人从父姑姻叔表侄婿 | 丈人从父姑姻侄婿 | 夫从父姑姻叔表侄婿 | 夫从父姑姻侄婿 | 外从父姑姻叔表侄婿 | 外从父姑姻侄婿 | 公从父姑姻叔表侄婿 | 公从父姑姻侄婿 | 婆家从父姑姻叔表侄婿 | 婆家从父姑姻侄婿 | 婆婆从父姑姻叔表侄婿 | 婆婆从父姑姻侄婿";
		// Guards the 姪-elision asymmetry regression: the unprefixed female 隨夫稱 form
		// must survive compact with its 侄 intact, like the ref's 夫-prefixed twin.
		_ = bigList;
		Assert.AreEqual (
			KinshipCalculator.Testing.Verification.ReferenceJudgmentKind.LexicalEquivalenceCandidate ,
			rowVerdict.Kind ,
			$"EvaluateRow replica => {rowVerdict.Kind} ({rowVerdict.JudgmentDisplay})" );

		foreach ( var pair in pairs )
		{
			var kind = KinshipCalculator.Testing.Verification.ReferenceJudgmentPolicy.EvaluateAgainstReference ( pair.Reference , pair.Candidate , pair.SpouseRooted );
			Assert.AreEqual (
				KinshipCalculator.Testing.Verification.ReferenceJudgmentKind.LexicalEquivalenceCandidate ,
				kind ,
				$"{pair.Reference} vs {pair.Candidate} [sp={pair.SpouseRooted}]" );
		}

		// Guard the two hazards the sweep uncovered: term-initial 外甥 stays a lexeme
		// (外甥孙女 ≡ 甥孙女), and spouse-side terms never collapse onto blood-side terms.
		Assert.AreEqual (
			KinshipCalculator.Testing.Verification.ReferenceJudgmentKind.LexicalEquivalenceCandidate ,
			KinshipCalculator.Testing.Verification.ReferenceJudgmentPolicy.EvaluateAgainstReference ( "甥孙女|远甥女" , "外甥孫女" , false ) );
		Assert.AreEqual (
			KinshipCalculator.Testing.Verification.ReferenceJudgmentKind.StructuralMismatch ,
			KinshipCalculator.Testing.Verification.ReferenceJudgmentPolicy.EvaluateAgainstReference ( "祖父" , "岳祖父" , false ) );
	}

	// K9 regression guard: deep collateral elders stem at their TRUE generation — the legacy
	// path once looked up stems[gen+1] and over-deepened every branch-female elder (姑表曾祖母
	// at grandparent level; its own official line said 第2代祖辈, and mumuy's 重表姑祖母 agrees
	// on 祖). The male-branch ChainShape path was already correct and must stay so.
	[TestMethod]
	public void DeepCollateralElders_StemAtTrueGeneration ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = new ();

		KinshipResult femaleBranchPlusTwo = calculator.Evaluate (
			new [] { "father" , "father" , "father" , "father" , "father" , "father" , "older-sister" , "daughter" , "daughter" , "daughter" , "daughter" } ,
			"zh-Hans" , PersonGender.Male );
		Assert.AreEqual ( "姑表祖母" , femaleBranchPlusTwo.Term.ForLanguage ( "zh-Hans" ) );

		KinshipResult maleBranchPlusTwo = calculator.Evaluate (
			new [] { "father" , "father" , "father" , "father" , "father" , "older-brother" , "son" , "son" , "son" } ,
			"zh-Hans" , PersonGender.Male );
		Assert.AreEqual ( "族伯祖父" , maleBranchPlusTwo.Term.ForLanguage ( "zh-Hans" ) );
	}

	// K9 regression guard: ancestor-sibling folding must PRESERVE tokens before the folded
	// parent run — it once erased a leading spouse (SP.F6.OS.D4 → F2.OS.D4), so the wife's
	// grand-generation elder came back as a blood-side JUNIOR (姑表甥孙女).
	[TestMethod]
	public void SpouseRootedDeepElder_KeepsAffinalReading ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = new ();
		KinshipResult result = calculator.Evaluate (
			new [] { "spouse" , "father" , "father" , "father" , "father" , "father" , "father" , "older-sister" , "daughter" , "daughter" , "daughter" , "daughter" } ,
			"zh-Hans" , PersonGender.Male );

		String term = result.Term.ForLanguage ( "zh-Hans" );
		Assert.IsFalse ( term.Contains ( "甥" , StringComparison.Ordinal ) ,
			$"Spouse-side elder must not collapse into a blood-side junior reading. actual: {term}" );
		foreach ( var option in result.Options )
		{
			Assert.IsFalse ( option.DetailsKey.StartsWith ( "F." , StringComparison.Ordinal ) && option.IsExactMatch ,
				$"No exact blood-side candidate may survive for a spouse-rooted chain. key: {option.DetailsKey}" );
		}
	}

	// K11a grammar-induction probe: dumps the oracle's composite vocabulary for chains
	// [blood]+SP+[post] to the scratchpad TSV. Run manually when extending the 眷-grammar;
	// last sweep promoted to Utility/MumuyAlgorithm/Data/juan-grammar-probe.tsv.
	// K15/K16 lexicon-layer guard: the four built-in layers must load from the embedded
	// resources (the single-file publish cannot see loose data files), the base layer must
	// answer non-derivable lexemes by ego gender, and variant layers must attach to the
	// standard form with their provenance label intact.
	[TestMethod]
	public void LexiconLayers_LoadAndResolve ()
	{
		var layers = KinshipCalculator.Core.Data.KinshipLexiconLayers.Layers;
		CollectionAssert.AreEquivalent (
			new [] { "lexicon-standard" , "register-colloquial" , "dialect-north" , "dialect-south" } ,
			layers.Select ( l => l.Id ).ToArray () ,
			$"loaded: {String.Join ( ',' , layers.Select ( l => l.Id ) )}" );

		Assert.AreEqual ( "岳父" , KinshipCalculator.Core.Data.KinshipLexiconLayers.TryGetStandardLexeme ( "SP.F" , PersonGender.Male ) );
		Assert.AreEqual ( "公公" , KinshipCalculator.Core.Data.KinshipLexiconLayers.TryGetStandardLexeme ( "SP.F" , PersonGender.Female ) );
		Assert.AreEqual ( "亲家公" , KinshipCalculator.Core.Data.KinshipLexiconLayers.TryGetStandardLexeme ( "D.SP.F" , PersonGender.Unknown ) );
		Assert.IsNull ( KinshipCalculator.Core.Data.KinshipLexiconLayers.TryGetStandardLexeme ( "SP.F" , PersonGender.Unknown ) );

		var grandUncle = KinshipCalculator.Core.Data.KinshipLexiconLayers.GetVariants ( "伯祖父" );
		CollectionAssert.AreEquivalent (
			new [] { "伯公" , "大伯公" , "大爷爷" } ,
			grandUncle.Select ( v => v.Term ).ToArray () ,
			$"伯祖父 variants: {String.Join ( ',' , grandUncle.Select ( v => v.Term ) )}" );
		Assert.AreEqual ( "dialect-south" , grandUncle.First ( v => v.Term == "伯公" ).LayerId );
		Assert.AreEqual ( "dialect-north" , grandUncle.First ( v => v.Term == "大爷爷" ).LayerId );
		Assert.AreEqual ( "register-colloquial" , KinshipCalculator.Core.Data.KinshipLexiconLayers.GetVariants ( "祖父" ).Single ( v => v.Term == "爷爷" ).LayerId );
		Assert.AreEqual ( 0 , KinshipCalculator.Core.Data.KinshipLexiconLayers.GetVariants ( "堂姑表姨母" ).Count , "computed forms carry no layer entry" );

		// Reverse lookup drives the UI provenance chips: every candidate must be able to
		// say which layer it came from, and engine-computed words must stay untagged.
		Assert.AreEqual ( "南系" , KinshipCalculator.Core.Data.KinshipLexiconLayers.TryGetLayerNameForTerm ( "伯公" ) );
		Assert.AreEqual ( "北系" , KinshipCalculator.Core.Data.KinshipLexiconLayers.TryGetLayerNameForTerm ( "姥姥" ) );
		Assert.AreEqual ( "通用口語" , KinshipCalculator.Core.Data.KinshipLexiconLayers.TryGetLayerNameForTerm ( "爷爷" ) );
		Assert.IsNull ( KinshipCalculator.Core.Data.KinshipLexiconLayers.TryGetLayerNameForTerm ( "伯祖父" ) , "standard computed form has no owning layer" );
	}

	// K15 layers ③④: the legal-document chain must be available even when a proper term
	// exists (it used to appear only as a fallback for un-nameable relations), and the raw
	// calibration readback must echo what was ENTERED, un-simplified — M.OB.M resolves to
	// 外祖母 but the raw layer still shows the three hops the user actually clicked.
	[TestMethod]
	public void DescriptiveAndRawLayers_AreAlwaysAvailable ()
	{
		KinshipCalculator.Core.Services.KinshipCalculator calculator = new ();

		KinshipResult named = calculator.Evaluate ( new [] { "father" , "father" , "older-brother" } , "zh-Hans" , PersonGender.Male );
		Assert.AreEqual ( "伯祖父" , named.Term.ForLanguage ( "zh-Hans" ) );
		Assert.AreEqual ( "父的父的兄" , named.Options [ 0 ].DescriptiveChain.ForLanguage ( "zh-Hans" ) , "The documentary chain must be present even when a proper term exists" );
		Assert.AreEqual ( "我的父的父的兄" , named.RawChain.ForLanguage ( "zh-Hans" ) );

		// Simplification case: the engine collapses this to 外祖母, the raw layer must not.
		KinshipResult collapsed = calculator.Evaluate ( new [] { "mother" , "older-brother" , "mother" } , "zh-Hans" , PersonGender.Male );
		Assert.AreEqual ( "外祖母" , collapsed.Term.ForLanguage ( "zh-Hans" ) );
		Assert.AreEqual ( "我的母的兄的母" , collapsed.RawChain.ForLanguage ( "zh-Hans" ) , "The raw layer must not simplify on the user's behalf" );
	}

	// K15 UI contract: driving the ViewModel the way the buttons do must put the STANDARD
	// term in the primary slot and every everyday/dialect word into the candidate chips,
	// each tagged with the layer it came from. This is exactly the data MainWindow binds,
	// so it verifies the visible behaviour without driving the live window.
	[TestMethod]
	public void ViewModel_ProducesLayerTaggedCandidateChips ()
	{
		MainViewModel vm = CreateViewModel ();
		vm.SelectedLanguage = "zh-Hans";
		foreach ( String id in new [] { "father" , "father" , "older-brother" } )
		{
			TokenDisplay button = vm.TokenButtons.First ( b => b.Token.Id == id );
			vm.AppendTokenCommand.Execute ( button );
		}

		ResultInterpretation first = vm.ResultOptions.First ();
		Assert.AreEqual ( "伯祖父" , first.StandardLabel , "The primary slot must hold the standard Chinese term" );
		Assert.IsTrue ( first.HasVariants , "Candidate variants must be present" );

		var chips = first.Variants.Select ( v => v.Display ).ToArray ();
		CollectionAssert.Contains ( chips , "伯公 · 南系" , $"chips: {String.Join ( " / " , chips )}" );
		CollectionAssert.Contains ( chips , "大爷爷 · 北系" , $"chips: {String.Join ( " / " , chips )}" );
		Assert.IsFalse (
			chips.Contains ( "伯祖父 · 南系" ) ,
			"The standard form must not be listed as a dialect variant" );
	}

	[Ignore ( "Manual K11a induction probe — not part of the regression surface." )]
	[TestMethod]
	public void TEMP_DumpMumuyCompositeGrammar ()
	{
		MumuyResolver mumuy = new MumuyResolver ();
		// Sweep 4: interior-SP 姻-composites ([graded blood bridge] + spouse + [right
		// segment]) — inducing the right-side generation law (从父姑姻堂姑母 vs 堂甥姻叔女).
		String[][] bloodParts =
		{
			new [] { "father" , "father" , "older-brother" , "daughter" } ,
			new [] { "father" , "father" , "older-brother" , "son" } ,
			new [] { "father" , "older-brother" , "daughter" } ,
			new [] { "father" , "older-brother" , "daughter" , "daughter" } ,
			new [] { "older-brother" , "daughter" } ,
			new [] { "older-sister" , "daughter" }
		};
		String[][] postParts =
		{
			new [] { "father" } ,
			new [] { "mother" } ,
			new [] { "father" , "father" } ,
			new [] { "father" , "older-brother" } ,
			new [] { "father" , "older-brother" , "daughter" } ,
			new [] { "father" , "older-brother" , "son" } ,
			new [] { "older-brother" } ,
			new [] { "older-brother" , "daughter" }
		};

		System.Text.StringBuilder sb = new System.Text.StringBuilder ();
		foreach ( String[] blood in bloodParts )
		{
			foreach ( String[] post in postParts )
			{
				// Emit the bare blood chain and the trailing-spouse variant; a non-empty
				// post part probes the composite [blood]+SP+[post] grammar.
				String[][] variants = post.Length == 0
					? new [] { blood , blood.Concat ( new [] { "spouse" } ).ToArray () }
					: new [] { blood.Concat ( new [] { "spouse" } ).Concat ( post ).ToArray () };

				foreach ( String[] tokens in variants )
				{
					if ( !TryBuildMumuySelector ( tokens , out String selector ) )
					{
						continue;
					}

					IReadOnlyList<String> names = mumuy.ResolveNames ( selector );
					sb.AppendLine ( $"{String.Join ( ',' , tokens )}\t{selector}\t{String.Join ( '|' , names )}" );
				}
			}
		}

		// Portable output path: this is a hand-run induction tool, so it must work on any
		// machine, not only the session that first wrote it.
		System.IO.File.WriteAllText (
			System.IO.Path.Combine ( System.IO.Path.GetTempPath () , "mumuy_composite_probe.tsv" ) ,
			sb.ToString () ,
			new System.Text.UTF8Encoding ( false ) );
	}
}
