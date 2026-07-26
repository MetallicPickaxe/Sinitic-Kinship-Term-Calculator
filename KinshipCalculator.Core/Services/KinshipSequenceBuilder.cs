using System;
using System.Collections.Generic;
using KinshipCalculator.Core.Models;

namespace KinshipCalculator.Core.Services;

public static class KinshipSequenceBuilder
{
    public static IReadOnlyList<String> TranslateToken(KinshipToken token, KinshipOrigin origin)
    {
        if (!String.IsNullOrEmpty(token.Origin))
        {
            return [token.Id];
        }

        return token.Category switch
        {
            "parents" => BuildParentSequence(token, origin),
            "children" => BuildChildSequence(token, origin),
            _ => [token.Id]
        };
    }

    private static IReadOnlyList<String> BuildParentSequence(KinshipToken token, KinshipOrigin origin)
    {
        return origin switch
        {
            KinshipOrigin.Biological => [token.Id],
            KinshipOrigin.Adoptive => token.Id switch
            {
                "father" => ["adoptive-father"],
                "mother" => ["adoptive-mother"],
                _ => [token.Id]
            },
            KinshipOrigin.Step => token.Id switch
            {
                "father" => ["mother", "spouse"],
                "mother" => ["father", "spouse"],
                _ => [token.Id]
            },
            _ => [token.Id]
        };
    }

    private static IReadOnlyList<String> BuildChildSequence(KinshipToken token, KinshipOrigin origin)
    {
        return origin switch
        {
            KinshipOrigin.Biological => [token.Id],
            KinshipOrigin.Adoptive => token.Id switch
            {
                "son" => ["adoptive-son"],
                "daughter" => ["adoptive-daughter"],
                _ => [token.Id]
            },
            KinshipOrigin.Step => token.Id switch
            {
                "son" => ["spouse", "son"],
                "daughter" => ["spouse", "daughter"],
                _ => [token.Id]
            },
            _ => [token.Id]
        };
    }
}
