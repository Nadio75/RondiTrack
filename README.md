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

## Assignment 4.4 — Documentation & Testing

This assignment added no endpoints and no rules. It documents every endpoint from 4.1 to 4.3 in Scalar (summary, guarantees, examples, and every realistic problem+json response) and adds a test suite in two layers: unit tests with no HTTP or DI container, and integration tests through `WebApplicationFactory` with the real validation, service layer and exception handler.

### Definition of Done

| # | Endpoint | Documented | Validated | Unit-tested (which rule) | Integration-tested (which cases) | Status codes reviewed |
|---|---|---|---|---|---|---|
| 1 | `GET /api/users` | yes | n/a (no input) | no (no rule, pure read) | 200 lists a created user | yes |
| 2 | `GET /api/users/{id}` | yes | yes (route must be a GUID) | no (no rule, pure read) | 200, 400 bad GUID, 404 | yes |
| 3 | `POST /api/users` | yes | yes (name required, max 100; contact number exactly 10 digits) | no (see gaps) | 201 + Location, 400 bad number, 400 blank name | yes |
| 4 | `PUT /api/users/{id}` | yes | yes (same as create) | no (see gaps) | 200, 400 before lookup, 404 | yes |
| 5 | `DELETE /api/users/{id}` | yes | yes (route must be a GUID) | no (no rule) | 204, then 404 on second delete | yes |
| 6 | `GET /api/stokvels` | yes | n/a (no input) | no (no rule, pure read) | 200 lists a created stokvel | yes |
| 7 | `GET /api/stokvels/{id}` | yes | yes (route must be a GUID) | no (no rule, pure read) | 200, 400 bad GUID, 404 | yes |
| 8 | `POST /api/stokvels` | yes | yes (name required, max 100; amount > 0) | yes: amount must be > 0, starts with no members | 201 + Location + zero members, 400 x3, name boundary 100/101 | yes |
| 9 | `PUT /api/stokvels/{id}` | yes | yes (same as create) | partly (amount rule via constructor only) | 200, 400 before lookup, 404 | yes |
| 10 | `DELETE /api/stokvels/{id}` | yes | yes (route must be a GUID) | no (no rule) | 204, then 404; cycles unreachable afterwards | yes |
| 11 | `POST /api/stokvels/{id}/members` | yes | yes (userId not empty GUID) | yes: membership rule (join once, both must exist) | 204 + member count, 409, 404 user, 404 stokvel, 400 empty GUID | yes |
| 12 | `POST /api/stokvels/{id}/contributions` | yes | yes (userId, cycle id not empty; amount > 0; header present) | yes: membership, cycle ownership, duplicate contribution, idempotency-key comparison | 201 + Location, 400 x2, 422 no key, 404 x3, 409 duplicate; 4 idempotency tests; 3 edge cases | yes |
| 13 | `GET /api/stokvels/{id}/cycles` | yes | yes (route must be a GUID) | no (no rule, pure read) | 200 only this stokvel's cycles, 200 empty array, 404 | yes |
| 14 | `GET /api/stokvels/{id}/cycles/{cycleId}` | yes | yes (routes must be GUIDs) | no (no rule, pure read) | 200, 404 unknown cycle, 404 via wrong stokvel | yes |
| 15 | `POST /api/stokvels/{id}/cycles` | yes | yes (period `YYYY-MM`; target > 0) | partly (target and period entity rules) | 201 + Location, 400 x3, 404, 409 duplicate period, month boundaries | yes |
| 16 | `PUT /api/stokvels/{id}/cycles/{cycleId}` | yes | yes (same as create) | no (see gaps) | 200, 400 before lookup, 404 | yes |
| 17 | `DELETE /api/stokvels/{id}/cycles/{cycleId}` | yes | yes (routes must be GUIDs) | no (no rule) | 204, then 404; paying a deleted cycle gives 404 | yes |

Rows where "Unit-tested" says "no (no rule)" are deliberate: those endpoints only read or remove data, so there is no business decision for a unit test to prove. Their behaviour is covered by the integration tests.

### Edge cases

| Edge case | How I found it | What the test asserts |
|---|---|---|
| Empty collection | Reading the list-cycles controller, the stokvel is checked before the list is fetched, which raised the question of what a real stokvel with no cycles returns. | `200` with an empty array, not `404`. |
| Boundary values | I read each validator's limits (the month pattern, the 100-character name limit, `amount > 0`) and tested both sides of every limit. | `2026-01` and `2026-12` are accepted; `2026-00`, `2026-13` and `2026-9` are rejected; a 100-character name is accepted and a 101-character one is rejected; `0.01` is accepted and `0` is rejected. |
| A valid request that depends on another resource | I read the contribution service and asked what each rule depends on: the cycle must exist and belong to this stokvel. | A real cycle cannot be fetched through another stokvel's route (`404`); a valid contribution to a deleted cycle gives `404`; cycles cannot be listed once their stokvel is deleted (`404`). |

### Test run

`dotnet test --logger "console;verbosity=detailed"` on the final commit:

```
Passed!  - Failed:     0, Passed:   101, Skipped:     0, Total:   101, Duration: 1 s - RondiTrack.Tests.dll (net10.0)
Test summary: total: 101, failed: 0, succeeded: 101, skipped: 0, duration: 4,3s
```

Every test by name:

```
Passed RondiTrack.Tests.ContributionsApiTests.A_member_who_already_paid_a_cycle_cannot_pay_it_again_with_a_new_key_and_gets_409 [20 ms]
Passed RondiTrack.Tests.ContributionsApiTests.A_user_who_is_not_a_member_of_the_stokvel_cannot_contribute_and_gets_404 [17 ms]
... (all 101 lines from test-run.txt, sorted by test class)
```

101 tests, 0 failed: 34 unit tests (no HTTP, no DI container) and 67 integration tests (through `WebApplicationFactory`, with validation, service layer and exception handler all running).

### Rule deliberately broken

I commented out the `RequestHash` comparison in `RecordContributionService.ExecuteAsync`, so a reused Idempotency-Key with a different body was no longer rejected. Four tests went red: three unit tests (`Reusing_a_key_with_a_different_amount_...`, `Reusing_a_key_with_a_different_cycle_...`, `A_rejected_key_reuse_records_nothing_new`) and one integration test (`The_same_key_with_a_different_amount_is_rejected_with_409`). I restored the file with `git restore` and the suite returned to all green.

### Gaps the audit surfaced and I did not close

- **Updating a cycle can create a duplicate period.** Creating a cycle returns `409` for a repeated period, but `PUT` does not check. This is written into the endpoint's description in Scalar. Fixing it would add a new business rule, which this assignment forbids, so it belongs in a future assignment.

- **The idempotency key is compared with the request body only, not the stokvel id in the URL.** Documented in Scalar for the same reason.

- **Scalar shows the `Idempotency-Key` header as optional though it is required** (a missing one gives `422`). Changing that would change the code, so the description says it in words.

- **Deleting leaves related data behind.** Deleting a stokvel leaves its cycles and contributions unreachable but stored; deleting a cycle leaves its contributions; deleting a user leaves them counted in any stokvel's `memberCount`, although they can no longer contribute (`404`). These need persistence and relationships to fix properly, which is Week 5.

- **No unit tests for `User` entity rules.** Its rules are shape rules already enforced by the validator and proven by the integration tests; adding a separate unit test would repeat the same check.

- **Idempotency keys live in memory.** They are lost on restart and never expire.

- **The `422` responses from the entity `catch` blocks are unreachable through the API**, because validation rejects the same input with `400` first. Only the missing-header `422` can be triggered.

## Assignment 5.1 — EF Core & Database Foundations

This assignment replaced RondiTrack's in-memory storage with real PostgreSQL persistence for two of its four repositories, and added Payout as a new, minimal feature protected by an explicit database transaction. The goal set by the assignment was that nothing above the repository interfaces — no controller, no service signature, no DTO — would need to change for this to work. It didn't.

### PostgreSQL setup

I installed PostgreSQL natively on Windows (not Docker — Docker Desktop wasn't already on this machine, and a native Windows service install is a smaller, more reliable first step than adding WSL2 as a dependency on top of everything else this assignment already asks for). Version: **PostgreSQL 18.6** (confirmed with `SELECT version();` in `psql`, shown below).

**How a teammate with a clean machine reproduces this from nothing:**

1. Download the Windows installer from [postgresql.org/download/windows](https://www.postgresql.org/download/windows/) and run it.
2. During install, set a password for the `postgres` superuser and keep the default port `5432`. The installer starts PostgreSQL automatically as a Windows service — no separate "start the database" step is needed afterward.
3. On the final screen, Stack Builder opens offering extra add-ons (pgAdmin, drivers, PostGIS, etc.). None of these are needed for RondiTrack — click **Cancel**.
4. Open **SQL Shell (psql)** from the Start menu. Press Enter through the connection prompts (Server, Database, Port, Username all default correctly) until prompted for the password, then enter it.
5. Create a dedicated database — not the shared default `postgres` one:
```sql
   CREATE DATABASE "RondiTrackDb";
   \c RondiTrackDb
   SELECT version();
```
6. This is the proof-of-connectivity step the assignment asks for, done independently of the API — `psql` connecting and returning a real PostgreSQL version string, before a single line of EF Core code was written:
```
   RondiTrackDb=# SELECT version();
                                    version
   -------------------------------------------------------------------------
    PostgreSQL 18.6 on x86_64-windows, compiled by msvc-19.44.35228, 64-bit
   (1 row)
```

From here, the real connection string is `Host=localhost;Port=5432;Database=RondiTrackDb;Username=postgres;Password=<the password set in step 2>` — stored only in User Secrets, never committed (see below).

### The mapping problem

I hit two separate EF Core mapping issues, both on `Contribution`, `ContributionCycle`, `Stokvel`, and `User` — every entity whose only constructor is the validating one it always had, with no setters on any property.

**First:** running `dotnet ef migrations add` failed outright with *"No suitable constructor was found for the type 'Contribution' — the following constructors had parameters that could not be bound to properties: stokvelId, userId, contributionCycleId."* EF Core's default materialization strategy tries to bind constructor parameters directly to properties by name, and couldn't resolve it for these read-only, Guid-typed parameters. The fix: I added a `private` parameterless constructor to each of the four entities. EF Core now materializes instances by writing straight to the compiler-generated backing fields, bypassing both the constructor and any setters — while every line of my own code still only ever calls the public, validating constructor. No validation was weakened.

**Second, and more surprising:** after fixing the first issue, generated migrations were still silently dropping properties with no error at all — `ContributionCycle.CreatedAt` disappeared from the schema entirely, while a property of the identical shape (`StokvelId`) survived only because a `HasIndex` call happened to reference it. I confirmed this directly with `dotnet ef dbcontext script`, which showed the compiled model itself only contained properties explicitly touched somewhere in `OnModelCreating` — plain get-only auto-properties were not being picked up by convention once the private constructors were added. Rather than chase why convention discovery behaved this way, I configured every property on every entity explicitly in `OnModelCreating`, so nothing is left to a convention decision that behaved unpredictably once.

*(Also worth being honest about: a chunk of the debugging time on this second issue was lost to repeatedly pasting the fix into the wrong open editor tab — `RondiTrackDbContextModelSnapshot.cs` instead of `RondiTrackDbContext.cs` — which looked identical to a stale-build problem until a direct `type` of the file on disk proved otherwise. Worth remembering for next time: verify a file's actual contents on disk before re-diagnosing.)*

### Secret management

The connection string, password included, is stored with **.NET User Secrets** and never appears in any file git tracks.

```powershell
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:RondiTrack" "Host=localhost;Port=5432;Database=RondiTrackDb;Username=postgres;Password=<your password>"
```

`dotnet user-secrets init` only adds a `<UserSecretsId>` GUID to `RondiTrack.csproj` — that's not a secret itself, just a pointer to a file under the Windows user profile (`%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json`), which lives entirely outside the repository and can never be committed by accident. `appsettings.json` only declares the key with an empty value:

```json
{ "ConnectionStrings": { "RondiTrack": "" } }
```

A teammate cloning the repo runs the two commands above with their own local password, and the app picks the value up automatically in Development — zero secrets ever touch source control.

### Retry configuration

```csharp
options.UseNpgsql(
    builder.Configuration.GetConnectionString("RondiTrack"),
    npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(
        maxRetryCount: 5,
        maxRetryDelay: TimeSpan.FromSeconds(10),
        errorCodesToAdd: null));
```

**5 retries, 10-second max delay.** Npgsql applies exponential backoff between attempts up to that cap, so this covers a brief network blip or a connection-pool-saturation spike within a few seconds, without holding an HTTP request open so long that a caller gives up and retries on their own, compounding the load.

**One kind of failure this should retry:** the connection pool being briefly saturated under a burst of concurrent requests — transient, and very likely to succeed on the very next attempt with no change in the request itself.

**One kind it deliberately shouldn't:** a unique constraint violation — for example, two attempts to add the same `StokvelMember` row, which `IX_StokvelMembers_StokvelId_UserId` rejects. This will fail identically every time it's retried; retrying it only adds latency before the same `409` reaches the caller.

### Repository swapped

I swapped **`IStokvelStore`** first — it backs `StokvelMembershipService` and `RecordContributionService`, the two files Assignment 4.4's unit tests exercise hardest (the membership rule, the duplicate-contribution rule, the idempotency-key comparison). It was the strongest available test of whether the repository interface actually held.

Modeling Payout correctly then **forced a second swap: `IContributionCycleStore`**. Payout's explicit transaction has to protect two writes — a new `Payout` row and a `ContributionCycle.Status` update — and that guarantee is meaningless if one of those writes lands in an in-memory list instead of the same PostgreSQL transaction as the other. A transaction spanning a real database write and an in-memory write isn't a transaction at all. This wasn't planned as "at least one" repository; it became two because Payout genuinely couldn't be built correctly with only one.

`IContributionStore` and `IIdempotencyStore` remain in-memory. This is a stated decision, not an oversight: today's scope was the repository with the most business logic, plus whatever Payout's transaction required — nothing more.

The `DbContext` is registered `AddScoped`, in place of the `AddSingleton` every in-memory store used. `DbContext` is a unit of work with its own change tracker; it isn't thread-safe. A Singleton `DbContext` would mean every concurrent request shares one tracker, corrupting each other's in-flight changes and eventually deadlocking under real traffic — exactly the inverse of the in-memory stores' reasoning, where the collection *was* the database and had to outlive any single request. This isn't theoretical: .NET's own service-provider validation refused to even start the app the first time the lifetime mismatch existed, with the error naming the exact conflict — *"Cannot consume scoped service 'RondiTrackDbContext' from singleton 'IStokvelStore'."*

### Before/after test run

**Before the swap (Assignment 4.4, in-memory):** 101 passed, 0 failed.

**After the swap, against real PostgreSQL:** 104 passed, 0 failed — 101 original tests plus 3 new Payout tests, confirmed running against the real database by direct SQL logging (`INSERT INTO "Stokvels"`, `INSERT INTO "StokvelMembers"`, `UPDATE "ContributionCycles" ... ; INSERT INTO "Payouts" ...` appearing in `dotnet test` output), not assumed from a green checkmark.

**One test genuinely broke during the swap**, and it's a real finding, not noise: `Creating_a_cycle_returns_201_with_a_location_and_the_cycle_can_then_be_fetched` compared two whole `ContributionCycleResponse` records with `Assert.Equal`. `TargetAmount` is `decimal`, stored as `numeric(18,2)` in PostgreSQL — a value round-tripped through the database always carries two decimal places (`1000.00`), while the in-memory object right after construction does not (`1000`). The two are numerically equal (`1000m == 1000.00m` is `true`), but C# record equality uses `decimal.Equals`, which considers scale, so the whole-record comparison failed on a representation detail introduced purely by persistence — not a behavior change the API makes any promise about. I changed the test to compare `TargetAmount` with `==` instead of relying on whole-record equality, and left every other field comparison as-is.

```
dotnet test --logger "console;verbosity=detailed" — final run:

Passed!  - Failed: 0, Passed: 104, Skipped: 0, Total: 104 - RondiTrack.Tests.dll (net10.0)


### Payout

Payout did not exist as a working feature before today — only as an entity name on the schema. The rotation rule I designed is deliberately minimal: **the next recipient is the member of the stokvel who joined earliest (`StokvelMember.JoinedAt` ascending) and has not yet received a payout from this stokvel.** First-come-first-served, no skipping, no scheduling, no partial payouts — exactly the scope the assignment asked for, nothing more.

**What the transaction protects:** processing a payout is two writes that must succeed or fail together — inserting the new `Payout` row, and marking the `ContributionCycle`'s `Status` as `"PaidOut"`. If only one of these landed (a payout recorded against a cycle still marked `"Open"`, or a cycle marked paid out with no corresponding `Payout` row), the stokvel's payout history would silently disagree with itself. Both writes run inside one explicit `IDbContextTransaction`, itself wrapped in `CreateExecutionStrategy().ExecuteAsync(...)` so it composes correctly with `EnableRetryOnFailure` (a retried attempt can't open a second transaction on top of one already in progress).

**Rollback test result:** the test forces a real failure — a stokvel with one member who has already been paid once, then a second payout attempt against a new cycle, where the rotation rule finds no eligible recipient and throws `BusinessRuleViolationException` after the transaction has begun but before either write happens. The test then re-queries the database directly, not the exception and not a status code, and confirms: `Payouts` has zero rows for the second cycle, and the second cycle's `Status` is still `"Open"`. Nothing partial was left behind. (Log excerpt showing the committed case for comparison — note the `UPDATE` and `INSERT` running as one batched statement on success:
```
UPDATE "ContributionCycles" SET "Status" = @p0 WHERE "Id" = @p1;
INSERT INTO "Payouts" ("Id", "Amount", "ContributionCycleId", "ProcessedAt", "RecipientUserId", "StokvelId")
VALUES (@p2, @p3, @p4, @p5, @p6, @p7);
```
)

**One deliberate gap on the Payout endpoint:** "cycle already paid out" and "every member already paid" both currently return `400` (via `BusinessRuleViolationException`/`ConflictException` routed through the existing handler), rather than `409`. I chose to leave this as-is rather than add a dedicated exception type purely to change a status code on a brand-new, intentionally minimal endpoint — it's a one-line fix if a future assignment calls for stricter alignment with the rest of the API's conflict-handling pattern, but wasn't worth the scope creep today.

### Definition of Done, extended

Two columns added to the Assignment 4.4 table: **Persisted via EF Core** and **Explicit transaction tested**.

| # | Endpoint | Documented | Validated | Unit-tested | Integration-tested | Status codes reviewed | Persisted via EF Core | Explicit transaction tested |
|---|---|---|---|---|---|---|---|---|
| 1 | `GET /api/users` | yes | n/a | no | yes | yes | yes | N/A |
| 2 | `GET /api/users/{id}` | yes | yes | no | yes | yes | yes | N/A |
| 3 | `POST /api/users` | yes | yes | no | yes | yes | yes | N/A |
| 4 | `PUT /api/users/{id}` | yes | yes | no | yes | yes | yes | N/A |
| 5 | `DELETE /api/users/{id}` | yes | yes | no | yes | yes | yes | N/A |
| 6 | `GET /api/stokvels` | yes | n/a | no | yes | yes | yes | N/A |
| 7 | `GET /api/stokvels/{id}` | yes | yes | no | yes | yes | yes | N/A |
| 8 | `POST /api/stokvels` | yes | yes | yes | yes | yes | yes | N/A |
| 9 | `PUT /api/stokvels/{id}` | yes | yes | partly | yes | yes | yes | N/A |
| 10 | `DELETE /api/stokvels/{id}` | yes | yes | no | yes | yes | yes | N/A |
| 11 | `POST /api/stokvels/{id}/members` | yes | yes | yes | yes | yes | yes | N/A |
| 12 | `POST /api/stokvels/{id}/contributions` | yes | yes | yes | yes | yes | **no** | N/A |
| 13 | `GET /api/stokvels/{id}/cycles` | yes | yes | no | yes | yes | yes | N/A |
| 14 | `GET /api/stokvels/{id}/cycles/{cycleId}` | yes | yes | no | yes | yes | yes | N/A |
| 15 | `POST /api/stokvels/{id}/cycles` | yes | yes | partly | yes | yes | yes | N/A |
| 16 | `PUT /api/stokvels/{id}/cycles/{cycleId}` | yes | yes | no | yes | yes | yes | N/A |
| 17 | `DELETE /api/stokvels/{id}/cycles/{cycleId}` | yes | yes | no | yes | yes | yes | N/A |
| 18 | `POST /api/stokvels/{id}/cycles/{cycleId}/payout` | yes | n/a | no | yes | yes | yes | **yes** |

Row 12 is the one honest "no" that matters: contributions still go through `IContributionStore`, which remains in-memory. Every contribution recorded through the live API today does not survive a restart, even though the stokvel, user, membership, and cycle it refers to now do.

### Gaps surfaced and not closed

- **Contributions and idempotency keys are still in-memory** (`IContributionStore`, `IIdempotencyStore`). A restart loses every recorded contribution and every idempotency record, even though the stokvel/cycle/membership data it refers to now persists. Scoped to today's "swap the repository with the most business logic, plus whatever Payout needs" — not an oversight.
- **Payout's "already paid out" / "everyone paid" conflicts return `400`, not `409`** — documented above, a one-line fix deferred rather than scope-crept into today.
- **`PUT` on a cycle can still create a duplicate period** (carried over from 4.4, unchanged — `POST` checks, `PUT` doesn't).
- **The idempotency key is still compared against the request body only, not the stokvel id in the URL** (carried over from 4.4, unchanged).
- **No optimistic concurrency control yet.** Two simultaneous payout requests for the same cycle both read `Status = "Open"` before either commits; PostgreSQL's row-level locking inside the transaction prevents real corruption, but there's no `xmin`-based concurrency token yet — that's named explicitly as a later Week 5 topic in the assignment brief itself, so it's deferred on purpose, not missed.

## Assignment 5.2 — Relationships & Query Behavior

This assignment confronted a shortcut the schema had been taking since Assignment 5.1: every relationship in RondiTrack was a bare `Guid` that happened to match another table's primary key, with EF Core never told any of it meant anything. Today gave every real relationship — membership, cycle ownership, contribution-to-member — an actual, enforced shape, and separately measured and fixed a real N+1 query rather than guessing at one.

### Why StokvelMember needed a composite key, not a surrogate one

Membership isn't a bare link between a `User` and a `Stokvel` — it carries data of its own: `Role` and `JoinedAtUtc`. That's what makes it a real entity rather than an implicit many-to-many EF Core could build silently. Its natural identity is the pair `(UserId, StokvelId)`: a user can only belong to a given stokvel once, and that fact *is* the primary key, not something enforced separately on top of a surrogate `Guid Id` nobody would ever read from. A surrogate key here would have let the same user/stokvel pair exist twice in the table with two different synthetic ids — exactly the bug the natural key rules out at the database level, for free.

**The FK question this raised:** `Contribution` and `Payout` both already stored the user and stokvel involved as two separate columns (`UserId`/`StokvelId` on `Contribution`, `RecipientUserId`/`StokvelId` on `Payout`) — which is exactly the shape of `StokvelMember`'s new composite key. Rather than add a new surrogate reference column to either entity, I configured a real, database-enforced composite foreign key on the columns that already existed:

```csharp
b.HasOne(c => c.Member)
    .WithMany()
    .HasForeignKey(c => new { c.UserId, c.StokvelId })
    .HasPrincipalKey(m => new { m.UserId, m.StokvelId });
```

I considered giving `StokvelMember` a surrogate `Guid` back specifically so other entities would have something simpler to reference, but that would have undone the whole point of the exercise — it would mean the "real" identity (the pair) and the "referenced" identity (a surrogate) were two different things, which is exactly the kind of quiet inconsistency the assignment warns against.

### What replaced generic repository access for StokvelMember

RondiTrack never used a shared `IRepository<T>` pattern — each entity has its own dedicated store interface (`IStokvelStore`, `IContributionCycleStore`, etc.), so the specific risk the assignment describes (a generic `GetByIdAsync(Guid id)` breaking outright) never literally applied here. But the underlying principle did apply: before today, `StokvelMember` had no lookup method at all, only an `AddMembershipAsync`. The method added today is explicitly composite-keyed from the start:

```csharp
Task<StokvelMember?> GetMembershipAsync(Guid stokvelId, Guid userId);
```

never a bare single-Guid lookup that a later key change would have broken. `AddMembershipAsync` also gained a `role` parameter (defaulting to `"Member"`) so a membership's role is set at creation, matching the new column.

### What I specifically checked reading this ALTER migration

Unlike Assignment 5.1's `CREATE TABLE` migration, this one altered an existing table that already held real data from testing, so the checks were different:

- **Confirmed it was a genuine `ALTER` sequence, not a drop-and-recreate.** The migration ran `DropPrimaryKey` → `DropIndex` → `DropColumn("Id")` → `RenameColumn("JoinedAt" → "JoinedAtUtc")` → `AddColumn("Role")` → `AddPrimaryKey` on the existing `StokvelMembers` table. A `DropTable`/`CreateTable` pair would have destroyed every existing row; this preserved them.
- **Checked the new `Role` column's default value specifically**, because a `NOT NULL` column added to a table with existing rows needs a real default or the migration fails outright. The first generated migration used `defaultValue: ""` — technically valid SQL, but a silent data-quality problem, since every pre-existing row would get an empty string instead of a meaningful value. I added `.HasDefaultValue("Member")` to the property configuration and regenerated so existing rows got `Role = "Member"` instead.
- **EF Core itself flagged the migration with "An operation was scaffolded that may result in the loss of data."** I read the migration specifically to find out why rather than clicking past it: the warning was about dropping `StokvelMembers.Id`, a synthetic surrogate key nobody read from anywhere in the application — a false alarm for this specific case, but only confirmed as one by actually reading the migration, not by assuming.
- **Confirmed `ReferentialAction.Cascade` was applied to all five new/changed foreign keys** (EF's convention default). This is a real behavior change worth naming: deleting a `Stokvel` now cascades to delete its `StokvelMembers`, `Contributions`, and `Payouts`, where before they were simply left orphaned/unreachable. I kept the convention default rather than overriding it, since cascading deletes match the domain's actual meaning (a contribution can't outlive the stokvel it belongs to), but it's a decision, not an accident.
- **Applying the migration genuinely failed once**, with a real `23503` foreign key violation on `FK_ContributionCycles_Stokvels_StokvelId` — three `ContributionCycles` rows referenced a `StokvelId` that no longer existed in `Stokvels`, orphaned from earlier manual testing before this FK existed to prevent it. This is exactly the kind of problem an `ALTER` can surface that a `CREATE` never would: the schema change itself was correct, but the data underneath it wasn't. I identified the three orphaned rows with a `LEFT JOIN ... WHERE ... IS NULL` query, deleted them (and checked for any `Contributions`/`Payouts` referencing them first — there were none), and reapplied the migration, which then completed successfully from where it had stopped.

### The second relationship wired, and the alternative

I wired `Stokvel` ↔ `ContributionCycle` as a real navigation on both sides (`Stokvel.ContributionCycles`, `ContributionCycle.Stokvel`), backed by a proper FK with cascade delete. I considered `ContributionCycle` ↔ `Contribution` instead, but `Contribution`'s configuration was already being touched by the composite-FK work for the `StokvelMember` relationship in the same pass — wiring a second relationship onto the same entity at the same time would have made that part of the migration harder to review cleanly. `ContributionCycle` ↔ `Contribution` remains a fair, equally real one-to-many left unwired today — an honest "not yet," not an oversight.

### N+1: measured, not estimated

Built `GET /api/stokvels/{stokvelId}/cycles/{cycleId}/contributions-naive` (a temporary measurement endpoint, since removed) that loaded contribution rows with no eager loading, then issued one hand-written per-row query for each contribution's member and user — "naive" meant hand-written, since lazy-loading proxies aren't permitted. Seeded a stokvel with 9 real members and 9 real contributions against one cycle, turned on `"Microsoft.EntityFrameworkCore.Database.Command": "Information"` logging, and counted actual `Executed DbCommand` lines in the console output.

**Measured results, for 9 contributions in one cycle:**

| Version | Queries | What it fetches |
|---|---|---|
| Naive (hand-written, 1 query per row) | **10** | 1 for contributions, then 9 separate `StokvelMembers` + `Users` joins, one per row |
| Eager (`.Include().ThenInclude()`) | **2** | 1 for contributions, 1 for all matching `StokvelMembers` + `Users` in a single batch — but every column of all three entities |
| Projection (`.Select()`, shipped) | **1** | 1 query, 3 columns total (`Id`, `Amount`, `MemberName`) — no entity graph materialized at all |

**Shipped the projection.** The eager version still pulls every column of `Contribution`, `StokvelMember`, and `User` for every row returned, even though the endpoint only ever shows three fields. The gap between what's fetched and what's used only grows as a stokvel's member count grows — for a cycle with fifty members instead of nine, the eager version's per-row column cost scales with the full width of three tables, while the projection's cost stays fixed to exactly the three fields the response needs, regardless of how many members the stokvel has.

### Loading-strategy decisions, named explicitly

- **`GET .../contributions`** (the shipped endpoint): **explicit, targeted loading via projection** — deliberately not `.Include()`, for the column-cost reason above.
- **`EfStokvelStore.GetStokvelByIdAsync` / `GetStokvelByIdReadOnlyAsync`** (via `HydrateMembersAsync`): **explicit loading via a separate, targeted query**, not `.Include()`, since membership is a collection only the stokvel-retrieval methods ever need — a dedicated query avoids pulling it anywhere else it isn't used.
- **Lazy loading appears nowhere.** It would mean EF silently issuing a query the moment any navigation property is touched, anywhere in the codebase, with no visible trace at the call site — exactly the hidden cost this whole assignment exists to make visible instead of hiding further.

### AsNoTracking(): where a read/write split was needed

Audited every `GetById`-style method shared between a pure `GET` action and an `Update`/`Delete` action that loads, mutates (or removes), then saves. Found the problem on three entities: `GetStokvelByIdAsync`, `GetUserByIdAsync`, and `IContributionCycleStore.GetByIdAsync` were each called by both a `GET` action and an `Update` action — blindly adding `AsNoTracking()` to any of them would have silently broken every `PUT` on that entity, since EF can't persist changes through a detached instance.

**Resolution: two separate methods per entity**, not a boolean flag, so the intent is visible at every call site rather than hidden in an argument:

```csharp
Task<Stokvel?> GetStokvelByIdAsync(Guid id);           // tracked — Update still uses this
Task<Stokvel?> GetStokvelByIdReadOnlyAsync(Guid id);    // AsNoTracking — GetById uses this
```

Applied the same split to `GetUserByIdAsync`/`GetUserByIdReadOnlyAsync` and `GetByIdAsync`/`GetByIdReadOnlyAsync` on `IContributionCycleStore`. For `ContributionCycle`, `Delete` was a worthwhile edge case to think through separately from `Update`: it loads the cycle only to check it exists and belongs to the right stokvel, never calls a mutation method on it, and the actual removal happens through a separate `DeleteAsync` call with its own tracked lookup — so `Delete`'s initial existence check was safe to point at the read-only method too, even though it isn't a pure `GET` action.

### Test suite: before/after, and what broke

**Before today's changes:** 104 passed, 0 failed (Assignment 5.1's final state).

**After, on the first full re-run:** 102 passed, 2 failed. Both failures were genuine and worth explaining, not estimating:

1. **`Creating_a_cycle_returns_201_with_a_location_and_the_cycle_can_then_be_fetched`** — failed comparing `CreatedAt` exactly between a freshly-created object and the same row re-fetched from PostgreSQL (`...4864383Z` vs `...4864380Z`, a 3-tick difference). This was a pre-existing fragility in the test unrelated to today's relationship work — PostgreSQL's timestamp round-trip doesn't preserve .NET's full sub-microsecond precision. Fixed by comparing with a millisecond tolerance instead of exact equality, the same pattern used for the decimal-scale issue found in Assignment 5.1.
2. **`Get_Stokvel_That_Does_Not_Exist_Returns_404_ProblemJson`** — failed with a `500` instead of `404`, and this one was a real regression caused directly by today's `AsNoTracking()` split: during the edit that introduced `GetStokvelByIdReadOnlyAsync`, the `if (stokvel is null) throw new NotFoundException(...)` check that must follow the lookup was lost, so a `null` result was passed straight into `StokvelMapper.ToResponse`, throwing a `NullReferenceException` that the global exception handler correctly reported as a `500`. This is exactly the kind of bug the read/write split is supposed to surface, not hide — found by re-running the suite immediately after the change rather than assuming it was safe. Fixed by restoring the null check.

**After both fixes:** 104 passed, 0 failed — back to the full baseline, with the same count as before today, which makes sense: today added relationship and query-behavior correctness, not new endpoints or new tests of its own beyond what the N+1 work needed (and that lived in a temporary measurement endpoint since removed).

I also specifically checked every existing test file for any direct reference to `StokvelMember.Id` — the thing that would have broken outright if the composite-key change had been incomplete. None exist: no test ever constructed a `StokvelMember` directly or asserted on its id; every test that touches membership does so through `Stokvel.MemberIds` (the domain-level view, unchanged) or through the HTTP API's `memberCount`/`201`/`409` responses, none of which reference the entity's internal key shape.

### Definition of Done, extended again

Two columns added: **Relationship modeled as navigation** and **N+1 measured and fixed**.

| # | Endpoint | Documented | Validated | Unit-tested | Integration-tested | Status codes reviewed | Persisted via EF Core | Explicit transaction tested | Relationship modeled as navigation | N+1 measured and fixed |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | `GET /api/users` | yes | n/a | no | yes | yes | yes | N/A | N/A | N/A |
| 2 | `GET /api/users/{id}` | yes | yes | no | yes | yes | yes | N/A | N/A | N/A |
| 3 | `POST /api/users` | yes | yes | no | yes | yes | yes | N/A | N/A | N/A |
| 4 | `PUT /api/users/{id}` | yes | yes | no | yes | yes | yes | N/A | N/A | N/A |
| 5 | `DELETE /api/users/{id}` | yes | yes | no | yes | yes | yes | N/A | N/A | N/A |
| 6 | `GET /api/stokvels` | yes | n/a | no | yes | yes | yes | N/A | N/A | N/A |
| 7 | `GET /api/stokvels/{id}` | yes | yes | no | yes | yes | yes | N/A | N/A | N/A |
| 8 | `POST /api/stokvels` | yes | yes | yes | yes | yes | yes | N/A | N/A | N/A |
| 9 | `PUT /api/stokvels/{id}` | yes | yes | partly | yes | yes | yes | N/A | N/A | N/A |
| 10 | `DELETE /api/stokvels/{id}` | yes | yes | no | yes | yes | yes | N/A | **yes** (cascades to StokvelMembers/Contributions/Payouts) | N/A |
| 11 | `POST /api/stokvels/{id}/members` | yes | yes | yes | yes | yes | yes | N/A | **yes** (StokvelMember composite key + navigations) | N/A |
| 12 | `POST /api/stokvels/{id}/contributions` | yes | yes | yes | yes | yes | **no** | N/A | **yes** (composite FK to StokvelMember) | N/A |
| 13 | `GET /api/stokvels/{id}/cycles` | yes | yes | no | yes | yes | yes | N/A | **yes** (Stokvel↔ContributionCycle) | N/A |
| 14 | `GET /api/stokvels/{id}/cycles/{cycleId}` | yes | yes | no | yes | yes | yes | N/A | yes | N/A |
| 15 | `POST /api/stokvels/{id}/cycles` | yes | yes | partly | yes | yes | yes | N/A | yes | N/A |
| 16 | `PUT /api/stokvels/{id}/cycles/{cycleId}` | yes | yes | no | yes | yes | yes | N/A | yes | N/A |
| 17 | `DELETE /api/stokvels/{id}/cycles/{cycleId}` | yes | yes | no | yes | yes | yes | N/A | yes | N/A |
| 18 | `POST /api/stokvels/{id}/cycles/{cycleId}/payout` | yes | n/a | no | yes | yes | yes | yes | **yes** (Payout composite FK to StokvelMember) | N/A |
| 19 | `GET /api/stokvels/{id}/cycles/{cycleId}/contributions` | partly | n/a | no | no | yes | yes | N/A | yes | **yes — 10 → 2 → 1, projection shipped** |

Row 19 has no dedicated integration test yet and no `EndpointSummary` block to the same depth as the others — a real gap, listed below rather than hidden.

### Gaps surfaced and not closed

- **`GET .../contributions` (the new endpoint) has no automated integration test.** It was verified manually against real seeded/measured data (the exact scenario used for the N+1 measurement), but no `ContributionsQueryApiTests.cs`-style test asserts on its `200`/empty-array/row-shape behavior the way every other endpoint's happy path is covered. A real gap, not an oversight I'm hiding — scoped out today to keep focus on the relationship and query-behavior work itself.
- **Payout's "already paid out" / "everyone paid" conflicts still return `400`, not `409`** (carried over from Assignment 5.1, unchanged today).
- **`PUT` on a cycle can still create a duplicate period** (carried over from Assignment 4.4, unchanged).
- **The idempotency key is still compared against the request body only, not the stokvel id in the URL** (carried over from Assignment 4.4, unchanged).
- **Contributions and idempotency keys are still in-memory** (carried over from Assignment 5.1, unchanged — `IContributionStore` and `IIdempotencyStore` were never swapped to EF Core).
- **No optimistic concurrency control.** Named explicitly in Assignment 5.1's gaps and still true today; the course names this as a dedicated later topic (`xmin`-based concurrency), so it's deferred on purpose.

# RondiTrack — Stokvel Tracker

RondiTrack is an ASP.NET Core Web API for managing stokvels, members, contribution cycles, contributions, and payouts.

The project uses:

- ASP.NET Core
- Entity Framework Core
- PostgreSQL
- Npgsql
- xUnit integration tests
- Scalar for API testing

---

# Assignment 5.3 — Optimizations, Concurrency & Database Defense

## 1. Assignment Overview

Assignment 5.3 focused on making RondiTrack safer and more efficient when the application contains a large amount of data and when multiple users attempt to modify data at the same time.

The assignment required improvements in three main areas:

1. **Database performance**
2. **Database integrity**
3. **Optimistic concurrency**

The main goal was to ensure that important work happens in PostgreSQL rather than unnecessarily inside the C# application, while also making PostgreSQL the final authority for rules that must always remain true.

---

# 2. What Was Required

The assignment required the following:

- Audit list endpoints and repositories for filtering, sorting, or paging happening after data had already been loaded into memory.
- Move filtering, sorting, and paging into the database.
- Introduce a pagination contract.
- Paginate the contributions endpoint.
- Paginate at least one additional list endpoint.
- Support a sensible default page size.
- Apply a server-side maximum page size.
- Reject negative page sizes with HTTP 400.
- Use an opaque page token.
- Use deterministic ordering with a unique tie-breaker.
- Explicitly allow only supported sorting/filtering fields.
- Return `ProblemDetails` for malformed or unknown sorting/filtering requests.
- Define behaviour for page-token reuse with different filter/sort settings.
- Decide whether total counts are returned.
- Choose and justify offset or keyset pagination.
- Capture the generated SQL.
- Seed at least 10,000 contributions.
- Run `EXPLAIN ANALYZE` before and after adding a composite index.
- Explain the query plan and performance difference.
- Enforce at least two existing multi-column uniqueness rules in PostgreSQL.
- Check for existing duplicate data before adding uniqueness constraints.
- Inspect migrations for potential locking/cost problems.
- Centrally translate PostgreSQL SQLSTATE `23505` into HTTP 409 Conflict.
- Use PostgreSQL `xmin` for optimistic concurrency.
- Protect entities that can be edited after creation.
- Round-trip the concurrency token through HTTP.
- Reject stale updates with HTTP 409 or 412.
- Create deterministic concurrency tests using two independent `DbContext` instances.
- Create an HTTP-level stale-token concurrency test.
- Rerun the complete test suite.
- Document the work and decisions.

---

# 3. Database-Side Filtering, Sorting and Paging

## Why this was necessary

A list endpoint can become inefficient if the application loads a large number of records into C# and only then filters, sorts, or pages them.

For example, an inefficient approach would effectively do this:

```text
PostgreSQL
    ↓
Return thousands of rows
    ↓
C# loads all rows into memory
    ↓
C# filters them
    ↓
C# sorts them
    ↓
C# takes the first 20

---

# Assignment 5.4 — Clean Architecture & Self-Contained Tests

## 1. Assignment Overview

Assignment 5.4 focused on restructuring RondiTrack into a cleaner architecture and making the integration test suite independent of the developer's local PostgreSQL database.

The assignment had three main goals:

1. Split the application into Domain, Infrastructure, and API projects with a clear dependency direction.
2. Audit dependency-injection lifetimes so that services using Entity Framework Core follow the correct `DbContext` lifetime.
3. Move integration tests to an isolated PostgreSQL database using Testcontainers so the complete test suite can run without the development database.

No new API features or endpoints were added as part of this refactor. Existing behaviour and test assertions were preserved.

---

## 2. Final Project Structure

The solution is now split into four projects:

```text
RondiTrack.slnx
│
├── RondiTrack.Domain
│   ├── Models
│   │   ├── Contribution.cs
│   │   ├── ContributionCycle.cs
│   │   ├── Payout.cs
│   │   ├── StokvelMember.cs
│   │   ├── Stokvels.cs
│   │   └── Users.cs
│   │
│   ├── Exceptions
│   │   ├── BusinessRuleViolationException.cs
│   │   ├── ConflictException.cs
│   │   ├── IdempotencyConflictException.cs
│   │   ├── NotFoundException.cs
│   │   └── RondiTrackException.cs
│   │
│   └── Data
│       ├── IContributionCycleStore.cs
│       ├── IContributionStore.cs
│       ├── IIdempotencyStore.cs
│       ├── IStokvelStore.cs
│       └── IdempotencyRecord.cs
│
├── RondiTrack.Infrastructure
│   ├── Data
│   │   ├── RondiTrackDbContext.cs
│   │   ├── EfContributionCycleStore.cs
│   │   ├── EfStokvelStore.cs
│   │   └── VolumeSeeder.cs
│   │
│   └── Migrations
│       └── EF Core migrations
│
├── RondiTrack
│   ├── Controllers
│   ├── Models
│   ├── Services
│   ├── Mapping
│   ├── Validation
│   ├── Helpers
│   └── Program.cs
│
└── RondiTrack.Tests
    ├── API/integration tests
    └── TestSupport
        └── PostgresApiFactory.cs



# Assignment 6.1 — Authentication & Stokvel-Scoped Authorization

## 1. Overview

Assignment 6.1 focuses on securing the RondiTrack API by introducing authentication and stokvel-specific authorization.

Previously, the application allowed users to access stokvel information without a complete authentication and authorization system. This created a security risk because stokvels manage financial information, including contributions and payouts.

The objective of this assignment is to ensure that:

- Users can register and log in securely.
- Passwords are stored as hashes rather than plain text.
- Authenticated users receive short-lived JWT access tokens.
- Refresh tokens allow users to obtain new access tokens without logging in repeatedly.
- Refresh tokens can only be used once and can be revoked.
- API endpoints are protected by default.
- Users can access only the stokvels they belong to.
- Permissions depend on the user's role within each individual stokvel.
- Security rules are enforced using ASP.NET Core authorization policies.
- Integration tests verify that the security mechanisms work correctly.

The main principle is that **authentication establishes who a user is, while authorization determines what that user is allowed to do.**

---

## 2. User Registration and Password Security

### Registration endpoint

`POST /api/auth/register`

Users can create an account by providing their registration information, including an email address and password.

The existing `User` entity was extended to support authentication rather than creating a separate user table.

### Password hashing

RondiTrack uses ASP.NET Core's `PasswordHasher<User>` to hash passwords before storing them in PostgreSQL.

A password hash is a protected representation of a password. The application does not need to store the original password to verify it during login.

For example:

- User enters a password during registration.
- The application hashes the password.
- The hash is stored in the database.
- During login, the entered password is checked against the stored hash.

### Registration validation

The registration requirements include:

- Passwords must contain at least 12 characters.
- FluentValidation validates registration requests.
- Invalid registration requests return HTTP 400.
- Duplicate email addresses return HTTP 409.
- Email addresses are normalized to prevent case-based duplicates.
- A unique database index protects against duplicate normalized emails.

This means that `Member@Example.com` and `member@example.com` must not be registered as separate accounts.

---

## 3. JWT Authentication

### Login endpoint

`POST /api/auth/login`

After successful login, the application issues a JWT access token.

JWT stands for JSON Web Token. It allows the API to identify an authenticated user without requiring the user to submit their password with every request.

The frontend sends the access token in the authorization header:

```http
Authorization: Bearer <access-token>
```

### Access-token lifetime

Assignment 6.1 requires access tokens to expire within 15 minutes.

**Implementation value to verify:** `[Enter the configured access-token lifetime]`

Short-lived tokens reduce the amount of time a stolen access token can be used.

### ClockSkew

ClockSkew controls how much additional time the JWT validation system allows for differences between server clocks.

**Implementation value to verify:** `[Enter the configured ClockSkew]`

A small ClockSkew makes token expiration more predictable. A zero ClockSkew enforces the expiration time without an additional grace period, but assumes the clocks involved are synchronized.

### JWT claims

JWT claims contain information about the authenticated user.

RondiTrack should include only the identity information necessary to identify the caller.

**Actual claims to verify:** `[List the claims issued by JwtTokenService.cs]`

Stable identity claims can safely remain in the token until it expires. Stokvel-specific roles should not be included because membership and role assignments may change while a token is still valid.

---

## 4. Refresh Tokens

### Refresh endpoint

`POST /api/auth/refresh`

Refresh tokens allow users to obtain new access tokens without entering their credentials every few minutes.

Unlike access tokens, refresh tokens are random opaque values rather than JWTs.

### Refresh-token storage

RondiTrack uses PostgreSQL to store refresh-token records.

The raw refresh-token value must not be stored in the database. Instead, the application stores its hash.

This reduces the risk of an attacker using refresh tokens directly if database records are exposed.

### Refresh-token rotation

Each refresh token is intended to work only once.

The rotation process is:

1. The user logs in and receives a refresh token.
2. The refresh token is stored in a secure cookie.
3. The access token expires.
4. The frontend calls the refresh endpoint.
5. The server validates the refresh token.
6. The existing refresh token is invalidated.
7. A new refresh token is issued.
8. The new token replaces the previous cookie.

This process is called **refresh-token rotation**.

### Refresh-token reuse detection

If someone tries to use an already-used refresh token, the server treats this as a possible token theft or replay attack.

The required behavior is to revoke the refresh-token family associated with that login session.

For example:

- Token A is used and replaced with Token B.
- Someone attempts to use Token A again.
- The server detects that Token A has already been used.
- Token A is rejected with HTTP 401.
- Token B and any other descendants from the same session are also revoked.

The legitimate user must log in again after the session is revoked.

This provides additional protection if an old refresh token has been stolen.

### Logout

`POST /api/auth/logout`

Logout revokes the session's refresh tokens so that they can no longer be used to obtain new access tokens.

Logout is intended to be idempotent, meaning that logging out repeatedly should not cause an unexpected error.

---

## 5. Refresh-Token Cookie Security

The refresh token is delivered through an HTTP cookie rather than being returned in a JSON response body.

The assignment requires the cookie settings to be explained.

| Cookie setting | Purpose |
|---|---|
| `HttpOnly` | Prevents JavaScript from directly reading the refresh token |
| `Secure` | Restricts cookie transmission to HTTPS |
| `SameSite` | Helps control cross-site cookie requests and reduce CSRF risk |
| `Path` | Restricts which URL paths receive the cookie |

**Actual cookie configuration to verify:**

- HttpOnly: `[Actual value]`
- Secure: `[Actual value]`
- SameSite: `[Actual value]`
- Path: `[Actual value]`

### Recommended frontend token storage

The access token should preferably be stored in frontend application memory rather than localStorage.

The refresh token should remain in an HttpOnly cookie managed by the browser.

Storing tokens in localStorage creates additional risk because JavaScript running in the page can read them. A successful cross-site scripting attack could therefore expose those tokens.

HttpOnly cookies prevent direct JavaScript access to the refresh token, although they do not eliminate every web security risk. Cookie-based authentication must still consider CSRF protection and secure transport.

---

## 6. Secure by Default

RondiTrack uses ASP.NET Core authentication and authorization middleware.

The assignment requires a fallback authorization policy so that endpoints require authentication unless explicitly marked as public.

For example:

```csharp
[AllowAnonymous]
```

This attribute allows an endpoint to be accessed without authentication.

Public endpoints include registration and login. The OpenAPI and Scalar documentation endpoints must also remain accessible without an access token.

### HTTP 401 — Unauthorized

A 401 response means the caller has not successfully authenticated.

Examples include:

- No access token was supplied.
- An access token is invalid.
- An access token has expired.

### HTTP 403 — Forbidden

A 403 response means the caller is authenticated but does not have the permission required for the requested operation.

Authentication and authorization failures must use the API's consistent `application/problem+json` response format.

---

## 7. Stokvel-Scoped Authorization

This is the main security requirement of Assignment 6.1.

In RondiTrack, a user can belong to multiple stokvels and have a different role in each one.

For example:

A user might be:

- Chairperson of Stokvel A.
- Treasurer of Stokvel B.
- Ordinary Member of Stokvel C.

Therefore, the user's role cannot be treated as one global permission.

### Database membership checks

Authorization uses the `StokvelMember` records stored in PostgreSQL.

The application checks whether the authenticated user belongs to the requested stokvel and whether their role permits the operation.

The authorization implementation is separated into components such as:

- `IStokvelAccessService.cs`
- `StokvelAccessService.cs`
- `StokvelAccessResource.cs`
- `StokvelRoleRequirement.cs`
- `StokvelRoleHandler.cs`

These components support membership lookups, authorization requirements, and resource-based permission decisions.

### Why stokvel roles are not stored in the JWT

A JWT represents information at the time it was issued.

Suppose a user logs in at 09:00 as a Treasurer. At 09:05, the Chairperson removes their Treasurer role.

If the role were stored only inside the JWT, the user might continue accessing Treasurer operations until the token expires.

Checking the database ensures that the application uses the user's current stokvel membership and role.

---

## 8. HTTP 404 vs HTTP 403

RondiTrack distinguishes between users who do not belong to a stokvel and users who belong but lack permission.

### Non-member — HTTP 404

A user who does not belong to a stokvel receives HTTP 404.

This prevents the API from revealing whether a private stokvel exists.

For example, a user should not be able to guess stokvel IDs and discover other people's financial groups.

### Member without permission — HTTP 403

A user who belongs to the stokvel but lacks the required role receives HTTP 403.

For example, an ordinary Member may be allowed to view contribution information but not process payouts.

Returning 403 is appropriate because the user is already authorized to know that the stokvel exists.

This distinction helps prevent unnecessary information leakage.

---

## 9. Stokvel Authorization Matrix

The following table represents the required authorization design. The exact behavior of each implemented endpoint must be verified against the controllers and integration tests.

| Operation | Non-member | Member | Treasurer / Chairperson | Platform admin |
|---|---|---|---|---|
| View stokvel | 404 | 200 | 200 | To verify |
| View contribution cycles | 404 | 200 | 200 | To verify |
| View contributions | 404 | 200 | 200 | To verify |
| Record own contribution | 404 | Success | Success | To verify |
| Record another member's contribution | 404 | 403 | Success | To verify |
| Open contribution cycle | 404 | 403 | Success | To verify |
| Process payout | 404 | 403 | Success | To verify |
| Delete stokvel | 404 | 403 | Chairperson only | To verify |
| Add stokvel member | 404 | 403 | Chairperson only | To verify |

Unauthenticated callers should receive HTTP 401 before stokvel-specific authorization is evaluated.

The platform-admin column depends on the application's chosen platform-level permission design.

---

## 10. Role-Based vs Policy-Based Authorization

ASP.NET Core supports multiple ways of restricting access.

### Role-based authorization

Role-based authorization checks whether the authenticated user has a particular role.

A typical example is:

```csharp
[Authorize(Roles = "Admin")]
```

This approach is useful for platform-wide roles that are not tied to an individual stokvel.

If the permitted role changes, the role restriction must be updated.

### Policy-based authorization

Policy-based authorization evaluates authorization requirements through registered policies and handlers.

In RondiTrack, this is more suitable for stokvel operations because the user's permission depends on the stokvel being accessed.

A custom authorization handler can check:

- The authenticated user's ID.
- The stokvel being requested.
- Whether a membership record exists.
- The role associated with that membership.
- Whether the operation requires Member, Treasurer, or Chairperson permissions.

If a stokvel permission rule changes, the relevant policy or authorization handler can be updated rather than scattering role checks throughout controllers.

### Why RondiTrack uses policies

Policy-based authorization provides a centralized and reusable way to enforce stokvel-specific rules.

It also separates security decisions from the controllers that handle API requests.

**Implementation check:** Identify the exact platform-level operation using a plain role check and document it here. Assignment 6.1 requires exactly one such use.

---

## 11. JWT Signing Key and Secrets

JWTs are signed using a secret key.

The assignment requires the signing key to:

- Be at least 32 bytes.
- Be stored in .NET user-secrets during development.
- Never appear in committed configuration files.
- Never be written to application logs.
- Cause startup to fail with a helpful error when missing.

The application should read the signing key from configuration and validate it during startup.

User-secrets allow development credentials to remain outside the Git repository.

No actual signing key should be included in this README.

---

## 12. Security Logging

Authentication-related logs must not expose passwords, access tokens, or refresh tokens.

The assignment requires an example of logs generated during login and refresh.

**Insert a sanitized excerpt from actual application logs here.**

Example format only — not a captured RondiTrack log:

```text
Information: Login request processed.
Information: Authentication successful.
Information: Refresh request processed.
Information: Refresh token rotated successfully.
```

The actual log excerpt must be reviewed to confirm that it contains no passwords, raw refresh tokens, JWT access tokens, or other sensitive credentials.

---

## 13. Testing

RondiTrack uses xUnit and Testcontainers with PostgreSQL for integration testing.

Testcontainers starts an isolated PostgreSQL database in Docker, allowing tests to run without depending on existing records in the developer's database.

### Latest confirmed test results

```text
Passed! - Failed: 0, Passed: 106, Skipped: 0, Total: 106

Test summary:
Total: 106
Failed: 0
Succeeded: 106
Skipped: 0

Build succeeded.
```

All 106 tests in the latest complete test run passed.

### Run the tests

From the repository root:

```powershell
dotnet test .\RondiTrack.Tests\RondiTrack.Tests.csproj --logger "console;verbosity=quiet"
```

### Authentication and authorization test requirements

Assignment 6.1 requires tests demonstrating:

- Missing authentication token returns HTTP 401 with ProblemDetails.
- A non-member receives HTTP 404.
- A member without the required role receives HTTP 403.
- A Treasurer can successfully perform a privileged operation.
- A Member can record their own contribution.
- A Member cannot record someone else's contribution.
- Refresh-token rotation invalidates the previous token.
- Replaying an old refresh token revokes the token family.
- Refreshing after logout returns HTTP 401.
- Incorrect passwords and unknown email addresses return identical problem details.

**Verification status:** The existing test suite passed 106/106 tests. Dedicated coverage of every authentication and authorization scenario listed above has not yet been confirmed.

The assignment also requires running the tests with the development PostgreSQL database stopped. That condition should be confirmed separately before submission.

### Existing test compatibility

Existing integration tests were adapted to authenticate using the test authentication handler and to arrange the appropriate stokvel memberships.

This allows the tests to focus on the behavior being tested without depending on real login credentials for every request.

The test fixture uses a Testcontainers PostgreSQL instance and applies EF Core migrations before running database-backed tests.

---

## 14. Database Changes

Assignment 6.1 introduces additional database support for authentication.

### User authentication fields

The existing User entity was extended to support password hashing and email-based authentication.

Migration:

```text
20261009113835_AddUserAuthenticationFields
```

### Refresh-token storage

Refresh-token records are stored in PostgreSQL to support session tracking, rotation, revocation, and reuse detection.

Migration:

```text
20261010184610_AddRefreshTokens
```

The Entity Framework Core model snapshot was also updated.

These migrations are committed as part of the assignment.

---

## 15. Known Gaps and Remaining Verification

The following items should be reviewed before final submission:

1. **Dedicated security test coverage:** Confirm that every required authentication, refresh-token, and authorization scenario has an integration test.
2. **Development database independence:** Confirm that the complete test suite passes while the development PostgreSQL database is stopped.
3. **First Chairperson assignment:** Newly created stokvels currently do not automatically assign their creator as Chairperson. A secure initial membership workflow still needs to be defined.
4. **Implementation documentation:** Confirm the actual JWT lifetime, ClockSkew, JWT claims, refresh-cookie settings, and platform-level role check.
5. **Security logs:** Capture and review actual login and refresh logs without exposing secrets.

These gaps are recorded explicitly rather than claiming that unverified requirements have been completed.

---

## 16. Conclusion

Assignment 6.1 introduces authentication and stokvel-specific authorization to RondiTrack.

The main improvements include password hashing, JWT authentication, PostgreSQL-backed refresh-token management, authorization policies, database-backed stokvel membership checks, and protection against unauthorized access to financial operations.

The implementation separates user identity from stokvel permissions so that one user can have different responsibilities in different stokvels.

The latest complete test run passed all 106 existing tests.

The remaining work is to verify the assignment's dedicated security scenarios and document the exact implementation choices before final submission.

# Assignment 6.2 — Observability & POPIA-Compliant Telemetry

## 1. Assignment Overview

Assignment 6.2 focuses on making RondiTrack easier to monitor, troubleshoot and operate safely.

Previously, the API could process requests and return errors, but it was difficult to follow an individual request through the application, identify database activity associated with that request, or measure important business operations.

The assignment introduces **observability**, which means understanding what an application is doing by examining traces, metrics and logs.

It also requires that observability does not compromise the privacy of RondiTrack users.

### Main objectives

- Introduce OpenTelemetry tracing, metrics and logging.
- Connect HTTP requests to database operations through distributed traces.
- Return consistent correlation IDs in API errors.
- Trace payout processing as a business operation.
- Count successful and failed business operations using metrics.
- Avoid logging sensitive personal and financial information.
- Introduce health endpoints suitable for deployment monitoring.
- Verify the behaviour through automated tests.

## 2. OpenTelemetry Configuration

OpenTelemetry is a standard way to collect information about an application's behaviour.

RondiTrack identifies itself using the service name:

`ronditrack-api`

This name helps distinguish RondiTrack telemetry from other services in an observability dashboard.

The configuration is registered in `Program.cs`.

### Traces

A trace describes the path taken by a request through the application.

For example, a request to record a contribution may involve:

1. Receiving the HTTP request.
2. Checking authentication and stokvel permissions.
3. Reading or writing data in PostgreSQL.
4. Returning an HTTP response.

Each operation can appear as a span within the same trace.

RondiTrack instruments incoming ASP.NET Core requests and Npgsql database activity.

Health endpoints are excluded from HTTP tracing to reduce unnecessary telemetry from repeated monitoring requests.

### Exporting telemetry

OpenTelemetry Protocol (OTLP) can be used to send telemetry to a compatible collector or dashboard.

The exporter is configured conditionally, based on whether an OTLP endpoint is supplied through the environment.

This means the application does not require a running observability dashboard during ordinary automated tests.

For local development, configure an OTLP endpoint using the `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable.

Do not place passwords, JWT signing keys or other secrets in the README.

## 3. Correlation IDs and Problem Details

A correlation ID connects an API error response to the trace and relevant application logs.

RondiTrack uses the current activity trace ID when available.

This allows a developer to investigate an error by searching for the same trace ID in telemetry.

Correlation IDs are included in Problem Details responses, including:

- Exceptions handled by the centralized exception handler.
- JWT authentication failures returning HTTP 401.
- Authorization failures returning HTTP 403.
- Controller-generated Problem Details responses.

The API also supports the W3C `traceparent` request header.

When a valid incoming trace context is supplied, the API preserves the associated trace ID rather than creating an unrelated one.

This is important when a request travels through multiple services.

## 4. Payout Tracing

Payout processing is an important financial operation in RondiTrack.

A custom `ActivitySource` named `RondiTrack.Payouts` is used to create a business-level span called:

`payout.process`

The payout span helps show where payout processing happens within a larger HTTP request.

When database operations are traced, their spans can appear beneath the relevant payout activity.

The payout activity records bounded operational information, such as whether processing succeeded or failed.

Sensitive information, such as payout amounts and personal identifiers, must not be added as telemetry tags.

## 5. Application Metrics

Metrics are numerical measurements that help show what the application is doing over time.

RondiTrack defines a custom meter named:

`RondiTrack`

It tracks important operations, including:

- Recorded contributions.
- Payout processing outcomes.
- Authorization refusals.

These measurements can help answer questions such as:

- How many contributions have been recorded?
- How many payout attempts succeeded or failed?
- How frequently are requests refused by authorization checks?

### Metric cardinality

Metric labels must have a limited number of possible values.

For example, labels such as `success`, `failure`, `forbidden` and `not_found` have a predictable number of values.

By contrast, user IDs, stokvel IDs, emails and idempotency keys could create enormous numbers of unique metric combinations.

RondiTrack therefore uses bounded outcome labels rather than personal or transaction-specific identifiers.

## 6. POPIA-Conscious Logging

POPIA, the Protection of Personal Information Act, requires careful handling of personal information.

Operational logging should help diagnose problems without unnecessarily collecting users' private data.

The logging review covers registration, login, refresh-token operations and contribution recording.

Sensitive information that should not appear in application logs includes:

- Plaintext passwords.
- Email addresses.
- JWT access tokens.
- Refresh-token values.
- Idempotency keys.
- Financial amounts.
- Personal and transaction identifiers.

The centralized exception handler records limited information for expected client errors.

For errors below HTTP 500, it logs operational information such as the status code, exception type and correlation ID at Information level.

Unexpected server failures are logged at Error level for investigation.

An expected HTTP 409 conflict is not treated as an unexpected server failure.

### Database command logging

Routine EF Core database-command logging is configured at Warning level.

This avoids unnecessary duplication between ordinary SQL log messages and Npgsql tracing.

It also reduces the risk of recording sensitive SQL-related information through verbose command logs.

In a production deployment, exception details, SQL tracing and exporter access must still be reviewed to ensure that sensitive data is not exposed.

## 7. Health Checks

RondiTrack exposes two health endpoints.

### `/health/live`

This endpoint answers whether the application process is running and able to respond.

It does not depend on PostgreSQL availability.

This distinction matters because a temporary database outage should not automatically mean that the application process itself needs restarting.

### `/health/ready`

This endpoint checks whether the application can connect to PostgreSQL.

It has a bounded timeout to prevent an unavailable database from causing the health request to hang indefinitely.

When PostgreSQL is unavailable, the endpoint returns HTTP 503 with an unhealthy result.

Both health endpoints allow anonymous access and return simple health information rather than sensitive diagnostic details.

## 8. Local Observability Dashboard

An OpenTelemetry-compatible collector or dashboard can be used during local development.

A local Aspire dashboard is one option.

The general process is:

1. Start an OTLP-compatible dashboard or collector.
2. Configure the RondiTrack application with the dashboard's OTLP endpoint.
3. Run the API.
4. Send a test request, preferably one that performs a database operation.
5. Open the dashboard and inspect traces from `ronditrack-api`.
6. Confirm that HTTP and database spans belong to the same trace.
7. For a payout request, inspect the `payout.process` span and its nested database spans.

The exact dashboard endpoint, transport and startup command depend on the locally installed dashboard version and configuration.

### Evidence to capture

The assignment's observability evidence should include a screenshot showing a payout trace with the `payout.process` span and related PostgreSQL spans.

The screenshot should not reveal JWTs, database credentials, personal information or financial amounts.

## 9. Automated Tests

The Assignment 6.2 test coverage includes:

- Health endpoint behaviour.
- PostgreSQL-unavailable readiness checks.
- Correlation IDs in error responses.
- Preservation of incoming trace context.
- Custom metric measurements.
- Authentication logging privacy checks.
- Contribution logging privacy checks.

The POPIA tests use temporary test data and inspect captured log messages for specific sensitive values.

These tests provide evidence that the tested workflows do not log those values. They are not a guarantee that every possible logging or telemetry path is free from sensitive information.

### Latest verified result

**137 tests passed, 0 failed, 0 skipped.**

Run the full suite with:

```powershell
dotnet test .\RondiTrack.Tests\RondiTrack.Tests.csproj --logger "console;verbosity=quiet"
```

The tests use Docker-backed PostgreSQL containers where required.

## 10. Design Decisions and Trade-offs

### Why exclude health checks from traces?

Health checks are called repeatedly by monitoring systems. Tracing every check creates noise without necessarily helping diagnose business requests.

### Why use correlation IDs?

A correlation ID makes it easier to connect a user-facing error to the trace and logs associated with that request.

### Why trace payouts separately?

A business-level payout span makes an important financial workflow visible instead of showing only generic HTTP and SQL operations.

### Why avoid identifiers in metric labels?

Identifiers create high-cardinality metrics, increase storage and processing costs, and may expose sensitive information.

### Why keep database command logging at Warning?

Routine SQL messages may duplicate trace data and increase the risk of sensitive information appearing in logs.

### Why separate liveness and readiness?

An application can be running while its database is unavailable. These are different operational conditions and should be monitored separately.

## 11. Remaining Verification

The automated test suite is passing.

The following should still be verified before the assignment is considered fully complete:

- Run the API with a local OTLP-compatible dashboard.
- Capture a real payout trace with nested PostgreSQL spans.
- Confirm telemetry export works with the chosen local dashboard.
- Review exported trace attributes and exception details for sensitive information.
- Save suitable evidence screenshots for submission.

## 12. Conclusion

Assignment 6.2 adds an observability foundation to RondiTrack.

The application now has distributed tracing, custom business spans, operational metrics, correlation-aware errors, privacy-conscious logging checks and separate liveness and readiness endpoints.

Together, these features make the API easier to troubleshoot and monitor while reducing unnecessary exposure of sensitive information.

# Assignment 6.3 — Ready for the Frontend

## 1. Assignment Overview

Assignment 6.3 focuses on preparing the RondiTrack backend API for integration with a frontend application.

Previous assignments established the database, business rules, authentication, authorization, observability, and automated tests. However, a frontend also needs a predictable and secure way to communicate with the backend.

The objective of this assignment was to make the API easier and safer for a frontend developer to consume.

The work was divided into six stages:

1. API versioning.
2. Cross-Origin Resource Sharing (CORS).
3. Rate limiting.
4. An authenticated home dashboard endpoint.
5. Consistent API error responses and OpenAPI contracts.
6. A generated TypeScript API client.

A final verification stage checked the complete test suite and confirmed that integration tests could run without the local PostgreSQL development server.

---

## 2. Stage 1 — API Versioning

### What was required?

The API needed a stable, versioned URL structure.

An API is a contract between the backend and frontend. If the backend changes its endpoints without warning, an existing frontend may stop working.

Versioning allows future changes to be introduced while preserving compatibility with existing clients.

### What we implemented

The API endpoints were organized under the `/api/v1` prefix.

For example:

```http
GET /api/v1/me/home
```

The `v1` portion identifies version 1 of the API.

### Why we did this

Imagine a mobile application is using version 1 of RondiTrack.

Later, the backend team introduces a new response structure. Rather than unexpectedly breaking the existing mobile application, the team can introduce a future version of the API.

Versioning makes it easier to maintain compatibility and coordinate frontend and backend development.

### Verification

Stage 1 was completed with 137 passing automated tests.

Git commit: `8d1c8e0`

---

## 3. Stage 2 — CORS Configuration

### What was required?

The backend needed to support requests from an approved frontend origin.

Browsers enforce a security mechanism called Cross-Origin Resource Sharing, or CORS.

A frontend and backend may run at different addresses during development.

For example:

- Frontend: `http://localhost:3000`
- Backend: `http://localhost:5000`

Even though both applications run on the same computer, the browser treats these addresses as different origins.

### What we implemented

We configured CORS in the ASP.NET Core application.

The configuration allows approved frontend origins to communicate with the API rather than permitting every website automatically.

### Why we did this

Without the appropriate CORS configuration, a browser may block a frontend request even when the backend endpoint itself works correctly.

CORS provides a controlled way to allow browser-based frontend applications to access the API.

CORS is not a replacement for authentication or authorization. The API must still validate the user's identity and permissions.

### Verification

Stage 2 was completed with 141 passing automated tests.

Git commit: `8fbf3a5`

---

## 4. Stage 3 — Rate Limiting

### What was required?

The API needed protection against excessive requests.

Rate limiting restricts how frequently a client can access certain endpoints within a defined period.

This is particularly important for authentication endpoints, which can be targeted by repeated login attempts.

### What we implemented

Rate limiting was configured in the ASP.NET Core application.

The login endpoint has a dedicated rate-limiting policy.

The automated security-test host uses a higher test-only login limit so that legitimate automated test requests do not interfere with one another.

### Why we did this

Without rate limiting, a client could send a very large number of requests in a short period.

This could overload the application or make password-guessing attacks easier.

Rate limiting helps protect server resources and reduces certain forms of abuse.

When a client exceeds an applicable limit, the API can respond with HTTP 429, meaning Too Many Requests.

### Verification

Stage 3 was completed with 145 passing automated tests.

Git commit: `fa968dc`

---

## 5. Stage 4 — Authenticated Home Dashboard

### What was required?

The frontend needed an endpoint that could supply information for a signed-in user's home screen.

Instead of forcing the frontend to coordinate many separate requests for its initial dashboard, the backend provides a dedicated endpoint.

### What we implemented

The following endpoint was introduced:

```http
GET /api/v1/me/home
```

The endpoint requires authentication.

The frontend supplies a JWT access token:

```http
Authorization: Bearer <access-token>
```

The backend identifies the authenticated user and returns the dashboard response.

### Why we did this

A frontend home screen should be able to load its required information without duplicating backend business logic.

A dedicated endpoint makes the frontend simpler and provides a clear contract for dashboard data.

It also ensures that dashboard information is associated with the authenticated user rather than an arbitrary user identifier supplied by the browser.

### Verification

Stage 4 was completed with 149 passing automated tests.

Git commit: `ce950a6`

---

## 6. Stage 5 — Consistent API Errors and OpenAPI Contracts

### What was required?

Frontend applications need predictable responses when requests fail.

For example, a frontend may need to distinguish between:

- Invalid input.
- Missing authentication.
- Insufficient permissions.
- Missing resources.
- Conflicting operations.
- Too many requests.
- Unexpected server errors.

If every endpoint returns errors in a different format, frontend developers must write special error-handling code for each endpoint.

### What we implemented

We improved the consistency of API error responses.

The changes included:

- Consistent handling of API validation errors.
- Camel-case validation error property names.
- Stricter JSON numeric input handling.
- Corrections to the generated OpenAPI schema for numeric properties.
- Additional automated tests for the error contract.

### Example: validation errors

If a frontend submits invalid data, the API should return a structured error response that the frontend can inspect.

A consistent response allows the frontend to display a useful message beside the relevant input field.

### Example: numeric values

The `CreateStokvelRequest` model contains a `contributionAmount` property.

Its OpenAPI schema now describes the value as a number:

```json
{
  "type": "number",
  "format": "double"
}
```

This matters because TypeScript code generated from OpenAPI should treat the property as a numeric value, not an arbitrary string.

### Why we did this

A reliable API contract reduces misunderstandings between frontend and backend developers.

It also makes automated client generation more accurate.

### Verification

Five new tests were added during this stage.

The complete suite passed 154 tests.

Git commit: `9c4d44c`

---

## 7. Stage 6 — Generated TypeScript API Client

### What was required?

A frontend developer should be able to use TypeScript definitions generated from the backend's OpenAPI document.

OpenAPI describes the API's routes, request bodies, responses, and data structures.

Generating TypeScript definitions reduces the need to manually recreate backend models in frontend code.

### What we implemented

We created a new frontend-client directory:

```text
clients/
└── typescript/
    ├── .gitignore
    ├── README.md
    ├── package.json
    ├── package-lock.json
    ├── tsconfig.json
    └── src/
        ├── generated/
        │   └── api.d.ts
        └── home.ts
```

### Explanation of the files

**`package.json`**

Defines the TypeScript project, its development dependencies, and commands for generating API definitions and checking TypeScript code.

**`package-lock.json`**

Records the installed npm dependency versions so that other developers can reproduce the development environment.

**`tsconfig.json`**

Configures TypeScript compilation and strict type checking.

**`src/generated/api.d.ts`**

Contains TypeScript definitions generated from the backend's OpenAPI document.

These definitions describe API paths and request/response types.

**`src/home.ts`**

Contains a small frontend API helper that demonstrates how to call the authenticated home dashboard endpoint.

**`.gitignore`**

Prevents installed dependencies, generated build folders, and local environment files from being accidentally committed.

**`README.md`**

Explains how to install dependencies, regenerate API definitions, and use the example frontend helper.

### Development tools

The client was created using:

- Node.js 24.
- npm 11.
- `openapi-typescript` 7.10.1.
- TypeScript 5.9.3.

### Generate the API definitions

Start the RondiTrack backend in Development mode:

```powershell
cd C:\Users\User\RondiTrack

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"

dotnet run --no-launch-profile --urls "http://localhost:5000"
```

Then, in another PowerShell window:

```powershell
cd C:\Users\User\RondiTrack\clients\typescript

npm.cmd ci

npm.cmd run generate
```

The generation command reads:

```text
http://localhost:5000/openapi/v1.json
```

It produces:

```text
src/generated/api.d.ts
```

### Check the TypeScript code

Run:

```powershell
npm.cmd run typecheck
```

This verifies that the TypeScript code matches the expected types.

### Example frontend request

The helper in `src/home.ts` uses the generated OpenAPI definitions.

A frontend can call it using:

```typescript
import { getHomeDashboard } from "./home.js";

const dashboard = await getHomeDashboard(
  "http://localhost:5000",
  accessToken
);
```

Here, `accessToken` is the JWT obtained after a successful login.

The helper sends the token in the `Authorization` header.

It checks whether the HTTP response was successful and throws an error when the request fails.

### Important distinction

The generated `api.d.ts` file contains TypeScript type definitions.

It is not a complete runtime API client.

The `home.ts` file provides an example of a runtime request using the browser's `fetch` function.

The TypeScript definitions help catch mistakes during development, but they do not validate server responses at runtime.

### Why we did this

Suppose the backend changes the name or type of a property.

Without generated types, the frontend developer might not notice the change until the application runs.

With generated TypeScript definitions, incompatible usage can often be detected during type checking.

This makes frontend development more reliable and reduces duplication.

### Verification

The generated definitions included the home dashboard route.

The `contributionAmount` property was generated as a TypeScript `number`.

TypeScript type checking completed successfully.

Git commit: `2a68a77`

---

## 8. Stage 7 — Final Integration Verification

### Objective

The final verification checked that the complete RondiTrack test suite still worked after all Assignment 6.3 changes.

It also checked that automated tests could run independently of the developer's local PostgreSQL database.

### Test environment

The application uses:

- .NET 10.
- ASP.NET Core.
- Entity Framework Core.
- PostgreSQL.
- xUnit.
- Docker Desktop.
- Testcontainers for PostgreSQL.

### Database independence

The test project contains two database-backed test factories:

**`PostgresApiFactory.cs`**

Creates a temporary PostgreSQL 17 database for general API integration tests.

**`RealJwtApiFactory.cs`**

Creates a separate temporary PostgreSQL 17 database for security and real-JWT authentication tests.

Both factories start their containers, configure the test application's database connection, and apply EF Core migrations.

### Verification procedure

The final test was performed with the locally installed PostgreSQL 18 Windows service stopped.

Docker Desktop remained running.

The following command was executed from the repository root:

```powershell
dotnet test
```

### Final confirmed result

```text
Test summary: total: 154, failed: 0, succeeded: 154, skipped: 0

Build succeeded
```

All 154 tests passed while the local PostgreSQL development service was stopped.

This demonstrates that the automated test suite can use its Docker-backed PostgreSQL databases without depending on the developer's local PostgreSQL service.

After testing, the local PostgreSQL service was restarted successfully.

### Important lesson

A failed build does not always mean that the source code is incorrect.

During verification, an already-running RondiTrack application was locking compiled DLL files.

The problem was resolved by stopping the running application before repeating the test command.

No source-code change was required to fix that file-lock issue.

---

## 9. Assignment 6.3 Git History

The assignment was completed incrementally on the following branch:

```text
assignment-6.3
```

The main commits were:

| Stage | Commit | Description |
|---|---|---|
| 1 | `8d1c8e0` | API versioning |
| 2 | `8fbf3a5` | CORS configuration |
| 3 | `fa968dc` | Rate limiting |
| 4 | `ce950a6` | Authenticated home dashboard |
| 5 | `9c4d44c` | Consistent errors and OpenAPI fixes |
| 6 | `2a68a77` | Generated TypeScript client |

Repository:

https://github.com/Nadio75/RondiTrack/tree/assignment-6.3

---

## 10. What Assignment 6.3 Achieved

Assignment 6.3 improved RondiTrack's readiness for frontend integration.

The backend now has:

- A versioned API route structure.
- Controlled browser access through CORS.
- Rate limiting to help protect selected endpoints.
- An authenticated endpoint for the user's home dashboard.
- More consistent validation and error responses.
- Improved OpenAPI numeric type definitions.
- Generated TypeScript API definitions.
- A working example of a typed frontend request.
- A reproducible npm-based TypeScript project.
- Automated integration tests using Docker PostgreSQL.

These improvements help a frontend developer communicate with the backend through a documented, predictable interface.

They also provide stronger automated checks for compatibility and API behavior.

---

## 11. Final Conclusion

Assignment 6.3 focused on turning RondiTrack from a functioning backend into an API that is easier for a frontend application to consume.

The work introduced API versioning, browser integration configuration, rate limiting, a home dashboard endpoint, consistent error contracts, and generated TypeScript definitions.

The final verification confirmed that the solution built successfully and all 154 automated tests passed.

The integration tests also passed while the local PostgreSQL development server was stopped, confirming their independence from that server.

**Final verified test result: 154 passed, 0 failed, 0 skipped.**

The RondiTrack backend now has a stronger foundation for frontend development and integration.

# Assignment 6.4 — Shipped, Scheduled and Handed Over

## 1. Assignment Overview

Assignment 6.4 focuses on preparing RondiTrack for deployment and ongoing operation.

Earlier assignments established the API, PostgreSQL database, authentication, authorization, observability, frontend integration, and automated tests.

This assignment builds on that foundation by introducing scheduled financial processing, database housekeeping, containerized deployment, and operational handover documentation.

The work includes:

1. Automatically closing contribution cycles.
2. Recording arrears for missed contributions.
3. Running scheduled background services.
4. Cleaning up old refresh-token records.
5. Packaging the application using Docker.
6. Automatically applying database migrations during deployment.
7. Verifying the application through health checks and automated tests.
8. Documenting deployment instructions and known limitations.

---

## 2. Monthly Contribution-Cycle Closing

### What was required?

RondiTrack needed a way to close contribution cycles automatically according to a configured monthly schedule.

A stokvel is a savings group whose members contribute money according to agreed rules.

At the end of a contribution period, the system needs to identify members who have not contributed and record their outstanding obligations.

### What we implemented

The implementation includes:

- `RondiTrack.Domain/CycleClosingSchedule.cs`
- `Services/CycleClosingService.cs`
- `Services/CycleClosingBackgroundService.cs`
- Updates to `ContributionCycle.cs` and `Stokvels.cs`

`CycleClosingSchedule` contains the rules for determining when a contribution cycle is due to close.

`CycleClosingService` performs the actual closing operation and records outstanding contributions.

`CycleClosingBackgroundService` runs periodically to check for cycles that need to be closed.

The system uses a configurable closing day for each stokvel.

### Why we did this

Without automatic closing, an administrator would need to check contribution periods manually.

That could result in forgotten periods, inconsistent records, and delays in identifying unpaid contributions.

Automatic closing makes the process more consistent and reduces manual work.

### Reliability safeguards

The closing implementation includes safeguards for:

- Preventing repeated closing of the same cycle.
- Handling missed closing periods.
- Using PostgreSQL transactions and locking to protect concurrent processing.
- Recording closing state in the database.

These safeguards are important because scheduled jobs may restart or run again after interruptions.

---

## 3. Contribution Arrears

### What was required?

The system needed to track contributions that members failed to make before a cycle closed.

An *arrear* is money that should already have been paid but remains outstanding.

### What we implemented

A new domain model was added:

`RondiTrack.Domain/Models/ContributionArrear.cs`

The database was updated to support arrears and contribution-cycle closing state.

The relevant EF Core migrations are:

- `20261014160151_AddContributionArrears`
- `20261014163221_AddContributionCycleClosingState`
- `20261014164918_AddStokvelClosingDay`
- `20261014165426_SyncStokvelClosingDayConstraint`

### Why we did this

Without arrears tracking, a missed contribution could be difficult to distinguish from a contribution that was never expected.

Recording arrears preserves the financial history of the stokvel and helps identify outstanding obligations.

---

## 4. Scheduled Database Housekeeping

### What was required?

The application needed routine maintenance to prevent old authentication records from accumulating indefinitely.

### What we implemented

The following services were added:

- `Services/DatabaseHousekeepingService.cs`
- `Services/DatabaseHousekeepingBackgroundService.cs`

The housekeeping process removes refresh-token records that have been expired for more than 30 days.

It runs periodically using configurable application settings.

### Why we did this

Refresh tokens support user sessions and authentication.

Over time, expired records can accumulate in PostgreSQL.

Removing sufficiently old expired tokens helps control database growth and reduces the amount of unnecessary authentication data retained.

### Important limitation

This housekeeping process does not replace a complete data-retention policy.

Other stored records, including financial and idempotency data, may require separate retention rules.

---

## 5. Docker Deployment

### What was required?

The application needed a reproducible way to run the API and PostgreSQL without manually configuring every component on a new machine.

### What we implemented

RondiTrack uses:

- `Dockerfile` to build the application and migration images.
- `compose.yaml` to define the services.
- `.env.example` to show the required environment variable names.

Docker Compose defines three services:

| Service | Purpose |
|---|---|
| `db` | Runs PostgreSQL 17 |
| `migrate` | Applies EF Core database migrations |
| `api` | Runs the RondiTrack ASP.NET Core API |

### Startup order

Docker Compose starts PostgreSQL and waits for its health check.

The migration container then applies outstanding EF Core migrations.

Only after the migration container completes successfully does the API start.

This prevents the application from starting against an outdated database schema.

### Why we did this

A developer deploying the application should not have to remember a separate manual database migration command.

The deployment configuration makes startup more repeatable and reduces the risk of missing a database update.

---

## 6. Running RondiTrack with Docker

### Prerequisites

Install:

- Docker Desktop with Docker Compose support.
- Git, if cloning the repository.

.NET SDK is not required on the host machine for the Docker-only deployment.

### Step 1 — Clone the repository

```powershell
git clone https://github.com/Nadio75/RondiTrack.git
cd RondiTrack
git checkout assignment-6.4
```

The branch must be pushed to GitHub before these instructions can be used from a fresh clone.

### Step 2 — Create the environment file

```powershell
Copy-Item .env.example .env
notepad .env
```

Configure the following values:

```dotenv
POSTGRES_PASSWORD=
JWT_ISSUER=
JWT_AUDIENCE=
JWT_SIGNING_KEY=
```

Use a strong, unique PostgreSQL password.

The JWT signing key must be a cryptographically secure, Base64-encoded key compatible with the application's validation requirements.

Do not commit `.env` or production secrets to Git.

### Step 3 — Start the application

```powershell
docker compose up -d --build
```

Docker Compose builds the images, starts PostgreSQL, runs outstanding database migrations, and starts the API.

### Step 4 — Check container status

```powershell
docker compose ps -a
```

Expected results:

- PostgreSQL: healthy.
- Migration container: exited successfully with code 0.
- API: running.

### Step 5 — Verify the health endpoints

```powershell
curl.exe -i http://localhost:8080/health/live
curl.exe -i http://localhost:8080/health/ready
```

Both endpoints should return HTTP 200 when the application and PostgreSQL are healthy.

The `/health/live` endpoint checks application liveness.

The `/health/ready` endpoint checks readiness, including database connectivity.

### Step 6 — View logs

```powershell
docker compose logs api --tail 100
docker compose logs migrate --tail 100
docker compose logs db --tail 100
```

### Step 7 — Stop the application

```powershell
docker compose down
```

This stops and removes the Compose containers while preserving the named PostgreSQL data volume.

**Warning:** Do not use `docker compose down -v` unless you intentionally want to delete the database volume and its stored data.

---

## 7. EF Core Migration Bundle

The Dockerfile creates a self-contained Linux EF Core migration executable using EF Core 10.0.12.

The migration executable is packaged into a dedicated Docker image.

During deployment, the migration container reads its PostgreSQL connection configuration from environment variables supplied by Docker Compose.

The migration bundle uses dummy build-time configuration values because the application validates JWT settings when EF Core constructs the database context.

Real production secrets are not required to

## Frontend — Assignment 7.1

### Overview

The RondiTrack frontend is a React 19 application built with Vite and TypeScript. Assignment 7.1 introduces two reusable components:

- **StokvelCard:** Displays a stokvel's name, the user's membership role, contribution amount, current cycle, payment summary and closing deadline.
- **ContributionCard:** Displays a member's contribution amount and recorded timestamp, with demonstration support for Pending, Paid and Confirmed status labels.

The frontend currently uses sample data. Authentication and live API integration are not part of this demonstration.

### Requirements

- Node.js 24
- npm
- Docker Desktop for running the backend API
- Backend OpenAPI endpoint available at `http://localhost:8080/openapi/v1.json`

### Running the frontend

From the repository root:

```powershell
cd web
npm.cmd ci
npm.cmd run dev
```

Open `http://localhost:3000`.

On systems where PowerShell permits npm scripts directly, `npm` may be used instead of `npm.cmd`.

### Generating API types

Start the backend in Development mode:

```powershell
docker compose -f compose.yaml -f compose.dev.yaml up -d --build
```

Then run:

```powershell
cd web
npm.cmd run generate:api
```

The generated TypeScript definitions are stored in `web/src/api/schema.d.ts` and should be committed to Git.

Do not manually edit this generated file.

### Running quality checks

```powershell
cd web
npm.cmd run typecheck
npm.cmd run lint
npx.cmd vitest run
npm.cmd run build
```

### Project structure

| Path | Purpose |
|---|---|
| `src/api/schema.d.ts` | Generated OpenAPI TypeScript definitions |
| `src/api/types.ts` | Convenient aliases for generated API types |
| `src/components/StokvelCard.tsx` | Stokvel summary component |
| `src/components/ContributionCard.tsx` | Contribution display component |
| `src/data/sample.ts` | Fictional demonstration data |
| `src/lib/money.ts` | Formats integer cents as South African rand |
| `src/lib/apiMoney.ts` | Validates and converts API integer-cent values |
| `src/lib/dates.ts` | Formats timestamps in South African Standard Time |

### Important implementation details

**Money:** Dashboard amounts are represented as integer cents to avoid floating-point rounding problems. The existing contribution-list API still returns decimal rand amounts, which the component converts for display.

**Dates:** UTC timestamps are displayed using the `Africa/Johannesburg` timezone. This includes timestamps that cross into the next calendar day in South Africa.

**Roles:** The Treasurer-only placeholder is displayed according to the role supplied for the individual stokvel. Hiding a button is a user-interface decision, not an authorization or security mechanism.

**Statuses:** Pending, Paid and Confirmed are demonstration labels only. The current backend does not yet implement a separate payment confirmation lifecycle.

**Payout dates:** An unavailable scheduled payout date is displayed as "Not scheduled" rather than inventing a date.

### Testing

The frontend includes 15 passing unit tests covering money formatting, API amount validation and timezone conversion.

The backend dashboard integration tests also pass, covering authentication, membership isolation, role information and payment summaries.