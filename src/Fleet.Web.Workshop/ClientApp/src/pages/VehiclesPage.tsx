import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'react-router';
import { vehiclesApi } from '../api/clients/vehicles';

// `type` and `status` arrive as numbers on the wire (see api/contracts.ts). The mapping lives
// here, next to the only page that renders them, rather than in the API client - nothing else
// in this app needs a label.
const typeLabels: Record<number, string> = {
  1: 'Car',
  2: 'Van',
  3: 'Truck',
  4: 'Bus',
};

const statusLabels: Record<number, string> = {
  1: 'Available',
  2: 'In maintenance',
  3: 'Retired',
};

const statusBadgeClass: Record<number, string> = {
  1: 'badge badge--available',
  2: 'badge badge--maintenance',
  3: 'badge badge--retired',
};

// The values the API's status filter accepts - the VehicleStatus enum's own names, not numbers.
const statusOptions: Array<{ value: string; label: string }> = [
  { value: 'Available', label: 'Available' },
  { value: 'InMaintenance', label: 'In maintenance' },
  { value: 'Retired', label: 'Retired' },
];

/**
 * Vehicles: the page you write.
 *
 * `GET /vehicles` is paged, sortable and filterable, which makes it the first page where the URL
 * and the UI have to agree with each other. See docs/react-client/week-1.md.
 */
export function VehiclesPage() {
  const [searchParams, setSearchParams] = useSearchParams();

  // The query string is the state container for page and status, not React state - a reload or a
  // shared link has to land the reader exactly where they were.
  const page = Number(searchParams.get('page') ?? '1');
  const status = searchParams.get('status') ?? '';

  const { data, isPending, isPlaceholderData, error } = useQuery({
    // Both page and status belong in the key - leave either out and a change to it would keep
    // serving a cached response for the wrong request.
    queryKey: ['vehicles', { page, status }],
    queryFn: () => vehiclesApi.list({ page, status: status || undefined }),
    // Keeps the previous page's rows on screen while the next page is in flight, instead of the
    // list flashing back to "Loading…" on every click.
    placeholderData: keepPreviousData,
  });

  function goToPage(nextPage: number) {
    const params = new URLSearchParams(searchParams);
    params.set('page', String(nextPage));
    setSearchParams(params);
  }

  function changeStatus(nextStatus: string) {
    const params = new URLSearchParams(searchParams);
    if (nextStatus) params.set('status', nextStatus);
    else params.delete('status');
    // A different filter makes the old page number meaningless - page 7 of a different filter is
    // not page 7 of anything - so it resets rather than carries over.
    params.delete('page');
    setSearchParams(params);
  }

  if (isPending) return <p className="state-message">Loading vehicles…</p>;

  // `error` is the ApiError from api/http.ts, so this message is the API's own `detail` string
  // rather than "Failed to fetch".
  if (error) {
    return (
      <p className="state-message" role="alert">
        Could not load vehicles: {error.message}
      </p>
    );
  }

  return (
    <main className="container page">
      <div className="page-header">
        <h1>Vehicles</h1>
      </div>

      <div className="filters">
        <label>
          Status
          <select
            className="select"
            value={status}
            onChange={(event) => changeStatus(event.target.value)}
          >
            <option value="">All</option>
            {statusOptions.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </label>
      </div>

      {data.items.length === 0 ? (
        <p className="state-message">No vehicles match this filter.</p>
      ) : (
        <div className="table-wrap">
          <table className="table" aria-busy={isPlaceholderData} style={{ opacity: isPlaceholderData ? 0.6 : 1 }}>
            <thead>
              <tr>
                <th>Plate</th>
                <th>Type</th>
                <th>Status</th>
                <th>Odometer (km)</th>
                <th>Depot</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((vehicle) => (
                <tr key={vehicle.id}>
                  <td>{vehicle.plate}</td>
                  <td>{typeLabels[vehicle.type] ?? vehicle.type}</td>
                  <td>
                    <span className={statusBadgeClass[vehicle.status] ?? 'badge'}>
                      {statusLabels[vehicle.status] ?? vehicle.status}
                    </span>
                  </td>
                  <td>{vehicle.odometerKm.toLocaleString()}</td>
                  <td>{vehicle.depotName}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <nav className="pager">
        <button
          type="button"
          className="button"
          disabled={!data.hasPreviousPage}
          onClick={() => goToPage(data.page - 1)}
        >
          Previous
        </button>
        <span>
          Page {data.page} of {data.totalPages}
        </span>
        <button
          type="button"
          className="button"
          disabled={!data.hasNextPage}
          onClick={() => goToPage(data.page + 1)}
        >
          Next
        </button>
      </nav>
    </main>
  );
}
