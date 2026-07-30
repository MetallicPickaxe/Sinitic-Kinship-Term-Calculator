# Third-party license inventory

**Generated** by `Utility\Scripts\Build-LicenseInventory.ps1` from the ARTIFACT of the
release publish: the resolved PackageReference closure in `UI\obj\project.assets.json`
(restored with the release properties) plus the runtime packs named in the release
build's own `deps.json`. Vendor files are discovered RECURSIVELY inside each package,
and a package whose nuspec declares `<license type="file">` must ship that exact file or
generation fails. It is not hand-maintained and cannot drift from the artifact.

- Target: `net10.0-windows10.0.26100/win-x64`
- Runtime packs (from the artifact): `Microsoft.NETCore.App.Runtime.win-x64/10.0.10`, `Microsoft.Windows.SDK.NET.Ref/10.0.26100.57`
- Components: **31**
- Files reproduced under `ThirdPartyLicenses\`: **35**

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
| Microsoft.NET.ILLink.Tasks | 10.0.10 | build only (not shipped) | expression: MIT | `THIRD-PARTY-NOTICES.TXT` |
| Microsoft.Windows.SDK.BuildTools | 10.0.28000.1-RTM | build only (not shipped) | url: https://aka.ms/WinSDKLicenseURL | — *(none in package)* |
| Microsoft.Windows.SDK.BuildTools.MSIX | 1.7.251221100 | build only (not shipped) | file: sdk_license.txt | `NOTICE.txt`<br>`sdk_license.txt` |
| Microsoft.WindowsAppSDK | 2.3.1 | build only (not shipped) | file: license.txt | `license.txt`<br>`NOTICE.txt` |
| Microsoft.WindowsAppSDK.Base | 2.0.4 | build only (not shipped) | file: license.txt | `license.txt`<br>`NOTICE.txt` |
| Microsoft.WindowsAppSDK.DWrite | 2.1.0 | build only (not shipped) | file: license.txt | `license.txt` |
| Microsoft.WindowsAppSDK.ML | 2.1.74 | build only (not shipped) | file: license.txt | `license.txt`<br>`ThirdPartyNotices.txt` |
| Microsoft.WindowsAppSDK.Runtime | 2.3.1 | build only (not shipped) | file: license.txt | `license.txt`<br>`NOTICE.txt` |
| Microsoft.NETCore.App.Runtime.win-x64 | 10.0.10 | runtime pack (embedded by self-contained publish) | expression: MIT | `LICENSE.TXT`<br>`THIRD-PARTY-NOTICES.TXT` |
| Microsoft.Windows.SDK.NET.Ref | 10.0.26100.57 | runtime pack (embedded by self-contained publish) | url: https://aka.ms/WinSDKLicenseURL | — *(none in package)* |
| CommunityToolkit.Mvvm | 8.4.0 | runtime payload | expression: MIT | `License.md`<br>`ThirdPartyNotices.txt` |
| Microsoft.Extensions.Configuration | 10.0.0 | runtime payload | expression: MIT | `THIRD-PARTY-NOTICES.TXT` |
| Microsoft.Extensions.Configuration.Abstractions | 10.0.0 | runtime payload | expression: MIT | `THIRD-PARTY-NOTICES.TXT` |
| Microsoft.Extensions.Configuration.Binder | 10.0.0 | runtime payload | expression: MIT | `THIRD-PARTY-NOTICES.TXT` |
| Microsoft.Extensions.Configuration.FileExtensions | 2.0.0 | runtime payload | url: https://raw.githubusercontent.com/aspnet/Home/2.0.0/LICENSE.txt | — *(none in package)* |
| Microsoft.Extensions.DependencyInjection | 10.0.0 | runtime payload | expression: MIT | `THIRD-PARTY-NOTICES.TXT` |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.0 | runtime payload | expression: MIT | `THIRD-PARTY-NOTICES.TXT` |
| Microsoft.Extensions.FileProviders.Abstractions | 2.0.0 | runtime payload | url: https://raw.githubusercontent.com/aspnet/Home/2.0.0/LICENSE.txt | — *(none in package)* |
| Microsoft.Extensions.FileProviders.Physical | 2.0.0 | runtime payload | url: https://raw.githubusercontent.com/aspnet/Home/2.0.0/LICENSE.txt | — *(none in package)* |
| Microsoft.Extensions.FileSystemGlobbing | 2.0.0 | runtime payload | url: https://raw.githubusercontent.com/aspnet/Home/2.0.0/LICENSE.txt | — *(none in package)* |
| Microsoft.Extensions.Primitives | 10.0.0 | runtime payload | expression: MIT | `THIRD-PARTY-NOTICES.TXT` |
| Microsoft.Web.WebView2 | 1.0.3719.77 | runtime payload | file: LICENSE.txt | `LICENSE.txt`<br>`NOTICE.txt` |
| Microsoft.Windows.AI.MachineLearning | 2.1.74 | runtime payload | file: license.txt | `license.txt`<br>`ThirdPartyNotices.txt` |
| Microsoft.WindowsAppSDK.AI | 2.3.4 | runtime payload | file: license.txt | `license.txt` |
| Microsoft.WindowsAppSDK.Foundation | 2.3.5 | runtime payload | file: license.txt | `license.txt` |
| Microsoft.WindowsAppSDK.InteractiveExperiences | 2.1.3 | runtime payload | file: license.txt | `license.txt` |
| Microsoft.WindowsAppSDK.Widgets | 2.0.5 | runtime payload | file: license.txt | `license.txt` |
| Microsoft.WindowsAppSDK.WinUI | 2.3.0 | runtime payload | file: license.txt | `license.txt`<br>`NOTICE.txt`<br>`tools/NOTICE.txt` |
| NetEscapades.Configuration.Yaml | 3.1.0 | runtime payload | expression: MIT | — *(none in package)* |
| System.Numerics.Tensors | 9.0.0 | runtime payload | expression: MIT | `LICENSE.TXT`<br>`THIRD-PARTY-NOTICES.TXT` |
| YamlDotNet | 16.3.0 | runtime payload | expression: MIT | — *(none in package)* |

## Package content hashes (as recorded by the restore)

| Component | Version | NuGet content hash |
|---|---|---|
| CommunityToolkit.Mvvm | 8.4.0 | `tqVU8yc/ADO9oiTRyTnwhFN68hCwvkliMierptWOudIAvWY1mWCh5VFh+guwHJmpMwfg0J0rY+yyd5Oy7ty9Uw==` |
| Microsoft.Extensions.Configuration | 10.0.0 | `H4SWETCh/cC5L1WtWchHR6LntGk3rDTTznZMssr4cL8IbDmMWBxY+MOGDc/ASnqNolLKPIWHWeuC1ddiL/iNPw==` |
| Microsoft.Extensions.Configuration.Abstractions | 10.0.0 | `d2kDKnCsJvY7mBVhcjPSp9BkJk48DsaHPg5u+Oy4f8XaOqnEedRy/USyvnpHL92wpJ6DrTPy7htppUUzskbCXQ==` |
| Microsoft.Extensions.Configuration.Binder | 10.0.0 | `tMF9wNh+hlyYDWB8mrFCQHQmWHlRosol1b/N2Jrefy1bFLnuTlgSYmPyHNmz8xVQgs7DpXytBRWxGhG+mSTp0g==` |
| Microsoft.Extensions.Configuration.FileExtensions | 2.0.0 | `ebFbu+vsz4rzeAICWavk9a0FutWVs7aNZap5k/IVxVhu2CnnhOp/H/gNtpzplrqjYDaNYdmv9a/DoUvH2ynVEQ==` |
| Microsoft.Extensions.DependencyInjection | 10.0.0 | `f0RBabswJq+gRu5a+hWIobrLWiUYPKMhCD9WO3sYBAdSy3FFH14LMvLVFZc2kPSCimBLxSuitUhsd6tb0TAY6A==` |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.0 | `L3AdmZ1WOK4XXT5YFPEwyt0ep6l8lGIPs7F5OOBZc77Zqeo01Of7XXICy47628sdVl0v/owxYJTe86DTgFwKCA==` |
| Microsoft.Extensions.FileProviders.Abstractions | 2.0.0 | `Z0AK+hmLO33WAXQ5P1uPzhH7z5yjDHX/XnUefXxE//SyvCb9x4cVjND24dT5566t/yzGp8/WLD7EG9KQKZZklQ==` |
| Microsoft.Extensions.FileProviders.Physical | 2.0.0 | `DKO2j2socZbHNCCVEWsLVpB3AQIIzKYFNyITVeWdA1jQ829GJIQf4MUD04+1c+Q2kbK03pIKQZmEy4CGIfgDZw==` |
| Microsoft.Extensions.FileSystemGlobbing | 2.0.0 | `UC87vRDUB7/vSaNY/FVhbdAyRkfFBTkYmcUoglxk6TyTojhSqYaG5pZsoP4e1ZuXktFXJXJBTvK8U/QwCo0z3g==` |
| Microsoft.Extensions.Primitives | 10.0.0 | `inRnbpCS0nwO/RuoZIAqxQUuyjaknOOnCEZB55KSMMjRhl0RQDttSmLSGsUJN3RQ3ocf5NDLFd2mOQViHqMK5w==` |
| Microsoft.NET.ILLink.Tasks | 10.0.10 | `f5VCIE7AJpd5YvzNTeMGVzQIgyE9tX+AreTYwQF+REbu+DZo/2Ae+jNSwhPEYrVz6RRkd7y8ubXjk6Nn6Ka+Cg==` |
| Microsoft.NETCore.App.Runtime.win-x64 | 10.0.10 | `d/6m2bwNP35/bBDmtNPb0GBLwlK0eyM8/Qb7zEtIFJDgjAo960bROUlB6XcuLM4VmfA8hklOgGDU98nUg6xDwA==` |
| Microsoft.Web.WebView2 | 1.0.3719.77 | `t+ucyKw5NTwMjsUrDF6R9Lk40lpcKQD1/HgyGFxl49tdA4h9dKlsj6FYGEmDRpFNfnTpENuTypMcdbrlkqBdDA==` |
| Microsoft.Windows.AI.MachineLearning | 2.1.74 | `/Z1yAQ0J53eD1RrOl5anTDS0UQOgneERY//hLljwGcWeUX9ynCvOl5qUxAsfWvwsZZGcvV4WKjAvnAtA6N5i+w==` |
| Microsoft.Windows.SDK.BuildTools | 10.0.28000.1-RTM | `2uWdC7++WJ8m2fqROh/NawNgAVgZ0n5bkzahG1SGk3AViqqIiK/J71KEyhrMw5Sdfn9U4gaHlB3OXrTWVYWtoA==` |
| Microsoft.Windows.SDK.BuildTools.MSIX | 1.7.251221100 | `f4aIZJ0NUth2403oxrpR+9rxVzZVI6dabqB21u8ncnk8eJAKCs9m77E4iYAnvP1YwrRe4axz3f8+yUcttNSfEA==` |
| Microsoft.Windows.SDK.NET.Ref | 10.0.26100.57 | `BG2X8tqoPJu7Lde8hlg/3bF9QwOW3VsxI7MGPEQM5+i4YG0WgNeesIeia2VTRkVEnoC9SdxpFGJtdGQHHIBwcA==` |
| Microsoft.WindowsAppSDK | 2.3.1 | `eUfqy7RM+yCn853kAy9fU3sXvqI3la2St4Uj9Qd5m8i0ngizM2X2l4foc/pCDGZyJybqKbcuZdq/CHHjAc534Q==` |
| Microsoft.WindowsAppSDK.AI | 2.3.4 | `lMpBN1Iy2VV7dAvVJPnALw9Xm2xxbuI6EVQVHxxgvo37qyz3Oz3zW+1wrKG5pOMuQchs1RUpelSy4cgbF6j1jA==` |
| Microsoft.WindowsAppSDK.Base | 2.0.4 | `QXSy2llX2/Bx9dYWGUEsb10F40q94ZiDnPMzqa2/qRmTG4y9EXxGp6uHITb7c0b4FCa+6KFBlgh6D53M0QEMEg==` |
| Microsoft.WindowsAppSDK.DWrite | 2.1.0 | `dgix7NSeo7Z8SZPyrsOqYm/6OUvBveHCiVyorYecmtDYoZW26dJ6/i9yveIxgWvmFpgfKXPeIuQBCvhbetHiYg==` |
| Microsoft.WindowsAppSDK.Foundation | 2.3.5 | `Ke/HAoFLq1kgMPUn6UrNNBy5kcuHCJK/SvHep0m4FY1YoDGz9XUDnucDmCMSUxuGnxtI65Uq5UfmLc3eS31Ovg==` |
| Microsoft.WindowsAppSDK.InteractiveExperiences | 2.1.3 | `2GwSpAWidRKZiGX0yeJ9SxjdgPYMpZXRaHbiss3rC4FglFYSpF/wmhW5axX2zJ/NP2e4LOoOiPtw1W4YRyQiFg==` |
| Microsoft.WindowsAppSDK.ML | 2.1.74 | `9Vd1ikH+n6pbqhgmCn+zNulJ6gIp9Hz9bC++ka1Uphh2gule6HiT6Jr25N/fQoEc4I9xOTKkn3DtlcpHlcocaw==` |
| Microsoft.WindowsAppSDK.Runtime | 2.3.1 | `QwXWOMMuTDjUSgUBQ25zgEw3KBP2peKckv8NxNEBW2+Gst6zb6Yg+7xdWG90wtag3hobcAz4y8a20e00uwjf4g==` |
| Microsoft.WindowsAppSDK.Widgets | 2.0.5 | `lRz+8+QU65gV8hRzLVE+GRsPKZ42lnzqkmsw96HzQd/DuFYfr4JYpUU7ZZ5YrkV2EfQe4BCmoRKAeZ3WwalMcg==` |
| Microsoft.WindowsAppSDK.WinUI | 2.3.0 | `NtE5frGZttAnMTl0YOCg3fKt6HrwGuB0rgoYCfP3se5xJToIpGreDYrUiVc3CeMmvnyptjTbwMt/41XWf8qMOQ==` |
| NetEscapades.Configuration.Yaml | 3.1.0 | `D5Pxt4hXABna5OwYQmAQukspW7LEoYgvfAqyw85gUF/gnH9pWHsZCLMXy2ewWoQ0PELZ1lOGFLDbDVeoCvtBgA==` |
| System.Numerics.Tensors | 9.0.0 | `hyJB4UlpAi19Xr9AXzu2NuagKC4lPfHObNMEAA0HmqFz2rX7wKgzeYzO/jM/eBHDhnUGFFEjk5cOoJaxqg5J4A==` |
| YamlDotNet | 16.3.0 | `SgMOdxbz8X65z8hraIs6hOEdnkH6hESTAIUa7viEngHOYaH+6q5XJmwr1+yb9vJpNQ19hCQY69xbFsLtXpobQA==` |

## Shipped files (SHA-256)

| File | Bytes | SHA-256 |
|---|---:|---|
| `ThirdPartyLicenses/CommunityToolkit.Mvvm.8.4.0/License.md` | 1158 | `651997EF19DBB9ECB8DD660DBC86444A65AF21F18CFE08DAA69BF201CD5CFDFC` |
| `ThirdPartyLicenses/CommunityToolkit.Mvvm.8.4.0/ThirdPartyNotices.txt` | 8600 | `7774F8B0AB66BFB47EC154CE24A2D6D574FEBC44A74D940B8A037D1C56756163` |
| `ThirdPartyLicenses/Microsoft.Extensions.Configuration.10.0.0/THIRD-PARTY-NOTICES.TXT` | 78041 | `6D15E10A101C6BFFF2AB4429ED061BF76C456FC4B23AD6B03E0D0F8377148A21` |
| `ThirdPartyLicenses/Microsoft.Extensions.Configuration.Abstractions.10.0.0/THIRD-PARTY-NOTICES.TXT` | 78041 | `6D15E10A101C6BFFF2AB4429ED061BF76C456FC4B23AD6B03E0D0F8377148A21` |
| `ThirdPartyLicenses/Microsoft.Extensions.Configuration.Binder.10.0.0/THIRD-PARTY-NOTICES.TXT` | 78041 | `6D15E10A101C6BFFF2AB4429ED061BF76C456FC4B23AD6B03E0D0F8377148A21` |
| `ThirdPartyLicenses/Microsoft.Extensions.DependencyInjection.10.0.0/THIRD-PARTY-NOTICES.TXT` | 78041 | `6D15E10A101C6BFFF2AB4429ED061BF76C456FC4B23AD6B03E0D0F8377148A21` |
| `ThirdPartyLicenses/Microsoft.Extensions.DependencyInjection.Abstractions.10.0.0/THIRD-PARTY-NOTICES.TXT` | 78041 | `6D15E10A101C6BFFF2AB4429ED061BF76C456FC4B23AD6B03E0D0F8377148A21` |
| `ThirdPartyLicenses/Microsoft.Extensions.Primitives.10.0.0/THIRD-PARTY-NOTICES.TXT` | 78041 | `6D15E10A101C6BFFF2AB4429ED061BF76C456FC4B23AD6B03E0D0F8377148A21` |
| `ThirdPartyLicenses/Microsoft.NET.ILLink.Tasks.10.0.10/THIRD-PARTY-NOTICES.TXT` | 78041 | `6D15E10A101C6BFFF2AB4429ED061BF76C456FC4B23AD6B03E0D0F8377148A21` |
| `ThirdPartyLicenses/Microsoft.NETCore.App.Runtime.win-x64.10.0.10/LICENSE.TXT` | 1139 | `D7A68596AB69B06F51CA278A6545148E4269A9381C26D597C13DF5D88E08CF5B` |
| `ThirdPartyLicenses/Microsoft.NETCore.App.Runtime.win-x64.10.0.10/THIRD-PARTY-NOTICES.TXT` | 78041 | `6D15E10A101C6BFFF2AB4429ED061BF76C456FC4B23AD6B03E0D0F8377148A21` |
| `ThirdPartyLicenses/Microsoft.Web.WebView2.1.0.3719.77/LICENSE.txt` | 1487 | `0AF8F1B807512AAE39C2AC1AA4D0CAE65CABECB6FD554B8439A5162A0D6ECA55` |
| `ThirdPartyLicenses/Microsoft.Web.WebView2.1.0.3719.77/NOTICE.txt` | 3894 | `106423785C5B7EBA0A8E61D1837F2132E9C828E20AD530F565D981C1DF60DD90` |
| `ThirdPartyLicenses/Microsoft.Windows.AI.MachineLearning.2.1.74/license.txt` | 13996 | `66395F8CB219087FAE2BD025010BD9076B736C14F03B48F20295471C0C376814` |
| `ThirdPartyLicenses/Microsoft.Windows.AI.MachineLearning.2.1.74/ThirdPartyNotices.txt` | 331175 | `FB0AF774B4D7CFFC5B9D046F2AAEADE2F37DF2F80ABF8033C95DFFFCC77A8866` |
| `ThirdPartyLicenses/Microsoft.Windows.SDK.BuildTools.MSIX.1.7.251221100/NOTICE.txt` | 2061 | `F09EBF5DE6A5C8E7EBC900BB2E52AF2F75E13610A1CF91E1514530E448D956C3` |
| `ThirdPartyLicenses/Microsoft.Windows.SDK.BuildTools.MSIX.1.7.251221100/sdk_license.txt` | 33483 | `A7A5C7E7FF998558983D6ACA2702117C328AEB0C6404D298CB275F5623C6FD13` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.2.3.1/license.txt` | 12637 | `5B11E6347756E40FE0274BC08C97F89201B94F0D50181A09A00F1F4740840501` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.2.3.1/NOTICE.txt` | 340893 | `572B43D41DEA717DAE7DC5DE69ACB20A74DF025E8F5A3C0AA6F7BCA02615E23C` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.AI.2.3.4/license.txt` | 12637 | `5B11E6347756E40FE0274BC08C97F89201B94F0D50181A09A00F1F4740840501` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.Base.2.0.4/license.txt` | 12637 | `5B11E6347756E40FE0274BC08C97F89201B94F0D50181A09A00F1F4740840501` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.Base.2.0.4/NOTICE.txt` | 340893 | `572B43D41DEA717DAE7DC5DE69ACB20A74DF025E8F5A3C0AA6F7BCA02615E23C` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.DWrite.2.1.0/license.txt` | 12637 | `5B11E6347756E40FE0274BC08C97F89201B94F0D50181A09A00F1F4740840501` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.Foundation.2.3.5/license.txt` | 12637 | `5B11E6347756E40FE0274BC08C97F89201B94F0D50181A09A00F1F4740840501` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.InteractiveExperiences.2.1.3/license.txt` | 12637 | `5B11E6347756E40FE0274BC08C97F89201B94F0D50181A09A00F1F4740840501` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.ML.2.1.74/license.txt` | 14033 | `656AAB74C15AA9F9964BCDCC993EB2755CBDB4822D5E0E3BC61D2E281897F758` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.ML.2.1.74/ThirdPartyNotices.txt` | 331175 | `FB0AF774B4D7CFFC5B9D046F2AAEADE2F37DF2F80ABF8033C95DFFFCC77A8866` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.Runtime.2.3.1/license.txt` | 12637 | `5B11E6347756E40FE0274BC08C97F89201B94F0D50181A09A00F1F4740840501` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.Runtime.2.3.1/NOTICE.txt` | 340893 | `572B43D41DEA717DAE7DC5DE69ACB20A74DF025E8F5A3C0AA6F7BCA02615E23C` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.Widgets.2.0.5/license.txt` | 12637 | `5B11E6347756E40FE0274BC08C97F89201B94F0D50181A09A00F1F4740840501` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.WinUI.2.3.0/license.txt` | 11503 | `C2F1D9EFBA5864366335C007D51CAB1D5B07005E9F1A67D7CA90B7B2A01FD615` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.WinUI.2.3.0/NOTICE.txt` | 2061 | `E25393C0D340A1821827B093FA4DBBFCCCD8FEB7BF769E7FA773E3955CD5314B` |
| `ThirdPartyLicenses/Microsoft.WindowsAppSDK.WinUI.2.3.0/tools/NOTICE.txt` | 638 | `4A9321FD173EBF4E8BBC79130794D4C3FE233ACCCBF7DB8765E0ADDB8FBFB8CA` |
| `ThirdPartyLicenses/System.Numerics.Tensors.9.0.0/LICENSE.TXT` | 1139 | `D7A68596AB69B06F51CA278A6545148E4269A9381C26D597C13DF5D88E08CF5B` |
| `ThirdPartyLicenses/System.Numerics.Tensors.9.0.0/THIRD-PARTY-NOTICES.TXT` | 75640 | `40686C6447A7D5B5D3693068E4571B5F483D7ED335AEEE773EF662440DE4C5D5` |

## Components shipping no license file

These packages carry no LICENSE/NOTICE file of their own. Their terms are the declared
license expression in the table above.

- Microsoft.Extensions.Configuration.FileExtensions 2.0.0 — url: https://raw.githubusercontent.com/aspnet/Home/2.0.0/LICENSE.txt
- Microsoft.Extensions.FileProviders.Abstractions 2.0.0 — url: https://raw.githubusercontent.com/aspnet/Home/2.0.0/LICENSE.txt
- Microsoft.Extensions.FileProviders.Physical 2.0.0 — url: https://raw.githubusercontent.com/aspnet/Home/2.0.0/LICENSE.txt
- Microsoft.Extensions.FileSystemGlobbing 2.0.0 — url: https://raw.githubusercontent.com/aspnet/Home/2.0.0/LICENSE.txt
- Microsoft.Windows.SDK.BuildTools 10.0.28000.1-RTM — url: https://aka.ms/WinSDKLicenseURL
- Microsoft.Windows.SDK.NET.Ref 10.0.26100.57 — url: https://aka.ms/WinSDKLicenseURL
- NetEscapades.Configuration.Yaml 3.1.0 — expression: MIT
- YamlDotNet 16.3.0 — expression: MIT

