using System;
using System.Collections.Generic;
using System.Linq;

using KinshipCalculator.Core.Data;
using KinshipCalculator.Core.Models;
using KinshipCalculator.Core.Services;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test_Unit;

[TestClass]
public sealed class RelationVectorBuilderTests
{
	private static readonly IReadOnlyDictionary<String , KinshipToken> TokenLookup = KinshipData
		.Tokens
		.ToDictionary ( token => token.Id , token => token , StringComparer.Ordinal );

	[TestMethod]
	public void PaternalAncestorMaintainsSide ()
	{
		IReadOnlyList<KinshipToken> tokens = BuildTokens ( "father" , "father" , "mother" );
		RelationVector vector = RelationVectorBuilder.Build ( tokens , PersonGender.Male );

		// Three ancestor steps (F,F,M) sit three generations up; the legacy lossy
		// projection undercounted the maternal tail as 2.
		Assert.AreEqual ( 3 , vector.Generation );
		Assert.AreEqual ( 2 , vector.PaternalDepth );
		Assert.AreEqual ( 1 , vector.MaternalDepth );
		Assert.AreEqual ( RelationSide.Paternal , vector.Side );
		Assert.IsFalse ( vector.IsAffinal );
	}

	[TestMethod]
	public void MaternalUncleRegistersCollateralDepth ()
	{
		IReadOnlyList<KinshipToken> tokens = BuildTokens ( "mother" , "older-brother" );
		RelationVector vector = RelationVectorBuilder.Build ( tokens , PersonGender.Female );

		Assert.AreEqual ( 1 , vector.Generation );
		Assert.AreEqual ( 1 , vector.MaternalDepth );
		Assert.AreEqual ( 1 , vector.CollateralDepth );
		Assert.AreEqual ( RelationSide.Maternal , vector.Side );
		Assert.AreEqual ( PersonGender.Male , vector.Gender );
	}

	[TestMethod]
	public void SpouseBranchMarkedAffinal ()
	{
		IReadOnlyList<KinshipToken> tokens = BuildTokens ( "spouse" , "father" );
		RelationVector vector = RelationVectorBuilder.Build ( tokens , PersonGender.Male );

		Assert.AreEqual ( 1 , vector.Generation );
		Assert.AreEqual ( 1 , vector.PaternalDepth );
		Assert.AreEqual ( RelationSide.Affinal , vector.Side );
		Assert.IsTrue ( vector.IsAffinal );
		Assert.AreEqual ( PersonGender.Male , vector.Gender );
	}

	private static IReadOnlyList<KinshipToken> BuildTokens ( params String[] ids )
		=> ids.Select ( id => TokenLookup [ id ] ).ToList ();
}
