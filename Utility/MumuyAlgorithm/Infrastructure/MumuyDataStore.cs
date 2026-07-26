using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace MumuyAlgorithm.Infrastructure;

internal sealed class MumuyDataStore
{
	public IReadOnlyDictionary<String , String[]> ModeMap { get; }
	public IReadOnlyDictionary<String , String[]> Cache { get; }
	public IReadOnlyDictionary<String , String[]> Sort { get; }
	public IReadOnlyDictionary<String , String[]> Pair { get; }
	public IReadOnlyList<RegexRule> Filters { get; }

	private MumuyDataStore (
		IReadOnlyDictionary<String , String[]> modeMap ,
		IReadOnlyDictionary<String , String[]> cache ,
		IReadOnlyDictionary<String , String[]> sort ,
		IReadOnlyDictionary<String , String[]> pair ,
		IReadOnlyList<RegexRule> filters )
	{
		ModeMap = modeMap;
		Cache = cache;
		Sort = sort;
		Pair = pair;
		Filters = filters;
	}

	public static MumuyDataStore Load ()
	{
		try
		{
			Assembly assembly = Assembly.GetExecutingAssembly ();
			JsonSerializerOptions options = new JsonSerializerOptions
			{
				ReadCommentHandling = JsonCommentHandling.Skip ,
				AllowTrailingCommas = true
			};

			static Dictionary<String , String[]> ReadDictionary (
				Assembly assembly ,
				JsonSerializerOptions options ,
				String resourceName )
			{
				using Stream stream = assembly.GetManifestResourceStream ( resourceName )
					?? throw new InvalidOperationException ( $"Resource '{resourceName}' not found." );

				using StreamReader reader = new StreamReader ( stream );
				String json = reader.ReadToEnd ();
				return JsonSerializer.Deserialize<Dictionary<String , String[]>> ( json , options )
					?? throw new InvalidOperationException ( $"Failed to deserialize '{resourceName}'." );
			}

			String baseName = typeof ( MumuyDataStore ).Namespace?.Split ( '.' )[ 0 ] ?? "MumuyAlgorithm";

			Dictionary<String , String[]> modeMap = ReadDictionary ( assembly , options , $"{baseName}.Data.mode-map.json" );
			Dictionary<String , String[]> cache = ReadDictionary ( assembly , options , $"{baseName}.Data.cache.json" );
			Dictionary<String , String[]> sort = ReadDictionary ( assembly , options , $"{baseName}.Data.sort.json" );
			Dictionary<String , String[]> pair = ReadDictionary ( assembly , options , $"{baseName}.Data.pair.json" );
			List<FilterRuleModel> filterModels = ReadFromJson<List<FilterRuleModel>> ( assembly , options , $"{baseName}.Data.filter.json" );

			List<RegexRule> filters = new List<RegexRule> ( filterModels.Count );
			foreach ( FilterRuleModel model in filterModels )
			{
				if ( model.Expansion is null )
				{
					continue;
				}

				filters.Add ( new RegexRule ( model.Pattern ?? String.Empty , model.Expansion ) );
			}

			return new MumuyDataStore ( modeMap , cache , sort , pair , filters );

			static T ReadFromJson<T> ( Assembly assembly , JsonSerializerOptions options , String resourceName )
			{
				using Stream stream = assembly.GetManifestResourceStream ( resourceName )
					?? throw new InvalidOperationException ( $"Resource '{resourceName}' not found." );

				using StreamReader reader = new StreamReader ( stream );
				String json = reader.ReadToEnd ();
				return JsonSerializer.Deserialize<T> ( json , options )
					?? throw new InvalidOperationException ( $"Failed to deserialize '{resourceName}'." );
			}
		}
		catch
		{
			return CreateEmpty ();
		}
	}

	private static MumuyDataStore CreateEmpty ()
	{
		return new MumuyDataStore (
			new Dictionary<String , String[]> ( StringComparer.Ordinal ),
			new Dictionary<String , String[]> ( StringComparer.Ordinal ),
			new Dictionary<String , String[]> ( StringComparer.Ordinal ),
			new Dictionary<String , String[]> ( StringComparer.Ordinal ),
			[] );
	}

	private sealed class FilterRuleModel
	{
		[System.Text.Json.Serialization.JsonPropertyName("exp")]
		public String? Pattern { get; init; }

		[System.Text.Json.Serialization.JsonPropertyName("str")]
		public String? Expansion { get; init; }
	}
}

internal sealed class RegexRule
{
	private readonly System.Text.RegularExpressions.Regex regex_field;

	public RegexRule ( String pattern , String replacement )
	{
		Pattern = pattern;
		Replacement = replacement;
		regex_field = new System.Text.RegularExpressions.Regex (
			pattern ,
			System.Text.RegularExpressions.RegexOptions.Compiled |
			System.Text.RegularExpressions.RegexOptions.CultureInvariant );
	}

	public String Pattern { get; }
	public String Replacement { get; }

	public String Replace ( String input ) => regex_field.Replace ( input , Replacement );
	public Boolean IsMatch ( String input ) => regex_field.IsMatch ( input );
}
