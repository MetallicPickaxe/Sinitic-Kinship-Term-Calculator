using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

using Windows.Foundation;
using Windows.Foundation.Collections;

using KinshipCalculator.WinUI.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace UI
{
	/// <summary>
	/// An empty window that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class MainWindow : Window
	{
		/// <summary>
		/// The window opens tall enough to hold what it actually shows.
		///
		/// At the default size the content did not fit: a 3x3 keypad and a control bar leave
		/// roughly 235 pixels for the answer, which clips the second row of other-names and hides
		/// a second reading entirely. The layout gives the answer whatever is left over, so
		/// "whatever is left over" has to be worth having. Measured against the case that needs
		/// the most room — 父→父→女, two readings of 姑母 with sixteen other names each.
		///
		/// Only the DEFAULT: the window stays freely resizable, and nothing depends on this size.
		/// </summary>
		private const Int32 DefaultWidthDips = 1180;
		private const Int32 DefaultHeightDips = 1010;

		public MainWindow ( MainViewModel viewModel )
		{
			InitializeComponent ();
			ViewModel = viewModel;

			// Clamped to the work area, so a small or scaled display never gets a window larger
			// than the screen it opens on.
			Microsoft.UI.Windowing.DisplayArea display = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId (
				AppWindow.Id , Microsoft.UI.Windowing.DisplayAreaFallback.Primary );

			AppWindow.Resize ( new Windows.Graphics.SizeInt32 (
				Math.Min ( DefaultWidthDips , display.WorkArea.Width - 40 ) ,
				Math.Min ( DefaultHeightDips , display.WorkArea.Height - 40 ) ) );
		}

		public MainViewModel ViewModel { get; }
	}
}