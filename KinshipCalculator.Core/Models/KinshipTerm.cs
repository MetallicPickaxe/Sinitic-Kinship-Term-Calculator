using System;

namespace KinshipCalculator.Core.Models;

public sealed class KinshipTerm
{
	public KinshipTerm ( String key , LocalizedText label )
		: this ( key , label , null )
	{
	}

	public KinshipTerm ( String key , LocalizedText label , LocalizedText? alternateLabel )
	{
		Key = key;
		Label = label;
		AlternateLabel = alternateLabel;
	}

	public String Key { get; }
	public LocalizedText Label { get; }
	public LocalizedText? AlternateLabel { get; }
}