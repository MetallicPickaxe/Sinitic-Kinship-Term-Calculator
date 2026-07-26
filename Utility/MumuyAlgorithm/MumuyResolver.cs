using System;
using System.Collections.Generic;

using MumuyAlgorithm.Infrastructure;
using MumuyAlgorithm.Processing;

namespace MumuyAlgorithm;

public sealed class MumuyResolver
{
	private readonly MumuyDataStore store_field;
	private readonly MumuySelectorProcessor selector_field;

	public MumuyResolver ()
	{
		store_field = MumuyDataStore.Load ();
		selector_field = new MumuySelectorProcessor ( store_field );
	}

	public IReadOnlyList<String> ResolveSelectors ( String selector , Int32? sex = null )
		=> selector_field.SelectorToIds ( selector , sex );

	public IReadOnlyList<String> ResolveNames ( String selector , Int32? sex = null )
	{
		IReadOnlyList<String> ids = ResolveSelectors ( selector , sex );
		List<String> results = new List<String> ();

		foreach ( String id in ids )
		{
			IReadOnlyList<String> names = MumuyIdUtility.GetItemsById ( id , store_field );
			foreach ( String name in names )
			{
				if ( !String.IsNullOrWhiteSpace ( name ) && !results.Contains ( name , StringComparer.Ordinal ) )
				{
					results.Add ( name );
				}
			}
		}

		return results;
	}
}
