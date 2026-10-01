import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'react-router';
import { vehiclesApi } from '../api/clients/vehicles';
import type { VehicleStatus } from '../api/contracts';

/*
  `type` and `status` arrive as the enum's own names (see api/contracts.ts), so most of what a
  label map used to do is already done. `type` needs no map at all - "Van" is what you would have
  written anyway - and rendering it directly is one fewer thing to keep in step with the server.

  `status` still needs one, because "InMaintenance" is a value and "In maintenance" is prose. That
  is the honest reason a mapping exists here: not translation, typography. It lives next to the
  only page that renders it rather than in the API client, because nothing else needs a label.
*/
const statusLabels: Record<VehicleStatus, string> = {
  Available: 'Available',
  InMaintenance: 'In maintenance',
  Retired: 'Retired',
};

const statusBadgeClass: Record<VehicleStatus, string> = {
  Available: 'badge badge--available',
  InMaintenance: 'badge badge--maintenance',
  Retired: 'badge badge--retired',
};

// The filter dropdown's options are the wire values themselves, paired with the labels above -
// so the <option value> is exactly what `GET /vehicles?status=` expects, and there is no third
// place for the two to disagree.
const statusOptions = Object.keys(statusLabels) as VehicleStatus[];

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
              <option key={option} value={option}>
                {statusLabels[option]}
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
                  <td>{vehicle.type}</td>
                  <td>
                    <span className={statusBadgeClass[vehicle.status]}>
                      {statusLabels[vehicle.status]}
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
