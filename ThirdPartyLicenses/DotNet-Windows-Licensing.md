# .NET on Windows — licensing note for this self-contained distribution

The executable in this package is a **self-contained** .NET publish for Windows: it embeds
the .NET runtime packs listed in `LICENSE-INVENTORY.md`. Most of .NET is MIT-licensed, but
Microsoft documents that certain Windows-specific components carried by self-contained
Windows apps are licensed under the **.NET Library License** and other Microsoft terms
rather than MIT:

- .NET license information for Windows:
  <https://github.com/dotnet/core/blob/main/license-information-windows.md>
- .NET license inventory: <https://github.com/dotnet/core/blob/main/license-information.md>
- .NET Library License: <https://dotnet.microsoft.com/dotnet_library_license.htm>
- Self-contained deployment for Windows apps:
  <https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps>

The runtime packs' own `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT` are reproduced beside
this note in their per-package folders. The Windows App SDK runtime embedded by
`WindowsAppSDKSelfContained=true` is governed by the Microsoft Software License Terms in
the `Microsoft.WindowsAppSDK.*` folders.

