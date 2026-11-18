# UC117 – Feature Flag & Configuration Management Console

## Stack
- **Backend**: ASP.NET Core 8 Minimal API, EF Core 8, ASP.NET Core Identity, JWT Bearer
- **Frontend**: React 18 with TypeScript, Redux Toolkit, React Router 6
- **Database**: Azure SQL / SQL Server LocalDB
- **Frontend state**: Redux Toolkit (RTK) — flags, change requests, API keys all in separate slices

## How to Run

### Prerequisites
- .NET 8 SDK
- Node.js 18+
- SQL Server LocalDB (included with VS) or Azure SQL

### Backend

```bash
cd src/FeatureFlags.Api

# (Optional) configure user secrets instead of editing appsettings.json
dotnet user-secrets set "Jwt:Key" "your-32-char-secret-here"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=...;Database=..."

dotnet run
```

API runs on `http://localhost:5000`. Swagger UI at `http://localhost:5000/swagger`.

**DB migration + seed happen automatically on startup.** Seeded credentials:
| Email | Password | Role |
|---|---|---|
| admin@flagconsole.dev | Admin@123! | Admin |
| approver@flagconsole.dev | Approver@123! | Approver |
| dev1@flagconsole.dev | Dev@123! | Developer |
| dev2@flagconsole.dev | Dev@123! | Developer |

**Seeded API keys** (dev reference only — never commit real keys):
- dev environment: `dev-api-key-00000000000000000001`
- test environment: `test-api-key-0000000000000000002`
- prod environment: `prod-api-key-0000000000000000003`

### Frontend

```bash
cd feature-flags-ui
npm install
npm start
```

App runs on `http://localhost:3000`, proxied to `http://localhost:5000`.

## Architecture & Design Choices

### Data Model
- `Flag` — definition (key, name, type). Key is unique and indexed.
- `AppEnvironment` — dev/test/prod. `IsProtected = true` requires approval.
- `FlagEnvironmentValue` — per-flag per-environment state: enabled/disabled, default value. Has a `RowVersion` for optimistic concurrency.
- `TargetingRule` — ordered rules per FEV: either `Percentage` or `Attribute`.
- `FlagVersion` — append-only history. Rollback creates a new version (never mutates history).
- `ChangeRequest` — approval flow for protected environments.
- `ApiKey` — only the SHA-256 hash is stored. Plain key shown once at creation.

### Stable Hashing
Percentage rollout uses `SHA-256(flagKey + ":" + userId) mod 100`. This is deterministic across restarts, processes, and deployments — unlike `string.GetHashCode()`.

### Approval Workflow
- Protected environments require a `ChangeRequest` to be submitted by a Developer.
- Approvers and Admins can approve or reject — but NOT their own requests.
- On approval the change is immediately applied and a new version is recorded.
- Non-protected environments are updated directly (Developer or Admin).

### Concurrency Control
- `RowVersion` timestamp column on `FlagEnvironmentValue`.
- PUT requests must include the current `rowVersion`.
- Stale version → HTTP 409 Conflict with a user-visible message.

### Evaluation Endpoint
- Authenticated by API key per environment via `X-Api-Key` header or `Bearer` token.
- Rules evaluated in `Order` ascending. First match wins.
- Returns a defined default (flag disabled, null value) for unknown flags — never throws.
- Sub-50 ms: single eagerly-loaded query with no N+1.

### Snapshot + ETag
- `GET /evaluate/{env}/snapshot` returns all flags for the environment.
- ETag = SHA-256 of the JSON response.
- `If-None-Match` → 304 Not Modified when nothing changed.

### Security
- Hashed API keys (SHA-256 hex).
- JWT roles enforced server-side. UI hiding is cosmetic only.
- Developer cannot approve own change — enforced in the approve endpoint.
- Secrets via environment variables or `dotnet user-secrets`.

## Known Limitations
- No real-time push notifications (clients must poll the snapshot endpoint).
- No multi-tenant isolation (single-org deployment).
- EF Core InMemory used in tests; integration tests target LocalDB.
- No email/Slack notification stubs wired (out of scope per spec).

## Demo Walkthrough (10 min)

1. **Create a flag**: Log in as Developer, click "+ New Flag", create `new-payments` (Boolean).
2. **Toggle per environment**: Click the `dev` env link, enable the flag, add a 20% rollout rule, save.
3. **Stable evaluation**: Use Swagger `POST /evaluate/dev/new-payments` with different user IDs; same user always returns same result.
4. **Approval flow**: Log in as Developer, propose a change to the `prod` env flag. Log in as Approver, approve it from the Approvals page.
5. **History & rollback**: View the history table on the flag environment page; click Restore on an earlier version.
6. **ETag**: Call `GET /evaluate/dev/snapshot` twice with the same `If-None-Match` header; second call returns 304. Call with wrong API key; get 401.
