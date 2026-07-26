using System;
using System.Collections.Generic;

using KinshipCalculator.Core.Models;

namespace KinshipCalculator.Core.Services;

public interface IKinshipCalculator
{
	IReadOnlyList<KinshipToken> Tokens { get; }
	KinshipResult Evaluate ( IReadOnlyList<String> tokenIds , String languageKey , PersonGender selfGender );
}
