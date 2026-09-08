# HTTP request files

Runnable from Visual Studio, Rider, and the VS Code REST Client extension. They cover the two
implemented resources - Vehicles and Bookings - and nothing else, because nothing else has an HTTP
surface yet.

Start here:

1. `auth.http` - get a token. Everything else needs one.
2. `vehicles.http` - the gentler resource: paging, filtering, sorting, caching.
3. `bookings.http` - ETags, `If-Match`, `Idempotency-Key`, and a driver who can only see their own.

Each file begins with the variables it needs. Run the requests in order the first time: several of
them capture an id or an ETag from the response before it and reuse it.

Writing the equivalent file for a resource you have just implemented is the cheapest way to check
your own work before reaching for a test.
