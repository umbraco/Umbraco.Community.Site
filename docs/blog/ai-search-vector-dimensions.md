# How a bot storm + one missing number took down our AI search (and what we learned about SQL Server vector dimensions)

We run [Umbraco.AI.Search](https://docs.umbraco.com) on the Umbraco Community site to power semantic "did you mean…" suggestions on our 404 page. It worked great — right up until an afternoon when the site started throwing 500s for about 90 minutes.

The post-mortem turned into a genuinely useful lesson about embedding models and SQL Server's native vector type, so we're sharing it.

## What we saw

The logs told a clear story once we lined them up:

- `System.OutOfMemoryException` — hundreds of them, deep inside `Microsoft.Data.SqlClient` reading rows.
- `Timeout expired. The timeout period elapsed prior to obtaining a connection from the pool.`
- `Resource ID : 1. The request limit for the database is 90 and has been reached.`
- A flood of 500s from the ASP.NET exception handler, all clustered in the same 90-minute window.

And the trigger? A **vulnerability-scanner bot storm** — thousands of requests for junk URLs like `/wp-login.php`, `/jmx-console/`, and `/_ignition/execute-solution`. Every one of those is a 404, every 404 renders our suggestion component, and every suggestion ran an AI vector search.

But scanners hit 404s all the time. Why did *this* fall over? Because of a warning we'd been ignoring for days.

## The warning we should have read

Buried at **Warning** level (not Error), repeating quietly since long before the outage:

> Vector search is using **brute-force** because the embedding model produces **3072 dimensions**, which exceeds the SQL Server vector type maximum of **1998**.

That one line is the whole story.

## Embeddings, dimensions, and the 1998 ceiling

A quick primer, because the model naming makes this more confusing than it should be.

When you index content for semantic search, each page is converted into an **embedding** — a list of floating-point numbers that encodes its *meaning* as a point in high-dimensional space. Search ranks results by measuring distance between those points. "Dimensions" is just how many numbers are in the list. More dimensions = more nuance captured, but more bytes to store and more math per comparison.

OpenAI's current embedding models come in exactly two sizes — there's no "medium":

| Model | Default dimensions |
|---|---|
| `text-embedding-3-small` | 1536 |
| `text-embedding-3-large` | **3072** |

And here's the constraint that bit us: **SQL Server's native `VECTOR` column type maxes out at 1998 dimensions.** That oddly specific number is a storage limit — each dimension is a 4-byte float, and 1998 × 4 ≈ 8 KB, roughly the row budget SQL Server allows for that type.

We were using `text-embedding-3-large` at its **default 3072**. That's larger than 1998, so the vectors don't fit the native column. When that happens, the library doesn't crash — it **silently falls back to brute-force**: it pulls *every stored vector* out of the database into application memory and computes all the distances in C#.

One brute-force search is fine. Hundreds of concurrent brute-force searches — courtesy of the bot storm — meant loading the entire vector store into memory over and over, exhausting RAM and holding SQL connections until the database hit its connection limit. Cascade complete.

## The fix: shorten the vector, not the model

The instinct is "the large model is too big, switch to a smaller one." But that's not quite right — and the real fix is more elegant.

The `text-embedding-3` models are trained with **Matryoshka representation learning**: the most important information is packed into the *front* of the vector. That means you can ask the model for a **shorter vector** and truncate the tail with **minimal quality loss**. It's the same strong model, just emitting a deliberately shortened — but still coherent — embedding.

So instead of downgrading to `small`, we kept `text-embedding-3-large` and set its **Dimensions to 1998** (the largest size that still fits SQL Server's native column). In Umbraco's backoffice that lives under **AI → Profiles → [your profile] → Settings → Dimensions**. The field hint reads *"Leave empty to use the model's default"* — and that default (3072) is exactly the trap.

| Config | Fits native vector search? | Quality |
|---|---|---|
| `large` @ **1998** | ✅ | Best of the options that fit |
| `small` @ 1536 | ✅ | Good, cheaper |
| `large` @ 3072 (default / empty) | ❌ brute-force | Highest in theory, broke prod |

Two gotchas worth knowing:

- **You must re-index after changing it.** A vector's dimensionality is fixed at index time — you can't compare 1998-dim queries against 3072-dim stored vectors. Change the setting, then rebuild the index.
- **Cost is unchanged.** OpenAI bills per *input token*, not per output dimension. Shrinking dimensions saves storage and speeds up comparisons, but doesn't lower your API bill.

## The other half: don't let a 404 be an attack surface

Fixing the dimensions stops the brute-force. But we didn't want a *future* misconfiguration (or any other slow search path) to turn a 404 flood into an outage again. So we hardened the suggestion service itself:

1. **Gate obvious junk.** Scanner probes for `.php`, `.env`, `.json`, etc. never reach the search — real Umbraco content URLs are extensionless. (We kept `.zip` allowed, because `/seed/latest.zip` is a real endpoint for us — know your own routes!)
2. **Cap concurrency.** A process-wide semaphore limits how many searches run at once, so a burst sheds load instead of fanning out into hundreds of simultaneous queries.
3. **Cache by outcome.** Hits cache for a day; transient failures cache for only a minute — previously a failure was cached as "no results" for a *full day*, which quietly suppressed suggestions.
4. **Retry transient DB faults.** The 404-tracking writer shares the same Azure SQL connection, so we enabled EF Core's `EnableRetryOnFailure` to ride out connection-limit blips.

## Takeaways

- **A silent fallback is a landmine.** "Brute-force" sounded harmless in a Warning log; it was the root cause. Treat performance-cliff warnings as errors-in-waiting.
- **Know your platform's limits.** SQL Server native vectors cap at 1998 dimensions. If your embedding model's default exceeds that, you're one traffic spike away from this exact failure.
- **You can shrink embeddings almost for free.** Matryoshka models let you trade a sliver of accuracy for a vector that fits your storage — use it.
- **Anything reachable by an anonymous request is an attack surface**, including your 404 page. Gate it, cap it, cache it.

If your AI search is mysteriously slow or memory-hungry on SQL Server, check your embedding profile's Dimensions against that 1998 ceiling *first*. It might just be one empty field.
