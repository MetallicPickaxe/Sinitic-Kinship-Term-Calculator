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

	/// <summary>
	/// THE EXACT-PRUNING POLICY, as a pure function.
	///
	/// Two rules, and both of them matter:
	///   • when ANY reading resolves exactly, the non-exact ones beside it are the unreduced
	///     raw-chain echo of the same person, and are dropped;
	///   • when NOTHING resolves exactly, the descriptive readings are KEPT — otherwise the user
	///     would be shown an empty result for every relation the engine cannot name.
	///
	/// Extracted from <c>Evaluate</c> because it could not be tested where it lived. The review
	/// of 2026-08-02 showed the previous test to be vacuous: it asserted only that the exact case
	/// ends up all-exact (true whether or not anything was pruned) and that the "nothing resolves"
	/// case returns at least one option — while the long chain it chose for that case actually
	/// resolves exactly, so the second branch was never reached. Both assertions stayed green
	/// under unconditional pruning, which is precisely the mistake the policy exists to prevent.
	///
	/// Behaviour is unchanged: same ordering, same predicate, same guard.
	/// </summary>
	internal static List<KinshipResolutionOption> ApplyExactPruningPolicy (
		IEnumerable<KinshipResolutionOption> options )
	{
		List<KinshipResolutionOption> sorted = options.OrderByDescending ( o => o.IsExactMatch ).ToList ();

		if ( sorted.Count > 1 && sorted [ 0 ].IsExactMatch )
		{
			sorted.RemoveAll ( static option => !option.IsExactMatch );
		}

		return sorted;
	}

	/// <summary>
	/// E2 backstop — one person must not be presented as several "possible relations".
	///
	/// The key is the pair the reader actually sees: the term and its 的-chain. Agreeing on BOTH
	/// means the same person by construction, because the 的-chain IS the relation spelled out —
	/// two identical spellings cannot denote two different people. The survivor keeps the shortest
	/// simplified path, per contract ("取最短寫法爲代表").
	///
	/// WHY NOT the relation vector, which the contract offers first: THE VECTOR CARRIES NO BIRTH
	/// ORDER. 父的姐 and 父的妹 build the identical vector (g1 p1 m0 c1 sp0 Paternal Female), and so
	/// do 伯父 and 叔父 — yet E1's own named acceptance requires BOTH 姑母 readings on screen, and
	/// E2 names 伯父/叔父 as people who must stay apart. Keying on the vector would delete exactly
	/// the readings the contract demands be kept. Measured rather than assumed: across all 1–4
	/// token paths there are 392 vector collisions and every one is a 兄/弟 or 姐/妹 pair.
	///
	/// Today this merges nothing — E1's fixpoint already closed both double images the sweep named
	/// (兄→配偶→子→母 was 哥哥眷配偶 + 嫂嫂, now one 嫂嫂; 姐→配偶→子→父 was 姐夫 × 2, now one). It
	/// stands as the backstop the contract asks for, so a future candidate rule cannot put the same
	/// person on screen twice, and the sweep test beside it is what proves the claim.
	/// </summary>
	internal static List<KinshipResolutionOption> GroupSamePersonReadings (
		IReadOnlyList<KinshipResolutionOption> options )
	{
		List<KinshipResolutionOption> grouped = new ( options.Count );
		if ( options.Count < 2 )
		{
			grouped.AddRange ( options );
			return grouped;
		}

		Dictionary<String , Int32> firstIndexByPerson = new ( StringComparer.Ordinal );

		foreach ( KinshipResolutionOption option in options )
		{
			String personKey = option.Label.ZhHant + "\0" + option.DescriptiveChain.ZhHant;

			if ( !firstIndexByPerson.TryGetValue ( personKey , out Int32 index ) )
			{
				firstIndexByPerson [ personKey ] = grouped.Count;
				grouped.Add ( option );
				continue;
			}

			if ( option.SimplifiedPath.ZhHant.Length < grouped [ index ].SimplifiedPath.ZhHant.Length )
			{
				grouped [ index ] = option;
			}
		}

		return grouped;
	}

	/// <summary>
	/// Removes identity round trips: stepping out to a child and back to that child's parent
	/// returns to where you started, PROVIDED the parent's sex matches the person you left from.
	///
	/// 我的子的父 is me when I am male. It is emphatically NOT me when I am female — a woman's
	/// child's father is her husband, or someone else entirely. That asymmetry is the whole reason
	/// this cannot be a blind two-token rewrite, and it is why the sweep that found these defects
	/// had to track sex along the chain to build them in the first place.
	///
	/// Only the plain child tokens qualify. 養子 does not: an adopted son's father is his adoptive
	/// father, which is a real relation and not a way of writing "me".
	///
	/// Repeats until nothing more cancels, because removing one pair can expose another —
	/// 子→子→父→父 collapses in two passes, not one.
	/// </summary>
	private static IReadOnlyList<KinshipToken> CancelIdentityDetours (
		IReadOnlyList<KinshipToken> sequence , PersonGender selfGender )
	{
		List<KinshipToken> working = new ( sequence );
		Boolean changed = true;

		while ( changed )
		{
			changed = false;
			Boolean male = selfGender != PersonGender.Female;

			for ( Int32 i = 0 ; i < working.Count - 1 ; i++ )
			{
				String child = working [ i ].Id;
				String parent = working [ i + 1 ].Id;

				Boolean isRoundTrip =
					( child is "son" or "daughter" )
					&& ( ( parent is "father" && male ) || ( parent is "mother" && !male ) );

				if ( isRoundTrip )
				{
					working.RemoveRange ( i , 2 );
					changed = true;
					break;
				}

				// Sex of the person standing at position i+1, for the next comparison.
				male = working [ i ].Id switch
				{
					"father" or "older-brother" or "younger-brother" or "son" or "adoptive-father" or "adoptive-son" => true,
					"mother" or "older-sister" or "younger-sister" or "daughter" or "adoptive-mother" or "adoptive-daughter" => false,
					"spouse" => !male,
					_ => male
				};
			}
		}

		return working.Count == sequence.Count ? sequence : working;
	}

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
		IReadOnlyList<KinshipToken> normalized = NormalizeTokens ( tokens , selfGender );
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

		// ================= FIXPOINT (E1) =================
		//
		// The rules used to run ONCE, on the original chain only. The simplifier could turn
		// 父→父→子→母→女 into 父的父的女 and hand that straight to the naming layer — but the
		// ancestor-sibling and ancestor-child folds, the ones that know 父的父的女 is 姑母, never
		// saw it, because they had already run and only ever ran on the input. Ask 父→父→女 on its
		// own and you get 姑母; bury the same relation inside a chain that doubles back and you got
		// five undigested 的-chains. The rules were fine. The wiring stopped after one pass.
		//
		// So every product now goes back in. A sequence is expanded — folds, resolver, simplifier,
		// loop pruning — and whatever comes out that is new gets expanded in turn, until nothing new
		// appears. That is a fixpoint, not a patch for one chain shape: the same loop closes all
		// 1,422 detour defects the sweep found, because they were all the same missing edge.
		//
		// BOUNDED, following EvaluateSubChain's precedent. Simplification shrinks, so rounds are
		// naturally few, but "naturally" is not a guarantee and a cycle in the rules would spin
		// forever. Both caps are ceilings that normal input never approaches; hitting one stops the
		// search and leaves whatever was found, which degrades to a descriptive reading rather than
		// throwing.
		const Int32 MaxExpansionRounds = 4;
		const Int32 MaxCandidateCount = 96;

		Queue<(IReadOnlyList<KinshipToken> Sequence, Int32 Round)> frontier = new ();
		HashSet<String> expandedKeys = new ( StringComparer.Ordinal );

		void Enqueue ( IReadOnlyList<KinshipToken> sequence , Int32 round )
		{
			if ( round > MaxExpansionRounds || sequence.Count == 0 )
			{
				return;
			}

			String key = BuildKey ( sequence );
			if ( key.Length > 0 && expandedKeys.Add ( key ) )
			{
				frontier.Enqueue ( ( sequence , round ) );
			}
		}

		Enqueue ( normalized , 0 );

		while ( frontier.Count > 0 && candidateEntries.Count < MaxCandidateCount )
		{
			( IReadOnlyList<KinshipToken> current, Int32 round ) = frontier.Dequeue ();
			Boolean isOriginal = round == 0;

			foreach ( CandidateSequence alt in BuildAncestorSiblingAlternatives ( current ) )
			{
				AddCandidate ( alt.Tokens , alt.Explanation , alt.Priority );
				Enqueue ( alt.Tokens , round + 1 );
			}

			foreach ( CandidateSequence alt in BuildAncestorChildSiblingCollapse ( current ) )
			{
				AddCandidate ( alt.Tokens , alt.Explanation , alt.Priority );
				Enqueue ( alt.Tokens , round + 1 );
			}

			// The smart analyzer, on THIS sequence rather than only on the input.
			if ( RuleDrivenKinshipResolver.TryResolve ( current , RelationVectorBuilder.Build ( current , selfGender ) , selfGender , out RuleResolution resolved )
				&& resolved.IsExactMatch )
			{
				AddCandidate ( current , "智能分析" , -100 , isExactMatch: true );
			}
			else if ( isOriginal )
			{
				// Only the question itself earns a descriptive fallback. A derived form that
				// resolves to nothing is not a reading of anything — offering it would put the
				// engine's scratch work on screen as though it were an answer.
				AddCandidate ( current , "智能分析" , 2 );
			}

			SimplifiedPathResult step = isOriginal
				? simplified
				: KinshipPathSimplifier.Simplify ( current , selfGender , tokenLookup_field );

			foreach ( IReadOnlyList<KinshipToken> candidate in step.CandidatePaths )
			{
				LocalizedText simplifiedPath = BuildPath ( candidate );
				AddCandidate ( candidate , BuildExplanation ( simplifiedPath , step.Loops ) , priority: 1 );
				Enqueue ( candidate , round + 1 );
			}

			IReadOnlyList<KinshipToken> pruned = RemoveLoopSegments ( current , step.Loops );
			if ( !ReferenceEquals ( pruned , current ) )
			{
				SimplifiedPathResult prunedResult = KinshipPathSimplifier.Simplify ( pruned , selfGender , tokenLookup_field );
				foreach ( IReadOnlyList<KinshipToken> path in prunedResult.CandidatePaths )
				{
					LocalizedText prunedPath = BuildPath ( path );
					String explanation = $"{BuildExplanation ( prunedPath , prunedResult.Loops )}；迴圈裁剪";
					AddCandidate ( path , explanation , priority: 0 );
					Enqueue ( path , round + 1 );
				}
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

			// K16 completion: the formatters query the lexicon only where they compose a term
			// themselves, so the relations that resolve BEFORE them — the atomic table above
			// (父親/母親/兒子/女兒/配偶…) and every descendant/collateral-descendant title —
			// reached the UI with an empty alternate slot no matter how many layers registered
			// variants for them. The layer stack is keyed by the standard form, which is exactly
			// what Label carries, so one lookup here serves every path that has not already
			// answered. It MERGES rather than fills a gap: a path that supplied its own set
			// (外孙子 → 外孙) would otherwise still shut the layers out.
			finalAlternate = MergeLayerVariants ( label , finalAlternate , selfGender );

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

		List<KinshipResolutionOption> sortedOptions = GroupSamePersonReadings ( ApplyExactPruningPolicy ( options ) );

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

	/// <summary>
	/// Normalisation run to a fixpoint (E1). Each rule can expose work for another — cancelling
	/// 子→父 can leave 配偶 next to 配偶, collapsing that can leave a sibling next to a parent — so
	/// one pass is not enough. Iterating here is what makes identity-detour invariance STRUCTURAL
	/// rather than a patch: 父→父→配偶→子→母 and 父→父→配偶 reduce to the same token list, so every
	/// rule downstream is handed the same question and cannot answer it two different ways.
	/// </summary>
	private IReadOnlyList<KinshipToken> NormalizeTokens ( IReadOnlyList<KinshipToken> tokens , PersonGender selfGender )
	{
		if ( tokens.Count < 2 )
		{
			return tokens;
		}

		// Bounded, per contract §三 E1. Every rule either shortens the list or rewrites a token in
		// place, so a run terminates on its own; the cap is the guard against a future rule pair
		// that rewrites in a cycle, not against normal input, which settles in one or two passes.
		const Int32 MaxNormalizationPasses = 8;

		IReadOnlyList<KinshipToken> current = tokens;
		for ( Int32 pass = 0 ; pass < MaxNormalizationPasses ; pass++ )
		{
			IReadOnlyList<KinshipToken> next = NormalizeOnce ( current , selfGender );
			if ( BuildKey ( next ).Equals ( BuildKey ( current ) , StringComparison.Ordinal ) )
			{
				return next;
			}

			current = next;
		}

		return current;
	}

	private IReadOnlyList<KinshipToken> NormalizeOnce ( IReadOnlyList<KinshipToken> tokens , PersonGender selfGender )
	{
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
		return CancelIdentityDetours ( normalized , selfGender );
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

	/// <summary>
	/// Appends the layer variants registered against a resolved standard form to whatever the
	/// formatter already produced, keeping the formatter's own words first and dropping repeats.
	/// Both spellings of the label are tried because the layer files are authored in whichever
	/// script the term is normally written in (父親 vs 婶母) and a computed title arrives here in
	/// only one of them. A descriptive 的-chain or a '|' set is not a standard form and is never
	/// looked up.
	/// </summary>
	private static LocalizedText MergeLayerVariants ( LocalizedText label , LocalizedText existing , PersonGender egoGender )
	{
		String hant = label.ZhHant;
		String hans = label.ZhHans;

		String set = String.Empty;
		if ( !String.IsNullOrWhiteSpace ( hans )
			&& !hans.Contains ( '的' , StringComparison.Ordinal )
			&& !hans.Contains ( '|' , StringComparison.Ordinal )
			&& !hans.StartsWith ( "自己" , StringComparison.Ordinal ) )
		{
			set = KinshipLexiconLayers.GetVariantSet ( hant , egoGender );
			if ( String.IsNullOrEmpty ( set ) && !String.Equals ( hans , hant , StringComparison.Ordinal ) )
			{
				set = KinshipLexiconLayers.GetVariantSet ( hans , egoGender );
			}
		}

		String mergedHans = String.IsNullOrEmpty ( set )
			? existing.ZhHans
			: AppendVariants ( existing.ZhHans , Formatting.KinshipScriptConverter.ToHans ( set ) , label.ZhHans );
		String mergedHant = String.IsNullOrEmpty ( set )
			? existing.ZhHant
			: AppendVariants ( existing.ZhHant , Formatting.KinshipScriptConverter.ToHant ( set ) , label.ZhHant );

		// THE ENGLISH SLICE IS LEFT ALONE, deliberately. An attempt to fill it by copying the
		// Traditional set in was reverted: an English interface that prints 爸爸 · 老爸 · 爹 is
		// not an English interface. The other names are Chinese words and there is nothing to
		// translate them into, so the honest place to deal with it is the empty-state notice,
		// which now distinguishes "nothing is recorded" from "what is recorded is Chinese"
		// instead of claiming the first when the second is true.
		//
		// The restructuring stays: the two early returns became one path, so the Simplified and
		// Traditional slices of a relation with NO layer variants still keep the names the
		// formatter composed for them.
		return new LocalizedText ( mergedHans , mergedHant , existing.English );
	}

	private static String AppendVariants ( String existing , String added , String label )
	{
		List<String> merged = new ();
		HashSet<String> seen = new ( StringComparer.Ordinal ) { label };
		foreach ( String part in existing.Split ( '|' , StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries )
			.Concat ( added.Split ( '|' , StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) ) )
		{
			if ( seen.Add ( part ) )
			{
				merged.Add ( part );
			}
		}

		return String.Join ( '|' , merged );
	}

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
