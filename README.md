# Delivery Quotes - Final Project (Introduction to Functional Programming)

| | |
|---|---|
| Student | Akhmet Amira, group IT-2514 (individual project) |
| Repository | https://github.com/amira928/IT-2514-FP-Final |
| Branch | `main` |
| Last code commit | `d08a5a1` (source and tests; README and reports were added after it) |

## 1. Problem, users, goals
A small delivery company needs to price delivery requests, accept orders from customers within their credit limit, and plan the dispatch with a service fee. Users: a dispatcher (workflows) and a manager or analyst (reports). The application is a console program. Goal: show the functional techniques of the course in one coherent domain.

## 2. Domain models
| Model | Purpose |
|---|---|
| `RawRequest`, `ValidatedRequest`, `Quote` | delivery request (Id, distance 1-50 km) and its price |
| `Customer` (from `RawCustomer`) | customer with tier (Regular or Gold) and credit limit, created only by `Customer.Create` |
| `ApprovedOrder`, `OrderBook` | approved order of a customer; immutable list of orders |
| `DispatchPlan` | price + service fee + total for an approved order |

## 3. Workflows (minimum for one student: 3)
| # | Workflow | Input | Business rules | Result | Code |
|---|---|---|---|---|---|
| 1 | Quote and confirm | request | distance 1-50 km; state order Requested, Quoted, Confirmed | confirmed quote | `QuoteWorkflow.cs`, `QuoteState.cs` |
| 2 | Place an order | customer id + request id | customer and request exist, request is valid, Gold discount, total spent within credit limit, no duplicate order | new `OrderBook` | `OrderWorkflow.cs` |
| 3 | Dispatch plan | approved order + service type | Express only up to 20 km; fee percent by service; fee from the native C library | `DispatchPlan` | `DispatchWorkflow.cs` |

Example output (`dotnet run --project DeliveryQuotes.App`):
```
C001 + R002: APPROVED, list 1500, final 1350
C002 + R004: REJECTED - R004: total 5500 exceeds the credit limit 2000 of customer C002.
R002 Standard: price 1350 + fee 135 = total 1485
R004 Express: REJECTED - R004: Express delivery is limited to 20 km, but the distance is 50 km.
```

## 4. Reports
1. Premium quotes (manager): Where, extension ordering, Select - `Reports.PremiumQuotes`.
2. Totals by distance band (analyst): GroupBy, Count and Sum, ordering with tie-breaker - `Reports.TotalsByBand`.

## 5. Requirement map
| Requirement | Where |
|---|---|
| Pure core, immutable values | `Pipeline.cs`, `OrderWorkflow.cs`, `DispatchWorkflow.cs`, `OrderBook.cs` (records, no I/O) |
| Pattern matching | `QuoteWorkflow.Describe`, `Eligibility` cases in `DispatchWorkflow.cs`, `switch` in `Program.cs` |
| Pipeline with 3 named stages | `Pipeline.NormalizeRequests`, `CalculateQuotes`, `BuildSummary` |
| Higher-order function changing a rule | `pricePolicy` and `discount` delegates in `OrderWorkflow.Place`; `feeCalculator` in `DispatchWorkflow.Plan` |
| Two policies, same input, different result | Standard vs Express (`PricePolicies.cs`); gold 10% vs gold 20% (`DiscountPolicies.cs`) |
| Validated factories | `ValidatedRequest.Create`, `Customer.Create` (private constructors), `ApprovedOrder` (internal constructor) |
| Safe operation sequence | `QuoteState` (confirm before calculate is rejected); `OrderBook.Add` returns a new book, a failed add changes nothing |
| Result, Map, Bind | `Result.cs`; `OrderWorkflow.Place`, `DispatchWorkflow.Plan`, `CustomerCatalog.Load` |
| Missing lookups | `Maybe.cs`, `RequestLookup.FindById`, `CustomerCatalog.FindById` |
| Boundary exceptions | `CsvLoader`, `FileBoundary`, `ReportExporter`, `NativeFeeAdapter` |
| Extension methods | `QuoteExtensions.cs` (`OrderByMostExpensive`, `DistanceBand`) |
| Currying and partial application | `Scoring.CreateScore(10)(5)`, `DiscountPolicies.CreateTierDiscount(10)` |
| Lazy processing | `LazyQueries.LazyQuotes` (`yield return`), `ToArray()` as the snapshot point |
| Persistence | load `data/requests.csv` and `data/customers.csv`; export `output/band_totals.csv` |
| Native C library | `native/delivery_native.c`, `NativeFeeAdapter.cs` |

## 6. Resource ownership and data retention
Exporters and `LineSource` own their writers and readers and release them with `using`, also when an error happens. Datasets are materialised once (`ToArray`), nothing is cached in static fields. Exported files in `output/` are overwritten on every run. Sample data contains only invented names.

## 7. Native C library
Function `int32_t service_fee(int32_t price, int32_t percent)` returns price * percent / 100 (rounded down); invalid input returns -1.
Platform: Windows x64. Compiler: gcc (MinGW-W64). Build: `native\build.bat` creates `native\runtime\win-x64\delivery_native.dll` (compiler setup: `native\setup_gcc.bat`). The compiled DLL is included and `DeliveryQuotes.App.csproj` copies it next to the application. Loading: `DllImport("delivery_native")`. A missing DLL, wrong format, missing entry point or result -1 becomes a `Result` failure in `NativeFeeAdapter`. Tests: real call (`Dispatch_RealNativeCall_CalculatesTheFee`) and controlled failure (`Dispatch_NativeLoadFailure_IsAControlledFailure`).

## 8. Build, run, test
Prerequisite: .NET SDK 10.0.401, Windows x64. Run from the folder that contains `DeliveryQuotes.slnx` (data paths are relative to it):
```
dotnet build
dotnet run --project DeliveryQuotes.App
dotnet test
```

## 9. Tests
81 tests: 56 from Assignments 5-8 and 25 new in `FinalProjectTests.cs`. Covered: pure functions and validation, boundaries, empty and missing cases, safe ordering, failure propagation, reports, configured functions, lazy processing, native call and native failure, missing and malformed files, two complete workflows. Last result: 81 passed, 0 failed.

## 10. Limitations
Console application only; demo orders are listed in `Program.cs`; the native DLL is for Windows x64 only; prices are integers; AI assistance was used and is described in the individual report.
