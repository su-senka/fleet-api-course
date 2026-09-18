import { Link } from 'react-router';
import { useUser } from '../auth/useUser';

/**
 * The landing page: a welcome and two doors, nothing this session doesn't already know.
 */
export function HomePage() {
  const { data: user } = useUser();

  return (
    <main className="container page">
      <section className="home__hero">
        <h1>Welcome{user?.name ? `, ${user.name}` : ''}.</h1>
        <p>Your fleet, in one place - depots and the vehicles based at them.</p>
      </section>

      <section className="home__grid">
        <Link to="/depots" className="card home__tile">
          <h2>Depots</h2>
          <p>Where the fleet is based.</p>
        </Link>
        <Link to="/vehicles" className="card home__tile">
          <h2>Vehicles</h2>
          <p>Search, filter and page through every vehicle.</p>
        </Link>
      </section>
    </main>
  );
}
