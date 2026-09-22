# Project22 — Laboratory Order Intake and Input Validation Service

## 1. Project overview

A C# class library that validates a single laboratory order submitted as a
JSON string. `OrderIntakeService.Process(string json)` returns an
`OrderResult` describing one of three outcomes:

- **Accepted** — the order satisfies every rule; the result carries a typed
  `LabOrder` and no errors.
- **Rejected** — the order breaks one or more rules; the result carries no
  order and every error found (field, code, and a short message).
- **Rejected (malformed input)** — the JSON is broken or isn't shaped like an
  order; the result carries a single `MALFORMED_INPUT` error with field `$`.

The service never throws an unhandled exception for bad input.

## 2. Project structure

```
Project22/
├── src/
│   └── OrderIntake/
│       ├── OrderIntake.csproj
│       ├── OrderIntakeService.cs   # public entry point: Process(string json)
│       ├── RawOrderReader.cs       # JSON parsing + JSON-type checking
│       ├── RawOrder.cs             # untyped intermediate shape
│       ├── LabOrder.cs             # typed, accepted order
│       ├── OrderResult.cs          # Accepted/Rejected outcome
│       ├── ValidationError.cs      # field + code + message
│       ├── SpecimenType.cs         # enum: Blood, Urine, Tissue, Saliva
│       ├── OrderStatus.cs          # enum: Accepted, Rejected
│       └── Priority.cs             # enum: Routine, Urgent
├── tests/
│   └── OrderIntake.Tests/
│       └── OrderIntakeServiceTests.cs
├── Project22.slnx
└── README.md
```

## 3. Prerequisites

- .NET SDK 10.0.401 (target framework `net10.0`)

## 4. Build

From the repository root:

```bash
dotnet build
```

## 5. Test

From the repository root:

```bash
dotnet test
```

## 6. Design summary

- **JSON parsing (`RawOrderReader`)** — parses the input with
  `System.Text.Json.JsonDocument` and reads only the seven recognized
  fields. For each one: missing or JSON `null` becomes a C# `null`
  (business rules decide what to do with that); present but the wrong JSON
  type (e.g. `{"orderId": 123}`) is treated as malformed input. Unknown
  fields (e.g. `senderNote`) are never read, so their type/value never
  affects the result.
- **Malformed-input handling** — a single `MALFORMED_INPUT` error (field
  `$`) is returned when: the input is null/empty/whitespace, the JSON
  doesn't parse, the root isn't a JSON object, or a recognized field has an
  incompatible JSON type. An empty object `{}` is well-formed, so it falls
  through to normal field validation instead (producing `REQUIRED` errors).
- **Validation (`OrderIntakeService`)** — each field is validated
  independently against its own rule set (see below).
- **Error collection** — every validator always runs, and every error is
  appended to one list; the service never stops at the first failure, so a
  `Rejected` result reports the complete set of problems in one pass.
- **Normalization** — `specimenType` and `priority` are matched
  case-insensitively against a fixed lookup table and converted to a C#
  `enum` (`SpecimenType`, `Priority`), so a valid value is always stored in
  its fixed form regardless of the input casing.
- **Accepted `LabOrder` creation** — only built once zero errors have been
  collected; holds the two IDs and specimen ID as strings, `SpecimenType`
  and `Priority` as enums, `CollectionDate` as a real `DateOnly`, and
  `RequestedTests` as a list of strings in the sender's original casing.
- **`OrderResult` creation** — two static factories, `Accepted(order)` and
  `Rejected(errors)`, keep the two outcomes mutually exclusive by
  construction (an accepted result can't carry errors; a rejected one can't
  carry an order).

## 7. Validation rules

| Field | Rule | Error codes |
|---|---|---|
| `orderId`, `patientId`, `specimenId` | Required, not blank, max 20 characters | `REQUIRED`, `MAX_LENGTH` |
| `specimenType` | Required; one of Blood/Urine/Tissue/Saliva, case-insensitive; normalized | `REQUIRED`, `INVALID_VALUE` |
| `priority` | Required; one of Routine/Urgent, case-insensitive; normalized | `REQUIRED`, `INVALID_VALUE` |
| `collectionDate` | Required; exactly `yyyy-MM-dd`; a real calendar date; not after today | `REQUIRED`, `INVALID_FORMAT`, `FUTURE_DATE` |
| `requestedTests` | Required; at least one item; no empty items; no case-insensitive duplicates | `REQUIRED`, `INVALID_VALUE`, `DUPLICATE` |
| Whole message | Must be a JSON object whose recognized fields have compatible JSON types | `MALFORMED_INPUT` |

## 8. Assumptions

The problem statement explicitly leaves some decisions to the implementer.
This is what was chosen, and why:

- **Empty `requestedTests` array (`[]`) → `REQUIRED`.** The spec groups "at
  least one item" under the same rule as "Required", so a present-but-empty
  list is treated the same as a missing one.
- **An empty/whitespace test name inside a non-empty list → one
  `INVALID_VALUE` error** (not one per bad item), to keep the error list
  proportionate — the spec only requires "one DUPLICATE error is enough"
  for duplicates, and the same proportionality was applied here.
- **Values are not trimmed.** `orderId`, etc. are validated and stored
  exactly as received; whitespace-only values fail as blank via
  `REQUIRED`, but surrounding whitespace on an otherwise valid value is
  preserved rather than stripped.
- **`collectionDate` is compared against `DateTime.Today`** (server-local
  date) to decide `FUTURE_DATE`.
- **Field names are matched case-sensitively** (`orderId`, not `OrderId`),
  matching the exact casing used in the problem statement's example JSON.
- **An array item of the wrong JSON type inside `requestedTests`** (e.g. a
  number) is treated as an incompatible recognized-field type, i.e.
  `MALFORMED_INPUT`, consistent with how a wrong-typed scalar field (like
  `{"orderId": 123}`) is handled.

## 9. Limitations / out of scope

As specified, this project intentionally does not include: a console app,
web API, database, UI, authentication, HTTP, cloud services, external
APIs, queues, load testing, production logging, or a validation framework.
It is a single class library plus its test project.

## 10. AI usage

AI assistance (Claude) was used while building this service, with all
output reviewed before being kept.

One concrete example: an AI-drafted test for an empty JSON object (`{}`)
initially asserted only `result.Errors.Count > 1` — enough to pass, but not
strong enough to prove *which* errors were actually produced. On review,
that assertion was identified as too weak and was checked against the
seven fields that should each report `REQUIRED`, then replaced with an
exact match on the full expected set of `(field, code)` pairs. The
weaker version was the AI's first draft; the stronger one is what's
submitted.

