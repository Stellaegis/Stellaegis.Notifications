# Third-party notices

Stellaegis.Notifications is licensed under the MIT License (see [LICENSE](LICENSE)).

Its packages contain no third-party code. They reference the packages below, which NuGet installs
alongside them, each under its own license.

| Component | Used by | License (SPDX) | Project |
| --- | --- | --- | --- |
| Stellaegis.Primitives, Stellaegis.Primitives.Hosting, Stellaegis.Primitives.AspNetCore | all packages | MIT | <https://github.com/Stellaegis/Stellaegis.Primitives> |
| Microsoft.Extensions.\*, ASP.NET Core, SignalR | all packages, through Stellaegis.Primitives.Hosting and the ASP.NET Core shared framework | MIT | <https://github.com/dotnet> |

Test-only dependencies (xUnit, ASP.NET Core TestHost, the SignalR client) are not part of any package and are
not listed here.
