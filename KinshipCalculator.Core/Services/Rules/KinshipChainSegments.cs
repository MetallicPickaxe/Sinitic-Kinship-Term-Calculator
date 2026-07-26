using System;
using System.Collections.Generic;
using System.Linq;

using KinshipCalculator.Core.Models;

namespace KinshipCalculator.Core.Services.Rules;

public sealed class KinshipChainSegments
{
	private KinshipChainSegments (
		IReadOnlyList<KinshipToken> parents ,
		IReadOnlyList<KinshipToken> siblings ,
		IReadOnlyList<KinshipToken> descendants ,
		IReadOnlyList<KinshipToken> spouses ,
		IReadOnlyList<KinshipToken> remaining )
	{
		Parents = parents;
		Siblings = siblings;
		Descendants = descendants;
		Spouses = spouses;
		Remaining = remaining;
	}

	public IReadOnlyList<KinshipToken> Parents { get; }
	public IReadOnlyList<KinshipToken> Siblings { get; }
	public IReadOnlyList<KinshipToken> Descendants { get; }
	public IReadOnlyList<KinshipToken> Spouses { get; }
	public IReadOnlyList<KinshipToken> Remaining { get; }

	public Boolean ContainsOnlyParents => Parents.Count > 0 && Parents.Count == TotalCount;
	public Boolean ContainsOnlyChildren => Descendants.Count > 0 && Descendants.Count == TotalCount;
	public Boolean ContainsOnlySiblings => Siblings.Count > 0 && Siblings.Count == TotalCount;
	public Boolean ContainsOnlySpouses => Spouses.Count > 0 && Spouses.Count == TotalCount;
	public Int32 TotalCount => Parents.Count + Siblings.Count + Descendants.Count + Spouses.Count + Remaining.Count;

	public static KinshipChainSegments Analyze ( IReadOnlyList<KinshipToken> tokens )
	{
		if ( tokens.Count == 0 )
		{
			return new KinshipChainSegments ( [] , [] , [] , [] , [] );
		}

		List<KinshipToken> parents = [];
		List<KinshipToken> siblings = [];
		List<KinshipToken> descendants = [];
		List<KinshipToken> spouses = [];
		List<KinshipToken> remaining = [];

		Int32 index = 0;

		while ( index < tokens.Count && IsParent ( tokens [ index ] ) )
		{
			parents.Add ( tokens [ index ] );
			index++;
		}

		while ( index < tokens.Count && IsSibling ( tokens [ index ] ) )
		{
			siblings.Add ( tokens [ index ] );
			index++;
		}

		while ( index < tokens.Count && IsChild ( tokens [ index ] ) )
		{
			descendants.Add ( tokens [ index ] );
			index++;
		}

		while ( index < tokens.Count && IsSpouse ( tokens [ index ] ) )
		{
			spouses.Add ( tokens [ index ] );
			index++;
		}

		if ( index < tokens.Count )
		{
			for ( Int32 i = index ; i < tokens.Count ; i++ )
			{
				remaining.Add ( tokens [ i ] );
			}
		}

		return new KinshipChainSegments ( parents , siblings , descendants , spouses , remaining );
	}

	private static Boolean IsParent ( KinshipToken token )
		=> token.Id is "father" or "mother" or "adoptive-father" or "adoptive-mother";

	private static Boolean IsChild ( KinshipToken token )
		=> token.Id is "son" or "daughter" or "adoptive-son" or "adoptive-daughter";

	private static Boolean IsSibling ( KinshipToken token )
		=> token.Id is "older-brother" or "younger-brother" or "older-sister" or "younger-sister";

	private static Boolean IsSpouse ( KinshipToken token )
		=> token.Id is "spouse";
}
