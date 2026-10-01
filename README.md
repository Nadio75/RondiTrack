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