using System;

namespace MumuyAlgorithm.Processing;

internal static class NumberConverter
{
	private static readonly String[] TextAttr = {
		String.Empty ,
		"一" ,
		"二" ,
		"三" ,
		"四" ,
		"五" ,
		"六" ,
		"七" ,
		"八" ,
		"九" ,
		"十"
	};

	public static String ToChineseOrdinal ( String value )
	{
		if ( Int32.TryParse ( value , out Int32 num ) )
		{
			return ToChineseOrdinal ( num );
		}

		return value;
	}

	public static String ToChineseOrdinal ( Int32 num )
	{
		if ( num == 1 )
		{
			return "大";
		}

		if ( num == 99 )
		{
			return "小";
		}

		Int32 dec = num / 10;
		Int32 unit = num % 10;
		String decText = dec > 0 ? $"{TextAttr[ dec ]}十" : String.Empty;
		if ( dec == 1 )
		{
			decText = "十";
		}

		String unitText = unit > 0 ? TextAttr[ unit ] : String.Empty;
		return decText + unitText;
	}
}
