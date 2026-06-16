# Lessons Learned

## Updating Umbraco Versions

As we never sync changes back from Cloud with our set-up (so we can go open source), we have to manually do all version upgrades in our codebase.

To make PR reviews easier, ideally this upgrade would be in a standalone PR, ie not mixed in with doing other changes.

You can use the upgrade tool to do a full upgrade of everything, including regenerating the Umbraco Deploy `.uda` files, see [tools/upgrade-umbraco/README.md](../tools/upgrade-umbraco/README.md) for full documentation.

**Note:** It would be good to get those changes out of the way in a commit where no *actual* schema changes are being done so the only updates in the `.uda` files are version number changes.

## Making Urgent Changes Directly

Sometimes we need to make changes to Staging or Production backoffices directly to fix an urgent issue. This will cause a commit to the associated .uda file in the appropriate Cloud repository. Note this *may* not get overwritten in future deployments as Cloud uses git merge to import changes: it does not do a complete overwrite. 

If you have to do manual changes like this, the safest thing would be to create an issue with a screenshot of the change you had to make. This can then either be replicated in the codebase and deployed properly through the pipeline in the next release, or the change manually reset via the Staging/Production backoffice directly if no longer required.

If there's an open issue about a manual change having been made then it will help someone having restore/deploy issues to understand why they are getting schema mismatch errors!

## Deleting "Schema" Items

If you delete a document type or data type etc locally, there will be a corresponding commit of the removal of the `.uda` file. This should get reflected in the cloud codebases during the merge and so the item will be deleted in those environments too.

If you delete something that you later realise you shouldn't have deleted (and this deletion hasn't yet been deployed through the pipeline), then you should restore the previously deleted .uda file and use the [Update Schema from Disk Files] option to recreate it in your local backoffice. If you add it manually to your backoffice as a new entity, it will have a different key from other environments that will cause deployment issues.

## Adding secrets

In order to add a secret that is being read from the appsettings.json file, add it in the Cloud Portal. This is using the default .NET secrets naming convention, [as documented](https://docs.umbraco.com/umbraco-cloud/begin-your-cloud-journey/project-features/secrets-management).

## Package-Owned Database Migrations

The Block Restrictions package (`UmbracoCommunity.BlockRestrictions`) owns its own EF Core `DbContext` and migrations rather than making the host project wire them up. A composer registers a hosted service (`BlockRestrictionMigrationHostedService`) that applies the migrations on startup.

The footgun: those migrations must run *after* Umbraco has finished booting, not during composition — Umbraco's database and connection setup isn't ready earlier, so running migrations too soon fails. Registering the migration step as a hosted service (which starts once the app is up) rather than in the composer's `Compose` method is what gets the ordering right. See PR #132 on `develop` for the fix and the constraint that drove it.

If you add another package that owns its own schema, follow the same pattern: `DbContext` + migrations inside the Razor Class Library, applied via a hosted service, so the host project consumes the package without wiring anything up.