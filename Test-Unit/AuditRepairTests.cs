using System;
using System.Collections.Generic;
using System.Linq;

using KinshipCalculator.Core.Data;
using KinshipCalculator.Core.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Calc = KinshipCalculator.Core.Services.KinshipCalculator;

namespace Test_Unit;

/// <summary>
/// Regression suite for the white-box audit repair round. The unifying defect class was:
/// structure-collapsing shortcuts (net-generation reduction, overlapping-loop union pruning)
/// produced a WRONG-person reading which unconditional exact-stamping then promoted above the
/// structure-preserving candidates. Each test pins one repaired cell with the OLD wrong output
/// documented, so the defect class stays dead.
/// </summary>
[TestClass]
public class AuditRepairTests
{
    private static readonly Dictionary<String, KinshipToken> ById =
        KinshipData.Tokens.ToDictionary(t => t.Id, t => t, StringComparer.Ordinal);

    private static String Term(PersonGender self, params String[] ids)
        => new Calc().Evaluate(ids, "zh-Hant", self).Term.ForLanguage("zh-Hant");

    private static String T(params String[] ids) => Term(PersonGender.Male, ids);

    // ---- complex-lineal scoping: collateral descent must not collapse to lineal ----

    [TestMethod]
    public void SisterSon_IsNephew_NotSon()
    {
        // OLD: net-generation reduction read F.D.S as -1 => 兒子 (my own son).
        Assert.AreEqual("外甥", T("father", "daughter", "son"));
        Assert.AreEqual("外甥女", T("father", "daughter", "daughter"));
    }

    [TestMethod]
    public void BrotherSon_IsNephew_NotSon()
    {
        // OLD: F.S.S => 兒子.
        Assert.AreEqual("姪子", T("father", "son", "son"));
        Assert.AreEqual("姪子", T("mother", "son", "son"));
    }

    [TestMethod]
    public void LoopBack_MaternalLine_KeepsSide()
    {
        // OLD: M.S.M.M net +2 => 祖母 with IsPaternal hard-coded true; the person is my
        // brother's mother's mother = my own maternal grandmother.
        Assert.AreEqual("外祖母", T("mother", "son", "mother", "mother"));
    }

    [TestMethod]
    public void SingleUpDown_SiblingReading_Preserved()
    {
        // The one cell where the net-generation reading IS sound stays: parent's child = sibling.
        Assert.AreEqual("兄弟", T("father", "son"));
        Assert.AreEqual("姐妹", T("father", "daughter"));
    }

    [TestMethod]
    public void DownThenUp_NoLongerLiesSibling()
    {
        // OLD: S.F (my child's father = myself) => 兄弟. Now an honest descriptive reading;
        // asserting only that the brother lie is gone.
        Assert.AreNotEqual("兄弟", T("son", "father"));
    }

    // ---- overlapping-loop pruning: reduce to the true target, not past it ----

    [TestMethod]
    public void OverlappingLoops_ReduceToTarget_NotAncestor()
    {
        // OLD: M.F.YS.OB.YS recorded two overlapping loops; their UNION pruned all trailing
        // siblings => 外祖父 (male ancestor). The person is the grandfather's sister: female, +2.
        Assert.AreEqual("姑外祖母", T("mother", "father", "younger-sister", "older-brother", "younger-sister"));
    }

    // ---- step-ancestor side law (mumuy: f,w collapses to a mother-figure) ----

    [TestMethod]
    public void StepMotherLine_IsMaternal()
    {
        // OLD: the side scan excluded the terminal parent, so F.SP.M lost its maternal side
        // => 繼祖母. mumuy: f,w,m = 外婆.
        Assert.AreEqual("繼外祖母", T("father", "spouse", "mother"));
        // Step-father entry stays paternal (M.SP ≈ a father-figure).
        Assert.AreEqual("繼祖父", T("mother", "spouse", "father"));
    }

    // ---- JuanComposite cells (oracle-adjudicated) ----

    [TestMethod]
    public void JuanFemaleFork_SplitsTerminalGender()
    {
        // OLD: both son and daughter produced 伯祖眷姨姑母 (a male person got a female term).
        // mumuy: f,f,ob,w,os,s = 叔祖眷姨伯父; ...os,d = 叔祖眷姨姑母.
        Assert.AreEqual("伯祖眷姨伯父", T("father", "father", "older-brother", "spouse", "older-sister", "son"));
        Assert.AreEqual("伯祖眷姨姑母", T("father", "father", "older-brother", "spouse", "older-sister", "daughter"));
    }

    [TestMethod]
    public void JuanSisterFork_IsBiaoLine_NotParallelPaternal()
    {
        // OLD: fork gender ignored — a SISTER fork was compacted 从父叔 like a brother fork.
        // mumuy: f,f,os,s,w,f = 姑表叔眷外祖父 vs f,f,ob,s,w,f = 从父叔眷外祖父.
        Assert.AreEqual("姑表叔眷外祖父", T("father", "father", "older-sister", "son", "spouse", "father"));
        Assert.AreEqual("從父叔眷外祖父", T("father", "father", "older-brother", "son", "spouse", "father"));
    }

    // ---- stacked tier-2 cell: entry line differentiated (was a collision) ----

    [TestMethod]
    public void StackedCell_DifferentiatesEntryLine()
    {
        // OLD: inner connector hard-coded 姨表, so paternal and maternal entry collided.
        // mumuy: f,m,ob,d,s = 舅表姑表哥; m,m,ob,d,s = 舅表姨表哥.
        Assert.AreEqual("舅表姑表哥", T("father", "mother", "older-brother", "daughter", "son"));
        Assert.AreEqual("舅表姨表哥", T("mother", "mother", "older-brother", "daughter", "son"));
    }

    // ---- NO-GO audit round: the residual families the release audit blocked on ----

    [TestMethod]
    public void SiblingCoParent_IsSpouse_NotWife()
    {
        // OLD: F.D.D.F => 嫂嫂 (a WIFE, female) for the sister's HUSBAND — complex-lineal's
        // spouse leg rendered the graph's F.D.SP candidate with an Unknown gender. The
        // sibling-co-parent identity now closes [sibling][child][other parent] onto the
        // sibling's spouse with the correct gender.
        Assert.AreEqual("姐夫", T("father", "daughter", "daughter", "father"));
        Assert.AreEqual("姐夫", T("older-sister", "daughter", "father"));
        Assert.AreEqual("嫂嫂", T("older-brother", "daughter", "mother"));
    }

    [TestMethod]
    public void StepSibling_ViaParentSpouse_IsBrother()
    {
        // OLD (transient): F.SP.S composed as 父親眷兄弟, and before that rode complex-lineal.
        // The parent's spouse is a step-PARENT (mumuy collapses f,w to 妈妈), so the child is
        // a sibling via the graph identity.
        Assert.AreEqual("兄弟", T("father", "spouse", "son"));
    }

    [TestMethod]
    public void CanonicalBridge_SpouseSibling_UsesJuanLaw()
    {
        // OLD (task#2 garble): the collateral-spouse-sibling analyzer named the whole family
        // with a flat 姻 connector and uniform flavor — F.F.OB.SP.YB => 叔姻祖伯/叔. The
        // mumuy-adjudicated 眷-law (AffinalWebComposer) now owns it: connector 眷 for a male
        // bridge, in-law flavor by the spouse's side, tier by the bridge generation.
        Assert.AreEqual("伯祖眷舅祖父", T("father", "father", "older-brother", "spouse", "younger-brother"));
        Assert.AreEqual("伯眷舅父", T("father", "older-brother", "spouse", "younger-brother"));
        Assert.AreEqual("舅眷舅父", T("mother", "older-brother", "spouse", "younger-brother"));
        Assert.AreEqual("伯祖眷姨祖母", T("father", "father", "older-brother", "spouse", "younger-sister"));
    }

    // ---- breakwater: the folds and faces the shortcuts used to shadow must not move ----

    [TestMethod]
    public void Breakwater_CoreCellsUnchanged()
    {
        Assert.AreEqual("伯父", T("father", "father", "son"));
        Assert.AreEqual("伯祖父", T("father", "father", "older-brother"));
        Assert.AreEqual("外祖母", T("mother", "mother"));
        Assert.AreEqual("嫂嫂", T("father", "son", "spouse"));
    }
}
