---
tags: [authentication, oauth, github, members]
---

# GitHub OAuth login for Umbraco members

> **Status:** Planned — this page is a stub. The full tutorial hasn't been written yet; see the [tutorial backlog](../IDEAS.md) for the framing and motivation.

Community members on this site sign in exclusively via GitHub — no username/password login exists. Wiring an external OAuth provider into Umbraco's member system has a few sharp edges that aren't obvious from the ASP.NET Core OAuth docs: the handler has to be registered with `AddOAuth` rather than the more generic `AddRemoteScheme` (the latter skips `OAuthPostConfigureOptions` and null-refs in `OAuthHandler.BuildChallengeUrl`), the `email` OAuth scope only exposes a member's *public* profile email so a second API call is needed for a private-but-verified address, and signing a member out cleanly means clearing all four Umbraco auth cookies (member, external login, 2FA, 2FA remember-me) via `IMemberSignInManager.SignOutAsync()` rather than a plain `HttpContext.SignOutAsync()`. This tutorial will walk through the working implementation in `Features/Members/`.

## What this will cover

- Registering a custom OAuth handler with `AddMemberExternalLogins` / `AddOAuth<TOptions, THandler>`, and why `AddRemoteScheme` breaks the challenge flow.
- Auto-linking and auto-approving new members via `GitHubExternalLoginProviderOptions`, including the `OnCreatingTicket` call to GitHub's `/user/emails` endpoint for a verified private email.
- The Umbraco-provided pieces this leans on: `UmbExternalLoginController` for the redirect/callback dance, `IMemberManager` for reading the signed-in member, `IMemberSignInManager` for a complete sign-out.
- Surfacing sign-in state in shared layout (`MenuViewModelBuilder` populating display name/avatar from claims) versus a dedicated account page.

*If you're picking this up to write, follow the section structure in [Contributing a new tutorial](../README.md#contributing-a-new-tutorial).*
