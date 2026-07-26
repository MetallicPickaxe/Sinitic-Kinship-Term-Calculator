using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using MumuyAlgorithm.Infrastructure;

namespace MumuyAlgorithm.Processing;

internal sealed class MumuySelectorProcessor
{
	private static readonly Regex SameSexPattern = new ( """
,[mwd0](?:&[ol\d]+)?,w|,[hfs1](?:&[ol\d]+)?,h
""" , RegexOptions.Compiled | RegexOptions.CultureInvariant );

	private static readonly Regex LeadingWifePattern = new ( """^,[w1]""" , RegexOptions.Compiled | RegexOptions.CultureInvariant );
	private static readonly Regex LeadingHusbandPattern = new ( """^,[h0]""" , RegexOptions.Compiled | RegexOptions.CultureInvariant );

	private readonly MumuyDataStore store_field;

	public MumuySelectorProcessor ( MumuyDataStore store )
	{
		store_field = store;
	}

	public IReadOnlyList<String> SelectorToIds ( String selector , Int32? sex = null )
	{
		if ( String.IsNullOrWhiteSpace ( selector ) )
		{
			return [];
		}

		String working = selector.StartsWith ( ',' ) ? selector : $",{selector}";
		Int32 workingSex = sex ?? -1;

		if ( workingSex >= 0 )
		{
			if ( workingSex == 1 && LeadingHusbandPattern.IsMatch ( working ) )
			{
				return [];
			}

			if ( workingSex == 0 && LeadingWifePattern.IsMatch ( working ) )
			{
				return [];
			}

			if ( !working.Contains ( ",1" , StringComparison.Ordinal ) &&
				 !working.Contains ( ",0" , StringComparison.Ordinal ) )
			{
				working = $",{workingSex}{working}";
			}
		}
		else
		{
			if ( LeadingWifePattern.IsMatch ( working ) )
			{
				workingSex = 1;
			}
			else if ( LeadingHusbandPattern.IsMatch ( working ) )
			{
				workingSex = 0;
			}
		}

		if ( SameSexPattern.IsMatch ( working ) )
		{
			return [];
		}

		IReadOnlyList<String> expanded = ExpandSelector ( working );
		IEnumerable<String> ids = expanded.Select ( id => Regex.Replace ( id , ",[01]" , String.Empty , RegexOptions.CultureInvariant ).TrimStart ( ',' ) );

		return MumuyIdUtility.FilterIds ( ids );
	}

	private IReadOnlyList<String> ExpandSelector ( String selector )
	{
		List<String> results = new List<String> ();
		HashSet<String> visited = new HashSet<String> ( StringComparer.Ordinal );

		void Visit ( String value )
		{
			if ( !visited.Add ( value ) )
			{
				return;
			}

			String current = value;
			while ( true )
			{
				String snapshot = current;
				foreach ( RegexRule rule in store_field.Filters )
				{
					current = rule.Replace ( current );
					if ( current.Contains ( '#' , StringComparison.Ordinal ) )
					{
						String[] parts = current.Split ( '#' , StringSplitOptions.RemoveEmptyEntries );
						foreach ( String part in parts )
						{
							Visit ( part );
						}

						return;
					}
				}

				if ( ReferenceEquals ( snapshot , current ) || snapshot == current )
				{
					break;
				}
			}

			if ( SameSexPattern.IsMatch ( current ) )
			{
				return;
			}

			results.Add ( current );
		}

		Visit ( selector );

		return results;
	}
}
