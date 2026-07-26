using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using KinshipCalculator.Core.Data;
using KinshipCalculator.Core.Models;
using KinshipCalculator.Core.Services.Rules;

namespace KinshipCalculator.Core.Services;

public sealed class KinshipCalculator : IKinshipCalculator
{
	private readonly IReadOnlyDictionary<String , KinshipTerm> terms_field;
	private readonly IReadOnlyDictionary<String , KinshipToken> tokenLookup_field;

private sealed record CandidateSequence (
	IReadOnlyList<KinshipToken> Tokens ,
	String Explanation ,
	Int32 Priority ,
	Int32 DirectionChanges ,
	Int32 Length,
	Boolean IsExactMatch = false
);

	private const Int32 InlineBufferLength = 256;

	private static readonly String SelfHansPrefix = Encoding.UTF8.GetString ( "自己→"u8 );
	private static readonly String SelfHantPrefix = Encoding.UTF8.GetString ( "自己→"u8 );
	private static readonly String EnglishSelfPrefix = Encoding.UTF8.GetString ( "Self → "u8 );
	private static readonly String HansArrowSeparator = Encoding.UTF8.GetString ( "→"u8 );
	private static readonly String EnglishArrowSeparator = Encoding.UTF8.GetString ( " → "u8 );
	private static readonly String HansConnector = Encoding.UTF8.GetString ( "的"u8 );
	private static readonly String HantConnector = Encoding.UTF8.GetString ( "的"u8 );
	private static readonly String EnglishConnector = Encoding.UTF8.GetString ( " of "u8 );
	private static readonly String ShortestPathPrefix = Encoding.UTF8.GetString ( "最短路徑："u8 );
	private static readonly String LoopPrefix = Encoding.UTF8.GetString ( "中途閉環："u8 );
	private static readonly String LoopSeparator = Encoding.UTF8.GetString ( "；"u8 );

	public KinshipCalculator ()
	{
		Tokens = KinshipData.Tokens;
		terms_field = KinshipData.Terms;
		tokenLookup_field = Tokens.ToDictionary ( t => t.Id , t => t );
		Formatting.AffinalWebComposer.Evaluator ??= EvaluateSubChain;
	}

	[ThreadStatic]
	private static Int32 subChainDepth_field;

	private static (String Hans , String Hant)? EvaluateSubChain ( IReadOnlyList<String> tokenIds , PersonGender selfGender )
	{
		// Recursive evaluator for the affinal-web composer. Splits strictly shorten the
		// chain, so recursion is finite; the depth cap is a hard rail against any future
		// candidate machinery reintroducing spouses. Descriptive/composite sub-terms
		// abstain so the whole-chain fallback keeps its wording.
		if ( subChainDepth_field >= 4 )
		{
			return null;
		}

		subChainDepth_field++;
		try
		{
			KinshipResult result = new KinshipCalculator ().Evaluate ( tokenIds , "zh-Hans" , selfGender );
			String hans = result.Term.ForLanguage ( "zh-Hans" );
			String hant = result.Term.ForLanguage ( "zh-Hant" );
			if ( String.IsNullOrWhiteSpace ( hans )
				|| hans.Contains ( '的' )
				|| hans.Contains ( "男：" , StringComparison.Ordinal )
				|| hans.StartsWith ( "自己" , StringComparison.Ordinal ) )
			{
				return null;
			}

			return ( hans , hant );
		}
		finally
		{
			subChainDepth_field--;
		}
	}

	public IReadOnlyList<KinshipToken> Tokens { get; }

	public KinshipResult Evaluate ( IReadOnlyList<String> tokenIds , String languageKey , PersonGender selfGender )
	{
        IReadOnlyList<KinshipToken> tokens = ResolveTokens ( tokenIds );
		IReadOnlyList<KinshipToken> normalized = NormalizeTokens ( tokens );
		SimplifiedPathResult simplified = KinshipPathSimplifier.Simplify ( normalized , selfGender , tokenLookup_field );

		List<CandidateSequence> candidateEntries = new ();
		HashSet<String> seenKeys = new ( StringComparer.Ordinal );

		void AddCandidate ( IReadOnlyList<KinshipToken> sequence , String explanation , Int32 priority , Boolean isExactMatch = false )
		{
			if ( sequence.Count == 0 )
			{
				return;
			}

			if ( normalized.Count > 0 && normalized [ ^1 ].Id.Equals ( "spouse" , StringComparison.Ordinal ) )
			{
				if ( sequence.Count == 0 || !sequence [ ^1 ].Id.Equals ( "spouse" , StringComparison.Ordinal ) )
				{
					return;
				}
			}

			String key = BuildKey ( sequence );
			if ( key.Length == 0 )
			{
				return;
			}

			if ( seenKeys.Add ( key ) )
			{
				Int32 directionChanges = CountDirectionChanges ( sequence );
				candidateEntries.Add ( new CandidateSequence ( sequence , explanation , priority , directionChanges , sequence.Count , isExactMatch ) );
			}
		}

		foreach ( CandidateSequence alt in BuildAncestorSiblingAlternatives ( normalized ) )
		{
			AddCandidate ( alt.Tokens , alt.Explanation , alt.Priority );
		}

		foreach ( CandidateSequence alt in BuildAncestorChildSiblingCollapse ( normalized ) )
		{
			AddCandidate ( alt.Tokens , alt.Explanation , alt.Priority );
		}

        // Always attempt to resolve the raw normalized chain using the new smart analyzer
        // If it's an exact match from a rule, give it highest priority
        RuleResolution tempResolution;
        if (RuleDrivenKinshipResolver.TryResolve(normalized, RelationVectorBuilder.Build(normalized, selfGender), selfGender, out tempResolution) && tempResolution.IsExactMatch)
        {
             AddCandidate(normalized, "智能分析", -100, isExactMatch: true); // Very high priority for exact rule matches
        }
        else
        {
             AddCandidate(normalized, "智能分析", 2); // Default priority for general smart analysis
        }

		foreach ( IReadOnlyList<KinshipToken> candidate in simplified.CandidatePaths )
		{
			LocalizedText simplifiedPath = BuildPath ( candidate );
		    AddCandidate ( candidate , BuildExplanation ( simplifiedPath , simplified.Loops ) , priority: 1 );
		}

		IReadOnlyList<KinshipToken> pruned = RemoveLoopSegments ( normalized , simplified.Loops );
		if ( !ReferenceEquals ( pruned , normalized ) )
		{
			SimplifiedPathResult prunedResult = KinshipPathSimplifier.Simplify ( pruned , selfGender , tokenLookup_field );
			foreach ( IReadOnlyList<KinshipToken> path in prunedResult.CandidatePaths )
			{
				LocalizedText prunedPath = BuildPath ( path );
				String explanation = $"{BuildExplanation ( prunedPath , prunedResult.Loops )}；迴圈裁剪";
				AddCandidate ( path , explanation , priority: 0 );
			}
		}

		List<KinshipResolutionOption> options = new ();

		foreach ( CandidateSequence entry in candidateEntries
			.OrderBy ( entry => entry.Priority )
			.ThenBy ( entry => entry.DirectionChanges )
			.ThenBy ( entry => entry.Length ) )
		{
			IReadOnlyList<KinshipToken> candidate = entry.Tokens;
			LocalizedText label;
			Boolean isExact;
			LocalizedText? alternate = LocalizedText.Empty; // Initialize to Empty
			LocalizedText? official = LocalizedText.Empty; // Initialize to Empty

			String candidateKey = BuildKey ( candidate );
			RelationVector vector = RelationVectorBuilder.Build ( candidate , selfGender );
			RuleResolution? ruleResolution = null;
			
			if ( terms_field.TryGetValue ( candidateKey , out KinshipTerm? known ) )
			{
				label = known.Label;
				isExact = true;
				alternate = known.AlternateLabel;
			}
			else if ( RuleDrivenKinshipResolver.TryResolve ( candidate , vector , selfGender , out RuleResolution resolution ) )
			{
				label = resolution.Label;
				// Honor the rule's own exactness claim instead of stamping every hit exact —
				// unconditional stamping is what let structure-collapsing shortcuts outrank
				// the structure-preserving candidates (the forced-exact amplifier).
				isExact = resolution.IsExactMatch;
				ruleResolution = resolution;
				alternate = resolution.AlternateLabel;
				official = resolution.OfficialDescription;
			}
			else
			{
				label = BuildFallback ( candidate );
				isExact = false;
			}

			LocalizedText simplifiedPath = BuildPath ( candidate );
			String explanation = entry.Explanation;
			LocalizedText finalAlternate = alternate ?? LocalizedText.Empty;
			if (ruleResolution?.AlternateLabel is not null)
			{
				finalAlternate = ruleResolution.AlternateLabel;
			}
			
			LocalizedText finalOfficial = official ?? simplifiedPath;
			if (ruleResolution?.OfficialDescription is not null)
			{
				finalOfficial = ruleResolution.OfficialDescription;
			}

			// K15 layer ③: the 的-chain is built for EVERY option, not only for the ones we
			// cannot name — a named relation still deserves its legal-document reading.
			LocalizedText descriptiveChain = BuildFallback ( candidate );

			options.Add ( new KinshipResolutionOption ( label , isExact , simplifiedPath , finalOfficial , explanation , candidateKey , vector , finalAlternate , descriptiveChain ) );
		}

		List<KinshipResolutionOption> sortedOptions = options.OrderByDescending ( o => o.IsExactMatch ).ToList ();

		if ( sortedOptions.Count > 1 && sortedOptions [ 0 ].IsExactMatch )
		{
			// Non-exact options next to an exact resolution are just the unreduced raw-chain
			// echo; descriptive fallbacks only surface when nothing resolves exactly.
			sortedOptions.RemoveAll ( static option => !option.IsExactMatch );
		}

		if ( sortedOptions.Count == 0 )
		{
			// Chains that cancel back onto the origin (e.g. spouse→spouse) normalize to an empty
			// sequence, which every candidate pass rejects; surface the canonical self term instead.
			KinshipTerm selfTerm = terms_field [ String.Empty ];
			sortedOptions.Add ( new KinshipResolutionOption (
				selfTerm.Label ,
				true ,
				selfTerm.Label ,
				selfTerm.Label ,
				"返回自己" ,
				String.Empty ,
				RelationVectorBuilder.Build ( normalized , selfGender ) ,
				selfTerm.AlternateLabel ,
				selfTerm.Label ) ); // 書面稱述:回到自己時就是「自己」,不留空欄
		}

		// K15 layer ④: built from `tokens` (what the user actually entered), NOT from
		// `normalized` or any simplified candidate — the whole point is to expose the input
		// before the engine touched it, possessor-led so it reads as a sentence.
		return new KinshipResult ( sortedOptions , BuildPath ( tokens ) , BuildRawChain ( tokens ) );
	}

	private IReadOnlyList<KinshipToken> ResolveTokens ( IReadOnlyList<String> tokenIds )
	{
        List<KinshipToken> result = new( tokenIds.Count );
		foreach ( String id in tokenIds )
		{
			if ( tokenLookup_field.TryGetValue ( id , out KinshipToken? token ) )
			{
				result.Add ( token );
			}
		}
		return result;
	}

	private IReadOnlyList<KinshipToken> NormalizeTokens ( IReadOnlyList<KinshipToken> tokens )
	{
		if ( tokens.Count < 2 )
		{
			return tokens;
		}

		List<KinshipToken> normalized = new ( tokens.Count );

		foreach ( KinshipToken token in tokens )
		{
			if ( normalized.Count > 0 && token.Id.Equals ( "spouse" , StringComparison.Ordinal ) && normalized [ ^1 ].Id.Equals ( "spouse" , StringComparison.Ordinal ) )
			{
				normalized.RemoveAt ( normalized.Count - 1 );
				continue;
			}

			if ( normalized.Count > 0 && IsSiblingToken ( token ) )
			{
				if ( IsChildToken ( normalized [ ^1 ] ) )
				{
					normalized [ normalized.Count - 1 ] = ConvertSiblingToChild ( token );
					continue;
				}

				normalized.Add ( token );
				continue;
			}

			normalized.Add ( token );
		}

		CollapseAncestorSiblingLoops ( normalized );
		return normalized;
	}

	private static void CollapseAncestorSiblingLoops ( List<KinshipToken> tokens )
	{
		Boolean modified;
		do
		{
			modified = false;
			for ( Int32 index = 1 ; index < tokens.Count ; index++ )
			{
				if ( IsParentToken ( tokens [ index ] ) && IsSiblingToken ( tokens [ index - 1 ] ) )
				{
					if ( index == 1 )
					{
						continue;
					}
					tokens.RemoveAt ( index - 1 );
					modified = true;
					break;
				}
			}
		}
		while ( modified );
	}

	private static Boolean IsSiblingToken ( KinshipToken token )
		=> token.Id is "older-brother" or "younger-brother" or "older-sister" or "younger-sister";

	private static Boolean IsChildToken ( KinshipToken token )
		=> token.Id is "son" or "daughter" or "adoptive-son" or "adoptive-daughter";

	private KinshipToken ConvertSiblingToChild ( KinshipToken sibling )
	{
		String targetId = sibling.Id is "older-brother" or "younger-brother"
			? "son"
			: "daughter";
		return tokenLookup_field [ targetId ];
	}

	private static Boolean IsParentToken ( KinshipToken token )
		=> token.Id is "father" or "mother" or "adoptive-father" or "adoptive-mother";

	private static String BuildKey ( IReadOnlyList<KinshipToken> tokens )
	{
		if ( tokens.Count == 0 )
		{
			return String.Empty;
		}

		return TryGetTokenSpan ( tokens , out ReadOnlySpan<KinshipToken> span )
			? JoinTokenStrings ( span , static token => token.Symbol , "." )
			: String.Join ( "." , tokens.Select ( static t => t.Symbol ) );
	}

	private static LocalizedText BuildPath ( IReadOnlyList<KinshipToken> tokens )
	{
		if ( tokens.Count == 0 )
		{
            LocalizedText self = KinshipData.Terms [ String.Empty ].Label;
			return new LocalizedText ( self.ZhHans , self.ZhHant , self.English );
		}

		if ( TryGetTokenSpan ( tokens , out ReadOnlySpan<KinshipToken> span ) )
		{
			String zhHans = JoinTokenStrings ( span , static token => token.Label.ZhHans , HansArrowSeparator , SelfHansPrefix );
			String zhHant = JoinTokenStrings ( span , static token => token.Label.ZhHant , HansArrowSeparator , SelfHantPrefix );
			String en = JoinTokenStrings ( span , static token => token.Label.English , EnglishArrowSeparator , EnglishSelfPrefix );
			return new LocalizedText ( zhHans , zhHant , en );
		}

		String zhHansFallback = $"{SelfHansPrefix}{String.Join ( HansArrowSeparator , tokens.Select ( static t => t.Label.ZhHans ) )}";
		String zhHantFallback = $"{SelfHantPrefix}{String.Join ( HansArrowSeparator , tokens.Select ( static t => t.Label.ZhHant ) )}";
		String enFallback = $"{EnglishSelfPrefix}{String.Join ( EnglishArrowSeparator , tokens.Select ( static t => t.Label.English ) )}";
		return new LocalizedText ( zhHansFallback , zhHantFallback , enFallback );
	}

	private static LocalizedText BuildFallback ( IReadOnlyList<KinshipToken> tokens )
	{
		if ( tokens.Count == 0 )
		{
			return new LocalizedText ( "自己" , "自己" , "Self" );
		}

		if ( TryGetTokenSpan ( tokens , out ReadOnlySpan<KinshipToken> span ) )
		{
			String zhHans = JoinTokenStrings ( span , static token => token.Label.ZhHans , HansConnector );
			String zhHant = JoinTokenStrings ( span , static token => token.Label.ZhHant , HantConnector );
			String en = JoinTokenStrings ( span , static token => token.Label.English.ToLowerInvariant () , EnglishConnector );
			return new LocalizedText ( zhHans , zhHant , en );
		}

		String zhHansFallback = String.Join ( HansConnector , tokens.Select ( static t => t.Label.ZhHans ) );
		String zhHantFallback = String.Join ( HantConnector , tokens.Select ( static t => t.Label.ZhHant ) );
		String enFallback = String.Join ( EnglishConnector , tokens.Select ( static t => t.Label.English.ToLowerInvariant () ) );
		return new LocalizedText ( zhHansFallback , zhHantFallback , enFallback );
	}

	/// <summary>
	/// K15 layer ④ (原始輸出/校正): the entered chain, possessor-led and un-contracted —
	/// 我的母的兄的母 stays spelled out even though the engine resolves it to 外祖母. Reading
	/// this back is how a user checks the machine understood the input.
	/// </summary>
	private static LocalizedText BuildRawChain ( IReadOnlyList<KinshipToken> tokens )
	{
		if ( tokens.Count == 0 )
		{
			return new LocalizedText ( "我" , "我" , "me" );
		}

		LocalizedText chain = BuildFallback ( tokens );
		return new LocalizedText (
			$"我的{chain.ZhHans}" ,
			$"我的{chain.ZhHant}" ,
			$"my {chain.English}" );
	}

	private static String BuildExplanation ( LocalizedText simplifiedPath , IReadOnlyList<KinshipLoopInfo> loops )
	{
		Int32 totalLength = ShortestPathPrefix.Length + simplifiedPath.ZhHans.Length;

		if ( loops.Count > 0 )
		{
			totalLength += LoopSeparator.Length + LoopPrefix.Length;
			for ( Int32 index = 0 ; index < loops.Count ; index++ )
			{
				if ( index > 0 )
				{
					totalLength += LoopSeparator.Length;
				}

				totalLength += DescribeLoop ( loops [ index ] ).Length;
			}
		}

		Char[]? rented = null;
		Span<char> destination = totalLength <= InlineBufferLength
			? stackalloc char [ InlineBufferLength ]
			: ( rented = ArrayPool<char>.Shared.Rent ( totalLength ) );
		Span<char> slice = destination [ ..totalLength ];

		Int32 offset = 0;
		offset = CopyTo ( ShortestPathPrefix , slice , offset );
		offset = CopyTo ( simplifiedPath.ZhHans , slice , offset );

		if ( loops.Count > 0 )
		{
			offset = CopyTo ( LoopSeparator , slice , offset );
			offset = CopyTo ( LoopPrefix , slice , offset );

			for ( Int32 index = 0 ; index < loops.Count ; index++ )
			{
				if ( index > 0 )
				{
					offset = CopyTo ( LoopSeparator , slice , offset );
				}

				offset = CopyTo ( DescribeLoop ( loops [ index ] ) , slice , offset );
			}
		}

		String result = new String ( slice );
		if ( rented is not null )
		{
			ArrayPool<char>.Shared.Return ( rented );
		}

		return result;
	}

	private static String DescribeLoop ( KinshipLoopInfo loop )
		=> $$"""步驟{{loop.StartIndex}}-{{loop.EndIndex}} {{loop.Description}}""";

	private static Int32 CopyTo ( String value , Span<char> destination , Int32 offset )
	{
		ReadOnlySpan<char> source = value.AsSpan ();
		source.CopyTo ( destination [ offset.. ] );
		return offset + source.Length;
	}

	private static IReadOnlyList<KinshipToken> RemoveLoopSegments ( IReadOnlyList<KinshipToken> tokens , IReadOnlyList<KinshipLoopInfo> loops )
	{
		if ( loops.Count == 0 || tokens.Count == 0 )
		{
			return tokens;
		}

		Int32 length = tokens.Count;
		Span<Boolean> excluded = length <= InlineBufferLength
			? stackalloc Boolean [ length ]
			: new Boolean [ length ];
		foreach ( KinshipLoopInfo loop in loops )
		{
			Int32 start = Math.Max ( loop.StartIndex - 1 , 0 );
			Int32 end = Math.Min ( loop.EndIndex , length );

			// Removing ONE loop span is sound: it returns the walk to a node it already
			// visited. Removing the UNION of OVERLAPPING spans is not — the second loop's
			// span describes a revisit in the ORIGINAL walk, which no longer exists once the
			// first span is gone, and unioning them prunes past the true target
			// (M.F.YS.OB.YS must reduce to M.F.YS = the grand-aunt, not M.F = 外祖父).
			// So overlapping spans are skipped; the pruned chain is re-simplified anyway.
			Boolean overlaps = false;
			for ( Int32 index = start ; index < end ; index++ )
			{
				if ( excluded [ index ] )
				{
					overlaps = true;
					break;
				}
			}

			if ( overlaps )
			{
				continue;
			}

			for ( Int32 index = start ; index < end ; index++ )
			{
				excluded [ index ] = true;
			}
		}

		List<KinshipToken> reduced = new ( length );
		for ( Int32 index = 0 ; index < length ; index++ )
		{
			if ( !excluded [ index ] )
			{
				reduced.Add ( tokens [ index ] );
			}
		}

		return reduced.Count == 0 ? tokens : reduced;
	}

	private static String JoinTokenStrings (
		ReadOnlySpan<KinshipToken> span ,
		Func<KinshipToken , String> selector ,
		String separator ,
		String prefix = "" )
	{
		Int32 prefixLength = prefix.Length;
		Int32 separatorLength = separator.Length;
		Int32 totalLength = prefixLength;

		for ( Int32 index = 0 ; index < span.Length ; index++ )
		{
			totalLength += selector ( span [ index ] ).Length;
		}

		if ( span.Length > 1 )
		{
			totalLength += ( span.Length - 1 ) * separatorLength;
		}

		Char[]? rented = null;
		Span<char> buffer = totalLength <= InlineBufferLength
			? stackalloc char [ InlineBufferLength ]
			: ( rented = ArrayPool<char>.Shared.Rent ( totalLength ) );
		Span<char> destination = buffer [ ..totalLength ];

		Int32 offset = 0;
		if ( prefixLength > 0 )
		{
			prefix.AsSpan ().CopyTo ( destination [ offset.. ] );
			offset += prefixLength;
		}

		for ( Int32 index = 0 ; index < span.Length ; index++ )
		{
			String value = selector ( span [ index ] );
			value.AsSpan ().CopyTo ( destination [ offset.. ] );
			offset += value.Length;

			if ( index < span.Length - 1 )
			{
				separator.AsSpan ().CopyTo ( destination [ offset.. ] );
				offset += separatorLength;
			}
		}

		String result = new String ( destination );
		if ( rented is not null )
		{
			ArrayPool<char>.Shared.Return ( rented );
		}

		return result;
	}

	private static Boolean TryGetTokenSpan ( IReadOnlyList<KinshipToken> tokens , out ReadOnlySpan<KinshipToken> span )
	{
		switch ( tokens )
		{
			case List<KinshipToken> list:
				span = CollectionsMarshal.AsSpan ( list );
				return true;
			case KinshipToken[] array:
				span = array;
				return true;
			default:
				span = default;
				return false;
		}
	}

	private IEnumerable<CandidateSequence> BuildAncestorChildSiblingCollapse ( IReadOnlyList<KinshipToken> tokens )
	{
		// A child hop directly under an ancestor run (depth >= 2) names a sibling of the
		// next-lower ancestor: M,F,S ≡ M + brother (舅), F,F,D ≡ F + sister (姑). The chain
		// cannot carry the age order, so both order variants join the candidate pool and the
		// term dictionary / rules pick their usual forms.
		List<CandidateSequence> results = new ();

		Int32 run = 0;
		while ( run < tokens.Count && IsParentToken ( tokens [ run ] ) )
		{
			run++;
		}

		if ( run < 1 || run >= tokens.Count )
		{
			return results;
		}

		// run == 1 folds F.D -> sister only when the chain CONTINUES past the child with a
		// non-parent token (true collateral descent: F.D.S is the sister's son, 外甥 — the
		// net-generation reading 兒子 names the wrong person). Bare F.S / F.D keep their
		// unified sibling reading, and a parent AFTER the child is a loop-back shape the
		// graph simplifier already reduces (M.S.M.M -> M.M).
		if ( run == 1 && ( run + 1 >= tokens.Count || IsParentToken ( tokens [ run + 1 ] ) ) )
		{
			return results;
		}

		String? siblingBase = tokens [ run ].Id switch
		{
			"son" => "brother" ,
			"daughter" => "sister" ,
			_ => null
		};

		if ( siblingBase is null )
		{
			return results;
		}

		foreach ( String order in new [] { "older" , "younger" } )
		{
			if ( !tokenLookup_field.TryGetValue ( $"{order}-{siblingBase}" , out KinshipToken? siblingToken ) )
			{
				continue;
			}

			List<KinshipToken> sequence = new ( tokens.Count );
			for ( Int32 i = 0 ; i < run - 1 ; i++ )
			{
				sequence.Add ( tokens [ i ] );
			}

			sequence.Add ( siblingToken );
			for ( Int32 i = run + 1 ; i < tokens.Count ; i++ )
			{
				sequence.Add ( tokens [ i ] );
			}

			LocalizedText pathText = BuildPath ( sequence );
			// Priority ahead of the raw-chain resolution: the sibling reading (舅/伯/叔) is the
			// primary interpretation; the raw complex-lineal reading (e.g. 爸爸) stays as an option.
			results.Add ( new CandidateSequence ( sequence , $"祖系折算：{pathText.ZhHans}" , -150 , CountDirectionChanges ( sequence ) , sequence.Count ) );
		}

		return results;
	}

	private IEnumerable<CandidateSequence> BuildAncestorSiblingAlternatives ( IReadOnlyList<KinshipToken> tokens )
	{
		const Int32 maxDepth = 4;
		const Int32 maxResults = 12;

		List<CandidateSequence> results = new ();
		List<(Int32 Index , KinshipToken Token)> parentPositions = new ();

		for ( Int32 index = 0 ; index < tokens.Count ; index++ )
		{
			if ( IsParentToken ( tokens [ index ] ) )
			{
				parentPositions.Add ( ( index , tokens [ index ] ) );
			}
		}

		if ( parentPositions.Count < 2 )
		{
			return results;
		}

		List<(Int32 Index , KinshipToken Token)> buffer = new ();

		void Search ( Int32 startPos )
		{
			if ( buffer.Count >= 2 && results.Count < maxResults )
			{
				if ( FindSiblingAfter ( tokens , buffer [ ^1 ].Index ) is (Int32 Index , KinshipToken Token) sibling )
				{
					if ( HasInvalidGap ( tokens , buffer ) )
					{
						return;
					}

					if ( HasChildAfterMixedParents ( tokens , buffer , sibling.Index ) )
					{
						return;
					}

					List<KinshipToken> sequence = new ( buffer.Count + 1 );
					// Preserve every token BEFORE the folded parent run — dropping them
					// silently erased a leading spouse (SP.F6.OS… → F2.OS…), letting a
					// blood-side reading win over the affinal chain as an exact match.
					for ( Int32 prefix = 0 ; prefix < buffer [ 0 ].Index ; prefix++ )
					{
						sequence.Add ( tokens [ prefix ] );
					}

					foreach ( (Int32 _ , KinshipToken token) in buffer )
					{
						sequence.Add ( token );
					}
					sequence.Add ( sibling.Token );

					for ( Int32 i = sibling.Index + 1 ; i < tokens.Count ; i++ )
					{
						sequence.Add ( tokens [ i ] );
					}

					LocalizedText pathText = BuildPath ( sequence );
					String parentIndexes = String.Join ( "→" , buffer.Select ( item => $"{item.Token.Label.ZhHans}(#{item.Index + 1})" ) );
					String siblingInfo = $"{sibling.Token.Label.ZhHans}(#{sibling.Index + 1})";
					String explanation = $"祖系折算：{pathText.ZhHans}（來源索引 {parentIndexes} → {siblingInfo}）";

					Int32 fatherCount = buffer.Count ( item => item.Token.Id.Equals ( "father" , StringComparison.Ordinal ) );
					Int32 priority = fatherCount > 0 ? 1 : 0;
					Int32 directionChanges = CountDirectionChanges ( sequence );
					results.Add ( new CandidateSequence ( sequence , explanation , priority , directionChanges , sequence.Count ) );
				}
			}

			if ( buffer.Count == maxDepth || results.Count >= maxResults )
			{
				return;
			}

			IEnumerable<Int32> EnumeratePositions ( Int32 begin )
			{
				for ( Int32 pos = begin ; pos < parentPositions.Count ; pos++ )
				{
					if ( parentPositions [ pos ].Token.Id.Equals ( "mother" , StringComparison.Ordinal ) )
					{
						yield return pos;
					}
				}

				for ( Int32 pos = begin ; pos < parentPositions.Count ; pos++ )
				{
					if ( !parentPositions [ pos ].Token.Id.Equals ( "mother" , StringComparison.Ordinal ) )
					{
						yield return pos;
					}
				}
			}

			foreach ( Int32 pos in EnumeratePositions ( startPos ) )
			{
				buffer.Add ( parentPositions [ pos ] );
				Search ( pos + 1 );
				buffer.RemoveAt ( buffer.Count - 1 );

				if ( results.Count >= maxResults )
				{
					break;
				}
			}
		}

		Search ( 0 );
		return results;
	}

	private static (Int32 Index , KinshipToken Token)? FindSiblingAfter ( IReadOnlyList<KinshipToken> tokens , Int32 startIndex )
	{
		for ( Int32 index = startIndex + 1 ; index < tokens.Count ; index++ )
		{
			KinshipToken candidate = tokens [ index ];
			if ( IsSiblingToken ( candidate ) )
			{
				return ( index , candidate );
			}
		}

		return null;
	}

	private static Int32 CountDirectionChanges ( IReadOnlyList<KinshipToken> sequence )
	{
		Int32 changes = 0;
		Int32 lastDirection = 0;

		foreach ( KinshipToken token in sequence )
		{
			Int32 direction = token.Id switch
			{
				"father" or "mother" => 1 ,
				"son" or "daughter" => -1 ,
				_ => 0
			};

			if ( direction == 0 )
			{
				continue;
			}

			if ( lastDirection != 0 && direction != lastDirection )
			{
				changes++;
			}

			lastDirection = direction;
		}

		return changes;
	}

	private static Boolean HasInvalidGap ( IReadOnlyList<KinshipToken> tokens , IReadOnlyList<(Int32 Index , KinshipToken Token)> parents )
	{
		for ( Int32 i = 1 ; i < parents.Count ; i++ )
		{
			(Int32 Index , KinshipToken Token) previous = parents [ i - 1 ];
			(Int32 Index , KinshipToken Token) current = parents [ i ];

			for ( Int32 index = previous.Index + 1 ; index < current.Index ; index++ )
			{
				if ( !IsSiblingToken ( tokens [ index ] ) )
				{
					return true;
				}
			}
		}

		return false;
	}

	private static Boolean HasChildAfterMixedParents (
		IReadOnlyList<KinshipToken> tokens ,
		IReadOnlyList<(Int32 Index , KinshipToken Token)> parents ,
		Int32 siblingIndex )
	{
		Int32 start = parents [ ^1 ].Index + 1;
		for ( Int32 index = start ; index < siblingIndex && index < tokens.Count ; index++ )
		{
			if ( !IsSiblingToken ( tokens [ index ] ) )
			{
				return true;
			}
		}

		return false;
	}
}
