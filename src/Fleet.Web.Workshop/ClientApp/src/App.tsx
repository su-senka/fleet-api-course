import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Outlet, RouterProvider, createBrowserRouter } from 'react-router';
import { RequireAuth } from './auth/RequireAuth';
import { TopBar } from './components/TopBar';
import { DepotsPage } from './pages/DepotsPage';
import { HomePage } from './pages/HomePage';
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

// TopBar renders NavLink, which needs router context - so it lives inside this layout route
// rather than beside <RouterProvider>, where that context does not reach it.
function Layout() {
  return (
    <>
      <TopBar />
      <Outlet />
    </>
  );
}

const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      {
        path: '/',
        element: (
          <RequireAuth>
            <HomePage />
          </RequireAuth>
        ),
      },
      {
        path: '/depots',
        element: (
          <RequireAuth>
            <DepotsPage />
          </RequireAuth>
        ),
      },
      {
        path: '/vehicles',
        element: (
          <RequireAuth>
            <VehiclesPage />
          </RequireAuth>
        ),
      },
    ],
  },
]);

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  );
}
