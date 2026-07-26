using System.Collections.Generic;

using KinshipCalculator.Core.Models;
using KinshipCalculator.Core.Services.Rules;

namespace KinshipCalculator.Core.Services;

internal static class RuleDrivenKinshipResolver
{
	public static bool TryResolve ( IReadOnlyList<KinshipToken> tokens , RelationVector vector , PersonGender selfGender , out RuleResolution resolution )
	{
		if ( tokens.Count == 0 )
		{
			resolution = default!;
			return false;
		}

		KinshipRuleContext context = new KinshipRuleContext ( tokens , vector , selfGender );
		return KinshipRuleEngine.TryResolve ( context , out resolution );
	}
}
