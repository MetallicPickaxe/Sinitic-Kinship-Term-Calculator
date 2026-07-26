using System;
using System.Collections.Generic;

using KinshipCalculator.Core.Models;

namespace KinshipCalculator.Core.Data;

public static class KinshipData
{
	public static IReadOnlyList<KinshipToken> Tokens { get; } = [
		new("father", "F", new LocalizedText("父", "父", "Father"), "parents"),
		new("mother", "M", new LocalizedText("母", "母", "Mother"), "parents"),
		new("adoptive-father", "AF", new LocalizedText("养父", "養父", "Adoptive Father"), "parents", origin: "adoptive"),
		new("adoptive-mother", "AM", new LocalizedText("养母", "養母", "Adoptive Mother"), "parents", origin: "adoptive"),
		new("older-brother", "OB", new LocalizedText("兄", "兄", "Older Brother"), "siblings"),
		new("younger-brother", "YB", new LocalizedText("弟", "弟", "Younger Brother"), "siblings"),
		new("older-sister", "OS", new LocalizedText("姐", "姐", "Older Sister"), "siblings"),
		new("younger-sister", "YS", new LocalizedText("妹", "妹", "Younger Sister"), "siblings"),
		new("son", "S", new LocalizedText("子", "子", "Son"), "children"),
		new("daughter", "D", new LocalizedText("女", "女", "Daughter"), "children"),
		new("adoptive-son", "AS", new LocalizedText("养子", "養子", "Adoptive Son"), "children", origin: "adoptive"),
		new("adoptive-daughter", "AD", new LocalizedText("养女", "養女", "Adoptive Daughter"), "children", origin: "adoptive"),
		new("spouse", "SP", new LocalizedText("配偶", "配偶", "Spouse"), "spouse")
	];
	public static IReadOnlyDictionary<String , KinshipTerm> Terms { get; } = new Dictionary<String , KinshipTerm>
	{
		[ "" ] = new ( "self" , new LocalizedText ( "自己" , "自己" , "Self" ) ) ,
		[ "F" ] = new ( "father" , new LocalizedText ( "父亲" , "父親" , "Father" ) ) ,
		[ "M" ] = new ( "mother" , new LocalizedText ( "母亲" , "母親" , "Mother" ) ) ,
		[ "S" ] = new ( "son" , new LocalizedText ( "儿子" , "兒子" , "Son" ) ) ,
		[ "D" ] = new ( "daughter" , new LocalizedText ( "女儿" , "女兒" , "Daughter" ) ) ,
		[ "OB" ] = new ( "olderBrother" , new LocalizedText ( "哥哥" , "哥哥" , "Older Brother" ) ) ,
		[ "YB" ] = new ( "youngerBrother" , new LocalizedText ( "弟弟" , "弟弟" , "Younger Brother" ) ) ,
		[ "OS" ] = new ( "olderSister" , new LocalizedText ( "姐姐" , "姐姐" , "Older Sister" ) ) ,
		[ "YS" ] = new ( "youngerSister" , new LocalizedText ( "妹妹" , "妹妹" , "Younger Sister" ) ) ,
		        		[ "SP" ] = new ( "spouse" , new LocalizedText ( "配偶" , "配偶" , "Spouse" ) ) 
		        	};
		        }
