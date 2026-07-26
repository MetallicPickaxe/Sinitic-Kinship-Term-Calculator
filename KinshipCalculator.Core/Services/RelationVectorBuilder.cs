using System;
using System.Collections.Generic;

using KinshipCalculator.Core.Models;

namespace KinshipCalculator.Core.Services;

public static class RelationVectorBuilder
{
	public static RelationVector Build ( IReadOnlyList<KinshipToken> tokens , PersonGender selfGender = PersonGender.Unknown )
	{
		if ( tokens.Count == 0 )
		{
			return RelationVector.Empty;
		}

		Int32 generation = 0;
		Int32 paternalDepth = 0;
		Int32 maternalDepth = 0;
		Int32 collateralDepth = 0;
		Int32 spouseParity = 0;
		Boolean isAffinal = false;
		RelationSide side = RelationSide.Unknown;
		RelationSide consanguineSide = RelationSide.Unknown;
		RelationSide preAffinalSide = RelationSide.Unknown;
		PersonGender gender = selfGender;
		Int32 ancestorDepth = 0;
		Int32 siblingRun = 0;

		foreach ( KinshipToken token in tokens )
		{
			switch ( token.Id )
			{
				case "father":
				case "adoptive-father":
				{
					Boolean fromBase = ancestorDepth == 0;
					generation++;
					paternalDepth++;
					ancestorDepth++;
					siblingRun = 0;
					if ( !isAffinal && fromBase )
					{
						consanguineSide = CombineSide ( consanguineSide , RelationSide.Paternal );
						side = consanguineSide;
					}
					gender = PersonGender.Male;
					break;
				}
				case "mother":
				case "adoptive-mother":
				{
					Boolean fromBase = ancestorDepth == 0;
					generation++;
					maternalDepth++;
					ancestorDepth++;
					siblingRun = 0;
					if ( !isAffinal && fromBase )
					{
						consanguineSide = CombineSide ( consanguineSide , RelationSide.Maternal );
						side = consanguineSide;
					}
					gender = PersonGender.Female;
					break;
				}
				case "son":
				case "adoptive-son":
				{
					generation--;
					if ( ancestorDepth > 0 )
					{
						ancestorDepth--;
					}
					siblingRun = 0;
					gender = PersonGender.Male;
					break;
				}
				case "daughter":
				case "adoptive-daughter":
				{
					generation--;
					if ( ancestorDepth > 0 )
					{
						ancestorDepth--;
					}
					siblingRun = 0;
					gender = PersonGender.Female;
					break;
				}
				case "older-brother":
				case "younger-brother":
				case "older-sister":
				case "younger-sister":
				{
					siblingRun++;
					collateralDepth = Math.Max ( collateralDepth , siblingRun );
					gender = token.Id is "older-brother" or "younger-brother"
						? PersonGender.Male
						: PersonGender.Female;
					if ( !isAffinal && consanguineSide == RelationSide.Unknown )
					{
						consanguineSide = RelationSide.Both;
						side = RelationSide.Both;
					}
					break;
				}
				case "spouse":
				{
					spouseParity++;
					siblingRun = 0;
					isAffinal = !isAffinal;
					gender = gender switch
					{
						PersonGender.Male => PersonGender.Female ,
						PersonGender.Female => PersonGender.Male ,
						_ => PersonGender.Unknown
					};

					if ( isAffinal )
					{
						preAffinalSide = consanguineSide != RelationSide.Unknown ? consanguineSide : side;
						side = RelationSide.Affinal;
					}
					else
					{
						side = consanguineSide != RelationSide.Unknown ? consanguineSide : preAffinalSide;
					}

					break;
				}
				default:
					siblingRun = 0;
					break;
			}
		}

		if ( !isAffinal && consanguineSide != RelationSide.Unknown )
		{
			side = consanguineSide;
		}

		return new RelationVector (
			generation ,
			paternalDepth ,
			maternalDepth ,
			collateralDepth ,
			spouseParity ,
			side ,
			gender ,
			isAffinal );
	}

	private static RelationSide CombineSide ( RelationSide current , RelationSide next )
		=> next switch
		{
			RelationSide.Unknown => current ,
			RelationSide.Affinal => RelationSide.Affinal ,
			_ => current switch
			{
				RelationSide.Unknown => next ,
				RelationSide.Affinal => RelationSide.Affinal ,
				RelationSide.Both => RelationSide.Both ,
				_ when current == next => current ,
				_ => RelationSide.Both
			}
		};
}
