import { http } from '../http';
import type { Depot, PagedResult, Vehicle } from '../contracts';

/**
 * One module per resource, mirroring the API's own grouping.
 *
 * These functions know about URLs and nothing else - no React, no caching, no loading state.
 * That separation is what makes them trivial to test and reusable from anywhere; React Query
 * sits on top and owns the caching.
 *
 * Every path is relative. In development Vite proxies /api to the BFF; in production the BFF
 * serves this bundle itself. An absolute URL would work in exactly one of those.
 */

const base = '/api/vehicles';

export const vehiclesApi = {
  list: (params: { page?: number; pageSize?: number; sort?: string; status?: string } = {}) => {
    const query = new URLSearchParams();

    // Skip empty values rather than sending `?status=`, which the API would have to interpret.
    for (const [key, value] of Object.entries(params)) {
      if (value !== undefined && value !== '') query.set(key, String(value));
    }

    const suffix = query.size > 0 ? `?${query}` : '';
    return http.get<PagedResult<Vehicle>>(`${base}${suffix}`);
  },

  getById: (vehicleId: string) => http.get<Vehicle>(`${base}/${vehicleId}`),
};

export const depotsApi = {
  list: () => http.get<Depot[]>('/api/depots'),
};
