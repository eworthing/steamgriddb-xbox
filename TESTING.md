# Testing

```powershell
.\run-tests.ps1
```

About a second for the whole suite. No packaging, no deployment, no Developer Mode.

## Why the tests are not a UWP project

The app is an `AppContainerExe`. The obvious way to test it is a UWP unit-test app, which means
building and deploying an MSIX for every run - slow enough that nobody runs the tests, which is the
same as not having them.

They are not needed. Everything under `Services\` uses only WinRT APIs that also project onto desktop
.NET, and those APIs behave the same outside an app container as in. `SmokeTests` asserts the two
facts the rest of the suite rests on: that `StorageFolder` works on an ordinary directory, and that a
missing file throws `FileNotFoundException` - the exact type the backup and restore paths branch on.
If a future Windows SDK changes either, those two tests fail first and loudly.

The one API that genuinely needs an app container is `ApplicationData.Current`. Two files touch it,
`AppliedArtworkStore` and `FixLog`, and both now take the folder as a settable property that defaults
to the real one.

## How the app's code gets into the test project

Linked, not referenced:

```xml
<Compile Include="..\SteamGridDB.Xbox\Services\**\*.cs" Link="AppSource\Services\..." />
```

Referencing an `AppContainerExe` from a desktop project is awkward, and the classes under test are
`internal` - compiling them in makes them visible without the app carrying an `InternalsVisibleTo`
purely for the tests' benefit.

Two consequences worth knowing:

- **The app project still needs its own `<Compile Include>` entry for every new file.** It is a
  legacy csproj and cannot glob. The test project globs, so it picks new service files up on its own -
  which means a file can pass its tests while being missing from the app build. The app build catches
  it immediately, but the error is confusing if you are not expecting it.
- **Linked files need `using System;` for WinRT awaits.** The extension method that makes
  `await someIAsyncOperation` compile lives there, and UWP gets it implicitly. Without it, every await
  in the file fails to build in the test project only.

## What is not covered

- **`PrimaryWidget.xaml.cs`.** It binds to `Windows.UI.Xaml`, which has no desktop projection. This
  is the reason `ArtworkFiles` takes a `StorageFolder` and a file name instead of a `GameEntry`, and
  why `GameImages` is generic over a key selector - `GameEntry` exposes `Visibility` and
  `BitmapImage`, and taking one would have dragged those modules back inside the app container.

  The bulk operation loops themselves stay in the widget for the same reason: they iterate
  `GameEntry` and assign to it. What they *compute* - which games to visit, the progress line, the
  summary - is extracted and covered. What they *do* to the UI is not.
- **Anything over the network.** `SteamGridDbClient` and most of `StoreNameLookup` call SteamGridDB,
  GOG, Epic and Ubisoft. A test that did that would be grading their uptime. Only the pure part -
  `NormaliseGameName` - is covered.
- **Whether artwork looks right.** The ranker tests pin the ordering rules and the reasons behind
  them, not the aesthetic outcome. That is still graded by eye against a real library.

## Where the value is

`ArtworkFilesTests` is the reason this exists. Backup and restore write, rename and delete real files
in the Xbox app's own folders, and a mistake there destroys artwork the user cannot recover. Those
tests run against a throwaway directory using the real file system, because the failures worth
catching - a backup overwritten with the artwork it was meant to protect, an image deleted when its
backup was already gone - are failures of file-system semantics, and a substitute file system would
only prove that the substitute agrees with itself.

The suite has been checked against deliberate mutations: reversing the "only back up once" rule fails
`Applying_a_second_time_keeps_the_first_backup` and `Apply_reports_the_backup_that_already_existed`,
moving the saved-customisation delete ahead of the backup lookup fails
`Restore_with_no_backup_keeps_the_saved_customisation`, and making the progress counter zero-based
fails three of the `OperationReport` tests. Worth repeating for any new test that passes first time.

## Known build wrinkle, unrelated to tests

`msbuild` on the app project with bundling enabled fails in `MakeAppx`: the arm64 and x64 manifests
declare different `Dependencies`. This predates the test project - it reproduces on a clean checkout.
`deploy-dev.ps1` passes `/p:AppxBundle=Never` and is unaffected, so the packaged build path works;
only a multi-architecture bundle would need it fixed.
