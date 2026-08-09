using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using KinshipCalculator.Core.Models;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KinshipCalculator.Core.Data;

/// <summary>
/// K15/K16 lexicon layer stack. The engine holds only the GENERATIVE machinery — every
/// looked-up surface word lives in data:
///   • <c>lexicon-standard</c> (base)  — standard-Chinese terms the morpheme machine cannot
///     derive (公公/婆婆/岳父…), keyed by chain + ego gender; always primary.
///   • <c>register-colloquial</c>      — nationwide colloquial variants, keyed by standard form.
///   • <c>dialect-north</c> / <c>dialect-south</c> — regional variants, same keying.
/// Built-in layers ship EMBEDDED (the single-file publish extracts to %TEMP%, so a directory
/// walk-up cannot find loose files); users may drop extra <c>*.yaml</c> layers into a
/// <c>Lexicon</c> folder beside the executable and they stack on top.
/// </summary>
public static class KinshipLexiconLayers
{
	private const String EmbeddedPrefix = "Lexicon.";

	public sealed record LayerInfo ( String Id , String Name , String Layer , String Provenance , Boolean DefaultEnabled );

	public sealed record LexemeEntry ( String Key , String Male , String Female , String Gloss );

	public sealed record VariantEntry ( String LayerId , String LayerName , String Term );

	private sealed class LayerFile
	{
		public Dictionary<String , String>? Meta { get; set; }
		public List<Dictionary<String , String>>? Entries { get; set; }
		public Dictionary<String , List<String>>? Variants { get; set; }

		/// <summary>
		/// Variants that only one ego may use. 配偶 is a single standard form covering both
		/// spouses, so a flat variants list would offer 老公 and 老婆 to the same person. These
		/// two blocks carry the ego-gender dimension the base layer's entries already have.
		/// </summary>
		public Dictionary<String , List<String>>? VariantsMale { get; set; }

		public Dictionary<String , List<String>>? VariantsFemale { get; set; }
	}

	private static readonly Lazy<LoadedLayers> Loaded = new ( Load );

	private sealed record LoadedLayers (
		IReadOnlyList<LayerInfo> Layers ,
		IReadOnlyDictionary<String , LexemeEntry> Lexemes ,
		IReadOnlyDictionary<String , IReadOnlyList<VariantEntry>> Variants ,
		IReadOnlyDictionary<String , IReadOnlyList<VariantEntry>> MaleEgoVariants ,
		IReadOnlyDictionary<String , IReadOnlyList<VariantEntry>> FemaleEgoVariants ,
		IReadOnlyDictionary<String , String> TermOwners );

	/// <summary>Every layer that loaded, in stack order (base first).</summary>
	public static IReadOnlyList<LayerInfo> Layers => Loaded.Value.Layers;

	/// <summary>
	/// Standard-Chinese term for a relation the morpheme machine cannot derive, or null.
	/// <paramref name="chainKey"/> is the dotted symbol path (SP.F, D.SP.M…).
	/// </summary>
	public static String? TryGetStandardLexeme ( String chainKey , PersonGender egoGender )
	{
		if ( !Loaded.Value.Lexemes.TryGetValue ( chainKey , out LexemeEntry? entry ) )
		{
			return null;
		}

		return egoGender switch
		{
			PersonGender.Male => String.IsNullOrWhiteSpace ( entry.Male ) ? null : entry.Male ,
			PersonGender.Female => String.IsNullOrWhiteSpace ( entry.Female ) ? null : entry.Female ,
			// Unknown ego: only unambiguous entries answer (亲家公 is gender-neutral).
			_ => String.Equals ( entry.Male , entry.Female , StringComparison.Ordinal ) ? entry.Male : null
		};
	}

	/// <summary>
	/// Layer variants registered against a standard form (伯祖父 → 伯公 [南] / 大爷爷 [北]),
	/// in stack order. Empty when no layer covers it.
	/// </summary>
	public static IReadOnlyList<VariantEntry> GetVariants ( String standardForm )
		=> Loaded.Value.Variants.TryGetValue ( standardForm , out IReadOnlyList<VariantEntry>? list )
			? list
			: Array.Empty<VariantEntry> ();

	/// <summary>
	/// Layer variants a given ego may use for a standard form: the gender-neutral ones plus the
	/// ones registered for that ego. An unknown ego gets only the neutral set, because offering
	/// both 老公 and 老婆 is worse than offering neither.
	/// </summary>
	public static IReadOnlyList<VariantEntry> GetVariants ( String standardForm , PersonGender egoGender )
	{
		IReadOnlyDictionary<String , IReadOnlyList<VariantEntry>>? gendered = egoGender switch
		{
			PersonGender.Male => Loaded.Value.MaleEgoVariants ,
			PersonGender.Female => Loaded.Value.FemaleEgoVariants ,
			_ => null
		};

		IReadOnlyList<VariantEntry> neutral = GetVariants ( standardForm );
		if ( gendered is null || !gendered.TryGetValue ( standardForm , out IReadOnlyList<VariantEntry>? extra ) )
		{
			return neutral;
		}

		return neutral.Count == 0 ? extra : neutral.Concat ( extra ).ToArray ();
	}

	/// <summary>
	/// Layer variants for a standard form as a '|'-joined set (empty when none). De-duplicated:
	/// a word current in several regions (大大 in both 北系 and 西北) is registered by each layer
	/// that uses it, and the reader should see it once. Attribution still goes to the first
	/// layer in stack order, matching <see cref="TryGetLayerNameForTerm"/>.
	/// </summary>
	public static String GetVariantSet ( String standardForm )
		=> String.Join ( '|' , GetVariants ( standardForm ).Select ( v => v.Term ).Distinct ( StringComparer.Ordinal ) );

	/// <summary>Ego-aware form of <see cref="GetVariantSet(String)"/>.</summary>
	public static String GetVariantSet ( String standardForm , PersonGender egoGender )
		=> String.Join ( '|' , GetVariants ( standardForm , egoGender ).Select ( v => v.Term ).Distinct ( StringComparer.Ordinal ) );

	/// <summary>
	/// Every standard form the loaded layers register variants against. A key nothing in the
	/// engine can emit is dead data — the lookup is keyed by the computed standard form, so a
	/// layer keyed on a colloquial word (伯伯 instead of 伯父) is never consulted. Exposed so a
	/// test can assert reachability instead of leaving that failure silent.
	/// </summary>
	public static IReadOnlyCollection<String> VariantKeys
		=> Loaded.Value.Variants.Keys
			.Concat ( Loaded.Value.MaleEgoVariants.Keys )
			.Concat ( Loaded.Value.FemaleEgoVariants.Keys )
			.Distinct ( StringComparer.Ordinal )
			.ToArray ();

	/// <summary>
	/// LAYER-PROVENANCE lookup: which layer registers this surface word (南系 / 北系 / 通用口語…),
	/// or null when no layer owns it — i.e. the naming rules composed it. Lets a UI tag each
	/// candidate with its provenance without threading a new field through the whole result
	/// pipeline. A word claimed by several layers reports the first in stack order.
	///
	/// Deliberately NOT called "reverse lookup", which it was until the audit of 2026-08-02
	/// pointed out that the name lays claim to a product feature this is not. Reverse kinship-term
	/// lookup answers "which relationship PATHS could this word mean" and is deferred; this
	/// answers only "which layer file does this word come from" — term → provenance, never
	/// term → relation.
	/// </summary>
	public static String? TryGetLayerNameForTerm ( String term )
		=> Loaded.Value.TermOwners.TryGetValue ( term , out String? layerName ) ? layerName : null;

	private static LoadedLayers Load ()
	{
		List<LayerInfo> layers = new ();
		Dictionary<String , LexemeEntry> lexemes = new ( StringComparer.Ordinal );
		Dictionary<String , List<VariantEntry>> variants = new ( StringComparer.Ordinal );
		Dictionary<String , List<VariantEntry>> maleEgo = new ( StringComparer.Ordinal );
		Dictionary<String , List<VariantEntry>> femaleEgo = new ( StringComparer.Ordinal );

		IDeserializer deserializer = new DeserializerBuilder ()
			.WithNamingConvention ( UnderscoredNamingConvention.Instance )
			.IgnoreUnmatchedProperties ()
			.Build ();

		HashSet<String> seenLayerIds = new ( StringComparer.OrdinalIgnoreCase );
		foreach ( (String source, String text) in ReadAllLayerSources () )
		{
			LayerFile? file;
			try
			{
				file = deserializer.Deserialize<LayerFile> ( text );
			}
			catch ( Exception )
			{
				continue; // a malformed user layer must never break the engine
			}

			if ( file?.Meta is null )
			{
				continue;
			}

			file.Meta.TryGetValue ( "id" , out String? id );
			file.Meta.TryGetValue ( "name" , out String? name );
			file.Meta.TryGetValue ( "layer" , out String? layer );
			file.Meta.TryGetValue ( "provenance" , out String? provenance );
			file.Meta.TryGetValue ( "default_enabled" , out String? enabledText );
			id ??= source;

			// The built-in layers ship BOTH embedded and as editable files next to the exe.
			// Loose files are read first (see ReadAllLayerSources), so a file that reuses a
			// built-in id REPLACES it — that is how a user edits a shipped layer. Without
			// this guard the same variants would load twice and every term would double.
			if ( !seenLayerIds.Add ( id ) )
			{
				continue;
			}

			LayerInfo info = new (
				id , name ?? id , layer ?? "dialect" , provenance ?? String.Empty ,
				!String.Equals ( enabledText , "false" , StringComparison.OrdinalIgnoreCase ) );
			layers.Add ( info );

			foreach ( Dictionary<String , String> row in file.Entries ?? new () )
			{
				row.TryGetValue ( "key" , out String? key );
				if ( String.IsNullOrWhiteSpace ( key ) )
				{
					continue;
				}

				row.TryGetValue ( "male" , out String? male );
				row.TryGetValue ( "female" , out String? female );
				row.TryGetValue ( "gloss" , out String? gloss );
				lexemes [ key ] = new LexemeEntry ( key , male ?? String.Empty , female ?? String.Empty , gloss ?? String.Empty );
			}

			Collect ( file.Variants , variants , info );
			Collect ( file.VariantsMale , maleEgo , info );
			Collect ( file.VariantsFemale , femaleEgo , info );
		}

		Dictionary<String , String> termOwners = new ( StringComparer.Ordinal );
		foreach ( List<VariantEntry> list in variants.Values.Concat ( maleEgo.Values ).Concat ( femaleEgo.Values ) )
		{
			foreach ( VariantEntry entry in list )
			{
				// First layer in stack order wins; a later layer reusing the same word does
				// not steal the attribution.
				if ( !termOwners.ContainsKey ( entry.Term ) )
				{
					termOwners [ entry.Term ] = entry.LayerName;
				}
			}
		}

		return new LoadedLayers (
			layers ,
			lexemes ,
			Freeze ( variants ) ,
			Freeze ( maleEgo ) ,
			Freeze ( femaleEgo ) ,
			termOwners );
	}

	private static void Collect (
		Dictionary<String , List<String>>? source ,
		Dictionary<String , List<VariantEntry>> target ,
		LayerInfo info )
	{
		foreach ( KeyValuePair<String , List<String>> pair in source ?? new () )
		{
			if ( !target.TryGetValue ( pair.Key , out List<VariantEntry>? list ) )
			{
				list = new List<VariantEntry> ();
				target [ pair.Key ] = list;
			}

			foreach ( String term in pair.Value ?? new List<String> () )
			{
				if ( !String.IsNullOrWhiteSpace ( term ) )
				{
					list.Add ( new VariantEntry ( info.Id , info.Name , term ) );
				}
			}
		}
	}

	private static IReadOnlyDictionary<String , IReadOnlyList<VariantEntry>> Freeze ( Dictionary<String , List<VariantEntry>> source )
		=> source.ToDictionary ( p => p.Key , p => (IReadOnlyList<VariantEntry>) p.Value , StringComparer.Ordinal );

	private static IEnumerable<(String Source, String Text)> ReadAllLayerSources ()
	{
		// USER LAYERS FIRST. The `Lexicon` folder beside the executable holds editable copies
		// of the built-in layers plus anything the user adds; reading it first lets a file
		// override the embedded layer that shares its id (the caller de-duplicates by id).
		String userDirectory = Path.Combine ( AppContext.BaseDirectory , "Lexicon" );
		if ( Directory.Exists ( userDirectory ) )
		{
			foreach ( String path in Directory.EnumerateFiles ( userDirectory , "*.yaml" ).OrderBy ( p => p , StringComparer.Ordinal ) )
			{
				String userText;
				try
				{
					userText = File.ReadAllText ( path );
				}
				catch ( IOException )
				{
					continue;
				}

				yield return ( Path.GetFileNameWithoutExtension ( path ) , userText );
			}
		}

		// Embedded fallback. Order is DECLARED, not alphabetical: base defines the canonical
		// forms, the nationwide colloquial register precedes the regional layers, and the
		// regional layers follow in a fixed order so the alternate list is stable.
		// Matched on the exact stem, not Contains: "Lexicon.dialect-northwest.yaml" CONTAINS
		// "dialect-north", so a substring test silently promoted the north-west layer into the
		// northern layer's slot and moved every layer after it — which decides who owns a word
		// shared by several regions (大大 is both 北系 and 西北).
		String[] builtInOrder = [ "lexicon-standard" , "register-colloquial" , "dialect-north" , "dialect-south" ];
		Assembly assembly = typeof ( KinshipLexiconLayers ).Assembly;
		foreach ( String resource in assembly.GetManifestResourceNames ()
			.Where ( n => n.StartsWith ( EmbeddedPrefix , StringComparison.Ordinal ) )
			.OrderBy ( n =>
			{
				String stem = ResourceStem ( n );
				Int32 index = Array.IndexOf ( builtInOrder , stem );
				return index < 0 ? builtInOrder.Length : index;
			} )
			.ThenBy ( n => n , StringComparer.Ordinal ) )
		{
			using Stream? stream = assembly.GetManifestResourceStream ( resource );
			if ( stream is null )
			{
				continue;
			}

			using StreamReader reader = new ( stream );
			yield return ( resource [ EmbeddedPrefix.Length.. ] , reader.ReadToEnd () );
		}
	}

	/// <summary>Bare layer name of an embedded resource: "Lexicon.dialect-north.yaml" → "dialect-north".</summary>
	private static String ResourceStem ( String resourceName )
	{
		String withoutPrefix = resourceName [ EmbeddedPrefix.Length.. ];
		Int32 dot = withoutPrefix.LastIndexOf ( '.' );
		return dot < 0 ? withoutPrefix : withoutPrefix [ ..dot ];
	}
}
