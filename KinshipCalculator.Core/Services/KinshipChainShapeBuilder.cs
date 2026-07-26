using System;
using System.Collections.Generic;

using KinshipCalculator.Core.Models;
using KinshipCalculator.Core.Models.Semantics;

namespace KinshipCalculator.Core.Services;

/// <summary>
/// Parses a token chain into its lossless canonical shape, or returns <c>null</c> when the
/// chain is not canonical (mid-chain spouses, multiple sibling pivots, sibling after descent,
/// adoptive links, ...). Non-canonical chains fall through to the legacy rule pipeline.
/// </summary>
public static class KinshipChainShapeBuilder
{
	public static KinshipChainShape? Build ( IReadOnlyList<KinshipToken> tokens , PersonGender selfGender )
	{
		if ( tokens.Count == 0 )
		{
			return null;
		}

		Int32 index = 0;
		Boolean leadingSpouse = false;

		if ( IsSpouse ( tokens [ index ] ) )
		{
			leadingSpouse = true;
			index++;
		}

		List<PersonGender> ascent = new ();
		Boolean adoptiveAscent = false;
		while ( index < tokens.Count && TryGetParentGender ( tokens [ index ] , out PersonGender parentGender , out Boolean parentIsAdoptive ) )
		{
			if ( parentIsAdoptive )
			{
				adoptiveAscent = true;
			}

			ascent.Add ( parentGender );
			index++;
		}

		KinshipBranchInfo? branch = null;
		if ( index < tokens.Count && TryGetSibling ( tokens [ index ] , out KinshipBranchInfo sibling ) )
		{
			branch = sibling;
			index++;
		}

		List<PersonGender> descent = new ();
		while ( index < tokens.Count && TryGetChildGender ( tokens [ index ] , out PersonGender childGender , out Boolean childIsAdoptive ) )
		{
			if ( childIsAdoptive )
			{
				return null;
			}

			descent.Add ( childGender );
			index++;
		}

		Boolean trailingSpouse = false;
		if ( index < tokens.Count && IsSpouse ( tokens [ index ] ) )
		{
			trailingSpouse = true;
			index++;
		}

		if ( index != tokens.Count )
		{
			// Leftover tokens: the chain does not match SP? P* B? C* SP? — not canonical.
			return null;
		}

		if ( ascent.Count == 0 && branch is null && descent.Count == 0 )
		{
			// Spouse-only chains stay with the legacy spouse-only rule.
			return null;
		}

		if ( adoptiveAscent && ( branch is not null || descent.Count > 0 || leadingSpouse ) )
		{
			// Adoptive links compose only on the pure-ancestor family for now; other
			// adoptive shapes keep their legacy handling.
			return null;
		}

		return new KinshipChainShape ( ascent , branch , descent , leadingSpouse , trailingSpouse , selfGender , adoptiveAscent );
	}

	private static Boolean IsSpouse ( KinshipToken token )
		=> token.Id.Equals ( "spouse" , StringComparison.Ordinal );

	private static Boolean TryGetParentGender ( KinshipToken token , out PersonGender gender , out Boolean isAdoptive )
	{
		switch ( token.Id )
		{
			case "father":
				gender = PersonGender.Male;
				isAdoptive = false;
				return true;
			case "mother":
				gender = PersonGender.Female;
				isAdoptive = false;
				return true;
			case "adoptive-father":
				gender = PersonGender.Male;
				isAdoptive = true;
				return true;
			case "adoptive-mother":
				gender = PersonGender.Female;
				isAdoptive = true;
				return true;
			default:
				gender = PersonGender.Unknown;
				isAdoptive = false;
				return false;
		}
	}

	private static Boolean TryGetChildGender ( KinshipToken token , out PersonGender gender , out Boolean isAdoptive )
	{
		switch ( token.Id )
		{
			case "son":
				gender = PersonGender.Male;
				isAdoptive = false;
				return true;
			case "daughter":
				gender = PersonGender.Female;
				isAdoptive = false;
				return true;
			case "adoptive-son":
				gender = PersonGender.Male;
				isAdoptive = true;
				return true;
			case "adoptive-daughter":
				gender = PersonGender.Female;
				isAdoptive = true;
				return true;
			default:
				gender = PersonGender.Unknown;
				isAdoptive = false;
				return false;
		}
	}

	private static Boolean TryGetSibling ( KinshipToken token , out KinshipBranchInfo branch )
	{
		switch ( token.Id )
		{
			case "older-brother":
				branch = new KinshipBranchInfo ( SiblingOrder.Older , PersonGender.Male );
				return true;
			case "younger-brother":
				branch = new KinshipBranchInfo ( SiblingOrder.Younger , PersonGender.Male );
				return true;
			case "older-sister":
				branch = new KinshipBranchInfo ( SiblingOrder.Older , PersonGender.Female );
				return true;
			case "younger-sister":
				branch = new KinshipBranchInfo ( SiblingOrder.Younger , PersonGender.Female );
				return true;
			default:
				branch = null!;
				return false;
		}
	}
}
