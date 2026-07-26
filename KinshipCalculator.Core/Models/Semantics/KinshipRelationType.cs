namespace KinshipCalculator.Core.Models.Semantics;

public enum KinshipRelationType
{
    Unknown = 0,
    Self,
    Parent,
    Child,
    Sibling,
    Spouse,
    
    // Ancestors
    GrandParent,
    GreatGrandParent,
    Ancestor, // Generic for deeper levels

    // Descendants
    GrandChild,
    GreatGrandChild,
    Descendant, // Generic

    // Collateral (Parents' Siblings)
    Uncle, // Father's Brother / Mother's Brother
    Aunt,  // Father's Sister / Mother's Sister
    GrandUncle,
    GrandAunt,
    GreatGrandUncle,
    GreatGrandAunt,

    // Collateral Descendants (Siblings' Children)
    SiblingDescendant, // Generic for Nephew/Niece and deeper
    Nephew,
    Niece,
    GrandNephew,
    GrandNiece,

    // Affinal (In-Laws)
    SpouseParent, // Partner's Parent (e.g. Father-in-law)
    SpouseSibling, // Partner's Sibling (e.g. Brother-in-law)
    SpouseSiblingChild, // Partner's Sibling's Child (e.g. Nephew-in-law)
    CoParentInLaw, // Child's Spouse's Parent (e.g. Qing Jia)
    DescendantSpouseSibling, // Child's Spouse's Sibling (e.g. Yin Zhi)
    CollateralDescendantSpouseSiblingDescendant, // Sibling's Descendant's Spouse's Sibling's Descendant
    SiblingSpouseSibling, // Sibling's Spouse's Sibling (e.g. Yin Xiong Di)
    SpouseSiblingSpouse, // Spouse's Sibling's Spouse (e.g. Lian Jin)
    CollateralDescendantSpouseSibling, // Sibling's Descendant's Spouse's Sibling

    SiblingSpouseSiblingChild, // Sibling's Spouse's Sibling's Child

    SiblingSpouseParent, // Sibling's Spouse's Parent (e.g. Qin Jia)
    StepAncestor, // Parent -> Spouse -> Parent... (e.g. Step-Grandparent)
    SpouseCollateral, // Spouse's Uncle/Aunt (e.g. Shu Yue Fu)

    // New specific type for father->spouse->mother->father
    PaternalGrandparentSpouseMaternalGrandparent,

    SiblingCoParentInLaw, // Sibling's Child's Spouse's Parent (e.g. Zhi Yin Xiong Di)

    CollateralSpouseParent, // Parent's Sibling's Spouse's Parent (e.g. Yi Yin Zu Fu)

    CollateralSpouseSibling, // Parent's Sibling's Spouse's Sibling (e.g. Yi Yin Jie Mei)

    // Cousins
    CousinTang, // Father's Brother's children
    CousinBiao // All other cousins
}
