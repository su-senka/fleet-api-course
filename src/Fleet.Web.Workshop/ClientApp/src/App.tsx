import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { RouterProvider, createBrowserRouter } from 'react-router';
import { DepotsPage } from './pages/DepotsPage';
import { VehiclesPage } from './pages/VehiclesPage';

/**
 * One QueryClient for the application, created once at module scope.
 *
 * Creating it inside the component would hand every render a brand-new cache, which looks like
 * "React Query does not cache anything" and wastes an afternoon.
 */
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Defaults worth setting deliberately rather than inheriting:
      //   retry              - one retry hides a blip; three hide a bug and delay the error.
      //   refetchOnWindowFocus - excellent for a dashboard, startling for a form.
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});

const router = createBrowserRouter([
  { path: '/', element: <DepotsPage /> },
  { path: '/vehicles', element: <VehiclesPage /> },
  // TODO(week-2): wrap the routes that need a session in <RequireAuth>, and add the sign-in page.
]);

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  );
}
