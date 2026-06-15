# Quickstart & Validation Guide: Fund Transfer Service

**Phase**: 1 — Design
**Date**: 2026-06-15
**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Contract**: [contracts/openapi.yaml](contracts/openapi.yaml)

This guide describes how to build, run, and validate that the Fund Transfer Service works
end-to-end. It covers all three user stories from the spec using Swagger UI and curl.

---

## Prerequisites

| Requirement | Version |
|-------------|---------|
| .NET SDK | 8.0 LTS or newer (`dotnet --version`) |
| Git | Any recent version |
| curl (optional) | Any; or use Swagger UI in browser |

No database setup required — the service uses an **in-memory database** that resets on restart.

---

## 1. Build & Run

```bash
# Clone and navigate to the repo root
git clone <repo-url>
cd <repo-root>

# Build the solution
dotnet build FundTransfer.sln

# Run the API
dotnet run --project src/FundTransfer.Api
```

The API starts on:
- HTTP:  `http://localhost:5000`
- HTTPS: `https://localhost:5001`

Swagger UI is available at: **`http://localhost:5000/swagger`**

---

## 2. Run Tests

```bash
# All tests (unit + integration + contract)
dotnet test FundTransfer.sln

# With coverage report (requires coverlet)
dotnet test FundTransfer.sln --collect:"XPlat Code Coverage"
```

Expected output: all tests pass; coverage ≥ 80% on `FundTransfer.Application`.

---

## 3. Authentication Note (Development)

The service validates JWT Bearer tokens. For local development, configure a known test JWT
or disable auth validation in `appsettings.Development.json`:

```json
{
  "Auth": {
    "BypassForDevelopment": true
  }
}
```

When bypass is active, the service uses a fixed development identity (`dev-user-001`) as the
caller. All Swagger UI requests will be authenticated as this identity.

To test owner authorization, set the JWT `sub` claim to match the `owner` field on the account.

---

## 4. Validation Scenarios

Use **Swagger UI** at `http://localhost:5000/swagger` or the `curl` commands below.
All amounts are in **minor currency units** (e.g., `100000` = $1,000.00 USD).

---

### Scenario A — Create Account (User Story 1)

**Goal**: Verify account creation assigns a unique number, stores owner/currency/balance.

**Step 1**: Open Swagger UI → `POST /v1/accounts` → Try it out.

Request body:
```json
{
  "owner": "user-alice-001",
  "currency": "USD",
  "initialBalance": 500000
}
```

```bash
curl -s -X POST http://localhost:5000/v1/accounts \
  -H "Content-Type: application/json" \
  -d '{"owner":"user-alice-001","currency":"USD","initialBalance":500000}'
```

**Expected**: `201 Created` with a response containing:
- `accountNumber`: system-assigned (e.g., `ACC-20260615-0001`)
- `owner`: `"user-alice-001"`
- `currency`: `"USD"`
- `balance`: `500000`

Save the returned `accountNumber` for the next scenarios (referred to as `ACCOUNT_A`).

---

**Step 2**: Create a second account for Bob (save as `ACCOUNT_B`):

```bash
curl -s -X POST http://localhost:5000/v1/accounts \
  -H "Content-Type: application/json" \
  -d '{"owner":"user-bob-002","currency":"USD","initialBalance":100000}'
```

**Expected**: `201 Created`, `balance: 100000`.

---

**Negative test — invalid currency**:

```bash
curl -s -X POST http://localhost:5000/v1/accounts \
  -H "Content-Type: application/json" \
  -d '{"owner":"user-test","currency":"XX","initialBalance":1000}'
```

**Expected**: `400 Bad Request`, error detail mentions invalid currency code.

---

**Negative test — negative balance**:

```bash
curl -s -X POST http://localhost:5000/v1/accounts \
  -H "Content-Type: application/json" \
  -d '{"owner":"user-test","currency":"USD","initialBalance":-100}'
```

**Expected**: `400 Bad Request`, error detail mentions non-negative balance required.

---

### Scenario B — Retrieve Balance (User Story 2)

**Goal**: Verify balance retrieval returns accurate account details.

Replace `{ACCOUNT_A}` with the account number from Scenario A.

```bash
curl -s http://localhost:5000/v1/accounts/{ACCOUNT_A}
```

**Expected**: `200 OK` with:
- `accountNumber`: matches `{ACCOUNT_A}`
- `currency`: `"USD"`
- `balance`: `500000`
- `owner`: `"user-alice-001"`

---

**Negative test — non-existent account**:

```bash
curl -s http://localhost:5000/v1/accounts/ACC-99999999-9999
```

**Expected**: `404 Not Found`.

---

### Scenario C — Transfer Funds (User Story 3)

**Goal**: Verify atomic debit + credit, with both balances updated correctly.

Transfer 50,000 minor units ($500.00) from `ACCOUNT_A` to `ACCOUNT_B`:

```bash
curl -s -X POST http://localhost:5000/v1/transfers \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: 11111111-1111-1111-1111-111111111111" \
  -d '{
    "sourceAccountNumber": "{ACCOUNT_A}",
    "destinationAccountNumber": "{ACCOUNT_B}",
    "amount": 50000
  }'
```

**Expected**: `201 Created` with:
- `status`: `"Completed"`
- `amount`: `50000`
- `currency`: `"USD"`
- `failureReason`: `null`

**Verify balances updated**:

```bash
curl -s http://localhost:5000/v1/accounts/{ACCOUNT_A}
# Expected: balance = 450000 (500000 - 50000)

curl -s http://localhost:5000/v1/accounts/{ACCOUNT_B}
# Expected: balance = 150000 (100000 + 50000)
```

---

**Idempotency test — replay same transfer**:

Submit the identical request again with the same `Idempotency-Key`:

```bash
curl -s -X POST http://localhost:5000/v1/transfers \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: 11111111-1111-1111-1111-111111111111" \
  -d '{
    "sourceAccountNumber": "{ACCOUNT_A}",
    "destinationAccountNumber": "{ACCOUNT_B}",
    "amount": 50000
  }'
```

**Expected**: `200 OK` (not 201) — same `transferId` returned, balances **unchanged** (no second debit).

---

**Negative test — insufficient funds**:

```bash
curl -s -X POST http://localhost:5000/v1/transfers \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: 22222222-2222-2222-2222-222222222222" \
  -d '{
    "sourceAccountNumber": "{ACCOUNT_A}",
    "destinationAccountNumber": "{ACCOUNT_B}",
    "amount": 9999999999
  }'
```

**Expected**: `422 Unprocessable Entity`, `failureReason: "InsufficientFunds"`, balances unchanged.

---

**Negative test — same account**:

```bash
curl -s -X POST http://localhost:5000/v1/transfers \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: 33333333-3333-3333-3333-333333333333" \
  -d '{
    "sourceAccountNumber": "{ACCOUNT_A}",
    "destinationAccountNumber": "{ACCOUNT_A}",
    "amount": 1000
  }'
```

**Expected**: `400 Bad Request` or `422`, detail mentions same-account transfer.

---

**Negative test — currency mismatch**:

First create an EUR account:
```bash
curl -s -X POST http://localhost:5000/v1/accounts \
  -H "Content-Type: application/json" \
  -d '{"owner":"user-carol-003","currency":"EUR","initialBalance":100000}'
# Save as ACCOUNT_C
```

Then attempt a USD→EUR transfer:
```bash
curl -s -X POST http://localhost:5000/v1/transfers \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: 44444444-4444-4444-4444-444444444444" \
  -d '{
    "sourceAccountNumber": "{ACCOUNT_A}",
    "destinationAccountNumber": "{ACCOUNT_C}",
    "amount": 1000
  }'
```

**Expected**: `422 Unprocessable Entity`, `failureReason: "CurrencyMismatch"`.

---

## 5. Acceptance Criteria Traceability

| Spec Scenario | Validation Step |
|---------------|----------------|
| US1-S1: Valid account creation | Scenario A, Step 1 |
| US1-S2: Negative balance rejected | Scenario A, negative test |
| US1-S3: Invalid currency rejected | Scenario A, negative test |
| US2-S1: Balance retrieval | Scenario B |
| US2-S2: Non-existent account | Scenario B, negative test |
| US3-S1: Successful transfer + balance update | Scenario C, transfer + verify |
| US3-S2: Insufficient funds | Scenario C, insufficient funds test |
| US3-S3: Same account transfer | Scenario C, same-account test |
| US3-S4: Currency mismatch | Scenario C, currency mismatch test |
| US3-S7: Idempotency | Scenario C, idempotency replay test |

---

## 6. Switching to SQL Server

When ready to activate SQL Server:

1. Update `Program.cs` — replace:
   ```csharp
   options.UseInMemoryDatabase("FundTransferDb")
   ```
   with:
   ```csharp
   options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
   ```

2. Add connection string to `appsettings.json` (or environment variable):
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=FundTransferDb;Trusted_Connection=True;"
     }
   }
   ```

3. Run EF Core migrations:
   ```bash
   dotnet ef migrations add InitialCreate --project src/FundTransfer.Infrastructure \
     --startup-project src/FundTransfer.Api
   dotnet ef database update --project src/FundTransfer.Infrastructure \
     --startup-project src/FundTransfer.Api
   ```

4. Re-run all validation scenarios above — behaviour should be identical.
