# ICEGATE SCMTR Open API Integration

A .NET 8 Web API (`Icegate.Integration`) that acts as the middleware/integration layer
between an existing ASP.NET / .NET Framework 4.0 logistics application and ICEGATE's SCMTR
Open API (per *API Contract Document - SCMTR Open API Filing, Version 1.6*). The legacy
application never implements ICEGATE authentication, token, or multipart logic directly - it
only calls this API over HTTPS/REST.

```
Existing ASP.NET / .NET Framework 4.0 app
            | HTTPS REST (JSON / multipart/form-data)
            v
   Icegate.Integration (.NET 8 Web API)
            |
            +-- Authentication + Token lifecycle
            +-- Inbound SCMTR upload
            +-- Get Acknowledgement (multipart)
            +-- Get Zip Acknowledgement (multipart)
            v
      ICEGATE UAT / PROD
```

## Repository layout

| Path | Contents |
|---|---|
| `src/Icegate.Integration/` | The .NET 8 Web API - controllers, services, HTTP client, middleware, EF Core data layer, configuration, Swagger. |
| `tests/Icegate.Integration.Tests/` | xUnit unit tests (token lifecycle, multipart parsing, file/JSON validation, error mapping, retry logic, configuration). |
| `db/scripts/` | SQL script to create `ICEGATE_API_TRANSACTION` (also created automatically via EF Core for local/dev use). |
| `legacy-client/IcegateLegacyClient/` | Complete .NET Framework 4.0 client (HttpWebRequest-based) demonstrating how the existing application calls this API. |
| `postman/` | UAT Postman collection + environment (both ICEGATE-direct troubleshooting requests and calls to this API). |
| `docs/` | Developer integration guide, UAT test cases, production checklist, and documented spec assumptions/`CONFIRM_WITH_ICEGATE` items. |

## Getting started (local/UAT)

```bash
# Restore, build, test
dotnet restore Icegate.Integration.sln
dotnet build Icegate.Integration.sln
dotnet test tests/Icegate.Integration.Tests/Icegate.Integration.Tests.csproj

# Configure secrets (never commit real values)
cd src/Icegate.Integration
dotnet user-secrets set "Icegate:EncryptedCredentialData" "<value>"
dotnet user-secrets set "Icegate:ApiKey" "<value>"
dotnet user-secrets set "InternalApi:ApiKeys:0" "<a-generated-internal-key>"

# Run against ICEGATE UAT (default Icegate:Environment = UAT)
dotnet run
```

Swagger UI is available at `/swagger` in the `Development` environment. `/health` and
`/api/icegate/health` are unauthenticated liveness/config-status probes.

## Environments

`Icegate:Environment` (`UAT` or `PROD`) selects which `appsettings.{Environment}.json` overlay
is applied, independent of `ASPNETCORE_ENVIRONMENT`. **Never** pair UAT credentials with a PROD
URL, or PROD credentials with a UAT URL - see `docs/Production_Checklist.md`.

## Key behaviors

- **Token lifecycle**: a single in-memory token is generated on first use, reused across
  requests, and refreshed automatically before its ~15 minute expiry (configurable safety
  buffer). On a token-invalid/expired response, the cached token is cleared and the original
  request is retried exactly once with a fresh token - never in a loop.
- **Business validation errors from ICEGATE are never retried automatically** and their
  original wording is always preserved in `errorMessage`.
- **Transient failures** (timeouts, 5xx, 429) go through a controlled Polly retry policy.
- SCMTR JSON content is validated for syntax only - never modified, renamed, or reinterpreted
  before being sent to ICEGATE.
- Multipart ACK/ZIP ACK responses are parsed generically (not assumed to be JSON) and the
  binary file part is saved to configurable, path-traversal-safe storage; the API returns a
  file reference rather than the raw bytes.
- Every request/response is logged with a correlation ID; ICEGATE tokens, API keys, and
  credentials are never logged.

## Further reading

- [`docs/Developer_Integration_Guide.md`](docs/Developer_Integration_Guide.md) - how the
  legacy application should call this API.
- [`docs/UAT_Test_Cases.md`](docs/UAT_Test_Cases.md) - TC01-TC22 test procedure.
- [`docs/Production_Checklist.md`](docs/Production_Checklist.md) - UAT -> PROD checklist.
- [`docs/API_Contract_Assumptions.md`](docs/API_Contract_Assumptions.md) - every
  `CONFIRM_WITH_ICEGATE` placeholder and documented assumption, with rationale.
