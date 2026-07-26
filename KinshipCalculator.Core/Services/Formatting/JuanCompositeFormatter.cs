using System;
using System.Collections.Generic;
using System.Linq;

using KinshipCalculator.Core.Models;
using KinshipCalculator.Core.Models.Semantics;

namespace KinshipCalculator.Core.Services.Formatting;

/// <summary>
/// Generative mumuy 眷-composite grammar (K11), induced from the ported oracle across two
/// probe sweeps: <c>[blood]+SP+[post]</c> names as <c>CompactBlood + 眷 + PostName</c>.
/// Three regimes by the blood part's generation:
///   gen 0 (兄弟/从父兄弟/姑表兄弟/舅表兄弟/姨表兄弟) and gen &lt; 0 (侄/甥/侄孙/侄外孙/堂侄/男/孙):
///     post named on the ego-generation ladder {+2:(外?)祖父, +1:父, 0:兄弟, -1:侄X|甥X or 男/女,
///     -2:(外?)孙X}; post-ancestor depth caps: blood 0 → ≤2 hops, -1 → ≤3, -2 → ≤1.
///   gen &gt; 0 (伯/叔/舅/伯祖/叔祖/叔外祖/舅祖/从父叔/从母叔/从父舅/从母舅):
///     the spouse's kin are named as ego's maternal-side elders — parent→外祖父/外祖母
///     (blood +1 only), sibling→舅父/姨母 (+1 blood) or 舅祖父/姨祖母 (+2 blood),
///     sibling's child→舅表/姨表 cousin forms (+1 blood) or 舅表伯父/舅表姑母/姨姑母 (+2 blood).
/// Only the wife-side pattern is attested, so the rule abstains unless the blood part lands
/// on a male. Specific affinal families (亲家/姻侄/连襟/兄弟眷…) keep their dedicated rules.
/// </summary>
public static class JuanCompositeFormatter
{
	private sealed record BloodPart ( String Hans , String Hant , Int32 Generation );

	public static (LocalizedText Label , LocalizedText Official)? TryFormat ( IReadOnlyList<KinshipToken> tokens , PersonGender selfGender )
	{
		Int32 spouseIndex = -1;
		for ( Int32 i = 0 ; i < tokens.Count ; i++ )
		{
			if ( tokens [ i ].Id.Equals ( "spouse" , StringComparison.Ordinal ) )
			{
				if ( spouseIndex >= 0 )
				{
					return null;
				}

				spouseIndex = i;
			}
		}

		if ( spouseIndex <= 0 || spouseIndex >= tokens.Count - 1 )
		{
			return null;
		}

		List<KinshipToken> blood = tokens.Take ( spouseIndex ).ToList ();
		List<KinshipToken> post = tokens.Skip ( spouseIndex + 1 ).ToList ();

		KinshipChainShape? bloodShape = KinshipChainShapeBuilder.Build ( blood , selfGender );
		KinshipChainShape? postShape = KinshipChainShapeBuilder.Build ( post , selfGender );
		if ( bloodShape is null || postShape is null )
		{
			return null;
		}

		if ( bloodShape.LeadingSpouse || bloodShape.TrailingSpouse || bloodShape.AdoptiveAscent
			|| postShape.LeadingSpouse || postShape.TrailingSpouse || postShape.AdoptiveAscent )
		{
			return null;
		}

		if ( bloodShape.SubjectGender != PersonGender.Male )
		{
			return null; // husband-side composites are unattested in the oracle
		}

		BloodPart? bloodPart = BuildBlood ( bloodShape );
		if ( bloodPart is null )
		{
			return null;
		}

		(String Hans , String Hant)? postPart = BuildPost ( postShape , bloodPart.Generation );
		if ( postPart is null )
		{
			return null;
		}

		LocalizedText label = new (
			$"{bloodPart.Hans}眷{postPart.Value.Hans}" ,
			$"{bloodPart.Hant}眷{postPart.Value.Hant}" ,
			"Affinal composite relative" );

		String zhOfficial = $"自己→{bloodPart.Hans}→配偶→其親屬";
		LocalizedText official = new ( zhOfficial , zhOfficial , "Self → blood relative → spouse → their kin" );

		return ( label , official );
	}

	private static BloodPart? BuildBlood ( KinshipChainShape shape )
	{
		Int32 h = shape.AscentDepth;
		Int32 d = shape.DescentDepth;

		if ( shape.HasBranch )
		{
			KinshipBranchInfo branch = shape.Branch!;
			Boolean branchIsMale = branch.Gender == PersonGender.Male;

			if ( h == 0 )
			{
				if ( d == 0 )
				{
					return branchIsMale ? new BloodPart ( "兄弟" , "兄弟" , 0 ) : null;
				}

				if ( d > 2 )
				{
					return null;
				}

				String baseHans = branchIsMale ? "侄" : "甥";
				String baseHant = branchIsMale ? "姪" : "甥";
				String wai = d >= 2 && shape.DescentGenders [ 0 ] == PersonGender.Female ? "外" : String.Empty;
				String ladderHans = d == 2 ? "孙" : String.Empty;
				String ladderHant = d == 2 ? "孫" : String.Empty;

				return new BloodPart ( $"{baseHans}{wai}{ladderHans}" , $"{baseHant}{wai}{ladderHant}" , -d );
			}

			if ( h == 1 && d == 0 )
			{
				// Parent's brother: 伯/叔 (paternal, by order) or 舅 (maternal).
				if ( !branchIsMale )
				{
					return null;
				}

				if ( shape.AscentGenders [ 0 ] == PersonGender.Male )
				{
					String flavor = branch.Order == SiblingOrder.Older ? "伯" : "叔";
					return new BloodPart ( flavor , flavor , 1 );
				}

				return new BloodPart ( "舅" , "舅" , 1 );
			}

			if ( h == 1 && d == 1 )
			{
				// Cousins: 从父兄弟 / 姑表兄弟 / 舅表兄弟 / 姨表兄弟.
				Boolean parentIsMale = shape.AscentGenders [ 0 ] == PersonGender.Male;
				String hans = parentIsMale
					? ( branchIsMale ? "从父兄弟" : "姑表兄弟" )
					: ( branchIsMale ? "舅表兄弟" : "姨表兄弟" );
				String hant = parentIsMale
					? ( branchIsMale ? "從父兄弟" : "姑表兄弟" )
					: ( branchIsMale ? "舅表兄弟" : "姨表兄弟" );

				return new BloodPart ( hans , hant , 0 );
			}

			if ( h == 1 && d == 2 )
			{
				// Cousin's son: only the pure-paternal 堂侄 form is attested.
				if ( shape.AscentGenders [ 0 ] == PersonGender.Male && branchIsMale
					&& shape.DescentGenders.All ( static gender => gender == PersonGender.Male ) )
				{
					return new BloodPart ( "堂侄" , "堂姪" , -1 );
				}

				return null;
			}

			if ( h == 2 && d == 0 )
			{
				// Grand-generation elder collateral compacts: 伯祖/叔祖 (all-male ascent),
				// 叔外祖 (male anchor via maternal entry), 舅祖/舅外祖 (female anchor).
				if ( !branchIsMale )
				{
					return null;
				}

				Boolean anchorIsMale = shape.AscentGenders [ 1 ] == PersonGender.Male;
				Boolean entryIsFemale = shape.AscentGenders [ 0 ] == PersonGender.Female;
				if ( anchorIsMale )
				{
					if ( entryIsFemale )
					{
						return new BloodPart ( "叔外祖" , "叔外祖" , 2 ); // oracle ignores order here
					}

					String flavor = branch.Order == SiblingOrder.Older ? "伯祖" : "叔祖";
					return new BloodPart ( flavor , flavor , 2 );
				}

				String stem = entryIsFemale ? "舅外祖" : "舅祖";
				return new BloodPart ( stem , stem , 2 );
			}

			if ( h == 2 && d == 1 )
			{
				// Parent's cousin. mumuy's composite compact is a THREE-factor law (read off
				// the full 12-cell face, e.g. F.F.OS.S.SP.F = 姑表叔眷外祖父, M.M.OB.S.SP.F =
				// 舅表舅眷外祖父 — note it differs from mumuy's own STANDALONE terms, which
				// use 姨伯父-style flavors):
				//   suffix = ENTRY hop:  father -> 叔, mother -> 舅
				//   prefix = (fork parent, fork sibling): same gender -> classical parallel
				//            line 从父/从母; cross gender -> 表 line 姑表 (his sister) /
				//            舅表 (her brother).
				// The old code keyed the prefix off the ascent hops alone, so a sister fork
				// rode the parallel 从父 line.
				Boolean forkParentMale = shape.AscentGenders [ 1 ] == PersonGender.Male;
				Boolean forkMale = shape.Branch!.Gender == PersonGender.Male;
				(String prefixHans , String prefixHant) = ( forkParentMale , forkMale ) switch
				{
					( true , true ) => ( "从父" , "從父" ) ,
					( true , false ) => ( "姑表" , "姑表" ) ,
					( false , true ) => ( "舅表" , "舅表" ) ,
					( false , false ) => ( "从母" , "從母" )
				};
				String suffix = shape.AscentGenders [ 0 ] == PersonGender.Male ? "叔" : "舅";

				return new BloodPart ( $"{prefixHans}{suffix}" , $"{prefixHant}{suffix}" , 1 );
			}

			return null;
		}

		if ( shape.IsPureDescendant )
		{
			if ( shape.DescentGenders.Any ( static gender => gender != PersonGender.Male ) || d > 2 )
			{
				return null;
			}

			return d == 1
				? new BloodPart ( "男" , "男" , -1 )
				: new BloodPart ( "孙" , "孫" , -2 );
		}

		return null;
	}

	private static (String Hans , String Hant)? BuildPost ( KinshipChainShape shape , Int32 bloodGeneration )
	{
		if ( shape.SubjectGender == PersonGender.Unknown )
		{
			return null;
		}

		Boolean isMale = shape.SubjectGender == PersonGender.Male;

		if ( bloodGeneration >= 1 )
		{
			return BuildElderRegimePost ( shape , bloodGeneration , isMale );
		}

		Int32 ancestorCap = bloodGeneration switch
		{
			0 => 2 ,
			-1 => 3 ,
			_ => 1
		};

		Int32 relativeToSpouse;
		if ( shape.IsPureAncestor )
		{
			if ( shape.AscentDepth > ancestorCap )
			{
				return null;
			}

			relativeToSpouse = shape.AscentDepth;
		}
		else if ( shape.HasBranch && shape.AscentDepth == 0 )
		{
			relativeToSpouse = -shape.DescentDepth;
		}
		else
		{
			return null;
		}

		Int32 egoGeneration = bloodGeneration + relativeToSpouse;
		switch ( egoGeneration )
		{
			case 2:
			{
				// 外-mark iff the post path enters through a female hop (兄弟眷外祖父).
				String wai = shape.IsPureAncestor && shape.AscentGenders [ 0 ] == PersonGender.Female && bloodGeneration == 0 ? "外" : String.Empty;
				return isMale ? ( $"{wai}祖父" , $"{wai}祖父" ) : ( $"{wai}祖母" , $"{wai}祖母" );
			}
			case 1:
				return isMale ? ( "父" , "父" ) : ( "母" , "母" );
			case 0:
				return isMale ? ( "兄弟" , "兄弟" ) : ( "姊妹" , "姊妹" );
			case -1 when bloodGeneration == 0 && shape.HasBranch:
			{
				String baseHans = shape.Branch!.Gender == PersonGender.Male ? "侄" : "甥";
				String baseHant = shape.Branch.Gender == PersonGender.Male ? "姪" : "甥";
				String tail = isMale ? "男" : "女";
				return ( $"{baseHans}{tail}" , $"{baseHant}{tail}" );
			}
			case -1:
				return isMale ? ( "男" , "男" ) : ( "女" , "女" );
			case -2 when shape.HasBranch:
			{
				String wai = shape.Branch!.Gender == PersonGender.Female ? "外" : String.Empty;
				String tail = isMale ? "男" : "女";
				return ( $"{wai}孙{tail}" , $"{wai}孫{tail}" );
			}
			default:
				return null;
		}
	}

	private static (String Hans , String Hant)? BuildElderRegimePost ( KinshipChainShape shape , Int32 bloodGeneration , Boolean isMale )
	{
		if ( shape.IsPureAncestor )
		{
			// Only the spouse's own parents are named, and only for +1 bloods (叔眷外祖父).
			if ( shape.AscentDepth != 1 || bloodGeneration != 1 )
			{
				return null;
			}

			return isMale ? ( "外祖父" , "外祖父" ) : ( "外祖母" , "外祖母" );
		}

		if ( !shape.HasBranch || shape.AscentDepth != 0 )
		{
			return null;
		}

		Boolean branchIsMale = shape.Branch!.Gender == PersonGender.Male;

		if ( shape.DescentDepth == 0 )
		{
			// Spouse's siblings read as ego's maternal-side elders.
			if ( bloodGeneration == 1 )
			{
				return branchIsMale ? ( "舅父" , "舅父" ) : ( "姨母" , "姨母" );
			}

			if ( bloodGeneration == 2 )
			{
				return branchIsMale ? ( "舅祖父" , "舅祖父" ) : ( "姨祖母" , "姨祖母" );
			}

			return null;
		}

		if ( shape.DescentDepth == 1 )
		{
			// Spouse's siblings' children.
			if ( bloodGeneration == 1 )
			{
				if ( branchIsMale )
				{
					return isMale ? ( "舅表兄" , "舅表兄" ) : ( "舅表姊" , "舅表姊" );
				}

				return isMale ? ( "姨表兄" , "姨表兄" ) : ( "姨表姊" , "姨表姊" );
			}

			if ( bloodGeneration == 2 )
			{
				if ( branchIsMale )
				{
					return isMale ? ( "舅表伯父" , "舅表伯父" ) : ( "舅表姑母" , "舅表姑母" );
				}

				// Female fork keeps the oracle's no-表 quirk but MUST still split on the
				// terminal's gender — the old unconditional 姨姑母 put a female morpheme on
				// a male person (mumuy: f,f,ob,w,os,s = 叔祖眷姨伯父, ...os,d = 叔祖眷姨姑母).
				return isMale ? ( "姨伯父" , "姨伯父" ) : ( "姨姑母" , "姨姑母" );
			}

			return null;
		}

		return null;
	}
}
