# Implementation Plan: Fund Transfer Service

**Branch**: `master` | **Date**: 2026-06-15 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-fund-transfer/spec.md`

## Summary

Build a .NET 8 ASP.NET Core Web API that provides three financial operations: account creation,
balance retrieval, and atomic fund transfers. The service uses Entity Framework Core with an
in-memory database for development/testing and is designed to swap to SQL Server via a single
DI registration change. All monetary amounts are stored as `long` (integer minor units) to
prevent floating-point rounding. Transfers are atomic (EF Core DB transactions), idempotent
(caller-supplied idempotency key), and protected against concurrent double-spend (EF Core
optimistic concurrency via row version). Swagger UI is the primary API exploration and manual
testing interface.

## Technical Context

**Language/Version**: C# 12 / .NET 8 LTS

**Primary Dependencies**:
- `Microsoft.AspNetCore` — Web API host
- `Swashbuckle.AspNetCore` — Swagger/OpenAPI UI and spec generation
- `Microsoft.EntityFrameworkCore.InMemory` — in-memory DB (current)
- `Microsoft.EntityFrameworkCore.SqlServer` — SQL Server provider (production target)
- `Microsoft.AspNetCore.Authentication.JwtBearer` — JWT validation
- `Asp.Versioning.Http` + `Asp.Versioning.Mvc.ApiExplorer` — API versioning
- `FluentValidation.AspNetCore` — input validation
- `Serilog.AspNetCore` — structured JSON logging

**Testing Dependencies**:
- `xunit` — test runner
- `Moq` — mocking
- `FluentAssertions` — assertion library
- `Microsoft.AspNetCore.Mvc.Testing` — integration test HTTP client

**Storage**:
- Current: EF Core InMemory (`UseInMemoryDatabase("FundTransferDb")`)
- Production target: SQL Server 2019+ (`UseSqlServer(connectionString)`)
- Swap: change a single line in `Program.cs` / DI registration; no service or domain code changes

**Testing**: xUnit + Moq + FluentAssertions; Swagger UI for exploratory/manual testing

**Target Platform**: Linux/Windows server (ASP.NET Core cross-platform)

**Project Type**: REST Web Service

**Performance Goals**:
- Balance retrieval p99 ≤ 500 ms (SC-002)
- Fund transfer end-to-end p99 ≤ 3 s (SC-003)
- Core transaction API p95 ≤ 200 ms (constitution baseline)
- 500 concurrent balance inquiries, 100 concurrent transfers (SC-008)

**Constraints**:
- Monetary amounts: `long` (integer minor currency units — e.g., cents); `float`/`double` PROHIBITED
- ACID: all transfer mutations wrapped in `IDbContextTransaction`
- Idempotency: unique DB index on `IdempotencyKey` in Transfers table
- Concurrency: EF Core `[Timestamp]` / RowVersion on Account entity (optimistic locking)
- API versioned under `/v1/` path prefix

**Scale/Scope**: Single deployable service; 3 REST endpoints; ~4 domain entities

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — ✅ PASSED*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Security by Design | ✅ PASS | JWT bearer validation via `JwtBearer` middleware; owner-only transfer authz; HTTPS enforced; EF Core parameterized queries; no hardcoded secrets |
| II. Data Integrity & ACID | ✅ PASS | EF Core DB transactions for transfers; idempotency keys with unique DB constraint; optimistic concurrency (RowVersion); monetary values as `long` minor units |
| III. Compliance & Regulatory | ✅ PASS | No card/PCI data in scope; KYC/AML upstream; owner field is opaque ID (not PII); audit log immutable (no delete on AuditLogEntry); FR-013 resolved (see research.md) |
| IV. Full Auditability | ✅ PASS | `AuditLogEntry` written on every account/transfer state change; Serilog JSON structured logging; `X-Correlation-ID` middleware; health endpoints |
| V. Test-First Development | ✅ PASS | xUnit TDD; 80% coverage gate in CI; contract tests via Swashbuckle-generated OpenAPI spec; integration tests via `WebApplicationFactory` |
| VI. API Versioning | ✅ PASS | `/v1/accounts`, `/v1/transfers`; `Asp.Versioning.Http` for future versioning; Swagger documents all versions |
| VII. Simplicity & YAGNI | ✅ PASS | Layered architecture (Api → Application → Infrastructure); no CQRS/event bus; repository pattern justified (see Complexity Tracking) |

## Project Structure

### Documentation (this feature)

```text
specs/001-fund-transfer/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── openapi.yaml     # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── FundTransfer.Api/                    # ASP.NET Core Web API entry point
│   ├── Controllers/
│   │   ├── AccountsController.cs
│   │   └── TransfersController.cs
│   ├── Middleware/
│   │   ├── CorrelationIdMiddleware.cs
│   │   └── ExceptionHandlingMiddleware.cs
│   └── Program.cs
├── FundTransfer.Application/            # Business logic (no EF/HTTP dependencies)
│   ├── Interfaces/
│   │   ├── IAccountRepository.cs
│   │   ├── ITransferRepository.cs
│   │   └── IAuditLogRepository.cs
│   ├── Services/
│   │   ├── AccountService.cs
│   │   └── TransferService.cs
│   └── DTOs/
│       ├── CreateAccountRequest.cs
│       ├── AccountResponse.cs
│       ├── CreateTransferRequest.cs
│       └── TransferResponse.cs
└── FundTransfer.Infrastructure/         # EF Core, repositories, logging
    └── Persistence/
        ├── AppDbContext.cs
        └── Repositories/
            ├── AccountRepository.cs
            ├── TransferRepository.cs
            └── AuditLogRepository.cs

tests/
├── FundTransfer.UnitTests/              # Service-layer unit tests (Moq)
├── FundTransfer.IntegrationTests/       # End-to-end via WebApplicationFactory + InMemory DB
└── FundTransfer.ContractTests/          # OpenAPI contract validation tests

FundTransfer.sln
```

**Structure Decision**: Single-backend .NET solution (no frontend). Three class library projects
within the solution enforce clean layering: `Api` → `Application` → `Infrastructure`. The
`Application` layer has no framework dependencies, making it fully unit-testable with Moq.

## Complexity Tracking

| Pattern | Why Needed | Simpler Alternative Rejected Because |
|---------|-----------|--------------------------------------|
| Repository pattern | Decouples service logic from EF Core; enables InMemory↔SqlServer swap and unit testing services with pure Moq mocks | Direct EF Core in services would couple business rules to the ORM and prevent unit testing without a real DB context |
