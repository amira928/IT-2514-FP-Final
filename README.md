# Delivery Quotes – Assignment 8: Extension Methods and LINQ

## Team
| Name | Group | Contribution |
|---|---|---|
| Akhmet Amira | IT-2514 | Whole Assignment 8 stage: extensions (`QuoteExtensions.cs`), reports (`Reports.cs`), currying (`Scoring.cs`), lazy and resource code (`LazyQueries.cs`, `LineSource.cs`, `ReportExporter.cs`), native C library (`native/`), adapter (`NativeFeeAdapter.cs`), demonstrations in `Program.cs`, 19 new tests (`Assignment8Tests.cs`), this README. |

## 1. Input and domain rules
Input: `data/requests.csv` (`Id,Distance`). Distance is an integer 1-50 km. Price: Standard = 500 + 100 per km. Earlier pipeline, Result/Maybe and CSV boundary from Assignments 5-7 are kept.

## 2. Custom extension methods (Task 1)
| Method | Receiver -> return | Behaviour | Why on this type |
|---|---|---|---|
| `OrderByMostExpensive` | `IEnumerable<Quote>` -> `IEnumerable<Quote>` | price descending, then Id; does not change the source | it compares quotes with each other, so it belongs to a sequence of quotes |
| `DistanceBand` | `Quote` -> `string` | Short (<=5 km), Medium (<=20), Long | it depends only on one quote's distance |

Both are used in `Reports.cs`.

## 3. LINQ reports (Task 2)
Both reports use quotes calculated from the CSV data.

| | Report 1: premium quotes | Report 2: totals by distance band |
|---|---|---|
| User | manager | analyst |
| Filter | price >= minPrice (1000) | none |
| Stages | Where, extension order, Select | GroupBy, Select (Count, Sum), OrderByDescending, ThenBy |
| Output | Id, km, price | band, count, total |
| Expected (Standard) | R004 5500, R002 1500 | Long 1/5500, Medium 1/1500, Short 2/1300 |

Ordering is deterministic: ties are broken by Id (report 1) and band name (report 2). Reports contain no I/O; printing is in `Program.cs`, export is in `ReportExporter`.

## 4. Currying and partial application (Task 3)
`Scoring.CreateScore : thresholdKm -> weightPerKm -> Quote -> int`; score = price + max(0, km - threshold) * weight.
`relaxed = CreateScore(10)(5)`, `strict = CreateScore(2)(20)`; the configured function is passed into `Scoring.Rank`. Same data, different result: R004 = 5700 (relaxed) vs 6460 (strict).

## 5. Deferred execution (Task 4)
`LazyQueries.LazyQuotes` uses `yield return`: nothing runs when it is created, validation and pricing run during iteration. `ToArray()` is the materialisation point. Iterating the snapshot again does not recalculate; iterating the lazy query again recalculates. The test counts policy calls to prove this.

## 6. Resource ownership (Tasks 5-6)
| Resource | Owner | Release |
|---|---|---|
| `TextReader` in `LineSource.Iterate` | `Iterate` (`using var`) | end of iteration, failure or early stop |
| `StreamWriter` in `ReportExporter` | `ExportBandTotals` (`using`) | end of the block, also on exception |
| `File.ReadAllLines` in `CsvLoader` | .NET (convenience API opens and closes the file) | inside the call |

Export writes a `.tmp` file and then moves it, so the target is never half-written; the temp file is removed in `finally`.
Retention note: datasets are materialised in `Pipeline` (arrays), `LazyQuotes(...).ToArray()` and `LineSource.Snapshot`. A lazy query keeps its source and policy until it is finished; I keep a snapshot where data is reused. No caches, static collections or event subscriptions are used. Streaming uses less memory but keeps the file open; a snapshot closes the file at once but holds all data.

## 7. Native C library (Tasks 7-9)
Source: `native/delivery_native.c`, header `native/delivery_native.h`.

| | |
|---|---|
| Function | `int32_t service_fee(int32_t price, int32_t percent)` |
| Meaning | service fee = price * percent / 100, rounded down; money in integer smallest currency units |
| Valid range | price 0-1 000 000, percent 0-100 |
| Invalid input | returns -1 |
| Manual example | `service_fee(1500, 10) = 150` |

C# binding: `NativeMethods` (`DllImport`, `EntryPoint = "service_fee"`, `Cdecl`). Adapter `NativeFeeAdapter.ServiceFee` validates inputs before the call and turns `DllNotFoundException`, `EntryPointNotFoundException`, `BadImageFormatException` and a -1 result into `Result` failures. It is used in `Program.cs` (demonstration 9) and in the workflow test.

Platform: Windows x64. Compiler: gcc 16.2.0 (MinGW-W64 x86_64-ucrt-posix-seh, WinLibs), target `x86_64-w64-mingw32`.
Build: `native\build.bat` creates `native\runtime\win-x64\delivery_native.dll` (needs MinGW in `C:\mingw64`, see `native\setup_gcc.bat`). The compiled DLL is included. `DeliveryQuotes.App.csproj` copies it next to the application on build, so the loader finds it.

Difficulty: my Windows user name has Cyrillic letters, and gcc failed with `Illegal byte sequence` (it could not read its own libraries from that path). Solution: copy MinGW to `C:\mingw64`, use relative paths and set `TMP`/`TEMP` to `C:\gcc_tmp` in `build.bat`.

## 8. Requirements map
| Requirement | Where |
|---|---|
| Two extensions | `QuoteExtensions.cs`, used in `Reports.cs` |
| Two LINQ reports | `Reports.cs` |
| Currying + two partial applications | `Scoring.cs`, `Program.cs` |
| Deferred execution | `LazyQueries.cs` |
| Resources and disposal | `LineSource.cs`, `ReportExporter.cs` |
| Native library and adapter | `native/`, `NativeFeeAdapter.cs` |
| Integration and export | `Program.cs`, `ReportExporter.cs` |

## 9. Tests
Total 56 (37 earlier + 19 new in `Assignment8Tests.cs`); latest run: 56 passed, 0 failed.

| Area | Tests |
|---|---|
| Extensions | order and no mutation; band boundaries 5, 6, 20, 21 |
| Reports | normal, no matches, empty, boundary, ties, grouping and totals |
| Configured functions | relaxed vs strict on the same input |
| Deferred | 0 calls before iteration, 2 after `ToArray`, snapshot reuse does not recalculate |
| Resources | disposed after full enumeration, early stop and failure |
| Native | real call = 150; invalid arguments rejected before the call; load failure -> `Failure` |
| Workflows | normal (CSV, report, export, native fee 220); failure (missing and malformed file, no output created) |
| File boundaries | missing and malformed file tests from Assignment 7 |

File tests use temporary folders and delete them.

## 10. Build, run, test
Prerequisite: .NET SDK 10.0.401, Windows x64. Run from the folder with `DeliveryQuotes.slnx`:

```
dotnet build
dotnet run --project DeliveryQuotes.App
dotnet test
```

`dotnet run` prints the earlier demonstrations and then native fee (9), both reports, the two scoring functions, lazy quotes, file lines and the export to `output/band_totals.csv`.
Failure demonstrations: missing file and malformed file (`data/malformed.csv`) are shown in the same output.