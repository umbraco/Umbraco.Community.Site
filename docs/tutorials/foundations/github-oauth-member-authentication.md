---
tags: [authentication, oauth, github, members]
---

# GitHub OAuth login for Umbraco members

Community members on this site don't have passwords. There's no "forgot password" flow to build, no email-verification loop to maintain, and no credential store to worry about leaking — sign-in is GitHub or nothing. For a site built *for* developers, most of whom already have a GitHub account they use daily, that's not a compromise; it's the obvious identity provider. This tutorial walks through how that's wired into Umbraco's member system, and the handful of sharp edges that bit along the way. It's a *foundation* piece — nothing else in this suite builds on it yet, but it's the base the account page and the wider community-profile feature sit on top of.

## Why you might want this

Umbraco ships two separate identity systems: **Users** (backoffice editors, who authenticate against your Umbraco instance directly or via SSO) and **Members** (front-end visitors, who can register, log in, and see gated content). [External OAuth login](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/) is well documented for the Users side — most Umbraco/Azure AD walkthroughs are about getting *editors* into the backoffice. Wiring GitHub in as an external login provider for **members** is a much thinner-trodden path, and username/password member accounts have their own tax: reset-password emails, a login form to style and secure, credentials to hash and store. If your members are already members of a community with an obvious identity provider — GitHub for developers, much as Google or Microsoft might be for other audiences — letting that provider do the authenticating removes an entire feature's worth of surface area.

## What we're building

Three moving pieces, glued together at startup:

1. **An OAuth handler for the *Members* identity system**, registered via a composer, using the third-party [`AspNet.Security.OAuth.GitHub`](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers) package (aspnet-contrib) rather than a hand-rolled one — GitHub OAuth is a solved problem, no need to re-solve it.
2. **Auto-linking rules** that create a `communityMember` on first GitHub sign-in, pre-approved, with a display name and handle sourced from GitHub's claims.
3. **Header integration** — a sign-in button that starts the OAuth round trip, and, once signed in, an avatar with a sign-out control that clears every auth cookie Umbraco set.

None of this needed a database migration or a new content type — it's entirely wiring, sitting on top of what Umbraco's `Umbraco.Cms.Web.Common.Security` namespace already gives you for member external logins.

## Why the obvious fix doesn't work

Three things looked right and weren't, each one worth knowing about *before* you hit it rather than after:

**Registering the handler with `AddRemoteScheme` instead of `AddOAuth`.** Both exist on `AuthenticationBuilder`. `AddOAuth<TOptions, THandler>` is generic and looks like the "proper" way to add a typed OAuth handler, and if you go looking at what it does under the hood it's mostly a call to `AddRemoteScheme` anyway — so it's tempting to just call `AddRemoteScheme` directly and skip a layer. Don't. `AddOAuth` also registers `OAuthPostConfigureOptions<TOptions, THandler>`, an `IPostConfigureOptions<TOptions>` that sets `options.StateDataFormat` after your configuration delegate runs, if you haven't set it yourself. Skip that registration and `StateDataFormat` stays null — and the handler null-refs the moment it tries to protect the OAuth state on the challenge redirect. The fix is one word: call `AddOAuth`, not `AddRemoteScheme`.

**Trusting the OAuth `email` claim on its own.** The [GitHub OAuth scopes](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/scopes-for-oauth-apps) you'd reach for first — `read:user`, `user:email` — get you a claim for the member's email, but it's their *public* profile email, which for a lot of developers is either empty or a noreply address. The actual verified address (the one you'd want for outbound email) only comes back from a separate authenticated call to `GET /user/emails`. If you stop at the claim, some fraction of your members end up with no usable email on record.

**Signing out with `HttpContext.SignOutAsync()`.** This is the ASP.NET Core-idiomatic way to sign a user out, and it's *almost* right — but Umbraco's member pipeline sets four separate auth cookies across a GitHub sign-in (member, external login, 2FA, 2FA remember-me), and a bare `SignOutAsync()` on the default scheme only clears the first. The member looks signed out — no avatar, sign-in button back — but stale cookies linger, and the next external-login attempt can behave oddly because Umbraco still thinks a login is in progress. `IMemberSignInManager.SignOutAsync()` is the method that actually knows about all four and clears them together.

## Walkthrough

### Step 1 — Reach for the community-maintained package, not a hand-rolled handler

`GitHubAuthenticationOptions` and `GitHubAuthenticationHandler` come from [`AspNet.Security.OAuth.GitHub`](https://www.nuget.org/packages/AspNet.Security.OAuth.GitHub) (`10.0.0`, pinned centrally in `Directory.Packages.props`), part of the aspnet-contrib OAuth providers collection that covers most of the well-known identity providers on top of `Microsoft.AspNetCore.Authentication.OAuth`'s generic `OAuthHandler<TOptions>`. Nothing in this repo re-implements the OAuth handshake — the whole feature is composition on top of that package plus Umbraco's own member-auth plumbing.

### Step 2 — Register the handler behind a config guard, scoped to Members

The whole thing lives in one `IComposer`, [`Features/Members/RegisterGitHubAuth.cs`](../../../src/UmbracoCommunity.Web/Features/Members/RegisterGitHubAuth.cs):

```csharp
public class RegisterGitHubAuth : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        var clientId = builder.Config["GitHub:ClientId"] ?? string.Empty;
        var clientSecret = builder.Config["GitHub:ClientSecret"] ?? string.Empty;

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            return;

        builder.AddMemberExternalLogins(logins =>
        {
            logins.AddMemberLogin(
                auth => auth.AddOAuth<GitHubAuthenticationOptions, GitHubAuthenticationHandler>(
                    GitHubExternalLoginProviderOptions.SchemeName,
                    "GitHub",
                    options =>
                    {
                        options.ClientId = clientId;
                        options.ClientSecret = clientSecret;
                        options.Scope.Add("read:user");
                        options.Scope.Add("user:email");

                        options.Events.OnCreatingTicket = /* Step 3 */;
                    }),
                GitHubExternalLoginProviderOptions.Configure);
        });
    }
}
```

Two things worth calling out. First, the guard clause: if either config key is missing, the composer returns *before* registering anything — no exception, no partially-wired authentication **scheme** (ASP.NET Core's term for a named, independently-configured login handler; an app can register several side by side), GitHub sign-in simply doesn't exist for that environment. That makes it safe to run this codebase locally or in CI with no GitHub App configured at all; the header falls back to whatever `enableMemberSignIn` and the rest of the login UI decide (Step 6). Second, `builder.AddMemberExternalLogins(...)` is Umbraco's own entry point for registering external logins *for members specifically* — a sibling to the backoffice-Users external-login registration, not the same call. `AddMemberLogin` takes the `AuthenticationBuilder` configuration (the `AddOAuth<...>` call above) and a second delegate — `GitHubExternalLoginProviderOptions.Configure` — that configures the *Umbraco-specific* side: auto-linking (Step 4).

### Step 3 — Backfill the verified private email

Still inside the options delegate, `OnCreatingTicket` is a hook the OAuth handler calls once it's got a token back from GitHub, before Umbraco does anything with the resulting identity:

```csharp
options.Events.OnCreatingTicket = async context =>
{
    var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
    request.Headers.UserAgent.ParseAdd("UmbracoCommunity/1.0");
    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

    var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
    if (!response.IsSuccessStatusCode) return;

    var json = await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted);
    var emails = JsonSerializer.Deserialize<List<GitHubEmail>>(json,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    var primary = emails?.FirstOrDefault(e => e.Primary && e.Verified);
    if (primary == null) return;

    context.Identity?.TryRemoveClaim(context.Identity.FindFirst(ClaimTypes.Email));
    context.Identity?.AddClaim(new Claim(ClaimTypes.Email, primary.Email));
};
```

`context.Backchannel` is the `HttpClient` the OAuth handler already has configured for server-to-server calls — reuse it rather than constructing a new client. GitHub's API insists on a `User-Agent` header (the request fails without one) and rewards a versioned `Accept` header. The response is a JSON array of every email on the account, each flagged `primary`/`verified` independently — a GitHub account can have several verified emails, but only one primary, so `FirstOrDefault(e => e.Primary && e.Verified)` is the one you want. If nothing matches (the account genuinely has no verified email — rare, but possible), the method returns without touching the identity, and whatever public email claim the base OAuth flow produced stands as-is.

### Step 4 — Auto-link, auto-approve, and keep the profile fresh on every login

The second delegate from Step 2, [`Features/Members/GitHubExternalLoginProviderOptions.cs`](../../../src/UmbracoCommunity.Web/Features/Members/GitHubExternalLoginProviderOptions.cs), configures what Umbraco does with the identity once it has one:

```csharp
public sealed class GitHubExternalLoginProviderOptions
{
    public const string SchemeName = "UmbracoMembers.GitHub";
    private const string DefaultGroup = "Community Members";

    internal static void Configure(MemberExternalLoginProviderOptions options)
    {
        options.AutoLinkOptions = new MemberExternalSignInAutoLinkOptions(
            true, false, CommunityMember.ModelTypeAlias, null, [DefaultGroup])
        {
            OnAutoLinking = (autoLinkUser, loginInfo) =>
            {
                var email = loginInfo.Principal.FindFirst(ClaimTypes.Email)?.Value;
                var handle = loginInfo.Principal.FindFirst(ClaimTypes.Name)?.Value;
                var displayName = loginInfo.Principal.FindFirst("urn:github:name")?.Value ?? handle;

                autoLinkUser.Name = displayName;
                autoLinkUser.UserName = handle;
                autoLinkUser.Email = email;
                autoLinkUser.IsApproved = true;
            },
            OnExternalLogin = (user, loginInfo) =>
            {
                var email = loginInfo.Principal.FindFirst(ClaimTypes.Email)?.Value;
                var handle = loginInfo.Principal.FindFirst(ClaimTypes.Name)?.Value;
                var displayName = loginInfo.Principal.FindFirst("urn:github:name")?.Value ?? handle;

                user.Name = displayName;
                user.UserName = handle;
                if (!string.IsNullOrEmpty(email))
                {
                    user.Email = email;
                }

                return true;
            }
        };
    }
}
```

`MemberExternalSignInAutoLinkOptions`'s constructor takes its parameters positionally — `(autoLinkExternalAccount, defaultIsApproved, defaultMemberTypeAlias, defaultCulture, defaultMemberGroups)` — so this call reads as: auto-link on (`true`), don't auto-approve via the built-in flag (`false`), create new members as `CommunityMember.ModelTypeAlias` (the [generated `ModelTypeAlias`](../../primers/content-modelling.md#never-hardcode-a-content-type-alias) for the `communityMember` document type, never a string literal), no explicit culture override (`null`, so Umbraco's global default applies), into the `"Community Members"` group.

Notice `defaultIsApproved` is `false` there, but `OnAutoLinking` sets `autoLinkUser.IsApproved = true` explicitly a few lines down — approval is forced through the callback rather than the constructor flag. Two callbacks, two different moments: `OnAutoLinking` fires exactly once, the first time a given GitHub identity signs in, and is where the member row actually gets created — hence forcing `IsApproved` here, so new community members aren't stuck behind manual backoffice approval. `OnExternalLogin` fires on **every** subsequent sign-in, refreshing name and handle each time in case they've changed on GitHub — but notice it only overwrites `Email` when the incoming claim is non-empty. Overwrite unconditionally and a sign-in where, for whatever reason, `OnCreatingTicket` didn't get a verified email back would silently blank out an email address that was captured correctly the first time.

Both callbacks read the same three claims — `ClaimTypes.Email` (Step 3), `ClaimTypes.Name` (GitHub's login/handle), and a GitHub-specific `"urn:github:name"` claim (the account's display name, which can be empty, hence the `?? handle` fallback) — which is why they're near-duplicates of each other; the split exists because Umbraco calls them at different points in the member lifecycle, not because the logic itself differs.

### Step 5 — Start the round trip from the header

Umbraco's member external-login flow is driven by its own built-in surface controller, `UmbExternalLoginController`, which this repo never has to touch directly — just point a form at it. In [`Menu.cshtml`](../../../src/UmbracoCommunity.Web.UI/Views/Shared/Components/Menu/Menu.cshtml):

```cshtml
@using (Html.BeginUmbracoForm("ExternalLogin", "UmbExternalLogin"))
{
    <input type="hidden" name="provider" value="@GitHubExternalLoginProviderOptions.SchemeName" />
    <input type="hidden" name="returnUrl" value="@returnUrl" />
    <button type="submit" class="btn is-white member-signin__btn">
        <partial name="~/Views/Partials/Icons/GitHub.cshtml" />
        Sign in
    </button>
}
```

`provider` is `GitHubExternalLoginProviderOptions.SchemeName` — the exact same `"UmbracoMembers.GitHub"` string used to register the scheme in Step 2, which is why that constant lives on the options class rather than being typed out twice. `returnUrl` is the current path plus querystring, captured just above this snippet, so the member lands back where they started once GitHub redirects home. Everything from here — redirecting to GitHub, handling the callback at `/signin-github`, calling `OnCreatingTicket` and then the auto-link callbacks — is Umbraco and the OAuth handler talking to each other; there's no custom callback controller in this repo.

### Step 6 — Read the signed-in state back in the header

The header needs to know, on every request, whether the current visitor is a signed-in member — [`MenuViewModelBuilder.cs`](../../../src/UmbracoCommunity.Web/ViewModelBuilders/Components/MenuViewModelBuilder.cs):

```csharp
var signInEnabled = siteSettings?.EnableMemberSignIn ?? false;
viewModel.IsSignInEnabled = signInEnabled;

if (signInEnabled && _memberManager.IsLoggedIn())
{
    viewModel.IsSignedIn = true;
    var user = _httpContextAccessor.HttpContext?.User;
    var handle = user?.Identity?.Name;
    viewModel.MemberDisplayName = user?.FindFirst(ClaimTypes.GivenName)?.Value ?? handle;
    viewModel.MemberAvatarUrl = handle != null ? $"https://github.com/{handle}.png" : null;
}
```

`EnableMemberSignIn` is a per-tenant boolean on the `Settings` node (surfaced through `GetSiteSettings()`, per the [multi-tenancy primer](../../primers/multi-tenancy.md)) — a whole tenant can opt out of member sign-in entirely, and `Menu.cshtml` checks `Model.IsSignInEnabled` before rendering *either* the sign-in button or the avatar, so a disabled tenant shows neither. Reading the identity here goes straight through `HttpContext.User` claims rather than an async `IMemberManager.GetCurrentMemberAsync()` call — cheaper for something rendered on every page — and the avatar URL is GitHub's own convention, `https://github.com/{handle}.png`, no upload or storage required.

### Step 7 — Sign out completely

[`LoginController.cs`](../../../src/UmbracoCommunity.Web/Controllers/LoginController.cs) is the other half:

```csharp
public class LoginController(IMemberSignInManager memberSignInManager) : Controller
{
    [HttpPost("/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await memberSignInManager.SignOutAsync();
        return Redirect("/");
    }
}
```

`IMemberSignInManager.SignOutAsync()` — not `HttpContext.SignOutAsync()` — is the one call that clears all four cookies Umbraco set across the sign-in (member, external login, 2FA, 2FA remember-me), for the reasons covered above. The header's sign-out control is a plain `<form method="post" action="/logout">` with `@Html.AntiForgeryToken()`, matching the `[ValidateAntiForgeryToken]` here — sign-out is a state change, so it's a POST with CSRF protection like any other, not a bare link.

## Alternatives we considered

- **A username/password member login.** This is what the site had before — a login/logout page backed by Umbraco's built-in member credentials. It's the more portable choice if your community doesn't have a shared identity provider, but it comes with the full password lifecycle (reset flows, hashing, breach exposure) for a community where GitHub was already a safe assumption. The old page was deleted outright in the same change that added GitHub sign-in, rather than kept as a fallback — one identity provider is simpler to reason about and support than two.
- **Hand-rolling the OAuth handler.** GitHub's OAuth flow is a fairly standard authorization-code exchange, and it would be possible to implement `OAuthHandler<GitHubAuthenticationOptions>`-equivalent code directly rather than depending on `AspNet.Security.OAuth.GitHub`. Not worth it: the package is small, actively maintained, and this is exactly the kind of well-trodden integration where a shared dependency saves you re-discovering edge cases — state validation, [PKCE](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps) (a defence against the authorization code being intercepted in transit), token refresh — other projects have already hit.
- **Registering GitHub as a backoffice external login instead.** Umbraco's Users and Members external-login systems are separate on purpose — the backoffice side is for editors, and reusing it for public community members would mean either giving every GitHub-authenticated visitor some flavour of backoffice access, or building a parallel permission system to prevent that. `AddMemberExternalLogins` exists specifically so you don't have to choose between those.

## Trade-offs and known limits

- **GitHub or nothing.** A visitor without a GitHub account — or one who doesn't want to link it — has no other way to become a member. That's the whole point for this community, but it's worth stating plainly: this isn't a "one of several providers" setup, it's the only one.
- **Auto-approval trusts GitHub's account creation, not this community.** `IsApproved = true` on first link means having *any* GitHub account is sufficient to become an approved member — there's no manual review step. That's a deliberate trade for low-friction sign-up; it also means the moderation burden shifts entirely to whatever a member *does* after joining, not who's allowed to join.
- **No automated test coverage.** Nothing in `tests/` exercises `RegisterGitHubAuth`, `GitHubExternalLoginProviderOptions`, `LoginController`, or the header's signed-in branch — the `OnCreatingTicket` HTTP call, the email-claim precedence between the two auto-link callbacks, and the four-cookie sign-out are all currently verified by hand, not by CI.
- **The avatar URL convention isn't the only one in this codebase.** The header builds it inline as `https://github.com/{handle}.png` (Step 6); the account page, which layers a separate community-profile feature on top of member auth, resolves it through a different path that can substitute a member-uploaded image. The two aren't wrong relative to each other — the header wants something fast and dependency-free on every render, the account page can afford to check for a custom upload — but if you go looking for "the" avatar-resolution code, know there are two.

## Where to go next

There's no natural next tutorial in this suite yet — this is the first one covering member auth rather than content or tenancy. If you're extending this pattern:

- The [content-modelling primer](../../primers/content-modelling.md) covers `ModelTypeAlias` and the compositions convention this tutorial leans on for `CommunityMember.ModelTypeAlias`.
- The [backend primer](../../primers/backend.md) covers composers in general — `RegisterGitHubAuth` is a small, self-contained example of the pattern.

Hopefully that's enough to add a second OAuth provider, or debug this one, without rediscovering the `AddOAuth`/`AddRemoteScheme` gotcha the hard way — welcome aboard!
