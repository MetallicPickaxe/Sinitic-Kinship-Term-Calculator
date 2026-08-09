using System;
using System.Linq;

using KinshipCalculator.Core.Models;
using KinshipCalculator.WinUI.Options;
using KinshipCalculator.WinUI.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test_Unit;

/// <summary>
/// F2 of the 2026-08-02 user-feature acceptance contract — query history.
///
/// The contract is explicit that undo is NOT history, and it names the cases to cover: add,
/// order, dedup, restore, clear, and reproduction after the language or the ego gender changes.
/// One test each, so a failure names the rule it broke.
/// </summary>
[TestClass]
public class AcceptanceF2HistoryTests
{
    private static MainViewModel CreateViewModel()
        => new(new KinshipCalculator.Core.Services.KinshipCalculator(), new ApplicationOptions());

    private static void Ask(MainViewModel vm, params String[] tokenIds)
    {
        vm.ClearCommand.Execute(null);
        foreach (String id in tokenIds)
        {
            vm.AppendTokenCommand.Execute(vm.TokenButtons.First(t => t.Token.Id == id));
        }
    }

    [TestMethod]
    public void AFinishedQueryIsRecordedWithAllFourFields()
    {
        MainViewModel vm = CreateViewModel();
        vm.SelectedLanguage = "zh-Hant";
        vm.SelectedGender = PersonGender.Female;
        Ask(vm, "father", "father");

        QueryHistoryEntry entry = vm.History.First();
        CollectionAssert.AreEqual(new[] { "father", "father" }, entry.TokenIds.ToArray());
        Assert.AreEqual(PersonGender.Female, entry.EgoGender);
        Assert.AreEqual("zh-Hant", entry.Language);
        Assert.AreEqual("祖父", entry.ResultText);
        Assert.IsFalse(String.IsNullOrWhiteSpace(entry.PathDisplay));
    }

    [TestMethod]
    public void NewestQueryComesFirst()
    {
        MainViewModel vm = CreateViewModel();
        Ask(vm, "father");
        Ask(vm, "mother");

        Assert.AreEqual("母親", vm.History[0].ResultText);
        Assert.IsTrue(vm.History.Any(e => e.ResultText == "父親"), "the earlier query must still be there");
        Assert.IsTrue(
            vm.History.ToList().IndexOf(vm.History.First(e => e.ResultText == "母親"))
            < vm.History.ToList().IndexOf(vm.History.First(e => e.ResultText == "父親")));
    }

    [TestMethod]
    public void AskingTheSameQuestionAgainMovesItUpInsteadOfDuplicating()
    {
        MainViewModel vm = CreateViewModel();
        Ask(vm, "father");
        Ask(vm, "mother");
        Ask(vm, "father");

        Assert.AreEqual(1, vm.History.Count(e => e.ResultText == "父親"), "a repeat must not stack");
        Assert.AreEqual("父親", vm.History[0].ResultText, "the repeat moves to the front");
    }

    [TestMethod]
    public void SamePathInAnotherLanguageIsADifferentQuestion()
    {
        // The stored answer differs, so treating it as the same entry would silently overwrite
        // one of the two results the user actually saw.
        MainViewModel vm = CreateViewModel();
        vm.SelectedLanguage = "zh-Hant";
        Ask(vm, "father", "father");
        vm.SelectedLanguage = "zh-Hans";
        Ask(vm, "father", "father");

        Assert.AreEqual(2, vm.History.Count(e => e.TokenIds.Count == 2));
        CollectionAssert.AreEquivalent(
            new[] { "祖父", "祖父" },
            vm.History.Where(e => e.TokenIds.Count == 2).Select(e => e.ResultText).ToArray());
        CollectionAssert.AreEquivalent(
            new[] { "zh-Hant", "zh-Hans" },
            vm.History.Where(e => e.TokenIds.Count == 2).Select(e => e.Language).ToArray());
    }

    [TestMethod]
    public void RestoringRebuildsTheInputState_NotJustTheText()
    {
        MainViewModel vm = CreateViewModel();
        vm.SelectedLanguage = "zh-Hant";
        Ask(vm, "father", "older-brother");
        String want = vm.ResultText;

        Ask(vm, "mother");
        Assert.AreNotEqual(want, vm.ResultText);

        QueryHistoryEntry earlier = vm.History.First(e => e.ResultText == want);
        vm.RestoreHistoryCommand.Execute(earlier);

        Assert.AreEqual(want, vm.ResultText);
        // Input state, not a pasted string: the path is back, so the user can keep building.
        Assert.IsTrue(vm.ClearCommand.CanExecute(null), "the restored path must be live input");
        Assert.IsTrue(vm.ResultOptions.Count > 0);
    }

    [TestMethod]
    public void RestoringReproducesAcrossLanguageAndEgoGender()
    {
        MainViewModel vm = CreateViewModel();
        vm.SelectedLanguage = "zh-Hant";
        vm.SelectedGender = PersonGender.Female;
        Ask(vm, "spouse", "father");
        String want = vm.ResultText;
        Assert.AreEqual("公公", want, "female ego sees her husband's father as 公公");

        // Move both dials away, then come back through history.
        vm.SelectedLanguage = "en";
        vm.SelectedGender = PersonGender.Male;
        Ask(vm, "mother");

        QueryHistoryEntry earlier = vm.History.First(e => e.ResultText == want);
        vm.RestoreHistoryCommand.Execute(earlier);

        Assert.AreEqual("zh-Hant", vm.SelectedLanguage);
        Assert.AreEqual(PersonGender.Female, vm.SelectedGender);
        Assert.AreEqual(want, vm.ResultText);
    }

    [TestMethod]
    public void RestoringDoesNotChurnTheList()
    {
        MainViewModel vm = CreateViewModel();
        Ask(vm, "father");
        Ask(vm, "mother");
        Int32 before = vm.History.Count;

        vm.RestoreHistoryCommand.Execute(vm.History.First(e => e.ResultText == "父親"));

        Assert.AreEqual(before, vm.History.Count, "restoring must not add an entry");
    }

    [TestMethod]
    public void HistoryCanBeCleared()
    {
        MainViewModel vm = CreateViewModel();
        Ask(vm, "father");
        Assert.IsTrue(vm.ClearHistoryCommand.CanExecute(null));
        Assert.AreEqual(Visibility.Collapsed, vm.HistoryEmptyVisibility);

        vm.ClearHistoryCommand.Execute(null);

        Assert.AreEqual(0, vm.History.Count);
        Assert.IsFalse(vm.ClearHistoryCommand.CanExecute(null));
        Assert.AreEqual(Visibility.Visible, vm.HistoryEmptyVisibility);
    }

    [TestMethod]
    public void HistoryStopsAtItsStatedLimit()
    {
        MainViewModel vm = CreateViewModel();
        String[] tokens = { "father", "mother", "older-brother", "younger-brother", "older-sister", "younger-sister", "son", "daughter" };

        // Enough distinct questions to overrun the cap.
        foreach (String a in tokens)
        {
            foreach (String b in tokens)
            {
                Ask(vm, a, b);
            }
        }

        Assert.AreEqual(MainViewModel.HistoryLimit, vm.History.Count);
    }

    [TestMethod]
    public void ClearingThePathIsNotAQuery()
    {
        MainViewModel vm = CreateViewModel();
        Ask(vm, "father");
        Int32 after = vm.History.Count;

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(after, vm.History.Count, "clearing the pad is not a question");
        Assert.IsFalse(vm.History.Any(e => String.IsNullOrWhiteSpace(e.ResultText)));
    }

    [TestMethod]
    public void UndoingFromANonEmptyPathAddsNoEntryButDoesReorderTheList()
    {
        // The review of 2026-08-02 found the Clear test calling Undo AFTER Clear, where the path
        // is already empty and the command cannot execute — so the Undo half asserted nothing.
        // This is the case it was supposed to cover, and writing it honestly turned up behaviour
        // nobody had recorded.
        //
        // Undo does not GROW the list: stepping back to 自己→父 is a question already asked, so
        // dedup catches it. But dedup moves a repeat to the FRONT, so walking backwards reorders
        // history — every step of an Undo walk becomes the most recent question.
        //
        // SETTLED, not merely unaddressed. The review ruled the contract does not forbid this,
        // so F2 was not downgraded; the underlying design question — every path mutation is a
        // recorded query, so one four-token question also files its three prefixes against a cap
        // of twenty — went to the user with three options and a worked example. The user chose to
        // keep it, which the acceptance contract calls ACCEPTED AS-IS: current behaviour meets
        // the need and the suggested improvement is no longer live work.
        //
        // So this test is the record of a decision, not a placeholder for one. Anyone who later
        // finds this surprising and "fixes" it will turn it red, which is the point.
        MainViewModel vm = CreateViewModel();
        Ask(vm, "father", "older-brother");
        Int32 before = vm.History.Count;

        Assert.IsTrue(vm.UndoCommand.CanExecute(null), "the case is worthless unless Undo can actually run here");
        vm.UndoCommand.Execute(null);

        Assert.AreEqual(before, vm.History.Count, "walking back must not add an entry");
        Assert.AreEqual(
            vm.PathText,
            vm.History.First().PathDisplay,
            "the shortened path is what moved to the front");
        Assert.IsFalse(vm.History.Any(e => String.IsNullOrWhiteSpace(e.ResultText)));
    }
}
