# Migrating a Connector/Enricher to Multi-Version Targeting

This document tracks the migration of `CluedIn.Enricher.GoogleImages` from a single-version build
to the multi-version targeting pattern, part of a larger effort that has already migrated
`CluedIn.Connector.Dataverse.V2`, `CluedIn.Enricher.Gleif`, `CluedIn.Enricher.OpenCorporates`,
`CluedIn.Enricher.Permid`, `CluedIn.Enricher.Brreg`, `CluedIn.Enricher.KnowledgeGraph`,
`CluedIn.Enricher.ClearBit`, `CluedIn.Enricher.CompanyHouse`, `CluedIn.Enricher.CVR`, and
`CluedIn.Enricher.DuckDuckGo`. Written as work lands, modeled on those prior docs.

Branch: `feature/multi-version-targeting` (off `develop`).

---

## Overview

| CluedIn version | .NET TFM | Package suffix |
|---|---|---|
| 4.7.0 | net6.0 | `.470` |
| 4.8.0 | net6.0 | `.480` |
| 5.0.0-beta.* | net10.0 | `.500` |

Verified independently for this repo's own feeds (not assumed from a prior doc): `5.0.0-*`
resolves to a `5.0.0-beta.*` prerelease, matching every other repo migrated so far. 4.6.0 excluded
— no evidence this small ExternalSearch provider needs it.

---

## Step 1 — Pipeline template (`azure-pipelines.yml`)

Status: **Done**

Switched from `crawler.build.yml` (with an explicit `UseDotNet@2 8.0.x` install — stale, the repo
already builds net10.0 via `global.json`) to `crawler.build.jobs.yml`. Also removed a dead
`createIntegrationEnvironmentScriptFilePath: './build/integration-test.ps1'` reference — that
script doesn't exist in this repo (same dead reference GoogleMaps' doc found and removed in its own
repo).

---

## Step 2 — `Directory.Build.props`

Status: **Done**

Honours `CluedInMultiVersionTargetFramework` (net10.0 local fallback), derives
`CLUEDIN_V47`/`V48`/`V50` `DefineConstants`, pins `LangVersion` to `13.0` up front (a trap several
prior repos in this effort hit on net6.0).

---

## Step 3 — `Packages.props`

Status: **Done**

Renamed from lowercase `packages.props`. `_CluedIn` guarded. Test package versions
(`Microsoft.NET.Test.Sdk`, `xunit`/`xunit.v3`, `xunit.runner.visualstudio`,
`AutoFixture.Xunit2`/`Xunit3`) split conditionally on `CLUEDIN_V50`. `CluedIn.Testing.Base`/
`CluedIn.CrawlerIntegrationTesting` switched to the version-suffixed package IDs
(`.470`/`.480`/`.500` — confirmed all three exist on the develop feed before wiring up) via a new
`_CluedInPackageSuffix` property.

**Bug hit while authoring this (not in any prior doc — worth flagging generally):** first attempt
put the `_CluedInPackageSuffix` property assignment inside an `<ItemGroup>` instead of a
`<PropertyGroup>` — valid-looking XML, but MSBuild rejects it outright: `MSB3644`... no,
`error MSB4232: Items that are outside Target elements must have one of the following operations:
Include, Update, or Remove.` The error pointed at the exact line, straightforward once seen, but
worth double-checking the containing element type (`PropertyGroup` vs `ItemGroup`) when copying this
pattern into the next repo.

---

## Step 4 — Test projects

Status: **Done**

- `test/Directory.Build.props` stripped to just `IsTestProject` — no more unconditional
  `PackageReference`s (avoids the xunit v2/v3 clash).
- **Deleted `test/unit/Directory.Build.props`** — dead scaffold pinning ancient `Moq 4.5.30`/
  `Should 1.1.20` with hardcoded versions, but no `test/unit` csproj exists to consume it (same
  pattern GoogleMaps' and ClearBit's repos had).
- Added conditional `ItemGroup`s to
  `test/integration/Integration.Tests/ExternalSearch.GoogleImages.Integration.Tests.csproj`: xunit
  v3 + `AutoFixture.Xunit3` under `CLUEDIN_V50`, xunit v2 + `AutoFixture.Xunit2` otherwise. Also had
  to add back `Microsoft.NET.Test.Sdk`/`coverlet.msbuild`/`Moq` explicitly (previously only
  available via the now-stripped `test/Directory.Build.props`).
- Added `GlobalUsings.cs` with `#if CLUEDIN_V50` for the `AutoFixture.Xunit2`/`Xunit3` namespace
  split; also needed for `Xunit.Abstractions` — `GoogleImagesTests.cs` uses `ITestOutputHelper` via
  a bare `using Xunit;`, which only resolves under xunit v3 without an extra using.

---

## Step 5 — API compatibility audit across 4.7.0 / 4.8.0 / 5.0.0-beta.*

Status: **Done** — 0 errors, verified for real (`dotnet build`/`dotnet test`), all three legs.

**RestSharp 106-vs-114 break** (same family every enricher in this effort has hit) in
`GoogleImagesExternalSearchProvider.cs`: `Method.Get`/`Method.GET` (2 call sites) and the
`ConstructVerifyConnectionResponse` parameter type (`RestResponse`/`IRestResponse`). Fixed with
`#if CLUEDIN_V50` guards. The two `var`-inferred `client.ExecuteAsync<T>(...).Result` locals needed
no guard.

No `Nager.PublicSuffix` or other transitive-dependency break found — this provider doesn't touch
domain/TLD parsing.

`dotnet test` on the integration test project reports "No test is available" (exit code 0) — the
one test method is `[Theory(Skip = "Requires a working api key")]`, unrelated to this migration.

---

## Step 6 — Reset the semantic version (`GitVersion.yml`)

Status: **Done**

```yaml
next-version: 1.0
...
ignore:
  sha: []
  commits-before: 2026-06-20T00:00:00
```

**This repo's `GitVersion.yml` already had an `ignore:` key** (`ignore: sha: []`) — merged
`commits-before` into the existing block rather than adding a second top-level `ignore:` key, which
would silently clobber the first (valid YAML, no error, wrong result — the exact trap CompanyHouse's
and CVR's repos hit in this same effort).

Highest pre-existing tag by actual commit date (not tag-name sort order) is `4.6.2` at
`2026-06-17T17:22:57+10:00`. Padded 2 full days per the Gleif-derived lesson (GitVersion.Tool 5.9.0
parses `commits-before` using local machine time, not UTC, and fails silently if the margin is too
tight). Verified directly with the pinned tool: `FullSemVer: "1.0.0-multi-version-targeting.51"` —
confirmed `1.0.0`, not `5.0.x`.

---

## Checklist

- [x] `azure-pipelines.yml` — switched to `crawler.build.jobs.yml` with `multiVersionCluedInTargets` (4.7.0, 4.8.0, 5.0.0-beta.*); dead `integration-test.ps1` reference removed
- [x] `Directory.Build.props` — honours `CluedInMultiVersionTargetFramework`; `DefineConstants` derived; `LangVersion` pinned to 13.0
- [x] `Packages.props` — renamed from lowercase; `_CluedIn` guarded; test packages split by `CLUEDIN_V50`; `CluedIn.Testing.Base`/`CluedIn.CrawlerIntegrationTesting` switched to suffixed package IDs
- [x] `NuGet.config` — renamed from `Nuget.config`
- [x] Test projects — `test/Directory.Build.props` stripped; dead `test/unit/Directory.Build.props` deleted; conditional xunit v2/v3 ItemGroups added; `GlobalUsings.cs` added
- [x] Source — `#if CLUEDIN_V50` guards for the RestSharp 106↔114 break (`GoogleImagesExternalSearchProvider.cs`)
- [x] `GitVersion.yml` — `next-version: 1.0`; `commits-before` merged into the existing `ignore:` block, padded 2 days; verified `1.0.0` with the pinned GitVersion.Tool 5.9.0
- [x] `src`/`test` build clean (0 errors) for all three legs, verified locally via real `dotnet build`
- [x] Pushed branch and confirmed the Azure DevOps pipeline is green end-to-end — PR #29, build 151984: all three legs + integration tests + `Multi-version: publish` passed

---

## Addendum — version baseline moved from 1.0.0 to 100.0.0

Status: **Done**

The CluedIn version is now carried entirely by the package suffix (`.470`/`.480`/`.500`), not by
this repo's own `next-version` number, so that number moved again, from `1.0` to `100.0`. Reason:
repos that were previously at 4.x/5.x under the old single-version-targeting scheme would appear to
"go backwards" if their next version showed as `1.0.0` — `100.0.0` is unambiguously higher than any
prior single-version release number this repo ever had.

Unlike the original `1.0` reset, no `commits-before`/`ignore` trick is needed this time:
`next-version` only needs help overriding an existing tag when the configured value is *lower* than
that tag, and `100.0` is already higher than every pre-existing tag here. Removed the
`ignore.commits-before` line entirely (kept `ignore.sha: []`).

Verified with a real local `dotnet-gitversion` run: `MajorMinorPatch` resolves to `"100.0.0"`.
`docs/1.0.0-release-notes.md` renamed to `docs/100.0.0-release-notes.md`.
