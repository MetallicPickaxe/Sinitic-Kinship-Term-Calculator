using System;
using System.Collections.Generic;

using KinshipCalculator.Core.Models;
using KinshipCalculator.Core.Models.Semantics;

namespace KinshipCalculator.Core.Services.Formatting;

/// <summary>
/// Recursive class-stacking composer for mixed elders reached via descent (K11a, induced
/// from the oracle's sweep-3 rows): the term is the ENTRY parent's relation-class stack to
/// the person's own generation, then the entry flavor and generation stem —
///   M.F.OB.S → 堂舅 · F.M.OB.S → 舅表伯父|叔父 · M.F.F.OB.S.S → 从堂舅父 ·
///   M.F.F.OB.D.D → 堂姑表姨母 · F.F.M.OB.D.D → 舅表姑表姑母 · F4.OS.D2 → 姑表姑表姑祖母.
/// Class stack: fork class by (sub-ascent purity × branch gender) with the 堂-grade ladder
/// for pure-male forks, 姑表 for pure-male forks through a sister, 舅表/姨表 under a female
/// anchor; every female descent hop before the target appends another 姑表. Wired as the
/// LAST rule, so it only claims chains every earlier family left descriptive.
/// </summary>
public static class StackedElderComposer
{
	public static (LocalizedText Label , LocalizedText Official)? TryFormat ( IReadOnlyList<KinshipToken> tokens , PersonGender selfGender )
	{
		KinshipChainShape? shape = KinshipChainShapeBuilder.Build ( tokens , selfGender );
		if ( shape is null || shape.AdoptiveAscent || !shape.HasBranch )
		{
			return null;
		}

		if ( shape.LeadingSpouse )
		{
			// K4 frozen spouse-side convention: male ego prefixes 岳 over the inner chain's
			// composition, female ego uses 隨夫稱 (the inner form as-is), unknown renders the
			// gender-conditional pair.
			KinshipChainShape inner = new (
				shape.AscentGenders , shape.Branch , shape.DescentGenders ,
				leadingSpouse: false , shape.TrailingSpouse , selfGender );

			(LocalizedText Label , LocalizedText Official)? sameGen = ComposeSpouseSameGeneration ( inner , selfGender );
			if ( sameGen is not null )
			{
				return sameGen;
			}

			(LocalizedText Label , LocalizedText Official)? innerName = Compose ( inner , spouseRooted: true );
			if ( innerName is null )
			{
				return null;
			}

			String maleHans = $"岳{innerName.Value.Label.ZhHans}";
			String maleHant = $"岳{innerName.Value.Label.ZhHant}";
			String hans;
			String hant;
			switch ( selfGender )
			{
				case PersonGender.Male:
					hans = maleHans;
					hant = maleHant;
					break;
				case PersonGender.Female:
					hans = innerName.Value.Label.ZhHans;
					hant = innerName.Value.Label.ZhHant;
					break;
				default:
					hans = $"男：{maleHans}；女：{innerName.Value.Label.ZhHans}";
					hant = $"男：{maleHant}；女：{innerName.Value.Label.ZhHant}";
					break;
			}

			LocalizedText spousedLabel = new ( hans , hant , $"Spouse's {innerName.Value.Label.English}" );
			String zhOfficialSp = $"自己→配偶→{innerName.Value.Official.ZhHans.Replace ( "自己→" , "" )}";
			LocalizedText spousedOfficial = new ( zhOfficialSp , zhOfficialSp , $"Self → spouse → {innerName.Value.Official.English}" );
			return ( spousedLabel , spousedOfficial );
		}

		return Compose ( shape );
	}

	/// <summary>
	/// g=0 spouse-rooted regime (mumuy 重表大姑子/四从父姑夫-family): the spouse's same-
	/// generation collateral takes the stacked class plus an EGO-gendered sibling-in-law
	/// word — male ego (wife's side) 姨子/姨夫, female ego (husband's side) 姑子/姑夫.
	/// Only the attested female-target cells compose; male targets stay descriptive.
	/// </summary>
	private static (LocalizedText Label , LocalizedText Official)? ComposeSpouseSameGeneration ( KinshipChainShape inner , PersonGender selfGender )
	{
		Int32 h = inner.AscentDepth;
		Int32 d = inner.DescentDepth;
		if ( h < 2 || h != d || !inner.HasBranch )
		{
			return null;
		}

		PersonGender entryGender = inner.AscentGenders [ 0 ];
		if ( entryGender == PersonGender.Unknown )
		{
			return null;
		}

		Boolean subAscentAllMale = true;
		for ( Int32 i = 1 ; i < h ; i++ )
		{
			if ( inner.AscentGenders [ i ] == PersonGender.Unknown )
			{
				return null;
			}

			if ( inner.AscentGenders [ i ] != PersonGender.Male )
			{
				subAscentAllMale = false;
			}
		}

		Boolean crossed = false;
		for ( Int32 i = 0 ; i < d - 1 ; i++ )
		{
			if ( inner.DescentGenders [ i ] == PersonGender.Female )
			{
				crossed = true;
			}
		}

		String crossings = crossed ? "姑表" : String.Empty;
		Boolean branchIsMale = inner.Branch!.Gender == PersonGender.Male;
		String forkClass;
		if ( subAscentAllMale )
		{
			forkClass = branchIsMale
				? ( crossed ? "堂" : ( h - 1 ) switch { 1 => "堂" , 2 => "从堂" , _ => "族" } )
				: "姑表";
		}
		else
		{
			forkClass = branchIsMale ? "舅表" : "姨表";
		}

		if ( crossings == forkClass )
		{
			crossings = String.Empty;
		}

		if ( h == 2 && crossings.Length == 0 )
		{
			return null; // shallow same-gen cells stay with legacy vocabulary
		}

		// Attested terminals: female targets (姑子/姨子 + 夫-closures) and the male
		// target's WIFE (四从父大婶子/妯娌); the bare male target stays descriptive.
		Boolean bloodIsFemale = inner.TrailingSpouse
			? inner.RelativeGender == PersonGender.Male
			: inner.RelativeGender == PersonGender.Female;
		String stack = $"{forkClass}{crossings}";
		String maleForm;
		String femaleForm;
		if ( bloodIsFemale )
		{
			String suffix = inner.TrailingSpouse ? "夫" : "子";
			maleForm = $"{stack}姨{suffix}";
			femaleForm = $"{stack}姑{suffix}";
		}
		else if ( inner.TrailingSpouse )
		{
			// Cousin-brother's wife: the wives-of-brothers network word for a female ego
			// (妯娌), the 婶-form for a male ego — mumuy lists both on these cells.
			maleForm = $"{stack}婶子";
			femaleForm = $"{stack}妯娌";
		}
		else
		{
			// The bare cousin-brother himself: 舅子 for a male ego (wife's side), the
			// order-dual pair word 伯叔 for a female ego. mumuy lists all four spellings
			// on these cells: 重表大伯子 | 重表小叔子 | 重表伯叔 | 重表大舅子.
			maleForm = $"{stack}舅子";
			femaleForm = $"{stack}伯叔";
		}
		String hans = selfGender switch
		{
			PersonGender.Male => maleForm ,
			PersonGender.Female => femaleForm ,
			_ => $"男：{maleForm}；女：{femaleForm}"
		};
		String hant = KinshipScriptConverter.ToHant ( hans );

		LocalizedText label = new ( KinshipScriptConverter.ToHans ( hans ) , hant , "Spouse's same-generation collateral" );
		String zhOfficial = $"自己→配偶→第{h}代祖輩分支(疊類{stack})→同輩{( inner.TrailingSpouse ? "→配偶" : "" )}";
		LocalizedText official = new ( zhOfficial , zhOfficial , "Spouse's same-generation collateral" );
		return ( label , official );
	}

	private static (LocalizedText Label , LocalizedText Official)? Compose ( KinshipChainShape shape , Boolean spouseRooted = false )
	{

		Int32 h = shape.AscentDepth;
		Int32 d = shape.DescentDepth;
		Int32 g = h - d;
		// Shallow-fork spouse closures (h ≤ 1, d ≥ 2) are rescued here because the legacy
		// sibling-descendant family builds their base word with the SPOUSE's gender
		// (F.OS.D.D.SP became 姑表甥子 instead of 姑表甥婿) — the slot law below is the
		// K1-correct closure. Blood shallow juniors stay legacy (main-face K1 faces).
		Boolean shallowSpouseJunior = shape.TrailingSpouse && h < 2 && d >= 2 && g < 0;
		if ( ( h < 2 && !shallowSpouseJunior ) || d < 1 || g is > 9 or 0 or < -4 )
		{
			return null;
		}

		PersonGender entryGender = h > 0 ? shape.AscentGenders [ 0 ] : PersonGender.Male;
		if ( h > 0 && entryGender == PersonGender.Unknown )
		{
			return null;
		}

		// Fork class from the ENTRY PARENT's perspective: sub-ascent = ascent[1..].
		Boolean subAscentAllMale = true;
		for ( Int32 i = 1 ; i < h ; i++ )
		{
			if ( shape.AscentGenders [ i ] == PersonGender.Unknown )
			{
				return null;
			}

			if ( shape.AscentGenders [ i ] != PersonGender.Male )
			{
				subAscentAllMale = false;
			}
		}

		// A female descent hop before the target (elders) or above the gen-zero person
		// (juniors: 堂姑表甥外孙婿 for F.F.OB.D⁴) marks ONE 姑表 crossing — the oracle caps
		// the stack at a single level, and the junior LADDER still ignores mid-descent
		// ordering (妻重表甥孙女 covers D4.S.D).
		String crossings = String.Empty;
		{
			Boolean crossed = false;
			Int32 crossingBound = Math.Min ( h , d ) - 1;
			for ( Int32 i = 0 ; i < crossingBound ; i++ )
			{
				if ( shape.DescentGenders [ i ] == PersonGender.Female )
				{
					crossed = true;
				}
			}

			crossings = crossed ? "姑表" : String.Empty;
		}

		if ( h == 2 && crossings.Length == 0 && !spouseRooted && g >= 1 )
		{
			// Simple h=2 ELDER cells (堂舅/舅表-tier) are legacy's settled ground and
			// already absorbed by the comparison bridges — the composer only adds value on
			// deep forks (h ≥ 3) and crossed lines, where legacy under-grades. Spouse-
			// rooted chains are exempt (legacy is unreachable from the SP-wrapper path),
			// and so are JUNIORS: their h=2 ground was only ever served by the folded
			// semantic-vector words (堂甥孙女 without the penult-外 slot, spouse-gender
			// bases), which claim exact and bury the correct form — K9-A surgery cut #1.
			return null;
		}

		Boolean branchIsMale = shape.Branch!.Gender == PersonGender.Male;
		String? forkClass;
		if ( h == 0 )
		{
			forkClass = String.Empty; // own-sibling line (甥外孙婿-class): no fork prefix
		}
		else if ( h == 1 && entryGender == PersonGender.Female )
		{
			// Maternal single-hop forks are the 舅表/姨表 lines (M.OB.D.D.SP = 舅表甥婿),
			// not the paternal 堂/姑表 the pure-male ladder below would assign.
			forkClass = branchIsMale ? "舅表" : "姨表";
		}
		else if ( subAscentAllMale )
		{
			if ( branchIsMale )
			{
				// Pure-male fork: 堂-grade ladder by the parent's fork height — but once the
				// line crosses a female hop the oracle collapses the grade back to plain 堂
				// (M.F.F.OB.S.S → 从堂舅父 yet M.F.F.OB.D.D → 堂姑表姨母).
				forkClass = crossings.Length > 0
					? "堂"
					: ( h - 1 ) switch
					{
						0 => "堂" ,
						1 => "堂" ,
						2 => "从堂" ,
						_ => "族"
					};
			}
			else
			{
				forkClass = "姑表";
			}
		}
		else
		{
			// Female anchor above the entry parent.
			forkClass = branchIsMale ? "舅表" : "姨表";
		}

		if ( forkClass is null )
		{
			return null;
		}

		// Terminal: entry flavor × target gender × generation stem; trailing spouse flips
		// male targets to the flavor's 母-form (堂舅妈-class), female+spouse is unattested.
		Boolean targetIsMale = shape.RelativeGender == PersonGender.Male;
		if ( shape.TrailingSpouse && !targetIsMale )
		{
			// RelativeGender already flipped: original female target with spouse → male form
			// handled below; an original male target's WIFE lands here as female.
		}

		if ( crossings == forkClass )
		{
			// mumuy stacks ONE class level total (重表甥女, not a double stack).
			crossings = String.Empty;
		}

		Boolean entryIsMale = entryGender == PersonGender.Male;
		String? terminal;
		if ( g >= 1 )
		{
			Boolean bareFlavor = forkClass == "堂" && crossings.Length == 0 && g == 1;
			terminal = BuildTerminal ( entryIsMale , targetIsMale , g , shape.TrailingSpouse , bareFlavor );
		}
		else
		{
			terminal = BuildJuniorTerminal ( shape , -g );
		}

		if ( terminal is null )
		{
			return null;
		}

		String term = $"{forkClass}{crossings}{terminal}";
		String hant = KinshipScriptConverter.ToHant ( term );
		String hans = KinshipScriptConverter.ToHans ( term );
		LocalizedText label = new ( hans , hant , $"Stacked collateral elder (+{g})" );

		String zhOfficial = $"自己→第{h}代祖輩分支(疊類{forkClass})→第{g}代長輩{( shape.TrailingSpouse ? "→配偶" : "" )}";
		LocalizedText official = new ( zhOfficial , zhOfficial , $"Stacked collateral elder (+{g})" );

		return ( label , official );
	}

	private static String? BuildJuniorTerminal ( KinshipChainShape shape , Int32 depthBelow )
	{
		if ( depthBelow > 4 )
		{
			return null;
		}

		// Junior line by the GEN-ZERO person's gender — the descent hop at ego's own
		// generation (descent[h-1]), the same rule K1 froze: 四从父甥孙女 (OB.S3.D.S.D,
		// genZero=D) vs 四从父侄外孙女 (OB.S4.D.D, genZero=S). A sister fork with an
		// all-male descent still reads 甥 via the branch itself; at h=0 the gen-zero
		// person IS the sibling, so the branch gender decides (sister → 甥外孙婿).
		// Juniors always have d ≥ h+1, so the gen-zero index is in range.
		String baseChar = shape.AscentDepth == 0
			? ( shape.Branch!.Gender == PersonGender.Female ? "甥" : "侄" )
			: ( shape.DescentGenders [ shape.AscentDepth - 1 ] == PersonGender.Female ? "甥" : "侄" );
		String ladder = String.Empty;
		if ( depthBelow == 2 )
		{
			Boolean penultimateFemale = shape.DescentGenders [ ^2 ] == PersonGender.Female;
			ladder = penultimateFemale ? "外孙" : "孙";
		}
		else if ( depthBelow >= 3 )
		{
			// 曾/玄-tier ladders: only the all-male interior is oracle-attested
			// (四从父侄曾孙女 / 四从父侄玄孙婿); a female hop between gen-zero and the
			// target has no attested 外-position — keep the descriptive fallback there.
			for ( Int32 i = shape.AscentDepth ; i < shape.DescentGenders.Count - 1 ; i++ )
			{
				if ( shape.DescentGenders [ i ] != PersonGender.Male )
				{
					return null;
				}
			}

			ladder = depthBelow == 3 ? "曾孙" : "玄孙";
		}

		if ( shape.TrailingSpouse )
		{
			// Spouse closures: a female junior's husband → 婿, a male junior's wife → 媳
			// (妻重表甥外孙妇; the compact layer unifies 妇/媳).
			Boolean bloodFemale = shape.RelativeGender == PersonGender.Male; // spouse flip
			return bloodFemale ? $"{baseChar}{ladder}婿" : $"{baseChar}{ladder}媳";
		}

		String tail = shape.RelativeGender == PersonGender.Female ? "女" : String.Empty;
		return $"{baseChar}{ladder}{tail}";
	}

	private static String? BuildTerminal ( Boolean entryIsMale , Boolean targetIsMale , Int32 g , Boolean trailingSpouse , Boolean bareFlavor )
	{
		// RelativeGender already accounts for the trailing spouse, so targetIsMale describes
		// the NAMED person; the flavor follows the blood person (flip back when spoused).
		Boolean bloodIsMale = trailingSpouse ? !targetIsMale : targetIsMale;

		// Maternal entry (mother as the first hop) yields 舅/姨 flavors; paternal entry
		// yields 伯/姑 (伯父|叔父 is an order-dual in the oracle — 伯 is emitted).
		String flavor = entryIsMale
			? ( bloodIsMale ? "伯" : "姑" )
			: ( bloodIsMale ? "舅" : "姨" );

		String stem = g switch
		{
			2 => "祖" ,
			3 => "曾祖" ,
			4 => "高祖" ,
			5 => "天祖" ,
			6 => "烈祖" ,
			7 => "太祖" ,
			8 => "远祖" ,
			9 => "鼻祖" ,
			_ => String.Empty
		};

		if ( trailingSpouse )
		{
			if ( g >= 2 )
			{
				// Grand-tier spouses take the plain slot form of their own gender
				// (妻重表姑祖父 = the 姑祖母-person's husband; 重表叔祖母 = the wife).
				return targetIsMale ? $"{flavor}{stem}父" : $"{flavor}{stem}母";
			}

			// Parent-tier: 妈-form at the bare-堂 tier (堂舅妈), 母-form on graded tiers
			// (从堂舅母); a female blood elder's husband takes the flavor's 父-form
			// (妻重表姑父 attested — the compact layer already equates 丈/父 spellings).
			if ( !bloodIsMale )
			{
				return $"{flavor}父";
			}

			return bareFlavor ? $"{flavor}妈" : $"{flavor}{stem}母";
		}

		if ( bareFlavor )
		{
			// 堂-fork parent-level elders keep the bare flavor (堂舅/堂姨) — the settled
			// legacy vocabulary and the oracle agree.
			return flavor;
		}

		return targetIsMale ? $"{flavor}{stem}父" : $"{flavor}{stem}母";
	}
}
