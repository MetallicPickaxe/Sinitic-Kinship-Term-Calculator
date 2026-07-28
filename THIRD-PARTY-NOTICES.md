# Third-Party Notices

This distribution of **Sinitic Kinship Term Calculator** is a self-contained Windows build:
the executable embeds the .NET runtime, the Windows App SDK runtime, and the NuGet
components below. **Not everything embedded is MIT** — the accurate license for each
component is listed here, and the full texts that binary redistribution requires are
reproduced in the `ThirdPartyLicenses\` folder of this package. The project's own license
is in `LICENSE`.

> Formal legal review status: this notices file was assembled from the license files
> shipped inside the exact NuGet packages used by the build. A formal legal review of the
> Windows App SDK distribution terms (which include end-user pass-through obligations) has
> **not** been performed; treat that as an open item before wide distribution.

## Microsoft platform components (NOT MIT)

| Component | License | Full text |
|---|---|---|
| Windows App SDK 2.3.1 (WinUI 3 runtime, embedded via self-contained publish) | **Microsoft Software License Terms — Microsoft Windows App SDK** (includes end-user terms that this notice passes through) | `ThirdPartyLicenses\WindowsAppSDK-2.3.1-License.txt` |
| WebView2 loader (`WebView2Loader.dll`, embedded; Microsoft.Web.WebView2 1.0.3719.77) | BSD-style Microsoft license **requiring reproduction of the copyright notice, conditions and disclaimer in binary distributions** — reproduced in full | `ThirdPartyLicenses\WebView2-License.txt` |
| .NET runtime & libraries (embedded via self-contained publish) | MIT for the cross-platform runtime, **plus** Windows-specific components under the .NET Library License and other Microsoft terms | `ThirdPartyLicenses\DotNet-Windows-Licensing.md` (with links to Microsoft's license inventory) |

## MIT-licensed NuGet components

| Component | Source |
|---|---|
| CommunityToolkit.Mvvm 8.4.0 | <https://github.com/CommunityToolkit/dotnet> |
| Microsoft.Extensions.Configuration / .Binder / .DependencyInjection 10.0.0 | <https://github.com/dotnet/runtime> |
| NetEscapades.Configuration.Yaml 3.1.0 | <https://github.com/andrewlock/NetEscapades.Configuration> |
| YamlDotNet (transitive) | <https://github.com/aaubry/YamlDotNet> |

## Data provenance

| Component | License | Notes |
|---|---|---|
| mumuy/relationship | MIT | The kinship-term corpus this project's lexicon layers and verification oracle derive from; see `ATTRIBUTION.md` for the exact relationship (data cited, structure re-derived) and `LICENSE` for the reproduced MIT notice. <https://github.com/mumuy/relationship> |

The regional lexicon layers (`Lexicon\*.yaml`) carry per-file header notes where their
contents overlap mumuy's collection.
