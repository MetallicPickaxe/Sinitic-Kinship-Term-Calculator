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
using Microsoft.UI.Xaml.Shapes;

using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;


using Service=KinshipCalculator.Core.Services;
using KinshipCalculator.WinUI.Options;
using KinshipCalculator.WinUI.ViewModels;
using NetEscapades.Configuration.Yaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KinshipCalculator.Core.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace UI
{
	/// <summary>
	/// Provides application-specific behavior to supplement the default Application class.
	/// </summary>
	public partial class App : Application
	{
		private Window? window_field;

		/// <summary>
		/// Initializes the singleton application object.  This is the first line of authored code
		/// executed, and as such is the logical equivalent of main() or WinMain().
		/// </summary>
		public App ()
		{
			InitializeComponent ();
			Services = ConfigureServices ();
		}

		/// <summary>
		/// Invoked when the application is launched.
		/// </summary>
		/// <param name="args">Details about the launch request and process.</param>
		protected override void OnLaunched ( Microsoft.UI.Xaml.LaunchActivatedEventArgs args )
		{
			window_field ??= Services.GetRequiredService<MainWindow> ();
			window_field.Activate ();
		}

		public static IServiceProvider Services { get; private set; } = default!;

		private static IServiceProvider ConfigureServices ()
		{
			IConfigurationRoot configuration = new ConfigurationBuilder ()
				.SetBasePath ( AppContext.BaseDirectory )
				.AddYamlFile ( "appsettings.yaml" , optional: true , reloadOnChange: true )
				.Build ();

			ApplicationOptions appOptions = new ();
			configuration.Bind ( appOptions );
			if ( appOptions.Languages.Count == 0 )
			{
				// Traditional first = the default (Languages.First()). Fallback list used only when
				// appsettings.yaml supplies none.
				appOptions.Languages.Add ( new LanguageOption { Key = "zh-Hant" , Display = "繁體中文" } );
				appOptions.Languages.Add ( new LanguageOption { Key = "zh-Hans" , Display = "简体中文" } );
				appOptions.Languages.Add ( new LanguageOption { Key = "en" , Display = "English" } );
			}

			ServiceCollection services = new ();
			services.AddSingleton<IConfiguration> ( configuration );
			services.AddSingleton ( appOptions );

			services.AddSingleton<IKinshipCalculator , KinshipCalculator.Core.Services.KinshipCalculator> ();
			services.AddSingleton<MainViewModel> ();
			services.AddTransient<MainWindow> ();

			return services.BuildServiceProvider ();
		}
	}
}