using System;
using System.Collections.Generic;
using System.Linq;

namespace KinshipCalculator.Core.Models;

public sealed class KinshipResult
{
	public KinshipResult (
		IReadOnlyList<KinshipResolutionOption> options ,
		LocalizedText originalPath ,
		LocalizedText? rawChain = null )
	{
		Options = options.Count > 0 ? options : throw new ArgumentException ( "Options cannot be empty." );
		PathDisplay = originalPath;
		Term = Options.First ().Label;
		IsExactMatch = Options.First ().IsExactMatch;
		RawChain = rawChain ?? LocalizedText.Empty;
	}

	public LocalizedText Term { get; }
	public Boolean IsExactMatch { get; }
	public LocalizedText PathDisplay { get; }
	public IReadOnlyList<KinshipResolutionOption> Options { get; }

	/// <summary>
	/// K15 layer ④ — raw calibration readback: the chain EXACTLY as entered, possessor-led
	/// and never simplified (我的母親的哥哥的母親, even though the engine collapses that to
	/// 外祖母). Its job is to let the reader check that the machine understood the input;
	/// every other layer shows what the machine did WITH it.
	/// </summary>
	public LocalizedText RawChain { get; }
}
