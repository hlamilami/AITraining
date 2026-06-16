# Tasks: Currency Conversion & Cross-Currency Transfers

**Input**: Design documents from `specs/002-currency-conversion/`

**Prerequisites**: plan.md ✅ | spec.md ✅ | research.md ✅ | data-model.md ✅ | contracts/openapi.yaml ✅

**Tests**: Included — Constitution Principle V mandates TDD (Red → Green → Refactor); unit tests must be written and confirmed failing before implementation begins.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- TDD tasks: Write test first, confirm it fails, then implement

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Extend the existing solution with the schema changes and constants needed by all user stories

- [ ] T001 Add `ExchangeRateCreated`, `ExchangeRateUpdated`, `CrossCurrencyTransferCompleted`, `CrossCurrencyTransferRejected` to `src/FundTransfer.Application/Constants/DomainConstants.cs`
- [ ] T002 [P] Add cross-currency fields (`AppliedExchangeRateId`, `SourceAmount`, `DestinationAmount`, `SourceCurrency`, `DestinationCurrency`) to `src/FundTransfer.Application/Models/Transfer.cs`
- [ ] T003 [P] Ensure `ExchangeRate` model contains all required fields (`Id`, `SourceCurrency`, `TargetCurrency`, `Rate decimal(18,6)`, `EffectiveFrom`, `CreatedBy`, `SupersededBy`, `RowVersion`) in `src/FundTransfer.Application/Models/ExchangeRate.cs`
- [ ] T004 Configure `ExchangeRates` `DbSet`, filtered unique index on `(SourceCurrency, TargetCurrency) WHERE SupersededBy IS NULL`, covering lookup index, and `Transfer` cross-currency column mappings in `src/FundTransfer.Infrastructure/Persistence/AppDbContext.cs`

**Checkpoint**: Schema and constants ready — no user story work can begin until T001–T004 are complete

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Repository interfaces and infrastructure that every user story depends on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T005 Define `FindCurrentAsync(sourceCurrency, targetCurrency)`, `GetByIdAsync(id)`, `GetHistoryAsync(sourceCurrency, targetCurrency)`, `AddAsync(rate)`, and `SupersedeAsync(oldId, newId)` methods in `src/FundTransfer.Application/Interfaces/IExchangeRateRepository.cs`
- [ ] T006 Implement `ExchangeRateRepository` using EF Core with optimistic concurrency (`RowVersion`) for the supersession update in `src/FundTransfer.Infrastructure/Persistence/Repositories/ExchangeRateRepository.cs`
- [ ] T007 [P] Register `IExchangeRateRepository` → `ExchangeRateRepository` in the DI container in `src/FundTransfer.Api/Program.cs`
- [ ] T008 [P] Ensure `ITransferRepository` and `IAccountRepository` expose the methods required for cross-currency transfers (`GetByIdWithLockAsync`, `UpdateBalanceAsync`) in `src/FundTransfer.Application/Interfaces/`

**Checkpoint**: Foundation ready — all three user story phases can now begin

---

## Phase 3: User Story 1 — Manage Exchange Rates (Priority: P1) 🎯 MVP

**Goal**: Administrators with `exchange-rate:admin` scope can create and update directed exchange-rate pairs; history is preserved immutably on every update.

**Independent Test**: `POST /v1/exchange-rates` with valid USD→EUR rate returns 201 with rate ID and effectiveFrom; a second POST for the same pair returns 201 and the first row is visible in history with `SupersededBy` set.

### Tests for User Story 1

> **TDD: Write these tests FIRST and confirm they FAIL before implementing**

- [ ] T009 [P] [US1] Write unit tests for `ExchangeRateService.SetRateAsync`: valid creation, same-currency rejection, non-positive rate rejection, supersession chain, and concurrent-write optimistic-concurrency retry in `tests/FundTransfer.UnitTests/Services/ExchangeRateServiceTests.cs`
- [ ] T010 [P] [US1] Write integration tests for `POST /v1/exchange-rates`: 201 on valid input, 400 on same-currency, 400 on non-positive rate, 400 on unsupported currency, 401 on missing JWT, 403 on missing `exchange-rate:admin` scope in `tests/FundTransfer.IntegrationTests/Controllers/ExchangeRatesControllerTests.cs`

### Implementation for User Story 1

- [ ] T011 [P] [US1] Implement `SetExchangeRateRequestValidator` (source ≠ target, rate > 0, supported ISO 4217 codes from `DomainConstants`) in `src/FundTransfer.Api/Validators/SetExchangeRateRequestValidator.cs`
- [ ] T012 [P] [US1] Implement `ExchangeRateService.SetRateAsync`: insert new `ExchangeRate` row, call `SupersedeAsync` on the previous current row if present, write `AuditLogEntry` with `ExchangeRateCreated` or `ExchangeRateUpdated` operation type in `src/FundTransfer.Application/Services/ExchangeRateService.cs`
- [ ] T013 [US1] Implement `ExchangeRatesController.SetRate` action: `[HttpPost]`, require `exchange-rate:admin` scope, validate via FluentValidation, call `ExchangeRateService.SetRateAsync`, return 201 with `ExchangeRateResponse` in `src/FundTransfer.Api/Controllers/ExchangeRatesController.cs`
- [ ] T014 [US1] Implement `ExchangeRateResponse` DTO (`Id`, `SourceCurrency`, `TargetCurrency`, `Rate`, `EffectiveFrom`, `CreatedBy`) in `src/FundTransfer.Application/DTOs/ExchangeRateResponse.cs`

**Checkpoint**: User Story 1 fully functional — admin can create and update exchange rates; history preserved; audit entries written

---

## Phase 4: User Story 2 — Retrieve Exchange Rates (Priority: P2)

**Goal**: Any authenticated user can query the current exchange rate for a directed currency pair and see the rate value with its effective timestamp.

**Independent Test**: After creating USD→EUR at 0.920000 (US1), `GET /v1/exchange-rates/USD/EUR` returns 200 with rate 0.920000 and the correct `effectiveFrom`. After updating to 0.915000, the GET returns the new rate, not the old one.

### Tests for User Story 2

> **TDD: Write these tests FIRST and confirm they FAIL before implementing**

- [ ] T015 [P] [US2] Write unit tests for `ExchangeRateService.GetCurrentRateAsync`: returns current rate, returns null when no rate exists, returns updated rate after supersession in `tests/FundTransfer.UnitTests/Services/ExchangeRateServiceTests.cs`
- [ ] T016 [P] [US2] Write integration tests for `GET /v1/exchange-rates/{source}/{target}`: 200 with correct rate, 404 for unknown pair, 401 for unauthenticated request, returns newest rate after update in `tests/FundTransfer.IntegrationTests/Controllers/ExchangeRatesControllerTests.cs`

### Implementation for User Story 2

- [ ] T017 [P] [US2] Implement `ExchangeRateService.GetCurrentRateAsync(sourceCurrency, targetCurrency)`: call `FindCurrentAsync`, return mapped `ExchangeRateResponse` or null in `src/FundTransfer.Application/Services/ExchangeRateService.cs`
- [ ] T018 [US2] Implement `ExchangeRatesController.GetCurrentRate` action: `[HttpGet("{sourceCurrency}/{targetCurrency}")]`, require any authenticated user, call service, return 200 or 404 in `src/FundTransfer.Api/Controllers/ExchangeRatesController.cs`

**Checkpoint**: User Stories 1 and 2 both independently functional — rate admin and rate query workflows complete

---

## Phase 5: User Story 3 — Cross-Currency Transfers (Priority: P3)

**Goal**: Account owners can transfer funds between accounts in different currencies; the system applies the current rate atomically, records both amounts and the applied rate, and enforces idempotency.

**Independent Test**: Create a USD account with balance 10000 (100.00 USD) and an EUR account, set USD→EUR rate to 0.920000. Submit `POST /v1/transfers` for 10000 USD. Verify source balance decreases by 10000 and destination balance increases by 9200 (floor(10000 × 0.920000) = 9200). Transfer record must have `appliedExchangeRateId`, `sourceAmount = 10000`, `destinationAmount = 9200`. Duplicate submission with same `Idempotency-Key` returns same result without second processing.

### Tests for User Story 3

> **TDD: Write these tests FIRST and confirm they FAIL before implementing**

- [ ] T019 [P] [US3] Write unit tests for cross-currency `TransferService.ExecuteAsync`: valid transfer produces correct floor calculation, missing rate rejects with `NO_EXCHANGE_RATE`, insufficient funds rejects, zero destination amount rejects, overflow rejects, idempotency replay returns stored result in `tests/FundTransfer.UnitTests/Services/TransferServiceTests.cs`
- [ ] T020 [P] [US3] Write integration tests for cross-currency `POST /v1/transfers`: successful transfer debits source in source currency and credits destination in destination currency, correct `destinationAmount` (floor), 422 with `NO_EXCHANGE_RATE` when pair missing, 422 with `INSUFFICIENT_FUNDS`, 422 with `ZERO_DESTINATION_AMOUNT`, idempotency replay returns 200 with original result in `tests/FundTransfer.IntegrationTests/Controllers/TransfersControllerTests.cs`
- [ ] T021 [P] [US3] Write unit tests for floor-calculation helper: `floor(10000 × 0.920000) = 9200`, `floor(1 × 0.000001) = 0` (zero destination), large amount precision check in `tests/FundTransfer.UnitTests/Services/TransferServiceTests.cs`

### Implementation for User Story 3

- [ ] T022 [P] [US3] Implement floor-calculation: `DestinationAmount = (long)(sourceAmount * rate)` using `decimal` arithmetic; reject if result is 0; reject if result exceeds `long.MaxValue` before cast in `src/FundTransfer.Application/Services/TransferService.cs`
- [ ] T023 [US3] Extend `TransferService.ExecuteAsync` for cross-currency path: snapshot current `ExchangeRate` by `FindCurrentAsync` at transaction start, compute `DestinationAmount`, validate (no rate → `NO_EXCHANGE_RATE`, zero destination → `ZERO_DESTINATION_AMOUNT`, overflow → `DESTINATION_OVERFLOW`), persist `Transfer` with all cross-currency fields, write `CrossCurrencyTransferCompleted` or `CrossCurrencyTransferRejected` audit entry, all within single DB transaction in `src/FundTransfer.Application/Services/TransferService.cs`
- [ ] T024 [US3] Update `CreateTransferRequestValidator` to allow `SourceCurrency`/`DestinationCurrency` fields when provided and validate they are supported ISO 4217 codes in `src/FundTransfer.Api/Validators/CreateTransferRequestValidator.cs`
- [ ] T025 [US3] Extend `TransferResponse` to include `appliedExchangeRateId`, `sourceAmount`, `destinationAmount`, `sourceCurrency`, `destinationCurrency` (nullable/optional fields for backward compatibility) in `src/FundTransfer.Application/DTOs/TransferResponse.cs`
- [ ] T026 [US3] Verify `TransfersController` passes cross-currency fields through to the service and includes them in the response; no controller logic change needed if service returns populated `TransferResponse` in `src/FundTransfer.Api/Controllers/TransfersController.cs`

**Checkpoint**: All three user stories fully functional — full cross-currency transfer flow with atomicity, idempotency, rate locking, and audit logging

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Contract validation, error consistency, and quickstart validation

- [ ] T027 [P] Update contract tests to validate `POST /v1/exchange-rates` and `GET /v1/exchange-rates/{source}/{target}` responses against `contracts/openapi.yaml` schema in `tests/FundTransfer.ContractTests/ContractTests.cs`
- [ ] T028 [P] Verify all new error responses (`NO_EXCHANGE_RATE`, `ZERO_DESTINATION_AMOUNT`, `DESTINATION_OVERFLOW`) are handled by `ExceptionHandlingMiddleware` and return consistent RFC 7807 problem-detail payloads in `src/FundTransfer.Api/Middleware/ExceptionHandlingMiddleware.cs`
- [ ] T029 Validate all scenarios in `quickstart.md` pass end-to-end against the running service using curl or `.http` file
- [ ] T030 [P] Run full test suite (`dotnet test FundTransfer.sln`) and confirm ≥ 80% coverage on `ExchangeRateService` and updated `TransferService` business logic

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately; T002 and T003 are parallel
- **Foundational (Phase 2)**: Requires Phase 1 completion — BLOCKS all user stories
- **US1 (Phase 3)**: Requires Phase 2 completion — tests T009/T010 parallel, then T011/T012 parallel, then T013
- **US2 (Phase 4)**: Requires Phase 2 completion — T015/T016 parallel then T017/T018 sequential; US2 does NOT require US1 but can share test setup
- **US3 (Phase 5)**: Requires Phase 2 AND US1 completion (rate must exist before transfer uses it end-to-end); T019/T020/T021 parallel then T022, T023, T024/T025 parallel, T026
- **Polish (Phase 6)**: Requires all user story phases complete

### User Story Dependencies

- **US1 (P1)**: Depends only on Foundational phase — no story dependencies
- **US2 (P2)**: Depends only on Foundational phase — can run in parallel with US1
- **US3 (P3)**: Depends on Foundational phase AND US1 (the rate-lookup code path in the service depends on `IExchangeRateRepository` from US1)

### Within Each User Story (TDD order)

1. Write tests → confirm they **FAIL**
2. Models/DTOs (parallel where different files)
3. Service logic
4. Controller/endpoint
5. Confirm tests **PASS**

---

## Parallel Examples

### Parallel Example: User Story 1

```text
# Write both test files together (different files, no code to depend on yet):
Task T009: ExchangeRateServiceTests.cs — unit tests
Task T010: ExchangeRatesControllerTests.cs — integration tests

# After tests confirmed failing, implement in parallel:
Task T011: SetExchangeRateRequestValidator.cs
Task T012: ExchangeRateService.cs (SetRateAsync)
# Then wire up controller:
Task T013: ExchangeRatesController.cs
```

### Parallel Example: User Story 3

```text
# Write all test files together:
Task T019: TransferServiceTests.cs — cross-currency unit tests
Task T020: TransfersControllerTests.cs — integration tests
Task T021: TransferServiceTests.cs — floor-calculation unit tests

# After tests confirmed failing, implement in parallel:
Task T022: Floor-calculation helper in TransferService.cs
Task T024: CreateTransferRequestValidator.cs
Task T025: TransferResponse.cs DTO extension
# Then complete service orchestration:
Task T023: TransferService.ExecuteAsync cross-currency path
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T004)
2. Complete Phase 2: Foundational (T005–T008) — **BLOCKS everything**
3. Complete Phase 3: User Story 1 (T009–T014)
4. **STOP and VALIDATE**: Run integration tests for `POST /v1/exchange-rates`; verify rate history preserved
5. Demo / deploy admin rate management independently

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. US1 (rate management) → test independently → deploy (MVP)
3. US2 (rate retrieval) → test independently → deploy
4. US3 (cross-currency transfer) → test independently → deploy
5. Polish → contract tests pass, quickstart validates

### Parallel Team Strategy

With two developers:

1. Both complete Setup + Foundational together
2. Once Foundational is complete:
   - **Developer A**: US1 (T009–T014) + US2 (T015–T018) sequentially
   - **Developer B**: Write US3 tests (T019–T021) while Developer A finishes US1; implement US3 (T022–T026) after T005 and US1 are complete
3. Both contribute to Phase 6 polish

---

## Notes

- `[P]` tasks operate on different files with no shared in-progress dependencies — safe to parallelize
- `[Story]` label maps each task to its user story for traceability
- TDD is mandatory per Constitution Principle V — tests MUST fail before implementation begins
- Monetary values: always `long` minor units; exchange rates: always `decimal(18,6)`; never `float` or `double`
- Idempotency key uniqueness is enforced at the DB layer — do not rely on application-level duplicate checks alone
- Commit after each checkpoint to preserve independently verified increments
- Stop at any checkpoint to validate the story independently before proceeding
