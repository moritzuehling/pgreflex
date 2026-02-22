# pgreflex

pgreflex is a package that allows any trpc + postgres + drizzle stack to become reactive - any change on the database is immediately reflected in the frontend.


## How it works

It uses postgres' logical replication, which sends a copy of the write-ahead-log (WAL) via the pg_output plugin.

pgreflex's server listens to these changes and sends invalidations.

## Similar work

pgrelfex is inspired by the idea behind the [convex.dev](https://www.convex.dev/) project - the fundamental idea is very cool.

Unfortunately, for my projects, I'd love postgres interoperability - and so pgreflex was conceived. Comprehensive benchmarks will be provided soon - but in the end, pgreflex is about as performant as postgres.

However, pgreflex does require `REPLICA IDENTITY FULL` to capture updates correctly - for some tables, that may be slower.
