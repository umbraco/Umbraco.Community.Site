---
tags: [ef-core, persistence, version-control, zip-import]
---

# Rules in both EF Core and JSON: dual persistence for version-controllable config

> **Status:** Planned — this page is a stub. The full tutorial hasn't been written yet; see the [tutorial backlog](../IDEAS.md) for the framing and motivation.
> **Prerequisites (when written):** This refinement will build on the planned [Configuration that inherits down the content tree](../foundations/content-tree-inherited-config.md) foundation (or stand alone).

Block Restrictions in this repo stores its rules in both an EF Core database (for runtime querying) and JSON files on disk (so the rules can be reviewed in pull requests). This refinement will walk through how to keep both stores in sync without an infinite save loop, what to do when they disagree on first boot, and the zip export/import flow that bridges environments where direct file access isn't available (like Umbraco Cloud staging). The non-obvious bit is that JSON is the source of truth on first boot but the database is the source of truth thereafter — and the small state machine that policy needs to be honest about.

## What this will cover

- EF Core for runtime querying, JSON files for version control.
- Avoiding the save-loop trap when both sides can write.
- First-boot resolution: JSON wins, and then the DB takes over.
- Zip export/import for cloud-hosted environments where you can't ship files via deployment.

*If you're picking this up to write, follow the section structure in [Contributing a new tutorial](../README.md#contributing-a-new-tutorial).*
