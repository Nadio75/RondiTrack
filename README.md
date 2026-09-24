# RondiTrack
# RondiTrack: API Foundations & Domain Modeling

RondiTrack is the backend API foundation for tracking **stokvels** — rotating
savings and credit associations. This assignment covers the initial domain
model (`User`, `Stokvel`) and full CRUD endpoints over an in-memory store.

## Running the project

1. Make sure the .NET 10 SDK is installed (`dotnet --version` should show `10.x`).
2. From the project root:
   ```bash
   dotnet run
   ```
3. Once running, open the Scalar API reference in your browser:
   ```
   http://localhost:5056/scalar
   ```
   (Port may differ — check the terminal output for the exact `Now listening on:` URL.)
4. All endpoints can be explored and tested directly from the Scalar UI,
   including the failure cases described below (no external tools like
   Postman required).

## Architecture choice: Controllers vs Minimal APIs

I chose **Controllers (`ControllerBase`)** for this assignment.

- The class-per-resource structure (`UsersController`, `StokvelsController`)
  keeps each resource's endpoints grouped in one place, which made it easier
  to reason about as the number of routes grew (CRUD + the nested
  `members` relationship).
- `[ApiController]` gives automatic model-binding validation for malformed
  request bodies for free, without needing a separate validation layer.
- Coming from [your background — e.g. "prior exposure to MVC-style
  frameworks" / "this being new to me but preferring the explicit class
  structure over free-floating route delegates"], Controllers were the more
  approachable structure to keep organized and readable.

Minimal APIs would have been a reasonable alternative — less boilerplate,
and the route/handler pairing is more visible at a glance — but for a
project with two related resources and a nested relationship route, I found
the Controller-per-resource grouping easier to keep navigable.

## Domain rules enforced

Rather than validating input in the controllers, validation and invariants
live **on the entities themselves** (`Models/User.cs`, `Models/Stokvel.cs`),
so an invalid `User` or `Stokvel` cannot be constructed or mutated into an
invalid state through any code path — not just checked for in one place.

A `User` must have a non-blank name and contact number, enforced in the
`User` constructor as well as `Rename` and `UpdateContactNumber`. A user
record with no way to identify or reach them isn't a usable member of a
stokvel.

A `Stokvel` must have a non-blank name, enforced in the `Stokvel`
constructor and `Rename`, for the same reason — an unnamed savings group
can't be meaningfully referenced.

A `Stokvel`'s contribution amount must be greater than zero, enforced in
the constructor and `UpdateContributionAmount`. A stokvel exists to pool
money on a schedule, so a zero or negative contribution amount is
meaningless in the real-world process this models.

A user cannot be added to a `Stokvel` they're already a member of, enforced
in `Stokvel.AddMember`. Membership is binary — you either belong to a
group or you don't — and allowing duplicates would corrupt any later logic
built on member counts or contribution totals.

A user cannot be removed from a `Stokvel` they aren't a member of, enforced
in `Stokvel.RemoveMember`. This prevents silently "succeeding" at an
operation that didn't actually correspond to a real state change.

The membership list (`_memberIds`) is a private field exposed only as
`IReadOnlyCollection<Guid>`, so no code outside `Stokvel` can add or remove
members without going through `AddMember`/`RemoveMember` — the validation
above is not optional or bypassable.

**Money type:** `ContributionAmount` is `decimal`, not `float`/`double`,
since `decimal` represents currency values exactly (base-10), whereas
binary floating-point types can introduce rounding errors unacceptable for
tracking real contributions.

## Status codes

`200 OK` is returned for a successful `GET` or `PUT`. `201 Created` is
returned for a successful `POST`, with a `Location` header via
`CreatedAtAction` pointing at the new resource. `204 No Content` is
returned for a successful `DELETE`, and for a successful member add or
remove. `400 Bad Request` is returned when input fails entity validation —
a blank name or a non-positive contribution amount. `404 Not Found` is
returned when the referenced user, stokvel, or membership doesn't exist.
`409 Conflict` is returned when the request is well-formed and both
resources exist, but the operation conflicts with the current state — for
example, adding a user who is already a member.

## Endpoints

```
GET    /api/users
GET    /api/users/{id}
POST   /api/users
PUT    /api/users/{id}
DELETE /api/users/{id}

GET    /api/stokvels
GET    /api/stokvels/{id}
POST   /api/stokvels
PUT    /api/stokvels/{id}
DELETE /api/stokvels/{id}

POST   /api/stokvels/{id}/members            (body: { "userId": "<guid>" })
DELETE /api/stokvels/{id}/members/{userId}
```

## Constraints followed

- In-memory data only (`InMemoryStokvelStore`) — no EF Core or database.
- No service layer, DTOs, validation attributes, centralized exception
  handling, or authentication — all validation lives on the domain entities
  and is translated to HTTP responses directly in the controllers.
- All endpoint and data-access methods are `async`, even though the
  in-memory store has no real I/O yet, per the assignment's async-by-default
  requirement.

  ## DTOs & Mapping

Every endpoint sends and receives a DTO, never a domain entity directly.
`User`, `Stokvel`, and `Contribution` never cross the HTTP boundary in
either direction.

- **Response DTOs** (`UserResponse`, `StokvelResponse`,
  `ContributionResponse`) decide what a caller actually needs to see —
  for example, `StokvelResponse` exposes a `MemberCount` instead of the
  raw internal list of member ids.
- **Request DTOs** (`CreateUserRequest`, `CreateStokvelRequest`,
  `AddMemberRequest`, `ContributionRequest`) decide what a caller is
  allowed to send in. This closes the over-posting risk of binding
  directly to an entity — a caller can never set fields like `Id` or
  `CreatedAt` that should only ever be decided by the system.

Mapping is done by hand, in one static `FromX` method per DTO, called
through a thin `Mapper` class (`UserMapper`, `StokvelMapper`,
`ContributionMapper`) — one clear place per entity to see how it becomes
a response.

**Why manual mapping is the right call here, constraint aside:** this
project moves money. A mapping library infers property matches by name
and reflection — that's exactly the kind of implicit behavior you don't
want near a financial record, where a renamed or reordered field could
silently misalign, mask a security issue like accidental over-exposure,
or introduce a subtle bug that's hard to trace back to its source.
Writing the conversion by hand means every field that leaves the API is
a deliberate, visible decision, not an inferred one.

## Service Layer

Two operations were moved into a service layer, because both involve a
decision that spans more than one entity and isn't simple field
validation:

- **`StokvelMembershipService.AddMemberAsync`** — checks the stokvel
  exists, the user exists, and enforces the actual business rule (a
  user can't join the same stokvel twice).
- **`RecordContributionService.ExecuteAsync`** — checks the stokvel and
  user exist, confirms the user is actually a member, enforces the
  duplicate-contribution rule (a member can't pay the same cycle
  twice), and owns the idempotency check end to end.

Every other endpoint (plain create/read/update/delete on `User` and
`Stokvel`) has no cross-entity decision to make, so it talks to the
store directly — pulling that into a service would just be an
unnecessary pass-through layer.

Controllers only call a service (or store), inspect the outcome via
`ServiceResult<T>`, and translate that outcome into an HTTP status code
via `Problem()`/`ToProblem()`. There is no business `if` left in any
controller action.

## Idempotency Design

The contribution-recording endpoint requires an `Idempotency-Key`
header, checked in `RecordContributionService.ExecuteAsync` before any
other work happens:

1. The incoming request body is hashed (SHA-256 over its serialized
   JSON) to get a fingerprint of its exact content.
2. The service looks up the given key in `IIdempotencyStore`.
   - **Key not found** → this is a new request. It proceeds through the
     normal validation and business rules below.
   - **Key found, hash matches** → this is a retry of a request already
     handled. The service returns the *exact original response* it
     saved the first time, without touching the contribution store
     again — nothing is recorded twice.
   - **Key found, hash differs** → the same key was reused with a
     different payload. This is rejected with a 409 Conflict, since
     silently accepting it could mean a different contribution gets
     recorded under a key the caller believes belongs to their original
     request.
3. Once a new contribution is successfully validated and saved, the
   service writes an `IdempotencyRecord` (key, request hash, status
   code, serialized response body) to `IIdempotencyStore` — so any
   future retry with that key short-circuits at step 2 instead of
   re-running the write.

The idempotency store is a second in-memory abstraction
(`IIdempotencyStore` / `InMemoryIdempotencyStore`), same pattern as the
other repositories — not a database, per this week's constraints.

## Error Shape — RFC 9457 Problem Details

Every error response across the whole API — 4.1's endpoints included —
returns `application/problem+json`, via ASP.NET Core's built-in
`Problem()` method and a small `ToProblem()` extension that maps a
`ServiceResultStatus` to the matching status code. A client never needs
a second code path to parse a failure, regardless of which endpoint
produced it.

## Status Code Reasoning

| Situation | Code | Reasoning |
|---|---|---|
| Malformed request (bad JSON, missing field, missing header) | 400 | Server can't process what was sent |
| Well-formed request that fails a business rule | 422 | Server understood it; the value is invalid |
| Resource referenced doesn't exist | 404 | Nothing to act on |
| Resource exists but the action conflicts with current state | 409 | Duplicate membership, duplicate contribution, reused idempotency key with a different body |
| Create succeeds | 201 | Includes a `Location` header via `CreatedAtAction` |
| Update succeeds | 200 | Returns the updated resource |
| Delete succeeds | 204 | Nothing to return |

### 400 vs 422

Applied in `RecordContributionService.ExecuteAsync` when constructing a
new `Contribution`: 400 is for a request that is malformed — invalid
JSON, or a required field missing entirely, which ASP.NET Core's model
binding catches before the service is even reached. 422 is for a
request that is well-formed JSON but violates a business rule once
evaluated — e.g. `Amount: -50`. `Contribution`'s constructor throws an
`ArgumentException` for this, which the service translates into
`ServiceResultStatus.Unprocessable`, mapped by the controller to 422.
In short: 400 means "I couldn't understand what you sent." 422 means "I
understood it perfectly, but the rules say no."

### Why the idempotency conflict is 409, not 422

A reused `Idempotency-Key` with a different payload is well-formed —
nothing about the JSON itself is invalid, and taken on its own the
request could otherwise succeed. What makes it fail is that it
conflicts with something the server already knows: this key was already
used to mean something else. That's a **state conflict**, not a value
violation, which is exactly what 409 is for — the same reasoning behind
using 409 for "already a member" and "already paid this cycle." 422 was
considered, since it's arguably a client-logic error, but 409 was chosen
to keep all three "this contradicts what the server already has on
record" cases consistent under one code.

## Problems Encountered

A record of the real issues hit while building this, since most of them
weren't about RondiTrack's code being wrong.

**1. `IContributionStore` registration silently disappeared from
`Program.cs`.** Mid-way through troubleshooting an unrelated NuGet
issue, the `builder.Services.AddSingleton<IContributionStore,
InMemoryContributionStore>();` line was accidentally deleted while
editing a nearby line. The build still succeeded (the type itself
compiles fine everywhere else), but the app crashed on startup with
`Unable to resolve service for type 'RondiTrack.Data.IContributionStore'`
— a good reminder that a clean build only proves your code compiles, not
that the DI container has everything it needs. Fixed by re-adding the
registration line.

**2. Duplicate nested project folder.** A second `RondiTrack` folder
ended up sitting inside the real project folder
(`RondiTrack\RondiTrack\`), containing its own `Program.cs` with
top-level statements. C# only allows one file with top-level statements
per project, so this produced `Only one compilation unit can have
top-level statements`. Found it by searching the whole project for
`CreateBuilder` and seeing it appear in two files; fixed by deleting the
nested duplicate folder entirely and doing a clean rebuild.

**3. Windows Smart App Control blocking the compiled DLL.** After a
clean build, `dotnet run` threw
`System.IO.FileLoadException: ... An Application Control policy has
blocked this file.` This had nothing to do with the code — Smart App
Control (a Windows security feature) was blocking the freshly built,
unsigned `RondiTrack.dll` from loading, which is a known friction point
with .NET SDK dev builds since debug DLLs are unsigned and rebuilt
constantly. Resolved by turning Smart App Control off in Windows
Security (noting this is a one-way setting — it can't be re-enabled
without reinstalling Windows).

**4. Stale build/IntelliSense cache after adding new packages.** After
confirming `Microsoft.AspNetCore.OpenApi` and `Scalar.AspNetCore` were
both correctly listed in `RondiTrack.csproj`, VS Code still showed red
squiggles on `AddOpenApi`, `MapOpenApi`, and `MapScalarApiReference` as
if the types didn't exist. `dotnet build` from the terminal actually
succeeded the whole time — this was purely a stale restore/IntelliSense
cache in the editor, not a real compile error. Fixed with a full clean:
delete `bin`/`obj`, `dotnet restore`, `dotnet build`, then fully close
and reopen VS Code so OmniSharp re-reads the fresh restore.

**Takeaway:** most of these weren't logic bugs in RondiTrack itself —
they were environment/tooling issues (a stray folder, a security
feature, a stale cache, one accidentally deleted line) that produced
error messages easy to mistake for something wrong with the design.
Worth separating "does the compiler accept this" from "does the app
actually start and behave correctly" when debugging — a successful
build is necessary but not sufficient.

## Assignment 4.3
## Validation vs Exceptions

Every request DTO now has a validator that runs before anything else happens. These validators only check whether a request is shaped properly — is a name actually filled in, is an amount a positive number, does a date look like a real date. 

They never look anything up in the data, and they never care whether a stokvel or user actually exists. If a request fails one of these checks, it never even reaches a controller or a service — it gets rejected straight away with a message explaining what's wrong.

Everything else — checking whether a record actually exists, or whether an action is allowed given what's already happened is handled by throwing an exception instead. 

The difference is really about timing and what kind of question is being asked. Validation asks "is this request even written properly?" before anything real happens. Exceptions get thrown once we've already started doing real work and discover something that isn't allowed — like a stokvel that doesn't exist, or a member who already paid this month.

Put simply: validation catches mistakes in how a request was written, exceptions catch reasons an otherwise well-written request still can't go through.

## The Exception Hierarchy

Four exception types cover every way a request can fail once it's already passed validation. 

A not-found exception is used any time something a request refers to — a stokvel, a user, a contribution cycle — simply doesn't exist.

A conflict exception is used when a request is technically fine but contradicts something that's already true, like trying to join a stokvel a person is already part of, or paying for a cycle that's already been paid.

A separate idempotency-conflict exception exists specifically for when someone reuses the same idempotency key but sends a different request body the second time. 

And a business-rule exception is used for anything that passes basic validation but still breaks a rule the system cares about, like an amount that's technically a number but isn't allowed to be zero or negative.

The idempotency-key conflict got its own exception type on purpose, even though it currently results in the same status code as a normal conflict.

The reasoning is that these two situations aren't really the same kind of problem. 

A duplicate contribution is about something that's already true in the real world — this person already paid. 

An idempotency-key conflict isn't about stokvels or payments at all it's about someone reusing a retry key incorrectly, which is a completely different kind of mistake. 

Keeping them as separate exception types means if the system ever needs to treat them differently later — different logging, a different message, maybe even a different status code down the line — that decision is already easy to make, because the code already tells the two situations apart.

## Every failure goes through one place now

Instead of each controller building its own error response by hand, every exception thrown anywhere in the app gets caught by a single handler. 

That handler decides what status code to send back, writes a log entry, and builds the response in the same consistent shape every time. This means no controller action needs to know anything about HTTP status codes anymore it just does its job and either succeeds or throws, and the handler takes care of turning that into something the caller can understand. 

All of the old hand-written error responses from earlier in the project were removed once this was in place, so there's now exactly one place responsible for turning a failure into a response.

## ContributionCycle — did it need a service?

ContributionCycle didn't end up needing its own service. Everything it actually requires is a straightforward lookup followed by a decision that only depends on one thing at a time — does the stokvel exist, and does a cycle for that period already exist. Neither of those is really a decision in the way earlier features needed one. Adding a member or recording a contribution both involved weighing multiple related facts together before deciding what to do. Creating a contribution cycle doesn't — it's really just "check one thing, then either allow it or reject it," which a controller talking directly to the data store can handle perfectly well on its own.

Giving it a service anyway would have just added an extra layer of code that didn't actually do anything a controller couldn't already do cleanly, so it was left out.

## Correlation IDs

Every error response now comes back with an ID attached to it, and that same ID also appears in the matching log entry on the server. 

The idea is that if something goes wrong, the ID from the response a user saw can be used to find the exact matching entry in the logs, instead of having to guess which log line belongs to which request. For example, a failed request that returns a "stokvel not found" error will include something like correlationId: 0HN7F8G3K2J1L:00000001 in its response body, and the server log for that same request will show a line like "Handled NotFoundException: Stokvel not found. [correlationId=0HN7F8G3K2J1L:00000001]"
— same id, both places. That's what makes it possible to trace a specific failure back to exactly what happened on the server, rather than just knowing that something, somewhere, went wrong.

## Negative-Path Tests

A small xUnit test project sits alongside the main project and proves that the error handling actually works the way it's supposed to, not just that it looks right in Scalar. 

There are four tests in total. One sends a stokvel-creation request with an empty name and a negative amount, and checks that it comes back as a 400 with the problem+json content type — this is checking that FluentValidation is actually running and rejecting bad input before anything else happens. Another asks for a stokvel using a random id that was never created, and checks that it comes back as a 404 with the same problem+json shape — this proves a not-found exception actually reaches the centralized handler correctly. The third one creates a real stokvel, creates a contribution cycle for it, then tries to create the exact same cycle again, and checks that the second attempt comes back as a 409 — this proves a genuine business-rule conflict gets caught and handled
the same way as everything else. 

Together these three cover a different layer of the system each — the very first check a request goes through, something further down that depends on the data store, and an actual rule
about what's allowed to happen twice. If any of the routing between validation, the exception hierarchy, and the handler ever broke, one of these tests would fail and say exactly why.

These tests actually caught a real bug while they were being written — an earlier version of the exception handler was quietly returning application/json instead of application/problem+json on every single error response, because of how a built-in .NET method behaves when you set a content type by hand right before calling it. The two tests checking for problem+json failed immediately and pointed straight at the exact line causing it, which is exactly the kind of regression this kind of test is meant to catch before it ships.

## Scalar Demonstration

Three real requests and their real responses, run directly against the
running app through Scalar, showing the same problem+json shape holding up
across a malformed request, a missing resource, and a business-rule
conflict.

### A malformed request (400)

**Request:** `POST /api/stokvels`

```json
{
  "name": "639c7141-83f9-4c5f-b965-e10e3d4cac14",
  "contributionAmount": -5
}
```

**Response:**

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "ContributionAmount": [
      "Contribution amount must be greater than zero."
    ]
  }
}
```

This request never reached a controller or the exception hierarchy at all
— FluentValidation rejected it before anything else ran, which is why the shape here is slightly different (an `errors` object listing every broken
field) from the other two below, which come from the centralized exception
handler instead.

### A request for something that doesn't exist (404)

**Request:** `GET /api/stokvels/630c7141-83f9-4c5f-b965-e10e3d4cac14`

(a well-formed GUID that was never created)

**Response:**

```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "Stokvel not found.",
  "correlationId": "0HNOPQOLK2QFB:0000000A"
}
```

### A business-rule conflict (409)

**Request:** `POST /api/stokvels/639c7141-83f9-4c5f-b965-e10e3d4cac14/cycles`,
sent twice with the same period:

```json
{
  "period": "2026-09",
  "targetAmount": 1
}
```

**Response (on the second attempt):**

```json
{
  "title": "Conflict",
  "status": 409,
  "detail": "A contribution cycle for this period already exists for this stokvel.",
  "correlationId": "0HNOPQOLK2QFD:00000002"
}
```

The first attempt succeeded and created the cycle; the second attempt hit
`ConflictException` because a cycle for that exact stokvel and period already existed — proving the duplicate-period rule is actually enforced, not just implemented and untested.

## Correlation ID Walkthrough

Here is one real response from the app next to the exact log line it produced on the server, showing the same id appearing in both places. This uses the 404 case above.

**Response body:**

```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "Stokvel not found.",
  "correlationId": "0HNOPQOLK2QFB:0000000A"
}
```

**Matching server log line:**