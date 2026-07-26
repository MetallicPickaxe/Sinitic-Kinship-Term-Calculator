using System.Collections.Generic;

using KinshipCalculator.Core.Models;

namespace KinshipCalculator.Core.Services.Rules;

public sealed record KinshipRuleContext
{
	public KinshipRuleContext ( IReadOnlyList<KinshipToken> tokens , RelationVector vector , PersonGender selfGender )
	{
		Tokens = tokens;
		Vector = vector;
		SelfGender = selfGender;
		Segments = KinshipChainSegments.Analyze ( tokens );
	}

	public IReadOnlyList<KinshipToken> Tokens { get; }
	public RelationVector Vector { get; }
	public PersonGender SelfGender { get; }
	public KinshipChainSegments Segments { get; }
}
