# .NET on Windows — licensing note for this self-contained distribution

The executable in this package is a **self-contained** .NET publish for Windows: it embeds
the .NET runtime and libraries. Most of .NET is MIT-licensed, but Microsoft documents that
certain Windows-specific components carried by self-contained Windows apps are licensed
under the **.NET Library License** and other Microsoft terms rather than MIT:

- .NET license information for Windows:
  <https://github.com/dotnet/core/blob/main/license-information-windows.md>
- .NET Library License: <https://dotnet.microsoft.com/dotnet_library_license.htm>

This note exists so the package does not misstate those components as MIT. The Windows App
SDK runtime embedded by `WindowsAppSDKSelfContained=true` is governed by the Microsoft
Software License Terms reproduced in `WindowsAppSDK-2.3.1-License.txt` in this folder.
