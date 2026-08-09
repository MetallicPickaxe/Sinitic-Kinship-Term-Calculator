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

	/// <summary>
	/// The keypad by SLOT rather than by reading order (round-2 contract, U7). The pad is laid out
	/// like a family tree — parents above, siblings and spouse across the middle with 配偶 dead
	/// centre, children below — and each cell has to name the token it holds, because a flow of
	/// nine in list order is what produced the arbitrary 3x3 the contract rejects.
	///
	/// Nine properties rather than an indexer or a function binding: the XAML then reads as the
	/// diagram it is meant to be, and reordering the token list cannot silently rearrange the pad.
	/// </summary>
	public TokenDisplay? TokenFather => FindToken ( "father" );
	public TokenDisplay? TokenMother => FindToken ( "mother" );
	public TokenDisplay? TokenOlderBrother => FindToken ( "older-brother" );
	public TokenDisplay? TokenYoungerBrother => FindToken ( "younger-brother" );
	public TokenDisplay? TokenSpouse => FindToken ( "spouse" );
	public TokenDisplay? TokenOlderSister => FindToken ( "older-sister" );
	public TokenDisplay? TokenYoungerSister => FindToken ( "younger-sister" );
	public TokenDisplay? TokenSon => FindToken ( "son" );
	public TokenDisplay? TokenDaughter => FindToken ( "daughter" );

	private TokenDisplay? FindToken ( String id )
		=> TokenButtons.FirstOrDefault ( t => String.Equals ( t.Token.Id , id , StringComparison.Ordinal ) );
	// The Languages collection went with the picker (round-2 contract, U8): removed, not hidden.
	public ObservableCollection<PersonGenderOption> GenderOptions { get; } = [];
	public ObservableCollection<ResultInterpretation> ResultOptions { get; } = [];

	/// <summary>
	/// Recent completed queries, newest first. SESSION ONLY — this is deliberately not persisted
	/// across restarts, and the UI says so rather than letting the reader assume otherwise.
	/// Undo is a different thing: it walks back the path being built, whereas this is a list of
	/// finished questions the user can return to.
	/// </summary>
	public ObservableCollection<QueryHistoryEntry> History { get; } = [];

	/// <summary>
	/// Cap on <see cref="History"/>. A fixed ceiling with newest-first eviction, stated here so
	/// the rule is inspectable rather than emergent.
	/// </summary>
	public const Int32 HistoryLimit = 20;

	/// <summary>Empty-state visibility for the history flyout — an empty popup explains nothing.</summary>
	public Visibility HistoryEmptyVisibility => History.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

	private static readonly String CopyPathHintZhHans = Encoding.UTF8.GetString ( "路径已复制"u8 );
	private static readonly String CopyPathHintZhHant = Encoding.UTF8.GetString ( "路徑已複製"u8 );
	private static readonly String CopyResultHintZhHans = Encoding.UTF8.GetString ( "结果已复制"u8 );
	private static readonly String CopyResultHintZhHant = Encoding.UTF8.GetString ( "結果已複製"u8 );
	private static readonly String CopyFailedHintZhHans = Encoding.UTF8.GetString ( "複製失敗"u8 );

	// U9 (round-2 contract): there is no empty-state notice any more. Announcing that a column
	// holds nothing is a sign saying "nothing here" — the block itself is gone instead, so there
	// is nothing to caption. This EXPRESSLY REPLACES the 2026-08-02 clause that required the
	// notice; the defect that clause addressed was a heading standing over an empty area, and a
	// heading that does not render cannot reproduce it.

	// The label on a same-word-other-spelling chip. Names a WRITING, not a place, so it cannot be
	// mistaken for a regional word.
	private const String VariantGlyphLabel = "variant glyph";

	// Not every alternate comes from a layer file. Some are composed by the naming rules and
	// carried on the term table itself — 嫂子 beside 嫂 — and the lexicon has no entry to
	// attribute them to. The audit of 2026-08-02 found 208 of 904 alternates falling down that
	// branch and rendering BARE, with no source at all: an unattributed chip is the one thing a
	// reader cannot check.
	private const String RuleComposedLabel = "composed by rule";

	/// <summary>
	/// Layer ID -> English scholarly name, keyed by the stable slug in each YAML meta block rather
	/// than by the layer's own display name, so renaming a label cannot silently unhook it.
	///
	/// ALWAYS English (round-2 contract, R5/R6). The chip reads 爸爸 · everyday speech: the WORD is
	/// content and stays Han, the SOURCE is chrome. Where the field has a settled English name it
	/// is used — Wu, Yue (Cantonese), Min, Xiang, Hakka — rather than a literal rendering.
	///
	/// A layer with no entry here falls back to its own declared name rather than losing its label:
	/// an unfamiliar tag is readable, an absent one puts the chip back in the unattributed state.
	/// </summary>
	private static readonly Dictionary<String , String> LayerDisplayNames = new ( StringComparer.Ordinal )
	{
		// "Standard Mandarin", not "standard Chinese". The visible UI does not use the word
		// Chinese anywhere: the product is named for Sinitic and aimed at English speakers, and
		// naming the variety is both more precise and not a flag anyone needs to argue about.
		[ "lexicon-standard" ] = "Standard Mandarin" ,
		[ "register-colloquial" ] = "everyday speech" ,
		[ "register-literary" ] = "literary" ,
		// Settled English names for the branches, not literal renderings of the Chinese labels.
		[ "dialect-north" ] = "Northern" ,
		[ "dialect-south" ] = "Southern" ,
		[ "dialect-northwest" ] = "Northwestern" ,
		[ "dialect-southwest" ] = "Southwestern" ,
		[ "dialect-wu" ] = "Wu" ,
		[ "dialect-yue" ] = "Yue (Cantonese)" ,
		[ "dialect-min" ] = "Min" ,
		[ "dialect-xiang" ] = "Xiang" ,
		[ "dialect-hakka" ] = "Hakka"
	};

	/// <summary>Chinese layer display name -> its ID, built once from the loaded layer stack.</summary>
	private static readonly Lazy<Dictionary<String , String>> LayerIdByName = new ( () =>
	{
		Dictionary<String , String> map = new ( StringComparer.Ordinal );
		foreach ( KinshipCalculator.Core.Data.KinshipLexiconLayers.LayerInfo info
			in KinshipCalculator.Core.Data.KinshipLexiconLayers.Layers )
		{
			map [ info.Name ] = info.Id;
		}

		return map;
	} );

	/// <summary>
	/// The layer's English name. One language, because the source tag is chrome and chrome is
	/// English (round-2 contract, R5/R6).
	/// </summary>
	private static String? LocalizeLayerName ( String? layerName )
	{
		if ( layerName is null )
		{
			return null;
		}

		return LayerIdByName.Value.TryGetValue ( layerName , out String? id )
			&& LayerDisplayNames.TryGetValue ( id , out String? english )
				? english
				: layerName;
	}

	/// <summary>
	/// THE CONTENT SCRIPT, and it is not a user setting any more (round-2 contract, R5/U8).
	///
	/// The Language picker is gone from the window — removed, not hidden. Terms are always
	/// Traditional and the chrome around them is always English, so there is nothing left to
	/// choose. The property survives as the engine call's argument and as the seam the existing
	/// suite drives; it is no longer bound to anything on screen.
	/// </summary>
	public const String ContentLanguage = "zh-Hant";

	[ObservableProperty]
	private String selectedLanguage = ContentLanguage;

	[ObservableProperty]
	private string resultText = string.Empty;

	[ObservableProperty]
	private string pathText = string.Empty;

	[ObservableProperty]
	private string pathTextEnglish = string.Empty;

	// K15 layer ④ — raw calibration readback of what was entered, un-simplified.
	[ObservableProperty]
	private string rawChainText = string.Empty;

	[ObservableProperty]
	private bool isExactMatch;

	[ObservableProperty]
	private PersonGender selectedGender = PersonGender.Male;

	[ObservableProperty]
	private PersonGenderOption? selectedGenderOption;

	[ObservableProperty]
	private string notificationMessage = string.Empty;

	[ObservableProperty]
	private bool isNotificationVisible;

	public IRelayCommand<TokenDisplay> AppendTokenCommand { get; }

	/// <summary>Append through a key's variant menu, carrying that one press's origin (V2).</summary>
	public IRelayCommand<TokenVariant> AppendVariantCommand { get; }
	public IRelayCommand UndoCommand { get; }
	public IRelayCommand ClearCommand { get; }
	public IRelayCommand CopyPathCommand { get; }
	public IRelayCommand CopyResultCommand { get; }
	public IRelayCommand<QueryHistoryEntry> RestoreHistoryCommand { get; }
	public IRelayCommand ClearHistoryCommand { get; }

	/// <summary>True while a history entry is being restored, so the restore does not itself
	/// churn the list it was launched from.</summary>
	private Boolean isRestoring_field;

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
		AppendVariantCommand = new RelayCommand<TokenVariant> ( OnAppendVariant , static variant => variant is not null );
		UndoCommand = new RelayCommand ( OnUndo , () => appendHistory_field.Count > 0 );
		ClearCommand = new RelayCommand ( OnClear , () => sequence_field.Count > 0 );
		CopyPathCommand = new RelayCommand ( () => CopyText ( PathText , CopyPathHintZhHans , CopyPathHintZhHant , "Path copied" ) , () => !String.IsNullOrWhiteSpace ( PathText ) );
		CopyResultCommand = new RelayCommand ( () => CopyText ( ResultText , CopyResultHintZhHans , CopyResultHintZhHant , "Result copied" ) , () => !String.IsNullOrWhiteSpace ( ResultText ) );
		RestoreHistoryCommand = new RelayCommand<QueryHistoryEntry> ( OnRestoreHistory , static entry => entry is not null );
		ClearHistoryCommand = new RelayCommand (
			() =>
			{
				History.Clear ();
				ClearHistoryCommand!.NotifyCanExecuteChanged ();
				OnPropertyChanged ( nameof ( HistoryEmptyVisibility ) );
			} ,
			() => History.Count > 0 );

		// No language list is read from options any more: there is nothing to pick between. Terms
		// are Traditional, chrome is English (round-2 contract, R5/U8).
		GenderOptions.Add ( new PersonGenderOption ( PersonGender.Male , "Male" ) );
		GenderOptions.Add ( new PersonGenderOption ( PersonGender.Female , "Female" ) );

		SelectedLanguage = ContentLanguage;
		PersonGender defaultGender = options.DefaultGender == PersonGender.Female ? PersonGender.Female : PersonGender.Male;
		SelectedGender = defaultGender;
		SelectedGenderOption = GenderOptions.FirstOrDefault ( option => option.Value == defaultGender );

		foreach ( KinshipToken token in calculator_field.Tokens.Where ( t => String.IsNullOrEmpty ( t.Origin ) ) )
		{
			TokenDisplay display = new ( token , AppendTokenCommand );
			AddOriginVariants ( display );
			TokenButtons.Add ( display );
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

	/// <summary>
	/// The alternate forms a key offers (round-3 contract, V2), in real words rather than mode
	/// names. Only the four keys the engine can actually vary get any: 父 / 母 / 子 / 女.
	///
	/// 養X and 繼X reach the engine by completely different routes, and the words are the honest
	/// way to say so. 養父 is its own token; 繼父 is not a token at all but a rewrite of the path —
	/// 母→配偶, the mother's current husband — because a step-parent IS a relative reached through
	/// a marriage. The menu shows the reader the word; the engine keeps the distinction.
	///
	/// The five keys with no variants (兄 弟 姐 妹 配偶) are left exactly as they were.
	/// </summary>
	private void AddOriginVariants ( TokenDisplay display )
	{
		(String Adoptive, String Step)? words = display.Token.Id switch
		{
			"father" => ( "養父" , "繼父" ) ,
			"mother" => ( "養母" , "繼母" ) ,
			"son" => ( "養子" , "繼子" ) ,
			"daughter" => ( "養女" , "繼女" ) ,
			_ => null
		};

		if ( words is null )
		{
			return;
		}

		display.Variants.Add ( new TokenVariant ( display.Token , KinshipOrigin.Biological , display.Label , AppendVariantCommand ) );
		display.Variants.Add ( new TokenVariant ( display.Token , KinshipOrigin.Adoptive , words.Value.Adoptive , AppendVariantCommand ) );
		display.Variants.Add ( new TokenVariant ( display.Token , KinshipOrigin.Step , words.Value.Step , AppendVariantCommand ) );
	}

	/// <summary>A plain tap is always the birth relation — the same thing it did before.</summary>
	private void OnAppendToken ( TokenDisplay? display )
		=> Append ( display?.Token , KinshipOrigin.Biological );

	/// <summary>
	/// A press through the variant menu (round-3 V2). The origin travels WITH this one press and
	/// is gone afterwards; there is no mode to leave behind, which is what made the old radio row
	/// able to disagree with what the user thought it said.
	/// </summary>
	private void OnAppendVariant ( TokenVariant? variant )
		=> Append ( variant?.Token , variant?.Origin ?? KinshipOrigin.Biological );

	private void Append ( KinshipToken? token , KinshipOrigin origin )
	{
		if ( token is null )
		{
			return;
		}

		// Unchanged Core path: the menu supplies the origin the radio row used to hold.
		IReadOnlyList<String> tokensToAdd = KinshipSequenceBuilder.TranslateToken ( token , origin );
		if ( tokensToAdd.Count == 0 )
		{
			return;
		}

		foreach ( String id in tokensToAdd )
		{
			sequence_field.Add ( id );
		}
		appendHistory_field.Push ( tokensToAdd.Count );

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
		// The path in English, shown beneath the Chinese one rather than behind the language
		// picker. Reading the STRUCTURE of a relation should not require changing the language of
		// the answer — those are different questions, and folding both readings in makes the
		// separate 我的X的Y line unnecessary.
		PathTextEnglish = result.PathDisplay.ForLanguage ( "en" );
		RawChainText = result.RawChain.ForLanguage ( SelectedLanguage );
		IsExactMatch = result.IsExactMatch;
		// The "Descriptive reading (no single term composed)" note that used to sit here is gone,
		// ruled out by the operator on 2026-08-04. It was the last survivor of the
		// pair — "Named term (composed by rule)" had already been cut for telling the reader
		// nothing they could act on — and it goes for the same reason from the reader's side: a
		// 的-chain in the answer slot IS visibly a 的-chain, so labelling it as one restates what is
		// already on screen. StatusHint carried nothing else, so the whole property went with it
		// rather than being left behind as dead state.

		ResultOptions.Clear ();
		// Two DIFFERENT things must stay distinguishable here (acceptance contract F1): several
		// entries in result.Options are several possible RELATIONS, while the chips inside one
		// entry are other NAMES for that one relation. Only the first gets a reading label, and
		// only when there is genuinely more than one reading.
		Int32 readingCount = result.Options.Count;
		Int32 readingIndex = 0;

		// Readings that come out as the SAME word are the case the 的-chain exists for: 父→父→女
		// names 姑母 twice, and "possible relation 1 / 2" does not say which sister. Everywhere
		// else the chain merely restates the path line, so it is computed here and shown only
		// where it separates something.
		HashSet<String> repeatedLabels = new (
			result.Options
				.GroupBy ( o => o.Label.ForLanguage ( SelectedLanguage ) , StringComparer.Ordinal )
				.Where ( g => g.Count () > 1 )
				.Select ( g => g.Key ) ,
			StringComparer.Ordinal );

		foreach ( KinshipResolutionOption option in result.Options )
		{
			readingIndex++;
			String label = option.Label.ForLanguage ( SelectedLanguage );
			String standard = label;
			String colloquial = option.HasAlternateLabel
				? option.AlternateLabel.ForLanguage ( SelectedLanguage )
				: "—"; // no colloquial variant
			// The line under the term is the relation's ENGLISH NAME, always in English whatever
			// script the term itself is in.
			//
			// It used to read OfficialDescription, which is the engine's own structural
			// coordinates — and it showed. Swept over every one- to three-token path: that field
			// is EMPTY for 266 of 955 results (父親 among them) and machine-shaped for 244 more
			// ("Self → ancestor +2 sibling line (female)" for 姑祖母), so more than half the
			// answers had a bad line under them. Label carries a proper name for all 955 —
			// Grandaunt, Step-mother, Daughter-in-law — because the formatters were already
			// building one and nothing was reading it.
			String official = option.Label.ForLanguage ( "en" );

			ResultInterpretation interpretation = new (
				standard ,
				colloquial ,
				official ,
				option.Explanation ,
				option.IsExactMatch ,
				option.DescriptiveChain.ForLanguage ( SelectedLanguage ) ,
				// The reading label is chrome, so it is English like the rest of the chrome. The
				// 的-chain beside it is CONTENT — a Chinese description of the relation — and stays
				// Han (round-2 contract, R5 boundary).
				readingCount > 1 ? $"Possible relation {readingIndex} of {readingCount}" : String.Empty ,
				chainDisambiguates: repeatedLabels.Contains ( label )
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
					interpretation.Variants.Add ( new VariantChip (
						variant ,
						LocalizeLayerName ( layer ) ?? RuleComposedLabel ) );
				}
			}

			// The other spelling of the SAME word (姪子 / 侄子). Both are current in Traditional
			// writing, so a Traditional display that only ever shows 姪 hides a spelling the
			// reader may be looking for — the script converter rewrites it away before it can be
			// offered. Labelled as a glyph rather than a place, and only when it really differs
			// from what is already on screen.
			//
			// Placed FIRST, ahead of the dialect words. It is the same word rather than another
			// one, so it is the closest thing to the answer above it; and putting it last buried
			// it below the fold of the scrolling chip list, which for the one question that
			// started this item is the same as not showing it.
			String? alternateGlyph = KinshipCalculator.Core.Services.Formatting.KinshipGlyphVariants
				.TryGetAlternateSpelling ( standard );
			if ( alternateGlyph is not null
				&& !interpretation.Variants.Any ( v => String.Equals ( v.Term , alternateGlyph , StringComparison.Ordinal ) ) )
			{
				interpretation.Variants.Insert ( 0 , new VariantChip (
					alternateGlyph ,
					VariantGlyphLabel ,
					isGlyphVariant: true ) );
			}

			BuildVariantGroups ( interpretation );
			ResultOptions.Add ( interpretation );
		}

		RecordHistory ();

		CopyPathCommand.NotifyCanExecuteChanged ();
		CopyResultCommand.NotifyCanExecuteChanged ();
	}

	/// <summary>
	/// Rank of a source tag in the grouped list (round-3 contract, V3). Lexicon layers keep the
	/// stack order they load in; the two tags that are not layers at all go last, because "the
	/// same word spelled the other way" and "the rules made this" are statements about the entry
	/// rather than places it is said.
	/// </summary>
	private static Int32 GroupRank ( String header )
	{
		if ( String.Equals ( header , VariantGlyphLabel , StringComparison.Ordinal ) ) { return Int32.MaxValue - 1; }
		if ( String.Equals ( header , RuleComposedLabel , StringComparison.Ordinal ) ) { return Int32.MaxValue; }

		Int32 index = 0;
		foreach ( KinshipCalculator.Core.Data.KinshipLexiconLayers.LayerInfo info
			in KinshipCalculator.Core.Data.KinshipLexiconLayers.Layers )
		{
			if ( String.Equals ( LocalizeLayerName ( info.Name ) , header , StringComparison.Ordinal ) )
			{
				return index;
			}

			index++;
		}

		// An unrecognised tag sorts just ahead of the two non-layer ones rather than to the front,
		// so a layer added later cannot silently displace 標準 from the top of the list.
		return Int32.MaxValue - 2;
	}

	/// <summary>
	/// Turns the flat chip list into sections (round-3 contract, V3).
	///
	/// The flat list repeated its own labels: 爸爸 · everyday speech, 老爸 · everyday speech,
	/// 老父親 · everyday speech, and the eye had to strip the same suffix off every chip to find
	/// the words. Seventeen chips carried seven distinct tags. The tag becomes a heading printed
	/// once and the chips go back to being bare words.
	///
	/// The attribution MOVES, it does not go away: every chip is still under a heading that names
	/// where it came from, which is the whole point the earlier rounds fought over.
	/// </summary>
	private static void BuildVariantGroups ( ResultInterpretation interpretation )
	{
		interpretation.VariantGroups.Clear ();

		foreach ( IGrouping<String , VariantChip> group in interpretation.Variants
			.GroupBy ( v => v.LayerName , StringComparer.Ordinal )
			.OrderBy ( g => GroupRank ( g.Key ) ) )
		{
			VariantGroup section = new ( group.Key );
			// Order within a group is left exactly as the lexicon supplied it.
			foreach ( VariantChip chip in group )
			{
				section.Chips.Add ( chip );
			}

			interpretation.VariantGroups.Add ( section );
		}
	}

	/// <summary>
	/// Files the query just evaluated. Identity is (path, ego gender, language) — the same path
	/// read in another language or for another ego is a DIFFERENT question with a different
	/// answer, and the contract asks for all three to be stored. A repeat of an identical
	/// question moves to the front instead of stacking, so the list stays a set of questions
	/// rather than a keystroke log.
	/// </summary>
	private void RecordHistory ()
	{
		if ( isRestoring_field || sequence_field.Count == 0 || String.IsNullOrWhiteSpace ( ResultText ) )
		{
			return;
		}

		QueryHistoryEntry entry = new (
			sequence_field.ToArray () ,
			SelectedGender ,
			SelectedLanguage ,
			PathText ,
			ResultText ,
			RestoreHistoryCommand );

		for ( Int32 i = History.Count - 1 ; i >= 0 ; i-- )
		{
			if ( History [ i ].HasSameQuestion ( entry ) )
			{
				History.RemoveAt ( i );
			}
		}

		History.Insert ( 0 , entry );
		while ( History.Count > HistoryLimit )
		{
			History.RemoveAt ( History.Count - 1 );
		}

		ClearHistoryCommand.NotifyCanExecuteChanged ();
		OnPropertyChanged ( nameof ( HistoryEmptyVisibility ) );
	}

	/// <summary>
	/// Puts back enough INPUT state to reproduce the entry — language and ego gender first, then
	/// the path — rather than pasting the old text back. Reproducing means the engine runs
	/// again, so a restored entry reflects the current engine, not a cached string.
	/// </summary>
	private void OnRestoreHistory ( QueryHistoryEntry? entry )
	{
		if ( entry is null )
		{
			return;
		}

		isRestoring_field = true;
		try
		{
			SelectedLanguage = entry.Language;
			PersonGenderOption? gender = GenderOptions.FirstOrDefault ( g => g.Value == entry.EgoGender );
			if ( gender is not null )
			{
				SelectedGenderOption = gender;
			}

			SelectedGender = entry.EgoGender;

			sequence_field.Clear ();
			sequence_field.AddRange ( entry.TokenIds );
			appendHistory_field.Clear ();
			appendHistory_field.Push ( entry.TokenIds.Count );

			UpdateTokenLabels ();
			Recalculate ();
		}
		finally
		{
			isRestoring_field = false;
		}

		UndoCommand.NotifyCanExecuteChanged ();
		ClearCommand.NotifyCanExecuteChanged ();
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

	/// <summary>
	/// The key's alternate forms, shown on press-and-hold / right-click (round-3 contract, V2).
	/// Empty for the five keys that have none.
	///
	/// This replaced the Relation-is-by radio row. A three-way MODE is a control that can be
	/// telling the truth or lying depending on when you look at it — the user pressed 父 expecting
	/// the mode he had set and got something else, because the mode reset itself after each press.
	/// Per-press selection has no such gap: what the menu says is what that one press does, and
	/// there is no state left over to disagree with afterwards.
	/// </summary>
	public List<TokenVariant> Variants { get; } = [];

	public Boolean HasVariants => Variants.Count > 0;
}

/// <summary>
/// One alternate form of a key: the word the user picks, and the origin that press runs under.
/// The label is a REAL WORD (養父, 繼父), not an abstract mode name — 「Adoption」 asked the reader
/// to hold a category in their head and apply it to whatever they pressed next; 養父 does not.
/// </summary>
public sealed class TokenVariant
{
	public TokenVariant ( KinshipToken token , KinshipOrigin origin , String label , ICommand appendCommand )
	{
		Token = token;
		Origin = origin;
		Label = label;
		AppendCommand = appendCommand;
	}

	public KinshipToken Token { get; }
	public KinshipOrigin Origin { get; }
	public String Label { get; }
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

// IsBiologicalOrigin / IsAdoptiveOrigin / IsStepOrigin lived here as two-way bindings for the
// three RadioButtons. Round-3 V2 removed the row and the mode behind it: the three forms are on
// the keys now and travel with a single press. Nothing binds these, so nothing keeps them.

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
	public ResultInterpretation (
		String standard , String colloquial , String official , String explanation , Boolean isExact ,
		String descriptiveChain = "" , String readingLabel = "" ,
		Boolean chainDisambiguates = false )
	{
		ChainDisambiguates = chainDisambiguates;
		StandardLabel = standard;
		ColloquialLabel = colloquial;
		OfficialLabel = official;
		Explanation = explanation;
		IsExact = isExact;
		DescriptiveChain = descriptiveChain;
		ReadingLabel = readingLabel;
	}

	public String StandardLabel { get; }
	public String ColloquialLabel { get; }
	/// <summary>
	/// The relation's English name (Grandaunt, Step-mother, Daughter-in-law). Hidden when the
	/// term on screen already IS that word — an English session showing "Father" under "Father"
	/// is a line spent saying nothing.
	/// </summary>
	public String OfficialLabel { get; }

	public Visibility EnglishNameVisibility
		=> String.IsNullOrWhiteSpace ( OfficialLabel )
			|| String.Equals ( OfficialLabel , StandardLabel , StringComparison.OrdinalIgnoreCase )
				? Visibility.Collapsed
				: Visibility.Visible;
	public String Explanation { get; }
	public Boolean IsExact { get; }

	/// <summary>K15 layer ③ — legal-document chain (父的父的兄), never contracted.</summary>
	public String DescriptiveChain { get; }

	/// <summary>
	/// Whether the 的-chain is the ONLY thing separating this reading from another one on screen.
	/// 父→父→女 gives 姑母 twice — the father's elder sister and his younger sister — and the
	/// numbered "possible relation" label cannot tell those apart.
	///
	/// The chain used to print under every result, where it restated the path line directly above
	/// it: 自己→兄→子, then 我的兄的子, then 兄的子 — one fact said three times. It now appears
	/// where it is doing work and stays out of the way where it is not.
	/// </summary>
	public Boolean ChainDisambiguates { get; }

	public Visibility DisambiguationVisibility
		=> ChainDisambiguates ? Visibility.Visible : Visibility.Collapsed;

	/// <summary>
	/// Alternate terms with their source layer (南系 / 北系 / 通用口語 / user layer). These are
	/// CANDIDATES only — the primary answer stays <see cref="StandardLabel"/>.
	/// </summary>
	public ObservableCollection<VariantChip> Variants { get; } = [];

	/// <summary>
	/// The same chips, sectioned by source tag (round-3 contract, V3). This is what the card
	/// renders; <see cref="Variants"/> stays as the flat truth the grouping is derived from and
	/// the tests assert against.
	/// </summary>
	public ObservableCollection<VariantGroup> VariantGroups { get; } = [];

	public Boolean HasVariants => Variants.Count > 0;

	/// <summary>
	/// Shown INSTEAD of the chip list when nothing is registered. An empty column reads as a
	/// broken feature — the reader cannot tell "no other name exists" from "the app lost them",
	/// which is exactly the confusion this whole item came from.
	/// </summary>
	// NoVariantsNotice was here. Round-2 contract U9 removed it: the whole Other-names block now
	// stays unrendered when there is nothing in it, so there is no caption to write.

	/// <summary>
	/// "Possible relation 1 / 2" when one input admits several RELATION readings. Empty when
	/// there is only one, so the single-answer case stays uncluttered. This is a different axis
	/// from <see cref="Variants"/>: those are other NAMES for one relation, this is another
	/// relation. The two must never read as one undifferentiated list.
	/// </summary>
	public String ReadingLabel { get; }

	// Typed visibilities: WinUI has no implicit bool-to-Visibility conversion and the project
	// registers no converters, so the view model states it directly.
	public Visibility VariantsVisibility => Variants.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

	// VariantsVisibility governs the WHOLE block, heading included. Its counterpart
	// NoVariantsVisibility is gone with the notice it used to reveal.

	public Visibility ReadingLabelVisibility => String.IsNullOrEmpty ( ReadingLabel ) ? Visibility.Collapsed : Visibility.Visible;
}

/// <summary>
/// One finished query, kept so the user can go back to it. Stores the four things the acceptance
/// contract asks for — path, ego gender, display language, and the primary answer at the time —
/// plus the token ids, which are what actually lets the input state be rebuilt rather than the
/// old text merely re-shown.
/// </summary>
public sealed class QueryHistoryEntry
{
	public QueryHistoryEntry (
		IReadOnlyList<String> tokenIds , PersonGender egoGender , String language , String pathDisplay , String resultText ,
		ICommand? restoreCommand = null )
	{
		TokenIds = tokenIds;
		EgoGender = egoGender;
		Language = language;
		PathDisplay = pathDisplay;
		ResultText = resultText;
		RestoreCommand = restoreCommand;
	}

	/// <summary>
	/// Carried ON the entry rather than reached through the page.
	///
	/// A flyout lives in its own popup tree, so an ElementName binding out to the page's data
	/// context does not resolve — the command silently lands as null and the row becomes inert
	/// while still looking like a button. That is what shipped for the length of one build, and
	/// only driving the real UI caught it: every view-model test passed the whole time, because
	/// the command they exercise is the one the XAML failed to reach.
	/// </summary>
	public ICommand? RestoreCommand { get; }

	public IReadOnlyList<String> TokenIds { get; }
	public PersonGender EgoGender { get; }
	public String Language { get; }
	public String PathDisplay { get; }
	public String ResultText { get; }

	/// <summary>
	/// Same QUESTION, not same object: identical path, ego and language. Used to move a repeat
	/// to the front instead of letting the list fill with duplicates.
	/// </summary>
	public Boolean HasSameQuestion ( QueryHistoryEntry other )
		=> EgoGender == other.EgoGender
		&& String.Equals ( Language , other.Language , StringComparison.Ordinal )
		&& TokenIds.Count == other.TokenIds.Count
		&& TokenIds.Zip ( other.TokenIds ).All ( pair => String.Equals ( pair.First , pair.Second , StringComparison.Ordinal ) );

	/// <summary>Ego gender shown next to the entry — the same answer differs by who is asking.</summary>
	public String EgoDisplay => EgoGender switch
	{
		PersonGender.Male => "♂",
		PersonGender.Female => "♀",
		_ => "?"
	};

	public String Display => $"{ResultText}    {PathDisplay}";
}

/// <summary>
/// One alternate term plus where it came from. Two KINDS live here and must stay tellable
/// apart (acceptance contract): a different WORD for this relation (from a dialect or register
/// layer), and the same word written with the other GLYPH (侄子 for 姪子). Labelling both
/// "candidate" and leaving the reader to guess is what the contract forbids.
/// </summary>
/// <summary>
/// One section of the Other-names list: a source tag printed once, and the words that came from
/// it (round-3 contract, V3).
/// </summary>
public sealed class VariantGroup
{
	public VariantGroup ( String header )
	{
		Header = header;
	}

	public String Header { get; }

	public ObservableCollection<VariantChip> Chips { get; } = [];
}

public sealed class VariantChip
{
	public VariantChip ( String term , String layerName , Boolean isGlyphVariant = false )
	{
		Term = term;
		LayerName = layerName;
		IsGlyphVariant = isGlyphVariant;
	}

	public String Term { get; }
	public String LayerName { get; }
	public Boolean IsGlyphVariant { get; }
	public Boolean HasLayer => !String.IsNullOrEmpty ( LayerName );
	public String Display => HasLayer ? $"{Term} · {LayerName}" : Term;
}
