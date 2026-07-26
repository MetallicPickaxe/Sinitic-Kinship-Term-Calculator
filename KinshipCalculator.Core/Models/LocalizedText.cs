using System;

namespace KinshipCalculator.Core.Models;

public sealed record LocalizedText ( String ZhHans , String ZhHant , String English )
{
	public static LocalizedText Empty { get; } = new LocalizedText ( String.Empty , String.Empty , String.Empty );

    public String ForLanguage ( String language ) => language switch
	{
		"zh-Hant" => ZhHant,
		"en" => English,
		_ => ZhHans
	};
}
