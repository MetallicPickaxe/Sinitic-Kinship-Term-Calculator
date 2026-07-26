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
	}

	private static readonly Lazy<LoadedLayers> Loaded = new ( Load );

	private sealed record LoadedLayers (
		IReadOnlyList<LayerInfo> Layers ,
		IReadOnlyDictionary<String , LexemeEntry> Lexemes ,
		IReadOnlyDictionary<String , IReadOnlyList<VariantEntry>> Variants ,
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

	/// <summary>Layer variants for a standard form as a '|'-joined set (empty when none).</summary>
	public static String GetVariantSet ( String standardForm )
		=> String.Join ( '|' , GetVariants ( standardForm ).Select ( v => v.Term ) );

	/// <summary>
	/// Reverse lookup: which layer registers this surface word (南系 / 北系 / 通用口語…), or
	/// null when no layer owns it — i.e. the engine computed it. Lets a UI tag each candidate
	/// with its provenance without threading a new field through the whole result pipeline.
	/// A word claimed by several layers reports the first in stack order.
	/// </summary>
	public static String? TryGetLayerNameForTerm ( String term )
		=> Loaded.Value.TermOwners.TryGetValue ( term , out String? layerName ) ? layerName : null;

	private static LoadedLayers Load ()
	{
		List<LayerInfo> layers = new ();
		Dictionary<String , LexemeEntry> lexemes = new ( StringComparer.Ordinal );
		Dictionary<String , List<VariantEntry>> variants = new ( StringComparer.Ordinal );

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

			foreach ( KeyValuePair<String , List<String>> pair in file.Variants ?? new () )
			{
				if ( !variants.TryGetValue ( pair.Key , out List<VariantEntry>? list ) )
				{
					list = new List<VariantEntry> ();
					variants [ pair.Key ] = list;
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

		Dictionary<String , String> termOwners = new ( StringComparer.Ordinal );
		foreach ( List<VariantEntry> list in variants.Values )
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
			variants.ToDictionary ( p => p.Key , p => (IReadOnlyList<VariantEntry>) p.Value , StringComparer.Ordinal ) ,
			termOwners );
	}

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
		String[] builtInOrder = [ "lexicon-standard" , "register-colloquial" , "dialect-north" , "dialect-south" ];
		Assembly assembly = typeof ( KinshipLexiconLayers ).Assembly;
		foreach ( String resource in assembly.GetManifestResourceNames ()
			.Where ( n => n.StartsWith ( EmbeddedPrefix , StringComparison.Ordinal ) )
			.OrderBy ( n =>
			{
				Int32 index = Array.FindIndex ( builtInOrder , key => n.Contains ( key , StringComparison.Ordinal ) );
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
}
