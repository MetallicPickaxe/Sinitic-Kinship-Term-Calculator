using System;
using System.Collections.Generic;
using System.Linq;

using KinshipCalculator.Core.Data;
using KinshipCalculator.Core.Models;
using KinshipCalculator.Core.Services;
using KinshipCalculator.Core.Services.Formatting;
using KinshipCalculator.Core.Services.Rules;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test_Unit;

/// <summary>
/// Regression suite for the lossless chain-shape re-founding (defect families A2 / descendant-外 / closure).
/// Each fix case documents the OLD wrong output so the defect stays pinned.
/// </summary>
[TestClass]
public class ChainShapeTests
{
    private static readonly Dictionary<String, KinshipToken> TokensById =
        KinshipData.Tokens.ToDictionary(t => t.Id, t => t, StringComparer.Ordinal);

    private static IReadOnlyList<KinshipToken> Chain(params String[] ids)
        => ids.Select(id => TokensById[id]).ToList();

    private static String FormalHant(params String[] ids)
    {
        KinshipChainShape? shape = KinshipChainShapeBuilder.Build(Chain(ids), PersonGender.Unknown);
        Assert.IsNotNull(shape, $"chain [{String.Join(".", ids)}] should be canonical");
        ChainShapeName? name = ChainShapeTermFormatter.TryFormat(shape!);
        Assert.IsNotNull(name, $"chain [{String.Join(".", ids)}] should be formatted by the shape path");
        return name!.Formal.ZhHant;
    }

    // ---------------------------------------------------------------- builder

    [TestMethod]
    public void Builder_PureAncestor_PreservesEveryHopGender()
    {
        KinshipChainShape? shape = KinshipChainShapeBuilder.Build(Chain("father", "mother", "father"), PersonGender.Unknown);
        Assert.IsNotNull(shape);
        CollectionAssert.AreEqual(
            new[] { PersonGender.Male, PersonGender.Female, PersonGender.Male },
            shape!.AscentGenders.ToArray());
        Assert.IsTrue(shape.IsPureAncestor);
    }

    [TestMethod]
    public void Builder_LeadingSpouse_IsCaptured_NotDropped()
    {
        KinshipChainShape? shape = KinshipChainShapeBuilder.Build(Chain("spouse", "father", "father"), PersonGender.Male);
        Assert.IsNotNull(shape);
        Assert.IsTrue(shape!.LeadingSpouse);
        Assert.IsFalse(shape.IsPureAncestor); // spouse-rooted: must NOT collapse to own-side ancestor
    }

    [TestMethod]
    public void Builder_MidChainSpouse_IsNotCanonical()
    {
        Assert.IsNull(KinshipChainShapeBuilder.Build(Chain("father", "spouse", "mother", "father"), PersonGender.Unknown));
    }

    [TestMethod]
    public void Builder_SiblingAfterDescent_IsNotCanonical()
    {
        Assert.IsNull(KinshipChainShapeBuilder.Build(Chain("son", "older-brother"), PersonGender.Unknown));
    }

    [TestMethod]
    public void Builder_AdoptiveLink_FallsToLegacy()
    {
        // Adoptive ASCENT links now build with the AdoptiveAscent flag and compose the 養-prefix.
        KinshipChainShape? shape = KinshipChainShapeBuilder.Build(Chain("adoptive-father", "father"), PersonGender.Unknown);
        Assert.IsNotNull(shape);
        Assert.IsTrue(shape!.AdoptiveAscent);
        ChainShapeName? name = ChainShapeTermFormatter.TryFormat(shape);
        Assert.IsNotNull(name);
        Assert.AreEqual("養祖父", name!.Formal.ZhHant);

        // Adoptive DESCENT links still fall to the legacy composer (养孙子 family).
        Assert.IsNull(KinshipChainShapeBuilder.Build(Chain("adoptive-son", "son"), PersonGender.Unknown));
    }

    [TestMethod]
    public void Formatter_ParentSiblings_StayWithLegacy()
    {
        // h=1 elder collaterals (伯/叔/姑) are already correct in legacy — the shape path abstains.
        KinshipChainShape? shape = KinshipChainShapeBuilder.Build(Chain("father", "older-brother"), PersonGender.Unknown);
        Assert.IsNotNull(shape);
        Assert.IsNull(ChainShapeTermFormatter.TryFormat(shape!));
    }

    // ------------------------------------------- A2: ancestor 外-collision fixes

    [TestMethod]
    public void Ancestor_CollisionTheorem_FFMF_And_FMFF_AreDistinct()
    {
        // OLD: both chains collapsed to the same RelationVector and both rendered 高祖父.
        String ffmf = FormalHant("father", "father", "mother", "father");
        String fmff = FormalHant("father", "mother", "father", "father");
        Assert.AreEqual("高外祖父", ffmf);   // workbook 表内 111
        Assert.AreEqual("曾外曾祖父", fmff); // workbook 表内 141
        Assert.AreNotEqual(ffmf, fmff);
    }

    [TestMethod]
    public void Ancestor_FMF_IsMaternalGreatGrandfather_NotPlainOne()
    {
        // OLD: 曾祖父 (collided with F.F.F).
        Assert.AreEqual("曾外祖父", FormalHant("father", "mother", "father"));
        Assert.AreEqual("曾祖父", FormalHant("father", "father", "father"));
    }

    [TestMethod]
    public void Ancestor_DeepMaternal_ReproducesAttestedStacking()
    {
        Assert.AreEqual("外曾外祖父", FormalHant("mother", "mother", "father"));      // 表内 217
        Assert.AreEqual("外曾外曾外祖母", FormalHant("mother", "mother", "mother", "mother")); // 表内 222
        Assert.AreEqual("外高外祖父", FormalHant("mother", "father", "mother", "father"));     // 表内 198
    }

    [TestMethod]
    public void Ancestor_StraightLines_PreserveLegacyGoodOutputs()
    {
        Assert.AreEqual("祖父", FormalHant("father", "father"));
        Assert.AreEqual("外祖父", FormalHant("mother", "father"));
        Assert.AreEqual("外祖母", FormalHant("mother", "mother"));
        Assert.AreEqual("高祖父", FormalHant("father", "father", "father", "father"));
        Assert.AreEqual("外曾祖父", FormalHant("mother", "father", "father"));
    }

    [TestMethod]
    public void Ancestor_Colloquial_StraightLines_Kept()
    {
        KinshipChainShape? shape = KinshipChainShapeBuilder.Build(Chain("mother", "father"), PersonGender.Unknown);
        ChainShapeName? name = ChainShapeTermFormatter.TryFormat(shape!);
        Assert.IsNotNull(name!.Colloquial);
        StringAssert.Contains(name.Colloquial!.ZhHans, "外公");
    }

    // --------------------------------- descendant 外-placement + closure fixes

    [TestMethod]
    public void Descendant_TwoSlotConvention_SpouseNeverShiftsSlots()
    {
        // Established convention: leading slot = first hop female, inner slot = penultimate hop female.
        // OLD: the spouse hop distorted the inner-slot check, producing arbitrary 外 placement.
        Assert.AreEqual("外玄孫媳", FormalHant("daughter", "daughter", "son", "son", "spouse"));   // leading only (表内 14; old: 外玄外孫媳)
        Assert.AreEqual("外玄孫婿", FormalHant("daughter", "daughter", "son", "daughter", "spouse")); // leading only (表内 12)
        Assert.AreEqual("外玄外孫媳", FormalHant("daughter", "son", "daughter", "son", "spouse"));  // both slots (D..D penultimate)
    }

    [TestMethod]
    public void Descendant_TwoSlotConvention_MarkedAndUnmarkedCrossings()
    {
        Assert.AreEqual("外曾外孫女", FormalHant("daughter", "daughter", "daughter")); // both slots — anchors the corpus convention
        Assert.AreEqual("玄外孫女", FormalHant("son", "daughter", "daughter", "daughter")); // inner slot (penultimate D)
        // Mid-only crossings (neither first nor penultimate) stay unmarked by convention:
        Assert.AreEqual("玄孫女", FormalHant("son", "daughter", "son", "daughter"));
    }

    [TestMethod]
    public void Descendant_SpouseClosure_KeepsInnerCrossing()
    {
        // OLD: S.D.D.SP rendered 曾孫婿 — excludeLastStep also erased the genuine inner crossing.
        Assert.AreEqual("曾外孫婿", FormalHant("son", "daughter", "daughter", "spouse"));
        Assert.AreEqual("曾孫媳", FormalHant("son", "son", "son", "spouse")); // 表内 344: unchanged good row
    }

    [TestMethod]
    public void Descendant_FirstCrossingDistinction_Attested()
    {
        // 外曾孫 (daughter-line start) vs 曾外孫 (crossing at the second hop) — dictionary-attested contrast.
        Assert.AreEqual("外曾孫子", FormalHant("daughter", "son", "son"));
        Assert.AreEqual("曾外孫子", FormalHant("son", "daughter", "son"));
    }

    [TestMethod]
    public void Descendant_StraightLines_PreserveLegacyGoodOutputs()
    {
        Assert.AreEqual("孫子", FormalHant("son", "son"));
        Assert.AreEqual("孫女", FormalHant("son", "daughter"));
        Assert.AreEqual("外孫子", FormalHant("daughter", "son"));
        Assert.AreEqual("曾孫子", FormalHant("son", "son", "son"));
    }

    [TestMethod]
    public void Descendant_BeyondLadder_NumericClosure_ConsistentWai()
    {
        String[] sevenSons = Enumerable.Repeat("son", 7).ToArray();
        Assert.AreEqual("8代外孫女", FormalHant(sevenSons.Append("daughter").Append("daughter").ToArray())); // 表内 305
        // OLD: the spouse row dropped the 外 (8代孫婿); the closure must keep it.
        Assert.AreEqual("8代外孫婿", FormalHant(sevenSons.Append("daughter").Append("daughter").Append("spouse").ToArray())); // 表内 306
    }

    // ------------------------------- B/C: collateral generation + grade fixes

    private static String FormalHantAs(PersonGender ego, params String[] ids)
    {
        KinshipChainShape? shape = KinshipChainShapeBuilder.Build(Chain(ids), ego);
        Assert.IsNotNull(shape);
        ChainShapeName? name = ChainShapeTermFormatter.TryFormat(shape!);
        Assert.IsNotNull(name, $"chain [{String.Join(".", ids)}] should be formatted by the shape path");
        return name!.Formal.ZhHant;
    }

    [TestMethod]
    public void Collateral_ElderGeneration_MatchesLadder()
    {
        // OLD (表内 71/78): note said gen-4 but the term said gen-2 (伯祖父/伯祖母).
        Assert.AreEqual("伯高祖父", FormalHant("father", "father", "father", "father", "older-brother"));
        Assert.AreEqual("伯高祖母", FormalHant("father", "father", "father", "father", "older-brother", "spouse"));
        Assert.AreEqual("叔高祖父", FormalHant("father", "father", "father", "father", "younger-brother"));
        // Unchanged good rows:
        Assert.AreEqual("伯祖父", FormalHant("father", "father", "older-brother"));
        Assert.AreEqual("叔祖母", FormalHant("father", "father", "younger-brother", "spouse"));
        Assert.AreEqual("姑祖母", FormalHant("father", "father", "older-sister"));
        Assert.AreEqual("姑祖父", FormalHant("father", "father", "older-sister", "spouse"));
    }

    [TestMethod]
    public void Collateral_DescentReachedElder_GenerationAligned()
    {
        // OLD (表内 72/73): terms were generation-shifted against the engine's own structural note.
        Assert.AreEqual("堂伯曾祖父", FormalHant("father", "father", "father", "father", "older-brother", "son"));
        Assert.AreEqual("從伯祖父", FormalHant("father", "father", "father", "father", "older-brother", "son", "son"));
        Assert.AreEqual("堂伯", FormalHant("father", "father", "older-brother", "son"));
        Assert.AreEqual("堂嬸", FormalHant("father", "father", "younger-brother", "son", "spouse"));
    }

    [TestMethod]
    public void Collateral_Grades_FollowFiveMourningDistances()
    {
        // OLD (表内 117/118/92): every depth was flattened to 堂.
        Assert.AreEqual("堂兄", FormalHant("father", "older-brother", "son"));
        Assert.AreEqual("從堂兄", FormalHant("father", "father", "older-brother", "son", "son"));
        Assert.AreEqual("堂姪子", FormalHant("father", "older-brother", "son", "son"));
        Assert.AreEqual("從堂姪子", FormalHant("father", "father", "older-brother", "son", "son", "son"));
        Assert.AreEqual("族姪子", FormalHant("father", "father", "father", "older-brother", "son", "son", "son", "son"));
    }

    [TestMethod]
    public void Collateral_SororalLine_And_JuniorSpouse_Fixed()
    {
        // OLD (表内 156/168): 堂甥女 lost the 外; the junior's spouse collapsed to a bare gender flip.
        Assert.AreEqual("堂外甥女", FormalHant("father", "older-brother", "daughter", "daughter"));
        Assert.AreEqual("堂姪媳", FormalHant("father", "older-brother", "son", "son", "spouse"));
        Assert.AreEqual("堂外甥媳", FormalHant("father", "older-brother", "daughter", "son", "spouse"));
    }

    [TestMethod]
    public void Collateral_MixedAscent_StaysWithLegacy()
    {
        // Mixed-ascent ELDER collaterals (d == 0) now compose from the anchor ancestor:
        // F,M anchors to 祖母 (female) so her elder brother is the 舅-flavor (舅祖父/舅公).
        KinshipChainShape? shape = KinshipChainShapeBuilder.Build(Chain("father", "mother", "older-brother"), PersonGender.Unknown);
        Assert.IsNotNull(shape);
        ChainShapeName? name = ChainShapeTermFormatter.TryFormat(shape!);
        Assert.IsNotNull(name);
        Assert.AreEqual("舅祖父", name!.Formal.ZhHant);
        // K16: the colloquial slot is now assembled from the lexicon layers, so it carries
        // every registered variant (北 舅爺 · 南 舅公) instead of one hard-coded dialect.
        CollectionAssert.AreEquivalent(
            new[] { "舅爺", "舅公" },
            (name.Colloquial?.ZhHant ?? string.Empty).Split('|'),
            $"colloquial set: {name.Colloquial?.ZhHant}");

        // Mixed-ascent families WITH descent (表-classes) still defer to the legacy composer.
        KinshipChainShape? cousinShape = KinshipChainShapeBuilder.Build(Chain("mother", "older-brother", "son"), PersonGender.Unknown);
        Assert.IsNotNull(cousinShape);
        Assert.IsNull(ChainShapeTermFormatter.TryFormat(cousinShape!));
    }

    // ----------------------------------- G: spouse-rooted chains (隨稱 / 岳-form)

    [TestMethod]
    public void SpouseRooted_AncestorForms_PerEgoGender()
    {
        Assert.AreEqual("岳祖父", FormalHantAs(PersonGender.Male, "spouse", "father", "father"));
        Assert.AreEqual("祖父", FormalHantAs(PersonGender.Female, "spouse", "father", "father"));
        StringAssert.Contains(FormalHantAs(PersonGender.Unknown, "spouse", "father", "father"), "男：岳祖父");
    }

    [TestMethod]
    public void SpouseRooted_MaternalSide_NotCollapsed()
    {
        // OLD: SP.M.F collapsed to the same 岳祖父 as SP.F.F (spouse-side lineage erased).
        Assert.AreEqual("岳外祖父", FormalHantAs(PersonGender.Male, "spouse", "mother", "father"));
        Assert.AreNotEqual(
            FormalHantAs(PersonGender.Male, "spouse", "father", "father"),
            FormalHantAs(PersonGender.Male, "spouse", "mother", "father"));
    }

    [TestMethod]
    public void SpouseRooted_Collateral_KeepsSpouseSide()
    {
        // OLD (表内 370/371): SP.F.F.F.OB became 伯岳父 (wrong generation) and its spouse row
        // dropped the leading SP entirely, collapsing to own-side 伯祖母.
        Assert.AreEqual("伯曾祖父", FormalHantAs(PersonGender.Female, "spouse", "father", "father", "father", "older-brother"));
        Assert.AreEqual("岳伯曾祖母", FormalHantAs(PersonGender.Male, "spouse", "father", "father", "father", "older-brother", "spouse"));
    }

    // ---------------------------------------------------- engine wiring (E2E)

    [TestMethod]
    public void Engine_ShapeRule_WinsOverLegacyLossyPath()
    {
        IReadOnlyList<KinshipToken> tokens = Chain("father", "mother", "father");
        RelationVector vector = RelationVectorBuilder.Build(tokens, PersonGender.Unknown);
        Boolean resolved = RuleDrivenKinshipResolver.TryResolve(tokens, vector, PersonGender.Unknown, out RuleResolution resolution);
        Assert.IsTrue(resolved);
        Assert.AreEqual("曾外祖父", resolution.Label.ZhHant); // legacy path said 曾祖父
        Assert.IsTrue(resolution.IsExactMatch);
    }

    [TestMethod]
    public void Engine_UncoveredFamilies_StillResolveViaLegacy()
    {
        // Collateral chain: shape path abstains, legacy must still answer.
        IReadOnlyList<KinshipToken> tokens = Chain("father", "older-brother");
        RelationVector vector = RelationVectorBuilder.Build(tokens, PersonGender.Unknown);
        Boolean resolved = RuleDrivenKinshipResolver.TryResolve(tokens, vector, PersonGender.Unknown, out RuleResolution resolution);
        Assert.IsTrue(resolved);
        Assert.IsFalse(String.IsNullOrEmpty(resolution.Label.ZhHant));
    }
}
