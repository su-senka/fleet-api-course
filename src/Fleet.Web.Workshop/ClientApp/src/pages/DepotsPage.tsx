import { useQuery } from '@tanstack/react-query';
import { depotsApi } from '../api/clients/vehicles';

/**
 * The worked example: the smallest honest page in the application.
 *
 * Three states, all of them rendered. A page that renders only the happy path is not finished,
 * it just has not failed yet - and `GET /depots` will fail the moment the API is not running,
 * which is often.
 */
export function DepotsPage() {
  const { data, isPending, error } = useQuery({
    // The key is the cache identity. Anything the request varies by belongs in it, or two
    // different requests will quietly share one cache entry.
    queryKey: ['depots'],
    queryFn: depotsApi.list,
  });

  if (isPending) return <p>Loading depots…</p>;

  // `error` is the ApiError from api/http.ts, so this message is the API's own `detail` string
  // rather than "Failed to fetch".
  if (error) return <p role="alert">Could not load depots: {error.message}</p>;

  if (data.length === 0) return <p>No depots.</p>;

  return (
    <main>
      <h1>Depots</h1>
      <ul>
        {data.map((depot) => (
          <li key={depot.id}>
            {depot.name} — {depot.city}
          </li>
        ))}
      </ul>
    </main>
  );
}
