using System;
using System.Collections.Generic;
using System.Linq;

using KinshipCalculator.Core.Models;
using KinshipCalculator.Core.Models.Semantics;

namespace KinshipCalculator.Core.Services.Formatting;

/// <summary>
/// Assembles formatter output into result slots. Contract (K16, 2026-07-20): Label carries
/// the STANDARD-Chinese term, AlternateLabel carries every layer variant (colloquial,
/// northern, southern, user-supplied) plus remaining formal variants; OfficialDescription is
/// handled by the caller. Variant sets use '|' as the separator in both directions.
/// Superseded contract (K8): Label used to carry the daily/colloquial term, which promoted
/// whichever dialect happened to be hard-coded in the engine.
/// </summary>
public static class NameSlotAssembler
{
    /// <summary>
    /// Deep-affinal composite families whose canonical term IS the book-register 姻/眷
    /// compound (K4: 姻-series accepted with the 書面/禮帖 tag). They have no daily word —
    /// the colloquial slot only carries descriptive paraphrases — so the formal compound
    /// stays primary and the paraphrase set stays in the alternate slot.
    /// </summary>
    private static readonly HashSet<KinshipRelationType> FormalPrimaryTypes = new()
    {
        KinshipRelationType.DescendantSpouseSibling,
        KinshipRelationType.CollateralDescendantSpouseSiblingDescendant,
        KinshipRelationType.SiblingSpouseSibling,
        // SpouseSiblingSpouse stays OUT: 连襟/妯娌 are genuine daily words, not book compounds.
        KinshipRelationType.CollateralDescendantSpouseSibling,
        KinshipRelationType.SiblingSpouseSiblingChild,
        KinshipRelationType.SiblingSpouseParent,
        KinshipRelationType.SpouseSiblingChild,
        KinshipRelationType.PaternalGrandparentSpouseMaternalGrandparent,
        KinshipRelationType.SiblingCoParentInLaw,
        KinshipRelationType.CollateralSpouseParent,
        KinshipRelationType.CollateralSpouseSibling
    };

    public static (LocalizedText Label, LocalizedText? Alternate) BuildSlotsFor(
        KinshipRelationType relationType,
        string formalHans, string formalHant, string formalEn,
        string colloquialHans, string colloquialHant, string colloquialEn)
    {
        if (FormalPrimaryTypes.Contains(relationType))
        {
            var label = KinshipScriptConverter.Normalize(new LocalizedText(formalHans, formalHant, formalEn));
            LocalizedText? alternate = null;
            if (!string.IsNullOrWhiteSpace(colloquialHans) || !string.IsNullOrWhiteSpace(colloquialHant) || !string.IsNullOrWhiteSpace(colloquialEn))
            {
                alternate = KinshipScriptConverter.Normalize(new LocalizedText(colloquialHans, colloquialHant, colloquialEn));
            }

            return (label, alternate);
        }

        return BuildDailySlots(formalHans, formalHant, formalEn, colloquialHans, colloquialHant, colloquialEn);
    }

    public static (LocalizedText Label, LocalizedText? Alternate) BuildDailySlots(
        string formalHans, string formalHant, string formalEn,
        string colloquialHans, string colloquialHant, string colloquialEn)
    {
        (string labelHans, string altHans) = SplitPreferred(colloquialHans, formalHans);
        (string labelHant, string altHant) = SplitPreferred(colloquialHant, formalHant);
        (string labelEn, string altEn) = SplitPreferred(colloquialEn, formalEn);

        labelHans = KinshipScriptConverter.ToHans(labelHans);
        altHans = KinshipScriptConverter.ToHans(altHans);
        labelHant = KinshipScriptConverter.ToHant(labelHant);
        altHant = KinshipScriptConverter.ToHant(altHant);

        var label = new LocalizedText(labelHans, labelHant, labelEn);

        if (string.IsNullOrWhiteSpace(altHans) && string.IsNullOrWhiteSpace(altHant) && string.IsNullOrWhiteSpace(altEn))
        {
            return (label, null);
        }

        return (label, new LocalizedText(altHans, altHant, altEn));
    }

    private static (string Primary, string Alternates) SplitPreferred(string colloquialSet, string formalSet)
    {
        List<string> colloquial = SplitVariants(colloquialSet);
        List<string> formal = SplitVariants(formalSet);

        // K16 contract change (operator decision, 2026-07-20): the STANDARD-Chinese form is
        // primary. It used to be colloquial[0], which silently promoted whichever dialect an
        // early agent had hard-coded (southern 伯公 outranking standard 伯祖父). Layer
        // variants — colloquial, northern, southern, user-supplied — now follow as alternates.
        string primary = formal.Count > 0 ? formal[0] : (colloquial.Count > 0 ? colloquial[0] : string.Empty);

        var alternates = new List<string>();
        foreach (string variant in colloquial.Concat(formal.Skip(1)))
        {
            if (!string.Equals(variant, primary, StringComparison.Ordinal) && !alternates.Contains(variant, StringComparer.Ordinal))
            {
                alternates.Add(variant);
            }
        }

        return (primary, string.Join('|', alternates));
    }

    private static List<string> SplitVariants(string set)
    {
        if (string.IsNullOrWhiteSpace(set))
        {
            return new List<string>();
        }

        return set.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}
