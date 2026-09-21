# RondiTrack
# RondiTrack — API Foundations & Domain Modeling

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
