using System;

namespace KinshipCalculator.Core.Models;

public sealed class RelationVector : IEquatable<RelationVector>
{
	public static RelationVector Empty { get; } = new RelationVector (
		generation: 0 ,
		paternalDepth: 0 ,
		maternalDepth: 0 ,
		collateralDepth: 0 ,
		spouseParity: 0 ,
		side: RelationSide.Unknown ,
		gender: PersonGender.Unknown ,
		isAffinal: false );

	public RelationVector (
		Int32 generation ,
		Int32 paternalDepth ,
		Int32 maternalDepth ,
		Int32 collateralDepth ,
		Int32 spouseParity ,
		RelationSide side ,
		PersonGender gender ,
		Boolean isAffinal )
	{
		Generation = generation;
		PaternalDepth = paternalDepth;
		MaternalDepth = maternalDepth;
		CollateralDepth = collateralDepth;
		SpouseParity = spouseParity;
		Side = side;
		Gender = gender;
		IsAffinal = isAffinal;
	}

	public Int32 Generation { get; }
	public Int32 PaternalDepth { get; }
	public Int32 MaternalDepth { get; }
	public Int32 CollateralDepth { get; }
	public Int32 SpouseParity { get; }
	public RelationSide Side { get; }
	public PersonGender Gender { get; }
	public Boolean IsAffinal { get; }

	public Boolean Equals ( RelationVector? other )
	{
		if ( other is null )
		{
			return false;
		}

		return Generation == other.Generation
			&& PaternalDepth == other.PaternalDepth
			&& MaternalDepth == other.MaternalDepth
			&& CollateralDepth == other.CollateralDepth
			&& SpouseParity == other.SpouseParity
			&& Side == other.Side
			&& Gender == other.Gender
			&& IsAffinal == other.IsAffinal;
	}

	public override Boolean Equals ( Object? obj )
		=> obj is RelationVector other && Equals ( other );

	public override Int32 GetHashCode ()
		=> HashCode.Combine (
			Generation ,
			PaternalDepth ,
			MaternalDepth ,
			CollateralDepth ,
			SpouseParity ,
			(Int32)Side ,
			(Int32)Gender ,
			IsAffinal );

	public static Boolean operator == ( RelationVector? left , RelationVector? right )
		=> ReferenceEquals ( left , right ) || left is not null && left.Equals ( right );

	public static Boolean operator != ( RelationVector? left , RelationVector? right )
		=> !( left == right );

	public override String ToString ()
		=> $"Gen={Generation}, Pat={PaternalDepth}, Mat={MaternalDepth}, Collateral={CollateralDepth}, SpouseParity={SpouseParity}, Side={Side}, Gender={Gender}, Affinal={IsAffinal}";
}
