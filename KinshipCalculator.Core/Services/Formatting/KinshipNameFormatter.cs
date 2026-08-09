using System;
using System.Collections.Generic;
using KinshipCalculator.Core.Models;
using KinshipCalculator.Core.Models.Semantics;

namespace KinshipCalculator.Core.Services.Formatting;

public enum NamingContext
{
    Formal,
    Colloquial,
    Official
}

public class KinshipNameFormatter
{
    public String Format(KinshipSemanticInfo info, String languageCode, NamingContext context)
    {
        if (languageCode.StartsWith("zh"))
        {
            return FormatChinese(info, context, languageCode == "zh-Hant");
        }
        return FormatEnglish(info, context);
    }

    private String FormatChinese(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        if (info.RelationType == KinshipRelationType.Sibling)
        {
            return FormatSibling(info, context, isTraditional);
        }

        if (info.RelationType == KinshipRelationType.CousinTang || info.RelationType == KinshipRelationType.CousinBiao)
        {
            return FormatCousin(info, context, isTraditional);
        }

        if (info.RelationType == KinshipRelationType.GrandUncle || info.RelationType == KinshipRelationType.GrandAunt ||
            info.RelationType == KinshipRelationType.GreatGrandUncle || info.RelationType == KinshipRelationType.GreatGrandAunt)
        {
            return FormatGrandCollateral(info, context, isTraditional);
        }
        
        if (info.RelationType == KinshipRelationType.Uncle || info.RelationType == KinshipRelationType.Aunt)
        {
            return FormatCollateral(info, context, isTraditional);
        }

        if (info.RelationType == KinshipRelationType.Parent || 
            info.RelationType == KinshipRelationType.GrandParent || 
            info.RelationType == KinshipRelationType.GreatGrandParent ||
            info.RelationType == KinshipRelationType.Ancestor)
        {
            return FormatAncestor(info, context, isTraditional);
        }

        if (info.RelationType == KinshipRelationType.Child || 
            info.RelationType == KinshipRelationType.GrandChild || 
            info.RelationType == KinshipRelationType.GreatGrandChild ||
            info.RelationType == KinshipRelationType.Descendant)
        {
            return FormatDescendant(info, context, isTraditional);
        }

        if (info.RelationType == KinshipRelationType.SpouseParent)
        {
            return FormatSpouseLineal(info, context, isTraditional);
        }

        if (info.RelationType == KinshipRelationType.SpouseSibling)
        {
            return FormatSpouseCollateral(info, context, isTraditional);
        }

        if (info.RelationType == KinshipRelationType.SpouseSiblingChild)
        {
            return FormatSpouseSiblingChild(info, context, isTraditional);
        }

        if (info.RelationType == KinshipRelationType.SiblingDescendant)
        {
            return FormatSiblingDescendant(info, context, isTraditional);
        }

        if (info.RelationType == KinshipRelationType.StepAncestor)
        {
            return FormatStepAncestor(info, context, isTraditional);
        }

        if (info.RelationType == KinshipRelationType.CoParentInLaw)
        {
            return FormatCoParentInLaw(info, context, isTraditional);
        }

        if (info.RelationType == KinshipRelationType.DescendantSpouseSibling)
        {
            return FormatDescendantSpouseSibling(info, context, isTraditional);
        }

        if (info.RelationType == KinshipRelationType.SiblingSpouseSibling)
        {
            return FormatSiblingSpouseSibling(info, context, isTraditional);
        }

        if (info.RelationType == KinshipRelationType.SpouseSiblingSpouse)
        {
            return FormatSpouseSiblingSpouse(info, context, isTraditional);
        }

        if (info.RelationType == KinshipRelationType.SiblingSpouseSiblingChild)
        {
            return FormatSiblingSpouseSiblingChild(info, context, isTraditional);
        }

        if (info.RelationType == KinshipRelationType.SpouseCollateral)
        {
            return FormatAffinalCollateral(info, context, isTraditional);
        }

        if (info.RelationType == KinshipRelationType.SiblingSpouseParent)
        {
            return FormatSiblingSpouseParent(info, context, isTraditional);
        }

        if (info.RelationType == KinshipRelationType.CollateralDescendantSpouseSibling)
        {
            return FormatCollateralDescendantSpouseSibling(info, context, isTraditional);
        }

        if (info.RelationType == KinshipRelationType.CollateralDescendantSpouseSiblingDescendant)
        {
            return FormatCollateralDescendantSpouseSiblingDescendant(info, context, isTraditional);
        }
        
        if (info.RelationType == KinshipRelationType.CollateralSpouseSibling)
        {
            return FormatCollateralSpouseSibling(info, context, isTraditional);
        }
        
        if (info.RelationType == KinshipRelationType.PaternalGrandparentSpouseMaternalGrandparent)
        {
            return FormatPaternalGrandparentSpouseMaternalGrandparent(info, context, isTraditional);
        }

        if (info.RelationType == KinshipRelationType.SiblingCoParentInLaw)
        {
            return FormatSiblingCoParentInLaw(info, context, isTraditional);
        }

        if (info.RelationType == KinshipRelationType.CollateralSpouseParent)
        {
            return FormatCollateralSpouseParent(info, context, isTraditional);
        }

        // K7: retired the "TODO: Implement Full Rules" placeholder — uncovered relation types now
        // surface as an honest bounded description instead of a debug literal.
        return isTraditional ? "親屬關係(規則未覆蓋)" : "亲属关系(规则未覆盖)";
    }

    private String FormatCollateralSpouseSibling(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        if (context == NamingContext.Official) return "旁系長輩的配偶的兄弟姐妹";

        string prefix = "";
        if (info.IsPaternal) 
        {
            prefix = info.SpouseSiblingIsMale ? "叔" : "姑"; 
        }
        else 
        {
            prefix = info.SpouseSiblingIsMale ? "舅" : "姨";
        }

        string mid = isTraditional ? "姻" : "姻";
        
        int depth = info.GenerationChange;
        string suffix = "";
        
        if (depth == 0) // Peer generation (Shadowing case or general sibling)
        {
            if (info.Gender == PersonGender.Male)
            {
                suffix = info.AgeOrder == SiblingOrder.Older ? "兄" : "弟";
            }
            else
            {
                suffix = info.AgeOrder == SiblingOrder.Older ? "姊" : "妹";
            }
        }
        else if (depth == 1) // Parent generation
        {
            if (info.Gender == PersonGender.Male)
            {
                suffix = info.AgeOrder == SiblingOrder.Older ? "伯父" : "叔父";
            }
            else
            {
                // Sisters of a male in-law (e.g., Uncle-in-law's sister) are 'Gu Mu'
                suffix = "姑母"; 
            }
        }
        else // Ancestor generation
        {
            var stems = isTraditional ? AscendStemHant : AscendStemHans;
            string stem = stems.ContainsKey(depth) ? stems[depth] : $"{depth-1}世祖";
            string genderSuffix = info.Gender == PersonGender.Male ? "伯/叔" : "姑";
            suffix = stem + genderSuffix;
        }

        if (context == NamingContext.Colloquial)
        {
            string descriptive = prefix + "丈的" + (info.AgeOrder == SiblingOrder.Older ? "姊" : "妹");
            if (prefix == "姑") descriptive = "姑丈的" + (info.AgeOrder == SiblingOrder.Older ? "姊" : "妹");
            
            return $"{prefix}{mid}{suffix}|{descriptive}";
        }

        return prefix + mid + suffix;
    }

    private String FormatCollateralSpouseParent(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        if (context == NamingContext.Official) return "旁系長輩的配偶的父母";

        // Determine prefix based on the collateral relative
        // info.IsPaternal = True if parent was Father.
        // info.SpouseSiblingIsMale = True if relative is Male.
        
        string prefix = "";
        if (info.IsPaternal) // Parent side
        {
            prefix = info.SpouseSiblingIsMale ? "叔" : "姑"; // Default to Shu for male (or Bo/Shu)
        }
        else // Maternal side
        {
            prefix = info.SpouseSiblingIsMale ? "舅" : "姨";
        }

        // Mid
        string mid = isTraditional ? "姻" : "姻";

        // Suffix based on generation
        int depth = info.GenerationChange;
        string stem = "";
        var stems = isTraditional ? AscendStemHant : AscendStemHans;
        if (stems.TryGetValue(depth, out string? value)) stem = value;
        else stem = $"{depth-1}世祖";

        string genderSuffix = info.Gender == PersonGender.Male ? "父" : "母";

        return prefix + mid + stem + genderSuffix;
    }

    private String FormatSiblingCoParentInLaw(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        if (context == NamingContext.Official) return "兄弟姐妹的子女的配偶的父母";

        // Logic: Prefix + "Yin" + Suffix
        // Prefix: Brother's side -> Zhi (侄/姪), Sister's side -> Sheng (甥)
        // Suffix: Male -> Brother (兄弟), Female -> Sister (姊妹)

        string prefix;
        if (info.IsPaternal) // Brother's side
        {
            prefix = isTraditional ? "姪" : "侄";
        }
        else // Sister's side
        {
            prefix = "甥";
        }

        string middle = isTraditional ? "姻" : "姻";

        string suffix;
        if (info.Gender == PersonGender.Male)
        {
            suffix = isTraditional ? "兄弟" : "兄弟";
        }
        else
        {
            suffix = isTraditional ? "姊妹" : "姊妹";
        }

        return prefix + middle + suffix;
    }

    private String FormatCollateralDescendantSpouseSiblingDescendant(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        if (context == NamingContext.Official) return "兄弟姐妹的子女的配偶的兄弟姐妹的子女"; // Official description
        
        // Parse Signature
        string[] path = info.DescendantPathSignature?.Split(',') ?? Array.Empty<string>();
        int initialCount = info.InitialDescendantCount;
        
        // 1. Build Initial Part (The source relative)
        // Brother side: 侄 / Sister side: 外甥
        // Check for 'D' in the initial path to determine "Wai" (Grand)
        
        bool isBrotherSide = info.IsPaternal;
        bool hasFemaleInInitialPath = false;
        
        for (int i = 0; i < initialCount; i++)
        {
            if (i < path.Length && path[i] == "D")
            {
                hasFemaleInInitialPath = true;
            }
        }
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        
        // Base: 侄 / 甥 (Traditional: 姪 / 甥)
        string baseTerm;
        if (isBrotherSide)
        {
             baseTerm = isTraditional ? "姪" : "侄";
             // If brother side has female in path (and depth > 1), it becomes "侄外..."
             if (hasFemaleInInitialPath && initialCount > 1)
             {
                 baseTerm += "外";
             }
        }
        else
        {
             // Sister side uses the compact 甥 in 眷-composites (mumuy: 甥眷外孙女, not
             // 外甥眷…) — the standalone 外甥 keeps its 外 only outside composites.
             baseTerm = "甥";
        }
        sb.Append(baseTerm);
        
        // Add generation stem for Initial Part
        // Depth 1: 侄/甥
        // Depth 2: 侄孙/甥孙
        // Depth 3: 曾侄孙...
        
        if (initialCount > 1)
        {
            var stems = isTraditional ? DescendStemHant : DescendStemHans;
            string stem = stems.ContainsKey(initialCount) ? stems[initialCount] : $"{initialCount-1}代孫";
            sb.Append(stem);
        }
        
        // 2. Add "眷" (Juan) - Meaning affinal kin
        
        // COLLOQUIAL LOGIC CHECK
        // If we are in Colloquial mode, and there are NO subsequent descendants (meaning we end at the spouse's sibling),
        // we can generate "X的小舅子" etc.
        int totalDepth = Math.Abs(info.GenerationChange);
        int subsequentCount = totalDepth - initialCount;
        
        if (context == NamingContext.Colloquial && subsequentCount == 0)
        {
             // Generate "BaseTerm" + "的" + "SpouseSiblingTerm"
             string baseTitle = sb.ToString(); // "侄外孙" or "侄孙" etc.
             
             string suffixTerm;
             bool isMale = info.SpouseSiblingIsMale;
             bool isOlder = info.SpouseSiblingAgeOrder == SiblingOrder.Older;
             
             if (isMale)
             {
                 suffixTerm = isOlder ? (isTraditional ? "大舅子" : "大舅子") : (isTraditional ? "小舅子" : "小舅子");
             }
             else
             {
                 suffixTerm = isOlder ? (isTraditional ? "大姨子" : "大姨子") : (isTraditional ? "小姨子" : "小姨子");
             }
             
             return $"{baseTitle}的{suffixTerm}";
        }

        sb.Append("眷");
        
        // 3. Build Final Part (The target relative)
        // Calculated based on TOTAL generation depth relative to Self.
        
        // Determine if the final descendant is 'External' (Wai) relative to the spouse's family
        // It is external if: 
        // 1. The spouse's sibling is a sister (!SpouseSiblingIsMale)
        // 2. Or if any descendant in the subsequent path is through a female ('D')
        bool isSubsequentExternal = !info.SpouseSiblingIsMale;
        if (!isSubsequentExternal && subsequentCount > 0)
        {
            // Check subsequent part of the path signature
            for (int i = initialCount; i < path.Length; i++)
            {
                if (path[i] == "D")
                {
                    isSubsequentExternal = true;
                    break;
                }
            }
        }

        string finalPrefix = isSubsequentExternal ? "外" : "";
        var finalStems = isTraditional ? DescendStemHant : DescendStemHans;
        string finalStem = finalStems.ContainsKey(totalDepth) ? finalStems[totalDepth] : $"{totalDepth-1}代孫";
        
        sb.Append(finalPrefix).Append(finalStem);
        
        // 4. Add Gender Suffix
        // 男 / 女
        sb.Append(info.Gender == PersonGender.Male ? "男" : "女");
        
        return sb.ToString();
    }

    private String FormatStepAncestor(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        if (context == NamingContext.Official) return "繼親長輩";

        // Reuse FormatAncestor but with Step prefix
        string baseTitle = FormatAncestor(info, context, isTraditional);
        
        // Remove existing "Yang" if any (unlikely for Step but for safety)
        if (baseTitle.StartsWith("养") || baseTitle.StartsWith("養")) baseTitle = baseTitle.Substring(1);

        return (isTraditional ? "繼" : "继") + baseTitle;
    }

    private String FormatPaternalGrandparentSpouseMaternalGrandparent(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        if (context == NamingContext.Official) return "父的配偶的母的父"; // Official description

        if (context == NamingContext.Colloquial)
        {
            return isTraditional ? "外曾祖父|曾祖父" : "外曾祖父|曾祖父";
        }
        
        return isTraditional ? "外曾外祖父" : "外曾外祖父";
    }

    private String FormatCollateralDescendantSpouseSibling(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        if (context == NamingContext.Official) return "兄弟姐妹的后代的配偶的兄弟姐妹";
        
        // Construct base name for the descendant (without "Yin")
        // We can reuse FormatSiblingDescendant logic but we need to be careful about the gender.
        // The gender in info is the FINAL relative (the spouse's sibling).
        // But FormatSiblingDescendant uses gender for the Descendant itself.
        // We don't have the Descendant's gender in info directly (it was lost in analyzer).
        // We only have GenerationChange (-depth).
        
        // However, "侄外孙" implies we know the path details (Sister vs Brother side, Son vs Daughter).
        // Our Analyzer only stored "IsPaternal" (Brother side). It didn't store intermediate "Wai".
        // So we can't generate "侄外孙" vs "侄孙" accurately if we don't know the descendant path.
        // We only know it's Brother's line or Sister's line.
        
        // Fallback: "姻" + Generic Descendant Title.
        // If IsPaternal (Brother side): 姻侄孙...
        // If !IsPaternal (Sister side): 姻外甥孙...
        
        // This is the best we can do without `Lineage` info.
        
        Int32 depth = Math.Abs(info.GenerationChange);
        String baseTitle = "";
        Boolean isBrotherSide = info.IsPaternal;
        Boolean isMale = info.Gender == PersonGender.Male; // Final relative gender
        
        if (isBrotherSide)
        {
             // Brother side
             if (depth == 1) baseTitle = "侄";
             else if (depth == 2) baseTitle = "侄孙";
             else baseTitle = "曾侄孙";
        }
        else
        {
             // Sister side
             if (depth == 1) baseTitle = "外甥";
             else if (depth == 2) baseTitle = "外甥孙";
             else baseTitle = "外甥曾孙";
        }
        
        String suffix = isMale ? (isBrotherSide ? "兄弟" : "兄弟") : "姊妹";
        // Or just "姻" + baseTitle + (Male?"男":"女")?
        // Mumuy: 侄外孙眷孙男.
        
        return $"姻{baseTitle}";
    }

    private String FormatSiblingSpouseSiblingChild(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        if (context == NamingContext.Official) return "兄弟姐妹的配偶的兄弟姐妹的子女";

        Boolean isMale = info.Gender == PersonGender.Male; // Child Gender
        Boolean initialSiblingIsBrother = info.IsPaternal;
        Boolean spouseSiblingIsBrother = info.IsTopLevelPaternal; // Reused field for spouse's sibling gender
        SiblingOrder ageOrder = info.AgeOrder; // Age order of the initial sibling

        // Base child term (侄/外甥) based on spouse's sibling gender
        String baseChildTerm = spouseSiblingIsBrother 
            ? (isTraditional ? "侄" : "侄") 
            : (isTraditional ? "外甥" : "外甥");
        
        // Suffix for child gender (子/女)
        String childGenderSuffix = isMale ? (isTraditional ? "子" : "子") : (isTraditional ? "女" : "女");

        // How far DOWN the spouse's sibling's line the chain went. The analyzer counts it
        // (GenerationChange = -descendantCount); this method used to stop at the first
        // generation, so 姊妹眷姪子 named both the nephew and his son. Depth 1 keeps its exact
        // wording; below that the shared descendant ladders take over (姪孫 / 外甥孫 …).
        Int32 depth = Math.Abs(info.GenerationChange);
        String deepChildTerm = depth <= 1
            ? baseChildTerm + childGenderSuffix
            : spouseSiblingIsBrother
                ? (isTraditional ? BuildCollateralDescHant(depth, isMale) : BuildCollateralDescHans(depth, isMale))
                : (isTraditional ? BuildSororalDescHant(depth, isMale) : BuildSororalDescHans(depth, isMale));

        // Colloquial 1: In-law + base child term
        String colloquialInLawChild = (isTraditional ? "姻" : "姻") + deepChildTerm;

        // Colloquial 2: Descriptive term (Brother's wife's brother's child)
        String initialSiblingTitle;
        if (initialSiblingIsBrother)
        {
            initialSiblingTitle = (ageOrder == SiblingOrder.Older) 
                ? (isTraditional ? "嫂嫂" : "嫂子") 
                : (isTraditional ? "弟媳" : "弟媳");
        }
        else
        {
            initialSiblingTitle = (ageOrder == SiblingOrder.Older) 
                ? (isTraditional ? "姐夫" : "姐夫") 
                : (isTraditional ? "妹夫" : "妹夫"); // This needs to be wife of brother or husband of sister
            // Let's simplify this. It's the initial sibling's spouse.
            initialSiblingTitle = initialSiblingIsBrother
                ? (isMale ? (isTraditional ? "弟媳" : "弟媳") : (isTraditional ? "嫂嫂" : "嫂子"))
                : (isMale ? (isTraditional ? "妹夫" : "妹夫") : (isTraditional ? "姐夫" : "姐夫"));
        }
        
        String descriptiveChildTitle = isMale 
            ? (spouseSiblingIsBrother ? (isTraditional ? "侄子" : "侄子") : (isTraditional ? "外甥" : "外甥"))
            : (spouseSiblingIsBrother ? (isTraditional ? "侄女" : "侄女") : (isTraditional ? "外甥女" : "外甥女"));

        String colloquialDescriptive = initialSiblingTitle + (isTraditional ? "的" : "的") + descriptiveChildTitle;

        if (context == NamingContext.Formal)
        {
            String initialSiblingFormal = initialSiblingIsBrother
                ? (isTraditional ? "兄弟" : "兄弟")
                : (isTraditional ? "姊妹" : "姊妹");
            return initialSiblingFormal + (isTraditional ? "眷" : "眷") + deepChildTerm;
        }

        if (context == NamingContext.Colloquial)
        {
            return $"{colloquialInLawChild}|{colloquialDescriptive}";
        }

        // Fallback or other contexts
        return (isTraditional ? "姻侄女" : "姻侄女");
    }

    private String FormatAffinalCollateral(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        if (context == NamingContext.Official) return "配偶的旁系長輩";

        // The chain is spouse -> parents -> sibling: the spouse's uncle or aunt.
        //
        // KNOWN LIMITATION — the emitted title is the WIFE-SIDE 岳 series for both egos.
        // Chinese distinguishes the two sides here (妻之伯父 = 伯岳父, 夫之伯父 = 伯公 or, by
        // 從夫稱, simply 伯父), but KinshipSemanticInfo spends its two lineage flags on other
        // questions: IsPaternal picks the 伯/叔/姑 vs 舅/姨 letter from the LAST parent, and
        // IsTopLevelPaternal picks the line from the FIRST one. Neither records which spouse
        // the chain entered through, even though TryAnalyzeSpouseCollateral has the ego gender
        // in hand. The southern 公/婆 spellings therefore reach a female ego only as dialect
        // variants (dialect-south registers 伯公 against 伯岳父), never as her primary.
        // Deciding what her primary SHOULD be — the affinal 伯公, or 伯父 by 從夫稱 — is a
        // naming policy question, not a plumbing one, and moves a whole family of rows.
        String uncleTitle = "";
        Boolean isOlder = info.AgeOrder == SiblingOrder.Older;
        Boolean isMale = info.Gender == PersonGender.Male;
        // The letter comes from the blood pivot, the 父/母 ending from the person being named.
        // They differ exactly when a marriage hop closes the chain (妻之伯父之妻 = 伯岳母).
        Boolean pivotIsMale = info.CollateralSiblingIsMale;

        if (info.IsPaternal) // Last parent is Father
        {
            if (pivotIsMale) uncleTitle = isOlder ? "伯" : "叔";
            else uncleTitle = "姑";
        }
        else // Last parent is Mother
        {
            if (pivotIsMale) uncleTitle = "舅";
            else uncleTitle = "姨";
        }
        
        // Add suffix
        // "岳父/岳母" if Wife side.
        // "公/婆" if Husband side.
        
        // K16: the standard spouse-side form is computed (叔岳父); the 公/婆 spellings are
        // southern lookups and now live in the dialect layer, appended when it registers them.
        String suffix = isMale ? "父" : "母";
        String standard = $"{uncleTitle}岳{suffix}";
        String set = Data.KinshipLexiconLayers.GetVariantSet(standard);
        return String.IsNullOrEmpty(set) ? standard : $"{standard}|{set}";
    }

    private String FormatSiblingSpouseParent(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        if (context == NamingContext.Official) return "兄弟姐妹的配偶的父母";
        
        Boolean isMale = info.Gender == PersonGender.Male;
        Boolean isBrotherSide = info.IsPaternal;

        // How far up the spouse's line the chain went. The analyzer counts it; this method used
        // to ignore it, so 兄之妻之父 and 兄之妻之父之父 -- a parent and a GRANDparent -- both
        // came out 兄弟眷父. Same ancestor ladder the lineal formatter uses (祖 / 曾祖 / 高祖 …).
        Int32 ascent = Math.Max(1, Math.Abs(info.GenerationChange));
        var stems = isTraditional ? AscendStemHant : AscendStemHans;
        String ascentStem = ascent <= 1
            ? String.Empty
            : (stems.TryGetValue(ascent, out String? s) ? s : $"{ascent - 1}世祖");

        if (context == NamingContext.Formal)
        {
            // Same 姻/眷 convention as AffinalWebComposer and FormatSiblingSpouseSibling: the
            // connector records which side the chain crossed on, so a brother bridge takes 眷.
            String siblingPrefix = isBrotherSide ? "兄弟" : "姊妹";
            String connector = isBrotherSide ? "眷" : "姻";
            return $"{siblingPrefix}{connector}{ascentStem}{(isMale ? "父" : "母")}";
        }

        // Past the parent generation there is no everyday paraphrase — 兄弟的岳父 does not
        // stretch to a grandparent — so the formal compound stays primary and the slot is empty.
        if (ascent > 1)
        {
            return String.Empty;
        }

        if (context == NamingContext.Colloquial)
        {
            String relationDesc;
            if (isBrotherSide)
            {
                // Brother's Wife's Parents -> Brother's Yue Fu/Mu (wife-side lexeme).
                relationDesc = "兄弟的" + LexemeOrDefault(isMale ? "SP.F" : "SP.M", PersonGender.Male, isMale ? "岳父" : "岳母");
            }
            else
            {
                // Sister's Husband's Parents -> Sister's Gong/Po (husband-side lexeme).
                relationDesc = "姊妹的" + LexemeOrDefault(isMale ? "SP.F" : "SP.M", PersonGender.Female, isMale ? "公公" : "婆婆");
            }

            // K16: the polite address forms are everyday vocabulary → lexicon layers.
            String politeAddress = String.Join('|', System.Linq.Enumerable.Where(new[]
            {
                Data.KinshipLexiconLayers.GetVariantSet(isMale ? "伯父" : "伯母"),
                Data.KinshipLexiconLayers.GetVariantSet(isMale ? "叔父" : "婶母")
            }, s => !String.IsNullOrEmpty(s)));

            return String.IsNullOrEmpty(politeAddress) ? relationDesc : $"{relationDesc}|{politeAddress}";
        }

        // Fallback (should not be reached typically if context is covered)
        String coParent = LexemeOrDefault(isMale ? "S.SP.F" : "S.SP.M", PersonGender.Unknown, isMale ? "亲家公" : "亲家母");
        return isTraditional ? KinshipScriptConverter.ToHant(coParent) : coParent;
    }

    /// <summary>
    /// Standard lexeme from the base layer, falling back to the built-in spelling when a
    /// user layer removed it (the engine must never emit an empty kinship word).
    /// </summary>
    private static String LexemeOrDefault(String chainKey, PersonGender egoGender, String fallback)
        => Data.KinshipLexiconLayers.TryGetStandardLexeme(chainKey, egoGender) ?? fallback;

    private String FormatDescendantSpouseSibling(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        if (context == NamingContext.Official) return "子女的配偶的兄弟姐妹";
        Boolean isMale = info.Gender == PersonGender.Male;
        return isMale ? (isTraditional ? "姻侄" : "姻侄") : (isTraditional ? "姻侄女" : "姻侄女");
    }

    private String FormatSiblingSpouseSibling(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        if (context == NamingContext.Official) return "兄弟姐妹的配偶的兄弟姐妹";
        
        Boolean isMale = info.Gender == PersonGender.Male;
        // Two slots, two different people. The first names the sibling the chain went THROUGH,
        // the second the person being named; they coincide only by accident. This used to write
        // the terminal gender into both, so 兄之妻之姐 read 姊妹姻姊妹.
        Boolean bridgeIsBrother = info.IsPaternal;
        // Connector by bridge gender, the project's own 姻/眷 convention (AffinalWebComposer:
        // "String connector = bridgeIsMale ? 眷 : 姻"). This method wrote 姻 unconditionally,
        // so the same relation carried one connector here and another there.
        String connector = bridgeIsBrother ? "眷" : "姻";

        if (context == NamingContext.Formal)
        {
            return $"{(bridgeIsBrother ? "兄弟" : "姊妹")}{connector}{(isMale ? "兄弟" : "姊妹")}";
        }

        // Colloquial and others: the person, not the path.
        return $"{connector}{(isMale ? "兄弟" : "姊妹")}";
    }

    private String FormatSpouseSiblingSpouse(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        if (context == NamingContext.Official) return "配偶的兄弟姐妹的配偶";

        Boolean isFinalMale = info.Gender == PersonGender.Male;
        Boolean isOlder = info.AgeOrder == SiblingOrder.Older;
        
        if (isFinalMale) // Lian Jin (Husbands of sisters)
        {
            if (context == NamingContext.Colloquial)
            {
                string baseTerm = isTraditional ? "連襟" : "连襟";
                string siblingTerm = isOlder ? (isTraditional ? "姐夫" : "姐夫") : (isTraditional ? "妹夫" : "妹夫");
                string specificTerm = isOlder 
                    ? (isTraditional ? "大姨丈|大姑丈" : "大姨丈|大姑丈") 
                    : (isTraditional ? "小姨丈|小姑丈" : "小姨丈|小姑丈");
                return $"{baseTerm}|{siblingTerm}|{specificTerm}";
            }
            return isTraditional ? "襟兄弟" : "襟兄弟";
        }
        else // Zhou Li (Wives of brothers)
        {
            if (context == NamingContext.Colloquial)
            {
                string baseTerm = isTraditional ? "妯娌" : "妯娌";
                string siblingTerm = isOlder 
                    ? (isTraditional ? "嫂子|嫂嫂" : "嫂子|嫂嫂") 
                    : (isTraditional ? "弟妹|弟媳" : "弟妹|弟媳");
                return $"{baseTerm}|{siblingTerm}";
            }
            return isTraditional ? "妯娌" : "妯娌";
        }
    }

    private String FormatCoParentInLaw(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        if (context == NamingContext.Official) return "子女的配偶的父母";
        
        Boolean isMale = info.Gender == PersonGender.Male;
        if (isMale) return isTraditional ? "親家公" : "亲家公";
        return isTraditional ? "親家母" : "亲家母";
    }

    private String FormatSibling(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        if (context == NamingContext.Official) return BuildOfficialPathDescription(info, isTraditional);

        Boolean isMale = info.Gender == PersonGender.Male;
        
        if (info.IsSpouse)
        {
            Boolean siblingIsMale = !isMale;
            
            if (siblingIsMale)
            {
                if (info.AgeOrder == SiblingOrder.Older) return isTraditional ? "嫂嫂|嫂子" : "嫂嫂|嫂子"; 
                else if (info.AgeOrder == SiblingOrder.Younger) return isTraditional ? "弟媳|弟妹" : "弟媳|弟妹"; 
                else return isTraditional ? "嫂嫂|弟媳" : "嫂嫂|弟媳"; 
            }
            else
            {
                // NO 连襟 here. This is MY OWN sister's husband; 连襟 is the reciprocal between
                // the husbands of two sisters, which he and I are not — he is my 姐夫 and I am
                // his 內弟 / 姨妹. The female branch above never offered 妯娌 next to 嫂嫂 for
                // the same reason; the asymmetry was the bug.
                if (info.AgeOrder == SiblingOrder.Older) return "姐夫";
                else if (info.AgeOrder == SiblingOrder.Younger) return "妹夫";
                else return "姐夫|妹夫";
            }
        }

        if (isMale)
        {
            if (info.AgeOrder == SiblingOrder.Older) return isTraditional ? "哥哥|哥" : "哥哥|哥";
            else if (info.AgeOrder == SiblingOrder.Younger) return isTraditional ? "弟弟|弟" : "弟弟|弟";
            else return isTraditional ? "兄弟" : "兄弟"; 
        }
        else
        {
            if (info.AgeOrder == SiblingOrder.Older) return isTraditional ? "姐姐|姐" : "姐姐|姐";
            else if (info.AgeOrder == SiblingOrder.Younger) return isTraditional ? "妹妹|妹" : "妹妹|妹";
            else return isTraditional ? "姐妹" : "姐妹"; 
        }
    }

    private String FormatCousin(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        if (context == NamingContext.Official) return BuildOfficialPathDescription(info, isTraditional);

        // 1. Determine the bounded collateral branch family
        String prefix = GetCollateralBranchPrefix(info);
        
        Int32 gen = info.GenerationChange;
        Boolean isMale = info.Gender == PersonGender.Male;
        
        // 2. Handle Generation Gaps
        if (gen > 0) // Elder generation (e.g. F.F.OB.S -> Tang Shu)
        {
             // Determine Stem (Uncle/GrandUncle...)
             // If gen == 1: Shu/Bo...
             // If gen == 2: Grand...
             
             if (gen == 1)
             {
                 String core = info.IsSpouse
                     ? GetElderCollateralSpouseSuffix(info)
                     : GetElderCollateralSuffix(info);
                 String composed = $"{prefix}{core}";

                 // K16: the graded composite is computed, but its BASE may have layer
                 // variants (姨父→姨丈, 舅母→舅妈); carry them onto the composite so the
                 // everyday spelling 堂姨丈 survives as an alternate.
                 String baseVariants = Data.KinshipLexiconLayers.GetVariantSet(core);
                 if (!String.IsNullOrEmpty(baseVariants))
                 {
                     composed += "|" + String.Join('|', System.Linq.Enumerable.Select(baseVariants.Split('|'), v => prefix + v));
                 }

                 return composed;
             }

             // AscendStem is keyed by generation itself ([2]=祖 for grandparent level);
             // gen+1 over-deepened every deep collateral elder by one rung (姑表曾祖母
             // where both our own official line and mumuy agree on the 祖-level).
             var stems = isTraditional ? AscendStemHant : AscendStemHans;
             string stem = stems.ContainsKey(gen) ? stems[gen] : $"{gen - 1}世祖";
             string suffix = isMale ? "父" : "母";
             return $"{prefix}{stem}{suffix}";
        }
        else if (gen < 0) // Younger generation (e.g. F.OB.S.S -> Tang Zhi)
        {
             // Determine Parent Gender from Signature
             // Path: [P...] -> Sibling -> [D0, D1, ... Dn]
             // Target is Dn. Parent is Dn-1.
             // If DescendantCount == 1, then Parent is Sibling. (But gen < 0 implies DescendantCount > AncestorDepth)
             // If AncestorDepth=1, DescendantCount=2 -> Gen=-1. D0 is Parent.
             
             string[] path = info.DescendantPathSignature?.Split(',') ?? Array.Empty<string>();
             bool parentIsMale = true; // Default
             
             if (path.Length >= 2)
             {
                 parentIsMale = path[^2] == "S";
             }
             else
             {
                 // Path length 1. Parent is the Sibling.
                 // But Cousin logic usually implies AncestorDepth >= 1.
                 // If Sibling is the parent, we need Sibling's gender.
                 // Info.IsPaternal/SpouseSiblingIsMale... 
                 // Actually FormatCousin info usually comes from TryAnalyzeCollateral.
                 // Sibling gender is not directly stored in specific field, but inferred.
                 // Wait, IsPaternal in TryAnalyzeCollateral logic:
                 // "type = siblingIsMale ? Uncle : Aunt" (if desc=0)
                 // But for Cousin, we don't store Sibling Gender explicitly in a field named SiblingGender.
                 // However, we can infer it?
                 // If path length is 1, and gen < 0, it means AncestorDepth=0? No, that's SiblingDescendant.
                 // If AncestorDepth=1, DescendantCount=2 -> Gen=-1. Path len=2.
                 // So Path length is always >= 2 for Gen < 0 Cousins?
                 // Example: F.OB.S.S (Tang Zhi). Ancestor=1. Desc=2. Path="S,S". Len=2. Parent is S (Male).
                 // Example: F.OB.S (Tang Xiong). Ancestor=1. Desc=1. Path="S". Len=1. Gen=0. (Peer).
                 // So for Gen < 0, Path Len must be >= 2.
             }

             string baseTerm;
             if (parentIsMale)
             {
                 baseTerm = isTraditional ? "姪" : "侄";
             }
             else
             {
                 baseTerm = "甥"; // 表甥
             }
             
             // Depth stem
             int depth = Math.Abs(gen);
             // Depth 1: Zhi
             // Depth 2: Zhi Sun
             string stem = "";
             if (depth > 1)
             {
                 var stems = isTraditional ? DescendStemHant : DescendStemHans;
                 stem = stems.ContainsKey(depth) ? stems[depth] : $"{depth-1}代孫";
             }
             
             string genderSuffix = isMale ? (isTraditional ? "子" : "子") : (isTraditional ? "女" : "女");
             
             // Tang-Zhi-Sun-Nu
             return $"{prefix}{baseTerm}{stem}{genderSuffix}";
        }

        // gen == 0: Peer generation (Original logic)
        String peerSuffix = "";
        
        if (info.IsSpouse)
        {
            Boolean cousinIsMale = !isMale;
            if (cousinIsMale)
            {
                if (info.AgeOrder == SiblingOrder.Older) peerSuffix = "嫂";
                else peerSuffix = "弟媳"; 
            }
            else
            {
                if (info.AgeOrder == SiblingOrder.Older) peerSuffix = "姐夫";
                else peerSuffix = "妹夫";
            }
        }
        else
        {
            if (isMale)
            {
                peerSuffix = info.AgeOrder == SiblingOrder.Older ? "兄|哥" : "弟";
            }
            else
            {
                peerSuffix = info.AgeOrder == SiblingOrder.Older ? "姐" : "妹";
            }
        }
        
        return $"{prefix}{peerSuffix}";
    }

    private String FormatCollateral(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        if (context == NamingContext.Official) return BuildOfficialPathDescription(info, isTraditional);

        Boolean isSubjectMale = info.Gender == PersonGender.Male;
        PersonGender bloodGender = info.IsSpouse ? (isSubjectMale ? PersonGender.Female : PersonGender.Male) : info.Gender;
        Boolean bloodIsMale = bloodGender == PersonGender.Male;
        Boolean isOlder = info.AgeOrder == SiblingOrder.Older;

        String title = "";

        // K16: the standard title is computed here; the everyday/dialect words (伯伯, 姑妈,
        // 舅舅, 阿姨, 婶婶…) are looked-up vocabulary and come from the lexicon layers.
        if (info.IsPaternal)
        {
            if (bloodIsMale) // Father's Brother
            {
                title = !info.IsSpouse
                    ? (isOlder ? "伯父" : "叔父")
                    : (isOlder ? "伯母" : "婶母");
            }
            else // Father's Sister
            {
                title = !info.IsSpouse ? "姑母" : "姑父";
            }
        }
        else
        {
            if (bloodIsMale) // Mother's Brother
            {
                title = !info.IsSpouse ? "舅父" : "舅母";
            }
            else // Mother's Sister
            {
                title = !info.IsSpouse ? "姨母" : "姨父";
            }
        }

        if (context == NamingContext.Colloquial)
        {
            return Data.KinshipLexiconLayers.GetVariantSet(title);
        }

        if (context == NamingContext.Colloquial) return title.Contains("|") ? title : title;
        return title.Split('|')[0];
    }
// ...
    private String FormatGrandCollateral(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        if (context == NamingContext.Official) return BuildOfficialPathDescription(info, isTraditional);

        Int32 depth = Math.Abs(info.GenerationChange); 
        Boolean isSubjectMale = info.Gender == PersonGender.Male;
        PersonGender bloodRelativeGender = info.IsSpouse 
            ? (isSubjectMale ? PersonGender.Female : PersonGender.Male) 
            : info.Gender;
        Boolean bloodRelativeIsMale = bloodRelativeGender == PersonGender.Male;
        Boolean isOlder = info.AgeOrder == SiblingOrder.Older;

        String prefix = "";
        if (info.IsPaternal)
        {
            if (bloodRelativeIsMale) prefix = isOlder ? "伯" : "叔";
            else prefix = "姑";
        }
        else
        {
            if (bloodRelativeIsMale) prefix = "舅";
            else prefix = "姨";
        }

        if (context == NamingContext.Formal)
        {
            return BuildGrandCollateralStandard(info, depth, prefix, isSubjectMale);
        }
        else if (context == NamingContext.Colloquial)
        {
            // K16: every colloquial/dialect grand-collateral form used to be hard-coded here
            // (伯公|大伯公, 姑外婆, 舅公|舅爺…). Those are LOOKED-UP vocabulary, not computed
            // morphology, so they now come from the lexicon layers keyed by the standard form
            // this method computes above. Layers absent → empty, and the formal stays primary.
            return Data.KinshipLexiconLayers.GetVariantSet(BuildGrandCollateralStandard(info, depth, prefix, isSubjectMale));
        }
        return "";
    }

    /// <summary>
    /// Standard form for an elder collateral two or more generations up. The 外 marks the
    /// maternal line and belongs AFTER the 伯/叔/姑/舅/姨 letter — 伯外祖父, matching the
    /// morpheme machine's own composition (flavor + 外祖 + 父) in ChainShapeTermFormatter.
    /// It used to be emitted here as 外伯祖父, so one relation had two standard spellings
    /// depending on which chain the user typed (母→父→兄 vs 母→父→兄→兄).
    /// </summary>
    private static String BuildGrandCollateralStandard(KinshipSemanticInfo info, Int32 depth, String prefix, Boolean isSubjectMale)
    {
        String lineagePrefix = info.IsTopLevelPaternal ? "" : "外";
        // The ladder used to be `depth == 3 ? 曾祖 : 祖`, so EVERY tier above the great-grand
        // one — 高祖, 天祖, 烈祖 … — fell back to 祖 and a +4 relative was named as a +2. Same
        // stem table the lineal formatter and the morpheme machine already use, which also puts
        // this path back in step with ChainShapeTermFormatter's {flavor}{stem}{terminal}.
        String stem = depth <= 2
            ? "祖"
            : (AscendStemHant.TryGetValue(depth, out String? s) ? s : $"{depth - 1}世祖");
        String suffix = isSubjectMale ? "父" : "母";

        return $"{prefix}{lineagePrefix}{stem}{suffix}";
    }

    private static readonly String[] CollateralMaleDescHans = { "侄", "侄孙", "曾侄孙", "玄侄孙", "来侄孙", "晜侄孙", "仍侄孙", "云侄孙" };
    private static readonly String[] CollateralFemaleDescHans = { "侄女", "侄孙女", "曾侄孙女", "玄侄孙女", "来侄孙女", "晜侄孙女", "仍侄孙女", "云侄孙女" };
    private static readonly String[] CollateralMaleDescHant = { "姪", "姪孫", "曾姪孫", "玄姪孫", "來姪孫", "晜姪孫", "仍姪孫", "雲姪孫" };
    private static readonly String[] CollateralFemaleDescHant = { "姪女", "姪孫女", "曾姪孫女", "玄姪孫女", "來姪孫女", "晜姪孫女", "仍姪孫女", "雲姪孫女" };

    private static String BuildCollateralDescHans(Int32 level, Boolean isMale)
    {
        if (level <= 0) return isMale ? "侄" : "侄女";
        if (level <= CollateralMaleDescHans.Length) return isMale ? CollateralMaleDescHans[level - 1] : CollateralFemaleDescHans[level - 1];
        return isMale ? $"侄（第{level + 1}代）" : $"侄女（第{level + 1}代）";
    }

    private static String BuildCollateralDescHant(Int32 level, Boolean isMale)
    {
        if (level <= 0) return isMale ? "姪" : "姪女";
        if (level <= CollateralMaleDescHant.Length) return isMale ? CollateralMaleDescHant[level - 1] : CollateralFemaleDescHant[level - 1];
        return isMale ? $"姪（第{level + 1}代）" : $"姪女（第{level + 1}代）";
    }

    private static String BuildCollateralDescEnglish(Int32 level, Boolean isMale)
    {
        if (level <= 0) return isMale ? "nephew" : "niece";
        if (level == 1) return isMale ? "nephew" : "niece";
        if (level == 2) return isMale ? "grand-nephew" : "grand-niece";
        String suffix = isMale ? "nephew" : "niece";
        Int32 extra = level - 2;
        String prefix = String.Concat(System.Linq.Enumerable.Repeat("great-", extra));
        return $"{prefix}grand-{suffix}";
    }

    private static readonly String[] SororalMaleDescHans = { "外甥", "外甥孙", "外甥曾孙", "外甥玄孙", "外甥来孙" };
    private static readonly String[] SororalFemaleDescHans = { "外甥女", "外甥孙女", "外甥曾孙女", "外甥玄孙女", "外甥来孙女" };
    private static readonly String[] SororalMaleDescHant = { "外甥", "外甥孫", "外甥曾孫", "外甥玄孫", "外甥來孫" };
    private static readonly String[] SororalFemaleDescHant = { "外甥女", "外甥孫女", "外甥曾孫女", "外甥玄孫女", "外甥來孫女" };

    /// <summary>
    /// Spouse's sister's descendant line: 姑甥 / 姑甥孫 / 姑甥曾孫 …. The depth used to be
    /// dropped here — 姑甥 named the child, the grandchild and the great-great-grandchild alike,
    /// four generations under one word — while the brother-side branch beside it has always
    /// carried the ladder. Built off the sororal ladder so the two stay in step.
    /// </summary>
    private static String BuildSpouseSororalDesc(Int32 depth, Boolean isMale, Boolean isTraditional)
    {
        String sororal = isTraditional
            ? BuildSororalDescHant(depth, isMale)
            : BuildSororalDescHans(depth, isMale);

        // 外甥孫女 -> 姑甥孫女: the 姑 marks the spouse's sister as the pivot, replacing the 外
        // that marks a sister of my own.
        return sororal.StartsWith(isTraditional ? "外" : "外", StringComparison.Ordinal)
            ? "姑" + sororal[1..]
            : "姑" + sororal;
    }

    private static String BuildSororalDescHans(Int32 depth, Boolean isMale)
    {
        int index = depth - 1;
        if (index < SororalMaleDescHans.Length) return isMale ? SororalMaleDescHans[index] : SororalFemaleDescHans[index];
        return isMale ? $"外甥（第{depth}代）" : $"外甥女（第{depth}代）";
    }

    private static String BuildSororalDescHant(Int32 depth, Boolean isMale)
    {
        int index = depth - 1;
        if (index < SororalMaleDescHant.Length) return isMale ? SororalMaleDescHant[index] : SororalFemaleDescHant[index];
        return isMale ? $"外甥（第{depth}代）" : $"外甥女（第{depth}代）";
    }

    private static readonly Dictionary<Int32, String> AscendStemHans = new()
    {
        [2] = "祖", [3] = "曾祖", [4] = "高祖", [5] = "天祖", [6] = "烈祖", [7] = "太祖", [8] = "远祖", [9] = "鼻祖", [10] = "开祖", [11] = "始祖", [12] = "先祖"
    };
    
    private static readonly Dictionary<Int32, String> AscendStemHant = new()
    {
        [2] = "祖", [3] = "曾祖", [4] = "高祖", [5] = "天祖", [6] = "烈祖", [7] = "太祖", [8] = "遠祖", [9] = "鼻祖", [10] = "開祖", [11] = "始祖", [12] = "先祖"
    };

    private static readonly Dictionary<Int32, String> DescendStemHans = new()
    {
        [2] = "孙", [3] = "曾孙", [4] = "玄孙", [5] = "来孙", [6] = "晜孙", [7] = "仍孙", [8] = "云孙"
    };

    private static readonly Dictionary<Int32, String> DescendStemHant = new()
    {
        [2] = "孫", [3] = "曾孫", [4] = "玄孫", [5] = "來孫", [6] = "晜孫", [7] = "仍孫", [8] = "雲孫"
    };

    private static String[] GetDescendantPath(KinshipSemanticInfo info)
    {
        if (String.IsNullOrWhiteSpace(info.DescendantPathSignature))
        {
            return Array.Empty<String>();
        }

        return info.DescendantPathSignature.Split(',', StringSplitOptions.RemoveEmptyEntries);
    }

    private static Boolean HasLeadingExternalDescendantTransition(String[] path)
        => path.Length > 0 && path[0] == "D";

    private static Boolean HasInnerExternalDescendantTransition(String[] path, Boolean excludeLastStep)
    {
        if (path.Length <= 2)
        {
            return false;
        }

        Int32 endExclusive = excludeLastStep ? path.Length - 1 : path.Length;
        if (endExclusive <= 2)
        {
            return false;
        }

        return path[endExclusive - 2] == "D";
    }

    private static String DecorateDescendantStemWithExternalMarker(String stem, Boolean hasInnerExternal, Boolean isTraditional)
    {
        if (!hasInnerExternal)
        {
            return stem;
        }

        String grandChar = isTraditional ? "孫" : "孙";
        Int32 grandIndex = stem.LastIndexOf(grandChar, StringComparison.Ordinal);
        if (grandIndex <= 0)
        {
            return stem;
        }

        return $"{stem[..grandIndex]}外{stem[grandIndex..]}";
    }

    private static String GetCollateralBranchPrefix(KinshipSemanticInfo info)
    {
        if (info.IsStrictlyPaternal)
        {
            return "堂";
        }

        if (info.IsPaternal)
        {
            return "姑表";
        }

        return info.CollateralSiblingIsMale ? "舅表" : "姨表";
    }

    private static String GetElderCollateralSuffix(KinshipSemanticInfo info)
    {
        Boolean isMale = info.Gender == PersonGender.Male;
        if (info.IsTopLevelPaternal)
        {
            if (isMale)
            {
                return info.AgeOrder == SiblingOrder.Older ? "伯" : "叔";
            }

            return "姑";
        }

        return isMale ? "舅" : "姨";
    }

    private static String GetElderCollateralSpouseSuffix(KinshipSemanticInfo info)
    {
        if (info.IsTopLevelPaternal)
        {
            if (info.Gender == PersonGender.Male)
            {
                return "姑丈";
            }

            return info.AgeOrder == SiblingOrder.Older ? "伯母" : "婶母";
        }

        return info.Gender == PersonGender.Male ? "姨父" : "舅母";
    }

    private String FormatAncestor(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        Int32 depth = Math.Abs(info.GenerationChange);
        Boolean isMale = info.Gender == PersonGender.Male;
        Boolean isMaternal = !info.IsPaternal; 

        if (context == NamingContext.Official) return BuildOfficialPathDescription(info, isTraditional);

        String suffix = isMale ? "父" : "母";
        String prefix = isMaternal ? "外" : "";
        
        if (depth == 1)
        {
            // K16: 爸爸/老爸/爹/妈妈/老妈/娘 are looked-up vocabulary → lexicon layers.
            if (context == NamingContext.Colloquial) return Data.KinshipLexiconLayers.GetVariantSet(isMale ? "父親" : "母親");


            // Refined logic for Adoptive/Step parents (養父 vs 養父親)
            if (info.Origin == KinshipOrigin.Adoptive || info.Origin == KinshipOrigin.Step)
            {
                String mod = info.Origin == KinshipOrigin.Adoptive 
                    ? (isTraditional ? "養" : "养") 
                    : (isTraditional ? "繼" : "继");
                return mod + suffix;
            }

            return isMale ? (isTraditional ? "父親" : "父亲") : (isTraditional ? "母親" : "母亲");
        }

        var stems = isTraditional ? AscendStemHant : AscendStemHans;
        String stem = stems.ContainsKey(depth) ? stems[depth] : $"{depth-1}世祖";
        
        String formalTitle = $"{prefix}{stem}{suffix}";

        if (info.Origin == KinshipOrigin.Adoptive)
        {
            formalTitle = (isTraditional ? "養" : "养") + formalTitle;
        }

        // K16: ancestor colloquials (外公/爷爷/太爷爷…) come from the lexicon layers, keyed
        // by the standard form; the adoptive 养-prefix is a computed modifier and has no
        // everyday variant, so prefixed titles simply find no layer entry.
        if (context == NamingContext.Colloquial)
        {
            return Data.KinshipLexiconLayers.GetVariantSet(formalTitle);
        }

        return formalTitle;
    }

    private String FormatDescendant(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        Int32 depth = Math.Abs(info.GenerationChange);
        Boolean isMale = info.Gender == PersonGender.Male;
        String[] descendantPath = GetDescendantPath(info);
        Boolean hasLeadingExternal = descendantPath.Length > 0 ? HasLeadingExternalDescendantTransition(descendantPath) : !info.IsPaternal;
        Boolean hasInnerExternal = HasInnerExternalDescendantTransition(descendantPath, info.IsSpouse);

        if (context == NamingContext.Official) return BuildOfficialPathDescription(info, isTraditional);

        if (info.IsSpouse && depth == 1)
        {
            if (isMale) return isTraditional ? "女婿" : "女婿";
            else return isTraditional ? "兒媳" : "儿媳";
        }

        if (depth == 1)
        {
             if (context == NamingContext.Colloquial) return isMale ? "儿子" : "闺女"; 
             
             // Refined logic for Adoptive children
             if (info.Origin == KinshipOrigin.Adoptive)
             {
                 String mod = isTraditional ? "養" : "养";
                 return mod + (isMale ? (isTraditional ? "子" : "子") : (isTraditional ? "女" : "女"));
             }

             return isMale ? (isTraditional ? "兒子" : "儿子") : (isTraditional ? "女兒" : "女儿");
        }

        var stems = isTraditional ? DescendStemHant : DescendStemHans;
        String stem = stems.ContainsKey(depth) ? stems[depth] : $"{depth-1}代孫";
        stem = DecorateDescendantStemWithExternalMarker(stem, hasInnerExternal, isTraditional);

        String prefix = hasLeadingExternal ? "外" : "";
        
        if (info.IsSpouse)
        {
            String spouseSuffix = isMale ? "婿" : (isTraditional ? "媳" : "媳"); 
            return $"{prefix}{stem}{spouseSuffix}";
        }

        String baseTitle = stem;
        if (depth >= 1)
        {
            if (isMale)
            {
                if (depth == 1) baseTitle = "儿";
                else baseTitle = stem + (isTraditional ? "子" : "子"); 
            }
            else
            {
                if (depth == 1) baseTitle = "女";
                else baseTitle = stem + (isTraditional ? "女" : "女");
            }
        }
        
        string result = $"{prefix}{baseTitle}";
        if (info.Origin == KinshipOrigin.Adoptive)
        {
            result = (isTraditional ? "養" : "养") + result;
        }
        return result;
    }

    private String FormatSpouseLineal(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        if (context == NamingContext.Official) return BuildOfficialPathDescription(info, isTraditional);

        Int32 depth = Math.Abs(info.GenerationChange);
        Boolean isMale = info.Gender == PersonGender.Male; 
        Boolean isPaternalSide = info.IsPaternal; 

        if (depth == 1)
        {
            // K16: the spouse's parents are non-derivable standard lexemes — the base layer
            // owns them (岳父/公公 by ego side), the variant layers own 老爷/丈母娘/丈人.
            String? standard = Data.KinshipLexiconLayers.TryGetStandardLexeme(
                isMale ? "SP.F" : "SP.M",
                isPaternalSide ? PersonGender.Female : PersonGender.Male);
            standard ??= isPaternalSide ? (isMale ? "公公" : "婆婆") : (isMale ? "岳父" : "岳母");

            if (context == NamingContext.Colloquial)
            {
                return Data.KinshipLexiconLayers.GetVariantSet(standard);
            }

            return standard;
        }
        else
        {
            var stems = isTraditional ? AscendStemHant : AscendStemHans;
            String stem = stems.ContainsKey(depth) ? stems[depth] : $"{depth-1}世祖";
            String suffix = isMale ? "父" : "母";
            String baseTitle = stem + suffix;

            if (isPaternalSide)
            {
                if (context == NamingContext.Colloquial)
                {
                    return Data.KinshipLexiconLayers.GetVariantSet(baseTitle);
                }
                return baseTitle;
            }
            else
            {
                if (context == NamingContext.Colloquial)
                {
                    if (depth == 2) return isMale ? "太岳父|岳祖父" : "太岳母|岳祖母";
                }
                
                String prefix = isTraditional ? "岳" : "岳";
                return prefix + baseTitle;
            }
        }
    }

    private String FormatSpouseCollateral(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        if (context == NamingContext.Official) return BuildOfficialPathDescription(info, isTraditional);

        Boolean isMale = info.Gender == PersonGender.Male; 
        Boolean isPaternalSide = info.IsPaternal; 
        Boolean isOlder = info.AgeOrder == SiblingOrder.Older;

        if (info.RelationType == KinshipRelationType.SpouseSibling)
        {
            if (info.IsSpouse)
            {
                Boolean siblingIsMale = !isMale;
                
                if (isPaternalSide) 
                {
                    if (siblingIsMale) return isOlder ? "嫂子|嫂嫂" : "弟妹|弟媳";
                    else return isOlder ? "姐夫" : "妹夫";
                }
                else
                {
                    if (siblingIsMale) return isOlder ? "舅嫂|大舅嫂" : "弟妹|舅弟媳";
                    // Wife's sister's husband. 姐夫 is what my WIFE calls him; between him and
                    // me the relation is 連襟, addressed 襟兄 / 襟弟. Leading with 姐夫 borrowed
                    // her term wholesale — the male-sibling line right above does not do that
                    // (it says 舅嫂, not her 嫂子). Same contract as FormatSpouseSiblingSpouse,
                    // which names this very relation when the other analyzer claims the chain.
                    if (context == NamingContext.Colloquial) return isOlder ? "连襟|姐夫" : "连襟|妹夫";
                    return isOlder ? "襟兄" : "襟弟";
                }
            }

            if (isPaternalSide) 
            {
                if (isMale) return isOlder ? (isTraditional ? "大伯子" : "大伯子") : (isTraditional ? "小叔子" : "小叔子");
                else return isOlder ? (isTraditional ? "大姑子" : "大姑子") : (isTraditional ? "小姑子" : "小姑子");
            }
            else 
            {
                if (isMale) return isOlder ? (isTraditional ? "大舅子|大舅哥" : "大舅子|大舅哥") : (isTraditional ? "小舅子" : "小舅子");
                else return isOlder ? (isTraditional ? "大姨子|大姨姐" : "大姨子|大姨姐") : (isTraditional ? "小姨子|小姨妹" : "小姨子");
            }
        }
        // K7: retired the "TODO: SpouseCollateral" placeholder (path unreachable via current rules).
        return isTraditional ? "配偶的旁系親屬" : "配偶的旁系亲属";
    }

    private String FormatSpouseSiblingChild(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        if (context == NamingContext.Official) return BuildOfficialPathDescription(info, isTraditional);

        Boolean isMale = info.Gender == PersonGender.Male; 
        Boolean isPaternalSide = info.IsPaternal; 
        Boolean isSiblingMale = info.AgeOrder == SiblingOrder.Older; 
        Int32 depth = Math.Abs(info.GenerationChange);

        if (isPaternalSide) 
        {
            if (isSiblingMale) 
            {
                return isTraditional 
                    ? BuildCollateralDescHant(depth, isMale) 
                    : BuildCollateralDescHans(depth, isMale);
            }
            else // Husband's Sister's Child
            {
                if (context == NamingContext.Formal)
                {
                    return BuildSpouseSororalDesc(depth, isMale, isTraditional);
                }
                else // Colloquial
                {
                    return isTraditional
                        ? BuildSororalDescHant(depth, isMale)
                        : BuildSororalDescHans(depth, isMale); // This is "外甥女"
                }
            }
        }
        else // Wife's side
        {
            if (isSiblingMale) 
            {
                String baseTerm = isTraditional 
                    ? BuildCollateralDescHant(depth, isMale) 
                    : BuildCollateralDescHans(depth, isMale);
                
                String prefix = isTraditional ? "內" : "内";
                return prefix + baseTerm;
            }
            else // Wife's Sister's Child
            {
                if (context == NamingContext.Formal)
                {
                    // Operator ruling: 姑甥 / 姑甥女 regardless of WHICH side the spouse's sister
                    // sits on — that ruling is kept. It says nothing about depth, and the flat
                    // return used to apply it to the grandchild and beyond as well.
                    return BuildSpouseSororalDesc(depth, isMale, isTraditional);
                }
                else // Colloquial
                {
                    return isTraditional 
                        ? BuildSororalDescHant(depth, isMale) 
                        : BuildSororalDescHans(depth, isMale); // This is "外甥女"
                }
            }
        }
    }

    private String FormatSiblingDescendant(KinshipSemanticInfo info, NamingContext context, Boolean isTraditional)
    {
        if (context == NamingContext.Official) return BuildOfficialPathDescription(info, isTraditional);

        Int32 depth = Math.Abs(info.GenerationChange); 
        Boolean isMale = info.Gender == PersonGender.Male;
        Boolean isBrotherSide = info.IsPaternal; 

        if (isBrotherSide)
        {
            if (depth == 1)
            {
                if (info.IsSpouse) return isMale ? (isTraditional ? "姪女婿" : "侄女婿") : (isTraditional ? "姪媳婦" : "侄媳妇");
                return isMale ? (isTraditional ? "姪子" : "侄子") : (isTraditional ? "姪女" : "侄女");
            }
            
            String baseTitle = isTraditional ? BuildCollateralDescHant(depth, isMale) : BuildCollateralDescHans(depth, isMale);
            if (info.IsSpouse)
            {
                return baseTitle + (isMale ? "婿" : (isTraditional ? "媳" : "媳"));
            }
            return baseTitle;
        }
        else
        {
            if (depth == 1)
            {
                if (info.IsSpouse) return isMale ? "外甥婿" : "外甥媳";
                return isMale ? "外甥" : "外甥女";
            }
            
            String baseTitle = isTraditional ? BuildSororalDescHant(depth, isMale) : BuildSororalDescHans(depth, isMale);
            if (info.IsSpouse)
            {
                return baseTitle + (isMale ? "婿" : (isTraditional ? "媳" : "媳"));
            }
            return baseTitle;
        }
    }

    private String BuildOfficialPathDescription(KinshipSemanticInfo info, Boolean isTraditional)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(isTraditional ? "自己" : "自己");

        String arrow = isTraditional ? "→" : "→";

        if (info.GenerationChange > 0) 
        {
            sb.Append(arrow).Append($"第{info.GenerationChange}代祖輩(");
            sb.Append(info.IsPaternal ? "父系" : "母系");
            sb.Append(info.Gender == PersonGender.Male ? "男" : "女").Append(")");
        }
        else if (info.GenerationChange < 0) 
        {
            sb.Append(arrow).Append($"第{Math.Abs(info.GenerationChange)}代晚輩(");
            sb.Append(info.IsPaternal ? "子系" : "女系"); 
            sb.Append(info.Gender == PersonGender.Male ? "男" : "女").Append(")");
        }
        
        if (info.RelationType == KinshipRelationType.Uncle || info.RelationType == KinshipRelationType.Aunt ||
            info.RelationType == KinshipRelationType.GrandUncle || info.RelationType == KinshipRelationType.GrandAunt ||
            info.RelationType == KinshipRelationType.Sibling ||
            info.RelationType == KinshipRelationType.CousinTang || info.RelationType == KinshipRelationType.CousinBiao)
        {
            sb.Append(arrow);
            PersonGender bloodRelativeGender = info.IsSpouse 
                ? (info.Gender == PersonGender.Male ? PersonGender.Female : PersonGender.Male) 
                : info.Gender;
            Boolean bloodRelativeIsMale = bloodRelativeGender == PersonGender.Male;
            
            if (info.RelationType == KinshipRelationType.Sibling || 
                info.RelationType == KinshipRelationType.CousinTang || info.RelationType == KinshipRelationType.CousinBiao)
            {
                 if (bloodRelativeIsMale)
                 {
                     if (info.AgeOrder == SiblingOrder.Older) sb.Append("兄");
                     else if (info.AgeOrder == SiblingOrder.Younger) sb.Append("弟");
                     else sb.Append("兄弟");
                 }
                 else
                 {
                     if (info.AgeOrder == SiblingOrder.Older) sb.Append("姐");
                     else if (info.AgeOrder == SiblingOrder.Younger) sb.Append("妹");
                     else sb.Append("姐妹");
                 }
            }
            else
            {
                 sb.Append(bloodRelativeIsMale ? "兄弟" : "姐妹");
            }
        }

        if (info.RelationType == KinshipRelationType.SpouseParent)
        {
            sb.Append(arrow).Append("配偶");
            sb.Append(arrow).Append($"第{Math.Abs(info.GenerationChange)}代祖輩(");
            sb.Append(info.Gender == PersonGender.Male ? "男" : "女").Append(")");
            return sb.ToString();
        }

        if (info.RelationType == KinshipRelationType.SpouseSibling)
        {
            sb.Append(arrow).Append("配偶");
            sb.Append(arrow);
            Boolean isMale = info.Gender == PersonGender.Male;
            if (info.IsSpouse) 
            {
                Boolean siblingMale = !isMale;
                if (siblingMale) sb.Append(info.AgeOrder == SiblingOrder.Older ? "兄" : "弟");
                else sb.Append(info.AgeOrder == SiblingOrder.Older ? "姐" : "妹");
                sb.Append(arrow).Append("配偶");
            }
            else 
            {
                if (isMale) sb.Append(info.AgeOrder == SiblingOrder.Older ? "兄" : "弟");
                else sb.Append(info.AgeOrder == SiblingOrder.Older ? "姐" : "妹");
            }
            return sb.ToString();
        }

        if (info.RelationType == KinshipRelationType.SpouseSiblingChild)
        {
            sb.Append(arrow).Append("配偶");
            sb.Append(arrow).Append("兄弟姐妹"); 
            
            int depth = Math.Abs(info.GenerationChange);
            if (depth == 1)
            {
                sb.Append(arrow).Append("子"); 
            }
            else
            {
                sb.Append(arrow).Append($"第{depth}代晚輩");
            }
            return sb.ToString();
        }

        if (info.RelationType == KinshipRelationType.SiblingDescendant)
        {
            sb.Append(arrow);
            Boolean isBrotherSide = info.IsPaternal;
            sb.Append(isBrotherSide ? "兄弟" : "姐妹");
            
            int depth = Math.Abs(info.GenerationChange);
            if (depth == 1)
            {
                sb.Append(arrow).Append("子"); 
            }
            else
            {
                sb.Append(arrow).Append($"第{depth}代晚輩");
            }
            return sb.ToString();
        }

        if (info.IsSpouse && (info.RelationType != KinshipRelationType.Uncle && info.RelationType != KinshipRelationType.Aunt &&
                              info.RelationType != KinshipRelationType.GrandUncle && info.RelationType != KinshipRelationType.GrandAunt))
        {
            sb.Append(arrow).Append("配偶");
        }

        return sb.ToString();
    }

    private String BuildOfficialDescription(KinshipSemanticInfo info, Boolean isTraditional)
    {
        var sb = new System.Text.StringBuilder();
        
        if (info.RelationType == KinshipRelationType.GrandUncle || info.RelationType == KinshipRelationType.GrandAunt ||
            info.RelationType == KinshipRelationType.GreatGrandUncle || info.RelationType == KinshipRelationType.GreatGrandAunt)
        {
            sb.Append(isTraditional ? "祖" : "祖");
            sb.Append(info.IsPaternal ? "父" : "母");
            sb.Append("的");
        }

        Boolean bloodMale = info.IsSpouse ? info.Gender == PersonGender.Female : info.Gender == PersonGender.Male;
        
        if (bloodMale) sb.Append(info.AgeOrder == SiblingOrder.Older ? "哥哥" : "弟弟");
        else sb.Append(info.AgeOrder == SiblingOrder.Older ? "姐姐" : "妹妹");

        if (info.IsSpouse)
        {
            sb.Append("的配偶");
        }

        return sb.ToString();
    }

    private String FormatEnglish(KinshipSemanticInfo info, NamingContext context)
    {
        Boolean isMale = info.Gender == PersonGender.Male;
        Int32 depth = Math.Abs(info.GenerationChange);

        // 1. Lineal Ancestors
        if (info.RelationType is KinshipRelationType.Parent or KinshipRelationType.GrandParent or 
            KinshipRelationType.GreatGrandParent or KinshipRelationType.Ancestor)
        {
            if (info.Origin == KinshipOrigin.Adoptive) return isMale ? "Adoptive Father" : "Adoptive Mother"; // Simplified for depth=1
            if (info.Origin == KinshipOrigin.Step) return isMale ? "Stepfather" : "Stepmother";

            string baseTitle = isMale ? "Father" : "Mother";
            if (depth == 1) return baseTitle;
            if (depth == 2) return "Grand" + baseTitle.ToLowerInvariant();
            return string.Concat(System.Linq.Enumerable.Repeat("Great-", depth - 2)) + "Grand" + baseTitle.ToLowerInvariant();
        }

        // 2. Lineal Descendants
        if (info.RelationType is KinshipRelationType.Child or KinshipRelationType.GrandChild or 
            KinshipRelationType.GreatGrandChild or KinshipRelationType.Descendant)
        {
            if (info.Origin == KinshipOrigin.Adoptive) return isMale ? "Adoptive Son" : "Adoptive Daughter";
            
            string baseTitle = isMale ? "Son" : "Daughter";
            if (depth == 1) return baseTitle;
            if (depth == 2) return "Grand" + baseTitle.ToLowerInvariant();
            return string.Concat(System.Linq.Enumerable.Repeat("Great-", depth - 2)) + "Grand" + baseTitle.ToLowerInvariant();
        }

        // 3. Siblings
        if (info.RelationType == KinshipRelationType.Sibling)
        {
            if (info.IsSpouse) // Sibling-in-law via spouse's sibling
            {
                // Sister's Husband -> Brother-in-law
                // Brother's Wife -> Sister-in-law
                // Spouse's Brother -> Brother-in-law
                // Spouse's Sister -> Sister-in-law
                return isMale ? "Brother-in-law" : "Sister-in-law";
            }
            
            if (info.AgeOrder == SiblingOrder.Older) return isMale ? "Older Brother" : "Older Sister";
            if (info.AgeOrder == SiblingOrder.Younger) return isMale ? "Younger Brother" : "Younger Sister";
            return isMale ? "Brother" : "Sister";
        }

        // 4. Uncles / Aunts
        if (info.RelationType is KinshipRelationType.Uncle or KinshipRelationType.Aunt)
        {
            if (info.IsSpouse) return isMale ? "Uncle" : "Aunt"; // Uncle-in-law is usually just Aunt/Uncle or Aunt-in-law
            return isMale ? "Uncle" : "Aunt";
        }
        
        // 5. Grand Uncles / Aunts
        if (info.RelationType is KinshipRelationType.GrandUncle or KinshipRelationType.GrandAunt)
        {
            return isMale ? "Great-Uncle" : "Great-Aunt";
        }

        // 6. Nephews / Nieces
        if (info.RelationType is KinshipRelationType.Nephew or KinshipRelationType.Niece or KinshipRelationType.SiblingDescendant)
        {
            string suffix = isMale ? "Nephew" : "Niece";
            string title;
            if (depth == 1) title = suffix;
            else if (depth == 2) title = "Grand" + suffix.ToLowerInvariant();
            else title = string.Concat(System.Linq.Enumerable.Repeat("Great-", depth - 2)) + "Grand" + suffix.ToLowerInvariant();

            return info.IsSpouse ? title + "-in-law" : title;
        }

        // 7. Cousins
        if (info.RelationType is KinshipRelationType.CousinTang or KinshipRelationType.CousinBiao)
        {
            // Simple English logic: just "Cousin". 
            // Advanced: "First Cousin", "First Cousin once removed".
            if (info.GenerationChange == 0) return "Cousin";
            if (info.GenerationChange == 1) return "Cousin (Elder)";
            if (info.GenerationChange == -1) return "Cousin (Younger)";
            return "Cousin";
        }

        // 8. In-Laws
        if (info.RelationType == KinshipRelationType.SpouseParent)
        {
            // Spouse's Parent
            string baseTitle = isMale ? "Father" : "Mother";
            if (depth == 1) return baseTitle + "-in-law";
            if (depth == 2) return "Grand" + baseTitle.ToLowerInvariant() + "-in-law";
            return "Ancestor-in-law";
        }

        if (info.RelationType == KinshipRelationType.SpouseSibling)
        {
            return isMale ? "Brother-in-law" : "Sister-in-law";
        }
        
        if (info.RelationType == KinshipRelationType.SiblingSpouseParent)
        {
            // Sibling's Spouse's Parent -> Usually no specific term, maybe "sibling's father-in-law"
            // Or just "Relative"
            return "Relative (In-law)";
        }

        if (info.RelationType == KinshipRelationType.CoParentInLaw)
        {
            // Child's Spouse's Parent -> Co-parent-in-law
            return "Co-parent-in-law"; // Rarely used, but technically correct translation of Qin Jia
        }

        return "Relative";
    }
}
