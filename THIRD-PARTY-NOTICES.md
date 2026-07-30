# Third-Party Notices

This distribution of **Sinitic Kinship Term Calculator** is a self-contained Windows build:
the executable embeds the .NET runtime pack, the Windows App SDK runtime family, Windows AI
MachineLearning and the NuGet components below. **Not everything embedded is MIT.**

**The authoritative, machine-generated list is [`LICENSE-INVENTORY.md`](LICENSE-INVENTORY.md)**,
derived by `Utility\Scripts\Build-LicenseInventory.ps1` from the locked dependency graph of
the very publish being shipped (`project.assets.json` for the PackageReference closure plus
the build's own `deps.json` for the embedded runtime packs). Every vendor license/notice
file found in those packages is reproduced under `ThirdPartyLicenses\<Package>.<Version>\`
and hashed in that inventory. This file is the human-readable summary; the inventory is the
part that cannot drift from the artifact.

> **Formal legal review status: NOT performed.** The inventory is a *technical completeness*
> artifact — it proves which vendor files exist for which embedded component and ships them.
> It does not adjudicate the redistribution terms. The Windows App SDK Software License
> Terms (including their end-user pass-through obligations), the Windows AI MachineLearning
> terms, and the .NET self-contained distribution surface still need a formal legal review
> before wide distribution. That review is an open release item.

## Microsoft platform components (NOT MIT)

| Component | License | Full text (in this package) |
|---|---|---|
| Windows App SDK 2.3.1 + its runtime family (WinUI, Foundation, Base, DWrite, InteractiveExperiences, Widgets, AI, ML) | **Microsoft Software License Terms — Microsoft Windows App SDK** (includes end-user terms that this notice passes through) | `ThirdPartyLicenses\Microsoft.WindowsAppSDK.2.3.1\` (`license.txt`, `NOTICE.txt`) and the per-component folders |
| Windows AI MachineLearning 2.1.74 (embedded) | Microsoft Software License Terms + its own third-party notices | `ThirdPartyLicenses\Microsoft.Windows.AI.MachineLearning.2.1.74\` (`license.txt`, `ThirdPartyNotices.txt`) |
| WebView2 loader (`WebView2Loader.dll`; Microsoft.Web.WebView2 1.0.3719.77) | BSD-style Microsoft license **requiring reproduction of the copyright notice, conditions and disclaimer in binary distributions** — reproduced in full | `ThirdPartyLicenses\Microsoft.Web.WebView2.1.0.3719.77\` (`LICENSE.txt`, `NOTICE.txt`) |
| .NET runtime packs 10.0.10 (`Microsoft.NETCore.App.Runtime.win-x64`, `Microsoft.WindowsDesktop.App.Runtime.win-x64`, embedded by the self-contained publish) | MIT for the cross-platform runtime, **plus** Windows-specific components under the .NET Library License and other Microsoft terms | `ThirdPartyLicenses\Microsoft.NETCore.App.Runtime.win-x64.10.0.10\` and `...WindowsDesktop...` (`LICENSE.TXT`, `THIRD-PARTY-NOTICES.TXT`); background in `ThirdPartyLicenses\DotNet-Windows-Licensing.md` |

## MIT-licensed NuGet components (direct references; the full closure is in the inventory)

| Component | Source |
|---|---|
| CommunityToolkit.Mvvm 8.4.0 | <https://github.com/CommunityToolkit/dotnet> |
| Microsoft.Extensions.Configuration / .Binder / .DependencyInjection 10.0.0 | <https://github.com/dotnet/runtime> |
| NetEscapades.Configuration.Yaml 3.1.0 | <https://github.com/andrewlock/NetEscapades.Configuration> |
| YamlDotNet (transitive) | <https://github.com/aaubry/YamlDotNet> |

This table names the direct references only. The **complete** resolved closure — every
transitive package and runtime pack actually embedded, with its declared license and the
SHA-256 of every shipped license/notice file — is in `LICENSE-INVENTORY.md`.

## Data provenance

| Component | License | Notes |
|---|---|---|
| mumuy/relationship | MIT | The kinship-term corpus this project's lexicon layers and verification oracle derive from; see `ATTRIBUTION.md` for the exact relationship (data cited, structure re-derived) and `LICENSE` for the reproduced MIT notice. <https://github.com/mumuy/relationship> |

The regional lexicon layers (`Lexicon\*.yaml`) carry per-file header notes where their
contents overlap mumuy's collection.
