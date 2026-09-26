---
name: back-end-enhance
description: Use whenever the user wants to implement or enhance Calypsov's .NET/Photino backend (Calypsov/Api, Calypsov/Services, Calypsov/Models, Program.cs) — new API endpoints, new service logic, new data models, or changes to existing backend behavior. Triggers on requests like "add an endpoint for X", "the backend needs to support Y", "wire up storage for Z", or "expose an API for W" — and is also invoked directly by the front-end-enhance skill when a frontend feature needs new backend support. Always read (or create) Calypsov/PROJECT_README.md before planning.
---

# back-end-enhance

Codifies a repeatable way to add or enhance Calypsov backend features: minimal diff, consistent with existing patterns, and — critically — never inventing business logic the user hasn't actually specified. When a request doesn't spell out the logic, this skill's job is to build the correct, clean *shape* for it and leave the actual behavior for the user to fill in by hand, rather than guessing.

## Workflow

1. **Read the project doc**: `Calypsov/PROJECT_README.md`. It has the stack, folder structure, and the conventions for endpoints/services/models, plus a running feature log.
   - If it doesn't exist yet, create it first — see "Creating the project doc" below. Don't guess at conventions from general ASP.NET knowledge; this project has specific patterns (the `Try` error-wrapper, the interface + swappable-implementation service pattern, `Program.cs` as the sole place that picks a concrete implementation) that generic minimal-API code wouldn't follow.
   - If it exists but looks stale against the actual code, refresh the relevant section before relying on it.

2. **Plan** where the change belongs given the existing structure: a minimal-API endpoint group in `Api/`, an interface + implementation in `Services/`, a DTO in `Models/`, and the DI/wiring line in `Program.cs`.

3. **Reuse first**: before adding anything new, check whether an existing service method or endpoint already does this, or could be extended with a parameter or an optional field. Prefer extending over duplicating — a smaller API surface is easier for the user to keep track of.

4. **Implement**, following the exact conventions already in the codebase (see the project doc for specifics):
   - Endpoints: a `Map<Feature>Endpoints(this WebApplication app, I<Feature>Service service)` extension method in `Api/`, registered in `Program.cs`, routes grouped under `/api/<feature>`, bodies that can fail wrapped in the existing `Try(...)` error-handling pattern.
   - Services: interface in `Services/I<Feature>Service.cs` describing the contract (including what each thrown exception means), plus a concrete implementation. `Program.cs` is the only place that decides which implementation is constructed — never hardwire a concrete type into an endpoint file.
   - Models: plain C# records/classes in `Models/`, matching the immutable-DTO style already there.
   - Keep the diff small — no unrelated refactors, no renaming things in passing. This is code the user will read and hand-modify next, so prioritize being obvious over being clever.

5. **When the logic isn't specified**: if the prompt gives you the actual business logic (or it's simple/unambiguous — e.g. "return the app version from the assembly"), implement it for real. If it doesn't (e.g. "add an endpoint to export settings as a file" with no detail on format or destination), still build the *entire* surrounding structure — endpoint, route, request/response models, service interface, method signature, DI registration — but leave the method body an explicit, clearly-marked stub (e.g. `throw new NotImplementedException("TODO: decide export format")`, or a short comment naming exactly what decision is needed) rather than inventing behavior. The user asked specifically for this so they don't come back to find guessed-at rules baked into working-looking code.

6. **Update the project doc** — after implementing, go back to `Calypsov/PROJECT_README.md` and append an entry to the Feature log describing what was added, and flag clearly if anything was left stubbed for the user to implement manually. Keep it short and factual, one entry per change.

## Creating the project doc (if missing)

Explore the actual current code rather than assuming. At minimum read `Program.cs`, one `Api/*Endpoints.cs` file, one `Services/I*.cs` interface plus its implementation(s), and skim `Models/`. Structure the doc as:

```markdown
# Backend Project Doc

## Stack
(.NET version, Photino.NET.Server, ASP.NET minimal APIs, the Debug-vs-Release hosting split)

## Structure
(what belongs in Api/, Services/, Models/, Helpers/, and how Program.cs wires them together)

## Conventions
(the endpoint-group pattern, the error-wrapper pattern, the interface+implementation service pattern and how Program.cs picks one, DTO style, concurrency approach)

## Feature log
(empty to start — this skill appends one entry per feature going forward, including anything left stubbed)
```

Cite real file paths and concrete examples pulled from the code, not generic ASP.NET advice — the doc's whole value is being specific to *this* codebase.
