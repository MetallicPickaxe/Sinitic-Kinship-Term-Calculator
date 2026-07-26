using System;
using System.Collections.Generic;
using System.Linq;

using KinshipCalculator.Core.Models;
using KinshipCalculator.Core.Models.Semantics;
using KinshipCalculator.Core.Services.Formatting;
using KinshipCalculator.Core.Services.Semantics;

namespace KinshipCalculator.Core.Services.Rules;

internal static class KinshipRuleEngine
{
	private static readonly IReadOnlyList<KinshipRule> Rules = BuildRules ();
    private static readonly KinshipNameFormatter _formatter = new KinshipNameFormatter();

	private static IReadOnlyList<KinshipRule> BuildRules ()
	{
		List<KinshipRule> rules = new List<KinshipRule>
		{
			new KinshipRule (
				"chain-shape-generative" ,
				priority: -50 , // Lossless canonical-shape path: takes covered families ahead of every legacy rule.
				context => context.Tokens.Count >= 2
					|| ( context.Tokens.Count == 1 && context.Tokens [ 0 ].Origin.Length > 0 ) ,
				context =>
				{
					KinshipChainShape? shape = KinshipChainShapeBuilder.Build ( context.Tokens , context.SelfGender );
					if ( shape is null )
					{
						return null;
					}

					ChainShapeName? name = ChainShapeTermFormatter.TryFormat ( shape );
					if ( name is null )
					{
						return null;
					}

					(LocalizedText label , LocalizedText? alternate) = NameSlotAssembler.BuildDailySlots (
						name.Formal.ZhHans , name.Formal.ZhHant , name.Formal.English ,
						name.Colloquial?.ZhHans ?? String.Empty , name.Colloquial?.ZhHant ?? String.Empty , name.Colloquial?.English ?? String.Empty );

					return new RuleResolution ( label , alternate , name.Official , true );
				}
			) ,
			new KinshipRule (
				"paternal-grandparent-spouse-maternal-grandparent" ,
				priority: -1 , // High priority to match this specific case first
				context => context.Tokens.Count == 4 && 
						   context.Tokens[0].Id == "father" &&
						   context.Tokens[1].Id == "spouse" &&
						   context.Tokens[2].Id == "mother" &&
						   context.Tokens[3].Id == "father",
				context => 
                {
                    if (KinshipSemanticAnalyzer.TryAnalyzePaternalGrandparentSpouseMaternalGrandparent(context, out KinshipSemanticInfo info))
                    {
                        return CreateResolution(info);
                    }
                    return null;
                }
			) ,
			new KinshipRule (
				"lineal-ancestor" ,
				priority: 0 ,
				context => context.Segments.ContainsOnlyParents && context.Tokens.Count >= 2 ,
				context => 
                {
                    if (KinshipSemanticAnalyzer.TryAnalyzeLinealAncestor(context, out KinshipSemanticInfo info))
                    {
                        return CreateResolution(info);
                    }
                    return null;
                }
			) ,
			new KinshipRule (
				"lineal-descendant" ,
				priority: 1 ,
				context => context.Segments.ContainsOnlyChildren && context.Tokens.Count >= 2 ,
				context => 
                {
                    if (KinshipSemanticAnalyzer.TryAnalyzeLinealDescendant(context, out KinshipSemanticInfo info))
                    {
                        return CreateResolution(info);
                    }
                    return null;
                }
			) ,
			new KinshipRule (
				"parent-spouse" ,
				priority: 2 ,
				context => context.Segments.Parents.Count >= 1
					&& context.Segments.Spouses.Count == 1
					&& context.Segments.TotalCount == context.Segments.Parents.Count + 1 ,
				context =>
				{
					if (KinshipSemanticAnalyzer.TryAnalyzeAncestorSpouse(context, out KinshipSemanticInfo info))
                    {
                        return CreateResolution(info);
                    }
					return null;
				}
			) ,
			new KinshipRule (
				"descendant-spouse" ,
				priority: 3 ,
				context => context.Segments.Descendants.Count >= 1
					&& context.Segments.Spouses.Count == 1
					&& context.Segments.TotalCount == context.Segments.Descendants.Count + 1 ,
				context =>
				{
					if (KinshipSemanticAnalyzer.TryAnalyzeDescendantSpouse(context, out KinshipSemanticInfo info))
                    {
                        return CreateResolution(info);
                    }
					return null;
				}
			) ,
            new KinshipRule (
                "step-ancestor",
                priority: 4,
                context => context.Tokens.Count >= 3 
                        && (context.Tokens[0].Id.Contains("father") || context.Tokens[0].Id.Contains("mother"))
                        && context.Tokens[1].Id == "spouse",
                context =>
                {
                    if (KinshipSemanticAnalyzer.TryAnalyzeStepAncestor(context, out KinshipSemanticInfo info))
                    {
                        return CreateResolution(info);
                    }
                    return null;
                }
            ),
			new KinshipRule (
				"affinal-family" ,
				priority: 5 ,
				context => context.Tokens.Count >= 2 && context.Tokens [ 0 ].Id.Equals ( "spouse" , StringComparison.Ordinal ) ,
				context =>
				{
					if (KinshipSemanticAnalyzer.TryAnalyzeAffinal(context, out KinshipSemanticInfo info))
                    {
                        return CreateResolution(info);
                    }
					return null;
				}
			) ,
			new KinshipRule (
				"sibling-or-inlaw" ,
				priority: 5 ,
				context => context.Segments.Parents.Count == 0
					&& context.Segments.Descendants.Count == 0
					&& context.Segments.Remaining.Count == 0
					&& context.Segments.Siblings.Count > 0 ,
				context =>
				{
					if (KinshipSemanticAnalyzer.TryAnalyzeSibling(context, out KinshipSemanticInfo info))
                    {
                        return CreateResolution(info);
                    }
					return null;
				}
			) ,
            new KinshipRule (
                "sibling-descendant",
                priority: 6,
                context => context.Segments.Siblings.Count > 0 
                        && context.Segments.Descendants.Count > 0 
                        && context.Segments.Parents.Count == 0,
                context =>
                {
                    if (KinshipSemanticAnalyzer.TryAnalyzeSiblingDescendant(context, out KinshipSemanticInfo info))
                    {
                        return CreateResolution(info);
                    }
                    return null;
                }
            ),
            new KinshipRule (
                "descendant-spouse-sibling",
                priority: 7,
                context => context.Tokens.Count >= 3 
                        && (context.Tokens[0].Id.Contains("son") || context.Tokens[0].Id.Contains("daughter"))
                        && context.Tokens[1].Id == "spouse"
                        && (context.Tokens[2].Id.Contains("brother") || context.Tokens[2].Id.Contains("sister")),
                context =>
                {
                    if (KinshipSemanticAnalyzer.TryAnalyzeDescendantSpouseSibling(context, out KinshipSemanticInfo info))
                    {
                        return CreateResolution(info);
                    }
                    return null;
                }
            ),
            new KinshipRule (
                "sibling-spouse-sibling",
                priority: 8,
                context => context.Tokens.Count >= 3 
                        && (context.Tokens[0].Id.Contains("brother") || context.Tokens[0].Id.Contains("sister"))
                        && context.Tokens[1].Id == "spouse"
                        && (context.Tokens[2].Id.Contains("brother") || context.Tokens[2].Id.Contains("sister")),
                context =>
                {
                    if (KinshipSemanticAnalyzer.TryAnalyzeSiblingSpouseSibling(context, out KinshipSemanticInfo info))
                    {
                        return CreateResolution(info);
                    }
                    return null;
                }
            ),
            new KinshipRule (
                "spouse-sibling-spouse",
                priority: 9,
                context => context.Tokens.Count >= 3 
                        && context.Tokens[0].Id == "spouse"
                        && (context.Tokens[1].Id.Contains("brother") || context.Tokens[1].Id.Contains("sister"))
                        && context.Tokens[2].Id == "spouse",
                context =>
                {
                    if (KinshipSemanticAnalyzer.TryAnalyzeSpouseSiblingSpouse(context, out KinshipSemanticInfo info))
                    {
                        return CreateResolution(info);
                    }
                    return null;
                }
            ),
            new KinshipRule (
                "co-parent-in-law",
                priority: 10,
                context => context.Tokens.Count >= 3 
                        && (context.Tokens[0].Id.Contains("son") || context.Tokens[0].Id.Contains("daughter"))
                        && context.Tokens[1].Id == "spouse",
                context =>
                {
                    if (KinshipSemanticAnalyzer.TryAnalyzeCoParentInLaw(context, out KinshipSemanticInfo info))
                    {
                        return CreateResolution(info);
                    }
                    return null;
                }
            ),
            new KinshipRule (
                "sibling-co-parent-in-law",
                priority: 10,
                context => context.Tokens.Count == 4 
                        && (context.Tokens[0].Id.Contains("brother") || context.Tokens[0].Id.Contains("sister")),
                context =>
                {
                    if (KinshipSemanticAnalyzer.TryAnalyzeSiblingCoParentInLaw(context, out KinshipSemanticInfo info))
                    {
                        return CreateResolution(info);
                    }
                    return null;
                }
            ),
            new KinshipRule (
                "collateral-spouse-parent",
                priority: 11,
                context => context.Tokens.Count >= 3 
                        && context.Tokens[^2].Id == "spouse"
                        && (context.Tokens[^1].Id.Contains("father") || context.Tokens[^1].Id.Contains("mother")),
                context =>
                {
                    if (KinshipSemanticAnalyzer.TryAnalyzeCollateralSpouseParent(context, out KinshipSemanticInfo info))
                    {
                        return CreateResolution(info);
                    }
                    return null;
                }
            ),
            new KinshipRule (
                "sibling-co-parent-spouse-identity",
                priority: 9,
                context => context.Tokens.Count == 3
                        && (context.Tokens[0].Id.Contains("brother") || context.Tokens[0].Id.Contains("sister"))
                        && (context.Tokens[1].Id is "son" or "daughter")
                        && (context.Tokens[2].Id is "father" or "mother"),
                context =>
                {
                    // [sibling][child][other parent] IS the sibling's spouse: 姐的女的父 = the
                    // sister's husband. Only the OPPOSITE-gender parent closes onto the spouse —
                    // the same-gender parent is the sibling again (OB.D.F = the brother himself),
                    // which the graph identity already handles. Without this rule the sibling
                    // fold of chains like F.D.D.F resolved nothing and the row fell to a
                    // descriptive reading (and before the complex-lineal scoping, to a wrong-
                    // person 嫂嫂 via the spouse leg).
                    Boolean siblingMale = context.Tokens[0].Id.Contains("brother");
                    Boolean parentMale = context.Tokens[2].Id == "father";
                    if (siblingMale == parentMale)
                    {
                        return null;
                    }

                    Boolean older = context.Tokens[0].Id.Contains("older");
                    LocalizedText label = siblingMale
                        ? (older
                            ? new LocalizedText("嫂子", "嫂嫂", "Elder brother's wife")
                            : new LocalizedText("弟媳", "弟媳", "Younger brother's wife"))
                        : (older
                            ? new LocalizedText("姐夫", "姐夫", "Elder sister's husband")
                            : new LocalizedText("妹夫", "妹夫", "Younger sister's husband"));
                    String officialZh = siblingMale ? "自己→兄弟→配偶" : "自己→姐妹→配偶";
                    String officialEn = siblingMale ? "Self → brother → spouse" : "Self → sister → spouse";
                    return new RuleResolution(label, null, new LocalizedText(officialZh, officialZh, officialEn), true);
                }
            ),
            // RETIRED: "collateral-spouse-sibling" (p11). Its whole matched family — elder
            // ascent + sibling + spouse + spouse's sibling — was named with a flat 姻 connector
            // and a uniform flavor (F.F.OB.SP.YB -> 叔姻祖伯/叔), disagreeing with the mumuy
            // composite face on every cell (叔祖眷舅祖父: 眷 for a male bridge, in-law flavor by
            // the spouse's side, tier by the bridge generation). AffinalWebComposer (p17)
            // implements that adjudicated law, so the family falls through to it.
            new KinshipRule (
                "complex-lineal",
                priority: 11,
                context => context.Segments.Parents.Count > 0, // Broad check, let Analyzer decide
                context => 
                {
                    if (KinshipSemanticAnalyzer.TryAnalyzeComplexLineal(context, out KinshipSemanticInfo info))
                    {
                        return CreateResolution(info);
                    }
                    return null;
                }
            ),
			new KinshipRule (
				"collateral" ,
				priority: 12 ,
				context => context.Segments.Parents.Count > 0 && context.Segments.Siblings.Count > 0 ,
				context =>
				{
					if (KinshipSemanticAnalyzer.TryAnalyzeCollateral(context, out KinshipSemanticInfo info))
                    {
                        return CreateResolution(info);
                    }
					return null;
				}
			) ,
            new KinshipRule (
                "spouse-collateral",
                priority: 13,
                context => context.Tokens.Count >= 3 && context.Tokens[0].Id == "spouse",
                context =>
                {
                    if (KinshipSemanticAnalyzer.TryAnalyzeSpouseCollateral(context, out KinshipSemanticInfo info))
                    {
                        return CreateResolution(info);
                    }
                    return null;
                }
            ),
            new KinshipRule (
                "sibling-spouse-sibling-child",
                priority: 14, // Before 'collateral-descendant-spouse-sibling-chain' (15)
                context => context.Tokens.Count >= 4 
                        && (context.Tokens[0].Id.Contains("brother") || context.Tokens[0].Id.Contains("sister"))
                        && context.Tokens[1].Id == "spouse"
                        && (context.Tokens[2].Id.Contains("brother") || context.Tokens[2].Id.Contains("sister")),
                context =>
                {
                    if (KinshipSemanticAnalyzer.TryAnalyzeSiblingSpouseSiblingChild(context, out KinshipSemanticInfo info))
                    {
                        return CreateResolution(info);
                    }
                    return null;
                }
            ),
            new KinshipRule (
                "sibling-spouse-parent",
                priority: 14,
                context => context.Tokens.Count >= 3 
                        && (context.Tokens[0].Id.Contains("brother") || context.Tokens[0].Id.Contains("sister"))
                        && context.Tokens[1].Id == "spouse",
                context =>
                {
                    if (KinshipSemanticAnalyzer.TryAnalyzeSiblingSpouseParent(context, out KinshipSemanticInfo info))
                    {
                        return CreateResolution(info);
                    }
                    return null;
                }
            ),
            new KinshipRule (
                "collateral-descendant-spouse-sibling-chain",
                priority: 15,
                context => context.Tokens.Count >= 4 
                        && (context.Tokens[0].Id.Contains("brother") || context.Tokens[0].Id.Contains("sister")),
                context =>
                {
                    if (KinshipSemanticAnalyzer.TryAnalyzeCollateralDescendantSpouseSiblingChain(context, out KinshipSemanticInfo info))
                    {
                        return CreateResolution(info);
                    }
                    return null;
                }
            ),
			new KinshipRule (
				"juan-composite-generative" ,
				priority: 15 , // Back-stop AFTER every specific affinal family: mumuy 眷-grammar (K11).
				context => context.Tokens.Count >= 3 ,
				context =>
				{
					(LocalizedText Label , LocalizedText Official)? name = JuanCompositeFormatter.TryFormat ( context.Tokens , context.SelfGender );
					if ( name is null )
					{
						return null;
					}

					return new RuleResolution ( name.Value.Label , null , name.Value.Official , true );
				}
			) ,
			new KinshipRule (
				"stacked-elder-composer" ,
				priority: 4 , // BEFORE the p5 sibling/affinal families and legacy collateral:
				              // both under-grade deep forks (族外甥孫女 for OB.S3.D.S.D,
				              // 堂舅 for M.F.F.OB.S.S); the composer's h≥2 gate keeps the
				              // shallow families theirs.
				context => context.Tokens.Count >= 4 ,
				context =>
				{
					(LocalizedText Label , LocalizedText Official)? name = StackedElderComposer.TryFormat ( context.Tokens , context.SelfGender );
					if ( name is null )
					{
						return null;
					}

					return new RuleResolution ( name.Value.Label , null , name.Value.Official , true );
				}
			) ,
			new KinshipRule (
				"spouse-only" ,
				priority: 16 ,
				context => context.Segments.ContainsOnlySpouses ,
				context => new RuleResolution ( RuleLocalization.BuildSpouseOnlyLabel ( context.Segments.Spouses.Count ) , null , null , true )
			) ,
			new KinshipRule (
				"affinal-web-generative" ,
				priority: 17 , // Last resort before descriptive: interior-SP 姻/眷 recursion
				               // (K11a sweep-4) — the juan regimes at p15 keep their cells.
				context => context.Tokens.Count >= 3 ,
				context =>
				{
					(LocalizedText Label , LocalizedText Official)? name = AffinalWebComposer.TryFormat ( context.Tokens , context.SelfGender );
					if ( name is null )
					{
						return null;
					}

					return new RuleResolution ( name.Value.Label , null , name.Value.Official , true );
				}
			)
		};

		return rules
			.OrderBy ( rule => rule.Priority )
			.ToList ();
	}

    private static RuleResolution CreateResolution(KinshipSemanticInfo info)
    {
        string formal = _formatter.Format(info, "zh-Hans", NamingContext.Formal);
        string formalHant = _formatter.Format(info, "zh-Hant", NamingContext.Formal);
        string formalEn = _formatter.Format(info, "en", NamingContext.Formal);
        
        string colloquial = _formatter.Format(info, "zh-Hans", NamingContext.Colloquial);
        string colloquialHant = _formatter.Format(info, "zh-Hant", NamingContext.Colloquial);
        string colloquialEn = _formatter.Format(info, "en", NamingContext.Colloquial);

        string official = _formatter.Format(info, "zh-Hans", NamingContext.Official);
        string officialHant = _formatter.Format(info, "zh-Hant", NamingContext.Official);
        string officialEn = _formatter.Format(info, "en", NamingContext.Official);

        (LocalizedText label, LocalizedText? alternate) = NameSlotAssembler.BuildSlotsFor(
            info.RelationType,
            formal, formalHant, formalEn,
            colloquial, colloquialHant, colloquialEn);
        var officialDesc = KinshipScriptConverter.Normalize(new LocalizedText(official, officialHant, officialEn));

        return new RuleResolution(label, alternate, officialDesc, true); // Positional argument
    }

	public static Boolean TryResolve ( KinshipRuleContext context , out RuleResolution resolution )
	{
		foreach ( KinshipRule rule in Rules )
		{
			if ( rule.TryMatch ( context , out resolution ) )
			{
				return true;
			}
		}

		resolution = RuleResolution.Empty;
		return false;
	}
}
