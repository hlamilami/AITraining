# Data Model: Currency Conversion & Cross-Currency Transfers

**Phase**: 1 - Design
**Date**: 2026-06-16
**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Research**: [research.md](research.md)

---

## Overview

This feature introduces a new `ExchangeRate` entity and extends the existing `Transfer` entity to
support cross-currency money movement while preserving exact rate history. Monetary amounts remain
`long` minor units. Exchange-rate values use fixed-point `decimal(18,6)`.

Supported ISO 4217 currencies in scope: **USD, EUR, GBP, SAR, AED**.

---

## Entity Definitions

### Entity: Account

Existing account entity used by the transfer flow.

| Field | Type | Constraints | Indexes | Notes |
|-------|------|-------------|---------|-------|
| `Id` | `Guid` | PK, non-null | Clustered PK | Stable account identifier used by transfer FKs |
| `Owner` | `string` | Non-null, max 256 | Optional non-unique index if owner lookups are required | JWT `sub` or upstream user identifier |
| `Currency` | `string` | Non-null, 3 uppercase chars, must be one of supported currencies | Optional non-unique index with `Owner` for account listings | ISO 4217 account denomination |
| `Balance` | `long` | Non-null, `>= 0` | None | Minor units only |
| `RowVersion` | `byte[]` | Non-null concurrency token | SQL Server `rowversion` backing | Protects concurrent balance updates |

**Business notes**:
- The feature never changes `Account.Currency` after creation.
- Cross-currency transfer logic debits `Balance` in the source account's currency and credits the
  destination account's `Balance` in the destination account's currency.

### Entity: ExchangeRate

New immutable reference-data entity representing the rate for one directed currency pair.

| Field | Type | Constraints | Indexes | Notes |
|-------|------|-------------|---------|-------|
| `Id` | `Guid` | PK, non-null | Clustered PK | Rate snapshot identifier |
| `SourceCurrency` | `string` | Non-null, 3 uppercase chars, supported ISO 4217 code | Part of filtered unique index; part of pair lookup index | Directed source currency |
| `TargetCurrency` | `string` | Non-null, 3 uppercase chars, supported ISO 4217 code | Part of filtered unique index; part of pair lookup index | Directed target currency |
| `Rate` | `decimal(18,6)` | Non-null, `> 0`, scale 6 max | Included in covering lookup index | Conversion multiplier from source minor units to destination minor units |
| `EffectiveFrom` | `DateTimeOffset` | Non-null, UTC, server-assigned | Included in covering lookup index; optional descending history index with pair | Timestamp when this row became current |
| `CreatedBy` | `string` | Non-null, max 256 | Included in covering lookup index | JWT `sub` of administrator |
| `SupersededBy` | `Guid?` | Nullable FK -> `ExchangeRate.Id` | Filter predicate column; separate index for history traversal | Null means current or active rate |
| `RowVersion` | `byte[]` | Non-null concurrency token | SQL Server `rowversion` backing | Protects the supersession update under concurrent admin writes |

**Constraints**:
- `SourceCurrency <> TargetCurrency`
- `Rate > 0`
- Precision limited to 6 decimal places
- Only one current row per pair may have `SupersededBy IS NULL`

### Entity: Transfer

Existing transfer entity extended for same-currency and cross-currency execution.

| Field | Type | Constraints | Indexes | Notes |
|-------|------|-------------|---------|-------|
| `Id` | `Guid` | PK, non-null | Clustered PK | Transfer identifier |
| `IdempotencyKey` | `string` | Non-null, unique, max 128 | Unique index | Existing duplicate-submission guard |
| `SourceAccountId` | `Guid` | Non-null, FK -> `Account.Id` | Non-unique index | Debited account |
| `DestinationAccountId` | `Guid` | Non-null, FK -> `Account.Id` | Non-unique index | Credited account |
| `Amount` | `long` | Non-null, `> 0` | None | Existing request amount; for this feature it equals `SourceAmount` |
| `Status` | `enum` | Non-null | Optional index with `CreatedAt` for reporting | `Pending`, `Completed`, `Failed` |
| `CreatedAt` | `DateTimeOffset` | Non-null, UTC | Optional index with `Status` | Server-assigned creation time |
| `AppliedExchangeRateId` | `Guid?` | Nullable FK -> `ExchangeRate.Id` | Non-unique index | Null for same-currency transfers; set for cross-currency transfers |
| `SourceAmount` | `long` | Non-null | None | Source minor-unit debit; mirrors `Amount` for backward compatibility |
| `DestinationAmount` | `long` | Non-null | None | Destination minor-unit credit |
| `SourceCurrency` | `string` | Non-null, 3 uppercase chars | Optional composite reporting index with `DestinationCurrency` | Snapshot of source account currency |
| `DestinationCurrency` | `string` | Non-null, 3 uppercase chars | Optional composite reporting index with `SourceCurrency` | Snapshot of destination account currency |

**Derived and behavioral rules**:
- Same-currency transfers set `AppliedExchangeRateId = null`, `SourceAmount = DestinationAmount`,
  and `SourceCurrency = DestinationCurrency`.
- Cross-currency transfers set `AppliedExchangeRateId` to the current rate row selected at
  initiation time and never change it afterward.
- `DestinationAmount = floor(SourceAmount x ExchangeRate.Rate)`.

### Entity: AuditLogEntry

Existing append-only audit entity used by this feature.

| Field | Type | Constraints | Indexes | Notes |
|-------|------|-------------|---------|-------|
| `Id` | `Guid` | PK, non-null | Clustered PK | Audit row identifier |
| `Operation_Type` | `string` | Non-null, controlled vocabulary only | Non-unique index with `Timestamp` | Feature vocabulary defined below |
| `Operation_Id` | `Guid` | Non-null | Unique index recommended | Usually the `ExchangeRate.Id` or `Transfer.Id` |
| `Initiator` | `string` | Non-null, max 256 | Optional index for investigations | JWT `sub` or service identity |
| `Timestamp` | `DateTimeOffset` | Non-null, UTC | Part of time-series indexes | Server-assigned event time |
| `CorrelationId` | `string` | Non-null, max 128 | Non-unique index | Request trace identifier |
| `EntityType` | `string` | Non-null, max 64 | Composite index with `EntityId`, `Timestamp` | `ExchangeRate` or `Transfer` for this feature |
| `EntityId` | `string` | Non-null, max 128 | Composite index with `EntityType`, `Timestamp` | String form of the affected entity ID |

**Audit payload expectation**:
- Exchange-rate create and update entries must include the before and after rate values in
  structured log context or an implementation-specific metadata payload associated with the audit event.
- Transfer attempt entries must include source and destination currencies, source and destination
  amounts, and the applied rate ID when present.

---

## Relationships and Cardinality

| Relationship | Cardinality | Notes |
|--------------|-------------|-------|
| `Account` -> `Transfer` (source) | 1-to-many | One account can originate many transfers; each transfer has exactly one source account |
| `Account` -> `Transfer` (destination) | 1-to-many | One account can receive many transfers; each transfer has exactly one destination account |
| `ExchangeRate` -> `Transfer` | 1-to-many (optional on transfer side) | One rate snapshot can be referenced by many cross-currency transfers; same-currency transfers have no rate FK |
| `ExchangeRate` -> `ExchangeRate` (`SupersededBy`) | 0..1 successor / 0..1 predecessor per row | Creates a forward-linked history chain for each currency pair |
| `Transfer` / `ExchangeRate` -> `AuditLogEntry` | 1-to-many logical relationship | Audit rows reference affected entities through `EntityType` + `EntityId` rather than hard FKs |

Relationship sketch:

```text
Account (1) ----< Transfer >---- (1) Account
                    |
                    | 0..1
                    v
              ExchangeRate
                    |
                    | 0..1 SupersededBy
                    v
              ExchangeRate

Transfer / ExchangeRate ----< AuditLogEntry (logical reference via EntityType + EntityId)
```

---

## State Transitions

### ExchangeRate lifecycle

```text
[Active / Current]
      |
      | Admin creates a new rate for the same pair
      v
[Superseded]
```

**Rules**:
- A new row is inserted in `Active / Current` state.
- The previously active row for that pair transitions to `Superseded` by setting `SupersededBy`.
- Superseded rows are immutable history and never become current again.

### Transfer lifecycle

```text
[Pending]
   |\
   | \__ validation or business rule failure, no rate, insufficient funds, zero destination,
   |     overflow, or authorization failure after persistence starts
   |
   |------> [Failed]
   |
   | successful atomic debit + credit + audit write
   v
[Completed]
```

**Rules**:
- `Pending` exists only within the transaction boundary or as an explicitly persisted in-progress
  record, depending on implementation.
- Terminal states are immutable.
- Idempotent replay does not create a new state transition; it returns the previously stored
  `Completed` or `Failed` record.

---

## Validation Rules Mapped to FRs

| FR | Validation or Enforcement Rule | Affected Entity or Operation |
|----|-------------------------------|------------------------------|
| FR-001 | `SourceCurrency` and `TargetCurrency` must be supported ISO 4217 codes, must differ, and `Rate` must be positive with scale <= 6 | `POST /v1/exchange-rates`, `ExchangeRate` |
| FR-002 | Reject same-currency rate submissions and non-positive rates with `400 Bad Request` | `POST /v1/exchange-rates` |
| FR-003 | Only authenticated callers may retrieve current rates | `GET /v1/exchange-rates/{source}/{target}` |
| FR-004 | Missing current rate for a pair returns `404 Not Found` | Rate lookup |
| FR-005 | Updating a rate inserts a new row and sets `SupersededBy` on the old current row | `ExchangeRate` write path |
| FR-006 | Cross-currency transfer requires an active rate for `SourceCurrency -> DestinationCurrency` | `POST /v1/transfers` |
| FR-007 | `DestinationAmount = floor(SourceAmount x Rate)` using fixed-point decimal arithmetic | Transfer calculation |
| FR-008 | Reject transfer when calculated `DestinationAmount = 0` | Transfer validation |
| FR-009 | Persist `AppliedExchangeRateId` on cross-currency transfers and snapshot the source and destination amounts and currencies | `Transfer` |
| FR-010 | Resolve the current rate once at transfer initiation and persist its ID; later rate changes do not alter the transfer | Transfer orchestration |
| FR-011 | Enforce uniqueness of `IdempotencyKey`; repeated key returns the original terminal transfer | `Transfer` + unique index |
| FR-012 | Debit, credit, transfer persistence, and audit write must commit or roll back together | Transaction boundary |
| FR-013 | Every rate create and update writes an `AuditLogEntry` with controlled `Operation_Type` and trace metadata | `AuditLogEntry` |
| FR-014 | Every cross-currency transfer attempt (success or rejection) writes an `AuditLogEntry` | `AuditLogEntry` |
| FR-015 | Rates are directional; `USD->EUR` and `EUR->USD` are separate rows with separate lifecycles | `ExchangeRate` |

---

## Index Strategy

### Required indexes

| Entity | Index | Type | Purpose |
|--------|-------|------|---------|
| `ExchangeRate` | `(SourceCurrency, TargetCurrency) WHERE SupersededBy IS NULL` | Unique filtered | Guarantees a single current rate per directed pair |
| `ExchangeRate` | `(SourceCurrency, TargetCurrency, SupersededBy)` INCLUDE (`Rate`, `EffectiveFrom`, `CreatedBy`) | Non-unique covering | Fast current-rate lookup for `GET /v1/exchange-rates/{source}/{target}` and transfer initiation |
| `ExchangeRate` | `(SupersededBy)` | Non-unique | Supports history traversal and integrity checks |
| `Transfer` | `(IdempotencyKey)` | Unique | Idempotent replay lookup |
| `Transfer` | `(SourceAccountId, CreatedAt)` | Non-unique | Source-account transfer history |
| `Transfer` | `(DestinationAccountId, CreatedAt)` | Non-unique | Destination-account transfer history |
| `Transfer` | `(AppliedExchangeRateId)` | Non-unique | Reconciliation from a rate row to all affected transfers |
| `AuditLogEntry` | `(Operation_Type, Timestamp)` | Non-unique | Audit and event investigations by type over time |
| `AuditLogEntry` | `(EntityType, EntityId, Timestamp)` | Non-unique | Fast retrieval of audit trail for one transfer or rate |
| `AuditLogEntry` | `(CorrelationId)` | Non-unique | Request trace lookup |

### Performance notes

- The filtered unique index is the main correctness guard for concurrent rate updates.
- The covering lookup index avoids key lookups when the service only needs the current rate value,
  effective timestamp, and creator metadata.
- `Transfer.AppliedExchangeRateId` is indexed to support post-hoc reconciliation and audit joins.

---

## AuditLog Operation Types (Controlled Vocabulary)

| Operation_Type | Trigger |
|----------------|---------|
| `ExchangeRateCreated` | A new directed pair is created with no previously current rate |
| `ExchangeRateUpdated` | A previously current rate is superseded by a newly inserted current rate |
| `CrossCurrencyTransferCompleted` | A cross-currency transfer commits successfully |
| `CrossCurrencyTransferRejected` | A cross-currency transfer attempt fails validation or business rules and is recorded as failed |

These values are closed vocabulary for this feature and MUST NOT be replaced with free text.