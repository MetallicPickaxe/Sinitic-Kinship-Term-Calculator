using System;
using System.Collections.Generic;
using System.Linq;
using KinshipCalculator.Core.Models;
using KinshipCalculator.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test_Unit;

[TestClass]
public class KinshipSequenceBuilderTests
{
    private static KinshipToken CreateToken(string id, string category, string origin = "")
    {
        return new KinshipToken(id, id, new LocalizedText(id, id, id), category, origin);
    }

    [TestMethod]
    public void TranslateToken_WithOrigin_ReturnsTokenId()
    {
        var token = CreateToken("father", "parents", "biological");
        var result = KinshipSequenceBuilder.TranslateToken(token, KinshipOrigin.Step);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("father", result[0]);
    }

    [TestMethod]
    public void TranslateToken_Biological_ReturnsTokenId()
    {
        var token = CreateToken("father", "parents");
        var result = KinshipSequenceBuilder.TranslateToken(token, KinshipOrigin.Biological);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("father", result[0]);
    }

    [TestMethod]
    public void TranslateToken_Adoptive_Father_ReturnsAdoptiveFather()
    {
        var token = CreateToken("father", "parents");
        var result = KinshipSequenceBuilder.TranslateToken(token, KinshipOrigin.Adoptive);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("adoptive-father", result[0]);
    }

    [TestMethod]
    public void TranslateToken_Adoptive_Mother_ReturnsAdoptiveMother()
    {
        var token = CreateToken("mother", "parents");
        var result = KinshipSequenceBuilder.TranslateToken(token, KinshipOrigin.Adoptive);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("adoptive-mother", result[0]);
    }

    [TestMethod]
    public void TranslateToken_Step_Father_ReturnsMotherSpouse()
    {
        var token = CreateToken("father", "parents");
        var result = KinshipSequenceBuilder.TranslateToken(token, KinshipOrigin.Step);
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("mother", result[0]);
        Assert.AreEqual("spouse", result[1]);
    }

    [TestMethod]
    public void TranslateToken_Step_Mother_ReturnsFatherSpouse()
    {
        var token = CreateToken("mother", "parents");
        var result = KinshipSequenceBuilder.TranslateToken(token, KinshipOrigin.Step);
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("father", result[0]);
        Assert.AreEqual("spouse", result[1]);
    }

    [TestMethod]
    public void TranslateToken_Step_Son_ReturnsSpouseSon()
    {
        var token = CreateToken("son", "children");
        var result = KinshipSequenceBuilder.TranslateToken(token, KinshipOrigin.Step);
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("spouse", result[0]);
        Assert.AreEqual("son", result[1]);
    }

    [TestMethod]
    public void TranslateToken_Step_Daughter_ReturnsSpouseDaughter()
    {
        var token = CreateToken("daughter", "children");
        var result = KinshipSequenceBuilder.TranslateToken(token, KinshipOrigin.Step);
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("spouse", result[0]);
        Assert.AreEqual("daughter", result[1]);
    }

    [TestMethod]
    public void TranslateToken_UnknownCategory_ReturnsTokenId()
    {
        var token = CreateToken("unknown", "misc");
        var result = KinshipSequenceBuilder.TranslateToken(token, KinshipOrigin.Step);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("unknown", result[0]);
    }
}
