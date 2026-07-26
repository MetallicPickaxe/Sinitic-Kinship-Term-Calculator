using System.Collections.Generic;
using KinshipCalculator.Core.Models;

namespace KinshipCalculator.WinUI.Options;

public sealed class ApplicationOptions
{
	public List<LanguageOption> Languages { get; set; } = [];
	public string StartingRole { get; set; } = "self";
	public PersonGender DefaultGender { get; set; } = PersonGender.Male;
}

public sealed class LanguageOption
{
	public string Key { get; set; } = "zh-Hans";
	public string Display { get; set; } = "简体中文";
}
