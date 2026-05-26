---
tags: [primer, deployment, umbraco-cloud, cicd]
---

# Deployment primer

> **Status:** Planned — this page is a stub. The full primer hasn't been written yet; see the [primer backlog](./IDEAS.md) for the framing and motivation.

Umbraco Cloud deploys, the `copy-for-cloud.js` asset step, the staging-reset flow, and the model-builder regen step. This primer will be the orientation layer for "how do changes get from develop to production"; for the operational notes — what to do when a Cloud deploy fails halfway, schema-management gotchas, the upgrade dance — see [`LESSONS_LEARNED.md`](../LESSONS_LEARNED.md).

## What this will cover

- The Umbraco Cloud deploy flow at a glance.
- The `copy-for-cloud.js` asset step and what it does.
- The staging reset: when to use it and what it nukes.
- Model-builder regeneration after backoffice changes.

*If you're picking this up to write, a primer is the orientation layer — keep it short and signpost the operational notes and tutorials for depth.*
