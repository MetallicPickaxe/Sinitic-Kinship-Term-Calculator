using System;

namespace KinshipCalculator.Core.Models;

public sealed class KinshipToken
{
	public KinshipToken ( String id , String symbol , LocalizedText label , String category = "default" , String? origin = null )
	{
		Id = id;
		Symbol = symbol;
		Label = label;
		Category = category;
		Origin = origin ?? String.Empty;
	}

	public String Id { get; }
	public String Symbol { get; }
	public LocalizedText Label { get; }
	public String Category { get; }
	public String Origin { get; }
}
