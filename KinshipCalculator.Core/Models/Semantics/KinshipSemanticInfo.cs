using System;

namespace KinshipCalculator.Core.Models.Semantics;

public sealed record KinshipSemanticInfo
{
    public KinshipRelationType RelationType { get; init; }
    public PersonGender Gender { get; init; }
    public Int32 GenerationChange { get; init; } // +1 for parent, -1 for child
    
    // Modifiers
    public Boolean IsSpouse { get; init; } // e.g. Aunt's Spouse (Uncle-in-law)
    public Boolean IsPaternal { get; init; } // True = Father's side, False = Mother's side
    public Boolean IsTopLevelPaternal { get; init; } // Added to distinguish multi-generational lineages (e.g. Maternal Grandfather's Sister)
    public Boolean IsStrictlyPaternal { get; init; } // True if the collateral split point is Father + Brother. Defines Tang vs non-Tang in the bounded rule family.
    public Boolean CollateralSiblingIsMale { get; init; } // The gender of the collateral pivot sibling at the split point
    public Boolean SpouseSiblingIsMale { get; init; } // True if spouse's sibling is male, False if female
    public SiblingOrder SpouseSiblingAgeOrder { get; init; } // Older/Younger for the spouse's sibling
    public KinshipOrigin Origin { get; init; } = KinshipOrigin.Biological;
    public String? DescendantPathSignature { get; init; } // Encodes descendant steps (e.g., "S,D,S") for precise naming
    public Int32 InitialDescendantCount { get; init; } // Number of descendants before the spouse link
    public Boolean IsMaternal => !IsPaternal;
    
    public SiblingOrder AgeOrder { get; init; } // Older/Younger relative to the parent/pivot

    public static KinshipSemanticInfo Empty { get; } = new() { RelationType = KinshipRelationType.Unknown };
}

public enum SiblingOrder
{
    Unknown = 0,
    Older,
    Younger
}
