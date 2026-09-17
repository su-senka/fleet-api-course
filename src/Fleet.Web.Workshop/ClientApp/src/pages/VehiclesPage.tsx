/**
 * Vehicles: the page you write.
 *
 * `GET /vehicles` is paged, sortable and filterable, which makes it the first page where the URL
 * and the UI have to agree with each other. See docs/react-client/week-1.md.
 */
export function VehiclesPage() {
  // TODO(week-1): render one page of vehicles from vehiclesApi.list().
  //
  // Handle the same three states DepotsPage does - pending, error, empty - and then the list.
  // Beyond that, three decisions that are the actual content of this page:
  //
  //  1. Where does the current page number live? React state is the obvious answer and the
  //     wrong one: a user who reloads, or shares the link, loses their place. The query string
  //     is the browser's own state container. react-router's useSearchParams reads and writes it.
  //
  //  2. What happens to the old rows while page 2 loads? A list that disappears and comes back
  //     flickers. React Query's placeholderData (keepPreviousData) keeps the previous page on
  //     screen while the next one is in flight - read what it does to `isPending` first.
  //
  //  3. `type` and `status` arrive as numbers (see api/contracts.ts). Print them as labels.
  //     Whether that mapping belongs here, in the client at all, or should have been strings on
  //     the wire is a question worth an opinion in your PR.
  return (
    <main>
      <h1>Vehicles</h1>
      <p>Not built yet.</p>
    </main>
  );
}
