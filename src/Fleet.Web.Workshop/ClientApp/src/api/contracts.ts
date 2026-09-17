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
 * `type` and `status` arrive as numbers, not strings - the API serialises its enums by value.
 * A UI that prints `1` at the user is not finished; mapping those numbers to labels is part of
 * week 1, and deciding whether that mapping belongs on the client at all is worth an argument.
 */
export interface Vehicle {
  id: string;
  plate: string;
  type: number;
  status: number;
  odometerKm: number;
  depotId: string;
  depotName: string;
}
