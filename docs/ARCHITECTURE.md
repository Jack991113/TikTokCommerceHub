# Architecture

## Goal

The old working suite grew by copying store apps, packaging outputs, and runtime data into the same tree.

This repository separates:

- maintainable source code
- shared business logic
- runtime data
- packaging scripts

without breaking the currently running local apps.

## Top-level structure

```text
TikTokCommerceHub/
  apps/
    Store1/
    Store2/
    StoreShared/
    Dashboard/
  src/
    TikTokCommerceHub.Domain/
    TikTokCommerceHub.Application/
    TikTokCommerceHub.Infrastructure/
    TikTokCommerceHub.Web/
  docs/
  tools/
```

## App layout

### Store1 and Store2

These are the local printing apps.

Responsibilities:

- poll TikTok order APIs
- capture buyer handle via Seller Center bridge
- render and print tickets
- expose local management UI

Most of the code is shared through:

- `apps/StoreShared`

Only store-specific launch/config files remain in each store app.

### Dashboard

This is the analytics app.

Responsibilities:

- sales overview
- reconciliation
- product performance
- streamer compensation
- export and workbook generation

### New layered foundation

The `src/` tree is the forward-looking architecture:

- `Domain`
  - business entities and shared rules
- `Application`
  - use cases and DTOs
- `Infrastructure`
  - file/runtime readers and external integration support
- `Web`
  - lightweight web endpoints

This allows gradual migration from the older app-heavy structure into a cleaner shared backend.

## Runtime data

Runtime data is intentionally outside Git-safe source control flow:

- `apps/Store1/Data/runtime-state.json`
- `apps/Store2/Data/runtime-state.json`
- `apps/Store1/Data/print-jobs/`
- `apps/Store2/Data/print-jobs/`

These files may contain:

- tokens
- printer settings
- cached order data
- print history

They must not be pushed to GitHub.

## Packaging strategy

Packaging is script-driven:

- `tools/package-commerce-suite.ps1`

Modes:

- `ClientClean`
  - safe to send to customers
  - excludes runtime secrets and order data
- `ConfiguredOwner`
  - includes current configuration for owner use

## GitHub export strategy

GitHub-safe export is created by:

- `tools/export-github-clean.ps1`

The export rewrites paths where needed and excludes runtime-sensitive files.

## Why this is slimmer than the old tree

The old size looked much larger mainly because of:

- duplicated Store1 and Store2 source
- `bin/obj/publish`
- packaged artifacts
- validation and staging directories
- runtime data mixed into source trees

After extracting shared code, the maintainable source is much smaller and easier to reason about.

## Next refactor direction

Recommended next steps:

1. Keep Store1 and Store2 running as-is.
2. Continue moving duplicated frontend pieces into shared assets.
3. Move more dashboard query logic into `src/Application`.
4. Reduce app-specific startup code even further.
5. Eventually make a cleaner deployable split:
   - server-side business backend
   - local print agent
