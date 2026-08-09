using System;
using System.Collections.Generic;
using System.Text;

using KinshipCalculator.Core.Models;
using KinshipCalculator.Core.Models.Semantics;

namespace KinshipCalculator.Core.Services.Formatting;

/// <summary>
/// Localized name bundle generated from a lossless chain shape.
/// <see cref="Colloquial"/> is <c>null</c> when no reliable colloquial form is known —
/// colloquial coverage widens via the tagged lexicon, not via guessing here.
/// </summary>
public sealed record ChainShapeName (
	LocalizedText Formal ,
	LocalizedText? Colloquial ,
	LocalizedText Official );

/// <summary>
/// Generative term formatter driven by <see cref="KinshipChainShape"/> — the replacement for
/// the lossy vector-driven paths. Covered families produce attested compositional terms;
/// uncovered families return <c>null</c> so the legacy pipeline keeps handling them.
///
/// Ancestor rule (recursive, matches dictionary forms 曾外祖父 / 外曾外祖父 / 高外祖父):
///   T(g) for one hop = 父/母; T(g0..gn) = prefix-外 when g0 is female, applied to Bump(T(g1..gn)),
///   where Bump advances the leading ladder morpheme (祖→曾→高→天→…) or prepends one when the
///   stem has none: 外X→曾外X, 祖X→曾祖X, 父/母→祖父/祖母.
///
/// Descendant rule (established two-slot convention, preserves the attested 外曾孫/曾外孫 distinction):
///   leading 外 iff the FIRST hop is a daughter link; inner 外 (between ladder and 孫) iff the
///   PENULTIMATE hop is a daughter link; both slots may coexist (D.D.D → 外曾外孫女); the
///   terminal hop's own gender never marks; a trailing spouse appends 婿/媳 without
///   re-evaluating either slot.
/// </summary>
public static class ChainShapeTermFormatter
{
	private enum Morpheme
	{
		Wai ,        // 外
		Zu ,         // 祖
		Zeng ,       // 曾
		Gao ,        // 高
		Tian ,       // 天
		Lie ,        // 烈
		Tai ,        // 太
		Yuan ,       // 远/遠
		Bi ,         // 鼻
		Kai ,        // 开/開
		Shi ,        // 始
		Xian ,       // 先
		Fu ,         // 父
		Mu           // 母
	}

	private static readonly Morpheme[] LadderProgression =
	{
		Morpheme.Zeng , Morpheme.Gao , Morpheme.Tian , Morpheme.Lie , Morpheme.Tai ,
		Morpheme.Yuan , Morpheme.Bi , Morpheme.Kai , Morpheme.Shi , Morpheme.Xian
	};

	public static ChainShapeName? TryFormat ( KinshipChainShape shape )
	{
		if ( shape.LeadingSpouse )
		{
			return FormatSpouseRooted ( shape );
		}

		if ( shape.IsPureAncestor )
		{
			return shape.TrailingSpouse ? FormatStepAncestor ( shape ) : FormatAncestor ( shape );
		}

		if ( shape.IsPureDescendant )
		{
			return FormatDescendant ( shape );
		}

		if ( shape.HasBranch )
		{
			return FormatCollateral ( shape );
		}

		return null;
	}

	// ---------------------------------------------------------------- ancestors

	private static ChainShapeName? FormatAncestor ( KinshipChainShape shape )
	{
		IReadOnlyList<PersonGender> genders = shape.AscentGenders;
		if ( genders.Count > 12 )
		{
			return null;
		}

		foreach ( PersonGender gender in genders )
		{
			if ( gender == PersonGender.Unknown )
			{
				return null;
			}
		}

		List<Morpheme>? morphemes = BuildAncestorMorphemes ( genders );
		if ( morphemes is null )
		{
			return null;
		}

		String hans = RenderMorphemes ( morphemes , isTraditional: false );
		String hant = RenderMorphemes ( morphemes , isTraditional: true );
		String english = RenderAncestorEnglish ( genders );

		if ( shape.AdoptiveAscent )
		{
			// 養-prefix composes on the bare 父/母 core (养父, not 养父亲) and suppresses
			// the colloquial slot — nobody says 养爷爷 for an adoptive grandfather.
			Boolean lastIsMale = genders [ ^1 ] == PersonGender.Male;
			String coreHans = genders.Count == 1 ? ( lastIsMale ? "父" : "母" ) : hans;
			String coreHant = genders.Count == 1 ? ( lastIsMale ? "父" : "母" ) : hant;
			LocalizedText adoptiveFormal = new ( $"养{coreHans}" , $"養{coreHant}" , $"Adoptive {english}" );
			return new ChainShapeName ( adoptiveFormal , null , BuildAncestorOfficial ( shape ) );
		}

		LocalizedText formal = new ( hans , hant , english );
		LocalizedText? colloquial = BuildAncestorColloquial ( genders );
		LocalizedText official = BuildAncestorOfficial ( shape );

		return new ChainShapeName ( formal , colloquial , official );
	}

	private static ChainShapeName? FormatStepAncestor ( KinshipChainShape shape )
	{
		IReadOnlyList<PersonGender> genders = shape.AscentGenders;
		if ( genders.Count > 12 )
		{
			return null;
		}

		foreach ( PersonGender gender in genders )
		{
			if ( gender == PersonGender.Unknown )
			{
				return null;
			}
		}

		// The trailing spouse names the anchor's partner: same ascent, terminal gender flipped
		// (F,SP → 继母; F,F,SP → 继祖母; M,F,SP → 继外祖母). 外-slots stay untouched.
		Boolean relativeIsMale = shape.RelativeGender == PersonGender.Male;
		List<PersonGender> flipped = new ( genders )
		{
			[ ^1 ] = relativeIsMale ? PersonGender.Male : PersonGender.Female
		};

		List<Morpheme>? morphemes = BuildAncestorMorphemes ( flipped );
		if ( morphemes is null )
		{
			return null;
		}

		String prefixHans = shape.AdoptiveAscent ? "养" : "继";
		String prefixHant = shape.AdoptiveAscent ? "養" : "繼";

		String coreHans;
		String coreHant;
		if ( genders.Count == 1 )
		{
			coreHans = relativeIsMale ? "父" : "母";
			coreHant = coreHans;
		}
		else
		{
			coreHans = RenderMorphemes ( morphemes , isTraditional: false );
			coreHant = RenderMorphemes ( morphemes , isTraditional: true );
		}

		String stepEn = genders.Count == 1
			? ( relativeIsMale ? "Step-father" : "Step-mother" )
			: $"Step {RenderAncestorEnglish ( flipped )}";
		LocalizedText formal = new ( $"{prefixHans}{coreHans}" , $"{prefixHant}{coreHant}" , stepEn );

		// K16: 后爸/后妈 are northern colloquial lookups, not computed morphology.
		LocalizedText? colloquial = null;
		if ( genders.Count == 1 && !shape.AdoptiveAscent )
		{
			String set = Data.KinshipLexiconLayers.GetVariantSet ( relativeIsMale ? "繼父" : "繼母" );
			if ( !String.IsNullOrEmpty ( set ) )
			{
				colloquial = new LocalizedText ( KinshipScriptConverter.ToHans ( set ) , KinshipScriptConverter.ToHant ( set ) , stepEn );
			}
		}

		String zhOfficial = $"自己→第{genders.Count}代祖輩→配偶";
		LocalizedText official = new ( zhOfficial , zhOfficial , $"Self → ancestor +{genders.Count} → spouse" );

		return new ChainShapeName ( formal , colloquial , official );
	}

	private static List<Morpheme>? BuildAncestorMorphemes ( IReadOnlyList<PersonGender> genders )
	{
		// Recursive from ego outward, implemented iteratively from the terminal ancestor inward.
		List<Morpheme> term = new ()
		{
			genders [ ^1 ] == PersonGender.Male ? Morpheme.Fu : Morpheme.Mu
		};

		for ( Int32 index = genders.Count - 2 ; index >= 0 ; index-- )
		{
			if ( !TryBump ( term ) )
			{
				return null;
			}

			if ( genders [ index ] == PersonGender.Female )
			{
				term.Insert ( 0 , Morpheme.Wai );
			}
		}

		return term;
	}

	private static Boolean TryBump ( List<Morpheme> term )
	{
		switch ( term [ 0 ] )
		{
			case Morpheme.Fu:
			case Morpheme.Mu:
				term.Insert ( 0 , Morpheme.Zu );
				return true;
			case Morpheme.Zu:
			case Morpheme.Wai:
				term.Insert ( 0 , Morpheme.Zeng );
				return true;
			default:
			{
				Int32 position = Array.IndexOf ( LadderProgression , term [ 0 ] );
				if ( position < 0 || position + 1 >= LadderProgression.Length )
				{
					return false;
				}

				term [ 0 ] = LadderProgression [ position + 1 ];
				return true;
			}
		}
	}

	private static String RenderMorphemes ( IReadOnlyList<Morpheme> morphemes , Boolean isTraditional )
	{
		// Depth-1 chains render as 父親/母親 rather than the bare morpheme.
		if ( morphemes.Count == 1 )
		{
			return morphemes [ 0 ] == Morpheme.Fu
				? ( isTraditional ? "父親" : "父亲" )
				: ( isTraditional ? "母親" : "母亲" );
		}

		return RenderStem ( morphemes , isTraditional );
	}

	/// <summary>Raw morpheme concatenation without the depth-1 父親/母親 shorthand (for stems).</summary>
	private static String RenderStem ( IReadOnlyList<Morpheme> morphemes , Boolean isTraditional )
	{
		StringBuilder builder = new ();
		foreach ( Morpheme morpheme in morphemes )
		{
			builder.Append ( morpheme switch
			{
				Morpheme.Wai => "外" ,
				Morpheme.Zu => "祖" ,
				Morpheme.Zeng => "曾" ,
				Morpheme.Gao => "高" ,
				Morpheme.Tian => "天" ,
				Morpheme.Lie => "烈" ,
				Morpheme.Tai => "太" ,
				Morpheme.Yuan => isTraditional ? "遠" : "远" ,
				Morpheme.Bi => "鼻" ,
				Morpheme.Kai => isTraditional ? "開" : "开" ,
				Morpheme.Shi => "始" ,
				Morpheme.Xian => "先" ,
				Morpheme.Fu => isTraditional ? "父" : "父" ,
				Morpheme.Mu => "母" ,
				_ => String.Empty
			} );
		}

		return builder.ToString ();
	}

	private static String RenderAncestorEnglish ( IReadOnlyList<PersonGender> genders )
	{
		Int32 depth = genders.Count;
		Boolean isMale = genders [ ^1 ] == PersonGender.Male;

		if ( depth == 1 )
		{
			return isMale ? "Father" : "Mother";
		}

		String core = isMale ? "Grandfather" : "Grandmother";
		String maternalMark = genders [ 0 ] == PersonGender.Female ? "Maternal " : "";
		if ( depth == 2 )
		{
			return $"{maternalMark}{core}";
		}

		String greats = String.Concat ( System.Linq.Enumerable.Repeat ( "Great-" , depth - 2 ) );
		return $"{maternalMark}{greats}{core}";
	}

	/// <summary>
	/// Colloquial/dialect forms for a straight ancestor line. K16: the surface words used to
	/// be hard-coded here (爸爸|老爸|爹, 外公|姥爷, 太爷爷|太公…); they are looked-up
	/// vocabulary and now come from the lexicon layers, keyed by the standard form.
	/// Only the straight lines carry a confident everyday word: a female middle hop crosses
	/// lines again (M,M,F = 外曾外祖父), and no layer registers those, so they stay formal.
	/// </summary>
	private static LocalizedText? BuildAncestorColloquial ( IReadOnlyList<PersonGender> genders )
	{
		Int32 depth = genders.Count;
		Boolean isMale = genders [ ^1 ] == PersonGender.Male;
		Boolean firstIsFemale = genders [ 0 ] == PersonGender.Female;

		String? standard = depth switch
		{
			1 => isMale ? "父親" : "母親" ,
			2 => firstIsFemale
				? ( isMale ? "外祖父" : "外祖母" )
				: ( isMale ? "祖父" : "祖母" ) ,
			3 when genders [ 1 ] == PersonGender.Male => firstIsFemale
				? ( isMale ? "外曾祖父" : "外曾祖母" )
				: ( isMale ? "曾祖父" : "曾祖母" ) ,
			_ => null
		};

		if ( standard is null )
		{
			return null;
		}

		String set = Data.KinshipLexiconLayers.GetVariantSet ( standard );
		if ( String.IsNullOrEmpty ( set ) )
		{
			return null;
		}

		return new LocalizedText ( set , KinshipScriptConverter.ToHant ( set ) , RenderAncestorEnglish ( genders ) );
	}

	private static LocalizedText BuildAncestorOfficial ( KinshipChainShape shape )
	{
		Int32 depth = shape.AscentDepth;
		Boolean firstIsFemale = shape.AscentGenders [ 0 ] == PersonGender.Female;
		Boolean isMale = shape.SubjectGender == PersonGender.Male;

		String lineHan = firstIsFemale ? "母系" : "父系";
		String genderHan = isMale ? "男" : "女";
		String zh = $"自己→第{depth}代祖輩({lineHan}{genderHan})";
		String lineEn = firstIsFemale ? "maternal line" : "paternal line";
		String genderEn = isMale ? "male" : "female";
		String en = $"Self → ancestor +{depth} ({lineEn}, {genderEn})";

		return new LocalizedText ( zh , zh , en );
	}

	// -------------------------------------------------------------- descendants

	private static ChainShapeName? FormatDescendant ( KinshipChainShape shape )
	{
		IReadOnlyList<PersonGender> genders = shape.DescentGenders;

		foreach ( PersonGender gender in genders )
		{
			if ( gender == PersonGender.Unknown )
			{
				return null;
			}
		}

		Int32 depth = genders.Count;
		Boolean subjectIsMale = genders [ ^1 ] == PersonGender.Male;

		// E4: 自己→養子 is 養子, not 兒子. Upward the three forms already came back as three words
		// (養父/繼父/養母); downward every form collapsed onto the birth relation, so choosing 養子
		// from the key menu looked like the menu had not fired — the path line said 自己→養子 while
		// the answer said 兒子. The builder admits an adoptive descent only at this exact shape.
		if ( shape.AdoptiveDescent )
		{
			return BuildAdoptedChildName ( subjectIsMale );
		}

		// Two-slot convention: leading slot = first hop female; inner slot = penultimate hop female.
		Boolean leadingWai = depth >= 2 && genders [ 0 ] == PersonGender.Female;
		Boolean innerWai = depth >= 3 && genders [ ^2 ] == PersonGender.Female;

		String hans = BuildDescendantTerm ( depth , leadingWai , innerWai , subjectIsMale , shape.TrailingSpouse , isTraditional: false );
		String hant = BuildDescendantTerm ( depth , leadingWai , innerWai , subjectIsMale , shape.TrailingSpouse , isTraditional: true );
		String english = BuildDescendantEnglish ( depth , subjectIsMale , shape.TrailingSpouse , leadingWai || innerWai );

		LocalizedText formal = new ( hans , hant , english );
		LocalizedText? colloquial = BuildDescendantColloquial ( depth , leadingWai , subjectIsMale , shape.TrailingSpouse );
		LocalizedText official = BuildDescendantOfficial ( shape );

		return new ChainShapeName ( formal , colloquial , official );
	}

	private static readonly String[] DescendLadderHans = { "孙" , "曾孙" , "玄孙" , "来孙" , "晜孙" , "仍孙" , "云孙" };
	private static readonly String[] DescendLadderHant = { "孫" , "曾孫" , "玄孫" , "來孫" , "晜孫" , "仍孫" , "雲孫" };

	private static String BuildDescendantTerm ( Int32 depth , Boolean leadingWai , Boolean innerWai , Boolean subjectIsMale , Boolean trailingSpouse , Boolean isTraditional )
	{
		String grandChar = isTraditional ? "孫" : "孙";

		if ( depth == 1 )
		{
			if ( trailingSpouse )
			{
				return subjectIsMale ? ( isTraditional ? "兒媳" : "儿媳" ) : "女婿";
			}

			return subjectIsMale ? ( isTraditional ? "兒子" : "儿子" ) : ( isTraditional ? "女兒" : "女儿" );
		}

		String[] ladder = isTraditional ? DescendLadderHant : DescendLadderHans;
		String stem = depth - 2 < ladder.Length ? ladder [ depth - 2 ] : $"{depth - 1}代{grandChar}";

		if ( innerWai )
		{
			Int32 grandPosition = stem.LastIndexOf ( grandChar , StringComparison.Ordinal );
			if ( grandPosition > 0 )
			{
				stem = $"{stem [ ..grandPosition ]}外{stem [ grandPosition.. ]}";
			}
		}

		String leadingMarker = leadingWai ? "外" : "";

		if ( trailingSpouse )
		{
			// Spouse closure attaches to the compact stem: 孫婿 / 孫媳 (no gender morpheme; slots unchanged).
			String closure = subjectIsMale ? "媳" : "婿";
			return $"{leadingMarker}{stem}{closure}";
		}

		String genderSuffix = subjectIsMale ? "子" : "女";
		return $"{leadingMarker}{stem}{genderSuffix}";
	}

	private static String BuildDescendantEnglish ( Int32 depth , Boolean subjectIsMale , Boolean trailingSpouse , Boolean hasCrossing )
	{
		if ( depth == 1 )
		{
			if ( trailingSpouse )
			{
				return subjectIsMale ? "Daughter-in-law" : "Son-in-law";
			}

			return subjectIsMale ? "Son" : "Daughter";
		}

		String core = trailingSpouse
			? ( subjectIsMale ? "Granddaughter-in-law" : "Grandson-in-law" )
			: ( subjectIsMale ? "Grandson" : "Granddaughter" );
		String maternalMark = hasCrossing ? "Maternal-line " : "";
		if ( depth == 2 )
		{
			return $"{maternalMark}{core}";
		}

		String greats = String.Concat ( System.Linq.Enumerable.Repeat ( "Great-" , depth - 2 ) );
		return $"{maternalMark}{greats}{core}";
	}

	private static LocalizedText? BuildDescendantColloquial ( Int32 depth , Boolean leadingWai , Boolean subjectIsMale , Boolean trailingSpouse )
	{
		if ( trailingSpouse )
		{
			return null;
		}

		if ( depth == 1 )
		{
			return subjectIsMale
				? new LocalizedText ( "儿子" , "兒子" , "Son" )
				: new LocalizedText ( "闺女|女儿" , "女兒" , "Daughter" );
		}

		if ( depth == 2 )
		{
			if ( leadingWai )
			{
				return subjectIsMale
					? new LocalizedText ( "外孙" , "外孫" , "daughter's son" )
					: new LocalizedText ( "外孙女" , "外孫女" , "daughter's daughter" );
			}

			return subjectIsMale
				? new LocalizedText ( "孙子" , "孫子" , "grandson" )
				: new LocalizedText ( "孙女" , "孫女" , "granddaughter" );
		}

		return null;
	}

	// -------------------------------------------------------------- collaterals
	//
	// Strictly-paternal branch system (堂/從/族). Scope: ascent all-male (h ≥ 1); mixed-ascent
	// collaterals stay with the legacy path, which already handles them correctly.
	// Grade = min(my distance, their distance) to the common ancestor = min(h+1, d+1):
	//   2 → 堂 ; 3 → 從 (elder) / 從堂 (same-gen & junior) ; ≥4 → 族  — the 五服-consistent rule.

	private static readonly String[] CollateralAscendStem = { "祖" , "曾祖" , "高祖" , "天祖" , "烈祖" , "太祖" };

	private static String? AscendStemAt ( Int32 level )
		=> level >= 2 && level - 2 < CollateralAscendStem.Length ? CollateralAscendStem [ level - 2 ] : null;

	private static readonly String[] JuniorSubLadderHans = { "" , "孙" , "曾孙" , "玄孙" };
	private static readonly String[] JuniorSubLadderHant = { "" , "孫" , "曾孫" , "玄孫" };

	private static ChainShapeName? FormatCollateral ( KinshipChainShape shape )
	{
		Int32 h = shape.AscentDepth;
		Int32 d = shape.DescentDepth;
		if ( h == 0 )
		{
			return null; // own siblings' lines: legacy sibling rules
		}

		KinshipBranchInfo branch = shape.Branch!;
		if ( branch.Gender == PersonGender.Unknown )
		{
			return null;
		}

		Boolean ascentAllMale = true;
		foreach ( PersonGender gender in shape.AscentGenders )
		{
			if ( gender == PersonGender.Unknown )
			{
				return null;
			}

			if ( gender != PersonGender.Male )
			{
				ascentAllMale = false;
			}
		}

		if ( d == 0 )
		{
			if ( h == 1 )
			{
				return null; // parent's siblings (伯/叔/姑/舅/姨) are correct in legacy
			}

			// Any ascent-gender mix: the flavor and 外-slots derive from the anchor ancestor.
			return FormatElderCollateral ( shape , h , branch );
		}

		if ( !ascentAllMale || branch.Gender != PersonGender.Male )
		{
			// 姑表-line elders reached via descent (all-male ascent + female branch), owned
			// here so the spouse-rooted wrapper can reuse them (legacy is unreachable from
			// that path). MIXED-ascent elders stay with legacy — its 堂舅/堂姨 and 舅表/姨表
			// classifications are settled and correct (a blanket takeover broke 堂舅).
			if ( ascentAllMale && branch.Gender == PersonGender.Female && d > 0 && h - d >= 1 )
			{
				return FormatBiaoLineElder ( shape , h - d , "姑表" );
			}

			// Attested tier-2 stacking cell (mumuy: 舅表姑表哥/舅表姨表哥-family): fork at h=2
			// under a female anchor, descending two hops with a female connector — the outer
			// prefix names the fork line, the inner connector names the ENTRY line (mumuy
			// differentiates: f,m,ob,d,s = 舅表姑表哥 vs m,m,ob,d,s = 舅表姨表哥; the old
			// hard-coded 姨表 collided the two). Generation is h − d = 0: a SAME-generation
			// cousin — the official string used to claim 第2代晚輩, two tiers off.
			if ( h == 2 && d == 2 && shape.AscentGenders [ 1 ] == PersonGender.Female
				&& shape.DescentGenders [ 0 ] == PersonGender.Female && !shape.TrailingSpouse )
			{
				String outer = branch.Gender == PersonGender.Male ? "舅表" : "姨表";
				Boolean entryMale = shape.AscentGenders [ 0 ] == PersonGender.Male;
				String inner = entryMale ? "姑表" : "姨表";
				Boolean terminalMale = shape.DescentGenders [ 1 ] == PersonGender.Male;
				String terminal = terminalMale ? "哥" : "姐";
				String stacked = $"{outer}{inner}{terminal}";
				LocalizedText stackedText = new ( stacked , stacked , "Second-order cousin" );
				String entryLine = entryMale ? "父系入線" : "母系入線";
				String stackedOfficial = $"自己→旁系(分家於第2代,{entryLine})→同輩";
				String stackedOfficialEn = $"Self → collateral (fork at +2, {( entryMale ? "paternal" : "maternal" )} entry) → same generation";
				return new ChainShapeName ( stackedText , null , new LocalizedText ( stackedOfficial , stackedOfficial , stackedOfficialEn ) );
			}

			// Remaining 表-classes (姑表/舅表/姨表): legacy composes the side prefix; the
			// 堂-grades below require an unbroken male line through ascent and fork.
			return null;
		}

		Int32 g = h - d;
		Int32 grade = Math.Min ( h + 1 , d + 1 );

		if ( g >= 1 )
		{
			return FormatDescentReachedElder ( shape , g , grade , branch );
		}

		if ( g == 0 )
		{
			if ( shape.TrailingSpouse )
			{
				return null; // same-generation spouse forms (堂嫂-class): legacy keeps them
			}

			return FormatSameGenerationCollateral ( shape , grade , branch );
		}

		return FormatJuniorCollateral ( shape , h , g , grade );
	}

	private static String GradeMorpheme ( Int32 grade , Boolean elderForm , Boolean isTraditional )
		=> grade switch
		{
			2 => "堂" ,
			3 => elderForm ? ( isTraditional ? "從" : "从" ) : ( isTraditional ? "從堂" : "从堂" ) ,
			_ => "族"
		};

	private static ChainShapeName? FormatElderCollateral ( KinshipChainShape shape , Int32 h , KinshipBranchInfo branch )
	{
		// The sibling is anchored to the ancestor at height h; that ancestor's own morpheme
		// name supplies the stem and every 外-slot, so mixed-gender ascents compose too
		// (M,F,oB → 伯外祖父; F,M,F,yB,SP → 叔曾外祖母).
		List<Morpheme>? anchor = BuildAncestorMorphemes ( shape.AscentGenders );
		if ( anchor is null )
		{
			return null;
		}

		List<Morpheme> stem = new ( anchor );
		stem.RemoveAt ( stem.Count - 1 );

		Boolean anchorIsMale = shape.AscentGenders [ ^1 ] == PersonGender.Male;
		Boolean branchIsMale = branch.Gender == PersonGender.Male;
		String flavor = anchorIsMale
			? ( branchIsMale
				? ( branch.Order == SiblingOrder.Older ? "伯" : "叔" )
				: "姑" )
			: ( branchIsMale ? "舅" : "姨" );
		Boolean subjectIsMale = shape.RelativeGender == PersonGender.Male;
		String terminal = subjectIsMale ? "父" : "母";

		String stemHans = RenderStem ( stem , isTraditional: false );
		String stemHant = RenderStem ( stem , isTraditional: true );
		String english = BuildElderCollateralEnglish ( h , branchIsMale , shape.TrailingSpouse , subjectIsMale );
		LocalizedText formal = new ( $"{flavor}{stemHans}{terminal}" , $"{flavor}{stemHant}{terminal}" , english );

		// K16: the dialect/colloquial surface forms used to be hard-coded here (伯公|大爷爷,
		// and a generative 公/婆 composer for the 外-line). Both are LOOKED-UP vocabulary,
		// not computed morphology, so they now live in the lexicon layers and attach to the
		// standard form the morpheme machine just produced.
		LocalizedText? colloquial = BuildLayerVariants ( $"{flavor}{stemHans}{terminal}" , english );

		String lineHan = shape.AscentGenders [ 0 ] == PersonGender.Female ? "母系" : "父系";
		String genderChar = subjectIsMale ? "男" : "女";
		String spouseTail = shape.TrailingSpouse ? "→配偶" : "";
		String zhOfficial = $"自己→第{h}代祖輩({lineHan}入線)→兄弟姐妹({genderChar}){spouseTail}";
		LocalizedText official = new ( zhOfficial , zhOfficial , $"Self → ancestor +{h} sibling line ({( subjectIsMale ? "male" : "female" )})" );

		return new ChainShapeName ( formal , colloquial , official );
	}

	/// <summary>
	/// Colloquial/dialect variants for a computed standard form, sourced from the lexicon
	/// layers (K15/K16). Null when no layer covers the term — deep computed forms
	/// (堂姑表姨母) legitimately have no looked-up variant.
	/// </summary>
	private static LocalizedText? BuildLayerVariants ( String standardHans , String english )
	{
		String set = Data.KinshipLexiconLayers.GetVariantSet ( standardHans );
		return String.IsNullOrEmpty ( set )
			? null
			: new LocalizedText ( set , KinshipScriptConverter.ToHant ( set ) , english );
	}

	private static String BuildElderCollateralEnglish ( Int32 h , Boolean branchIsMale , Boolean trailingSpouse , Boolean subjectIsMale )
	{
		String core = subjectIsMale ? "Granduncle" : "Grandaunt";
		String inLaw = trailingSpouse ? "-in-law" : "";
		if ( h == 2 )
		{
			return $"{core}{inLaw}";
		}

		String greats = String.Concat ( System.Linq.Enumerable.Repeat ( "Great-" , h - 2 ) );
		return $"{greats}{core}{inLaw}";
	}

	private static ChainShapeName? FormatBiaoLineElder ( KinshipChainShape shape , Int32 g , String line )
	{
		Boolean subjectIsMale = shape.RelativeGender == PersonGender.Male;

		if ( g == 1 && shape.DescentDepth >= 2 )
		{
			// Deep-descent parent-tier elders flavor by the ENTRY parent, not the line: a male
			// blood relative here is my father's 姑表-cousin-brother (mumuy 重表伯父), which the
			// line-flavor below would misread as an aunt's husband (姑父). The stacked composer
			// carries the entry law — defer, same surgery as the K1 junior deference.
			return null;
		}

		String core;
		if ( g == 1 )
		{
			// Flavor at parent level follows the line for FEMALE blood and for spouse
			// closures (姑表姑母 / 姑表姑父 = the aunt's husband); a male BLOOD relative
			// flavors by the entry law instead — he is father's 姑表-cousin-brother, mumuy
			// 姑表伯父|叔父 (the line-flavor 姑父 misread him as an aunt's husband).
			String flavor = line switch
			{
				"舅表" => "舅" ,
				"姨表" => "姨" ,
				_ => "姑"
			};
			Boolean bloodIsMale = shape.TrailingSpouse ? !subjectIsMale : subjectIsMale;
			if ( bloodIsMale && line == "姑表" )
			{
				// Male BLOOD on the 姑表 line is father's cousin-brother: 伯父/叔父 by
				// order, and his wife takes the matching 母-form (mumuy 姑表伯母).
				String orderFlavor = shape.Branch!.Order == SiblingOrder.Older ? "伯" : "叔";
				core = subjectIsMale ? $"{orderFlavor}父" : $"{orderFlavor}母";
			}
			else
			{
				core = subjectIsMale
					? ( line == "舅表" ? "舅" : flavor + "父" )
					: ( line == "舅表" ? "舅母" : flavor );
			}
		}
		else
		{
			String? stem = AscendStemAt ( g );
			if ( stem is null )
			{
				return null;
			}

			core = stem + ( subjectIsMale ? "父" : "母" );
		}

		String term = $"{line}{core}";
		LocalizedText formal = new ( term , term , $"Biao-line elder (+{g})" );

		String zhOfficial = $"自己→第{shape.AscentDepth}代祖輩{line}線→第{g}代長輩{( shape.TrailingSpouse ? "→配偶" : "" )}";
		LocalizedText official = new ( zhOfficial , zhOfficial , $"Biao-line elder (+{g})" );

		return new ChainShapeName ( formal , null , official );
	}

	private static ChainShapeName? FormatDescentReachedElder ( KinshipChainShape shape , Int32 g , Int32 grade , KinshipBranchInfo branch )
	{
		Boolean subjectIsMale = shape.SubjectGender == PersonGender.Male;
		// Note: for elders reached via descent, the branch-order proxy decides 伯/叔 for a male subject.
		String flavor = subjectIsMale
			? ( branch.Order == SiblingOrder.Older ? "伯" : "叔" )
			: "姑";

		String? stem;
		if ( g == 1 )
		{
			stem = "";
		}
		else
		{
			stem = AscendStemAt ( g );
			if ( stem is null )
			{
				return null;
			}
		}

		String BuildFor ( Boolean isTraditional )
		{
			String grade_ = GradeMorpheme ( grade , elderForm: true , isTraditional );
			if ( shape.TrailingSpouse )
			{
				// Spouse of the elder: 伯→伯母/婶 at g==1; otherwise flip the 父/母 terminal.
				if ( g == 1 )
				{
					if ( subjectIsMale )
					{
						return grade_ + ( branch.Order == SiblingOrder.Older ? "伯母" : ( isTraditional ? "嬸" : "婶" ) );
					}

					return grade_ + "姑丈";
				}

				String flipped = subjectIsMale ? "母" : "父";
				return $"{grade_}{flavor}{stem}{flipped}";
			}

			if ( g == 1 )
			{
				return grade_ + flavor;
			}

			String terminal = subjectIsMale ? "父" : "母";
			return $"{grade_}{flavor}{stem}{terminal}";
		}

		String hans = BuildFor ( false );
		String hant = BuildFor ( true );
		String en = $"Collateral elder (+{g}, grade {grade})";
		LocalizedText formal = new ( hans , hant , en );

		String zhOfficial = $"自己→第{shape.AscentDepth}代祖輩(父系)分支→第{g}代長輩{( shape.TrailingSpouse ? "→配偶" : "" )}";
		LocalizedText official = new ( zhOfficial , zhOfficial , en );

		return new ChainShapeName ( formal , null , official );
	}

	private static ChainShapeName? FormatSameGenerationCollateral ( KinshipChainShape shape , Int32 grade , KinshipBranchInfo branch )
	{
		Boolean subjectIsMale = shape.SubjectGender == PersonGender.Male;
		String core = subjectIsMale
			? ( branch.Order == SiblingOrder.Older ? "兄" : "弟" )
			: ( branch.Order == SiblingOrder.Older ? "姐" : "妹" );

		String hans = GradeMorpheme ( grade , elderForm: false , isTraditional: false ) + core;
		String hant = GradeMorpheme ( grade , elderForm: false , isTraditional: true ) + core;
		String en = "Cousin"; // legacy English convention for same-generation collaterals
		LocalizedText formal = new ( hans , hant , en );

		String zhOfficial = $"自己→旁系同輩(分家於第{shape.AscentDepth}代)";
		LocalizedText official = new ( zhOfficial , zhOfficial , en );

		return new ChainShapeName ( formal , null , official );
	}

	private static ChainShapeName? FormatJuniorCollateral ( KinshipChainShape shape , Int32 h , Int32 g , Int32 grade )
	{
		Int32 depthBelow = -g;
		if ( depthBelow >= JuniorSubLadderHans.Length + 1 )
		{
			return null;
		}

		// DEEP forks with mixed-gender descents (F4.OB + D.S.D-style) are ambiguous on this
		// family's 外甥-lexeme surface, and a female hop above gen-zero is a CROSSING the
		// grade ladder here cannot express (mumuy 堂姑表甥外孙婿 where this family would
		// say 從堂-grade) — the stacked composer names both with slot-aware compacts.
		// Shallow forks (h==1, the main-face corpus) keep the K1 face.
		if ( h >= 2 )
		{
			Boolean sawFemale = false;
			Boolean sawMale = false;
			foreach ( PersonGender gender in shape.DescentGenders )
			{
				if ( gender == PersonGender.Female ) { sawFemale = true; }
				else if ( gender == PersonGender.Male ) { sawMale = true; }
			}

			Boolean crossedAboveGenZero = false;
			for ( Int32 i = 0 ; i < h - 1 ; i++ )
			{
				if ( shape.DescentGenders [ i ] == PersonGender.Female )
				{
					crossedAboveGenZero = true;
				}
			}

			if ( ( sawFemale && sawMale ) || crossedAboveGenZero )
			{
				return null;
			}
		}

		// The person at generation 0 on the branch line decides 姪-line vs 甥-line.
		PersonGender genZeroGender = shape.DescentGenders [ h - 1 ];
		if ( genZeroGender == PersonGender.Unknown )
		{
			return null;
		}

		Boolean nephewLine = genZeroGender == PersonGender.Male;
		Boolean subjectIsMale = shape.SubjectGender == PersonGender.Male;

		String BuildFor ( Boolean isTraditional )
		{
			String grade_ = GradeMorpheme ( grade , elderForm: false , isTraditional );
			String lineCore = nephewLine
				? ( isTraditional ? "姪" : "侄" )
				: "外甥";
			String subLadder = ( isTraditional ? JuniorSubLadderHant : JuniorSubLadderHans ) [ depthBelow - 1 ];

			String suffix;
			if ( shape.TrailingSpouse )
			{
				suffix = subjectIsMale ? "媳" : "婿";
			}
			else if ( nephewLine )
			{
				suffix = subjectIsMale ? "子" : "女"; // 姪-line keeps the corpus 子/女 tail (堂姪子)
			}
			else
			{
				suffix = subjectIsMale ? "" : "女"; // 甥-line males are bare (堂外甥), matching the sororal tables
			}

			return $"{grade_}{lineCore}{subLadder}{suffix}";
		}

		String hans = BuildFor ( false );
		String hant = BuildFor ( true );
		String en = nephewLine ? "Paternal-line collateral junior (nephew line)" : "Paternal-line collateral junior (sororal line)";
		LocalizedText formal = new ( hans , hant , en );

		String zhOfficial = $"自己→旁系(分家於第{h}代)→第{depthBelow}代晚輩{( shape.TrailingSpouse ? "→配偶" : "" )}";
		LocalizedText official = new ( zhOfficial , zhOfficial , en );

		return new ChainShapeName ( formal , null , official );
	}

	// ------------------------------------------------------------ spouse-rooted
	//
	// Frozen convention (K4, 2026-07-19): male ego uses 岳-forms for the wife's side;
	// female ego uses 隨夫稱 (formats the chain as the husband would). Unknown ego renders
	// the gender-conditional combined form the workbook established (男：X；女：Y).

	/// <summary>
	/// E4, the adoptive half: 自己→養子 / 自己→養女. Composed here rather than looked up, exactly as
	/// 養父/養母 are, so no lexicon row is added — §四.1 keeps `Resource/Data/Lexicon` at zero change.
	/// </summary>
	private static ChainShapeName BuildAdoptedChildName ( Boolean subjectIsMale )
	{
		LocalizedText formal = subjectIsMale
			? new ( "养子" , "養子" , "Adopted son" )
			: new ( "养女" , "養女" , "Adopted daughter" );

		String zhOfficial = subjectIsMale ? "自己→養子" : "自己→養女";
		LocalizedText official = new (
			zhOfficial , zhOfficial , subjectIsMale ? "Self → adopted son" : "Self → adopted daughter" );

		return new ChainShapeName ( formal , null , official );
	}

	/// <summary>
	/// E4, the step half: 自己→配偶→子 is the spouse's child — 繼子 — not 兒子. Mirrors
	/// <see cref="FormatStepAncestor"/>, which has always named 母→配偶 繼父 on the way up.
	/// </summary>
	private static ChainShapeName BuildStepChildName ( Boolean subjectIsMale )
	{
		LocalizedText formal = subjectIsMale
			? new ( "继子" , "繼子" , "Stepson" )
			: new ( "继女" , "繼女" , "Stepdaughter" );

		String zhOfficial = subjectIsMale ? "自己→配偶→子" : "自己→配偶→女";
		LocalizedText official = new (
			zhOfficial , zhOfficial , subjectIsMale ? "Self → spouse → son" : "Self → spouse → daughter" );

		return new ChainShapeName ( formal , null , official );
	}

	private static ChainShapeName? FormatSpouseRooted ( KinshipChainShape shape )
	{
		if ( shape.AscentDepth == 0 && !shape.HasBranch )
		{
			// E4: the spouse's own child is a 繼子/繼女. Depth 1 with no closing marriage hop only —
			// 配偶→子→子 and 配偶→子→配偶 keep the legacy naming they have always had, so this opens
			// the two words the contract names and nothing beside them.
			if ( shape.DescentDepth == 1 && !shape.TrailingSpouse && !shape.AdoptiveDescent )
			{
				return BuildStepChildName ( shape.DescentGenders [ 0 ] == PersonGender.Male );
			}

			return null; // spouse-only / deeper spouse+descent chains: legacy
		}

		if ( shape.AscentDepth == 1 && !shape.HasBranch && shape.DescentDepth == 0 && !shape.TrailingSpouse )
		{
			return null; // SP.F / SP.M (公公/岳父-class) already correct in legacy
		}

		KinshipChainShape inner = new (
			shape.AscentGenders ,
			shape.Branch ,
			shape.DescentGenders ,
			leadingSpouse: false ,
			shape.TrailingSpouse ,
			shape.EgoGender );

		if ( inner.HasBranch && inner.DescentDepth == 0 && inner.AscentDepth == 1 )
		{
			return null; // spouse's parent's siblings (伯岳-class h=1): legacy keeps its forms
		}

		ChainShapeName? innerName = inner.IsPureAncestor && !inner.TrailingSpouse
			? FormatAncestor ( inner )
			: inner.HasBranch
				? FormatCollateral ( inner )
				: null;
		if ( innerName is null )
		{
			return null;
		}

		String maleHans = PrefixYue ( innerName.Formal.ZhHans );
		String maleHant = PrefixYue ( innerName.Formal.ZhHant );
		String femaleHans = innerName.Formal.ZhHans;
		String femaleHant = innerName.Formal.ZhHant;

		String hans;
		String hant;
		String en;
		switch ( shape.EgoGender )
		{
			case PersonGender.Male:
				hans = maleHans;
				hant = maleHant;
				en = $"Wife's {innerName.Formal.English}";
				break;
			case PersonGender.Female:
				hans = femaleHans;
				hant = femaleHant;
				en = $"Husband's {innerName.Formal.English}";
				break;
			default:
				hans = $"男：{maleHans}；女：{femaleHans}";
				hant = $"男：{maleHant}；女：{femaleHant}";
				en = $"Spouse's {innerName.Formal.English}";
				break;
		}

		LocalizedText formal = new ( hans , hant , en );
		String zhOfficial = $"自己→配偶→{innerName.Official.ZhHans.Replace ( "自己→" , "" )}";
		LocalizedText official = new ( zhOfficial , zhOfficial , $"Self → spouse → {innerName.Official.English.Replace ( "Self → " , "" )}" );

		return new ChainShapeName ( formal , null , official );
	}

	private static String PrefixYue ( String innerTerm )
		=> $"岳{innerTerm}";

	private static LocalizedText BuildDescendantOfficial ( KinshipChainShape shape )
	{
		Int32 depth = shape.DescentDepth;
		Boolean firstIsFemale = shape.DescentGenders [ 0 ] == PersonGender.Female;
		Boolean subjectIsMale = shape.SubjectGender == PersonGender.Male;

		String lineHan = firstIsFemale ? "女系" : "子系";
		String genderHan = subjectIsMale ? "男" : "女";
		String spouseTail = shape.TrailingSpouse ? "→配偶" : "";
		String zh = $"自己→第{depth}代晚輩({lineHan}{genderHan}){spouseTail}";
		String lineEn = firstIsFemale ? "daughter line" : "son line";
		String genderEn = subjectIsMale ? "male" : "female";
		String spouseTailEn = shape.TrailingSpouse ? " → spouse" : "";
		String en = $"Self → descendant -{depth} ({lineEn}, {genderEn}){spouseTailEn}";

		return new LocalizedText ( zh , zh , en );
	}
}
