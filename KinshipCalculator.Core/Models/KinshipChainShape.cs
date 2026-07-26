using System;
using System.Collections.Generic;

using KinshipCalculator.Core.Models.Semantics;

namespace KinshipCalculator.Core.Models;

/// <summary>
/// Sibling pivot at the top of the ascent segment of a canonical chain.
/// </summary>
public sealed record KinshipBranchInfo ( SiblingOrder Order , PersonGender Gender );

/// <summary>
/// Lossless canonical decomposition of a kinship chain:
/// <c>SP? (F|M)* (OB|YB|OS|YS)? (S|D)* SP?</c>.
/// Unlike <see cref="RelationVector"/> (a lossy 8-feature projection), this shape preserves
/// the position of every female link, the fork height of a collateral branch, and the
/// spouse rooting of the chain — the three pieces of information whose loss caused the
/// 外-collision, 堂/從/族-flattening, and spouse-side-drop defect families.
/// </summary>
public sealed class KinshipChainShape
{
	public KinshipChainShape (
		IReadOnlyList<PersonGender> ascentGenders ,
		KinshipBranchInfo? branch ,
		IReadOnlyList<PersonGender> descentGenders ,
		Boolean leadingSpouse ,
		Boolean trailingSpouse ,
		PersonGender egoGender ,
		Boolean adoptiveAscent = false )
	{
		AscentGenders = ascentGenders;
		Branch = branch;
		DescentGenders = descentGenders;
		LeadingSpouse = leadingSpouse;
		TrailingSpouse = trailingSpouse;
		EgoGender = egoGender;
		AdoptiveAscent = adoptiveAscent;
	}

	/// <summary>Gender of each parent hop, in order from ego upward. Preserves 外-insertion positions.</summary>
	public IReadOnlyList<PersonGender> AscentGenders { get; }

	/// <summary>Sibling pivot at the top of the ascent, if any. Fork height = <see cref="AscentDepth"/>.</summary>
	public KinshipBranchInfo? Branch { get; }

	/// <summary>Gender of each child hop, in order downward. Preserves first-crossing position.</summary>
	public IReadOnlyList<PersonGender> DescentGenders { get; }

	/// <summary>Chain starts at ego's spouse (spouse-side rooting; drives 岳-forms / 隨夫稱).</summary>
	public Boolean LeadingSpouse { get; }

	/// <summary>Chain ends on the relative's spouse (drives 婿/媳/岳母-style closures).</summary>
	public Boolean TrailingSpouse { get; }

	public PersonGender EgoGender { get; }

	/// <summary>At least one ascent hop is an adoptive link (drives the 養-prefix).</summary>
	public Boolean AdoptiveAscent { get; }

	public Int32 AscentDepth => AscentGenders.Count;

	public Int32 DescentDepth => DescentGenders.Count;

	public Boolean HasBranch => Branch is not null;

	/// <summary>Generation of the named relative relative to ego (positive = older).</summary>
	public Int32 Generation => AscentDepth - DescentDepth;

	public Boolean IsPureAncestor => AscentDepth > 0 && !HasBranch && DescentDepth == 0 && !LeadingSpouse;

	public Boolean IsPureDescendant => AscentDepth == 0 && !HasBranch && DescentDepth > 0 && !LeadingSpouse;

	/// <summary>Gender of the person the chain lands on before any trailing-spouse hop.</summary>
	public PersonGender SubjectGender
		=> DescentDepth > 0
			? DescentGenders [ ^1 ]
			: Branch is not null
				? Branch.Gender
				: AscentDepth > 0
					? AscentGenders [ ^1 ]
					: EgoGender;

	/// <summary>Gender of the named relative (trailing spouse flips the subject's gender).</summary>
	public PersonGender RelativeGender
		=> TrailingSpouse ? Flip ( SubjectGender ) : SubjectGender;

	private static PersonGender Flip ( PersonGender value ) => value switch
	{
		PersonGender.Male => PersonGender.Female ,
		PersonGender.Female => PersonGender.Male ,
		_ => PersonGender.Unknown
	};
}
