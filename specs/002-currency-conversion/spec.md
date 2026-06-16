# Feature Specification: Currency Conversion & Cross-Currency Transfers

**Feature Branch**: `002-currency-conversion`

**Created**: 2026-06-15

**Status**: Draft

**Input**: User description: "currency conversion table and an ability to transfer money between
different-currency accounts"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Manage Exchange Rates (Priority: P1)

A system administrator needs to define and maintain the exchange rates between supported
currency pairs. They provide a source currency, a target currency, and the applicable rate.
The system stores the rate and makes it immediately available for transfer calculations.

**Why this priority**: Without exchange rates in the system, no cross-currency transfer can
be processed. This is the foundational capability that all other stories depend on. It must
be in place and independently testable before any transfer logic is built.

**Independent Test**: Can be fully tested by submitting a valid exchange rate entry
(e.g., USD → EUR at 0.920000) and verifying the system confirms the rate is stored and
retrievable. No transfer needs to exist to validate this story.

**Acceptance Scenarios**:

1. **Given** an authenticated administrator with the `exchange-rate:admin` scope, **When**
   they submit a valid source currency, target currency, and positive rate, **Then** the
   system stores the rate and returns a confirmation including the rate ID, currency pair,
   rate value, and the timestamp it became effective.
2. **Given** an existing exchange rate for a currency pair, **When** the administrator
   submits a new rate for the same pair, **Then** the system replaces the current rate,
   preserves the previous rate in history for audit purposes, and the new rate is used for
   all subsequent transfers.
3. **Given** a rate submission with the same source and target currency (e.g., USD → USD),
   **When** submitted, **Then** the system rejects it with a validation error.
4. **Given** a rate submission with a non-positive rate value (zero or negative), **When**
   submitted, **Then** the system rejects it with a validation error.
5. **Given** an authenticated user without the `exchange-rate:admin` scope, **When** they
   attempt to create or update an exchange rate, **Then** the system rejects the request
   with an authorisation error.

---

### User Story 2 - Retrieve Exchange Rates (Priority: P2)

An account owner or operator wants to check the current exchange rate between two currencies
before initiating a cross-currency transfer. They query the system for the rate and receive
the current value along with the effective date.

**Why this priority**: Users need visibility into exchange rates to make informed transfer
decisions and to verify the rate that will be applied to their transfer. This is also
required for displaying rate information in any client application.

**Independent Test**: Can be fully tested by first creating an exchange rate (US1), then
querying it by currency pair and verifying the returned rate, effective timestamp, and
currency details match what was stored.

**Acceptance Scenarios**:

1. **Given** an existing exchange rate for a currency pair (e.g., USD → EUR), **When**
   any authenticated user queries that pair, **Then** the system returns the current rate,
   the source and target currencies, the rate value, and the effective-from timestamp.
2. **Given** no exchange rate exists for a requested currency pair, **When** a user queries
   it, **Then** the system returns a not-found error.
3. **Given** an exchange rate was updated (US1-S2), **When** a user queries the pair,
   **Then** the system returns the most recently set rate, not the historical one.

---

### User Story 3 - Transfer Funds Between Different-Currency Accounts (Priority: P3)

An account owner wants to send money from their account in one currency to an account
denominated in a different currency. The system applies the current exchange rate,
debits the source in its currency, credits the destination in its currency, and confirms
the amounts and rate used.

**Why this priority**: This is the primary business capability the feature exists to deliver.
It builds on exchange rate management (US1) and retrieval (US2) and represents the highest
user value.

**Independent Test**: Can be fully tested by creating two accounts with different currencies
(e.g., USD and EUR), setting a USD → EUR rate, executing a cross-currency transfer, then
verifying the source is debited in USD and the destination is credited in the equivalent
EUR amount (rounded to the nearest minor unit). The transfer record must include the applied
rate.

**Acceptance Scenarios**:

1. **Given** a USD account with sufficient balance and an EUR account, and a current
   USD → EUR exchange rate, **When** a transfer of a valid positive USD amount is requested,
   **Then** the source account is debited by the exact USD amount, the destination account
   is credited by the converted EUR amount (source amount × rate, rounded to nearest minor
   unit), and the transfer record captures both amounts and the applied rate.
2. **Given** a cross-currency transfer request where no exchange rate exists for the
   currency pair, **When** submitted, **Then** the system rejects the transfer with a
   clear no-rate-available error and neither balance changes.
3. **Given** a source account with insufficient balance for the requested transfer amount,
   **When** a cross-currency transfer is submitted, **Then** the system rejects it with an
   insufficient-funds error and neither balance changes.
4. **Given** two accounts in the same currency, **When** a transfer is submitted via the
   cross-currency endpoint, **Then** the system processes it as a same-currency transfer
   (no conversion applied, rate = 1.0) without error.
5. **Given** a cross-currency transfer request, **When** the exchange rate changes between
   the time the user views the rate and the time the transfer is processed, **Then** the
   rate locked at the moment of transfer submission is the one applied; the user is shown
   the actual applied rate in the response.
6. **Given** an identical cross-currency transfer submitted twice with the same idempotency
   key, **When** the second request is received, **Then** the system returns the original
   result without processing a second conversion or debit.
7. **Given** a cross-currency transfer where the converted destination amount rounds to zero
   minor units, **When** submitted, **Then** the system rejects it with a validation error
   (minimum transfer must result in at least 1 minor unit at the destination).

---

### Edge Cases

- What happens if the system crashes between debiting the source and crediting the
  destination in a cross-currency transfer?
  → The entire operation must be rolled back; neither balance changes and no partial
  transfer record is committed.
- What happens if two administrators update the same exchange rate simultaneously?
  → The last-write-wins; the system must preserve both entries in the rate history so
  neither update is silently lost from the audit trail.
- What is the maximum precision required for exchange rates?
  → Rates must support at least 6 decimal places (e.g., 0.920345) to avoid rounding
  errors on large transfer amounts.
- What happens if a cross-currency transfer produces a converted amount that exceeds the
  destination account's representable maximum balance?
  → The system must reject the transfer with an overflow error before any funds move.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow users with the `exchange-rate:admin` scope to create an
  exchange rate entry for any valid currency pair, specifying source currency (ISO 4217),
  target currency (ISO 4217), and a positive rate value with up to 6 decimal places.
- **FR-002**: System MUST reject exchange rate submissions where source and target currency
  are identical, or where the rate is zero or negative.
- **FR-003**: System MUST allow any authenticated user to retrieve the current exchange rate
  for a given source–target currency pair.
- **FR-004**: System MUST return a not-found error when no exchange rate exists for a
  requested currency pair.
- **FR-005**: System MUST store exchange rate history; when a rate is updated, the previous
  rate entry MUST be retained and immutably preserved for audit purposes.
- **FR-006**: System MUST allow fund transfers between accounts with different currencies,
  provided a current exchange rate exists for the source → target currency pair.
- **FR-007**: System MUST calculate the destination credit amount as:
  `floor(sourceAmount × rate)` in the destination currency's minor units, ensuring no
  fractional minor units are created.
- **FR-008**: System MUST reject a cross-currency transfer if the calculated destination
  amount is zero (i.e., the source amount is too small for the current rate).
- **FR-009**: System MUST record the applied exchange rate value and rate ID in the transfer
  record so the exact rate used is permanently traceable.
- **FR-010**: System MUST lock the exchange rate at the moment the transfer is initiated;
  rate changes occurring after submission MUST NOT affect an in-flight transfer.
- **FR-011**: System MUST enforce idempotency on cross-currency transfers via the same
  caller-supplied idempotency key mechanism as same-currency transfers.
- **FR-012**: System MUST process cross-currency transfers atomically — either both the
  debit (in source currency) and credit (in target currency) succeed, or neither is applied.
- **FR-013**: System MUST record an `AuditLog` entry for every exchange rate creation or
  update, capturing `Operation_Type` (e.g., `ExchangeRateCreated`, `ExchangeRateUpdated`),
  `Operation_Id`, `Initiator`, and `Timestamp`, plus before/after rate values.
- **FR-014**: System MUST record an `AuditLog` entry for every cross-currency transfer
  attempt (successful or rejected), including the applied rate.
- **FR-015**: Exchange rates MUST be stored unidirectionally — USD→EUR and EUR→USD are
  independent entries that administrators manage separately. This ensures rate accuracy
  (market rates are not exact inverses) and gives administrators full control over both
  directions.

### Key Entities

- **ExchangeRate**: Unique identifier, source currency (ISO 4217), target currency
  (ISO 4217), rate value (positive, up to 6 decimal places), effective-from timestamp
  (UTC), created-by (actor identity), superseded-by (reference to newer rate, if any).
- **Modified Transfer**: Extends the existing Transfer entity with: applied exchange rate
  ID (nullable — null for same-currency transfers), source amount (in source currency
  minor units), destination amount (in destination currency minor units).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An administrator can create or update an exchange rate in under 2 seconds
  for 99% of requests under normal operating load.
- **SC-002**: Any authenticated user can retrieve the current exchange rate for a currency
  pair in under 500 milliseconds for 99% of requests.
- **SC-003**: A cross-currency transfer completes end-to-end (both balances updated and
  confirmed, applied rate recorded) in under 3 seconds for 99% of requests under normal
  load.
- **SC-004**: Zero instances of cross-currency transfers where the source is debited but
  the destination is not credited (or vice versa), verifiable via audit log.
- **SC-005**: Zero instances of a transfer being processed with an exchange rate that was
  not active at the time the transfer was initiated.
- **SC-006**: 100% of exchange rate changes produce an immutable history entry; no rate
  update silently overwrites the previous value without preserving the old one.
- **SC-007**: The applied exchange rate is present in 100% of cross-currency transfer
  records, enabling full post-hoc reconciliation.
- **SC-008**: Exchange rates do not expire automatically. Rates remain valid until an
  administrator replaces them. The system MUST display the effective-from timestamp on
  every rate so operators can identify stale rates. Automatic rate expiry is out of scope
  for this version.

## Assumptions

- Exchange rates are entered manually by administrators; automated feed from a market data
  provider is out of scope for this version.
- Exchange rate precision is fixed at 6 decimal places; higher precision is not required.
- The converted destination amount is calculated using `floor()` rounding (truncation toward
  zero) to avoid crediting more than the exact converted value. Regulatory rounding
  requirements are handled by the rate itself.
- The same supported currency list as the existing Fund Transfer Service applies
  (USD, EUR, GBP, SAR, AED); adding new currencies requires only adding to the supported
  list, not a schema change.
- Exchange rates are stored unidirectionally by default (administrator assumption pending
  FR-015 clarification); cross-rate derivation (e.g., USD→GBP via USD→EUR→GBP) is out
  of scope.
- The `exchange-rate:admin` scope is a new, distinct scope from `transfer:admin`; a user
  may hold one, both, or neither.
- Historical exchange rates are retained indefinitely for audit and reconciliation purposes;
  there is no automatic expiry or archival in this version.
- Performance and concurrency targets from the existing Fund Transfer Service
  (SC-008: 500 concurrent balance inquiries, 100 concurrent transfers) apply to
  cross-currency transfers as well.
