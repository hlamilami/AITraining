# Quickstart Validation Guide: Currency Conversion & Cross-Currency Transfers

**Phase**: 1 - Design
**Date**: 2026-06-16
**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Data Model**: [data-model.md](data-model.md) | **Contract**: [contracts/openapi.yaml](contracts/openapi.yaml)

This guide validates the feature end-to-end against the contract and data model. It is a runtime
validation guide only; it intentionally does not include implementation code, migrations, or a
full automated test suite.

---

## Prerequisites

1. .NET 8 SDK installed (`dotnet --version`)
2. The solution restored and the API running locally
3. A bearer JWT for:
   - an administrator with `exchange-rate:admin` scope
   - a normal authenticated user for transfer execution
4. `curl` and a shell capable of exporting environment variables

Start the API from the repository root if it is not already running:

```bash
dotnet run --project src/FundTransfer.Api
```

Example local endpoints:
- `http://localhost:5000`
- `https://localhost:5001`

Set reusable variables:

```bash
export BASE_URL=http://localhost:5000
export ADMIN_TOKEN=<jwt-with-exchange-rate-admin-scope>
export USER_TOKEN=<jwt-for-transfer-owner>
```

> Use the schemas and field definitions in [contracts/openapi.yaml](contracts/openapi.yaml) and the
> persistence rules in [data-model.md](data-model.md) as the source of truth.

---

## Validation Scenario A - Create an exchange rate (USD -> EUR)

Create the initial USD -> EUR rate that the transfer flow will use.

```bash
curl -i -X POST "$BASE_URL/v1/exchange-rates" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "sourceCurrency": "USD",
    "targetCurrency": "EUR",
    "rate": 0.920345
  }'
```

**Expected outcome**
- HTTP `201 Created`
- Response contains:
  - `id` -> save as `RATE_ID_V1`
  - `sourceCurrency = "USD"`
  - `targetCurrency = "EUR"`
  - `rate = 0.920345`
  - `effectiveFrom` populated
  - `createdBy` equals the admin JWT `sub`

---

## Validation Scenario B - Retrieve the current exchange rate

```bash
curl -i "$BASE_URL/v1/exchange-rates/USD/EUR" \
  -H "Authorization: Bearer $USER_TOKEN"
```

**Expected outcome**
- HTTP `200 OK`
- Returned `id` matches `RATE_ID_V1`
- `rate = 0.920345`
- `effectiveFrom` matches or is later than the create response timestamp

---

## Validation Scenario C - Create a USD source account and an EUR destination account

Create the source account:

```bash
curl -i -X POST "$BASE_URL/v1/accounts" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "owner": "user-usd-001",
    "currency": "USD",
    "initialBalance": 100000
  }'
```

Create the destination account:

```bash
curl -i -X POST "$BASE_URL/v1/accounts" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "owner": "user-eur-001",
    "currency": "EUR",
    "initialBalance": 2000
  }'
```

**Expected outcome**
- Both requests return HTTP `201 Created`
- Save the returned account IDs as:
  - `USD_ACCOUNT_ID`
  - `EUR_ACCOUNT_ID`
- Confirm balances:
  - USD source starts at `100000`
  - EUR destination starts at `2000`

---

## Validation Scenario D - Execute a cross-currency transfer

Transfer 10,000 USD minor units to the EUR account using a fresh idempotency key.

```bash
export IDEMPOTENCY_KEY=7e9af95e-ef59-4c98-8a6d-c8b767a5a56d

curl -i -X POST "$BASE_URL/v1/transfers" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -H "Idempotency-Key: $IDEMPOTENCY_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "sourceAccountId": "'$USD_ACCOUNT_ID'",
    "destinationAccountId": "'$EUR_ACCOUNT_ID'",
    "amount": 10000,
    "sourceCurrency": "USD",
    "destinationCurrency": "EUR"
  }'
```

**Expected outcome**
- HTTP `201 Created`
- Response fields:
  - `status = "Completed"`
  - `appliedExchangeRateId = RATE_ID_V1`
  - `sourceAmount = 10000`
  - `sourceCurrency = "USD"`
  - `destinationCurrency = "EUR"`
  - `destinationAmount = 9203`

`9203` is required because `floor(10000 x 0.920345) = 9203.45 -> 9203`.

Save the transfer ID as `TRANSFER_ID_V1`.

---

## Validation Scenario E - Verify balances and floor-based conversion

Retrieve both accounts after the transfer:

```bash
curl -i "$BASE_URL/v1/accounts/$USD_ACCOUNT_ID" \
  -H "Authorization: Bearer $USER_TOKEN"

curl -i "$BASE_URL/v1/accounts/$EUR_ACCOUNT_ID" \
  -H "Authorization: Bearer $USER_TOKEN"
```

**Expected outcome**
- USD account balance is `90000` (`100000 - 10000`)
- EUR account balance is `11203` (`2000 + 9203`)
- These values prove atomic debit/credit and the `floor()` rule from [data-model.md](data-model.md)

---

## Validation Scenario F - Verify idempotency

Replay the exact same transfer request with the same `Idempotency-Key`.

```bash
curl -i -X POST "$BASE_URL/v1/transfers" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -H "Idempotency-Key: $IDEMPOTENCY_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "sourceAccountId": "'$USD_ACCOUNT_ID'",
    "destinationAccountId": "'$EUR_ACCOUNT_ID'",
    "amount": 10000,
    "sourceCurrency": "USD",
    "destinationCurrency": "EUR"
  }'
```

**Expected outcome**
- HTTP `200 OK`
- Response `id` remains `TRANSFER_ID_V1`
- Response `appliedExchangeRateId` remains `RATE_ID_V1`
- Account balances do **not** change again

---

## Validation Scenario G - Verify rate history is preserved after an update

Update the USD -> EUR rate:

```bash
curl -i -X POST "$BASE_URL/v1/exchange-rates" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "sourceCurrency": "USD",
    "targetCurrency": "EUR",
    "rate": 0.950000
  }'
```

Retrieve the current rate again:

```bash
curl -i "$BASE_URL/v1/exchange-rates/USD/EUR" \
  -H "Authorization: Bearer $USER_TOKEN"
```

**Expected outcome**
- The update request returns HTTP `201 Created`
- Save the new rate ID as `RATE_ID_V2`
- `RATE_ID_V2` is different from `RATE_ID_V1`
- Current rate lookup now returns `RATE_ID_V2` with `rate = 0.950000`
- The original transfer response from Scenario D/F still references `RATE_ID_V1`

This validates that the previous row was superseded, not overwritten, and that transfer rate
snapshots remain stable.

---

## Validation Scenario H - Rejection: no exchange rate exists

Create a GBP destination account without defining a USD -> GBP rate:

```bash
curl -i -X POST "$BASE_URL/v1/accounts" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "owner": "user-gbp-001",
    "currency": "GBP",
    "initialBalance": 0
  }'
```

Save the returned account ID as `GBP_ACCOUNT_ID`, then request a USD -> GBP transfer:

```bash
curl -i -X POST "$BASE_URL/v1/transfers" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -H "Idempotency-Key: 4a0905d8-7a5e-47d3-8fc5-8ef26fc80f32" \
  -H "Content-Type: application/json" \
  -d '{
    "sourceAccountId": "'$USD_ACCOUNT_ID'",
    "destinationAccountId": "'$GBP_ACCOUNT_ID'",
    "amount": 5000,
    "sourceCurrency": "USD",
    "destinationCurrency": "GBP"
  }'
```

**Expected outcome**
- HTTP `422 Unprocessable Entity`
- Error `code = "NO_EXCHANGE_RATE"`
- Neither account balance changes

---

## Validation Scenario I - Rejection: insufficient funds

Attempt a transfer larger than the remaining USD balance.

```bash
curl -i -X POST "$BASE_URL/v1/transfers" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -H "Idempotency-Key: c3ec5c91-b9af-4937-b442-acfd9505d7b4" \
  -H "Content-Type: application/json" \
  -d '{
    "sourceAccountId": "'$USD_ACCOUNT_ID'",
    "destinationAccountId": "'$EUR_ACCOUNT_ID'",
    "amount": 999999,
    "sourceCurrency": "USD",
    "destinationCurrency": "EUR"
  }'
```

**Expected outcome**
- HTTP `422 Unprocessable Entity`
- Error `code = "INSUFFICIENT_FUNDS"`
- USD and EUR balances remain unchanged from Scenario E

---

## Validation Scenario J - Rejection: zero destination amount

Create a very small rate and use a minimal transfer amount.

Create GBP -> AED rate `0.000001`:

```bash
curl -i -X POST "$BASE_URL/v1/exchange-rates" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "sourceCurrency": "GBP",
    "targetCurrency": "AED",
    "rate": 0.000001
  }'
```

Create a GBP source account and an AED destination account:

```bash
curl -i -X POST "$BASE_URL/v1/accounts" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "owner": "user-gbp-small-001",
    "currency": "GBP",
    "initialBalance": 10
  }'

curl -i -X POST "$BASE_URL/v1/accounts" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "owner": "user-aed-small-001",
    "currency": "AED",
    "initialBalance": 0
  }'
```

Save the returned account IDs as `GBP_SMALL_ACCOUNT_ID` and `AED_SMALL_ACCOUNT_ID`, then attempt a 1-minor-unit GBP transfer:

```bash
curl -i -X POST "$BASE_URL/v1/transfers" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -H "Idempotency-Key: 3bcff0c9-9e35-47b2-a75e-c27c5cb65451" \
  -H "Content-Type: application/json" \
  -d '{
    "sourceAccountId": "'$GBP_SMALL_ACCOUNT_ID'",
    "destinationAccountId": "'$AED_SMALL_ACCOUNT_ID'",
    "amount": 1,
    "sourceCurrency": "GBP",
    "destinationCurrency": "AED"
  }'
```

**Expected outcome**
- HTTP `422 Unprocessable Entity`
- Error `code = "ZERO_DESTINATION_AMOUNT"`
- No debit or credit occurs

---

## Optional Additional Check - Destination overflow

If the implementation exposes or documents the maximum supported account balance, create a
scenario where `destinationBalance + destinationAmount` would exceed that limit.

**Expected outcome**
- HTTP `422 Unprocessable Entity`
- Error `code = "DESTINATION_OVERFLOW"`
- Neither balance changes

---

## References

- Data persistence, states, and indexes: [data-model.md](data-model.md)
- Request/response schemas and error contracts: [contracts/openapi.yaml](contracts/openapi.yaml)