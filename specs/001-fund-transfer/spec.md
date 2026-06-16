# Feature Specification: Fund Transfer Service

**Feature Branch**: `001-fund-transfer`

**Created**: 2026-06-15

**Status**: Draft

**Input**: User description: "Create a backend for a new Fund Transfer service. It should provide
a simple CRUD for creating a new account with a starting balance, retrieving an account balance,
and transferring funds between accounts. Each account has: number, currency, balance, owner."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Open a New Account (Priority: P1)

A new customer wants to open a bank account. They provide their identity (owner), select a
currency, and deposit an initial balance. The system creates the account, assigns a unique
account number, and confirms the account is ready to use.

**Why this priority**: Without accounts, no other operation is possible. This is the foundational
capability that every other story depends on for its own independent testing.

**Independent Test**: Can be fully tested by submitting a valid account creation request and
verifying the returned account record contains the assigned number, the supplied owner, currency,
and starting balance.

**Acceptance Scenarios**:

1. **Given** a valid owner identifier, a supported currency code, and a non-negative starting
   balance, **When** a new account is created, **Then** the system returns a unique account
   number, confirms the currency and owner, and records the exact starting balance.
2. **Given** an account creation request with a negative starting balance, **When** submitted,
   **Then** the system rejects the request with a clear validation error.
3. **Given** an account creation request with an unsupported or missing currency code, **When**
   submitted, **Then** the system rejects the request with a clear validation error.
4. **Given** a duplicate owner + account-number combination already exists, **When** a second
   creation request is made with the same number, **Then** the system rejects it with a
   conflict error.

---

### User Story 2 - Retrieve Account Balance (Priority: P2)

An account owner wants to check the current balance on their account. They provide the account
number and the system returns the up-to-date balance and account details.

**Why this priority**: Balance retrieval is the most frequently used read operation and is a
prerequisite for validating any transfer scenario. It can be developed and tested independently
as soon as accounts exist (US1).

**Independent Test**: Can be fully tested by creating an account (US1) and then querying its
balance, verifying the returned figure matches the recorded balance.

**Acceptance Scenarios**:

1. **Given** an existing account, **When** the balance is requested by account number, **Then**
   the system returns the current balance, currency, owner, and account number.
2. **Given** a non-existent account number, **When** the balance is requested, **Then** the
   system returns a not-found error.
3. **Given** a balance inquiry on an account that has had prior transfers, **When** the balance
   is requested, **Then** the returned balance reflects all completed transactions accurately.

---

### User Story 3 - Transfer Funds Between Accounts (Priority: P3)

An account owner initiates a funds transfer from their account to another account. The system
validates sufficient funds, debits the source, credits the destination atomically, and confirms
the outcome.

**Why this priority**: Fund transfer is the core business capability. It builds on accounts
(US1) and balance visibility (US2) and represents the highest-value deliverable.

**Independent Test**: Can be fully tested by creating two accounts with known balances (US1),
executing a transfer, and then verifying both balances via retrieval (US2) match the expected
post-transfer values.

**Acceptance Scenarios**:

1. **Given** two existing accounts with the same currency and sufficient funds in the source,
   **When** a transfer is requested for a valid positive amount, **Then** the source balance
   decreases by exactly the transfer amount, the destination balance increases by exactly the
   transfer amount, and the operation is confirmed.
2. **Given** a source account with insufficient balance, **When** a transfer is requested,
   **Then** the system rejects the transfer with an insufficient-funds error and neither
   balance changes.
3. **Given** a transfer request where source and destination are the same account, **When**
   submitted, **Then** the system rejects the request with a validation error.
4. **Given** two accounts with different currencies, **When** a transfer is requested between
   them, **Then** the system rejects the transfer with a currency-mismatch error.
5. **Given** a transfer request with a zero or negative amount, **When** submitted, **Then**
   the system rejects it with a validation error.
6. **Given** a transfer request referencing a non-existent source or destination account,
   **When** submitted, **Then** the system returns a not-found error and no funds move.
7. **Given** an identical transfer request submitted twice with the same idempotency key,
   **When** the second request is received, **Then** the system returns the result of the
   first transfer without processing a second debit.

---

### Edge Cases

- What happens if the system crashes mid-transfer between debit and credit?
  → The operation must be rolled back; neither account should reflect a partial change.
- What happens when two transfers from the same account are submitted simultaneously?
  → Both must be validated against the balance at the point of processing; at most one
  should succeed if only one has sufficient funds.
- What is the maximum monetary amount the system must support?
  → The system must handle amounts up to at least 1 billion units in any supported currency
  without rounding or truncation.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow creation of a new account with an owner identifier, a currency
  (ISO 4217 code), and a non-negative starting balance; the system MUST auto-assign a unique
  account number.
- **FR-002**: System MUST reject account creation requests with negative starting balances or
  invalid/unsupported currency codes.
- **FR-003**: System MUST allow retrieval of an account's current balance and full account
  details (number, currency, balance, owner) by account number.
- **FR-004**: System MUST return a clear not-found error when an account retrieval is attempted
  for a non-existent account number.
- **FR-005**: System MUST allow fund transfers between two existing accounts of the same
  currency, given a positive transfer amount and sufficient source balance.
- **FR-006**: System MUST process each transfer atomically — either both the debit and credit
  succeed or neither is applied.
- **FR-007**: System MUST enforce idempotency on transfer requests; a duplicate transfer
  submitted with the same idempotency key MUST NOT produce a second debit/credit.
- **FR-008**: System MUST reject transfers between accounts of differing currencies.
- **FR-009**: System MUST reject transfers where source and destination account are identical.
- **FR-010**: System MUST reject transfers with zero or negative amounts.
- **FR-011**: System MUST record an immutable audit log entry for every account creation and
  every fund transfer, capturing the actor, timestamp, and before/after balances.
- **FR-012**: System MUST handle concurrent transfer requests against the same account without
  allowing balance inconsistencies or double-spend.
- **FR-013**: System MUST [NEEDS CLARIFICATION: Authorization scope — should fund transfers be
  restricted to the account owner only, or may any authenticated user initiate a transfer from
  any account? This affects the entire authorization model.]

### Key Entities

- **Account**: Unique account number (system-assigned), ISO 4217 currency code, balance
  (non-negative fixed-point monetary value), owner identifier (references the account holder).
- **Transfer**: Source account number, destination account number, amount, currency, timestamp
  (UTC), status (completed / rejected), idempotency key, failure reason (if rejected).
- **Audit Log Entry**: Entity type, entity identifier, actor identity, timestamp (UTC),
  operation type, before-state snapshot, after-state snapshot.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Account creation completes in under 2 seconds for 99% of requests under normal
  operating load.
- **SC-002**: Balance retrieval returns results in under 500 milliseconds for 99% of requests.
- **SC-003**: Fund transfers complete end-to-end (both balances updated and confirmed) in under
  3 seconds for 99% of requests under normal load.
- **SC-004**: Zero instances of partial transfers (debit without credit, or vice versa) under
  any failure scenario, verifiable via audit log.
- **SC-005**: Zero instances of duplicate debits when the same transfer is submitted more than
  once with an identical idempotency key.
- **SC-006**: The system correctly rejects 100% of transfers where source balance is
  insufficient, with no false approvals.
- **SC-007**: All account and transfer operations produce audit log entries; 100% traceability
  from operation to log entry.
- **SC-008**: The service handles at least 500 concurrent balance inquiries and 100 concurrent
  transfer requests without data inconsistency.

## Assumptions

- Account numbers are auto-generated by the system (not provided by the caller) to ensure
  global uniqueness and prevent enumeration attacks.
- All monetary amounts are treated as fixed-point values in the minor unit of the currency
  (e.g., cents for USD/EUR) to avoid floating-point rounding errors.
- Only same-currency transfers are supported in this version; multi-currency conversion is
  out of scope.
- The "owner" field is a free-form identifier (e.g., a user ID or name string) supplied by
  the caller; identity verification and KYC are handled by an upstream system outside this
  service's scope.
- An account may hold a balance of zero but MUST NOT go negative.
- Transfer idempotency is enforced via a caller-supplied idempotency key included in the
  transfer request.
- Deletion of accounts is out of scope for this version; once created, accounts persist.
- Updating account metadata (owner, currency) is out of scope; only balance changes via
  transfers are permitted post-creation.
- Authentication (verifying caller identity) is handled by an upstream API gateway or
  middleware; this service receives only pre-authenticated requests.
