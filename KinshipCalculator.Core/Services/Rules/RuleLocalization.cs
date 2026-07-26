using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using KinshipCalculator.Core.Models;
using KinshipCalculator.Core.Models.Semantics;
using KinshipCalculator.Core.Services.Formatting;
using KinshipCalculator.Core.Services.Semantics;

namespace KinshipCalculator.Core.Services.Rules;

public static class RuleLocalization
{
    // Bridge method for testing new architecture
    public static (Boolean success, LocalizedText? formal, LocalizedText? colloquial, LocalizedText? official) TryAnalyzeAndFormat(KinshipRuleContext context)
    {
        var formatter = new KinshipNameFormatter();
        KinshipSemanticInfo info;

        if (KinshipSemanticAnalyzer.TryAnalyzeComplexLineal(context, out info))
        {
            return (true, 
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Formal), formatter.Format(info, "zh-Hant", NamingContext.Formal), formatter.Format(info, "en", NamingContext.Formal)),
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Colloquial), formatter.Format(info, "zh-Hant", NamingContext.Colloquial), formatter.Format(info, "en", NamingContext.Colloquial)),
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Official), formatter.Format(info, "zh-Hant", NamingContext.Official), formatter.Format(info, "en", NamingContext.Official)));
        }

        if (KinshipSemanticAnalyzer.TryAnalyzeCollateral(context, out info))
        {
            return (true, 
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Formal), formatter.Format(info, "zh-Hant", NamingContext.Formal), formatter.Format(info, "en", NamingContext.Formal)),
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Colloquial), formatter.Format(info, "zh-Hant", NamingContext.Colloquial), formatter.Format(info, "en", NamingContext.Colloquial)),
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Official), formatter.Format(info, "zh-Hant", NamingContext.Official), formatter.Format(info, "en", NamingContext.Official)));
        }

        if (KinshipSemanticAnalyzer.TryAnalyzeLinealAncestor(context, out info))
        {
            return (true, 
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Formal), formatter.Format(info, "zh-Hant", NamingContext.Formal), formatter.Format(info, "en", NamingContext.Formal)),
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Colloquial), formatter.Format(info, "zh-Hant", NamingContext.Colloquial), formatter.Format(info, "en", NamingContext.Colloquial)),
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Official), formatter.Format(info, "zh-Hant", NamingContext.Official), formatter.Format(info, "en", NamingContext.Official)));
        }

        if (KinshipSemanticAnalyzer.TryAnalyzeLinealDescendant(context, out info))
        {
            return (true, 
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Formal), formatter.Format(info, "zh-Hant", NamingContext.Formal), formatter.Format(info, "en", NamingContext.Formal)),
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Colloquial), formatter.Format(info, "zh-Hant", NamingContext.Colloquial), formatter.Format(info, "en", NamingContext.Colloquial)),
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Official), formatter.Format(info, "zh-Hant", NamingContext.Official), formatter.Format(info, "en", NamingContext.Official)));
        }

        if (KinshipSemanticAnalyzer.TryAnalyzeAncestorSpouse(context, out info))
        {
            return (true, 
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Formal), formatter.Format(info, "zh-Hant", NamingContext.Formal), formatter.Format(info, "en", NamingContext.Formal)),
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Colloquial), formatter.Format(info, "zh-Hant", NamingContext.Colloquial), formatter.Format(info, "en", NamingContext.Colloquial)),
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Official), formatter.Format(info, "zh-Hant", NamingContext.Official), formatter.Format(info, "en", NamingContext.Official)));
        }

        if (KinshipSemanticAnalyzer.TryAnalyzeDescendantSpouse(context, out info))
        {
            return (true, 
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Formal), formatter.Format(info, "zh-Hant", NamingContext.Formal), formatter.Format(info, "en", NamingContext.Formal)),
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Colloquial), formatter.Format(info, "zh-Hant", NamingContext.Colloquial), formatter.Format(info, "en", NamingContext.Colloquial)),
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Official), formatter.Format(info, "zh-Hant", NamingContext.Official), formatter.Format(info, "en", NamingContext.Official)));
        }

        if (KinshipSemanticAnalyzer.TryAnalyzeSibling(context, out info))
        {
            return (true, 
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Formal), formatter.Format(info, "zh-Hant", NamingContext.Formal), formatter.Format(info, "en", NamingContext.Formal)),
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Colloquial), formatter.Format(info, "zh-Hant", NamingContext.Colloquial), formatter.Format(info, "en", NamingContext.Colloquial)),
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Official), formatter.Format(info, "zh-Hant", NamingContext.Official), formatter.Format(info, "en", NamingContext.Official)));
        }

        if (KinshipSemanticAnalyzer.TryAnalyzeSiblingDescendant(context, out info))
        {
            return (true, 
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Formal), formatter.Format(info, "zh-Hant", NamingContext.Formal), formatter.Format(info, "en", NamingContext.Formal)),
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Colloquial), formatter.Format(info, "zh-Hant", NamingContext.Colloquial), formatter.Format(info, "en", NamingContext.Colloquial)),
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Official), formatter.Format(info, "zh-Hant", NamingContext.Official), formatter.Format(info, "en", NamingContext.Official)));
        }

        if (KinshipSemanticAnalyzer.TryAnalyzeAffinal(context, out info))
        {
            return (true, 
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Formal), formatter.Format(info, "zh-Hant", NamingContext.Formal), formatter.Format(info, "en", NamingContext.Formal)),
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Colloquial), formatter.Format(info, "zh-Hant", NamingContext.Colloquial), formatter.Format(info, "en", NamingContext.Colloquial)),
                    new LocalizedText(formatter.Format(info, "zh-Hans", NamingContext.Official), formatter.Format(info, "zh-Hant", NamingContext.Official), formatter.Format(info, "en", NamingContext.Official)));
        }

        return (false, null, null, null);
    }

    // Keep helper methods/dictionaries for backward compatibility if needed, or remove them if unused.
    // I will keep them to avoid breaking other things not yet migrated (if any).
    // But for brevity in this overwrite, I will only include the necessary parts.
    // Actually, KinshipRuleEngine relies on RuleLocalization helpers? 
    // `RuleLocalization.BuildSpouseOnlyLabel` is used in KinshipRuleEngine.
    // So I must preserve them.
    // I will use `read_file` to get the rest of the file content if I was appending, but here I am overwriting.
    // I must include `BuildSpouseOnlyLabel`.
    
    public static LocalizedText BuildSpouseOnlyLabel ( Int32 spouseCount )
	{
		if ( spouseCount % 2 == 1 )
		{
			return new LocalizedText ( "配偶" , "配偶" , "Spouse" );
		}

		return new LocalizedText ( "自己" , "自己" , "Self" );
	}
}