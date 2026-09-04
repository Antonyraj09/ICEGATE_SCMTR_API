# API Contract Assumptions & CONFIRM_WITH_ICEGATE Items

This solution was built strictly from the ICEGATE endpoint, header, request, and response
details supplied in the integration task description (which mirrors *API Contract Document -
SCMTR Open API Filing, Version 1.6*). The original PDF/DOCX of Version 1.6 was not attached
to the development session, so implementation is limited to what that description specifies.
No field, URL, header, or business rule not present in that specification has been invented.

Anywhere the document indicates a value must be rechecked with ICEGATE, or where the
specification is silent on an implementation detail, this project either exposes a
configuration placeholder of `CONFIRM_WITH_ICEGATE`, or documents the assumption below so it
can be verified against the actual v1.6 document (or ICEGATE support) before UAT/go-live.

## 1. Endpoints marked CONFIRM_WITH_ICEGATE

| Setting | Current value | Why |
|---|---|---|
| `Icegate:GetAcknowledgementUrl` | `CONFIRM_WITH_ICEGATE` | The specification explicitly states the Get Acknowledgement URL must be rechecked with ICEGATE before integration testing / go-live. No UAT URL for this endpoint was supplied. |

All other UAT endpoints (Authentication, Inbound Upload, Get Zip Acknowledgement) were given
explicit URLs in the specification and are pre-populated in `appsettings.UAT.json`.
**PROD** endpoints for all four APIs are `CONFIRM_WITH_ICEGATE` in `appsettings.PROD.json` -
production URLs were not supplied and must never be assumed to equal the UAT URLs with a
different hostname.

## 2. Multipart response field extraction (Get ACK / Get ZIP ACK)

The specification states both the Get Acknowledgement and Get Zip Acknowledgement responses
are **multipart**, and names the fields each contains (`senderId`, `uniqueId`, `responseDesc`,
ACK file / `icegateId`, `messageId`, `custodianCode`, `responseId`, `fileCount`,
`responseDesc`, file) - but does not specify:

- The exact multipart part **names** ICEGATE uses for each metadata field (e.g. whether the
  field is literally named `responseDesc` or something else).
- Whether metadata arrives as individual named text parts, or bundled into a single JSON part.
- The `Content-Disposition` details of the file part (field name used for the binary content).

**Assumption implemented** (`Helpers/MultipartResponseParser.cs`,
`Services/IcegateAcknowledgementService.cs`, `Services/IcegateZipAcknowledgementService.cs`):
the parser looks up each metadata field by name, case-insensitively, among the multipart
form-data parts (`MultipartResponseParser.FindField`), and treats the first part that carries
a `filename` in its `Content-Disposition` header as the ACK/ZIP payload
(`MultipartResponseParser.FindFilePart`). This is the most common shape for a multipart
response describing metadata + one file, and matches the field names given in the
specification exactly. **Verify against a real ICEGATE UAT response during Step 6/8 of the
UAT Testing Procedure (see `docs/UAT_Test_Cases.md`)** and adjust the candidate names passed
to `FindField` if ICEGATE uses different part names.

## 3. Generic ICEGATE error response shape

The specification lists the *documented business error messages* (see
`Models/Common/IcegateDocumentedErrors.cs`) but does not give a single documented JSON error
envelope shape for arbitrary 4xx/5xx responses. `Helpers/IcegateErrorMapper.cs` looks for the
message under `errorMessage`, `message`, `error`, or `description` (in that priority order) in
the response body, falling back to the raw response body text if none match. Update the
property-name priority list if the real ICEGATE error envelope differs.

## 4. HTTP status code -> exception category mapping

The specification lists status codes to handle (400, 401, 403, 404, 408, 409, 422, 429, 500,
502, 503, 504) without prescribing which are "business validation - never retry" vs.
"transient - safe to retry". The mapping implemented in `IcegateErrorMapper` is:

| Status | Category | Rationale |
|---|---|---|
| 400, 404, 409, 422 | Business validation (never retried) | Client-side/data errors; retrying without changing the request cannot succeed. |
| 401 | Token (clear + regenerate + retry once) | Matches "invalid token / expired token / missing token" language in the document. |
| 403 | Token **if** the message mentions "token"; otherwise business validation (e.g. "Authorization failed, request not allowed for provided sender ID.") | The document uses 403-style wording for both an authorization/sender failure and (elsewhere) token language: the mapper inspects the message text to disambiguate. |
| 408, 429, 500, 502, 503, 504 | Transient (controlled retry via Polly) | Standard transient/gateway/timeout semantics. |

## 5. Token response shape

The document states the token is valid for "approximately 15 minutes" but the exact JSON key
returned by the Authentication API for the token value and its expiry was not given.
`Models/Authentication/AuthenticationModels.cs` (`IcegateAuthenticationResponse`) accepts
either `token` or `access_token` for the token value, and an optional `expiresIn` (seconds);
if no explicit expiry is present, `Icegate:TokenNominalLifetimeSeconds` (default 900 seconds =
15 minutes) is used. Confirm the actual response field names against ICEGATE UAT and adjust
`ResolveToken()`/`ExpiresInSeconds` if different.

## 6. Encrypted credential payload construction

The document specifies the Authentication API request body is `{ "data": "<encrypted-user-
credentials>" }` but does not specify the encryption algorithm/key exchange used to build that
payload. This solution treats `Icegate:EncryptedCredentialData` as an already-encrypted,
externally-supplied value (populated via secret storage) and passes it through unchanged -
**it does not perform any encryption itself**, since the algorithm was not specified. If
ICEGATE requires a specific encryption routine to build `data` from a username/password, that
routine must be added to `IcegateAuthenticationService` once the algorithm is confirmed.

## 7. Internal API authentication mechanism

The document does not govern how our own (.NET 8) API should be secured against the legacy
application, only that it must be secured separately from ICEGATE. `X-API-KEY` was chosen
per the task's own example. This is an implementation choice, not an ICEGATE requirement, and
can be swapped for another mechanism (mutual TLS, OAuth client-credentials, etc.) without
touching any ICEGATE-facing code.

## How to resolve these items

1. Obtain the actual API Contract Document v1.6 (or ICEGATE UAT support contact) and confirm
   each item above.
2. Update the relevant `appsettings.{UAT|PROD}.json` value and/or the field-name lists in
   `MultipartResponseParser`/`IcegateErrorMapper`/`AuthenticationModels`.
3. Re-run the UAT test cases in `docs/UAT_Test_Cases.md`, in particular TC01 (token), TC11/TC12
   (ACK), and TC14 (ZIP ACK), against a real ICEGATE UAT response to validate the parsing
   assumptions in section 2.
