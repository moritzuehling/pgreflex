# pgreflex

pgreflex is a package that allows any trpc + postgres + drizzle stack to become reactive - any change on the database is immediately reflected in the frontend.


## How it works

It uses postgres' logical replication, which sends a copy of the write-ahead-log (WAL) via the pg_output plugin.

pgreflex's server listens to these changes and sends invalidations.

## Similar work

pgrelfex is inspired by the idea behind the [convex.dev](https://www.convex.dev/) project - the fundamental idea is very cool.

Unfortunately, for my projects, I'd love postgres interoperability - and so pgreflex was conceived. Comprehensive benchmarks will be provided soon - but in the end, pgreflex is about as performant as postgres.

## Performance and Scale

Pgreflex does require `REPLICA IDENTITY FULL` to capture updates correctly - for some tables, that may be slower that raw postgres. Otherwise, it adds (essentially) the network latency between the application server and the pgreflex server. If they're running in the same datacenter, that should be neglible - the aim is less than 1ms of processing time overhead.

If a query is invalidated, the whole procedure is re-run. This is similar to calling `.invalidate()` on `react-query` in the frontend, so no additional load is expect - except in cases where you previously forgot to invalidate.

In the end, the dependencies are more granular thant the frontend would know - so it might ultimately reduce the load on the application server. Either way, it's a light plugin.s

## Integrations

I'm aiming to make this plug + play with neondb soon - you just point the server at your neon instance, and it works automatically.
