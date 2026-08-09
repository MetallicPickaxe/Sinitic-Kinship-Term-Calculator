using KinshipCalculator.Core.Models;

namespace KinshipCalculator.WinUI.Options;

public sealed class ApplicationOptions
{
	// Languages / LanguageOption were here. Round-2 contract R5/U8 withdrew the choice: terms are
	// always Traditional and chrome is always English, so there is nothing for configuration to
	// select between. Removed rather than left as an ignored setting — a knob that no longer turns
	// anything is worse than no knob, because it reads as though it does.
	public string StartingRole { get; set; } = "self";
	public PersonGender DefaultGender { get; set; } = PersonGender.Male;
}
