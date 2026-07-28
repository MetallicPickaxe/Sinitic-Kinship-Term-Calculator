# Third-Party Notices

This distribution of **Sinitic Kinship Term Calculator** contains, or was built with, the
following third-party components. Full license texts are available at the linked upstream
projects; the project's own license is in `LICENSE`.

## Bundled in the executable (self-contained publish)

| Component | License | Source |
|---|---|---|
| .NET Runtime and libraries | MIT | <https://github.com/dotnet/runtime> |
| Windows App SDK (WinUI 3) | MIT | <https://github.com/microsoft/WindowsAppSDK> |
| CommunityToolkit.Mvvm | MIT | <https://github.com/CommunityToolkit/dotnet> |
| Microsoft.Extensions.Configuration (+ Binder, DI) | MIT | <https://github.com/dotnet/runtime> |
| NetEscapades.Configuration.Yaml | MIT | <https://github.com/andrewlock/NetEscapades.Configuration> |
| YamlDotNet (transitive) | MIT | <https://github.com/aaubry/YamlDotNet> |

## Data provenance

| Component | License | Notes |
|---|---|---|
| mumuy/relationship | MIT | The kinship-term corpus this project's lexicon layers and verification oracle derive from; see `ATTRIBUTION.md` for the exact relationship (data cited, structure re-derived) and `LICENSE` for the reproduced MIT notice. <https://github.com/mumuy/relationship> |

The regional lexicon layers (`Lexicon\*.yaml`) carry per-file header notes where their
contents overlap mumuy's collection.
