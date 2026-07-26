using System;
using System.Collections.Generic;
using System.Linq;
using KinshipCalculator.Core.Models;
using KinshipCalculator.Core.Models.Semantics;
using KinshipCalculator.Core.Services.Rules;

namespace KinshipCalculator.Core.Services.Semantics;

internal static class KinshipSemanticAnalyzer
{
    private static KinshipOrigin DetermineOrigin(IReadOnlyList<KinshipToken> tokens)
    {
        if (tokens.Any(t => t.Origin == "adoptive")) return KinshipOrigin.Adoptive;
        return KinshipOrigin.Biological;
    }

    private static Boolean IsSonToken(KinshipToken token)
        => token.Id == "son" || token.Id == "adoptive-son";

    private static String BuildDescendantPathSignature(IReadOnlyList<KinshipToken> descendants)
    {
        if (descendants.Count == 0)
        {
            return String.Empty;
        }

        List<String> steps = new(descendants.Count);
        foreach (KinshipToken descendant in descendants)
        {
            steps.Add(IsSonToken(descendant) ? "S" : "D");
        }

        return String.Join(",", steps);
    }

    public static Boolean TryAnalyzeLinealAncestor(KinshipRuleContext context, out KinshipSemanticInfo info)
    {
        info = KinshipSemanticInfo.Empty;
        
        KinshipChainSegments segments = context.Segments;
        if (!segments.ContainsOnlyParents || context.Tokens.Count < 1)
        {
            return false;
        }

        IReadOnlyList<KinshipToken> parents = segments.Parents;
        Int32 depth = parents.Count;
        KinshipToken lastToken = parents[^1];
        
        KinshipRelationType type = depth switch
        {
            1 => KinshipRelationType.Parent,
            2 => KinshipRelationType.GrandParent,
            3 => KinshipRelationType.GreatGrandParent,
            _ => KinshipRelationType.Ancestor
        };

        PersonGender gender = (lastToken.Id == "father" || lastToken.Id == "adoptive-father") 
            ? PersonGender.Male 
            : PersonGender.Female;

        Boolean isPaternal = parents[0].Id == "father" || parents[0].Id == "adoptive-father";

        info = new KinshipSemanticInfo
        {
            RelationType = type,
            Gender = gender,
            IsPaternal = isPaternal,
            IsTopLevelPaternal = isPaternal, // Same for direct lineal
            GenerationChange = depth,
            IsSpouse = false,
            Origin = DetermineOrigin(context.Tokens)
        };

        return true;
    }

    public static Boolean TryAnalyzeLinealDescendant(KinshipRuleContext context, out KinshipSemanticInfo info)
    {
        info = KinshipSemanticInfo.Empty;
        
        KinshipChainSegments segments = context.Segments;
        if (!segments.ContainsOnlyChildren || context.Tokens.Count < 1)
        {
            return false;
        }

        IReadOnlyList<KinshipToken> children = segments.Descendants;
        Int32 depth = children.Count;
        KinshipToken lastToken = children[^1];

        KinshipRelationType type = depth switch
        {
            1 => KinshipRelationType.Child,
            2 => KinshipRelationType.GrandChild,
            3 => KinshipRelationType.GreatGrandChild,
            _ => KinshipRelationType.Descendant
        };

        PersonGender gender = IsSonToken(lastToken)
            ? PersonGender.Male
            : PersonGender.Female;

        Boolean isPaternal = IsSonToken(children[0]);
        String descendantPathSignature = BuildDescendantPathSignature(children);

        info = new KinshipSemanticInfo
        {
            RelationType = type,
            Gender = gender,
            IsPaternal = isPaternal,
            IsTopLevelPaternal = isPaternal,
            GenerationChange = -depth,
            IsSpouse = false,
            Origin = DetermineOrigin(context.Tokens),
            DescendantPathSignature = descendantPathSignature,
            InitialDescendantCount = depth
        };

        return true;
    }

    public static Boolean TryAnalyzeAncestorSpouse(KinshipRuleContext context, out KinshipSemanticInfo info)
    {
        info = KinshipSemanticInfo.Empty;
        
        KinshipChainSegments segments = context.Segments;
        if (segments.Parents.Count < 1 || segments.Spouses.Count != 1 || segments.TotalCount != segments.Parents.Count + 1)
        {
            return false;
        }

        KinshipToken lastParent = segments.Parents[^1];
        Boolean isParentMale = lastParent.Id == "father" || lastParent.Id == "adoptive-father";
        
        Int32 depth = segments.Parents.Count;
        KinshipRelationType type = depth switch
        {
            1 => KinshipRelationType.Parent,
            2 => KinshipRelationType.GrandParent,
            3 => KinshipRelationType.GreatGrandParent,
            _ => KinshipRelationType.Ancestor
        };

        PersonGender gender = isParentMale ? PersonGender.Female : PersonGender.Male;
        Boolean isPaternal = segments.Parents[0].Id == "father" || segments.Parents[0].Id == "adoptive-father";

        info = new KinshipSemanticInfo
        {
            RelationType = type,
            Gender = gender,
            IsPaternal = isPaternal,
            IsTopLevelPaternal = isPaternal,
            GenerationChange = depth,
            IsSpouse = true
        };

        return true;
    }

    public static Boolean TryAnalyzeDescendantSpouse(KinshipRuleContext context, out KinshipSemanticInfo info)
    {
        info = KinshipSemanticInfo.Empty;
        
        KinshipChainSegments segments = context.Segments;
        if (segments.Descendants.Count < 1 || segments.Spouses.Count != 1 || segments.TotalCount != segments.Descendants.Count + 1)
        {
            return false;
        }

        KinshipToken lastChild = segments.Descendants[^1];
        Boolean isSon = IsSonToken(lastChild);
        
        Int32 depth = segments.Descendants.Count;
        KinshipRelationType type = depth switch
        {
            1 => KinshipRelationType.Child,
            2 => KinshipRelationType.GrandChild,
            3 => KinshipRelationType.GreatGrandChild,
            _ => KinshipRelationType.Descendant
        };

        PersonGender gender = isSon ? PersonGender.Female : PersonGender.Male;
        Boolean isPaternal = IsSonToken(segments.Descendants[0]);
        String descendantPathSignature = BuildDescendantPathSignature(segments.Descendants);

        info = new KinshipSemanticInfo
        {
            RelationType = type,
            Gender = gender,
            IsPaternal = isPaternal,
            IsTopLevelPaternal = isPaternal,
            GenerationChange = -depth,
            IsSpouse = true,
            Origin = DetermineOrigin(context.Tokens),
            DescendantPathSignature = descendantPathSignature,
            InitialDescendantCount = depth
        };

        return true;
    }

    public static Boolean TryAnalyzeComplexLineal(KinshipRuleContext context, out KinshipSemanticInfo info)
    {
        info = KinshipSemanticInfo.Empty;

        if (context.Tokens.Count == 0) return false;

        int parentCount = 0;
        int childCount = 0;
        bool childBeforeParent = false;
        int netGeneration = 0;
        KinshipToken lastToken = context.Tokens[0];

        foreach (var token in context.Tokens)
        {
            string id = token.Id;

            if (id == "father" || id == "mother" || id == "adoptive-father" || id == "adoptive-mother")
            {
                parentCount++;
                netGeneration++;
                if (childCount > 0)
                {
                    childBeforeParent = true;
                }
            }
            else if (id == "son" || id == "daughter" || id == "adoptive-son" || id == "adoptive-daughter")
            {
                childCount++;
                netGeneration--;
            }
            else if (id.Contains("brother") || id.Contains("sister"))
            {
                return false; // Explicit sibling tokens make it collateral, not lineal.
            }

            lastToken = token;
        }

        // The net-generation reduction is only sound for the single up-down cell — F.S is a
        // brother (parent's son, generation 0) — and ONLY as the exact two-token chain. Any
        // longer shape names the WRONG person: F.S.S is the brother's son (侄), not 兒子;
        // M.S.M.M is the maternal grandmother (外祖母), not 祖母; S.F is self; and any rider
        // token re-opens the hole — a trailing spouse made F.D.SP the sibling's HUSBAND with
        // an Unknown gender that rendered 嫂嫂 (the residual gauge family), an interior
        // spouse (F.SP.S) rode the same path. Longer shapes belong to the structure-
        // preserving candidates (ancestor-sibling folds, the graph simplifier, the canonical
        // chain shape), so this analyzer abstains on all of them.
        if (childBeforeParent || parentCount != 1 || childCount != 1 || context.Tokens.Count != 2)
        {
            return false;
        }
        KinshipRelationType type;
        if (netGeneration > 0)
        {
            type = netGeneration switch
            {
                1 => KinshipRelationType.Parent,
                2 => KinshipRelationType.GrandParent,
                3 => KinshipRelationType.GreatGrandParent,
                _ => KinshipRelationType.Ancestor
            };
        }
        else if (netGeneration < 0)
        {
            int depth = Math.Abs(netGeneration);
            type = depth switch
            {
                1 => KinshipRelationType.Child,
                2 => KinshipRelationType.GrandChild,
                3 => KinshipRelationType.GreatGrandChild,
                _ => KinshipRelationType.Descendant
            };
        }
        else
        {
            type = KinshipRelationType.Sibling;
        }

        PersonGender gender = PersonGender.Unknown;
        string lastId = lastToken.Id;
        if (lastId.Contains("father") || lastId.Contains("son") || lastId.Contains("brother") || lastId == "husband") 
            gender = PersonGender.Male;
        else if (lastId.Contains("mother") || lastId.Contains("daughter") || lastId.Contains("sister") || lastId == "wife") 
            gender = PersonGender.Female;

        info = new KinshipSemanticInfo
        {
            RelationType = type,
            Gender = gender,
            IsPaternal = true, 
            IsTopLevelPaternal = true, // Assume paternal for simplifications
            AgeOrder = SiblingOrder.Unknown, 
            GenerationChange = netGeneration,
            IsSpouse = lastId == "spouse"
        };

        return true;
    }

    public static Boolean TryAnalyzeSibling(KinshipRuleContext context, out KinshipSemanticInfo info)
    {
        info = KinshipSemanticInfo.Empty;
        
        KinshipChainSegments segments = context.Segments;
        if (segments.Parents.Count != 0 || segments.Descendants.Count != 0)
        {
            return false;
        }

        if (segments.Siblings.Count == 0)
        {
            return false;
        }

        KinshipToken sibling = segments.Siblings[^1];
        Boolean isMale = sibling.Id == "older-brother" || sibling.Id == "younger-brother";
        Boolean isOlder = sibling.Id == "older-brother" || sibling.Id == "older-sister";
        
        Boolean isSpouse = segments.Spouses.Count > 0 && segments.Spouses.Count % 2 != 0;
        PersonGender finalGender = isSpouse ? (isMale ? PersonGender.Female : PersonGender.Male) : (isMale ? PersonGender.Male : PersonGender.Female);

        info = new KinshipSemanticInfo
        {
            RelationType = KinshipRelationType.Sibling,
            Gender = finalGender,
            IsPaternal = true,
            IsTopLevelPaternal = true,
            AgeOrder = isOlder ? SiblingOrder.Older : SiblingOrder.Younger,
            IsSpouse = isSpouse,
            GenerationChange = 0
        };

        return true;
    }

    public static Boolean TryAnalyzeSiblingDescendant(KinshipRuleContext context, out KinshipSemanticInfo info)
    {
        info = KinshipSemanticInfo.Empty;
        
        KinshipChainSegments segments = context.Segments;
        if (segments.Parents.Count != 0 || segments.Siblings.Count == 0 || segments.Descendants.Count == 0)
        {
            return false;
        }

        if (segments.Remaining.Count > 0)
        {
            return false;
        }

        KinshipToken sibling = segments.Siblings[^1];
        Boolean isSiblingMale = sibling.Id == "older-brother" || sibling.Id == "younger-brother";
        
        KinshipToken lastDescendant = segments.Descendants[^1];
        Boolean isMale = lastDescendant.Id == "son" || lastDescendant.Id == "adoptive-son";
        
        Int32 depth = segments.Descendants.Count;

        info = new KinshipSemanticInfo
        {
            RelationType = KinshipRelationType.SiblingDescendant,
            Gender = isMale ? PersonGender.Male : PersonGender.Female,
            IsPaternal = isSiblingMale,
            IsTopLevelPaternal = isSiblingMale,
            GenerationChange = -depth,
            IsSpouse = false
        };

        if (segments.Spouses.Count > 0 && segments.Spouses.Count % 2 != 0)
        {
            info = info with 
            { 
                IsSpouse = true,
                Gender = isMale ? PersonGender.Female : PersonGender.Male 
            };
        }

        return true;
    }

    public static Boolean TryAnalyzeAffinal(KinshipRuleContext context, out KinshipSemanticInfo info)
    {
        info = KinshipSemanticInfo.Empty;
        
        if (context.Tokens.Count < 2 || context.Tokens[0].Id != "spouse")
        {
            return false;
        }

        var remainingTokens = context.Tokens.Skip(1).ToList();
        
        // Pattern 1: Spouse -> Parents (in-laws)
        bool allParents = true;
        if (remainingTokens.Count == 0) allParents = false;
        
        foreach (var token in remainingTokens)
        {
            if (token.Id != "father" && token.Id != "mother" && 
                token.Id != "adoptive-father" && token.Id != "adoptive-mother")
            {
                allParents = false;
                break;
            }
        }

        if (allParents)
        {
            KinshipToken lastToken = remainingTokens[^1];
            Boolean isMale = lastToken.Id == "father" || lastToken.Id == "adoptive-father";
            int depth = remainingTokens.Count;

            info = new KinshipSemanticInfo
            {
                RelationType = KinshipRelationType.SpouseParent,
                Gender = isMale ? PersonGender.Male : PersonGender.Female,
                IsPaternal = (context.SelfGender == PersonGender.Female),
                IsTopLevelPaternal = (context.SelfGender == PersonGender.Female),
                GenerationChange = depth,
                IsSpouse = false
            };
            return true;
        }

        // Pattern 2: Spouse -> Sibling (and Spouse)
        if (remainingTokens.Count >= 1 && 
            (remainingTokens[0].Id.Contains("brother") || remainingTokens[0].Id.Contains("sister")))
        {
            bool isChildNext = false;
            if (remainingTokens.Count >= 2 && (remainingTokens[1].Id.Contains("son") || remainingTokens[1].Id.Contains("daughter")))
            {
                isChildNext = true;
            }

            if (!isChildNext)
            {
                Boolean isMaleSibling = remainingTokens[0].Id.Contains("brother");
                Boolean isOlder = remainingTokens[0].Id.Contains("older");
                
                Boolean isSpouseOfSibling = false;
                if (remainingTokens.Count == 2 && remainingTokens[1].Id == "spouse")
                {
                    isSpouseOfSibling = true;
                }
                else if (remainingTokens.Count > 1)
                {
                    return false; 
                }

                PersonGender finalGender = isSpouseOfSibling 
                    ? (isMaleSibling ? PersonGender.Female : PersonGender.Male) 
                    : (isMaleSibling ? PersonGender.Male : PersonGender.Female);

                info = new KinshipSemanticInfo
                {
                    RelationType = KinshipRelationType.SpouseSibling,
                    Gender = finalGender,
                    IsPaternal = (context.SelfGender == PersonGender.Female),
                    IsTopLevelPaternal = (context.SelfGender == PersonGender.Female),
                    AgeOrder = isOlder ? SiblingOrder.Older : SiblingOrder.Younger,
                    GenerationChange = 0,
                    IsSpouse = isSpouseOfSibling
                };
                return true;
            }
        }

        // Pattern 3: Spouse -> Sibling -> Descendants
        if (remainingTokens.Count >= 2 && 
            (remainingTokens[0].Id.Contains("brother") || remainingTokens[0].Id.Contains("sister")))
        {
            bool allChildren = true;
            for(int i = 1; i < remainingTokens.Count; i++)
            {
                if (!remainingTokens[i].Id.Contains("son") && !remainingTokens[i].Id.Contains("daughter"))
                {
                    allChildren = false;
                    break;
                }
            }

            if (allChildren)
            {
                Boolean isSiblingMale = remainingTokens[0].Id.Contains("brother");
                Boolean isSiblingOlder = remainingTokens[0].Id.Contains("older");
                Boolean isChildMale = remainingTokens[^1].Id.Contains("son");
                
                int descendantDepth = remainingTokens.Count - 1; 

                info = new KinshipSemanticInfo
                {
                    RelationType = KinshipRelationType.SpouseSiblingChild,
                    Gender = isChildMale ? PersonGender.Male : PersonGender.Female,
                    IsPaternal = (context.SelfGender == PersonGender.Female),
                    IsTopLevelPaternal = (context.SelfGender == PersonGender.Female),
                    AgeOrder = isSiblingMale ? SiblingOrder.Older : SiblingOrder.Younger,
                    GenerationChange = -descendantDepth,
                    IsSpouse = false
                };
                return true;
            }
        }

        return false;
    }

    public static Boolean TryAnalyzeCoParentInLaw(KinshipRuleContext context, out KinshipSemanticInfo info)
    {
        info = KinshipSemanticInfo.Empty;
        
        if (context.Tokens.Count != 3) return false;
        
        var t0 = context.Tokens[0].Id;
        var t1 = context.Tokens[1].Id;
        var t2 = context.Tokens[2].Id;

        if ((t0 == "son" || t0 == "daughter" || t0 == "adoptive-son" || t0 == "adoptive-daughter") &&
            t1 == "spouse" &&
            (t2 == "father" || t2 == "mother" || t2 == "adoptive-father" || t2 == "adoptive-mother"))
        {
            Boolean isMale = t2.Contains("father");
            info = new KinshipSemanticInfo
            {
                RelationType = KinshipRelationType.CoParentInLaw,
                Gender = isMale ? PersonGender.Male : PersonGender.Female,
                GenerationChange = 0,
                IsPaternal = false, 
                IsTopLevelPaternal = false,
                IsSpouse = false
            };
            return true;
        }
        return false;
    }

    public static Boolean TryAnalyzeDescendantSpouseSibling(KinshipRuleContext context, out KinshipSemanticInfo info)
    {
        info = KinshipSemanticInfo.Empty;
        if (context.Tokens.Count < 3) return false;

        int index = 0;
        while (index < context.Tokens.Count && 
               (context.Tokens[index].Id.Contains("son") || context.Tokens[index].Id.Contains("daughter") || 
                context.Tokens[index].Id.Contains("adoptive-son") || context.Tokens[index].Id.Contains("adoptive-daughter")))
        {
            index++;
        }

        if (index == 0 || index >= context.Tokens.Count) return false; 

        if (context.Tokens[index].Id != "spouse") return false;
        index++;

        if (index >= context.Tokens.Count) return false;

        if (index != context.Tokens.Count - 1) return false;
        
        var siblingToken = context.Tokens[index];
        if (!siblingToken.Id.Contains("brother") && !siblingToken.Id.Contains("sister")) return false;

        Boolean isSiblingMale = siblingToken.Id.Contains("brother");
        
        int depth = index - 1;

        info = new KinshipSemanticInfo
        {
            RelationType = KinshipRelationType.DescendantSpouseSibling,
            Gender = isSiblingMale ? PersonGender.Male : PersonGender.Female,
            GenerationChange = -depth,
            IsPaternal = false, 
            IsTopLevelPaternal = false,
            IsSpouse = false
        };
        return true;
    }

    public static Boolean TryAnalyzeSiblingSpouseSibling(KinshipRuleContext context, out KinshipSemanticInfo info)
    {
        info = KinshipSemanticInfo.Empty;
        if (context.Tokens.Count != 3) return false;
        
        var t0 = context.Tokens[0].Id;
        var t1 = context.Tokens[1].Id;
        var t2 = context.Tokens[2].Id;

        if ((t0.Contains("brother") || t0.Contains("sister")) &&
            t1 == "spouse" &&
            (t2.Contains("brother") || t2.Contains("sister")))
        {
            Boolean isMale = t2.Contains("brother");
            info = new KinshipSemanticInfo
            {
                RelationType = KinshipRelationType.SiblingSpouseSibling,
                Gender = isMale ? PersonGender.Male : PersonGender.Female,
                GenerationChange = 0,
                IsPaternal = false, 
                IsTopLevelPaternal = false,
                IsSpouse = false
            };
            return true;
        }
        return false;
    }

    public static Boolean TryAnalyzeSiblingSpouseSiblingChild(KinshipRuleContext context, out KinshipSemanticInfo info)
    {
        info = KinshipSemanticInfo.Empty;
        if (context.Tokens.Count < 4) return false;

        // Pattern: Sibling -> Spouse -> Sibling -> Child...
        var t0 = context.Tokens[0];
        if (!t0.Id.Contains("brother") && !t0.Id.Contains("sister")) return false;

        if (context.Tokens[1].Id != "spouse") return false;

        var t2 = context.Tokens[2];
        if (!t2.Id.Contains("brother") && !t2.Id.Contains("sister")) return false;

        int index = 3;
        int descendantCount = 0;
        KinshipToken lastToken = null;

        while (index < context.Tokens.Count && 
               (context.Tokens[index].Id.Contains("son") || context.Tokens[index].Id.Contains("daughter") || 
                context.Tokens[index].Id.Contains("adoptive-son") || context.Tokens[index].Id.Contains("adoptive-daughter")))
        {
            descendantCount++;
            lastToken = context.Tokens[index];
            index++;
        }

        if (descendantCount == 0 || index != context.Tokens.Count) return false;

        Boolean isMale = lastToken.Id.Contains("son") || lastToken.Id.Contains("adoptive-son");
        Boolean initialSiblingIsBrother = t0.Id.Contains("brother");
        Boolean spouseSiblingIsBrother = t2.Id.Contains("brother");
        Boolean isOlder = t0.Id.Contains("older");

        info = new KinshipSemanticInfo
        {
            RelationType = KinshipRelationType.SiblingSpouseSiblingChild,
            Gender = isMale ? PersonGender.Male : PersonGender.Female,
            GenerationChange = -descendantCount,
            IsPaternal = initialSiblingIsBrother, // Initial Sibling Side
            IsTopLevelPaternal = spouseSiblingIsBrother, // Spouse's Sibling Gender (reused field)
            AgeOrder = isOlder ? SiblingOrder.Older : SiblingOrder.Younger,
            IsSpouse = false
        };
        return true;
    }

    public static Boolean TryAnalyzeSpouseSiblingSpouse(KinshipRuleContext context, out KinshipSemanticInfo info)
    {
        info = KinshipSemanticInfo.Empty;
        if (context.Tokens.Count != 3) return false;
        
        var t0 = context.Tokens[0].Id;
        var t1 = context.Tokens[1].Id;
        var t2 = context.Tokens[2].Id;

        if (t0 == "spouse" &&
            (t1.Contains("brother") || t1.Contains("sister")) &&
            t2 == "spouse")
        {
            Boolean siblingIsMale = t1.Contains("brother");
            Boolean siblingIsOlder = t1.Contains("older");
            
            PersonGender finalGender = siblingIsMale ? PersonGender.Female : PersonGender.Male;

            info = new KinshipSemanticInfo
            {
                RelationType = KinshipRelationType.SpouseSiblingSpouse,
                Gender = finalGender,
                GenerationChange = 0,
                IsPaternal = false, 
                IsTopLevelPaternal = false,
                AgeOrder = siblingIsOlder ? SiblingOrder.Older : SiblingOrder.Younger,
                IsSpouse = true
            };
            return true;
        }
        return false;
    }

    public static Boolean TryAnalyzeStepAncestor(KinshipRuleContext context, out KinshipSemanticInfo info)
    {
        info = KinshipSemanticInfo.Empty;
        if (context.Tokens.Count < 3) return false;

        var t0 = context.Tokens[0];
        if (!IsParentToken(t0)) return false;
        
        if (context.Tokens[1].Id != "spouse") return false;
        
        int index = 2;
        int parentCount = 0;
        while (index < context.Tokens.Count)
        {
            if (!IsParentToken(context.Tokens[index])) return false;
            parentCount++;
            index++;
        }
        
        if (parentCount == 0) return false;

        Boolean isFatherSide = t0.Id.Contains("father");

        // Side law (mumuy-adjudicated): father's spouse IS a mother-figure, so the whole
        // step-family behind her is maternal-line — mumuy collapses f,w to 妈妈 and names
        // f,w,m 外婆 / f,w,f 外公 / f,w,f,f 外曾祖父. The old interior-only scan excluded
        // the terminal parent, so the 3-token F.SP.M lost its maternal side entirely
        // (繼祖母 instead of 繼外祖母). A step-father entry (M.SP) stays paternal unless an
        // interior mother-hop crosses the line.
        bool hasMaternalIntermediary = isFatherSide;
        if (!hasMaternalIntermediary)
        {
            for (int i = 2; i < context.Tokens.Count - 1; i++) // interior hops only
            {
                if (context.Tokens[i].Id.Contains("mother"))
                {
                    hasMaternalIntermediary = true;
                    break;
                }
            }
        }

        info = new KinshipSemanticInfo
        {
            RelationType = KinshipRelationType.StepAncestor,
            Gender = context.Tokens[^1].Id.Contains("father") ? PersonGender.Male : PersonGender.Female,
            GenerationChange = 1 + parentCount,
            IsPaternal = !hasMaternalIntermediary, // False if there is a mother in the chain
            IsTopLevelPaternal = isFatherSide,
            IsSpouse = false,
            Origin = KinshipOrigin.Step
        };
        return true;
    }

    public static Boolean TryAnalyzeCollateralDescendantSpouseSiblingBase(KinshipRuleContext context, out KinshipSemanticInfo info)
    {
        info = KinshipSemanticInfo.Empty;
        if (context.Tokens.Count < 4) return false;

        var t0 = context.Tokens[0];
        if (!t0.Id.Contains("brother") && !t0.Id.Contains("sister")) return false;
        
        int index = 1;
        int descendantCount = 0;
        while (index < context.Tokens.Count && 
               (context.Tokens[index].Id.Contains("son") || context.Tokens[index].Id.Contains("daughter") || 
                context.Tokens[index].Id.Contains("adoptive-son") || context.Tokens[index].Id.Contains("adoptive-daughter")))
        {
            descendantCount++;
            index++;
        }
        if (descendantCount == 0) return false;

        if (index >= context.Tokens.Count || context.Tokens[index].Id != "spouse") return false;
        index++;

        if (index != context.Tokens.Count - 1) return false;
        var lastToken = context.Tokens[index];
        if (!lastToken.Id.Contains("brother") && !lastToken.Id.Contains("sister")) return false;

        Boolean isMale = lastToken.Id.Contains("brother");
        
        info = new KinshipSemanticInfo
        {
            RelationType = KinshipRelationType.CollateralDescendantSpouseSibling,
            Gender = isMale ? PersonGender.Male : PersonGender.Female,
            GenerationChange = -descendantCount,
            IsPaternal = t0.Id.Contains("brother"), 
            IsTopLevelPaternal = false,
            IsSpouse = false
        };
        return true;
    }

    // New generalized method
    public static Boolean TryAnalyzeCollateralDescendantSpouseSiblingChain(KinshipRuleContext context, out KinshipSemanticInfo info)
    {
        info = KinshipSemanticInfo.Empty;
        if (context.Tokens.Count < 4) return false;

        var initialSiblingToken = context.Tokens[0];
        if (!initialSiblingToken.Id.Contains("brother") && !initialSiblingToken.Id.Contains("sister")) return false;

        int index = 1;
        int initialDescendantCount = 0;
        List<string> pathSignature = new List<string>();

        while (index < context.Tokens.Count && 
               (context.Tokens[index].Id.Contains("son") || context.Tokens[index].Id.Contains("daughter") || 
                context.Tokens[index].Id.Contains("adoptive-son") || context.Tokens[index].Id.Contains("adoptive-daughter")))
        {
            initialDescendantCount++;
            pathSignature.Add((context.Tokens[index].Id.Contains("son") || context.Tokens[index].Id.Contains("adoptive-son")) ? "S" : "D");
            index++;
        }
        if (initialDescendantCount == 0) return false;

        if (index >= context.Tokens.Count || context.Tokens[index].Id != "spouse") return false;
        index++; // After spouse

        if (index >= context.Tokens.Count) return false; // Must have at least a sibling after spouse
        
        var spouseSiblingToken = context.Tokens[index];
        if (!spouseSiblingToken.Id.Contains("brother") && !spouseSiblingToken.Id.Contains("sister")) return false;
        index++; // After spouse's sibling

        int subsequentDescendantCount = 0;
        KinshipToken lastSubsequentDescendantToken = null;

        while (index < context.Tokens.Count &&
               (context.Tokens[index].Id.Contains("son") || context.Tokens[index].Id.Contains("daughter") ||
                context.Tokens[index].Id.Contains("adoptive-son") || context.Tokens[index].Id.Contains("adoptive-daughter")))
        {
            subsequentDescendantCount++;
            lastSubsequentDescendantToken = context.Tokens[index];
            pathSignature.Add((context.Tokens[index].Id.Contains("son") || context.Tokens[index].Id.Contains("adoptive-son")) ? "S" : "D");
            index++;
        }
        
        if (index != context.Tokens.Count) return false; // All tokens must be consumed

        // Determine final gender
        PersonGender finalGender;
        if (subsequentDescendantCount > 0)
        {
            finalGender = (lastSubsequentDescendantToken.Id.Contains("son") || lastSubsequentDescendantToken.Id.Contains("adoptive-son")) 
                          ? PersonGender.Male 
                          : PersonGender.Female;
        }
        else // The final person is the spouse's sibling
        {
            finalGender = (spouseSiblingToken.Id.Contains("brother")) 
                          ? PersonGender.Male 
                          : PersonGender.Female;
        }

        Boolean spouseSiblingIsMale = (spouseSiblingToken.Id.Contains("brother"));
        SiblingOrder spouseSiblingAgeOrder = SiblingOrder.Unknown;
        if (spouseSiblingToken.Id.Contains("older")) spouseSiblingAgeOrder = SiblingOrder.Older;
        else if (spouseSiblingToken.Id.Contains("younger")) spouseSiblingAgeOrder = SiblingOrder.Younger;

        info = new KinshipSemanticInfo
        {
            RelationType = KinshipRelationType.CollateralDescendantSpouseSiblingDescendant, // New relation type
            Gender = finalGender,
            GenerationChange = -(initialDescendantCount + subsequentDescendantCount),
            IsPaternal = initialSiblingToken.Id.Contains("brother"), // Paternal if initial sibling is brother
            IsTopLevelPaternal = false, // Not top-level paternal for this complex chain
            IsSpouse = false, // The final person is not a spouse
            SpouseSiblingIsMale = spouseSiblingIsMale,
            SpouseSiblingAgeOrder = spouseSiblingAgeOrder,
            DescendantPathSignature = string.Join(",", pathSignature),
            InitialDescendantCount = initialDescendantCount
        };
        return true;
    }


    public static Boolean TryAnalyzeSpouseCollateral(KinshipRuleContext context, out KinshipSemanticInfo info)
    {
        info = KinshipSemanticInfo.Empty;
        if (context.Tokens.Count < 3) return false;

        if (context.Tokens[0].Id != "spouse") return false;

        int index = 1;
        int parentCount = 0;
        KinshipToken firstParent = default;
        KinshipToken lastParent = default;

        while (index < context.Tokens.Count &&
               (context.Tokens[index].Id == "father" || context.Tokens[index].Id == "mother" ||
                context.Tokens[index].Id == "adoptive-father" || context.Tokens[index].Id == "adoptive-mother"))
        {
            if (parentCount == 0) firstParent = context.Tokens[index];
            lastParent = context.Tokens[index];
            parentCount++;
            index++;
        }

        if (parentCount == 0 || index >= context.Tokens.Count) return false;

        if (index != context.Tokens.Count - 1) return false;

        var siblingToken = context.Tokens[index];
        if (!siblingToken.Id.Contains("brother") && !siblingToken.Id.Contains("sister")) return false;

        Boolean isSiblingMale = siblingToken.Id.Contains("brother");
        Boolean isSiblingOlder = siblingToken.Id.Contains("older");
        Boolean isLastParentPaternal = lastParent.Id == "father" || lastParent.Id == "adoptive-father";
        Boolean isTopLevelPaternal = firstParent.Id == "father" || firstParent.Id == "adoptive-father";

        info = new KinshipSemanticInfo
        {
            RelationType = KinshipRelationType.SpouseCollateral,
            Gender = isSiblingMale ? PersonGender.Male : PersonGender.Female,
            GenerationChange = parentCount, 
            IsPaternal = isLastParentPaternal, 
            IsTopLevelPaternal = isTopLevelPaternal, 
            AgeOrder = isSiblingOlder ? SiblingOrder.Older : SiblingOrder.Younger,
            IsSpouse = true 
        };
        return true;
    }

    public static Boolean TryAnalyzeSiblingSpouseParent(KinshipRuleContext context, out KinshipSemanticInfo info)
    {
        info = KinshipSemanticInfo.Empty;
        if (context.Tokens.Count < 3) return false;

        var t0 = context.Tokens[0];
        if (!t0.Id.Contains("brother") && !t0.Id.Contains("sister")) return false;

        if (context.Tokens[1].Id != "spouse") return false;

        int index = 2;
        int parentCount = 0;
        
        while (index < context.Tokens.Count &&
               (context.Tokens[index].Id == "father" || context.Tokens[index].Id == "mother" ||
                context.Tokens[index].Id == "adoptive-father" || context.Tokens[index].Id == "adoptive-mother"))
        {
            parentCount++;
            index++;
        }

        if (parentCount == 0 || index != context.Tokens.Count) return false; 

        Boolean isLastParentMale = context.Tokens[^1].Id.Contains("father");
        Boolean isSiblingBrother = t0.Id.Contains("brother");

        info = new KinshipSemanticInfo
        {
            RelationType = KinshipRelationType.SiblingSpouseParent,
            Gender = isLastParentMale ? PersonGender.Male : PersonGender.Female,
            GenerationChange = parentCount, 
            IsPaternal = isSiblingBrother, // True for Brother, False for Sister
            IsTopLevelPaternal = false,
            IsSpouse = false
        };
        return true;
    }

    public static Boolean TryAnalyzeCollateralSpouseSibling(KinshipRuleContext context, out KinshipSemanticInfo info)
    {
        info = KinshipSemanticInfo.Empty;
        if (context.Tokens.Count < 3) return false;

        // Pattern: [Parents]+ -> Sibling -> Spouse -> Sibling
        if (!context.Tokens[^1].Id.Contains("brother") && !context.Tokens[^1].Id.Contains("sister")) return false;
        if (context.Tokens[^2].Id != "spouse") return false;

        var headTokens = context.Tokens.Take(context.Tokens.Count - 2).ToList();
        int siblingIndex = headTokens.FindLastIndex(t => t.Id.Contains("brother") || t.Id.Contains("sister"));
        if (siblingIndex == -1) return false;
        if (siblingIndex == 0) return false; 

        for (int i = 0; i < siblingIndex; i++)
        {
            if (!IsParentToken(headTokens[i])) return false;
        }
        if (siblingIndex != headTokens.Count - 1) return false;

        bool isPaternalLineage = headTokens[0].Id.Contains("father");
        bool isLastParentMale = headTokens[siblingIndex - 1].Id.Contains("father");
        bool isSourceSiblingMale = headTokens[siblingIndex].Id.Contains("brother");
        
        var targetSiblingToken = context.Tokens[^1];
        bool isTargetSiblingMale = targetSiblingToken.Id.Contains("brother");
        bool isTargetOlder = targetSiblingToken.Id.Contains("older");

        info = new KinshipSemanticInfo
        {
            RelationType = KinshipRelationType.CollateralSpouseSibling,
            Gender = isTargetSiblingMale ? PersonGender.Male : PersonGender.Female,
            GenerationChange = siblingIndex, // Same generation as the parent's sibling (e.g., M.OS.SP.OS is Gen 1)
            IsPaternal = isLastParentMale,
            IsTopLevelPaternal = isPaternalLineage,
            SpouseSiblingIsMale = isSourceSiblingMale,
            AgeOrder = isTargetOlder ? SiblingOrder.Older : SiblingOrder.Younger,
            IsSpouse = false
        };

        return true;
    }

    private static Boolean IsParentToken(KinshipToken t) => 
        t.Id == "father" || t.Id == "mother" || t.Id == "adoptive-father" || t.Id == "adoptive-mother";

    public static Boolean TryAnalyzeCollateral(KinshipRuleContext context, out KinshipSemanticInfo info)
    {
        info = KinshipSemanticInfo.Empty;
        
        KinshipChainSegments segments = context.Segments;
        if (segments.Parents.Count == 0 || segments.Siblings.Count == 0)
        {
            return false;
        }

        if (segments.Remaining.Count > 0)
        {
            return false;
        }

        String parentId = segments.Parents[0].Id;
        Boolean isPaternal = parentId == "father" || parentId == "adoptive-father";
        Boolean isMaternal = parentId == "mother" || parentId == "adoptive-mother";
        
        if (!isPaternal && !isMaternal)
        {
            return false;
        }

        KinshipToken lastParent = segments.Parents[^1];
        Boolean isLastParentPaternal = lastParent.Id == "father" || lastParent.Id == "adoptive-father";

        KinshipToken sibling = segments.Siblings[^1];
        Boolean siblingIsMale = sibling.Id == "older-brother" || sibling.Id == "younger-brother";
        Boolean isStrictlyPaternal = isLastParentPaternal && siblingIsMale;
        Boolean siblingIsOlder = sibling.Id == "older-brother" || sibling.Id == "older-sister";
        SiblingOrder ageOrder = siblingIsOlder ? SiblingOrder.Older : SiblingOrder.Younger;

        Int32 descendantCount = segments.Descendants.Count;
        Boolean lastDescendantIsMale = descendantCount > 0 
            ? (segments.Descendants[^1].Id == "son" || segments.Descendants[^1].Id == "adoptive-son")
            : false;

        Boolean isSpouse = segments.Spouses.Count > 0 && segments.Spouses.Count % 2 != 0;

        Int32 ancestorDepth = segments.Parents.Count;
        Int32 netGeneration = ancestorDepth - descendantCount;
        KinshipRelationType type = KinshipRelationType.Unknown;

        // Build Descendant Signature for Formatter
        string descSig = "";
        if (descendantCount > 0)
        {
            var sigs = new List<string>();
            foreach (var d in segments.Descendants)
            {
                sigs.Add((d.Id.Contains("son") || d.Id.Contains("adoptive-son")) ? "S" : "D");
            }
            descSig = string.Join(",", sigs);
        }

        // Determine Type based on Generation and Path
        if (ancestorDepth == 1)
        {
            if (descendantCount == 0)
            {
                type = siblingIsMale ? KinshipRelationType.Uncle : KinshipRelationType.Aunt;
            }
            else
            {
                type = isStrictlyPaternal ? KinshipRelationType.CousinTang : KinshipRelationType.CousinBiao;
            }
        }
        else
        {
            // Deeper collateral (Grand-uncle, etc.)
            if (descendantCount == 0)
            {
                if (ancestorDepth == 2) type = siblingIsMale ? KinshipRelationType.GrandUncle : KinshipRelationType.GrandAunt;
                else if (ancestorDepth == 3) type = siblingIsMale ? KinshipRelationType.GreatGrandUncle : KinshipRelationType.GreatGrandAunt;
                else type = siblingIsMale ? KinshipRelationType.GrandUncle : KinshipRelationType.GrandAunt;
            }
            else
            {
                // Here we have a generation gap in deeper collateral (e.g. F.F.OB.S -> Tang Shu)
                // If it's peer, it's still a cousin. If it's elder, it's a cousin-uncle.
                if (netGeneration == 0) type = isStrictlyPaternal ? KinshipRelationType.CousinTang : KinshipRelationType.CousinBiao;
                else type = isStrictlyPaternal ? KinshipRelationType.CousinTang : KinshipRelationType.CousinBiao; // Will be refined in Formatter
            }
        }

        PersonGender genderOfFinalSubject;
        if (isSpouse) {
            genderOfFinalSubject = (descendantCount > 0 
                ? (lastDescendantIsMale ? PersonGender.Female : PersonGender.Male)
                : (siblingIsMale ? PersonGender.Female : PersonGender.Male));
        } else {
            genderOfFinalSubject = (descendantCount > 0 
                ? (lastDescendantIsMale ? PersonGender.Male : PersonGender.Female)
                : (siblingIsMale ? PersonGender.Male : PersonGender.Female));
        }

        info = new KinshipSemanticInfo
        {
            RelationType = type,
            Gender = genderOfFinalSubject,
            IsPaternal = isLastParentPaternal, 
            IsTopLevelPaternal = isPaternal,   
            IsStrictlyPaternal = isStrictlyPaternal,
            CollateralSiblingIsMale = siblingIsMale,
            AgeOrder = ageOrder,
            IsSpouse = isSpouse,
            GenerationChange = netGeneration,
            DescendantPathSignature = descSig
        };
        return true;
    }

    public static Boolean TryAnalyzeCollateralSpouseParent(KinshipRuleContext context, out KinshipSemanticInfo info)
    {
        info = KinshipSemanticInfo.Empty;
        if (context.Tokens.Count < 3) return false;

        // Pattern: [Parents]+ -> Sibling -> Spouse -> Parent
        // We look for the last Spouse and Parent first.
        if (context.Tokens[^1].Id is not ("father" or "mother" or "adoptive-father" or "adoptive-mother")) return false;
        if (context.Tokens[^2].Id != "spouse") return false;

        // Everything before [SP, P] must be a valid Collateral path (Parent -> Sibling)
        var headTokens = context.Tokens.Take(context.Tokens.Count - 2).ToList();
        
        // Use recursive analysis or a simpler check? 
        // Let's use the Segments logic from a sub-context if possible, but keep it simple here.
        int siblingIndex = headTokens.FindLastIndex(t => t.Id.Contains("brother") || t.Id.Contains("sister"));
        if (siblingIndex == -1) return false;
        if (siblingIndex == 0) return false; // Ensure there's at least one parent (shadowing check for SiblingSpouseParent)

        // Check that all tokens before sibling are parents
        for (int i = 0; i < siblingIndex; i++)
        {
            if (!IsParentToken(headTokens[i])) return false;
        }
        // Check that there is nothing between sibling and spouse (headTokens ends at siblingIndex)
        if (siblingIndex != headTokens.Count - 1) return false;

        // Determine the "Source" collateral type (e.g. Aunt)
        // We can reuse logic from Collateral rule implicitly.
        bool isPaternalLineage = headTokens[0].Id.Contains("father");
        bool isLastParentMale = headTokens[siblingIndex - 1].Id.Contains("father");
        bool isSiblingMale = headTokens[siblingIndex].Id.Contains("brother");

        info = new KinshipSemanticInfo
        {
            RelationType = KinshipRelationType.CollateralSpouseParent,
            Gender = context.Tokens[^1].Id.Contains("father") ? PersonGender.Male : PersonGender.Female,
            GenerationChange = siblingIndex + 1, // Fixed: siblingIndex represents the generation of the parents (e.g., F.OB.SP.F is Gen 2)
            IsPaternal = isLastParentMale, // Reusing IsPaternal to store if the parent of the sibling was male (Bo/Shu side)
            IsTopLevelPaternal = isPaternalLineage, // Inner vs Outer lineage
            SpouseSiblingIsMale = isSiblingMale, // Gender of the sibling (Uncle vs Aunt)
            IsSpouse = false
        };

        return true;
    }

    public static Boolean TryAnalyzeSiblingCoParentInLaw(KinshipRuleContext context, out KinshipSemanticInfo info)
    {
        info = KinshipSemanticInfo.Empty;
        if (context.Tokens.Count != 4) return false;

        var t0 = context.Tokens[0]; // Sibling
        var t1 = context.Tokens[1]; // Child
        var t2 = context.Tokens[2]; // Spouse
        var t3 = context.Tokens[3]; // Parent

        // Pattern Check
        bool isSibling = t0.Id.Contains("brother") || t0.Id.Contains("sister");
        bool isChild = t1.Id.Contains("son") || t1.Id.Contains("daughter") || t1.Id.Contains("adoptive-son") || t1.Id.Contains("adoptive-daughter");
        bool isSpouse = t2.Id == "spouse";
        bool isParent = t3.Id.Contains("father") || t3.Id.Contains("mother") || t3.Id.Contains("adoptive-father") || t3.Id.Contains("adoptive-mother");

        if (!isSibling || !isChild || !isSpouse || !isParent) return false;

        // Semantics extraction
        bool isInitialSiblingBrother = t0.Id.Contains("brother");
        bool isFinalParentMale = t3.Id.Contains("father");

        info = new KinshipSemanticInfo
        {
            RelationType = KinshipRelationType.SiblingCoParentInLaw,
            Gender = isFinalParentMale ? PersonGender.Male : PersonGender.Female,
            GenerationChange = 0, // Peer generation
            IsPaternal = isInitialSiblingBrother, // True = Brother's side, False = Sister's side
            IsTopLevelPaternal = false,
            IsSpouse = false
        };

        return true;
    }

    public static Boolean TryAnalyzePaternalGrandparentSpouseMaternalGrandparent(KinshipRuleContext context, out KinshipSemanticInfo info)
    {
        info = KinshipSemanticInfo.Empty;
        if (context.Tokens.Count != 4) return false;

        // Pattern: F -> SP -> M -> F
        if (context.Tokens[0].Id == "father" &&
            context.Tokens[1].Id == "spouse" &&
            context.Tokens[2].Id == "mother" &&
            context.Tokens[3].Id == "father")
        {
            info = new KinshipSemanticInfo
            {
                RelationType = KinshipRelationType.PaternalGrandparentSpouseMaternalGrandparent,
                Gender = PersonGender.Male, // The final person (mother's father) is male
                GenerationChange = 3, // 3 generations up (father, mother, father)
                IsPaternal = false, // Not purely paternal (due to 'mother' in chain)
                IsTopLevelPaternal = false, // Not top-level paternal
                IsSpouse = false // The final person is not a spouse
            };
            return true;
        }
        return false;
    }
}
