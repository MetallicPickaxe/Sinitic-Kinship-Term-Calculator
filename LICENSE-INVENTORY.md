# Third-party license inventory

**Generated** by `Utility\Scripts\Build-LicenseInventory.ps1` from the ARTIFACT of the
release publish: the resolved PackageReference closure in `UI\obj\project.assets.json`
(restored with the release properties) plus the runtime packs named in the release
build's own `deps.json`. Vendor files are discovered RECURSIVELY inside each package,
and a package whose nuspec declares `<license type="file">` must ship that exact file or
generation fails. It is not hand-maintained and cannot drift from the artifact.

- Target: `net11.0-windows10.0.26100.0/win-x64`
- Runtime packs (from the artifact): `Microsoft.NETCore.App.Runtime.win-x64/11.0.0-preview.6.26359.118`, `Microsoft.Windows.SDK.NET.Ref/10.0.26100.57`
- Components: **28**
- Files reproduced under `ThirdPartyLicenses\`: **32**

> **Layering matters.** Only rows marked *runtime payload* or *runtime pack* are
> carried inside the shipped executable. *Reference only* and *build only* rows take
> part in producing it without being embedded; they are listed for completeness of
> the build graph, not as redistributed binaries.

> **Legal review status:** this inventory is a *technical completeness* artifact —
> it proves which vendor files exist for which component of the build and ships them.
> A formal legal review of the redistribution terms (notably the Windows App SDK
> Software License Terms and their end-user pass-through obligations, the Windows AI
> MachineLearning terms, and the .NET self-contained distribution surface) has **not**
> been performed. That review remains an open release item.

## Components

| Component | Version | Layer | Declared license | Shipped license/notice files |
|---|---|---|---|---|
| Microsoft.NET.ILLink.Tasks | 11.0.0-preview.6.26359.118 | build only (not shipped) | expression: MIT | `THIRD-PARTY-NOTICES.TXT` |
| Microsoft.Windows.SDK.BuildTools | 10.0.28000.2526 | build only (not shipped) | url: https://aka.ms/WinSDKLicenseURL | — *(none in package)* |
| Microsoft.Windows.SDK.BuildTools.MSIX | 1.7.251221100 | build only (not shipped) | file: sdk_license.txt | `NOTICE.txt`<br>`sdk_license.txt` |
| Microsoft.WindowsAppSDK | 2.3.2-experimentalA | build only (not shipped) | file: license.txt | `license.txt`<br>`NOTICE.txt` |
| Microsoft.WindowsAppSDK.Base | 2.0.5-experimental2 | build only (not shipped) | file: license.txt | `license.txt`<br>`NOTICE.txt` |
| Microsoft.WindowsAppSDK.DWrite | 2.1.1-experimental | build only (not shipped) | file: license.txt | `license.txt` |
| Microsoft.WindowsAppSDK.ML | 2.2.11-experimental | build only (not shipped) | file: license.txt | `license.txt` |
| Microsoft.WindowsAppSDK.Runtime | 2.3.2-experimentalA | build only (not shipped) | file: license.txt | `license.txt`<br>`NOTICE.txt` |
| Microsoft.NETCore.App.Runtime.win-x64 | 11.0.0-preview.6.26359.118 | runtime pack (embedded by self-contained publish) | expression: MIT | `LICENSE.TXT`<br>`THIRD-PARTY-NOTICES.TXT` |
| Microsoft.Windows.SDK.NET.Ref | 10.0.26100.57 | runtime pack (embedded by self-contained publish) | url: https://aka.ms/WinSDKLicenseURL | — *(none in package)* |
| CommunityToolkit.Mvvm | 8.4.2 | runtime payload | expression: MIT | `License.md`<br>`ThirdPartyNotices.txt` |
| Microsoft.Extensions.Configuration | 11.0.0-preview.6.26359.118 | runtime payload | expression: MIT | `THIRD-PARTY-NOTICES.TXT` |
| Microsoft.Extensions.Configuration.Binder | 11.0.0-preview.6.26359.118 | runtime payload | expression: MIT | `THIRD-PARTY-NOTICES.TXT` |
| Microsoft.Extensions.Configuration.FileExtensions | 2.0.0 | runtime payload | url: https://raw.githubusercontent.com/aspnet/Home/2.0.0/LICENSE.txt | — *(none in package)* |
| Microsoft.Extensions.DependencyInjection | 11.0.0-preview.6.26359.118 | runtime payload | expression: MIT | `THIRD-PARTY-NOTICES.TXT` |
| Microsoft.Extensions.FileProviders.Physical | 2.0.0 | runtime payload | url: https://raw.githubusercontent.com/aspnet/Home/2.0.0/LICENSE.txt | — *(none in package)* |
| Microsoft.Extensions.FileSystemGlobbing | 2.0.0 | runtime payload | url: https://raw.githubusercontent.com/aspnet/Home/2.0.0/LICENSE.txt | — *(none in package)* |
| Microsoft.Web.WebView2 | 1.0.3719.77 | runtime payload | file: LICENSE.txt | `LICENSE.txt`<br>`NOTICE.txt` |
| Microsoft.Windows.AI.MachineLearning | 2.4.66-preview | runtime payload | file: license.txt | `license.txt`<br>`ThirdPartyNotices.txt` |
| Microsoft.WindowsAppSDK.AI | 2.3.10-experimental | runtime payload | file: license.txt | `license.txt` |
| Microsoft.WindowsAppSDK.Foundation | 2.3.7-experimental | runtime payload | file: license.txt | `license.txt` |
| Microsoft.WindowsAppSDK.InteractiveExperiences | 2.1.4-experimental | runtime payload | file: license.txt | `license.txt` |
| Microsoft.WindowsAppSDK.Search | 2.3.10-experimental | runtime payload | file: license.txt | `license.txt` |
| Microsoft.WindowsAppSDK.Widgets | 2.0.6-experimental | runtime payload | file: license.txt | `license.txt` |
| Microsoft.WindowsAppSDK.WinUI | 2.3.3-experimental | runtime payload | file: license.txt | `license.txt`<br>`NOTICE.txt`<br>`tools/NOTICE.txt` |
| NetEscapades.Configuration.Yaml | 3.1.0 | runtime payload | expression: MIT | — *(none in package)* |
| System.Numerics.Tensors | 9.0.0 | runtime payload | expression: MIT | `LICENSE.TXT`<br>`THIRD-PARTY-NOTICES.TXT` |
| YamlDotNet | 18.1.0 | runtime payload | expression: MIT | — *(none in package)* |

## Package content hashes (as recorded by the restore)

| Component | Version | NuGet content hash |
|---|---|---|
| CommunityToolkit.Mvvm | 8.4.2 | `WadCzGEc2U+3e20avRLng4qNtt4zoOGWrdUISqJWrHe3/FSnrYjuM5Sb4yQb09LhkBXrrI4Zt3dLKgRMbItsrg==` |
| Microsoft.Extensions.Configuration | 11.0.0-preview.6.26359.118 | `OYs4no/Sa4J7yO5rkhCmJS0FjVhp1WyWKyLMEqX2N5oVgBIq6n42PNKN3t6ySQ8hnA+bgrb73JKVv5+isfzhdA==` |
| Microsoft.Extensions.Configuration.Binder | 11.0.0-preview.6.26359.118 | `wESnm2Vim8kbLMC0i0NBifmMWINGbC31ifGhusIeOv0U+bnMuXpOGY282dajis+8YHvO5Cg2ECp1q4ltdtIcqA==` |
| Microsoft.Extensions.Configuration.FileExtensions | 2.0.0 | `ebFbu+vsz4rzeAICWavk9a0FutWVs7aNZap5k/IVxVhu2CnnhOp/H/gNtpzplrqjYDaNYdmv9a/DoUvH2ynVEQ==` |
| Microsoft.Extensions.DependencyInjection | 11.0.0-preview.6.26359.118 | `MwlfMwxXewuq+X85a31yYx6emRYIyJOboVXZtRH3Wvp/hZZWmlg1pu/tI+XJUm4ltoCNMzpcFF6zJcmbnbnuGA==` |
| Microsoft.Extensions.FileProviders.Physical | 2.0.0 | `DKO2j2socZbHNCCVEWsLVpB3AQIIzKYFNyITVeWdA1jQ829GJIQf4MUD04+1c+Q2kbK03pIKQZmEy4CGIfgDZw==` |
| Microsoft.Extensions.FileSystemGlobbing | 2.0.0 | `UC87vRDUB7/vSaNY/FVhbdAyRkfFBTkYmcUoglxk6TyTojhSqYaG5pZsoP4e1ZuXktFXJXJBTvK8U/QwCo0z3g==` |
| Microsoft.NET.ILLink.Tasks | 11.0.0-preview.6.26359.118 | `8nEniNeLHH6d4cuzUK8LsHb3FlnyAaYjU1LL24li9LxB2V3Equ7Khvz4v5fdQNBZ7CfNnwwJ2DvPVnE+yuDAjw==` |
| Microsoft.NETCore.App.Runtime.win-x64 | 11.0.0-preview.6.26359.118 | `7PsYbP/YHuLMGKmX3efW4dYbh+LS3rSO9u0+HfWPiBxIGZ9oXdCIzd5WDfDYDFTi5FcNa45iEiqX5EAUnDQ/kw==` |
| Microsoft.Web.WebView2 | 1.0.3719.77 | `t+ucyKw5NTwMjsUrDF6R9Lk40lpcKQD1/HgyGFxl49tdA4h9dKlsj6FYGEmDRpFNfnTpENuTypMcdbrlkqBdDA==` |
| Microsoft.Windows.AI.MachineLearning | 2.4.66-preview | `U1XiMef0We7n1nHSvQ2az1u5tHTTvRYKPXfclOAg1GalGf6r2vETirpilg/wP98vTwLteQyj6Jdvfhkk8Sze2A==` |
| Microsoft.Windows.SDK.BuildTools | 10.0.28000.2526 | `voeXXRC5EKuvnUCkPxkEhCbNuv/FM1RzjCQ3lj58Ju5AlVzh/9Lg+a7hqy57mh/00BH/9YNZCJRUjIZJrzTJ4w==` |
| Microsoft.Windows.SDK.BuildTools.MSIX | 1.7.251221100 | `f4aIZJ0NUth2403oxrpR+9rxVzZVI6dabqB21u8ncnk8eJAKCs9m77E4iYAnvP1YwrRe4axz3f8+yUcttNSfEA==` |
| Microsoft.Windows.SDK.NET.Ref | 10.0.26100.57 | `BG2X8tqoPJu7Lde8hlg/3bF9QwOW3VsxI7MGPEQM5+i4YG0WgNeesIeia2VTRkVEnoC9SdxpFGJtdGQHHIBwcA==` |
| Microsoft.WindowsAppSDK | 2.3.2-experimentalA | `ji8m9ItXbzanN6TG6m5yhtoyFnDiMXHRWnK5hpItWU/AZKHTDYjzFsvJkou2Z0iFYf2JTM/bK/L3GFa4E+rIPw==` |
| Microsoft.WindowsAppSDK.AI | 2.3.10-experimental | `jp5fTZ+cBuDSX3L9ONPEmzrJTgZ7b/f3nfkvCGSoHukVjVZwUHUrvTkeJVfv0YwOAuA2+mBeiTb+hG2T8Md2Tg==` |
| Microsoft.WindowsAppSDK.Base | 2.0.5-experimental2 | `1ZLLuK+S830P9U55xbDLiRUHlGD4r2kMtI34A6jJ9OF7pBzkqpBkfsVwGWare6BJ1616VNutez86sJtzFFGwng==` |
| Microsoft.WindowsAppSDK.DWrite | 2.1.1-experimental | `Ctnt+XC61o5iURsedqFoU9Jyur/Ecf6WZNj8d5cNJo7XDNQevQCGw8flikfO9kqsHUnZSTEq83OgcAC3exoh2Q==` |
| Microsoft.WindowsAppSDK.Foundation | 2.3.7-experimental | `gZJFiAkAo9VGNc7GOznQfG2MgyFw/HA9zwq+KkDtm3lnqbKbQONsz4OwHV0FTgzzBdVOWbB4YiDj8o+cFMXlvg==` |
| Microsoft.WindowsAppSDK.InteractiveExperiences | 2.1.4-experimental | `u4JkyLzcdgBOEwoOQnRCjzldSMYOSVmgmgNocuXxoDJtHi225BcXe7chW3ciiqfFT5YmH/Ieu3GRqEUuHU7ahQ==` |
| Microsoft.WindowsAppSDK.ML | 2.2.11-experimental | `9DExOTdI5PZxgJxh+p6JbhOfpcW5yunlomj/J9hqeeCGp+3kvDehlq3rSYn3jwT6s2RFyuxgTZSrGZjET8KMWA==` |
| Microsoft.WindowsAppSDK.Runtime | 2.3.2-experimentalA | `ZCbVzbKJ8ChdZrzISeLmp+eklSHtCge7Us5GOJg6n9HBhjHECw2C50LT1BSWtg9qLNu1KkkaOplbkb8MVSX8pg==` |
| Microsoft.WindowsAppSDK.Search | 2.3.10-experimental | `oKvktrDm6W1J+x5uA1oX0YLWUG23F1yPWzuOmTVLtFtX81wVg/z418zL20dpIhZ6QecaPcpY+dcUld4PPSprRQ==` |
| Microsoft.WindowsAppSDK.Widgets | 2.0.6-experimental | `YtM+o6qH8KVwI7QjnpaOX8HQeKd4xIBYl+a2PsgypPN2OphyWu/agB5DXqHAc0edn/cSHnqU4tIJMNw1nzoFrA==` |
| Microsoft.WindowsAppSDK.WinUI | 2.3.3-experimental | `1MHdVW+4gtX06Ga/7OelyQZ4Mv7CRypXA7JQ+tHKveZA1iI3YdCD2SIs2gs3OAs4pRbERj10QFuHDGUs/5/G8w==` |
| NetEscapades.Configuration.Yaml | 3.1.0 | `D5Pxt4hXABna5OwYQmAQukspW7LEoYgvfAqyw85gUF/gnH9pWHsZCLMXy2ewWoQ0PELZ1lOGFLDbDVeoCvtBgA==` |
| System.Numerics.Tensors | 9.0.0 | `hyJB4UlpAi19Xr9AXzu2NuagKC4lPfHObNMEAA0HmqFz2rX7wKgzeYzO/jM/eBHDhnUGFFEjk5cOoJaxqg5J4A==` |
| YamlDotNet | 18.1.0 | `5K+9KFg2TdTl7VXv88Qzi/0lqK6JFoNP3lRuImPYGRV7K/QYklDyTrj4+A+KAki1JsQi6qKY+hDyY7d6WRqjrw==` |

## Shipped files (SHA-256)

| File | Bytes | SHA-256 |
|---|---:|---|
| `ThirdPartyLicenses/CommunityToolkit.Mvvm.8.4.2/License.md` | 1158 | `651997EF19DBB9ECB8DD660DBC86444A65AF21F18CFE08DAA69BF201CD5CFDFC` |
| `ThirdPartyLicenses/CommunityToolkit.Mvvm.8.4.2/ThirdPartyNotices.txt` | 8600 | `7774F8B0AB66BFB47EC154CE24A2D6D574FEBC44A74D940B8A037D1C56756163` |
| `ThirdPartyLicenses/Microsoft.Extensions.Configuration.11.0.0-preview.6.26359.118/THIRD-PARTY-NOTICES.TXT` | 91864 | `08CB63505AAB037FD513D093AA62FF7201D3CCA46C781F39389AF92C4E367B3B` |
| `ThirdPartyLicenses/Microsoft.Extensions.Configuration.Binder.11.0.0-preview.6.26359.118/THIRD-PARTY-NOTICES.TXT` | 91864 | `08CB63505AAB037FD513D093AA62FF7201D3CCA46C781F39389AF92C4E367B3B` |
| `ThirdPartyLicenses/Microsoft.Extensions.DependencyInjection.11.0.0-preview.6.26359.118/THIRD-PARTY-NOTICES.TXT` | 91864 | `08CB63505AAB037FD513D093AA62FF7201D3CCA46C781F39389AF92C4E367B3B` |
| `ThirdPartyLicenses/Microsoft.NET.ILLink.Tasks.11.0.0-preview.6.26359.118/THIRD-PARTY-NOTICES.TXT` | 91864 | `08CB63505AAB037FD513D093AA62FF7201D3CCA46C781F39389AF92C4E367B3B` |
| `ThirdPartyLicenses/Microsoft.NETCore.App.Runtime.win-x64.11.0.0-preview.6.26359.118/LICENSE.TXT` | 1139 | `D7A68596AB69B06F51CA278A6545148E4269A9381C26D597C13DF5D88E08CF5B` |
| `ThirdPartyLicenses/Microsoft.NETCore.App.Runtime.win-x64.11.0.0-preview.6.26359.118/THIRD-PARTY-NOTICES.TXT` | 91864 | `08CB63505AAB037FD513D093AA62FF7201D3CCA46C781F39389AF92C4E367B3B` |
| `ThirdPartyLicenses/Microsoft.Web.WebView2.1.0.3719.77/LICENSE.txt` | 1487 | `0AF8F1B807512AAE39C2AC1AA4D0CAE65CABECB6FD554B8439A5162A0D6ECA55` |
| `ThirdPartyLicenses/Microsoft.Web.WebView2.1.0.3719.77/NOTICE.txt` | 3894 | `106423785C5B7EBA0A8E61D1837F2132E9C828E20AD530F565D981C1DF60DD90` |
| `ThirdPartyLicenses/Microsoft.Windows.AI.MachineLearning.2.4.66-preview/license.txt` | 13996 | `66395F8CB219087FAE2BD025010BD9076B736C14F03B48F20295471C0C376814` |
| `ThirdPartyLicenses/Microsoft.Windows.AI.MachineLearning.2.4.66-preview/ThirdPartyNotices.txt` | 331175 | `FB0AF774B4D7CFFC5B9D046F2AAEADE2F37DF2F80ABF8033C95DFFFCC77A8866` |
| `ThirdPartyLicenses/Microsoft.Windows.SDK.BuildTools.MSIX.1.7.251221100/NOTICE.txt` | 2061 | `F09EBF5DE6A5C8E7EBC900BB2E52AF2F75E13610A1CF91E1514530E448D956C3` |
| `ThirdPartyLicenses/Microsoft.Windows.SDK.BuildTools.MSIX.1.7.251221100/sdk_license.txt` | 33483 | `A7A5C7E7FF998558983D6ACA2702117C328AEB0C6404D298CB275F5623C6FD13` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.2.3.2-experimentalA/license.txt` | 11503 | `C2F1D9EFBA5864366335C007D51CAB1D5B07005E9F1A67D7CA90B7B2A01FD615` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.2.3.2-experimentalA/NOTICE.txt` | 340893 | `572B43D41DEA717DAE7DC5DE69ACB20A74DF025E8F5A3C0AA6F7BCA02615E23C` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.AI.2.3.10-experimental/license.txt` | 11503 | `C2F1D9EFBA5864366335C007D51CAB1D5B07005E9F1A67D7CA90B7B2A01FD615` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.Base.2.0.5-experimental2/license.txt` | 11503 | `C2F1D9EFBA5864366335C007D51CAB1D5B07005E9F1A67D7CA90B7B2A01FD615` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.Base.2.0.5-experimental2/NOTICE.txt` | 340893 | `572B43D41DEA717DAE7DC5DE69ACB20A74DF025E8F5A3C0AA6F7BCA02615E23C` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.DWrite.2.1.1-experimental/license.txt` | 12637 | `5B11E6347756E40FE0274BC08C97F89201B94F0D50181A09A00F1F4740840501` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.Foundation.2.3.7-experimental/license.txt` | 11503 | `C2F1D9EFBA5864366335C007D51CAB1D5B07005E9F1A67D7CA90B7B2A01FD615` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.InteractiveExperiences.2.1.4-experimental/license.txt` | 11503 | `C2F1D9EFBA5864366335C007D51CAB1D5B07005E9F1A67D7CA90B7B2A01FD615` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.ML.2.2.11-experimental/license.txt` | 14033 | `656AAB74C15AA9F9964BCDCC993EB2755CBDB4822D5E0E3BC61D2E281897F758` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.Runtime.2.3.2-experimentalA/license.txt` | 11503 | `C2F1D9EFBA5864366335C007D51CAB1D5B07005E9F1A67D7CA90B7B2A01FD615` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.Runtime.2.3.2-experimentalA/NOTICE.txt` | 340893 | `572B43D41DEA717DAE7DC5DE69ACB20A74DF025E8F5A3C0AA6F7BCA02615E23C` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.Search.2.3.10-experimental/license.txt` | 11503 | `C2F1D9EFBA5864366335C007D51CAB1D5B07005E9F1A67D7CA90B7B2A01FD615` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.Widgets.2.0.6-experimental/license.txt` | 11503 | `C2F1D9EFBA5864366335C007D51CAB1D5B07005E9F1A67D7CA90B7B2A01FD615` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.WinUI.2.3.3-experimental/license.txt` | 11503 | `C2F1D9EFBA5864366335C007D51CAB1D5B07005E9F1A67D7CA90B7B2A01FD615` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.WinUI.2.3.3-experimental/NOTICE.txt` | 2061 | `E25393C0D340A1821827B093FA4DBBFCCCD8FEB7BF769E7FA773E3955CD5314B` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.WinUI.2.3.3-experimental/tools/NOTICE.txt` | 638 | `4A9321FD173EBF4E8BBC79130794D4C3FE233ACCCBF7DB8765E0ADDB8FBFB8CA` |
| `ThirdPartyLicenses/System.Numerics.Tensors.9.0.0/LICENSE.TXT` | 1139 | `D7A68596AB69B06F51CA278A6545148E4269A9381C26D597C13DF5D88E08CF5B` |
| `ThirdPartyLicenses/System.Numerics.Tensors.9.0.0/THIRD-PARTY-NOTICES.TXT` | 75640 | `40686C6447A7D5B5D3693068E4571B5F483D7ED335AEEE773EF662440DE4C5D5` |

## Components shipping no license file

These packages carry no LICENSE/NOTICE file of their own. Their terms are the declared
license expression in the table above.

- Microsoft.Extensions.Configuration.FileExtensions 2.0.0 — url: https://raw.githubusercontent.com/aspnet/Home/2.0.0/LICENSE.txt
- Microsoft.Extensions.FileProviders.Physical 2.0.0 — url: https://raw.githubusercontent.com/aspnet/Home/2.0.0/LICENSE.txt
- Microsoft.Extensions.FileSystemGlobbing 2.0.0 — url: https://raw.githubusercontent.com/aspnet/Home/2.0.0/LICENSE.txt
- Microsoft.Windows.SDK.BuildTools 10.0.28000.2526 — url: https://aka.ms/WinSDKLicenseURL
- Microsoft.Windows.SDK.NET.Ref 10.0.26100.57 — url: https://aka.ms/WinSDKLicenseURL
- NetEscapades.Configuration.Yaml 3.1.0 — expression: MIT
- YamlDotNet 18.1.0 — expression: MIT

