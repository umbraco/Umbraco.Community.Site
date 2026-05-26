---
tags: [sessionize, external-api, data-extraction]
---

# Extracting structured-but-arbitrary data from an external API

> **Status:** Planned — this page is a stub. The full tutorial hasn't been written yet; see the [tutorial backlog](../IDEAS.md) for the framing and motivation.

Sessionize stores per-speaker data like pronouns as free-form question/answer entries — there's a top-level `questions` array with IDs and labels, and each speaker has a `questionAnswers` array with `questionId` + `answer`. To extract "the pronouns answer for this speaker", you need to resolve the right `questionId` once, cache it on the parent data object (`SessionizeAllData.PronounsQuestionId`), then look it up in each speaker's answers — instead of scanning every speaker's questions per item. The pattern transfers anywhere an external API stores semi-structured data behind a layer of indirection (Airtable, Notion, plenty of CMSs).

## What this will cover

- Resolving the right `questionId` once and caching it on the parent data object.
- Per-record answer lookup off the cached ID.
- Cache invalidation when the upstream schema shifts (and how to notice that it has).
- The "answers keyed by question ID" pattern in general, beyond Sessionize.

*If you're picking this up to write, follow the section structure in [Contributing a new tutorial](../README.md#contributing-a-new-tutorial).*
