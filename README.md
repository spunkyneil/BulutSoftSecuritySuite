# BulutSoft Security Suite

Defensive security auditing and monitoring for Windows PCs and small business networks — local, transparent and under your control.

BulutSoft Security Suite is a Windows desktop application (.NET, WinForms) that helps small and medium-sized organizations audit and monitor the security of **their own** computers and networks. It checks system configuration, open ports, shares, local accounts and startup items, scores the findings, and produces HTML/PDF reports with remediation guidance. Fixes are applied only after user approval and can be rolled back. Optional monitoring detects suspicious events such as failed sign-ins, new USB devices and unknown network devices.

Active scans are restricted to private network addresses; external sites are only observed passively and only after ownership verification. The application collects no telemetry (see [PRIVACY.md](PRIVACY.md)).

## Download

Installers are published on the [Releases](../../releases) page (SHA-256 checksums included).

## Build

Requirements: Windows, .NET SDK 10.

```powershell
dotnet build BulutSoft.sln -c Release
dotnet test LabDenetim.Tests --filter "Category!=Integration"
.\Yayinla.ps1   # builds the installers (requires Inno Setup 6)
```

## Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).

Release binaries are built from tagged commits by GitHub Actions and submitted to SignPath for signing.

Team roles (single-maintainer project):

- Committers and reviewers: [@spunkyneil](https://github.com/spunkyneil)
- Approvers: [@spunkyneil](https://github.com/spunkyneil)

Only binaries built from this repository's source are signed. The program does not transfer any user data to the developers (see [PRIVACY.md](PRIVACY.md)). Contact: bulutsoftsecurity@info.com

## License

[MIT](LICENSE). Third-party components keep their own licenses (see `LabDenetim/Kure/*-LICENSE.txt`).

## Licensing model

The source code is open under the [MIT license](LICENSE). The ready-made, tested installers come with a 14-day trial; after the trial (or when a license expires) only *starting new scans* is restricted — sign-in, history, reports, running protections and your data are never locked or deleted. Paid licenses, support and updates are provided by BulutSoft. You are free to build the project from source yourself.
