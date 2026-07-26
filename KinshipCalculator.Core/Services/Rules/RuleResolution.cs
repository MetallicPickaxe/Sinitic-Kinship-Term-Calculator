using System;
using KinshipCalculator.Core.Models;

namespace KinshipCalculator.Core.Services.Rules;

/// <summary>
/// A rule's answer for a chain. <see cref="IsExactMatch"/> means the engine produced a
/// NAMED kinship term (as opposed to a descriptive <c>的</c>-chain reading) — it is NOT a
/// claim that the name is attested or independently verified; that calibration lives in
/// the comparison faces and the metamorphic invariants. The flag is deliberately
/// non-defaulted so every construction site states its claim consciously (a silent
/// <c>true</c> default is how wrong-person readings once rode the exact ranking).
/// </summary>
public sealed record RuleResolution (
	LocalizedText Label ,
	LocalizedText? AlternateLabel ,
	LocalizedText? OfficialDescription ,
	Boolean IsExactMatch
)
{
	public static RuleResolution Empty => new ( LocalizedText.Empty , null , null , false );
}
