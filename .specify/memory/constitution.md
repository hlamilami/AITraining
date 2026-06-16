<!--
SYNC IMPACT REPORT
==================
Version change: 1.0.0 → 1.1.0  [MINOR — material expansion of Principle IV]
Added sections:
  - Principle IV: mandatory AuditLog table schema (Operation_Type, Operation_Id, Initiator, Timestamp)
  - Principle IV: universal action coverage rule (every system action must produce an audit entry)
  - Principle IV: append-only enforcement and retention rules
Modified principles:
  - IV. Full Auditability & Observability — significantly expanded with prescriptive schema and scope
Removed sections: N/A
Templates checked:
  - .specify/templates/plan-template.md     ✅ Constitution Check gate updated; audit table gate added
  - .specify/templates/spec-template.md     ✅ Requirements section guidance updated
  - .specify/templates/tasks-template.md    ✅ Foundational phase now includes audit table task
Deferred TODOs: none
-->

# Banking Backend Constitution

## Core Principles

### I. Security by Design (NON-NEGOTIABLE)

Every feature MUST be designed with security as a first-class constraint, not an afterthought.

- All API endpoints MUST enforce authentication (e.g., JWT/OAuth2) and role-based authorization.
- Sensitive data (PII, card data, credentials) MUST be encrypted at rest (AES-256 or equivalent)
  and in transit (TLS 1.2+).
- Secrets (API keys, DB passwords, signing keys) MUST NEVER be hardcoded or committed to source
  control; use a secrets manager (e.g., Vault, AWS Secrets Manager).
- All inputs MUST be validated and sanitized; parameterized queries MUST be used to prevent
  SQL injection.
- OWASP Top 10 compliance is mandatory for every release.
- The principle of least privilege MUST be applied to all service accounts, IAM roles, and
  database users.

**Rationale**: Banking systems are high-value targets. A single security breach causes financial
loss, regulatory penalties, and irreversible reputational damage.

### II. Data Integrity & ACID Compliance (NON-NEGOTIABLE)

Financial data MUST always be correct, consistent, and durable.

- All financial operations (transfers, credits, debits) MUST execute within ACID-compliant
  database transactions; partial writes are never acceptable.
- Idempotency MUST be enforced on all financial mutation endpoints via idempotency keys.
- Double-entry bookkeeping rules MUST be enforced: every debit MUST have a corresponding credit.
- Concurrent account writes MUST use optimistic locking or row-level locking to prevent
  race conditions.
- Monetary amounts MUST be stored and computed using fixed-point arithmetic (e.g., integer cents
  or `DECIMAL`); floating-point types are PROHIBITED for money.

**Rationale**: Loss or corruption of financial data is catastrophic. Correctness is non-negotiable
and supersedes performance optimization.

### III. Compliance & Regulatory Adherence (NON-NEGOTIABLE)

All features MUST meet applicable legal and regulatory requirements before reaching production.

- Each feature specification MUST document applicable regulations (PCI-DSS, KYC/AML, GDPR,
  local banking law) in its Requirements section.
- PII MUST be identified, classified, and handled per data residency and retention requirements.
- Any feature touching card or payment data MUST be assessed for PCI-DSS scope before
  implementation begins.
- Audit logs MUST be immutable and retained for the legally required period (minimum 7 years
  unless jurisdiction mandates otherwise).
- Sanctions/OFAC screening MUST be applied to all counterparties before processing any
  transaction.

**Rationale**: Non-compliance results in regulatory fines, licence revocation, and criminal
liability. Compliance gates MUST be part of the definition of done.

### IV. Full Auditability & Observability (NON-NEGOTIABLE)

Every system action — without exception — MUST be fully traceable through a canonical
`AuditLog` table. Observability infrastructure MUST be in place before any business feature
is implemented.

#### Mandatory AuditLog Table

Every service MUST maintain a dedicated `AuditLog` table (or collection) with **at minimum**
the following columns. Additional columns are permitted but these four are NON-NEGOTIABLE:

| Column | Type | Rules |
|--------|------|-------|
| `Operation_Id` | UUID / GUID | Primary key; system-generated; unique per entry |
| `Operation_Type` | String (non-null) | Identifies what happened (e.g., `AccountCreated`, `TransferCompleted`, `LoginAttempt`, `BalanceQueried`). Use a controlled vocabulary — free-text values are PROHIBITED. |
| `Initiator` | String (non-null) | Identity of the actor who triggered the action. MUST be sourced from the authenticated JWT `sub` claim, system service account name, or scheduler identity. Anonymous or empty values are PROHIBITED. |
| `Timestamp` | DateTimeOffset / UTC | Exact UTC time the action was processed. MUST be server-assigned; client-supplied timestamps are PROHIBITED. |

**Recommended additional columns** (MUST be included wherever technically feasible):
- `CorrelationId` — links the audit entry to the originating request trace.
- `EntityType` — the domain object affected (e.g., `Account`, `Transfer`).
- `EntityId` — the identifier of the specific entity instance.
- `BeforeState` — JSON snapshot of entity state before the operation.
- `AfterState` — JSON snapshot of entity state after the operation.

#### Universal Coverage Rule

The following rule applies to **every action in the system** — not only financial mutations:

> **Every operation that creates, modifies, queries, or deletes data, or that represents a
> meaningful system event, MUST produce exactly one `AuditLog` entry before the operation
> is considered complete.**

This includes but is not limited to:
- Account creation, balance changes, fund transfers (financial state mutations)
- Read operations on sensitive data (e.g., balance queries, account lookups)
- Authentication and authorization events (login, token refresh, access denied)
- Administrative actions (configuration changes, role assignments)
- System-initiated operations (scheduled jobs, retries, compensating transactions)

Failure to write an audit entry MUST cause the enclosing operation to fail; partial
operations with no audit trail are PROHIBITED.

#### Immutability & Retention

- The `AuditLog` table MUST be append-only: `UPDATE` and `DELETE` statements targeting
  audit rows are PROHIBITED at the application layer.
- At the database layer, row-level security or trigger-based guards MUST enforce
  append-only access in non-development environments.
- Audit records MUST be retained for a minimum of 7 years (or longer if jurisdiction
  requires), consistent with Principle III (Compliance & Regulatory Adherence).

#### Observability Infrastructure

- Structured JSON logging MUST be used throughout; free-text log lines are NOT acceptable
  in production code.
- Distributed tracing correlation IDs MUST be generated at ingress and propagated to all
  downstream service calls, log entries, and `AuditLog.CorrelationId`.
- All services MUST expose latency (p50/p95/p99), error-rate, and saturation metrics to
  the observability platform.
- Silent failures are PROHIBITED; every caught exception MUST be logged with sufficient
  context to reproduce the issue.

**Rationale**: Auditability is both a regulatory requirement and an operational necessity.
A consistent, machine-readable audit table enables fraud investigation, incident response,
compliance reporting, and forensic analysis. Without universal coverage, gaps in the audit
trail become attack vectors and compliance liabilities.

### V. Test-First Development (NON-NEGOTIABLE)

No production code may be written before the failing tests that describe its behaviour exist.

- TDD is mandatory: tests MUST be written and confirmed to fail before implementation begins
  (Red → Green → Refactor).
- Unit test coverage MUST be ≥ 80% for all business-logic modules; coverage gates are enforced
  in CI.
- Contract tests MUST be written for every inter-service API boundary.
- Integration tests MUST cover all critical financial workflows end-to-end (e.g., fund transfer,
  account creation, statement generation).
- Tests MUST be deterministic; time-dependent or network-dependent tests MUST use mocks/fakes.

**Rationale**: Financial bugs can cause direct monetary loss. Test-first practices catch
regressions before they reach production, reducing the cost of defects by orders of magnitude.

### VI. API Versioning & Zero-Downtime Deployments

APIs and deployments MUST be managed to eliminate breaking changes and service interruptions.

- All public APIs MUST be versioned with a path prefix (e.g., `/v1/accounts`).
- Breaking changes MUST increment the major version; a deprecation notice MUST be published
  at least 90 days before a previous version is retired.
- Database schema migrations MUST be backward-compatible; additive-only changes MUST be
  applied before code deployment; destructive changes require a multi-phase migration plan.
- Production releases MUST use blue/green or canary deployment strategies.
- Feature flags MUST be used to control rollout of risky changes in production.

**Rationale**: Banking clients (mobile apps, third-party integrations) cannot be force-updated.
Downtime directly translates to lost transactions and SLA penalties.

### VII. Simplicity & YAGNI

Complexity MUST be justified by a concrete, present problem — never a hypothetical future need.

- Every non-trivial abstraction (repository pattern, event bus, CQRS, etc.) MUST include
  documented rationale explaining why simpler alternatives were rejected.
- YAGNI (You Aren't Gonna Need It) MUST be applied; speculative generality is a code smell.
- Prefer proven, "boring" technology for core financial data flows over novel solutions.
- The Complexity Tracking table in `plan.md` MUST be completed for any pattern that violates
  this principle.

**Rationale**: Accidental complexity hides bugs, slows onboarding, and increases the attack
surface. In banking systems, clarity and auditability outweigh clever design.

## Security & Compliance Standards

All development MUST conform to the following standards and policies:

**Regulatory Frameworks**
- PCI-DSS (v4.0 or current) for any feature touching card data or payment processing.
- KYC/AML requirements as mandated by the operating jurisdiction.
- GDPR / local data-protection law for all PII handling.
- DORA (Digital Operational Resilience Act) for EU-regulated entities.

**Technical Security Baselines**
- Transport: TLS 1.2 minimum; TLS 1.3 preferred. Self-signed certificates are PROHIBITED
  in non-development environments.
- Authentication: OAuth 2.0 / OpenID Connect with short-lived access tokens (≤ 15 min TTL).
- Password hashing: bcrypt (cost ≥ 12) or Argon2id.
- Dependency scanning: MUST run on every CI build; critical CVEs MUST block merge.
- SAST/DAST: Static analysis MUST run on every PR; dynamic scanning MUST run before each
  production release.

**Performance Baselines**
- Core transaction API p95 latency: ≤ 200 ms under normal load.
- System availability SLA: ≥ 99.95% monthly uptime.
- Recovery Time Objective (RTO): ≤ 4 hours; Recovery Point Objective (RPO): ≤ 1 hour.

## Development Workflow & Quality Gates

The following gates are MANDATORY before any code merges to the main branch:

**Pull Request Requirements**
1. All CI checks pass (build, lint, unit tests, contract tests, SAST).
2. Minimum two approvals from engineers, at least one of whom has domain expertise in the
   changed area.
3. Security review required for any change touching authentication, authorization,
   cryptography, or PCI-DSS scope.
4. Compliance sign-off required for any change affecting audit logging, data retention,
   or regulatory reporting.

**Definition of Done (per feature)**
- [ ] All acceptance scenarios from `spec.md` pass.
- [ ] Unit coverage ≥ 80% for new/changed business-logic modules.
- [ ] Contract tests written and passing for all new API boundaries.
- [ ] Integration tests cover the critical path end-to-end.
- [ ] Audit logging verified for every new state-change operation; **every action has an `AuditLog` entry with `Operation_Type`, `Operation_Id`, `Initiator`, and `Timestamp`**.
- [ ] Regulatory requirements documented and confirmed met.
- [ ] `plan.md` Constitution Check section completed without unresolved violations.
- [ ] Performance tested under expected load; baselines met.
- [ ] No secrets committed; dependency scan clean.

**Branch Strategy**
- `main` — production-ready; protected; deploy-on-merge via CI/CD.
- `release/*` — release stabilisation; no feature work.
- Feature branches follow the naming convention: `###-feature-name`.

## Governance

This constitution supersedes all other practices, coding standards, and informal agreements.
Any conflict between this document and another guideline is resolved in favour of this document.

**Amendment Procedure**
1. Open a PR with the proposed change to `.specify/memory/constitution.md`.
2. Provide written rationale and a version bump justification (MAJOR / MINOR / PATCH).
3. Obtain approval from at least two senior engineers and the engineering lead.
4. Update `LAST_AMENDED_DATE` and `CONSTITUTION_VERSION` upon merge.
5. Propagate changes to all dependent templates (run `/speckit-constitution` to validate).

**Versioning Policy** (Semantic Versioning)
- **MAJOR**: Removal or redefinition of a principle; backward-incompatible governance change.
- **MINOR**: New principle or section added; material expansion of guidance.
- **PATCH**: Clarifications, wording refinements, typo fixes.

**Compliance Review**
- Constitution compliance MUST be reviewed at the start of every feature via the
  "Constitution Check" gate in `plan.md`.
- A full compliance audit MUST be performed at least quarterly.
- Any observed violation MUST be raised as a severity-1 issue and resolved before the next
  production release.

**Version**: 1.1.0 | **Ratified**: 2026-06-15 | **Last Amended**: 2026-06-15
