---
tags: [backoffice, property-editor, block-editor, clipboard]
---

# Wrapping Umbraco's native block editor with restriction filtering

> **Status:** Planned — this page is a stub. The full tutorial hasn't been written yet; see the [tutorial backlog](../IDEAS.md) for the framing and motivation.

`BlockGridRestricted` and `BlockListRestricted` are custom property editor UIs that don't replace Umbraco's native block editors — they wrap them, filter the allowed-block list down to the editor's tenant-scoped restrictions, and then hand off to the native UI for everything else. This refinement will show how to extend an existing Umbraco backoffice editor without forking it, including the clipboard-translator boilerplate that copy-paste needs in order to survive the wrapping (otherwise a copy from a wrapped editor pasted into a native editor will be silently rejected, which is a fun bug to track down).

## What this will cover

- Hooking into the native block editor as a wrapper rather than a replacement.
- Filtering the allowed-block list at the right layer in the property editor lifecycle.
- Clipboard translators for copy/paste between wrapped and native editors.
- Workspace-view registration for the per-document-type configuration UI.

*If you're picking this up to write, follow the section structure in [Contributing a new tutorial](../README.md#contributing-a-new-tutorial).*
