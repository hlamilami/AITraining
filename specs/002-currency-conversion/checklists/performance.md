# Performance Requirements Quality Checklist: Currency Conversion & Cross-Currency Transfers

**Purpose**: Rigorous gate — validate completeness, clarity, consistency, and measurability of latency & throughput requirements before planning begins
**Created**: 2026-06-16
**Feature**: [spec.md](../spec.md)
**Depth**: Rigorous gate (all items must be resolved before planning proceeds)
**Focus**: Latency & throughput requirements quality

---

## Performance Target Completeness

- [ ] CHK001 - Are performance targets defined for the exchange rate history retrieval operation (listing previous rates for a currency pair)? No latency target exists for this operation in the spec. [Gap]
- [ ] CHK002 - Are latency requirements specified for the idempotency key lookup that every cross-currency transfer must perform before processing (FR-011)? [Gap, Spec §FR-011]
- [ ] CHK003 - Is a performance target defined for the error/rejection path of a cross-currency transfer (e.g., insufficient funds, no rate found) — not just the success path? [Gap, Spec §SC-003]
- [ ] CHK004 - Is a performance target defined for the rate-snapshot / rate-lock step within the transfer initiation flow (FR-010), isolated from the overall transfer latency? [Gap, Spec §FR-010]
- [ ] CHK005 - Are throughput requirements defined for bulk or concurrent exchange rate read scenarios (e.g., client apps displaying a full rate table for all supported pairs simultaneously)? [Gap]

---

## Performance Target Clarity

- [ ] CHK006 - Is "normal operating load" in SC-001 quantified with explicit concurrent request or TPS values so the 2-second target is reproducibly testable? [Ambiguity, Spec §SC-001]
- [ ] CHK007 - Is the 2-second target in SC-001 specified as a p99 percentile, a mean, or an absolute maximum? The current wording "under 2 seconds for 99% of requests" implies p99, but this should be stated explicitly. [Clarity, Spec §SC-001]
- [ ] CHK008 - Is the 500 ms target in SC-002 qualified as p99? Are p50 and p95 intermediate targets also specified for exchange rate retrieval? [Clarity, Spec §SC-002]
- [ ] CHK009 - Is "end-to-end" in SC-003 precisely scoped — does it cover server-side processing only, or the full round-trip from client submission to confirmed response? [Ambiguity, Spec §SC-003]
- [ ] CHK010 - Is the 3-second SC-003 target broken into individual step-level latency budgets (e.g., rate lock ≤ Xms, debit ≤ Yms, credit ≤ Zms, audit write ≤ Wms) to enable targeted optimization? [Clarity, Spec §SC-003]
- [ ] CHK011 - Is "normal load" for cross-currency transfers (SC-003) quantified with a specific concurrency level (e.g., N concurrent transfers) so the 3-second target becomes independently testable? [Clarity, Spec §SC-003]

---

## Performance Target Consistency

- [ ] CHK012 - Does the 3-second SC-003 target for cross-currency transfers conflict with the Constitution's p95 ≤ 200 ms baseline for core transaction APIs, and is any exception or exemption explicitly documented in the spec? [Conflict, Spec §SC-003, Constitution §Performance Baselines]
- [ ] CHK013 - Is the 500 ms SC-002 retrieval target consistent with the Constitution's p95 ≤ 200 ms API baseline, and if exchange rate retrieval is classified as a non-core endpoint, is this classification documented? [Conflict, Spec §SC-002, Constitution §Performance Baselines]
- [ ] CHK014 - Is the assumption that cross-currency transfers inherit the Fund Transfer Service's load profile (500 concurrent balance inquiries, 100 concurrent transfers) formally validated in the spec rather than only referenced as an assumption? [Consistency, Spec §Assumptions]
- [ ] CHK015 - Are the performance targets in SC-001–SC-003 consistent with the Constitution's system availability SLA (≥ 99.95% monthly uptime) — i.e., does meeting the latency targets at the stated load also satisfy the uptime constraint? [Consistency, Constitution §Performance Baselines]

---

## Load Definition

- [ ] CHK016 - Is "normal operating load" for exchange rate writes (SC-001) defined with explicit metrics (e.g., max concurrent admin sessions, write TPS) since admin operations may have a different load profile than end-user operations? [Completeness, Spec §SC-001]
- [ ] CHK017 - Are distinct load profiles defined for peak vs. normal conditions — e.g., is there a burst throughput requirement for exchange rate reads during high-frequency trading windows or month-end reconciliation? [Gap, Spec §SC-002]
- [ ] CHK018 - Is the load definition for cross-currency transfers specified independently from same-currency transfer load, given that cross-currency transfers involve additional steps (rate lookup, rate lock, two-currency ledger writes)? [Gap, Spec §SC-003]

---

## Latency Percentile Specificity

- [ ] CHK019 - Are p50 and p95 latency targets defined for each operation class (rate create, rate retrieve, cross-currency transfer) in addition to the p99 targets stated in SC-001–SC-003? [Gap, Completeness, Spec §SC-001–§SC-003]
- [ ] CHK020 - Is there a requirement for the observability platform to capture and expose p50/p95/p99 latency metrics for each operation class, as mandated by the Constitution's observability infrastructure requirements? [Coverage, Gap, Constitution §Observability Infrastructure]

---

## Throughput & Concurrency Requirements

- [ ] CHK021 - Are maximum sustained throughput requirements (e.g., requests/sec or TPS) specified for exchange rate creation/update as a distinct metric from the latency target in SC-001? [Gap, Completeness]
- [ ] CHK022 - Are concurrency requirements for simultaneous exchange rate reads and writes specified — e.g., does read throughput degrade when an admin is updating a rate, and is that degradation bounded? [Gap, Spec §FR-003, §FR-001]

---

## Contention & Lock Performance

- [ ] CHK023 - Are the performance implications of the rate-locking mechanism in FR-010 specified — for example, does acquiring a snapshot lock on the exchange rate row become a throughput bottleneck at high transfer concurrency, and is a maximum lock acquisition time defined? [Gap, Spec §FR-010]
- [ ] CHK024 - Is the serialization overhead for the "last-write-wins" concurrent administrator update scenario (Edge Cases) bounded by a latency requirement, so that the optimistic/pessimistic locking strategy can be selected with performance evidence? [Gap, Spec §Edge Cases]
- [ ] CHK025 - Are latency requirements defined for idempotency key lookups, given that every cross-currency transfer invocation must query the idempotency store as a prerequisite step (FR-011)? [Gap, Spec §FR-011]

---

## Acceptance Criteria Measurability

- [ ] CHK026 - Are the performance success criteria in SC-001–SC-003 required to be enforced as automated load-test pass/fail gates in CI, or are they only post-deployment operational monitoring targets? The distinction determines whether they block a release. [Measurability, Gap, Spec §SC-001–§SC-003]
- [ ] CHK027 - Are baseline performance benchmarks established (e.g., from the existing Fund Transfer Service) against which regressions in SC-001–SC-003 can be automatically detected during development? [Gap, Completeness]
- [ ] CHK028 - Can the "99% of requests" threshold in SC-001–SC-003 be objectively measured with a specified observation window and minimum sample size (e.g., p99 over a 1-minute window with ≥ 1,000 requests)? [Measurability, Clarity, Spec §SC-001–§SC-003]

---

## Notes

- Check items off as completed: `[x]`
- Add findings or resolution notes inline after each item
- Items **CHK012** and **CHK013** are potential conflicts with the Constitution — they MUST be resolved with explicit documented exceptions or spec corrections before planning proceeds
- Items **CHK006**, **CHK009**, **CHK011** are ambiguity risks that will directly affect how load tests are designed; resolve before any performance test plan is written
- All 28 items are mandatory gate items for this rigorous pre-planning review
