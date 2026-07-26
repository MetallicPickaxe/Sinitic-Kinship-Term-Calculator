using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text;
using KinshipCalculator.Core.Services;
using KinshipCalculator.WinUI.Options;
using KinshipCalculator.Core.Models;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Windows.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;

namespace KinshipCalculator.WinUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
	private readonly IKinshipCalculator calculator_field;
	private readonly ApplicationOptions options_field;
	private readonly List<String> sequence_field = [];
	private readonly Stack<Int32> appendHistory_field = new ();
	private readonly DispatcherQueue dispatcherQueue_field;
	private readonly DispatcherQueueTimer notificationTimer_field;

	public ObservableCollection<TokenDisplay> TokenButtons { get; } = [];
	public ObservableCollection<LanguageOption> Languages { get; } = [];
	public ObservableCollection<PersonGenderOption> GenderOptions { get; } = [];
	public ObservableCollection<ResultInterpretation> ResultOptions { get; } = [];

	private static readonly String CopyPathHintZhHans = Encoding.UTF8.GetString ( "路径已复制"u8 );
	private static readonly String CopyPathHintZhHant = Encoding.UTF8.GetString ( "路徑已複製"u8 );
	private static readonly String CopyResultHintZhHans = Encoding.UTF8.GetString ( "结果已复制"u8 );
	private static readonly String CopyResultHintZhHant = Encoding.UTF8.GetString ( "結果已複製"u8 );
	private static readonly String CopyFailedHintZhHans = Encoding.UTF8.GetString ( "複製失敗"u8 );

	[ObservableProperty]
	private String selectedLanguage = "zh-Hans";

	[ObservableProperty]
	private string resultText = string.Empty;

	[ObservableProperty]
	private string pathText = string.Empty;

	// K15 layer ④ — raw calibration readback of what was entered, un-simplified.
	[ObservableProperty]
	private string rawChainText = string.Empty;

	[ObservableProperty]
	private string statusHint = string.Empty;

	[ObservableProperty]
	private bool isExactMatch;

	[ObservableProperty]
	private PersonGender selectedGender = PersonGender.Male;

	[ObservableProperty]
	private PersonGenderOption? selectedGenderOption;

	[ObservableProperty]
	private KinshipOrigin selectedOrigin = KinshipOrigin.Biological;

	[ObservableProperty]
	private string notificationMessage = string.Empty;

	[ObservableProperty]
	private bool isNotificationVisible;

	public IRelayCommand<TokenDisplay> AppendTokenCommand { get; }
	public IRelayCommand UndoCommand { get; }
	public IRelayCommand ClearCommand { get; }
	public IRelayCommand CopyPathCommand { get; }
	public IRelayCommand CopyResultCommand { get; }

	public MainViewModel ( IKinshipCalculator calculator , ApplicationOptions options )
	{
		calculator_field = calculator;
		options_field = options;
		dispatcherQueue_field = DispatcherQueue.GetForCurrentThread () ?? DispatcherQueueController.CreateOnDedicatedThread ().DispatcherQueue;
		notificationTimer_field = dispatcherQueue_field.CreateTimer ();
		notificationTimer_field.Interval = TimeSpan.FromSeconds ( 2 );
		notificationTimer_field.IsRepeating = false;
		notificationTimer_field.Tick += (_, _) =>
		{
			IsNotificationVisible = false;
		};

		AppendTokenCommand = new RelayCommand<TokenDisplay> ( OnAppendToken , static display => display is not null );
		UndoCommand = new RelayCommand ( OnUndo , () => appendHistory_field.Count > 0 );
		ClearCommand = new RelayCommand ( OnClear , () => sequence_field.Count > 0 );
		CopyPathCommand = new RelayCommand ( () => CopyText ( PathText , CopyPathHintZhHans , CopyPathHintZhHant , "Path copied" ) , () => !String.IsNullOrWhiteSpace ( PathText ) );
		CopyResultCommand = new RelayCommand ( () => CopyText ( ResultText , CopyResultHintZhHans , CopyResultHintZhHant , "Result copied" ) , () => !String.IsNullOrWhiteSpace ( ResultText ) );

		if ( options.Languages.Count == 0 )
		{
			// Traditional first = the default (Languages.First() below). The product face uses
			// traditional characters; simplified and English remain selectable.
			options.Languages.Add ( new LanguageOption { Key = "zh-Hant" , Display = "繁體中文" } );
			options.Languages.Add ( new LanguageOption { Key = "zh-Hans" , Display = "简体中文" } );
			options.Languages.Add ( new LanguageOption { Key = "en" , Display = "English" } );
		}

		foreach ( LanguageOption lang in options.Languages )
		{
			Languages.Add ( lang );
		}

		// UI chrome is English (the kinship terms themselves stay in the selected script).
		GenderOptions.Add ( new PersonGenderOption ( PersonGender.Male , "Male" ) );
		GenderOptions.Add ( new PersonGenderOption ( PersonGender.Female , "Female" ) );

		SelectedLanguage = Languages.First ().Key;
		PersonGender defaultGender = options.DefaultGender == PersonGender.Female ? PersonGender.Female : PersonGender.Male;
		SelectedGender = defaultGender;
		SelectedGenderOption = GenderOptions.FirstOrDefault ( option => option.Value == defaultGender );

		foreach ( KinshipToken token in calculator_field.Tokens.Where ( t => String.IsNullOrEmpty ( t.Origin ) ) )
		{
			TokenButtons.Add ( new TokenDisplay ( token , AppendTokenCommand ) );
		}

		UpdateTokenLabels ();
		Recalculate ();
	}

	partial void OnSelectedLanguageChanged ( String value )
	{
		if ( String.IsNullOrWhiteSpace ( value ) )
		{
			return;
		}

		UpdateTokenLabels ();
		Recalculate ();
	}

	partial void OnSelectedGenderChanged ( PersonGender value )
	{
		PersonGenderOption? match = GenderOptions.FirstOrDefault ( option => option.Value == value );
		if ( match is not null && !ReferenceEquals ( match , SelectedGenderOption ) )
		{
			SelectedGenderOption = match;
		}
		Recalculate ();
	}

	partial void OnSelectedGenderOptionChanged ( PersonGenderOption? value )
	{
		if ( value is null )
		{
			return;
		}

		if ( SelectedGender != value.Value )
		{
			SelectedGender = value.Value;
		}
	}

	partial void OnSelectedOriginChanged ( KinshipOrigin value )
	{
		OnPropertyChanged ( nameof ( IsBiologicalOrigin ) );
		OnPropertyChanged ( nameof ( IsAdoptiveOrigin ) );
		OnPropertyChanged ( nameof ( IsStepOrigin ) );
	}

	private void OnAppendToken ( TokenDisplay? display )
	{
		if ( display is null )
		{
			return;
		}

		IReadOnlyList<String> tokensToAdd = KinshipSequenceBuilder.TranslateToken ( display.Token , SelectedOrigin );
		if ( tokensToAdd.Count == 0 )
		{
			return;
		}

		foreach ( String id in tokensToAdd )
		{
			sequence_field.Add ( id );
		}
		appendHistory_field.Push ( tokensToAdd.Count );
		ResetOriginSelection ();

		Recalculate ();
		UndoCommand.NotifyCanExecuteChanged ();
		ClearCommand.NotifyCanExecuteChanged ();
		CopyPathCommand.NotifyCanExecuteChanged ();
		CopyResultCommand.NotifyCanExecuteChanged ();
	}

	private void OnUndo ()
	{
		if ( appendHistory_field.Count == 0 )
		{
			return;
		}

		Int32 count = appendHistory_field.Pop ();
		for ( Int32 i = 0 ; i < count && sequence_field.Count > 0 ; i++ )
		{
			sequence_field.RemoveAt ( sequence_field.Count - 1 );
		}

		Recalculate ();
		UndoCommand.NotifyCanExecuteChanged ();
		ClearCommand.NotifyCanExecuteChanged ();
		CopyPathCommand.NotifyCanExecuteChanged ();
		CopyResultCommand.NotifyCanExecuteChanged ();
	}

	private void OnClear ()
	{
		sequence_field.Clear ();
		appendHistory_field.Clear ();
		ResetOriginSelection ();
		Recalculate ();
		UndoCommand.NotifyCanExecuteChanged ();
		ClearCommand.NotifyCanExecuteChanged ();
		CopyPathCommand.NotifyCanExecuteChanged ();
		CopyResultCommand.NotifyCanExecuteChanged ();
	}

	private void Recalculate ()
	{
		KinshipResult result = calculator_field.Evaluate ( sequence_field , SelectedLanguage , SelectedGender );
		ResultText = result.Term.ForLanguage ( SelectedLanguage );
		PathText = result.PathDisplay.ForLanguage ( SelectedLanguage );
		RawChainText = result.RawChain.ForLanguage ( SelectedLanguage );
		IsExactMatch = result.IsExactMatch;
		// The flag means "the engine COMPOSED a name" (vs a descriptive 的-chain reading) —
		// it is not a certification of attested correctness, so the hint must not promise
		// one (the release audit flagged "Resolved to a proper term" as an over-claim).
		StatusHint = result.IsExactMatch
			? "Named term (composed by rule)"
			: "Descriptive reading (no single term composed)";

		ResultOptions.Clear ();
		foreach ( KinshipResolutionOption option in result.Options )
		{
			String label = option.Label.ForLanguage ( SelectedLanguage );
			String standard = label;
			String colloquial = option.HasAlternateLabel
				? option.AlternateLabel.ForLanguage ( SelectedLanguage )
				: "—"; // no colloquial variant
			// Structural path is chrome (the engine explaining the relationship structure),
			// so it renders in English regardless of the selected term language.
			String official = option.OfficialDescription.ForLanguage ( "en" );

			ResultInterpretation interpretation = new (
				standard ,
				colloquial ,
				official ,
				option.Explanation ,
				option.IsExactMatch ,
				option.DescriptiveChain.ForLanguage ( SelectedLanguage )
			);

			// K15/K16: tag every alternate with the lexicon layer that owns it, so the card
			// shows 「伯公 · 南系」 rather than an unlabelled pile of synonyms. Words no layer
			// claims were computed by the engine and carry no tag.
			if ( option.HasAlternateLabel )
			{
				foreach ( String variant in option.AlternateLabel.ForLanguage ( SelectedLanguage )
					.Split ( '|' , StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
				{
					String? layer = KinshipCalculator.Core.Data.KinshipLexiconLayers.TryGetLayerNameForTerm ( variant )
						?? KinshipCalculator.Core.Data.KinshipLexiconLayers.TryGetLayerNameForTerm (
							KinshipCalculator.Core.Services.Formatting.KinshipScriptConverter.ToHans ( variant ) );
					interpretation.Variants.Add ( new VariantChip ( variant , layer ?? String.Empty ) );
				}
			}

			ResultOptions.Add ( interpretation );
		}

		CopyPathCommand.NotifyCanExecuteChanged ();
		CopyResultCommand.NotifyCanExecuteChanged ();
	}

	private void UpdateTokenLabels ()
	{
		foreach ( TokenDisplay button in TokenButtons )
		{
			button.Label = button.Token.Label.ForLanguage ( SelectedLanguage );
		}
	}

	private String GetLocalizedHint ( String zhHans , String zhHant , String en ) => SelectedLanguage switch
	{
		"zh-Hant" => zhHant,
		"en" => en,
		_ => zhHans
	};

	private void CopyText ( String? text , String zhHans , String zhHant , String en )
	{
		if ( String.IsNullOrWhiteSpace ( text ) )
		{
			return;
		}

		try
		{
			DataPackage package = new ();
			package.SetText ( text );
			Clipboard.SetContent ( package );
			Clipboard.Flush ();
			// Chrome toasts are English regardless of the selected term language.
			_ = zhHans; _ = zhHant;
			ShowNotification ( en );
		}
		catch
		{
			ShowNotification ( "Copy failed" );
		}
	}

	private void ShowNotification ( String message )
	{
		NotificationMessage = message;
		IsNotificationVisible = true;
		notificationTimer_field.Stop ();
		notificationTimer_field.Start ();
	}

	private void ResetOriginSelection ()
	{
		SelectedOrigin = KinshipOrigin.Biological;
	}
}

public sealed partial class TokenDisplay : ObservableObject
{
	public TokenDisplay ( KinshipToken token , ICommand appendCommand )
	{
		Token = token;
		Label = token.Label.ZhHans;
		AppendCommand = appendCommand;
	}

	public KinshipToken Token { get; }

	public String Label { get; set;}
	public ICommand AppendCommand { get; }
}

public sealed class PersonGenderOption
{
	public PersonGenderOption ( PersonGender value , String display )
	{
		Value = value;
		Display = display;
	}

	public PersonGender Value { get; }
	public String Display { get; }
}

public partial class MainViewModel
{
	public Boolean IsBiologicalOrigin
	{
		get => SelectedOrigin == KinshipOrigin.Biological;
		set
		{
			if ( value )
			{
				SelectedOrigin = KinshipOrigin.Biological;
			}
		}
	}

	public Boolean IsAdoptiveOrigin
	{
		get => SelectedOrigin == KinshipOrigin.Adoptive;
		set
		{
			if ( value )
			{
				SelectedOrigin = KinshipOrigin.Adoptive;
			}
		}
	}

	public Boolean IsStepOrigin
	{
		get => SelectedOrigin == KinshipOrigin.Step;
		set
		{
			if ( value )
			{
				SelectedOrigin = KinshipOrigin.Step;
			}
		}
	}
}

public sealed class OriginOption
{
	public OriginOption ( KinshipOrigin value , String display )
	{
		Value = value;
		Display = display;
	}

	public KinshipOrigin Value { get; }
	public String Display { get; }
}

public sealed class ResultInterpretation
{
	public ResultInterpretation ( String standard , String colloquial , String official , String explanation , Boolean isExact , String descriptiveChain = "" )
	{
		StandardLabel = standard;
		ColloquialLabel = colloquial;
		OfficialLabel = official;
		Explanation = explanation;
		IsExact = isExact;
		DescriptiveChain = descriptiveChain;
	}

	public String StandardLabel { get; }
	public String ColloquialLabel { get; }
	public String OfficialLabel { get; }
	public String Explanation { get; }
	public Boolean IsExact { get; }

	/// <summary>K15 layer ③ — legal-document chain (父的父的兄), never contracted.</summary>
	public String DescriptiveChain { get; }

	/// <summary>
	/// Alternate terms with their source layer (南系 / 北系 / 通用口語 / user layer). These are
	/// CANDIDATES only — the primary answer stays <see cref="StandardLabel"/>.
	/// </summary>
	public ObservableCollection<VariantChip> Variants { get; } = [];

	public Boolean HasVariants => Variants.Count > 0;
}

/// <summary>One alternate term plus the lexicon layer it came from (empty = engine-computed).</summary>
public sealed class VariantChip
{
	public VariantChip ( String term , String layerName )
	{
		Term = term;
		LayerName = layerName;
	}

	public String Term { get; }
	public String LayerName { get; }
	public Boolean HasLayer => !String.IsNullOrEmpty ( LayerName );
	public String Display => HasLayer ? $"{Term} · {LayerName}" : Term;
}
