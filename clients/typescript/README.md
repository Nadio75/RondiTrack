# RondiTrack TypeScript API Client

This folder contains TypeScript definitions generated from the RondiTrack ASP.NET Core backend's OpenAPI specification.

## Purpose

The TypeScript client allows frontend developers to use strongly typed API requests and responses without manually recreating the backend's C# models.

The generated definitions cover the endpoints documented by the RondiTrack API, including authentication, stokvel management, and the authenticated home dashboard.

## Requirements

- Node.js 24
- npm 11
- RondiTrack backend running in Development mode when regenerating types

## Installation

Open a terminal in `clients/typescript` and run:

```bash
npm ci
```

On Windows PowerShell, use `npm.cmd ci` if PowerShell blocks `npm.ps1`.

## Generate API Types

First, start the RondiTrack backend from the repository root:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
dotnet run --no-launch-profile --urls "http://localhost:5000"
```

Keep the backend running.

In another terminal, navigate to `clients/typescript` and run:

```powershell
npm.cmd run generate
```

This reads:

`http://localhost:5000/openapi/v1.json`

And generates:

`src/generated/api.d.ts`

Do not manually edit the generated file. Regenerate it whenever the backend's API contract changes.

## Check TypeScript

Run:

```powershell
npm.cmd run typecheck
```

This uses TypeScript's strict mode to verify that the client code compiles.

## Example: Authenticated Home Dashboard

The file `src/home.ts` demonstrates how to call:

`GET /api/v1/me/home`

It imports the generated API types, sends a bearer token in the Authorization header, and returns a typed dashboard response.

Example usage:

```typescript
import { getHomeDashboard } from "./home.js";

const dashboard = await getHomeDashboard(
  "http://localhost:5000",
  "<access-token>"
);

console.log(dashboard);
```

Replace the placeholder with a valid access token obtained through the application's authentication flow. Never commit real access tokens.

This example is illustrative and is not automatically executed by the TypeScript type-check command.

## Project Structure

```text
clients/typescript/
├── .gitignore
├── README.md
├── package.json
├── package-lock.json
├── tsconfig.json
└── src/
    ├── home.ts
    └── generated/
        └── api.d.ts
```

## Important Notes

- `api.d.ts` provides TypeScript type definitions, not a complete HTTP client.
- `home.ts` is a small example that uses the browser or Node.js `fetch` API.
- Generated types provide compile-time checking; they do not validate API responses at runtime.
- `node_modules` is excluded from Git.
- `package-lock.json` is committed to support reproducible dependency installation.
- If the backend's API changes, regenerate the definitions and run the type checker again.