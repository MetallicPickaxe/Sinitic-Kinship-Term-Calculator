using System;

using KinshipCalculator.Core.Models;

namespace KinshipCalculator.Core.Services.Rules;

internal sealed class KinshipRule
{
	private readonly Func<KinshipRuleContext , Boolean> predicate_field;
	private readonly Func<KinshipRuleContext , RuleResolution?> resolver_field;

	public KinshipRule ( String id , Int32 priority , Func<KinshipRuleContext , Boolean> predicate , Func<KinshipRuleContext , RuleResolution?> resolver )
	{
		Id = id;
		Priority = priority;
		predicate_field = predicate;
		resolver_field = resolver;
	}

	public String Id { get; }
	public Int32 Priority { get; }

	public Boolean TryMatch ( KinshipRuleContext context , out RuleResolution resolution )
	{
		if ( !predicate_field ( context ) )
		{
			resolution = default!;
			return false;
		}

		RuleResolution? result = resolver_field ( context );
		if ( result is null )
		{
			resolution = default!;
			return false;
		}

		resolution = result;
		return true;
	}
}
