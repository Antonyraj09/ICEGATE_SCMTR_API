# UAT -> Production Deployment Checklist

Do not deploy `Icegate:Environment = PROD` until every item below is checked. Never mix a
UAT credential with a PROD URL, or a PROD credential with a UAT URL - `Environment` in
configuration, logs, and every `ICEGATE_API_TRANSACTION` row must consistently read `PROD`
once cut over.

## 1. ICEGATE confirmations (must come from ICEGATE, not assumed)

- [ ] Production Authentication URL confirmed and set in `appsettings.PROD.json` /
      production secret store (`Icegate:AuthenticationUrl`).
- [ ] Production Inbound Upload URL confirmed (`Icegate:InboundUploadUrl`).
- [ ] Production Get Acknowledgement URL confirmed (`Icegate:GetAcknowledgementUrl`) - this
      was `CONFIRM_WITH_ICEGATE` even in UAT per the source document; must not go live with
      that placeholder still set.
- [ ] Production Get Zip Acknowledgement URL confirmed (`Icegate:GetZipAcknowledgementUrl`).
- [ ] Production API key obtained and stored in secret manager (never in `appsettings.*.json`
      in source control).
- [ ] Production credentials (whatever ICEGATE requires to build the encrypted `data` payload)
      obtained and stored securely.
- [ ] Production ICEGATE ID confirmed (`Icegate:IcegateId`).
- [ ] Production sender ID confirmed (`Icegate:SenderId`).
- [ ] Production custodian code confirmed (`Icegate:CustodianCode`).
- [ ] Authorization mapping confirmed with ICEGATE (sender ID / ICEGATE ID / custodian code
      combination is authorized for production SCMTR filing).

## 2. Connectivity & security

- [ ] SSL/TLS reachability to all four production ICEGATE endpoints confirmed from the
      production network (no proxy/firewall blocking outbound HTTPS).
- [ ] No SSL certificate validation bypass is present anywhere in the code path used for
      production (`ServicePointManager`/`HttpClientHandler` certificate callbacks). Any UAT-only
      workaround is isolated behind an environment check and does not compile/run under
      `Environment = PROD`.
- [ ] Internal API (`InternalApi:ApiKeys`) production key(s) generated, distinct from any
      UAT/dev key, and distributed to the legacy application's production configuration only
      through secret storage.
- [ ] HTTPS enforced end-to-end (our API's `UseHttpsRedirection`, the reverse proxy/load
      balancer terminating TLS, and the legacy application's outbound calls).

## 3. Functional verification against PROD (or a PROD-equivalent staging slot, if ICEGATE
   offers one)

- [ ] Token generation tested against the production Authentication URL.
- [ ] SCMTR file upload tested end-to-end, `uniqueId` captured and persisted.
- [ ] ACK retrieval tested end-to-end, ACK file parsed and saved.
- [ ] ZIP ACK retrieval tested end-to-end for at least one `batchSize` value.
- [ ] A deliberate business-validation failure (e.g. a known-bad sender ID, if ICEGATE
      provides one) confirmed to return the original ICEGATE message unmodified, with no
      automatic retry.

## 4. Operational readiness

- [ ] Development-level logging disabled (`Logging:LogLevel:Default` at `Information` or
      higher; Serilog `MinimumLevel` set per `appsettings.PROD.json`).
- [ ] Confirmed no token, API key, password, JWT, or encrypted credential payload appears in
      any log sink (console, file, or centralized log aggregator) - re-run a log audit after a
      full token lifecycle (generate -> reuse -> refresh) in the production configuration.
- [ ] Confirmed the full SCMTR JSON body is not logged in production (only metadata: file
      name, hash, size, correlation ID, transaction status).
- [ ] Secret storage verified: `Icegate:ApiKey`, `Icegate:EncryptedCredentialData`,
      `InternalApi:ApiKeys`, and the production database connection string are sourced from
      the production secret manager / environment variables, not from any file under source
      control.
- [ ] File storage backup/retention policy verified for `FileStorage:InboundPath`,
      `FileStorage:AckPath`, `FileStorage:ZipAckPath` (production paths, disk space, backup
      job, retention/purge policy).
- [ ] `ICEGATE_API_TRANSACTION` database migrated in production (`db/scripts/001_create_
      icegate_api_transaction.sql` or the EF Core migration equivalent) and backed up per the
      standard database backup schedule.
- [ ] Monitoring/alerting configured: HTTP 5xx rate, ICEGATE transient-failure rate, token
      refresh failures, and `SUBMISSION_FAILED` / `ACK_FAILED` transaction counts.
- [ ] Health check endpoints (`/health`, `/api/icegate/health`) wired into the production
      monitoring/load balancer probe configuration.
- [ ] Rollback plan documented: how to revert the legacy application to point at the previous
      integration mechanism (or take the SCMTR filing feature offline) if production ICEGATE
      calls fail after cutover.

## 5. Sign-off

- [ ] Business/compliance sign-off that production SCMTR filings via this integration are
      approved to go live.
- [ ] Technical sign-off that all items above are checked and evidenced (screenshots/logs of
      each verification step retained).
