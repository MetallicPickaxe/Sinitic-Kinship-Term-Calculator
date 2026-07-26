using System;
using System.Collections.Generic;
using System.Linq;

using KinshipCalculator.Core.Models;

namespace KinshipCalculator.Core.Services;

internal sealed class KinshipPathSimplifier
{
	private readonly IReadOnlyDictionary<String , KinshipToken> tokenLookup_field;
	private readonly PersonGender selfGender_field;
	private readonly List<KinshipLoopInfo> loops_field = new ();
	private readonly Dictionary<KinshipGraphNode , Int32> firstVisit_field = new ();
	private readonly Dictionary<KinshipGraphNode , AncestorEntry> ancestorMap_field = new ();

	private readonly KinshipGraphNode root_field;

	private KinshipPathSimplifier ( PersonGender selfGender , IReadOnlyDictionary<String , KinshipToken> tokenLookup )
	{
		selfGender_field = selfGender;
		tokenLookup_field = tokenLookup;
		root_field = new KinshipGraphNode ( selfGender , generation: 0 );
		firstVisit_field [ root_field ] = 0;
	}

	public static SimplifiedPathResult Simplify (
		IReadOnlyList<KinshipToken> tokens ,
		PersonGender selfGender ,
		IReadOnlyDictionary<String , KinshipToken> tokenLookup ,
		Int32 maxCandidates = 4
	)
	{
		KinshipPathSimplifier simplifier = new KinshipPathSimplifier ( selfGender , tokenLookup );
		KinshipGraphNode target = simplifier.Traverse ( tokens );
		simplifier.BuildAncestorMap ();
		List<IReadOnlyList<KinshipToken>> candidates = new ();
		candidates.AddRange ( simplifier.FindCandidatePaths ( target , maxCandidates , GetDefaultEdgeCost ) );
		candidates.AddRange ( simplifier.FindCandidatePaths ( target , maxCandidates , GetSiblingPreferredEdgeCost ) );
		IReadOnlyList<KinshipToken>? structural = simplifier.BuildAncestorSiblingCandidate ( target );
		if ( structural is not null )
		{
			candidates.Insert ( 0 , structural );
		}

		if ( candidates.Count == 0 )
		{
			candidates.Add ( tokens );
		}

		return new SimplifiedPathResult ( candidates , simplifier.loops_field );
	}

	private KinshipGraphNode Traverse ( IReadOnlyList<KinshipToken> tokens )
	{
		KinshipGraphNode current = root_field;
		for ( Int32 index = 0 ; index < tokens.Count ; index++ )
		{
			current = ApplyToken ( current , tokens [ index ] );
			RecordVisit ( current , index + 1 );
		}
		return current;
	}

	private void RecordVisit ( KinshipGraphNode node , Int32 step )
	{
		if ( firstVisit_field.TryGetValue ( node , out Int32 first ) )
		{
			loops_field.Add ( new KinshipLoopInfo (
				first + 1 ,
				step ,
				DescribeNode ( node )
			) );
		}
		else
		{
			firstVisit_field [ node ] = step;
		}
	}

	private void BuildAncestorMap ()
	{
		if ( ancestorMap_field.Count > 0 )
		{
			return;
		}

		Stack<(KinshipGraphNode Node , List<KinshipGraphNode> Nodes , List<KinshipToken> Tokens)> stack = new ();
		stack.Push ( ( root_field , [ root_field ] , [] ) );

		while ( stack.Count > 0 )
		{
			(KinshipGraphNode node , List<KinshipGraphNode> nodes , List<KinshipToken> tokens) = stack.Pop ();
			if ( ancestorMap_field.ContainsKey ( node ) )
			{
				continue;
			}

			ancestorMap_field [ node ] = new AncestorEntry ( new List<KinshipGraphNode> ( nodes ) , new List<KinshipToken> ( tokens ) );

			if ( node.Father is not null )
			{
				List<KinshipGraphNode> nextNodes = [ ..nodes , node.Father ];
				List<KinshipToken> nextTokens = [ ..tokens , tokenLookup_field [ "father" ] ];
				stack.Push ( ( node.Father , nextNodes , nextTokens ) );
			}

			if ( node.Mother is not null )
			{
				List<KinshipGraphNode> nextNodes = [ ..nodes , node.Mother ];
				List<KinshipToken> nextTokens = [ ..tokens , tokenLookup_field [ "mother" ] ];
				stack.Push ( ( node.Mother , nextNodes , nextTokens ) );
			}
		}
	}

	private KinshipGraphNode ApplyToken ( KinshipGraphNode current , KinshipToken token )
		=> token.Id switch
		{
			"father" => EnsureFather ( current ) ,
			"adoptive-father" => EnsureFather ( current ) ,
			"mother" => EnsureMother ( current ) ,
			"adoptive-mother" => EnsureMother ( current ) ,
			"son" => EnsureChild ( current , PersonGender.Male ) ,
			"adoptive-son" => EnsureChild ( current , PersonGender.Male ) ,
			"daughter" => EnsureChild ( current , PersonGender.Female ) ,
			"adoptive-daughter" => EnsureChild ( current , PersonGender.Female ) ,
			"spouse" => EnsureSpouse ( current , PersonGender.Unknown ) ,
			"older-brother" => EnsureSibling ( current , PersonGender.Male , SiblingOrder.Older ) ,
			"younger-brother" => EnsureSibling ( current , PersonGender.Male , SiblingOrder.Younger ) ,
			"older-sister" => EnsureSibling ( current , PersonGender.Female , SiblingOrder.Older ) ,
			"younger-sister" => EnsureSibling ( current , PersonGender.Female , SiblingOrder.Younger ) ,
			_ => current
		};

	private KinshipGraphNode EnsureFather ( KinshipGraphNode node )
	{
		if ( node.Father is null )
		{
			KinshipGraphNode father = new ( PersonGender.Male , node.Generation + 1 );
			node.Father = father;
			father.Children.Add ( node );
			AddEdge ( node , father , "father" );
			AddEdge ( father , node , node.Gender == PersonGender.Female ? "daughter" : "son" );
			if ( node.Mother is not null )
			{
				LinkSpouses ( father , node.Mother );
			}
		}

		return node.Father!;
	}

	private KinshipGraphNode EnsureMother ( KinshipGraphNode node )
	{
		if ( node.Mother is null )
		{
			KinshipGraphNode mother = new ( PersonGender.Female , node.Generation + 1 );
			node.Mother = mother;
			mother.Children.Add ( node );
			AddEdge ( node , mother , "mother" );
			AddEdge ( mother , node , node.Gender == PersonGender.Female ? "daughter" : "son" );
			if ( node.Father is not null )
			{
				LinkSpouses ( node.Father , mother );
			}
		}

		return node.Mother!;
	}

	private KinshipGraphNode EnsureChild ( KinshipGraphNode node , PersonGender gender )
	{
		String key = gender == PersonGender.Male ? "son" : "daughter";
		if ( node.ChildrenByToken.TryGetValue ( key , out KinshipGraphNode? existingChild ) )
		{
			return existingChild;
		}

		KinshipGraphNode child = new ( gender , node.Generation - 1 );
		node.Children.Add ( child );
		node.ChildrenByToken [ key ] = child;

		KinshipGraphNode? spouse = node.Spouse;
		if ( spouse is null )
		{
			spouse = EnsureSpouse ( node , OppositeGender ( node.Gender ) );
		}

		if ( node.Gender == PersonGender.Male )
		{
			child.Father = node;
			child.Mother = spouse;
		}
		else if ( node.Gender == PersonGender.Female )
		{
			child.Mother = node;
			child.Father = spouse;
		}
		else
		{
			child.Father = node;
			child.Mother = spouse;
		}

		spouse.Children.Add ( child );
		spouse.ChildrenByToken [ key ] = child;

		AddEdge ( node , child , key );
		AddEdge ( child , node , node.Gender == PersonGender.Female ? "mother" : "father" );
		AddEdge ( spouse , child , key );
		AddEdge ( child , spouse , spouse.Gender == PersonGender.Female ? "mother" : "father" );

		return child;
	}

	private KinshipGraphNode EnsureSpouse ( KinshipGraphNode node , PersonGender expectedGender )
	{
		if ( node.Spouse is not null )
		{
			if ( expectedGender != PersonGender.Unknown && node.Spouse.Gender == PersonGender.Unknown )
			{
				node.Spouse.Gender = expectedGender;
			}
			return node.Spouse;
		}

		PersonGender gender = expectedGender switch
		{
			PersonGender.Unknown => OppositeGender ( node.Gender ) ,
			_ => expectedGender
		};

		KinshipGraphNode spouse = new ( gender , node.Generation );
		node.Spouse = spouse;
		spouse.Spouse = node;
		LinkSpouses ( node , spouse );
		LinkExistingChildrenToSpouse ( node , spouse );
		return spouse;
	}

	private KinshipGraphNode EnsureSibling ( KinshipGraphNode node , PersonGender gender , SiblingOrder order )
	{
		String tokenId = GetSiblingTokenId ( gender , order );
		if ( node.SiblingsByToken.TryGetValue ( tokenId , out KinshipGraphNode? existing ) )
		{
			return existing;
		}

		KinshipGraphNode father = EnsureFather ( node );
		KinshipGraphNode mother = EnsureMother ( node );

		KinshipGraphNode sibling = new ( gender , node.Generation );
		sibling.Father = father;
		sibling.Mother = mother;
		father.Children.Add ( sibling );
		mother.Children.Add ( sibling );

		String reciprocal = GetSiblingTokenId ( node.Gender , order == SiblingOrder.Older ? SiblingOrder.Younger : SiblingOrder.Older );
		node.SiblingsByToken [ tokenId ] = sibling;
		sibling.SiblingsByToken [ reciprocal ] = node;

		AddEdge ( node , sibling , tokenId );
		AddEdge ( sibling , node , reciprocal );

		return sibling;
	}

	private static PersonGender OppositeGender ( PersonGender value ) => value switch
	{
		PersonGender.Male => PersonGender.Female ,
		PersonGender.Female => PersonGender.Male ,
		_ => PersonGender.Unknown
	};

	private void LinkSpouses ( KinshipGraphNode first , KinshipGraphNode second )
	{
		first.Spouse = second;
		second.Spouse = first;

		AddEdge ( first , second , "spouse" );
		AddEdge ( second , first , "spouse" );
	}

	private void LinkExistingChildrenToSpouse ( KinshipGraphNode node , KinshipGraphNode spouse )
	{
		foreach ( KinshipGraphNode child in node.Children )
		{
			if ( node.Gender == PersonGender.Male )
			{
				child.Father ??= node;
				child.Mother ??= spouse;
			}
			else if ( node.Gender == PersonGender.Female )
			{
				child.Mother ??= node;
				child.Father ??= spouse;
			}

			if ( !spouse.Children.Contains ( child ) )
			{
				spouse.Children.Add ( child );
			}

			String key = child.Gender == PersonGender.Male ? "son" : "daughter";
			// Deliberately NOT registered in spouse.ChildrenByToken: a later child-token hop
			// from this spouse must mint a SIBLING, not memoize back onto this child. With
			// the registration, F.SP.S (father's spouse's son) folded onto the root itself
			// and the row fell to a descriptive reading; the sibling convention (parent's
			// child = 兄弟, never self — mumuy agrees: f,w,s names a brother) routes it to
			// the F.S graph candidate instead. Edges and parent links stay for pathing.
			AddEdge ( spouse , child , key );
			AddEdge ( child , spouse , spouse.Gender == PersonGender.Female ? "mother" : "father" );
		}
	}

	private void AddEdge ( KinshipGraphNode source , KinshipGraphNode target , String tokenId )
	{
		if ( !tokenLookup_field.TryGetValue ( tokenId , out KinshipToken? token ) )
		{
			return;
		}
		source.Edges.Add ( new RelationEdge ( target , token , ClassifyEdge ( tokenId ) ) );
	}

private static RelationEdgeKind ClassifyEdge ( String tokenId ) => tokenId switch
{
	"father" or "mother" => RelationEdgeKind.Parent ,
	"son" or "daughter" => RelationEdgeKind.Child ,
	"older-brother" or "younger-brother" or "older-sister" or "younger-sister" => RelationEdgeKind.Sibling ,
	"spouse" => RelationEdgeKind.Spouse ,
	_ => RelationEdgeKind.Parent
};

	private static String GetSiblingTokenId ( PersonGender gender , SiblingOrder order )
	{
		return (gender , order) switch
		{
			(PersonGender.Male , SiblingOrder.Older) => "older-brother" ,
			(PersonGender.Male , SiblingOrder.Younger) => "younger-brother" ,
			(PersonGender.Female , SiblingOrder.Older) => "older-sister" ,
			(PersonGender.Female , SiblingOrder.Younger) => "younger-sister" ,
			_ => "older-brother"
		};
	}

	private String DescribeNode ( KinshipGraphNode node )
	{
		if ( ReferenceEquals ( node , root_field ) )
		{
			return "返回自己";
		}

		if ( root_field.Spouse is not null && ReferenceEquals ( node , root_field.Spouse ) )
		{
			return "返回配偶";
		}

		return node.Gender switch
		{
			PersonGender.Male => "返回男性親屬" ,
			PersonGender.Female => "返回女性親屬" ,
			_ => "返回已訪問的親屬"
		};
	}

	private List<IReadOnlyList<KinshipToken>> FindCandidatePaths (
		KinshipGraphNode target ,
		Int32 maxCandidates ,
		Func<RelationEdgeKind , Int32> edgeCostProvider
	)
	{
		Dictionary<KinshipGraphNode , List<PathBackReference>> parents = new Dictionary<KinshipGraphNode , List<PathBackReference>> ();
		Dictionary<KinshipGraphNode , Int32> distance = new Dictionary<KinshipGraphNode , Int32> ();
		PriorityQueue<KinshipGraphNode , Int32> queue = new ();

		queue.Enqueue ( root_field , 0 );
		distance [ root_field ] = 0;

		Int32? targetDistance = null;

		while ( queue.TryDequeue ( out KinshipGraphNode? node , out Int32 currentDistance ) )
		{
			if ( distance.TryGetValue ( node , out Int32 known ) && currentDistance > known )
			{
				continue;
			}

			if ( targetDistance is not null && currentDistance > targetDistance )
			{
				break;
			}

			foreach ( RelationEdge edge in node.Edges )
			{
				Int32 edgeCost = edgeCostProvider ( edge.Kind );

				Int32 candidateCost = currentDistance + edgeCost;
				if ( !distance.TryGetValue ( edge.Target , out Int32 existing ) || candidateCost < existing )
				{
					distance [ edge.Target ] = candidateCost;
					parents [ edge.Target ] = new List<PathBackReference> { new PathBackReference ( node , edge.Token ) };
					queue.Enqueue ( edge.Target , candidateCost );
				}
				else if ( candidateCost == existing )
				{
					if ( parents.TryGetValue ( edge.Target , out List<PathBackReference>? list ) )
					{
						list.Add ( new PathBackReference ( node , edge.Token ) );
					}
				}
			}

			if ( node == target )
			{
				targetDistance ??= currentDistance;
			}
		}

		List<IReadOnlyList<KinshipToken>> results = new ();
		if ( !distance.ContainsKey ( target ) )
		{
			return results;
		}

		Stack<KinshipToken> buffer = new ();

		void BuildPaths ( KinshipGraphNode node )
		{
			if ( ReferenceEquals ( node , root_field ) )
			{
				results.Add ( buffer.ToArray () );
				return;
			}

			if ( !parents.TryGetValue ( node , out List<PathBackReference>? refs ) )
			{
				return;
			}

			foreach ( PathBackReference back in refs )
			{
				buffer.Push ( back.Token );
				BuildPaths ( back.Node );
				buffer.Pop ();

				if ( results.Count >= maxCandidates )
				{
					break;
				}
			}
		}

		BuildPaths ( target );
		return results;
	}

	private readonly record struct PathBackReference ( KinshipGraphNode Node , KinshipToken Token );

	private IReadOnlyList<KinshipToken>? BuildAncestorSiblingCandidate ( KinshipGraphNode target )
	{
		IReadOnlyList<KinshipToken>? direct = FindAncestorSiblingPath ( target );
		if ( direct is not null )
		{
			return direct;
		}

		foreach ( (KinshipGraphNode ancestor , AncestorEntry entry) in ancestorMap_field )
		{
			foreach ( RelationEdge edge in ancestor.Edges )
			{
				if ( edge.Kind == RelationEdgeKind.Sibling && ReferenceEquals ( edge.Target , target ) )
				{
					List<KinshipToken> tokens = new ( entry.Tokens.Count + 1 );
					tokens.AddRange ( entry.Tokens );
					tokens.Add ( edge.Token );
					return tokens;
				}
			}
		}

		return null;
	}

	private IReadOnlyList<KinshipToken>? FindAncestorSiblingPath ( KinshipGraphNode target )
	{
		Queue<AscendTraversalState> queue = new ();
		Dictionary<AscendTraversalState , PathBackReferenceState> parents = new ();
		HashSet<AscendTraversalState> visited = new ();

		AscendTraversalState start = new ( root_field , false , false );
		queue.Enqueue ( start );
		visited.Add ( start );

		while ( queue.Count > 0 )
		{
			AscendTraversalState state = queue.Dequeue ();
			if ( ReferenceEquals ( state.Node , target ) && state.TookSibling )
			{
				return ReconstructPath ( parents , state );
			}

			foreach ( RelationEdge edge in state.Node.Edges )
			{
				switch ( edge.Kind )
				{
					case RelationEdgeKind.Parent:
					{
						AscendTraversalState next = new ( edge.Target , true , state.TookSibling );
						if ( visited.Add ( next ) )
						{
							queue.Enqueue ( next );
							parents [ next ] = new PathBackReferenceState ( state , edge.Token );
						}
						break;
					}

					case RelationEdgeKind.Sibling when state.HasAscended && !state.TookSibling:
					{
						AscendTraversalState next = new ( edge.Target , state.HasAscended , true );
						if ( visited.Add ( next ) )
						{
							queue.Enqueue ( next );
							parents [ next ] = new PathBackReferenceState ( state , edge.Token );
						}
						break;
					}
				}
			}
		}

		return null;
	}

	private static IReadOnlyList<KinshipToken> ReconstructPath (
		IDictionary<AscendTraversalState , PathBackReferenceState> parents ,
		AscendTraversalState endState
	)
	{
		List<KinshipToken> output = new ();
		AscendTraversalState current = endState;

		while ( parents.TryGetValue ( current , out PathBackReferenceState back ) )
		{
			output.Add ( back.Token );
			current = back.State;
		}

		output.Reverse ();
		return output;
	}

	private enum SiblingOrder
	{
		Older ,
		Younger
	}

	private sealed class KinshipGraphNode
	{
		private static Int32 nextId_field = 1;

		public KinshipGraphNode ( PersonGender gender , Int32 generation )
		{
			Gender = gender;
			Generation = generation;
			Id = nextId_field++;
		}

		public Int32 Id { get; }
		public PersonGender Gender { get; set; }
		public Int32 Generation { get; set; }

		public KinshipGraphNode? Father { get; set; }
		public KinshipGraphNode? Mother { get; set; }
		public KinshipGraphNode? Spouse { get; set; }

		public List<KinshipGraphNode> Children { get; } = new ();
		public Dictionary<String , KinshipGraphNode> ChildrenByToken { get; } = new ( StringComparer.Ordinal );
		public Dictionary<String , KinshipGraphNode> SiblingsByToken { get; } = new ( StringComparer.Ordinal );
		public List<RelationEdge> Edges { get; } = new ();
	}

	private sealed record RelationEdge ( KinshipGraphNode Target , KinshipToken Token , RelationEdgeKind Kind );

	private sealed record AncestorEntry (
		IReadOnlyList<KinshipGraphNode> Nodes ,
		IReadOnlyList<KinshipToken> Tokens
	);

	private readonly record struct AscendTraversalState ( KinshipGraphNode Node , Boolean HasAscended , Boolean TookSibling );

	private readonly record struct PathBackReferenceState ( AscendTraversalState State , KinshipToken Token );

	private static Int32 GetDefaultEdgeCost ( RelationEdgeKind kind ) => kind switch
	{
		RelationEdgeKind.Parent => 1 ,
		RelationEdgeKind.Child => 1 ,
		RelationEdgeKind.Sibling => 2 ,
		RelationEdgeKind.Spouse => 3 ,
		_ => 1
	};

	private static Int32 GetSiblingPreferredEdgeCost ( RelationEdgeKind kind ) => kind switch
	{
		RelationEdgeKind.Parent => 3 ,
		RelationEdgeKind.Child => 3 ,
		RelationEdgeKind.Sibling => 1 ,
		RelationEdgeKind.Spouse => 1 ,
		_ => 2
	};
}

internal sealed record KinshipLoopInfo ( Int32 StartIndex , Int32 EndIndex , String Description );

internal sealed record SimplifiedPathResult (
	IReadOnlyList<IReadOnlyList<KinshipToken>> CandidatePaths ,
	IReadOnlyList<KinshipLoopInfo> Loops
);

internal enum RelationEdgeKind
{
	Parent ,
	Child ,
	Sibling ,
	Spouse
}
