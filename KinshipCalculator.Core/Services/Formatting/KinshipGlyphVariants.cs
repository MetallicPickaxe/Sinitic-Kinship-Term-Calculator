using System;
using System.Collections.Generic;
using System.Text;

namespace KinshipCalculator.Core.Services.Formatting;

/// <summary>
/// Settled variant-glyph pairs: two spellings of ONE kinship word, both current, neither a
/// different relation.
///
/// This is NOT the same thing as <see cref="KinshipScriptConverter"/>. That table answers "what
/// is this character in the other script", and its answer is a replacement — the source spelling
/// is gone. A variant glyph is different: 侄 and 姪 are both written by Traditional readers, so a
/// Traditional display that silently rewrites 侄 to 姪 removes a spelling the reader may well
/// have been looking for. The converter is right for 孙/孫; it is too strong here.
///
/// The distinction the product must keep (acceptance contract, 2026-08-02):
///   • other NAMES     — 爸爸 for 父親: a different word, from a dialect or register layer
///   • variant GLYPH   — 侄子 for 姪子: the same word, written the other way   ← this file
///   • possible RELATION — a different person entirely
/// Collapsing any two of those into one list is what made the feature unreadable.
///
/// The 女 radical in 姪 is NOT a modern gender marker. 姪子 is male and 姪女 is female; the
/// gender is carried by the 子 / 女 that follows. Any UI copy implying otherwise is wrong.
///
/// Kept deliberately small and reusable: reverse lookup (deferred) will need exactly this
/// normalisation so that a user typing either spelling reaches the same candidates.
/// </summary>
public static class KinshipGlyphVariants
{
	/// <summary>
	/// (Traditional-leaning form, other current form). Both are correct; the pair only records
	/// that a reader may write either. Extend ONLY with pairs that are genuinely one word in two
	/// spellings — a pair that separates two relations does not belong here.
	/// </summary>
	private static readonly (Char Primary, Char Alternate)[] Pairs =
	{
		( '姪' , '侄' )
	};

	/// <summary>
	/// The same term written with the other glyph, or null when the term contains no settled
	/// variant character (the overwhelmingly common case) or when the rewrite would just repeat
	/// the input.
	/// </summary>
	public static String? TryGetAlternateSpelling ( String term )
	{
		if ( String.IsNullOrEmpty ( term ) )
		{
			return null;
		}

		StringBuilder builder = new ( term.Length );
		Boolean changed = false;
		foreach ( Char c in term )
		{
			Char mapped = c;
			foreach ( (Char primary, Char alternate) in Pairs )
			{
				if ( c == primary )
				{
					mapped = alternate;
					changed = true;
					break;
				}

				if ( c == alternate )
				{
					mapped = primary;
					changed = true;
					break;
				}
			}

			builder.Append ( mapped );
		}

		if ( !changed )
		{
			return null;
		}

		String result = builder.ToString ();
		return String.Equals ( result , term , StringComparison.Ordinal ) ? null : result;
	}

	/// <summary>
	/// Canonical spelling for comparison and lookup: every settled variant character folded to
	/// its primary form. Two spellings of one word normalise to the same string, which is what
	/// lets either one find the same candidates.
	/// </summary>
	public static String Normalize ( String term )
	{
		if ( String.IsNullOrEmpty ( term ) )
		{
			return term;
		}

		StringBuilder builder = new ( term.Length );
		foreach ( Char c in term )
		{
			Char mapped = c;
			foreach ( (Char primary, Char alternate) in Pairs )
			{
				if ( c == alternate )
				{
					mapped = primary;
					break;
				}
			}

			builder.Append ( mapped );
		}

		return builder.ToString ();
	}

	/// <summary>Do these two spellings denote the same word?</summary>
	public static Boolean AreSameWord ( String left , String right )
		=> String.Equals ( Normalize ( left ) , Normalize ( right ) , StringComparison.Ordinal );

	/// <summary>Every settled variant character, for gates that need to know the closed set.</summary>
	public static IReadOnlyList<(Char Primary, Char Alternate)> SettledPairs => Pairs;
}
