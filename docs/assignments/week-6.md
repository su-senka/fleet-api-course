# Week 6 - Sub-resources, and serving something that is not JSON

<!--
  A stub. The specification for this week is written by the course author.

  What is already true, and does not need restating here:
    Endpoint stub   src/Fleet.Api.Workshop/Endpoints/CertificateEndpoints.cs
    Module contract IDriverService, IBlobStore
    Reference       src/Fleet.Api/ implements Vehicles and Bookings in full

  Everything the students call is finished, tested and seeded. The assignment is the HTTP layer
  over it, and the decisions that layer has to make.
-->

## Goal

Add Certificates as a sub-resource of Drivers, and serve its scanned document - the first response
in this course that is not JSON. Two finished dependencies do this together:
`IDriverService.AddCertificateAsync` / `ListCertificatesAsync` (`Fleet.Modules.Drivers.Contracts`)
for the data, and `IBlobStore` (`Fleet.Common.Storage`) for the bytes. `IBlobStore` is already
registered - `AddFleetInfrastructure` calls `services.AddScoped<IBlobStore, AzureBlobStore>()`
whenever a `Blobs` connection string is configured, which Compose provides - so there is nothing to
wire in `Program.cs` this week.

Notice what `IDriverService` does *not* give you: a way to fetch one certificate by id.
`ListCertificatesAsync(driverId)` returns all of a driver's certificates, current and superseded,
newest expiry first - finding one specific certificate to serve its scan means listing and picking
the one whose `Id` matches, not adding a database call the module doesn't offer.

Certificates carry a scanned document as a `ScanBlobId` on `CertificateDto` - a blob id, not a URL.
`IBlobStore.GetAsync` returns a `BlobContent` (`Content` stream, `ContentType`, `FileName`,
`Length`) or `null` when the id doesn't resolve to anything stored. Turning that into something a
browser can open - the right `Content-Type`, the right filename, a `404` rather than an exception
when the id is real but the blob is gone - is this week's actual content; the JSON parts of this
resource are the same shape you've already written twice for Vehicles and Drivers.

There is no reference implementation for Certificates in `src/Fleet.Api` to read afterwards -
same as weeks 1, 5 and 11-14.

## What to build

In `Fleet.Api.Workshop/Endpoints/CertificateEndpoints.cs`, grouped under
`/drivers/{driverId}/certificates` - a certificate only exists inside a driver, which is the
argument for nesting it rather than a top-level `/certificates`, though the file's own remarks
already note it's worth being able to defend either:

- **`GET /drivers/{driverId}/certificates`** - calls `ListCertificatesAsync`. `ErrorKind.NotFound`
  (unknown `driverId`) maps to `404`. A driver that exists but holds no certificates is a `200`
  with an empty array - a different answer from a driver that doesn't exist at all, and the stub
  comment already warns against collapsing the two.
- **`POST /drivers/{driverId}/certificates`** - maps a request to `AddCertificateCommand` (`Kind`,
  `Number`, `IssuedOn`, `ExpiresOn`, `ScanBlobId`) and calls `AddCertificateAsync`. `ScanBlobId` is
  nullable specifically so a certificate can be recorded before a scan exists for it - decide, and
  state in your PR description, whether this endpoint accepts the file in the same request (in
  which case save it via `IBlobStore.SaveAsync` first to get the id the command needs) or leaves
  attaching a scan for later. Map `ErrorKind.NotFound` (unknown `driverId`) to `404` and
  `ErrorKind.Validation` (from `Certificate.Issue` - an empty number, a number over 40 characters,
  an unknown `Kind`, or `ExpiresOn` before `IssuedOn`) to `400`.
- **`GET /drivers/{driverId}/certificates/{certificateId}/scan`** - find the matching certificate
  via `ListCertificatesAsync`, `404` if the driver or that certificate id doesn't exist, `404`
  again if its `ScanBlobId` is `null` (nothing was ever uploaded). Otherwise call
  `IBlobStore.GetAsync(blobId)` - `null` here is the dangling-reference case the stub's remarks
  call out explicitly, also a `404`, not an unhandled exception - and stream `BlobContent.Content`
  back with its `ContentType` and `FileName` rather than buffering it into a `byte[]` first;
  `IBlobStore`'s own remarks explain why that distinction matters for a large file.

**Do not add:** authentication, an `ETag` or caching header on the scan response, or automated
tests beyond confirming the endpoints by hand - each of those is a later week's subject and adding
it now just makes it harder to compare everyone's PRs against the same bar.

## What to hand in

A pull request from your own repository (created from the template), containing:

- The three endpoints described above.
- A short PR description defending the route shape you chose (nested under `/drivers` versus a
  top-level `/certificates`) and how you designed the scan upload.
- Evidence it works: paste at least four request/response pairs into the PR description - one
  successful `POST` that records a certificate with a scan, one `GET` on the certificates list
  showing it, one `GET .../scan` returning the file with the right `Content-Type`, and one
  `GET .../scan` for an id that doesn't exist, returning `404` rather than a stack trace.

**Reviewer checklist:**
- [ ] Builds and runs against the Compose stack
- [ ] `GET` certificates for an unknown driver is `404`; for a driver with none is `200` with `[]`
- [ ] The scan endpoint streams `IBlobStore`'s content directly rather than reading it fully into
      memory first
- [ ] A missing or dangling `ScanBlobId` returns `404`, not an unhandled exception
- [ ] The scan response's `Content-Type` matches what was uploaded
- [ ] PR description defends the route shape and the scan-upload design
