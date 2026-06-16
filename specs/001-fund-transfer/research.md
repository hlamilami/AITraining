# Research: Fund Transfer Service

**Phase**: 0 — Outline & Research
**Date**: 2026-06-15
**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

All NEEDS CLARIFICATION items from the spec and Technical Context are resolved here.

---

## 1. Language & Framework

**Decision**: C# 12 / .NET 8 LTS + ASP.NET Core Web API

**Rationale**:
- .NET 8 is an LTS release (supported through November 2026); the team has specified .NET.
- ASP.NET Core is the industry standard for high-throughput, cross-platform REST APIs in .NET.
- Strong EF Core integration, native JWT middleware, and first-class Swagger support via Swashbuckle.
- Mature ecosystem for banking/fintech (used by major banks for internal services).
- Minimal API or Controller-based: **controller-based** chosen for clearer separation and better Swagger code-gen compatibility.

**Alternatives considered**:
- Python/FastAPI — rejected (team specified .NET).
- Java/Spring Boot — rejected (team specified .NET).
- Minimal API style — deferred; controller-based provides better structure for contract tests.

---

## 2. API Documentation & Manual Testing

**Decision**: Swashbuckle.AspNetCore (Swagger UI + OpenAPI 3.0 spec generation)

**Rationale**:
- User explicitly specified Swagger for testing.
- Swashbuckle integrates natively with ASP.NET Core, auto-generates OpenAPI 3.0 spec from controller attributes and XML comments.
- Swagger UI served at `/swagger` provides an interactive browser-based testing interface.
- Generated `openapi.yaml` is the source of truth for contract tests.

**Configuration notes**:
- Enable XML doc generation (`<GenerateDocumentationFile>true</GenerateDocumentationFile>`) for richer Swagger descriptions.
- Add `[ProducesResponseType]` attributes to all controller actions for accurate response schemas.
- Swagger UI enabled in all environments (development + production) for this service; access can be restricted by environment variable if needed.

**Alternatives considered**:
- NSwag — feature-equivalent; Swashbuckle chosen for wider community adoption and simpler setup.
- Postman collections — supplementary, not primary.

---

## 3. Database & ORM

**Decision**: Entity Framework Core 8 with in-memory database now; SQL Server as production target

**Rationale**:
- User explicitly specified this configuration.
- EF Core InMemory (`Microsoft.EntityFrameworkCore.InMemory`) requires zero infrastructure, is ideal for rapid development and integration testing.
- SQL Server (`Microsoft.EntityFrameworkCore.SqlServer`) is the production target; swap is a single DI line change in `Program.cs`.
- EF Core supports ACID transactions (`IDbContextTransaction`), optimistic concurrency (`[Timestamp]`), and DECIMAL/long mapping for monetary values.
- Migrations will be written for the SQL Server provider; InMemory DB is schema-less and uses the same EF model.

**Important constraint**: The InMemory provider does **not** enforce unique constraints or FK constraints at the DB level. Unique-key enforcement for idempotency keys and account numbers must be done in the application layer (service logic) during the InMemory phase, and also enforced at the DB level once SQL Server is activated.

**Alternatives considered**:
- SQLite in-process — acceptable alternative to InMemory for testing; InMemory chosen for simplicity as instructed.
- PostgreSQL — not chosen; SQL Server is the team's target.
- Dapper — rejected; EF Core provides the abstraction needed for the InMemory↔SqlServer swap.

---

## 4. Monetary Value Representation

**Decision**: `long` (Int64) representing minor currency units (e.g., cents for USD/EUR)

**Rationale**:
- Constitution Principle II explicitly prohibits floating-point for money.
- Using `long` (integer cents) eliminates rounding entirely.
- API accepts and returns amounts as integers in minor units (e.g., `10050` = $100.50 USD).
- Stored as `bigint` in SQL Server (maps to `long` in EF Core).
- Amounts up to 9.2 × 10¹⁸ minor units — well beyond the spec's 1 billion unit maximum.
- All balances and transfer amounts represented this way consistently across domain, API, and storage.

**Alternatives considered**:
- `decimal` — acceptable and common in .NET; `long` minor units chosen for zero-ambiguity and simpler equality comparisons in tests.

---

## 5. Idempotency Pattern

**Decision**: Caller-supplied `Idempotency-Key` header (UUID); stored in `Transfers` table with unique constraint

**Rationale**:
- Standard pattern used by Stripe, Adyen, and Open Banking APIs.
- On transfer request: check DB for existing record with the same `IdempotencyKey`. If found, return the stored result (no re-processing). If not found, process and store.
- Unique index on `Transfers.IdempotencyKey` prevents race conditions between concurrent identical requests (DB constraint is the final guard).
- `IdempotencyKey` is required on `POST /v1/transfers`; 400 returned if missing.
- Key format: UUID v4 (validated server-side).

**InMemory note**: Unique constraint must be enforced at service layer (check before insert) since InMemory provider doesn't enforce DB-level constraints.

**Alternatives considered**:
- Server-generated idempotency tokens — rejected; caller-supplied keys are the industry standard and match the spec assumption.
- Redis for idempotency store — rejected (YAGNI; DB-level unique index is sufficient).

---

## 6. Concurrency Control

**Decision**: Optimistic concurrency via EF Core `[Timestamp]` (RowVersion) on the `Account` entity

**Rationale**:
- Optimistic concurrency is appropriate when conflicts are infrequent (most transfers target different accounts).
- EF Core's `[Timestamp]` property adds a `rowversion` column in SQL Server; EF Core auto-includes it in `WHERE` clauses on `UPDATE`. If the row was modified since read, EF throws `DbUpdateConcurrencyException`.
- On conflict: catch `DbUpdateConcurrencyException`, reload the entity, re-validate business rules (balance check), and retry up to 3 times before returning a 409 Conflict.
- For the InMemory provider: `[Timestamp]` is a no-op; concurrency conflicts won't be detected in InMemory. **This is an accepted limitation of the InMemory phase.** Concurrent transfer correctness is fully tested via integration tests against SQL Server in CI.

**Alternatives considered**:
- Pessimistic locking (`SELECT FOR UPDATE`) — would require raw SQL; rejected in favour of EF Core-native approach.
- `[ConcurrencyCheck]` on Balance field — equivalent; `[Timestamp]` chosen as it covers the full row.

---

## 7. Authorization Model (FR-013 Resolution)

**Decision**: Owner-only transfer authorization with JWT claims; `transfer:admin` scope bypass

**Rationale**:
- The spec states authentication is handled upstream; this service receives pre-authenticated requests carrying a JWT.
- The service validates the JWT signature and extracts the caller's identity from the `sub` claim.
- For `POST /v1/transfers`: the service checks that `sourceAccount.OwnerId == User.FindFirst("sub").Value`.
- Callers with the `transfer:admin` scope (present in JWT `scope` claim) may initiate transfers from any account (support/operations use case).
- For `GET /v1/accounts/{number}`: accessible to any authenticated caller (balance visibility is not restricted to owner for this version; could be tightened later).
- For `POST /v1/accounts`: any authenticated caller may create an account; the supplied `owner` value is stored as-is (upstream identity already verified).

**Constitution alignment**: Principle of least privilege satisfied — callers can only debit accounts they own; admin scope is explicitly scoped and audited.

**Alternatives considered**:
- Fully open (no owner check) — rejected; violates constitution Principle I.
- Full RBAC with multiple roles — deferred (YAGNI); owner-check + admin scope is sufficient for this version.

---

## 8. Testing Strategy

**Decision**: xUnit + Moq + FluentAssertions; `WebApplicationFactory<Program>` for integration tests

**Rationale**:
- xUnit is the dominant .NET test framework; well-supported by CI/CD platforms.
- Moq provides clean interface-based mocking for service unit tests.
- FluentAssertions improves test readability (`result.Should().Be(100)`).
- `Microsoft.AspNetCore.Mvc.Testing` provides `WebApplicationFactory` for in-process integration testing with the actual HTTP pipeline and InMemory DB.
- Contract tests: Swashbuckle generates `openapi.yaml` on build; tests validate the running API responses conform to the schema.

**TDD mandate (Constitution Principle V)**:
1. Write failing xUnit test describing behaviour.
2. Implement minimum code to pass.
3. Refactor; repeat.
Coverage gate: ≥ 80% on `FundTransfer.Application` and `FundTransfer.Infrastructure`.

---

## 9. Structured Logging & Observability

**Decision**: Serilog with JSON console sink; `X-Correlation-ID` middleware

**Rationale**:
- Serilog (`Serilog.AspNetCore`, `Serilog.Sinks.Console`) provides structured JSON logging with minimal config.
- `X-Correlation-ID` request header is read (or generated if absent) at ingress and attached to all log entries via `LogContext.PushProperty`.
- All financial state changes (account created, transfer completed/rejected) emit structured log events with before/after amounts.
- Constitution Principle IV requires this; silent failures are prohibited.

---

## Summary of Resolved Clarifications

| Item | Resolution |
|------|-----------|
| FR-013: Authorization scope | Owner-only; caller's JWT `sub` must match `account.OwnerId` for transfers; `transfer:admin` scope bypasses |
| Language/Framework | C# 12 / .NET 8 / ASP.NET Core Web API |
| API docs & testing | Swashbuckle.AspNetCore (Swagger UI) |
| Database (current) | EF Core InMemory |
| Database (target) | SQL Server 2019+ via EF Core SqlServer provider |
| Monetary type | `long` (integer minor units, e.g., cents) |
| Idempotency | Caller-supplied `Idempotency-Key` UUID header; unique DB index |
| Concurrency | EF Core `[Timestamp]` optimistic concurrency; retry on `DbUpdateConcurrencyException` |
| Testing | xUnit + Moq + FluentAssertions + WebApplicationFactory |
| Logging | Serilog JSON + `X-Correlation-ID` middleware |
