# Multi-Client Onboarding Process

This API supports multiple clients at once. Each client is a distinct consuming
application/business entity with its **own** ICEGATE registration (own encrypted credential,
own default sender ID / ICEGATE ID / custodian code) and its **own** internal API key. Nothing
is shared between clients except infrastructure that is identical for everyone (the ICEGATE
URLs, the environment, timeouts). This document is the operational process for adding,
verifying, rotating, and removing a client.

## How it works (read this first)

- Every client is one entry in the `IcegateClients` configuration array
  (`Configuration/IcegateClientSettings.cs`).
- A request authenticates with `X-API-KEY: <that client's ApiKey>`. `ApiKeyAuthMiddleware`
  resolves the key to a `ClientId` via `IIcegateClientRegistry` and stores it on the request
  (`HttpContext.GetClientId()`) - every downstream service call is scoped to that `ClientId`.
- `IIcegateTokenService` caches one ICEGATE token **per `ClientId`**. Client A's token is
  never handed to Client B, and invalidating Client A's token never touches Client B's.
- If a request omits `senderId`/`icegateId`/`custodianCode`, the client's own
  `DefaultSenderId`/`DefaultIcegateId`/`DefaultCustodianCode` is used - never another client's.
- Every `ICEGATE_API_TRANSACTION` row carries the `ClientId` that produced it, so transaction
  history, audit, and reporting can always be filtered per client.
- A disabled client (`Enabled: false`) is rejected at the very first step
  (`ApiKeyAuthMiddleware`, HTTP 401) - no ICEGATE call, no transaction row is ever created for
  a disabled client.

## Step-by-step: onboarding a new client

1. **Collect the client's ICEGATE registration details.** From the client (or directly from
   ICEGATE, depending on who owns the ICEGATE relationship), obtain:
   - The encrypted credential payload for the Authentication API's `data` field (or whatever
     material is needed to produce it - see `docs/API_Contract_Assumptions.md` section 6).
   - The client's ICEGATE-issued API key/subscription key, if ICEGATE requires one per filer.
   - The client's sender ID, ICEGATE ID, and custodian code.
   - Confirm with ICEGATE that this sender ID / ICEGATE ID / custodian code combination is
     authorized for SCMTR filing in the target environment (UAT first, always).

2. **Choose a `ClientId`.** A short, stable, human-readable identifier (e.g. `ACME-CFS`). This
   never changes once chosen - it's what appears in logs and transaction rows for this
   client's entire lifetime, so pick something durable, not a temporary project code.

3. **Generate a new internal API key for this client.** High-entropy, unique, generated
   independently for every client (never reused, never derived from the client's name).
   ```bash
   # any of these are fine - just needs to be long and random
   openssl rand -base64 32
   ```

4. **Add the client's entry to configuration**, via secret storage / environment variables in
   any shared environment (never commit real values to source control):
   ```bash
   dotnet user-secrets set "IcegateClients:0:ClientId" "ACME-CFS"
   dotnet user-secrets set "IcegateClients:0:ApiKey" "<the key generated in step 3>"
   dotnet user-secrets set "IcegateClients:0:EncryptedCredentialData" "<from step 1>"
   dotnet user-secrets set "IcegateClients:0:IcegateApiKey" "<from step 1, if applicable>"
   dotnet user-secrets set "IcegateClients:0:DefaultSenderId" "<from step 1>"
   dotnet user-secrets set "IcegateClients:0:DefaultIcegateId" "<from step 1>"
   dotnet user-secrets set "IcegateClients:0:DefaultCustodianCode" "<from step 1>"
   dotnet user-secrets set "IcegateClients:0:Enabled" "true"
   ```
   In a real UAT/PROD deployment, set the equivalent environment variables
   (`IcegateClients__0__ClientId`, etc.) or entries in your secret manager (Azure Key Vault,
   AWS Secrets Manager, etc.) instead of user-secrets. Use the next available array index if
   other clients already exist.

5. **Distribute the internal API key to the client** through a secure channel (secret manager
   share, not email/chat) for them to configure in their legacy application
   (`legacy-client/IcegateLegacyClient/App.config`'s `OurApiKey`, or the equivalent in their
   real deployment).

6. **Verify the key resolves correctly** before any real traffic:
   ```
   GET /api/icegate/whoami
   X-API-KEY: <the new key>
   ```
   Expect `{ "success": true, "data": { "clientId": "ACME-CFS" } }`. If this fails, re-check
   step 4 - do not proceed to real ICEGATE calls until `whoami` succeeds.

7. **Run the UAT test cases** (`docs/UAT_Test_Cases.md`) scoped to this client's sender ID /
   custodian code, especially TC01 (token generation) and TC04 (SCMTR upload), to confirm the
   client's own ICEGATE credentials actually work end-to-end. Also run TC23 (multi-client
   isolation) if any other client is already onboarded in the same environment.

8. **Monitor.** Watch this client's `ICEGATE_API_TRANSACTION` rows (`WHERE ClientId =
   'ACME-CFS'`) for `SUBMISSION_FAILED`/`ACK_FAILED` spikes in the days after go-live.

9. **Repeat for PROD** only after the client has passed the full
   `docs/Production_Checklist.md`, using a **separate** internal API key and (once ICEGATE
   confirms production URLs) separate production ICEGATE credentials - never reuse a UAT key
   or UAT credential in production.

## Rotating a client's internal API key

1. Generate a new key (step 3 above).
2. Update `IcegateClients[].ApiKey` for that client to the new value; do not remove the entry.
3. Verify with `GET /api/icegate/whoami` using the new key.
4. Have the client switch their configuration to the new key.
5. Once the client confirms they've cut over (check for recent successful calls under their
   `ClientId`), the old key is already invalid the moment step 2 was applied - there is
   nothing further to revoke.

## Revoking / suspending a client

- **Temporary suspension**: set that client's `Enabled: false`. Every request with their key
  now gets HTTP 401 immediately; their existing transaction history and cached ICEGATE token
  (which stops being reachable, since no request can resolve to their `ClientId` from an
  incoming key) are left intact for audit.
- **Reversing a suspension**: set `Enabled: true` again - nothing else to do, the next request
  from that client generates a fresh token as normal.
- **Permanent removal**: only after confirming the client's transaction history has been
  archived per your data-retention policy, remove their entry from `IcegateClients` entirely.
  Do not reuse their `ClientId` or `ApiKey` for a different client afterward.

## Common mistakes to avoid

- Never assign the same `ApiKey` to two clients - `IIcegateClientRegistry` resolves the
  **first** enabled match, so a collision silently misattributes one client's traffic to
  another's ICEGATE identity and transaction history.
- Never put a client's `EncryptedCredentialData` or `ApiKey` in `appsettings.json` /
  `appsettings.{UAT,PROD}.json` in source control - those files should only ever contain
  `"IcegateClients": []` (see the comments in each file). Real entries come from secret
  storage or environment variables only.
- Never point a UAT-onboarded client's `EncryptedCredentialData` at the PROD environment (or
  vice versa) - `Icegate:Environment` is a single global switch for every client in that
  deployment, so mixing UAT/PROD credentials with the wrong environment's URLs affects
  everyone, not just one client.
