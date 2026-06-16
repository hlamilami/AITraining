# Data Model: Fund Transfer Service

**Phase**: 1 — Design
**Date**: 2026-06-15
**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Research**: [research.md](research.md)

---

## Overview

Four entities: `Account`, `Transfer`, `AuditLogEntry`, and `IdempotencyRecord`.
All monetary values are stored as `long` (Int64) representing **minor currency units** (e.g.,
cents for USD/EUR). `float`/`double` are prohibited for money fields.

---

## Entity: Account

Represents a bank account. The account number is system-assigned; the owner is an opaque
identifier supplied by the caller (e.g., a user ID from the upstream identity system).

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `Guid` | PK, non-null | Internal surrogate key |
| `AccountNumber` | `string` | Unique, non-null, max 30 chars | System-assigned (e.g., `ACC-{timestamp}-{seq}`) |
| `Owner` | `string` | Non-null, max 256 chars | Opaque caller-supplied identifier |
| `Currency` | `string` | Non-null, exactly 3 chars, uppercase | ISO 4217 code (e.g., `USD`, `EUR`, `GBP`) |
| `Balance` | `long` | Non-null, `>= 0` | Minor currency units (e.g., cents); NEVER negative |
| `CreatedAt` | `DateTimeOffset` | Non-null, UTC | Set on creation; immutable |
| `RowVersion` | `byte[]` | Concurrency token (`[Timestamp]`) | EF Core optimistic concurrency; `rowversion` in SQL Server |

**Validation rules**:
- `Currency` must be a valid ISO 4217 code from a supported list (initially: `USD`, `EUR`, `GBP`, `SAR`, `AED` — extensible).
- `Balance` on creation must be `>= 0` (non-negative).
- `Balance` must never go negative post-transfer (enforced in `TransferService`).
- `AccountNumber` is unique system-wide; no two accounts share the same number.

**State transitions**:
```
Created (Balance = initialBalance)
    │
    ├─► [transfer debit]  → Balance decreases by amount
    └─► [transfer credit] → Balance increases by amount
```
Account deletion is out of scope; accounts are immutable except for balance changes.

---

## Entity: Transfer

Represents a fund transfer request. Every attempted transfer (including rejected ones) is
persisted with its outcome. Completed transfers are immutable once written.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `Guid` | PK, non-null | Internal surrogate key |
| `IdempotencyKey` | `string` | Unique, non-null, max 36 chars | Caller-supplied UUID; unique DB index |
| `SourceAccountNumber` | `string` | Non-null, FK → `Account.AccountNumber` | |
| `DestinationAccountNumber` | `string` | Non-null, FK → `Account.AccountNumber` | Different from `SourceAccountNumber` |
| `Amount` | `long` | Non-null, `> 0` | Minor currency units |
| `Currency` | `string` | Non-null, 3 chars | Must match both accounts' currency |
| `Status` | `TransferStatus` | Non-null | `Pending`, `Completed`, `Rejected` |
| `FailureReason` | `string?` | Nullable, max 512 chars | Populated on rejection (e.g., `InsufficientFunds`) |
| `InitiatedBy` | `string` | Non-null, max 256 chars | JWT `sub` claim of the requester |
| `Timestamp` | `DateTimeOffset` | Non-null, UTC | Time of processing |

**TransferStatus enum**:
```csharp
public enum TransferStatus { Pending, Completed, Rejected }
```

**Failure reason codes** (string constants):
- `InsufficientFunds`
- `CurrencyMismatch`
- `SameAccountTransfer`
- `SourceAccountNotFound`
- `DestinationAccountNotFound`
- `InvalidAmount`

**Validation rules**:
- `Amount > 0` (zero and negative amounts rejected — FR-010).
- `SourceAccountNumber != DestinationAccountNumber` (FR-009).
- Both accounts must exist (FR-006).
- Both accounts must share the same `Currency` (FR-008).
- Source account `Balance >= Amount` (FR-005).
- `IdempotencyKey` must be unique; duplicate keys return the stored result (FR-007).

**Idempotency behaviour**:
- On duplicate `IdempotencyKey`: return the previously stored `Transfer` record unchanged.
- The HTTP response code for an idempotent replay is `200 OK` (vs. `201 Created` for a new transfer).

---

## Entity: AuditLogEntry

Immutable audit trail. One entry per account creation; one entry per transfer outcome
(including rejected transfers). No updates or deletes on this table ever.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `Guid` | PK, non-null | |
| `EntityType` | `string` | Non-null, max 64 chars | `Account` or `Transfer` |
| `EntityId` | `string` | Non-null, max 64 chars | The `AccountNumber` or `Transfer.Id` |
| `Actor` | `string` | Non-null, max 256 chars | JWT `sub` claim |
| `Operation` | `string` | Non-null, max 64 chars | `AccountCreated`, `TransferCompleted`, `TransferRejected` |
| `Timestamp` | `DateTimeOffset` | Non-null, UTC | |
| `CorrelationId` | `string` | Non-null, max 64 chars | `X-Correlation-ID` from request |
| `BeforeState` | `string?` | Nullable, JSON | Serialized snapshot of entity before operation |
| `AfterState` | `string?` | Nullable, JSON | Serialized snapshot of entity after operation |

**Operation codes**:
- `AccountCreated`
- `TransferCompleted`
- `TransferRejected`

**Immutability enforcement**:
- No `UPDATE` or `DELETE` SQL statements may target `AuditLogEntry`.
- EF Core: no `Update`/`Remove` calls against `AuditLogEntry` DbSet.
- Future SQL Server deployment: row-level security / DENY DELETE can enforce this at DB level.

---

## Entity Relationships

```
Account (1) ──< Transfer (many) via SourceAccountNumber
Account (1) ──< Transfer (many) via DestinationAccountNumber
Transfer (1) ──> AuditLogEntry (1)      [TransferCompleted / TransferRejected]
Account  (1) ──> AuditLogEntry (1)      [AccountCreated]
```

No foreign key from `AuditLogEntry` back to `Transfer`/`Account` (append-only log;
loose coupling is intentional for immutability and performance).

---

## EF Core DbContext Summary

```csharp
// AppDbContext
DbSet<Account>        Accounts
DbSet<Transfer>       Transfers
DbSet<AuditLogEntry>  AuditLog

// Key indexes (SQL Server migrations)
// Accounts:  UNIQUE on AccountNumber
// Transfers: UNIQUE on IdempotencyKey
//            INDEX on SourceAccountNumber, DestinationAccountNumber
// AuditLog:  INDEX on EntityId, Timestamp
```

---

## InMemory vs SQL Server Behaviour Differences

| Concern | InMemory (current) | SQL Server (target) |
|---------|-------------------|---------------------|
| Unique constraints | Enforced by service layer | Enforced by DB index |
| Optimistic concurrency | `[Timestamp]` is a no-op | `rowversion` enforced by DB |
| Transactions | No real transaction isolation | Full ACID (READ COMMITTED) |
| Concurrent race conditions | Not detectable | Detected via `rowversion` + retry |

**Action required before SQL Server activation**: add and run EF Core migrations; validate
idempotency and concurrency under load tests.
