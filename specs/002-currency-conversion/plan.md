# Implementation Plan: Currency Conversion & Cross-Currency Transfers

**Branch**: `main` | **Date**: 2026-06-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-currency-conversion/spec.md`

## Summary

Extend the .NET 8 banking backend with administrator-managed exchange rates and cross-currency
transfers. The design adds an immutable `ExchangeRate` history table with supersession links,
extends `Transfer` to snapshot source and destination amounts and currencies, and keeps transfer
processing atomic, idempotent, and fully auditable.

## Technical Context

**Language/Version**: C# 12 / .NET 8 LTS

**Primary Dependencies**:
- `Microsoft.AspNetCore` - REST API host
- `Microsoft.AspNetCore.Authentication.JwtBearer` - Bearer JWT validation and scope checks
- `Microsoft.EntityFrameworkCore` - persistence and transactions
- `Microsoft.EntityFrameworkCore.InMemory` - current development/test backing store
- `Microsoft.EntityFrameworkCore.SqlServer` - production target for filtered indexes and rowversion
- `Swashbuckle.AspNetCore` - OpenAPI generation and Swagger UI
- `FluentValidation.AspNetCore` - request validation
- `Serilog.AspNetCore` - structured JSON logging with correlation IDs

**Storage**:
- Current: EF Core InMemory for local development/tests
- Production target: SQL Server 2019+
- Additive schema changes: new `ExchangeRates` table, nullable cross-currency columns on `Transfers`,
  supporting indexes on `ExchangeRates`, `Transfers`, and `AuditLog`

**Testing**: xUnit + FluentAssertions + Moq + `Microsoft.AspNetCore.Mvc.Testing`; contract validation against `contracts/openapi.yaml`

**Target Platform**: Linux/Windows ASP.NET Core server deployment

**Project Type**: Versioned REST web service

**Performance Goals**:
- Exchange-rate creation/update p99 under 2 seconds (SC-001)
- Exchange-rate lookup p99 under 500 ms (SC-002)
- Cross-currency transfer completion p99 under 3 seconds (SC-003)
- Core transaction API p95 under 200 ms under normal load (constitution baseline)

**Constraints**:
- Monetary values remain `long` minor units; floating-point money is prohibited
- Exchange rates use `decimal(18,6)` and must be positive
- Destination amount is `floor(sourceAmount x rate)` and must be at least 1 minor unit
- Current rate uniqueness is enforced per `(SourceCurrency, TargetCurrency)` where `SupersededBy IS NULL`
- Transfers remain atomic and idempotent under the existing `Idempotency-Key` mechanism
- `Account.RowVersion` and `ExchangeRate.RowVersion` protect concurrent updates
- All rate mutations and transfer attempts create `AuditLogEntry` rows using controlled operation types
- Supported currencies are limited to `USD`, `EUR`, `GBP`, `SAR`, and `AED`

**Scale/Scope**: Single backend service; two new exchange-rate endpoints plus an extension of the existing `/v1/transfers` endpoint; four core persisted entities used by this feature (`Account`, `Transfer`, `ExchangeRate`, `AuditLogEntry`)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design - PASS*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Security by Design | PASS | All endpoints require bearer JWTs; `POST /v1/exchange-rates` additionally requires `exchange-rate:admin`; supported-currency and numeric validation are enforced at the API boundary. |
| II. Data Integrity & ACID | PASS | Exchange-rate writes are immutable inserts plus supersession update; transfers stay inside one DB transaction; idempotency key remains unique; money uses `long`, rates use fixed-point `decimal(18,6)`. |
| III. Compliance & Regulatory | PASS | No card data introduced; immutable rate history supports reconciliation; audit records retain initiator/correlation/entity references for regulatory review. |
| IV. Full Auditability | PASS | Every rate mutation and transfer attempt writes an `AuditLogEntry`; correlation IDs and structured logs preserve request traceability; controlled vocabulary is defined in `data-model.md`. |
| V. Test-First Development | PASS | Design anticipates unit tests for conversion/rate locking and contract/integration coverage for new endpoints and rejection paths. |
| VI. API Versioning | PASS | All new and updated APIs remain under `/v1`; transfer changes are additive and backward compatible. |
| VII. Simplicity & YAGNI | PASS | Uses direct pair lookup only; no derived cross-rates, event bus, or pricing feed integration in this version. |

## Project Structure

### Documentation (this feature)

```text
specs/002-currency-conversion/
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   `-- openapi.yaml
`-- tasks.md             # Phase 2 output (not created here)
```

### Source Code (repository root)

```text
src/
|-- FundTransfer.Api/
|   |-- Controllers/
|   |   |-- AccountsController.cs
|   |   |-- ExchangeRatesController.cs
|   |   `-- TransfersController.cs
|   |-- Validators/
|   |   |-- CreateAccountRequestValidator.cs
|   |   |-- CreateTransferRequestValidator.cs
|   |   `-- SetExchangeRateRequestValidator.cs
|   `-- Program.cs
|-- FundTransfer.Application/
|   |-- Constants/
|   |-- DTOs/
|   |-- Interfaces/
|   |   |-- IAccountRepository.cs
|   |   |-- ITransferRepository.cs
|   |   |-- IAuditLogRepository.cs
|   |   `-- IExchangeRateRepository.cs
|   |-- Models/
|   |   |-- Account.cs
|   |   |-- Transfer.cs
|   |   |-- AuditLogEntry.cs
|   |   `-- ExchangeRate.cs
|   `-- Services/
|       |-- AccountService.cs
|       |-- TransferService.cs
|       `-- ExchangeRateService.cs
`-- FundTransfer.Infrastructure/
    `-- Persistence/
        |-- AppDbContext.cs
        `-- Repositories/
            |-- AccountRepository.cs
            |-- AuditLogRepository.cs
            |-- ExchangeRateRepository.cs
            `-- TransferRepository.cs

tests/
|-- FundTransfer.UnitTests/
|-- FundTransfer.IntegrationTests/
`-- FundTransfer.ContractTests/
```

**Structure Decision**: Keep the existing three-layer .NET solution (`Api` -> `Application` -> `Infrastructure`). The feature remains inside the current bounded context and adds one new persistent entity plus additive transfer fields without introducing additional services.

## Complexity Tracking

| Pattern | Why Needed | Simpler Alternative Rejected Because |
|---------|------------|--------------------------------------|
| Immutable `ExchangeRate` rows with supersession chain | Preserves exact history, supports rate locking by ID, and avoids silent overwrites | Updating one mutable row would break FR-005/FR-010 and make transfer reconciliation ambiguous |
| Filtered unique index on current rates | Guarantees only one active rate per directed pair while preserving history | Application-only checks are race-prone under concurrent admin updates |