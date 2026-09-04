# IcegateLegacyClient (.NET Framework 4.0)

Sample client code showing how the existing ASP.NET / .NET Framework 4.0 business
application calls the `Icegate.Integration` .NET 8 Web API. This project talks **only**
to our own API (`OurApiBaseUrl` in `App.config`) - it never contacts ICEGATE directly and
never implements ICEGATE authentication, tokens, or multipart parsing itself.

## Contents

- `IcegateApiClient.cs` - the reusable client class (`HttpWebRequest`/`HttpWebResponse`
  based, no `HttpClient`, no `async`/`await` - neither is available/idiomatic on .NET
  Framework 4.0).
- `Models/` - plain POCOs matching the `IcegateApiResult<T>` envelope, deserialized with
  `System.Web.Script.Serialization.JavaScriptSerializer` (part of `System.Web.Extensions`,
  available on .NET Framework 4.0 - no third-party JSON package required).
- `Program.cs` - a console demo of the full flow: select SCMTR JSON, upload, receive
  `uniqueId`, save it, poll `/api/icegate/ack`, save/display the ACK.
- `App.config` - `OurApiBaseUrl`, `OurApiKey` and related settings.

## Integrating into the real application

In a real ASP.NET Web Forms / MVC 4.0 application, do not copy `Program.cs` verbatim -
instead:

1. Reference `IcegateApiClient.cs` and `Models/` from your business/service layer project.
2. Instantiate `IcegateApiClient` once per request (it is cheap - just wraps
   `HttpWebRequest` calls) using `OurApiBaseUrl` / `OurApiKey` from your existing
   configuration mechanism (`web.config`, a settings table, etc.).
3. Call `UploadScmtrFile(...)` from the action/handler that currently lets the user submit
   an SCMTR filing. Persist the returned `uniqueId` against your existing shipping-bill /
   filing record.
4. Call `GetAcknowledgement(...)` from a retry job (a Windows Service, a scheduled task, or
   a "Check ACK" button) rather than blocking the user's request - ICEGATE may take time to
   generate the ACK. Treat `IcegateClientException` with `StatusCode == 202` as
   "not ready yet, retry later", not as an error.
5. Never log `OurApiKey`, and never surface it in any UI.

## Building

This is a classic (non-SDK-style) .csproj targeting `.NET Framework 4.0`, compatible with
Visual Studio 2010+ or `msbuild`/`devenv` on a machine with the .NET Framework 4.0
Developer Pack installed. It has no NuGet dependencies - only the framework assemblies
`System`, `System.Core`, and `System.Web.Extensions`.

```
msbuild IcegateLegacyClient.csproj /p:Configuration=Release
bin\Release\IcegateLegacyClient.exe C:\path\to\scmtr.json REF-0001
```

## Security notes

- `OurApiKey` is the internal API key for **our** API, issued separately from any ICEGATE
  credential. Store it in a protected configuration section, environment variable, or the
  Windows Credential Manager in production - not in plaintext `web.config` checked into
  source control.
- TLS 1.2 is enabled explicitly in `IcegateApiClient`'s static constructor via the numeric
  `SecurityProtocolType` value `3072`, because `SecurityProtocolType.Tls12` is not defined
  until .NET Framework 4.5.
