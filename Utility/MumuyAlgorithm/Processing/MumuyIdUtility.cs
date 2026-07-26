using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using MumuyAlgorithm.Infrastructure;

namespace MumuyAlgorithm.Processing;

internal static class MumuyIdUtility
{
	private static readonly Regex SiblingAgeRegex = new ( """[ol](?=[sb])""" , RegexOptions.Compiled | RegexOptions.CultureInvariant );
	private static readonly Regex AgeMarkerRegex = new ( """&[ol]""" , RegexOptions.Compiled | RegexOptions.CultureInvariant );
	private static readonly Regex SortSuffixRegex = new ( """&([\d]+)(,[hw])?$""" , RegexOptions.Compiled | RegexOptions.CultureInvariant );
	private static readonly Regex SortCleanupRegex = new ( """&\d+""" , RegexOptions.Compiled | RegexOptions.CultureInvariant );
	private static readonly Regex RemoveAgeRegex = new ( """&[ol]""" , RegexOptions.Compiled | RegexOptions.CultureInvariant );
	private static readonly Regex NeutralSiblingRegex = new ( """[ol]([bs])""" , RegexOptions.Compiled | RegexOptions.CultureInvariant );
	private static readonly Regex LeadingBigSmallRegex = new ( """^[大小]""" , RegexOptions.Compiled | RegexOptions.CultureInvariant );
	private static readonly Regex SpouseRegex = new ( """[hw],""" , RegexOptions.Compiled | RegexOptions.CultureInvariant );

	public static IReadOnlyList<String> FilterIds ( IEnumerable<String> ids )
	{
		List<String> source = ids.Where ( static id => !String.IsNullOrWhiteSpace ( id ) ).ToList ();
		if ( source.Count == 0 )
		{
			return [];
		}

		HashSet<String> sameList = new HashSet<String> ( source.Where ( id => Simplify ( id ) == id ) , StringComparer.Ordinal );
		List<String> result = new List<String> ();

		foreach ( String id in source )
		{
			String simplified = Simplify ( id );
			if ( sameList.Contains ( id ) || ( id != simplified && !sameList.Contains ( simplified ) ) )
			{
				if ( !result.Contains ( id , StringComparer.Ordinal ) )
				{
					result.Add ( id );
				}
			}
		}

		return result;
	}

	public static IReadOnlyList<String> GetItemsById ( String id , MumuyDataStore store )
	{
		List<String> results = [];
		String working = id;

		Match sortMatch = SortSuffixRegex.Match ( working );
		if ( sortMatch.Success )
		{
			String number = sortMatch.Groups[ 1 ].Value;
			String zh = NumberConverter.ToChineseOrdinal ( number );
			working = SortCleanupRegex.Replace ( working , String.Empty );

			if ( TryResolveSort ( working , zh , store , results ) )
			{
				return results;
			}
		}

		working = SortCleanupRegex.Replace ( id , String.Empty );
		if ( TryAppendData ( working , store , results ) )
		{
			return results;
		}

		String withoutAge = RemoveAgeRegex.Replace ( working , String.Empty );
		if ( TryAppendData ( withoutAge , store , results ) )
		{
			return results;
		}

		String neutral = NeutralSiblingRegex.Replace ( working , "x$1" );
		if ( TryAppendData ( neutral , store , results ) )
		{
			return results;
		}

		String lId = working.Replace ( 'x' , 'l' );
		String oId = working.Replace ( 'x' , 'o' );
		if ( TryAppendData ( oId , store , results ) )
		{
			return results;
		}

		if ( TryAppendData ( lId , store , results ) )
		{
			return results;
		}

		return results;
	}

	private static Boolean TryResolveSort ( String id , String number ,
		MumuyDataStore store , List<String> results )
	{
		if ( store.Sort.TryGetValue ( id , out String[]? sortNames ) && sortNames.Length > 0 )
		{
			results.Add ( sortNames[ 0 ].Replace ( "几" , number , StringComparison.Ordinal ) );
			return true;
		}

		if ( store.ModeMap.TryGetValue ( id , out String[]? names ) && names.Length > 0 )
		{
			Int32 gen = GetGenerationById ( id );
			if ( gen < 3 && !SpouseRegex.IsMatch ( id ) )
			{
				String? special = names.FirstOrDefault ( static n => n.Contains ( "几" , StringComparison.Ordinal ) );
				if ( !String.IsNullOrEmpty ( special ) )
				{
					results.Add ( special.Replace ( "几" , number , StringComparison.Ordinal ) );
				}
				else
				{
					String fallback = names[ 0 ];
					if ( LeadingBigSmallRegex.IsMatch ( fallback ) )
					{
						fallback = LeadingBigSmallRegex.Replace ( fallback , number , 1 );
					}
					else
					{
						fallback = number + fallback;
					}

					results.Add ( fallback );
				}
			}
			else
			{
				results.Add ( names[ 0 ] );
			}

			return true;
		}

		return false;
	}

	private static Boolean TryAppendData ( String key , MumuyDataStore store , List<String> results )
	{
		IReadOnlyList<String> data = GetData ( key , store );
		if ( data.Count == 0 )
		{
			return false;
		}

		foreach ( String item in data )
		{
			if ( !String.IsNullOrWhiteSpace ( item ) && !results.Contains ( item , StringComparer.Ordinal ) )
			{
				results.Add ( item );
			}
		}

		return results.Count > 0;
	}

	private static IReadOnlyList<String> GetData ( String key , MumuyDataStore store )
	{
		List<String> ids = [];
		String keyWithElder = Regex.Replace ( key , "(,[sd])(,[wh])?$" , "$1&o$2" , RegexOptions.CultureInvariant );
		String keyWithYounger = Regex.Replace ( key , "(,[sd])(,[wh])?$" , "$1&l$2" , RegexOptions.CultureInvariant );

		if ( store.ModeMap.ContainsKey ( keyWithElder ) && store.ModeMap.ContainsKey ( keyWithYounger ) )
		{
			ids.AddRange ( FilterIds ( new[] { keyWithElder , keyWithYounger } ) );
		}
		else if ( store.ModeMap.ContainsKey ( key ) )
		{
			ids.Add ( key );
		}

		List<String> list = [];
		foreach ( String id in ids )
		{
			if ( store.ModeMap.TryGetValue ( id , out String[]? names ) && names.Length > 0 )
			{
				list.Add ( names[ 0 ] );
			}
		}

		return list;
	}

	public static Int32 GetGenerationById ( String id )
	{
		ReadOnlySpan<char> remaining = id.AsSpan ();
		Int32 generation = 0;

		while ( !remaining.IsEmpty )
		{
			Int32 commaIndex = remaining.IndexOf ( ',' );
			ReadOnlySpan<char> segment = commaIndex >= 0 ? remaining[..commaIndex] : remaining;
			segment = segment.Trim ();

			if ( !segment.IsEmpty )
			{
				Span<char> buffer = stackalloc char[ segment.Length ];
				Int32 bufferLength = 0;

				for ( Int32 i = 0 ; i < segment.Length ; i++ )
				{
					if ( segment[ i ] == '&' )
					{
						i++;
						while ( i < segment.Length && ( segment[ i ] == 'o' || segment[ i ] == 'l' || Char.IsDigit ( segment[ i ] ) ) )
						{
							i++;
						}
						i--;
						continue;
					}

					buffer[ bufferLength++ ] = segment[ i ];
				}

				ReadOnlySpan<char> cleaned = buffer[..bufferLength];
				if ( TryResolveGenerationToken ( cleaned , out Int32 value ) )
				{
					generation += value;
				}
			}

			if ( commaIndex < 0 )
			{
				break;
			}

			remaining = remaining[(commaIndex + 1)..];
		}

		return generation;
	}

	private static Boolean TryResolveGenerationToken ( ReadOnlySpan<char> token , out Int32 value )
	{
		if ( token.SequenceEqual ( "f" ) || token.SequenceEqual ( "m" ) )
		{
			value = 1;
			return true;
		}

		if ( token.SequenceEqual ( "s" ) || token.SequenceEqual ( "d" ) )
		{
			value = -1;
			return true;
		}

		value = 0;
		return false;
	}

	private static String Simplify ( String value )
	{
		String result = SiblingAgeRegex.Replace ( value , "x" );
		result = AgeMarkerRegex.Replace ( result , String.Empty );
		return result;
	}

	private static Boolean Contains ( this List<String> source , String value , StringComparer comparer )
	{
		foreach ( String item in source )
		{
			if ( comparer.Equals ( item , value ) )
			{
				return true;
			}
		}

		return false;
	}
}
