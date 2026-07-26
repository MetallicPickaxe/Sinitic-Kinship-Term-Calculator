using System;

namespace KinshipCalculator.Core.Models;

public sealed class KinshipResolutionOption
{
	public KinshipResolutionOption (
		LocalizedText label ,
		Boolean isExact ,
		LocalizedText simplifiedPath ,
		LocalizedText officialDescription ,
		String explanation ,
		String detailsKey ,
		RelationVector vector ,
		LocalizedText? alternateLabel = null ,
		LocalizedText? descriptiveChain = null
	)
	{
		Label = label;
		IsExactMatch = isExact;
		SimplifiedPath = simplifiedPath;
		OfficialDescription = officialDescription;
		Explanation = explanation;
		DetailsKey = detailsKey;
		Vector = vector;
		AlternateLabel = alternateLabel ?? LocalizedText.Empty;
		HasAlternateLabel = !ReferenceEquals ( AlternateLabel , LocalizedText.Empty );
		DescriptiveChain = descriptiveChain ?? LocalizedText.Empty;
	}

	public LocalizedText Label { get; }
	public Boolean IsExactMatch { get; }
	public LocalizedText SimplifiedPath { get; }
	public LocalizedText OfficialDescription { get; }
	public String Explanation { get; }
	public String DetailsKey { get; }
	public RelationVector Vector { get; }
	public LocalizedText AlternateLabel { get; }
	public Boolean HasAlternateLabel { get; }

	/// <summary>
	/// K15 layer ③ — the legal-document chain (父的父的兄): the simplified relation path
	/// spelled out with 的, never contracted into a kinship word. Always populated, even
	/// when a proper term exists, so the reading is available beside the name rather than
	/// only as a fallback for relations we cannot name.
	/// </summary>
	public LocalizedText DescriptiveChain { get; }
}
