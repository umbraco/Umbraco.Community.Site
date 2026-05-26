---
tags: [ef-core, migrations, razor-class-library, package, composer]
---

# Shipping EF Core migrations from a Razor Class Library package

> **Status:** Planned — this page is a stub. The full tutorial hasn't been written yet; see the [tutorial backlog](../IDEAS.md) for the framing and motivation.

A package that needs its own database schema (like Block Restrictions in this repo) shouldn't make the host project wire migrations up by hand. This refinement will walk through the pattern: a Razor Class Library that owns its `DbContext`, its migrations, and a composer that registers a hosted service to run those migrations *after* Umbraco has finished booting. Getting the ordering right was harder than it looked — see PR #132 on develop for the actual fix and the constraint that drove it.

## What this will cover

- Owning a `DbContext` and EF Core migrations inside a Razor Class Library.
- A composer + hosted-service pattern for migration timing.
- The ordering footgun: Umbraco must finish booting before EF migrations can run.
- How a host project consumes the package without wiring anything up itself.

*If you're picking this up to write, follow the section structure in [Contributing a new tutorial](../README.md#contributing-a-new-tutorial).*
