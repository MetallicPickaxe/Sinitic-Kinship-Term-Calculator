using System;
using System.Linq;

using KinshipCalculator.WinUI.Options;
using KinshipCalculator.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace Test;

[TestClass]
public class MainWindowTests
{
	private static UI.MainWindow CreateWindow ( out MainViewModel viewModel , out FrameworkElement root )
	{
		viewModel = new MainViewModel ( new KinshipCalculator.Core.Services.KinshipCalculator () , new ApplicationOptions () );
		UI.MainWindow window = new ( viewModel );
		window.Activate ();

		Assert.IsNotNull ( window.Content , "Window should expose content." );
		root = (FrameworkElement) window.Content!;
		root.UpdateLayout ();
		return window;
	}

	[UITestMethod]
	public void TokensListBindsToViewModelSource ()
	{
		_ = CreateWindow ( out MainViewModel vm , out FrameworkElement root );

		ListView? listView = (ListView?) root.FindName ( "TokensList" );
		Assert.IsNotNull ( listView , "TokensList should exist in visual tree." );
		Assert.AreSame ( vm.TokenButtons , listView!.ItemsSource );
	}

	[UITestMethod]
	public void TemplateButtonExecutesAppendCommand ()
	{
		_ = CreateWindow ( out MainViewModel vm , out FrameworkElement root );
		ListView? listView = (ListView?) root.FindName ( "TokensList" );
		Assert.IsNotNull ( listView , "TokensList should exist in XAML." );

		listView!.UpdateLayout ();
		Object? firstItem = listView.Items.FirstOrDefault ();
		Assert.IsNotNull ( firstItem , "TokensList should have at least one item." );

		listView.ScrollIntoView ( firstItem );
		listView.UpdateLayout ();

		ListViewItem? container = listView.ContainerFromIndex ( 0 ) as ListViewItem;
		Assert.IsNotNull ( container , "First ListViewItem should be realized." );
		container!.UpdateLayout ();

		Button? button = FindDescendant<Button> ( container );
		Assert.IsNotNull ( button , "Button inside DataTemplate should be located." );
		Assert.IsNotNull ( button!.Command , "Button.Command must be set via binding." );

		object? parameter = button.CommandParameter ?? button.DataContext;
		button.Command.Execute ( parameter );

		Assert.IsFalse ( string.IsNullOrWhiteSpace ( vm.PathText ) , "Executing button command should update ViewModel.PathText." );
	}

	[UITestMethod]
	public void UndoCommandReflectsSequenceState ()
	{
		_ = CreateWindow ( out MainViewModel vm , out FrameworkElement root );

		Assert.IsFalse ( vm.UndoCommand.CanExecute ( null ) );
		TokenDisplay token = vm.TokenButtons.First ();
		vm.AppendTokenCommand.Execute ( token );
		Assert.IsTrue ( vm.UndoCommand.CanExecute ( null ) );

		vm.UndoCommand.Execute ( null );
		Assert.IsFalse ( vm.UndoCommand.CanExecute ( null ) );
	}

	private static T? FindDescendant<T> ( DependencyObject? start ) where T : DependencyObject
	{
		if ( start is null )
		{
			return null;
		}

		int children = VisualTreeHelper.GetChildrenCount ( start );
		for ( int i = 0 ; i < children ; i++ )
		{
			DependencyObject child = VisualTreeHelper.GetChild ( start , i );
			if ( child is T typed )
			{
				return typed;
			}

			T? result = FindDescendant<T> ( child );
			if ( result is not null )
			{
				return result;
			}
		}

		return null;
	}
}
