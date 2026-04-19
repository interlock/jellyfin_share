# Security Review

## Summary

- Critical: 2
- High: 4
- Medium: 5
- Low: 2

## Findings

### [Critical] No Authentication or Authorization on Any Controller Endpoint

- **File**: `src/Controllers/ShareLinkController.cs` (all routes)
- **File**: `src/Controllers/StreamController.cs` (all routes)
- **Description**: None of the plugin's API endpoints carry any `[Authorize]` attribute, authentication scheme, or authorization check. This means every route -- including the administrative endpoints at `POST /admin/links`, `DELETE /admin/links/{id}`, and `GET /admin/links` -- is fully accessible to any unauthenticated requester who can reach the Jellyfin server. There is no check that the caller is a logged-in Jellyfin user, nor that they have admin rights.

  Specifically:
  - `POST /mediashare/invite/{code}` -- accepts and acts on any incoming invite code without auth
  - `GET /mediashare/share/{linkId}/catalog` -- returns library catalog to anyone
  - `GET /mediashare/stream/{linkId}/{fileId}` -- streams media content to anyone
  - `POST /mediashare/admin/links` -- creates share links without auth
  - `DELETE /mediashare/admin/links/{id}` -- revokes links without auth
  - `GET /mediashare/admin/links` -- lists all active links without auth

  In a Jellyfin plugin, controller actions run inside the Jellyfin host process and inherit the server's web pipeline. Without explicit `[Authorize]` decorations, anonymous web access is enabled by default in ASP.NET Core.

- **Remediation**: Apply `[Authorize]` at the controller level or per-action, and configure it to require Jellyfin's authentication handler (e.g. `[Authorize(AuthenticationSchemes = "Jellyfin")]`) or a custom policy that validates the user session. Admin routes (`/admin/*`) should additionally require an admin role or policy. Consult the Jellyfin plugin authentication pattern using `IAuthenticationService` or the plugin base class's auth configuration.

---

### [Critical] Server-Side Request Forgery (SSRF) via Unvalidated Peer URL

- **File**: `src/Services/FederationService.cs`, lines 18-46, 75-91
- **Description**: `SyncIncomingShareAsync` and `FetchLibraryMetadataAsync` accept a `peerServerUrl` string parameter and use it to construct outbound HTTP requests without validating that the URL points to an allowed host. This value originates from `SharedLibrary.PeerServerUrl`, which is populated from untrusted external input (the invite URL provided by a peer server). An attacker could craft an invite URL with a `peerServerUrl` pointing at internal network resources (e.g. `http://169.254.169.254/latest/meta-data/` for cloud metadata, `http://localhost:9000/internal-api`, `http://10.0.0.1:8080/debug`).

  The `ParseInviteUrl` method (line 120-129) extracts the host/port from the invite URL without any allowlist or hostname validation, and this becomes the base for all subsequent HTTP calls.

- **Remediation**: Implement a strict allowlist of permitted peer server hostnames or IP ranges before issuing any HTTP request. Reject URLs that resolve to private/reserved IP ranges (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, `127.0.0.0/8`, `169.254.0.0/16`). Use `System.Net.Dns.GetHostAddresses()` to resolve the hostname and validate the resulting IP before making any HTTP call. Consider adding a per-library opt-in allowlist stored in plugin configuration.

---

### [High] Unvalidated Input on Public API Routes (IDOR Risk)

- **File**: `src/Controllers/ShareLinkController.cs`, lines 36-43
- **File**: `src/Controllers/StreamController.cs`, lines 17-48
- **Description**: Route parameters `linkId`, `fileId`, and `code` are passed directly into service methods without any format validation, length limits, or character allowlisting.

  In `StreamController.ParseFileId` (line 58-64), the `fileId` is split on `::` and the first segment is used verbatim as a URL for outbound requests. A malicious `fileId` could contain a crafted value like `http://evil.com//` or `localhost:9000/api/secret//`, enabling SSRF even without the peer URL path.

  There is no check that `linkId` corresponds to a link the requesting user owns. An attacker who can guess or enumerate a link ID can retrieve its catalog and stream its content -- a classic Insecure Direct Object Reference (IDOR).

  `ShareLinkController.GetLinks()` returns all active links with full details (including `InviteCode`) to any caller regardless of authentication.

- **Remediation**: Validate all route parameters with strict regex patterns (e.g. only alphanumeric characters, length limits). Implement proper authorization so that `GetLinks()` and `RevokeLink` are only accessible to authenticated admins. For streaming, verify the link belongs to the calling user's library before returning content.

---

### [High] Weak Invite Code Generation Using `Math.random()`

- **File**: `src/Configuration/configPage.html`, line 199
- **Description**: The configuration UI generates invite codes client-side using `Math.random().toString(36).slice(2)`. `Math.random()` is a seeded PRNG with approximately 53 bits of entropy and is trivially predictable from JavaScript. An attacker who observes one invite code can enumerate past and future codes generated by the same browser session, allowing them to hijack incoming share invitations.

  This is compounded by the absence of authentication on the invite acceptance endpoint (`POST /mediashare/invite/{code}`).

- **Remediation**: Generate invite codes server-side using `RandomNumberGenerator` (from `System.Security.Cryptography`) and store the resulting value in the database before returning it to the client. Codes should be at minimum 128 bits of entropy (32 hex characters). Remove the client-side code generation entirely and call a server endpoint instead.

---

### [High] Hardcoded Server URL Causes Broken Functionality and Information Disclosure

- **File**: `Jellyfin.Plugin.MediaShare/Plugin.cs`, line 39
- **Description**: The server URL is hardcoded as `"http://localhost:8096"`. All share link invite URLs are generated using this value, meaning remote peers will receive invite links pointing to `http://localhost:8096` -- which is only resolvable on the local machine. Remote peers can never use these links correctly. Additionally, this hardcoded URL may leak internal infrastructure details if invite URLs are shared over untrusted networks.

- **Remediation**: Determine the server URL dynamically from the incoming HTTP request (e.g. `Request.Scheme`, `Request.Host`) in the controller layer, or inject the correct base URL from the Jellyfin host configuration (`IServerApplicationHost`). Remove the hardcoded constant.

---

### [High] SSRF via `fileId` Parameter in Stream Controller

- **File**: `src/Controllers/StreamController.cs`, lines 58-64, 26-27
- **Description**: The `fileId` path parameter (format `{peerServerUrl}::{itemId}`) is parsed by splitting on `::`. The extracted `peerUrl` is used directly as the target for an outbound HTTP request via `_http.GetAsync($"{peerUrl}/mediashare/file/{itemId}")`. An attacker can supply a `fileId` like `http://internal-service:9000/api::item123` to trigger a request to an internal host.

  No validation confirms that `peerUrl` is a legitimate peer server URL or that it belongs to an active share link. The `linkId` is checked for existence but not for ownership or trust.

- **Remediation**: Validate that `peerUrl` matches the `PeerServerUrl` stored in the database for the given `linkId`. Reject any `fileId` whose `peerUrl` does not correspond to a known share link. Apply the same SSRF protections described in the SSRF finding above.

---

### [Medium] Cross-Site Scripting (XSS) in Configuration UI

- **File**: `src/Configuration/configPage.html`, lines 141, 159, 176-179
- **Description**: User-controlled values from the plugin configuration (`s.peerServerUrl`, `s.libraryName`, `l.inviteUrl`) are interpolated directly into the DOM via `innerHTML` without HTML encoding. If any of these values contain malicious HTML/JavaScript (e.g. a peer server URL like `javascript:alert(1)`), the script will execute in the context of the Jellyfin admin panel.

  The configuration values flow from external peer servers (via invite URLs) and are stored in the plugin config, making this a stored XSS risk.

- **Remediation**: Replace all `innerHTML` interpolations with textContent assignments or use a sanitization library (e.g. DOMPurify) to sanitize values before inserting them into the DOM. For the `inviteUrl` and `peerServerUrl` fields specifically, validate that they are valid HTTP(S) URLs and reject `javascript:` or `data:` URIs.

---

### [Medium] XML Generation Without Entity Expansion Prevention (XXE Precursor)

- **File**: `src/Services/FederationService.cs`, lines 107-113
- **Description**: NFO files are generated using string concatenation with raw `item.Title` values embedded directly into an XML document. While `System.Security.SecurityElement.Escape()` is used for the title, this only prevents XML text content injection -- it does not prevent XML external entity injection if the generated file is parsed by an XML parser with DTD processing enabled. Additionally, other fields (e.g. `item.Year`) are embedded without any encoding, and the overall XML structure has no `<?xml ...?>` declaration with explicit `ISOLATIN` or feature-disabling settings.

  If any external tool reads these generated NFO files with an unsafe XML parser configuration, XXE could be triggered.

- **Remediation**: Use `System.Xml.XmlWriter` with `XmlWriterSettings.DtdProcessing = DtdProcessing.Prohibit` and `XmlWriterSettings.XmlSecurity = XmlSecurity.None` to generate the NFO XML safely. Encode all field values using the XML writer's proper encoding. Consider using a dedicated DTO with serialization attributes rather than raw string concatenation.

---

### [Medium] Directory Traversal Risk in Shared File Path

- **File**: `src/Services/FederationService.cs`, lines 96, 103-104
- **Description**: The root path for `.strm` files is constructed using `shareLinkId` from the database: `Path.Combine(..., "shared", shareLinkId)`. Although `shareLinkId` is a GUID generated by the local server (so it is trusted in the incoming-share flow), `GenerateStrmFilesAsync` is called from `SyncIncomingShareAsync` where `shareLinkId` originates from the peer server's catalog response. A malicious peer server could return a `shareLinkId` containing path traversal sequences (e.g. `../../../../etc`). Combined with the known server-side path, this could write `.strm` files outside the intended directory.

  The `SanitizeFileName` function only removes invalid filename characters but does not strip `..` path components.

- **Remediation**: Validate `shareLinkId` as a strict GUID format (using `Guid.TryParse()`) before using it in any filesystem path. Reject any value that does not conform to a GUID pattern. As a defense-in-depth measure, use `Path.GetFullPath()` after `Path.Combine()` and verify the resulting path starts with the expected root directory.

---

### [Medium] Range Header Parsing Does Not Validate Bounds

- **File**: `src/Controllers/StreamController.cs`, lines 50-56
- **Description**: The `ParseRange` method parses bytes range headers without validating that `start <= end`, that `start` and `end` are non-negative, or that `end` does not exceed `total`. Malformed or adversarial range values (e.g. `bytes=99999999-`) could cause incorrect `Content-Range` headers to be sent, incorrect byte ranges to be returned, or exceptions from `long.Parse` on out-of-range values (though the try-catch in the calling code will suppress this). The partial stream implementation at line 38 reads the entire stream without honoring the parsed range.

- **Remediation**: Validate that `start <= end`, both values are non-negative, and `end < total`. Return a `416 Range Not Satisfiable` response when the range is invalid. The partial-stream logic currently ignores the parsed range and copies the full stream regardless -- it should read only `end - start + 1` bytes from the stream.

---

### [Medium] Weak Cryptographic PRNG for Share Link IDs

- **File**: `src/Models/ShareLink.cs`, line 5
- **File**: `src/Models/SharedLibrary.cs`, line 5
- **Description**: Both model classes initialize `Id` using `Guid.NewGuid().ToString("N")`. In .NET, `Guid.NewGuid()` uses `System.Security.Cryptography.RandomNumberGenerator` internally and is cryptographically strong. However, when combined with the weak client-side code generation in `configPage.html` (which generates invite codes that can be guessed via `Math.random()`), the overall link ID entropy is weakened by the invite code path.

  More importantly, `ShareLink.InviteCode` is generated client-side using `Math.random()` (see above), and the server-side `CreateShareLink` method does not regenerate it -- it accepts whatever code the client sends.

- **Remediation**: Generate all secret values (invite codes, link IDs) server-side using `RandomNumberGenerator` with at least 128 bits of entropy. Remove client-side code generation from the HTML and replace with a server API call.

---

### [Low] Content-Type Taken Directly From Untrusted Proxy Response

- **File**: `src/Controllers/StreamController.cs`, line 29
- **Description**: The `contentType` for the streamed response is taken from `resp.Content.Headers.ContentType?.MediaType`, which is controlled by the peer server. A malicious peer could return a `Content-Type` of `text/html` or `application/javascript`, and if combined with a downstream cache or browser, could lead to content sniffing issues. This is low severity because the response is streamed directly to the client and the primary risk (XSS from the peer) is already present in the architecture.

- **Remediation**: Validate the returned `MediaType` against an allowlist of expected media types (e.g. `video/*`, `audio/*`, `application/octet-stream`) before setting the response Content-Type header.

---

### [Low] No Rate Limiting on Public Endpoints

- **File**: `src/Controllers/ShareLinkController.cs`
- **File**: `src/Controllers/StreamController.cs`
- **Description**: No rate limiting or throttling is applied to any endpoint. An attacker could rapidly enumerate link IDs, trigger many catalog fetches from peer servers, or stream large amounts of data by sending many requests to the `/stream` endpoint.

- **Remediation**: Apply ASP.NET Core rate limiting middleware (available in `Microsoft.AspNetCore.RateLimiting` in .NET 7+) to all public endpoints, with particular limits on `/invite/{code}`, `/stream`, and `/admin/*` routes.

---

## Dependency Vulnerability Notes

The following non-framework transitive dependencies were detected. Direct references are:

| Package | Version | Notes |
|---|---|---|
| LiteDB | 5.0.21 | Multiple CVEs for crafted file/DOS; see CVE-2021-42161, CVE-2021-43297. Upgrade path is limited by Jellyfin's plugin SDK constraints. |
| Microsoft.EntityFrameworkCore | 9.0.11 | Patch release; check CVE database for full list. |
| System.Text.Json | 9.0.11 | Patch release; ensure no deserialization vulns in use. |
| Jellyfin.Controller/Model | 10.11.8 | Follow Jellyfin server version. |

Consider checking [GitHub Advisory Database](https://github.com/advisories) and [NIST NVD](https://nvd.nist.gov) for the above packages for any applicable CVEs. Update to the latest compatible patch versions where feasible within the Jellyfin plugin SDK constraints.

---

## Recommendations Summary (Priority Order)

1. **Add `[Authorize]` to all controller routes** and configure Jellyfin authentication -- this is the single highest-impact fix.
2. **Fix SSRF**: validate peer server URLs against an IP allowlist before making outbound HTTP requests. Validate `fileId` peerUrl against the stored link.
3. **Generate invite codes server-side** using `RandomNumberGenerator`; remove `Math.random()` from the HTML.
4. **Replace hardcoded `http://localhost:8096`** with the real server base URL from Jellyfin's host configuration.
5. **Fix stored XSS** in configPage.html by using textContent or DOMPurify sanitization.
6. **Validate `shareLinkId` as a GUID** before using it in any filesystem path.
7. **Fix partial-stream range parsing** logic (currently ignores the range and streams everything).
8. **Apply rate limiting** to all public endpoints.
9. **Review LiteDB version** for known CVEs and assess risk within the plugin threat model.