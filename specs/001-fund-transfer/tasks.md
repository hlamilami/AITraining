# Tasks: Fund Transfer Service

**Input**: Design documents from `specs/001-fund-transfer/`

**Prerequisites**: plan.md ✅ | spec.md ✅ | research.md ✅ | data-model.md ✅ | contracts/openapi.yaml ✅ | quickstart.md ✅

**Stack**: C# 12 / .NET 8 / ASP.NET Core Web API / Swashbuckle / EF Core InMemory → SQL Server

**Tests**: Included — TDD is NON-NEGOTIABLE per constitution Principle V. Write failing tests before implementation.

**Organization**: Grouped by user story for independent implementation, testing, and delivery.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Parallelizable — different files, no dependency on incomplete tasks
- **[Story]**: User story this task belongs to (US1, US2, US3)
- All paths relative to repository root

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Initialize the .NET 8 solution, projects, and tooling. No business logic yet.

- [x] T001 Create `FundTransfer.sln` and all four projects: `src/FundTransfer.Api`, `src/FundTransfer.Application`, `src/FundTransfer.Infrastructure`, run `dotnet new sln` and `dotnet new webapi/classlib` for each
- [x] T002 Add NuGet packages: `Swashbuckle.AspNetCore`, `Asp.Versioning.Http`, `Asp.Versioning.Mvc.ApiExplorer`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `FluentValidation.AspNetCore`, `Serilog.AspNetCore`, `Serilog.Sinks.Console` to `src/FundTransfer.Api/FundTransfer.Api.csproj`
- [x] T003 [P] Add NuGet packages: `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.InMemory`, `Microsoft.EntityFrameworkCore.SqlServer` to `src/FundTransfer.Infrastructure/FundTransfer.Infrastructure.csproj`
- [x] T004 [P] Add NuGet packages: `xunit`, `Moq`, `FluentAssertions`, `Microsoft.AspNetCore.Mvc.Testing`, `coverlet.collector` to all three test projects under `tests/`
- [x] T005 [P] Add project references: `Api → Application`, `Api → Infrastructure`, `Infrastructure → Application`; add `Application` reference to all test projects in `FundTransfer.sln`
- [x] T006 [P] Enable `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, and `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in all `src/` `.csproj` files

**Checkpoint**: `dotnet build FundTransfer.sln` succeeds with zero errors.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Cross-cutting infrastructure all user stories depend on. No user story work begins until this phase is complete.

**⚠️ CRITICAL**: Phases 3–5 are blocked until this phase is complete.

- [x] T007 Define domain constants — `SupportedCurrencies` list, `TransferStatus` enum (`Pending`, `Completed`, `Rejected`), `FailureReasonCodes` static class — in `src/FundTransfer.Application/Constants/DomainConstants.cs`
- [x] T008 [P] Define repository interfaces `IAccountRepository`, `ITransferRepository`, `IAuditLogRepository` (with async CRUD signatures) in `src/FundTransfer.Application/Interfaces/`
- [x] T009 [P] Create `Account` domain model (fields: `Id`, `AccountNumber`, `Owner`, `Currency`, `Balance`, `CreatedAt`, `RowVersion`) in `src/FundTransfer.Application/Models/Account.cs`
- [x] T010 [P] Create `Transfer` domain model (fields: `Id`, `IdempotencyKey`, `SourceAccountNumber`, `DestinationAccountNumber`, `Amount`, `Currency`, `Status`, `FailureReason`, `InitiatedBy`, `Timestamp`) in `src/FundTransfer.Application/Models/Transfer.cs`
- [x] T011 [P] Create `AuditLogEntry` domain model (fields: `Id`, `EntityType`, `EntityId`, `Actor`, `Operation`, `Timestamp`, `CorrelationId`, `BeforeState`, `AfterState`) in `src/FundTransfer.Application/Models/AuditLogEntry.cs`
- [x] T012 Implement `AppDbContext` — register all three `DbSet<>` properties, configure `[Timestamp]` on `Account.RowVersion`, unique index on `Account.AccountNumber`, unique index on `Transfer.IdempotencyKey` — in `src/FundTransfer.Infrastructure/Persistence/AppDbContext.cs`
- [x] T013 [P] Implement `CorrelationIdMiddleware` — read `X-Correlation-ID` header (generate UUID if absent), attach to `HttpContext.Items` and Serilog `LogContext` — in `src/FundTransfer.Api/Middleware/CorrelationIdMiddleware.cs`
- [x] T014 [P] Implement `ExceptionHandlingMiddleware` — catch all unhandled exceptions, return RFC 7807 `ProblemDetails` JSON with `traceId`, log structured error — in `src/FundTransfer.Api/Middleware/ExceptionHandlingMiddleware.cs`
- [x] T015 Configure `Program.cs`: register EF Core InMemory (`UseInMemoryDatabase("FundTransferDb")`), all repositories and services, JWT Bearer auth, FluentValidation, API versioning, Serilog, Swagger with `SecurityDefinition` for Bearer JWT, middleware pipeline order in `src/FundTransfer.Api/Program.cs`
- [x] T016 [P] Configure Swashbuckle: enable XML comments, add `IOperationFilter` for `Idempotency-Key` header on transfer endpoint, set API version in `src/FundTransfer.Api/Configuration/SwaggerConfig.cs`

**Checkpoint**: `dotnet run --project src/FundTransfer.Api` starts; Swagger UI loads at `http://localhost:5000/swagger`; all endpoints appear with auth lock icon.

---

## Phase 3: User Story 1 — Open a New Account (Priority: P1) 🎯 MVP

**Goal**: `POST /v1/accounts` creates an account with system-assigned number, owner, currency, and non-negative balance.

**Independent Test**: Submit a valid creation request → `201 Created` with `accountNumber`, `owner`, `currency`, `balance`. Submit invalid inputs → `400 Bad Request`.

> **TDD**: Write and confirm tests FAIL (Red) before writing implementation code.

### Tests for User Story 1

- [x] T017 [P] [US1] Write failing unit tests for `AccountService.CreateAccountAsync` covering: valid creation returns account with assigned number; negative balance throws `ValidationException`; unsupported currency throws `ValidationException` — in `tests/FundTransfer.UnitTests/Services/AccountServiceTests.cs`
- [x] T018 [P] [US1] Write failing integration tests for `POST /v1/accounts` covering: `201` on valid payload; `400` on negative balance; `400` on invalid currency code — in `tests/FundTransfer.IntegrationTests/Controllers/AccountsControllerTests.cs`

### Implementation for User Story 1

- [x] T019 [P] [US1] Implement `AccountRepository` (async `AddAsync`, `GetByAccountNumberAsync`, `ExistsAsync`) with EF Core in `src/FundTransfer.Infrastructure/Persistence/Repositories/AccountRepository.cs`
- [x] T020 [P] [US1] Implement `AuditLogRepository` (async `AddAsync` only — no update/delete) in `src/FundTransfer.Infrastructure/Persistence/Repositories/AuditLogRepository.cs`
- [x] T021 [P] [US1] Create `CreateAccountRequest` DTO (properties: `Owner`, `Currency`, `InitialBalance`) and `AccountResponse` DTO in `src/FundTransfer.Application/DTOs/`
- [x] T022 [US1] Implement `AccountService.CreateAccountAsync`: validate currency against `SupportedCurrencies`; validate `InitialBalance >= 0`; generate unique `AccountNumber` (`ACC-{yyyyMMdd}-{seq}`); persist via `IAccountRepository`; write `AuditLogEntry` (`AccountCreated`, before=null, after=snapshot) via `IAuditLogRepository` in `src/FundTransfer.Application/Services/AccountService.cs`
- [x] T023 [US1] Implement `AccountsController` action `CreateAccount` (`POST /v1/accounts`): validate model, call `AccountService.CreateAccountAsync`, return `201 Created` with `AccountResponse` in `src/FundTransfer.Api/Controllers/AccountsController.cs`
- [x] T024 [US1] Add `CreateAccountRequestValidator` (FluentValidation): `Owner` non-empty; `Currency` matches `^[A-Z]{3}$` and in `SupportedCurrencies`; `InitialBalance >= 0` in `src/FundTransfer.Api/Validators/CreateAccountRequestValidator.cs`

**Checkpoint**: `dotnet test` — T017 and T018 tests pass (Green). `POST /v1/accounts` via Swagger UI returns `201`. Invalid inputs return `400`. US1 is fully functional independently.

---

## Phase 4: User Story 2 — Retrieve Account Balance (Priority: P2)

**Goal**: `GET /v1/accounts/{accountNumber}` returns current balance and full account details for a known account.

**Independent Test**: Create an account (US1), then GET it → `200 OK` with matching `accountNumber`, `owner`, `currency`, `balance`. GET unknown number → `404 Not Found`.

> **TDD**: Write and confirm tests FAIL before writing implementation code.

### Tests for User Story 2

- [x] T025 [P] [US2] Write failing unit tests for `AccountService.GetAccountAsync` covering: returns `AccountResponse` for existing account; throws `NotFoundException` for unknown number — in `tests/FundTransfer.UnitTests/Services/AccountServiceTests.cs`
- [x] T026 [P] [US2] Write failing integration tests for `GET /v1/accounts/{accountNumber}` covering: `200` with correct payload after account creation; `404` for non-existent number — in `tests/FundTransfer.IntegrationTests/Controllers/AccountsControllerTests.cs`

### Implementation for User Story 2

- [x] T027 [US2] Implement `AccountService.GetAccountAsync`: call `IAccountRepository.GetByAccountNumberAsync`; throw `NotFoundException` (maps to `404`) if null; return mapped `AccountResponse` in `src/FundTransfer.Application/Services/AccountService.cs`
- [x] T028 [US2] Add `GetAccount` action to `AccountsController` (`GET /v1/accounts/{accountNumber}`): call `AccountService.GetAccountAsync`, return `200 OK` with `AccountResponse`; `ExceptionHandlingMiddleware` handles `NotFoundException` → `404` in `src/FundTransfer.Api/Controllers/AccountsController.cs`

**Checkpoint**: `dotnet test` — T025 and T026 tests pass. `GET /v1/accounts/{number}` returns `200` for known accounts, `404` for unknown. US2 functional independently.

---

## Phase 5: User Story 3 — Transfer Funds Between Accounts (Priority: P3)

**Goal**: `POST /v1/transfers` atomically debits source and credits destination, enforces all business rules, and guarantees idempotency.

**Independent Test**: Create two accounts (US1), execute transfer, verify both balances via GET (US2). Replay same `Idempotency-Key` → `200 OK` with no second debit. Insufficient funds → `422`. Same account → `400`. Currency mismatch → `422`.

> **TDD**: Write and confirm tests FAIL before writing implementation code.

### Tests for User Story 3

- [x] T029 [P] [US3] Write failing unit tests for `TransferService` covering: successful transfer returns `Completed` status; insufficient funds returns `Rejected/InsufficientFunds`; same-account returns validation error; currency mismatch returns `Rejected/CurrencyMismatch`; idempotent replay returns original result — in `tests/FundTransfer.UnitTests/Services/TransferServiceTests.cs`
- [x] T030 [P] [US3] Write failing integration tests for `POST /v1/transfers` covering: `201` with correct balances; `200` on idempotent replay; `422` on insufficient funds; `400` on same account; `422` on currency mismatch; `404` on unknown accounts; `403` when caller does not own source account — in `tests/FundTransfer.IntegrationTests/Controllers/TransfersControllerTests.cs`

### Implementation for User Story 3

- [x] T031 [P] [US3] Implement `TransferRepository` (async `AddAsync`, `GetByIdempotencyKeyAsync`) with EF Core in `src/FundTransfer.Infrastructure/Persistence/Repositories/TransferRepository.cs`
- [x] T032 [P] [US3] Create `CreateTransferRequest` DTO (`SourceAccountNumber`, `DestinationAccountNumber`, `Amount`) and `TransferResponse` DTO in `src/FundTransfer.Application/DTOs/`
- [x] T033 [US3] Implement `TransferService.ExecuteTransferAsync`: (1) check idempotency — return stored result if key exists; (2) load both accounts, throw `NotFoundException` if either missing; (3) validate same-currency, different accounts, positive amount, sufficient balance; (4) open `IDbContextTransaction`, debit source, credit destination, persist `Transfer` record, write two `AuditLogEntry` records, commit; (5) catch `DbUpdateConcurrencyException`, retry up to 3 times; (6) on business rule failure persist rejected `Transfer` with `FailureReason` (no balance change) in `src/FundTransfer.Application/Services/TransferService.cs`
- [x] T034 [US3] Implement `TransfersController` action `CreateTransfer` (`POST /v1/transfers`): extract `Idempotency-Key` header (return `400` if missing/invalid UUID); extract caller identity from `User.FindFirst("sub")`; call `TransferService.ExecuteTransferAsync`; return `201` for new transfer, `200` for idempotent replay in `src/FundTransfer.Api/Controllers/TransfersController.cs`
- [x] T035 [US3] Add `CreateTransferRequestValidator` (FluentValidation): `SourceAccountNumber` non-empty; `DestinationAccountNumber` non-empty and `!= SourceAccountNumber`; `Amount > 0` in `src/FundTransfer.Api/Validators/CreateTransferRequestValidator.cs`
- [x] T036 [US3] Add owner authorization check in `TransferService` or `TransfersController`: verify `sourceAccount.Owner == callerIdentity` or caller has `transfer:admin` scope; throw `ForbiddenException` (maps to `403`) otherwise in `src/FundTransfer.Api/Controllers/TransfersController.cs`

**Checkpoint**: `dotnet test` — T029 and T030 tests pass. All 7 transfer acceptance scenarios from spec pass via Swagger UI. US3 functional independently.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Observability, SQL Server readiness, contract validation, and Swagger documentation polish.

- [x] T037 [P] Add `NotFoundException` and `ForbiddenException` custom exception types; update `ExceptionHandlingMiddleware` to map them to `404` and `403` RFC 7807 responses in `src/FundTransfer.Application/Exceptions/` and `src/FundTransfer.Api/Middleware/ExceptionHandlingMiddleware.cs`
- [x] T038 [P] Add health check endpoint: register `AddHealthChecks()`, map `/health` in `src/FundTransfer.Api/Program.cs`
- [x] T039 [P] Write contract tests that start the API with `WebApplicationFactory`, call each endpoint, and assert response schemas match `contracts/openapi.yaml` definitions in `tests/FundTransfer.ContractTests/`
- [x] T040 [P] Add SQL Server EF Core migration: add `UseSqlServer` conditional branch in `Program.cs` (env-var toggle), run `dotnet ef migrations add InitialCreate` targeting `src/FundTransfer.Infrastructure` in `src/FundTransfer.Infrastructure/Persistence/Migrations/`
- [x] T041 [P] Add XML doc comments (`/// <summary>`) to all controller actions and public DTOs; verify Swagger UI shows descriptions matching `contracts/openapi.yaml` in `src/FundTransfer.Api/Controllers/` and `src/FundTransfer.Application/DTOs/`
- [x] T042 Run all quickstart.md validation scenarios end-to-end via Swagger UI; confirm all acceptance criteria from spec.md are met

**Checkpoint**: `dotnet test FundTransfer.sln` — all tests pass. `dotnet test --collect:"XPlat Code Coverage"` — Application layer coverage ≥ 80%.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — **BLOCKS Phases 3–5**
- **Phase 3 (US1)**: Depends on Phase 2 — MVP deliverable
- **Phase 4 (US2)**: Depends on Phase 2; integrates with US1 (uses `IAccountRepository`)
- **Phase 5 (US3)**: Depends on Phase 2; builds on US1 accounts and US2 balance visibility
- **Phase 6 (Polish)**: Depends on Phases 3–5 complete

### User Story Dependencies

- **US1 (P1)**: Depends only on Foundational phase — fully independent
- **US2 (P2)**: Depends only on Foundational phase + `IAccountRepository` (from US1) — can start in parallel with US1 if repository interface is defined
- **US3 (P3)**: Depends on US1 (accounts must exist to transfer) and US2 (balance verification) — implement last

### Within Each User Story

1. Write failing tests (Red) — T0xx test tasks
2. Domain models / repository interfaces (already done in Foundational)
3. Repository implementations (Infrastructure layer)
4. DTOs (Application layer) — parallelizable with repositories
5. Service implementation (Application layer)
6. Controller action (Api layer)
7. FluentValidation validator (Api layer)
8. Run tests → Green

### Parallel Opportunities

- All `[P]`-marked tasks within a phase can run in parallel
- Phase 1 setup tasks T002–T006 can all run in parallel after T001
- Phase 2 foundational tasks T008–T011 (interfaces + models) run in parallel after T007
- Phase 3 test tasks T017–T018 run in parallel; repository T019 and DTOs T021 run in parallel
- Phase 4 test tasks T025–T026 run in parallel
- Phase 5 test tasks T029–T030, repository T031, DTOs T032 all in parallel
- Phase 6 tasks T037–T041 are all independent and fully parallelizable

---

## Parallel Example: User Story 3

```
# After Phase 2 complete, launch simultaneously:
Task T029: TransferService unit tests   → tests/FundTransfer.UnitTests/Services/TransferServiceTests.cs
Task T030: TransfersController intg tests → tests/FundTransfer.IntegrationTests/Controllers/TransfersControllerTests.cs
Task T031: TransferRepository impl      → src/FundTransfer.Infrastructure/Persistence/Repositories/TransferRepository.cs
Task T032: CreateTransferRequest DTOs   → src/FundTransfer.Application/DTOs/

# Then, once T029–T032 complete:
Task T033: TransferService impl         → src/FundTransfer.Application/Services/TransferService.cs

# Then, once T033 complete:
Task T034: TransfersController action   → src/FundTransfer.Api/Controllers/TransfersController.cs
Task T035: CreateTransferRequestValidator → src/FundTransfer.Api/Validators/CreateTransferRequestValidator.cs
Task T036: Owner authorization check   → src/FundTransfer.Api/Controllers/TransfersController.cs
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational ← **CRITICAL BLOCKER**
3. Complete Phase 3: US1 (Open Account)
4. **STOP & VALIDATE**: `POST /v1/accounts` works; tests pass; Swagger UI confirms contract
5. Demo / ship the account-creation endpoint as MVP

### Incremental Delivery

| Increment | Phases | Deliverable |
|-----------|--------|-------------|
| MVP | 1 + 2 + 3 | Account creation — `POST /v1/accounts` |
| v0.2 | + 4 | Balance retrieval — `GET /v1/accounts/{number}` |
| v0.3 | + 5 | Fund transfers — `POST /v1/transfers` |
| v1.0 | + 6 | Polish, SQL Server ready, contract tests |

### Parallel Team Strategy

After Phase 2 is complete:
- **Developer A**: Phase 3 (US1 — Account creation)
- **Developer B**: Phase 4 (US2 — Balance retrieval, uses same `IAccountRepository` interface)
- **Developer C**: Phase 5 (US3 — Fund transfer, depends on US1 accounts)

All three stories use interfaces defined in Phase 2; no cross-story merge conflicts.

---

## Notes

- `[P]` tasks touch different files — safe to parallelize without merge conflicts
- TDD is mandatory (constitution Principle V): tests must be written and confirmed RED before implementation
- All monetary values are `long` (minor units) — never use `decimal`/`double` for money
- InMemory DB does not enforce unique constraints — service layer must guard idempotency keys in Phase 5
- Swap to SQL Server: change one line in `Program.cs` + run `dotnet ef database update`
- Commit after each phase checkpoint or logical group
- Validate with quickstart.md scenarios before marking any user story done

