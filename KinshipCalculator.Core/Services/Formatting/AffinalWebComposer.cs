using System;
using System.Collections.Generic;
using System.Linq;

using KinshipCalculator.Core.Models;
using KinshipCalculator.Core.Models.Semantics;

namespace KinshipCalculator.Core.Services.Formatting;

/// <summary>
/// Generative 姻/眷 affinal-web recursion (K11a sweep-4 induction): a chain with an
/// interior spouse splits at that spouse into <c>[left bridge] + SP + [right segment]</c>,
/// and the oracle names it <c>compact(left-term) + connector + child-frame(right-term)</c> —
/// the right side is what the bridge couple's CHILD calls the person, i.e. the right chain
/// re-evaluated behind a mother-hop (male bridge, connector 眷: 从父叔眷舅表姊 for
/// F.F.OB.S+SP+OB.D = M.OB.D = 舅表姊) or a father-hop (female bridge, connector 姻:
/// 从父姑姻堂姑母 for F.F.OB.D+SP+F.OB.D = F.F.OB.D = 堂姑母). Sibling-led composites
/// stay with the dedicated juan regimes (they run at an earlier priority); descriptive
/// sub-terms abstain so the whole-chain fallback keeps its wording.
/// </summary>
public static class AffinalWebComposer
{
	/// <summary>
	/// Recursive sub-chain evaluator injected by <see cref="KinshipCalculator"/>: returns
	/// the zh-Hans/zh-Hant primary term for a token-id chain, or null when the engine only
	/// has a descriptive fallback for it. Recursion terminates because every split strictly
	/// shortens the chain.
	/// </summary>
	public static Func<IReadOnlyList<String> , PersonGender , (String Hans , String Hant)?>? Evaluator { get; set; }

	private static readonly (String Tail , String Replacement)[] BridgeTailTrims =
	{
		// Tier words drop their trailing 父/母 classifier before the connector (mumuy
		// 叔祖眷… from a 叔祖父 bridge, 姨表祖姻… from a 姨表祖母 bridge). Longest tails first
		// so 曾/高祖 are not shortened by the bare 祖 rule (EndsWith breaks on first match).
		( "高祖父" , "高祖" ) ,
		( "高祖母" , "高祖" ) ,
		( "曾祖父" , "曾祖" ) ,
		( "曾祖母" , "曾祖" ) ,
		( "祖父" , "祖" ) ,
		( "祖母" , "祖" ) ,
		( "伯父" , "伯" ) ,
		( "叔父" , "叔" ) ,
		( "姑母" , "姑" ) ,
		( "舅父" , "舅" ) ,
		( "姨母" , "姨" ) ,
		( "甥女" , "甥" ) ,
		( "侄女" , "侄" ) ,
		( "姪女" , "姪" ) ,
		( "甥子" , "甥" ) ,
		( "侄子" , "侄" ) ,
		( "姪子" , "姪" ) ,
		( "孙女" , "孙" ) ,
		( "孫女" , "孫" )
	};

	public static (LocalizedText Label , LocalizedText Official)? TryFormat ( IReadOnlyList<KinshipToken> tokens , PersonGender selfGender )
	{
		if ( Evaluator is null )
		{
			return null;
		}

		Int32 spouseIndex = -1;
		for ( Int32 i = 1 ; i < tokens.Count - 1 ; i++ )
		{
			if ( tokens [ i ].Id.Equals ( "spouse" , StringComparison.Ordinal ) )
			{
				spouseIndex = i;
				break;
			}
		}

		if ( spouseIndex < 0 )
		{
			return null;
		}

		List<KinshipToken> left = tokens.Take ( spouseIndex ).ToList ();
		List<KinshipToken> right = tokens.Skip ( spouseIndex + 1 ).ToList ();

		// The bridge is the last blood person on the left; its gender picks the connector
		// and the child-frame hop. A canonical bridge goes through the shape builder; a
		// bridge with an internal 表/堂 fork (姨表祖母 = F.M.M.OB.OS.D) is NOT canonical, so
		// its generation and gender come straight from the blood tokens instead — that is
		// the whole non-canonical-bridge long-tail the composer used to abstain on, dropping
		// the entire chain to a descriptive 的-fallback.
		Int32 bridgeGeneration;
		Boolean bridgeIsMale;
		KinshipChainShape? leftShape = KinshipChainShapeBuilder.Build ( left , selfGender );
		if ( leftShape is not null && leftShape.IsPureAncestor && !leftShape.LeadingSpouse )
		{
			// A bare lineal ancestor is NOT an affinal bridge: the spouse of a parent is a
			// STEP-parent, and mumuy itself collapses that hop (f,w = 妈妈, f,f,w,lb = 小舅爷
			// = the grandmother's brother). The graph simplifier already reaches the person
			// through the parent identity (F.SP.S -> F.S = 兄弟, F.F.SP.YB -> F.M.YB), so
			// composing 父親眷兄弟-style surfaces here would name a worse reading.
			return null;
		}

		if ( leftShape is not null && !leftShape.TrailingSpouse && leftShape.RelativeGender != PersonGender.Unknown )
		{
			bridgeIsMale = leftShape.RelativeGender == PersonGender.Male;
			bridgeGeneration = leftShape.AscentDepth - leftShape.DescentDepth;
		}
		else if ( TryAnalyzeBloodBridge ( left , out bridgeGeneration , out bridgeIsMale ) )
		{
			// Non-canonical pure-blood bridge (internal 表/堂 fork) — newly reachable.
		}
		else
		{
			return null;
		}

		String connector = bridgeIsMale ? "眷" : "姻";
		String childHop = bridgeIsMale ? "mother" : "father";

		(String Hans , String Hant)? leftTerm = Evaluator ( left.Select ( t => t.Id ).ToList () , selfGender );
		if ( leftTerm is null && left.Count > 1 && left [ 0 ].Id.Equals ( "spouse" , StringComparison.Ordinal ) )
		{
			// Spouse-led bridges whose full form has no dedicated family yet: compose the
			// blood part and apply the K4 wrap ourselves (male ego 岳-prefix, female ego
			// 隨夫稱) — mumuy's 妻姑表甥姻叔女-family stays descriptive without this.
			(String Hans , String Hant)? bloodTerm = Evaluator ( left.Skip ( 1 ).Select ( t => t.Id ).ToList () , selfGender );
			if ( bloodTerm is not null )
			{
				leftTerm = selfGender switch
				{
					PersonGender.Male => ( $"岳{bloodTerm.Value.Hans}" , $"岳{bloodTerm.Value.Hant}" ) ,
					PersonGender.Female => bloodTerm ,
					_ => null
				};
			}
		}

		if ( leftTerm is null )
		{
			return null;
		}

		// Elder bridges name the right side from the bridge couple's CHILD (从父叔眷舅表姊
		// = the child's M.OB.D); a SAME-GENERATION bridge names it from the bridge herself
		// (从父姊妹姻叔表姊妹 = HER F.OB.D cousins) — no extra hop. JUNIOR bridges tier the
		// right side in MY frame (bridge generation + the spouse-frame offset: 堂甥姻叔母
		// for F.F.OB+SP, 堂甥姻父 for F.F) — the child-frame recursion overshoots there.
		(String Hans , String Hant)? rightTerm = null;
		if ( bridgeGeneration < 0 )
		{
			rightTerm = TryMyFrameJuniorRight ( right , bridgeGeneration );
		}
		else if ( bridgeGeneration >= 1 )
		{
			// Elder bridge, bare spouse-sibling right: an in-law at the BRIDGE's own
			// generation, not one tier lower (mumuy 叔祖眷舅祖父 for F.F.OB.W.LB — 舅祖父,
			// not the child-frame's 舅父). The child-frame recursion below still handles a
			// right side that descends further into the spouse's line.
			rightTerm = TryTieredElderInLawRight ( right , bridgeGeneration , bridgeIsMale );
		}

		if ( rightTerm is null )
		{
			List<String> rightIds = new ();
			if ( bridgeGeneration != 0 )
			{
				rightIds.Add ( childHop );
			}

			rightIds.AddRange ( right.Select ( t => t.Id ) );
			rightTerm = Evaluator ( rightIds , selfGender );
		}

		if ( rightTerm is null )
		{
			return null;
		}

		(String leftHans , String leftHant) = TrimBridgeTail ( leftTerm.Value.Hans , leftTerm.Value.Hant );
		String hans = $"{leftHans}{connector}{rightTerm.Value.Hans}";
		String hant = $"{leftHant}{connector}{rightTerm.Value.Hant}";
		// A deep affinal composite has no English word — the kinship term itself stays Han in
		// every language (option B: only the chrome is English, terms stay in-script).
		LocalizedText label = new ( hans , hant , hant );

		// Structural path: English chrome ("Self → … → spouse → husband-side …") with the
		// Han terms left intact, so the en-bound Structural-path column reads in English.
		String sideZh = bridgeIsMale ? "妻側" : "夫側";
		String sideEn = bridgeIsMale ? "wife-side" : "husband-side";
		String officialHans = $"自己→{leftHans}→配偶→{sideZh}{rightTerm.Value.Hans}";
		String officialHant = $"自己→{leftHant}→配偶→{sideZh}{rightTerm.Value.Hant}";
		String officialEn = $"Self → {leftHant} → spouse → {sideEn} {rightTerm.Value.Hant}";
		LocalizedText official = new ( officialHans , officialHant , officialEn );
		return ( label , official );
	}

	/// <summary>
	/// My-frame tier words for a JUNIOR bridge's right side (mumuy 堂甥姻叔母-law): the
	/// target's generation relative to ME = bridge generation + its offset in the spouse's
	/// frame. Only simple shapes compose (pure ascent, optional single branch, optional
	/// trailing spouse); anything else falls back to the child-frame recursion.
	/// </summary>
	private static (String Hans , String Hant)? TryMyFrameJuniorRight ( IReadOnlyList<KinshipToken> right , Int32 bridgeGeneration )
	{
		Int32 ascents = 0;
		Int32 index = 0;
		while ( index < right.Count && right [ index ].Id is "father" or "mother" )
		{
			ascents++;
			index++;
		}

		String? branchId = null;
		if ( index < right.Count && right [ index ].Id is "older-brother" or "younger-brother" or "older-sister" or "younger-sister" )
		{
			branchId = right [ index ].Id;
			index++;
		}

		Boolean spoused = false;
		if ( index < right.Count && right [ index ].Id == "spouse" )
		{
			spoused = true;
			index++;
		}

		if ( index != right.Count || ( ascents == 0 && branchId is null ) )
		{
			return null;
		}

		Int32 myGen = bridgeGeneration + ascents;
		Boolean branchMale = branchId is "older-brother" or "younger-brother";

		String? word;
		if ( branchId is null )
		{
			// Pure ascent: the bare tier word (mumuy 姻父/姻祖父); a female link marks the
			// stem 外 from the 祖-tier up (堂甥姻外祖父 for F.M.F). At my-frame generation 0
			// the ascent lands on my own tier: sibling words (堂甥姻兄 for F from a -1
			// bridge). Spouses unattested on the ascent path.
			if ( spoused || myGen < 0 )
			{
				return null;
			}

			if ( myGen == 0 )
			{
				Boolean genZeroMale = right [ ascents - 1 ].Id == "father";
				String sibling = genZeroMale ? "兄" : "姐";
				return ( sibling , KinshipScriptConverter.ToHant ( sibling ) );
			}

			// Only INTERIOR links mark the stem 外 (堂甥姻外祖父 for F.M.F); the target's
			// own gender at the top does not (F.F.M is a plain 姻祖母).
			Boolean anyFemaleLink = false;
			for ( Int32 i = 0 ; i < ascents - 1 ; i++ )
			{
				if ( right [ i ].Id == "mother" )
				{
					anyFemaleLink = true;
				}
			}

			Boolean targetMale = right [ ascents - 1 ].Id == "father";
			String marker = anyFemaleLink && myGen >= 2 ? "外" : "";
			word = myGen switch
			{
				1 => targetMale ? "父" : "母" ,
				2 => targetMale ? $"{marker}祖父" : $"{marker}祖母" ,
				3 => targetMale ? $"{marker}曾祖父" : $"{marker}曾祖母" ,
				4 => targetMale ? $"{marker}高祖父" : $"{marker}高祖母" ,
				_ => null
			};
		}
		else if ( myGen >= 1 )
		{
			// Branch tier at my-frame generation ≥ 1: 叔/姑-flavor (mumuy defaults the male
			// flavor to 叔); a spouse closure flips to the matching form. Gen-0 branches
			// keep mumuy's path words (姻叔兄弟) via the child-frame fallback + quirk rules.
			if ( myGen > 3 )
			{
				return null;
			}

			String stem = myGen switch
			{
				2 => "祖" ,
				3 => "曾祖" ,
				_ => ""
			};
			if ( branchMale )
			{
				word = spoused ? $"叔{stem}母" : $"叔{stem}父";
			}
			else
			{
				word = spoused ? $"姑{stem}父" : $"姑{stem}母";
			}
		}
		else
		{
			// Branch tier BELOW my generation: the junior ladder in my frame (mumuy
			// 侄外孙姻孙男/孙妇 for OB(.SP) from a -2 bridge; 姻侄 attested at -1).
			if ( myGen < -3 || ascents > 0 )
			{
				return null; // mixed ascent-then-branch juniors are unattested
			}

			String baseWord = myGen switch
			{
				-1 => "侄" ,
				-2 => "孙" ,
				_ => "曾孙"
			};
			if ( spoused )
			{
				word = branchMale ? $"{baseWord}妇" : $"{baseWord}婿";
			}
			else
			{
				word = branchMale ? baseWord : $"{baseWord}女";
			}
		}

		if ( word is null )
		{
			return null;
		}

		String hant = KinshipScriptConverter.ToHant ( word );
		String hans = KinshipScriptConverter.ToHans ( word );
		return ( hans , hant );
	}

	/// <summary>
	/// Bridge generation (+1 per ascent, −1 per descent) and the last blood person's gender,
	/// straight from the tokens — for bridges the canonical shape builder rejects because of
	/// an internal 表/堂 fork (姨表祖母 = F.M.M.OB.OS.D). Pure blood only: a spouse anywhere in
	/// the bridge abstains so the established spouse-led / trailing-spouse paths keep theirs.
	/// </summary>
	private static Boolean TryAnalyzeBloodBridge ( IReadOnlyList<KinshipToken> left , out Int32 generation , out Boolean isMale )
	{
		generation = 0;
		isMale = false;
		Boolean sawPerson = false;

		foreach ( KinshipToken token in left )
		{
			switch ( token.Id )
			{
				case "father":
				case "adoptive-father":
					generation++; isMale = true; sawPerson = true; break;
				case "mother":
				case "adoptive-mother":
					generation++; isMale = false; sawPerson = true; break;
				case "son":
				case "adoptive-son":
					generation--; isMale = true; sawPerson = true; break;
				case "daughter":
				case "adoptive-daughter":
					generation--; isMale = false; sawPerson = true; break;
				case "older-brother":
				case "younger-brother":
					isMale = true; sawPerson = true; break;
				case "older-sister":
				case "younger-sister":
					isMale = false; sawPerson = true; break;
				default:
					return false; // spouse or unknown token -> not a pure-blood bridge
			}
		}

		return sawPerson;
	}

	/// <summary>
	/// Elder bridge (generation ≥ 1), bare spouse-sibling right side: the spouse's own
	/// brother/sister is an in-law at the BRIDGE's generation. Flavor by bridge gender —
	/// male bridge (眷, spouse = wife): brother 舅 / sister 姨; female bridge (姻, spouse =
	/// husband): brother 叔 / sister 姑 — tiered by the 祖/曾祖/高祖 morpheme (mumuy
	/// 叔祖眷舅祖父 for F.F.OB.W.LB, 叔眷舅父 for F.OB.W.LB, 叔祖眷姨祖母 for F.F.OB.W.LS). Any
	/// other right shape returns null so the child-frame recursion keeps its descent cases.
	/// </summary>
	private static (String Hans , String Hant)? TryTieredElderInLawRight ( IReadOnlyList<KinshipToken> right , Int32 bridgeGeneration , Boolean bridgeIsMale )
	{
		if ( right.Count != 1 )
		{
			return null;
		}

		Boolean brother = right [ 0 ].Id is "older-brother" or "younger-brother";
		Boolean sister = right [ 0 ].Id is "older-sister" or "younger-sister";
		if ( !brother && !sister )
		{
			return null;
		}

		String? stem = bridgeGeneration switch
		{
			1 => "" ,
			2 => "祖" ,
			3 => "曾祖" ,
			4 => "高祖" ,
			_ => null
		};
		if ( stem is null )
		{
			return null;
		}

		String word = ( bridgeIsMale , brother ) switch
		{
			( true , true ) => $"舅{stem}父" ,   // wife's brother
			( true , false ) => $"姨{stem}母" ,  // wife's sister
			( false , true ) => $"叔{stem}父" ,  // husband's brother
			( false , false ) => $"姑{stem}母"   // husband's sister
		};

		return ( KinshipScriptConverter.ToHans ( word ) , KinshipScriptConverter.ToHant ( word ) );
	}

	private static (String Hans , String Hant) TrimBridgeTail ( String hans , String hant )
	{
		foreach ( (String tail , String replacement) in BridgeTailTrims )
		{
			if ( hans.Length > tail.Length && hans.EndsWith ( tail , StringComparison.Ordinal ) )
			{
				hans = hans [ ..^tail.Length ] + replacement;
				break;
			}
		}

		foreach ( (String tail , String replacement) in BridgeTailTrims )
		{
			if ( hant.Length > tail.Length && hant.EndsWith ( tail , StringComparison.Ordinal ) )
			{
				hant = hant [ ..^tail.Length ] + replacement;
				break;
			}
		}

		return ( hans , hant );
	}
}
