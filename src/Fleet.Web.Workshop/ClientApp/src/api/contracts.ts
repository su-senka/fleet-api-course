/**
 * The wire shapes this client consumes.
 *
 * Hand-written on purpose for now: writing them once makes you read the API's actual responses
 * instead of guessing at them. They are the client's own view of the contract, not a copy of the
 * server's DTOs - a field the UI never renders does not have to appear here.
 *
 * In a production codebase this file is usually generated from the OpenAPI document the API
 * already publishes at http://localhost:5101/openapi/v1.json. Generating it is the right answer
 * and also the one that teaches you least on day one, so: by hand now, generated later.
 */

/**
 * The envelope every collection endpoint answers with.
 *
 * Note `totalPages`, `hasPreviousPage` and `hasNextPage`: the API computes them server-side and
 * sends them, so a pager does not have to derive them - and cannot disagree with the server about
 * where the last page is.
 */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface Depot {
  id: string;
  name: string;
  city: string;
}

/**
 * The vehicle enums, as names rather than numbers.
 *
 * Both hosts register `JsonStringEnumConverter`, so `type` and `status` arrive as `"Van"` and
 * `"InMaintenance"` - the enum's own names - from your API on :5101 and from the reference API on
 * :5100 alike. Writing them as unions rather than `string` is the point: a typo in a filter value
 * is then a compile error instead of a request that quietly returns nothing.
 *
 * These are still hand-written, which means nothing tells them when the server adds a fourth
 * status. Week 5 replaces them with types generated from the OpenAPI document.
 */
export type VehicleType = 'Car' | 'Van' | 'Truck' | 'Bus';

export type VehicleStatus = 'Available' | 'InMaintenance' | 'Retired';

/**
 * A vehicle as it appears in a list.
 *
 * Note that the names are not labels. `"InMaintenance"` is a value the API understands and will
 * accept back as a query-string filter; "In maintenance" is prose for a human. Turning one into
 * the other is presentation, and belongs in the page rather than here.
 */
export interface Vehicle {
  id: string;
  plate: string;
  type: VehicleType;
  status: VehicleStatus;
  odometerKm: number;
  depotId: string;
  depotName: string;
}
