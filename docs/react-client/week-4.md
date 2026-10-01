# Week 4 - Validation: RFC 9457 in a form

<!--
  What is already true, and does not need restating here:
    Scaffold   src/Fleet.Web.Workshop/ (BFF host) and ClientApp/ (Vite + TypeScript + React Query)
    Leans on   API course week 5 (Validation and error responses), resource Drivers
    Fallback   Drivers have no reference implementation anywhere, by design. If your own API has
               not reached week 5, do this week against Vehicles at :5100 instead - its POST
               already answers 400/409 problem documents from FluentValidation.
    Scaffold   ApiError in ClientApp/src/api/http.ts already lifts `errors` out of the problem
               document into Record<string, string[]>; no consumer reads it yet.
    Scaffold   index.css has .field, .field__error and .form-error, and styles invalid inputs
               from aria-invalid rather than from a class - so the accessibility attribute and
               the red border cannot disagree.
    Verified   The `errors` keys arrive PascalCase - see "The detail that catches everyone".

  Not this week
    A form library, optimistic updates, file inputs.

  The scaffold builds, lints and runs before anyone touches it. The assignment is what is
  stubbed, and the decisions it forces.
-->

## Goal

Turn the API's own validation output into a form a person can actually fix.

Week 3 ended with a form that shows one message when anything goes wrong. That is honest but
useless: told "the request could not be accepted as sent", a user has four fields and no idea which
one offended. The API already said which - it sent an `errors` object naming each rejected field -
and `ApiError` has been carrying it this whole time with nobody reading it.

So the work is small and the decisions are not. The argument underneath this week is about
**authority**: who is entitled to decide a value is invalid, what the client is allowed to assume,
and what it must do when the server rejects something the form has no box for.

## Which API, and which resource

This week's resource is **Drivers**, and Drivers have no reference implementation anywhere - by
design, so nobody can copy a finished answer. That gives you two routes:

- **Your own API has reached week 5.** Point the BFF at it: in
  `src/Fleet.Web.Workshop/appsettings.json`, change
  `ReverseProxy:Clusters:fleetApi:Destinations:primary:Address` from `http://localhost:5100/` to
  `http://localhost:5101/`. One line. Then build the form over `POST /api/drivers`, whose body is
  `{ employeeNumber, name, userId }` and which answers `409` when the employee number is taken.
- **It has not.** Stay on `:5100` and do the week against `POST /api/vehicles`, which already
  answers exactly the problem documents this week is about. The form is the one you wrote in
  week 3, so the week becomes purely about the error path - which is the part being taught.

Say in your PR which you did. Both are fine; pretending is not.

## The detail that catches everyone

The `errors` keys are **PascalCase**. A real response from `POST /api/vehicles`:

```json
{
  "type": "https://fleet.example/problems/request.invalid",
  "title": "Invalid request",
  "status": 400,
  "detail": "The request could not be accepted as sent.",
  "instance": "POST /vehicles",
  "code": "request.invalid",
  "errors": {
    "Plate": ["A registration plate is required."],
    "OdometerKm": ["Mileage cannot be negative."]
  }
}
```

Your form fields are named `plate` and `odometerKm`, because that is what the request body uses.
So `errors[fieldName]` returns `undefined` for every field, the form renders no errors at all, and
nothing anywhere throws. It is a silent, total failure of the feature.

The cause is worth understanding rather than just patching. ASP.NET Core's web defaults camel-case
**property names**, and `errors` itself is camel-cased for exactly that reason. But the contents of
`errors` are a `Dictionary<string, string[]>`, and dictionary *keys* are data, not properties -
`JsonSerializerOptions.DictionaryKeyPolicy` is unset, so they are serialised verbatim. The keys
come from FluentValidation's `PropertyName`, which is the C# property: `Plate`, `OdometerKm`.

Do not fix this by renaming your form fields to `Plate`. Match the keys **case-insensitively** in
one helper, and treat the casing as the server's business rather than something your component
layout has to agree with.

## What to build

In `src/Fleet.Web.Workshop/ClientApp`:

- **One mapping helper**, in `src/api/` - not in the component. Signature roughly
  `fieldErrors(error: unknown, fields: readonly string[])`, returning the per-field messages it
  could place and the messages it could not. It owns four decisions:
  1. Is this even an `ApiError` with a `400` and an `errors` object? A `409`, a `500` or a network
     failure has no field errors and must not be silently dropped.
  2. Case-insensitive key matching, per above.
  3. A key naming a field the form **does** render → that field's message.
  4. A key naming anything else → the leftovers, which the form shows at form level.

  Point 4 is the one people skip, and skipping it is how a user gets a form that refuses to submit
  and says nothing. The server is allowed to reject a field your form does not show; `userId` on
  `POST /drivers` is a live example. If you cannot place a message, you must still display it.

- **Per-field display**, wired for accessibility and not only for looks: `aria-invalid` on the
  input and `aria-describedby` pointing at the element holding the message. The CSS already keys
  off `aria-invalid`, so getting this right styles itself and getting it wrong looks wrong. A red
  border that a screen reader cannot see is not a validation message.

  A field with several messages shows them all - `Plate` can fail `NotEmpty` and `MaximumLength`
  in one response, which is why the value is `string[]` and not `string`.

- **A form-level message**, for the unplaceable keys and for the failures that were never
  field-shaped at all.

- **Clear the errors when the user edits.** Stale red next to a field someone has already fixed
  trains them to ignore it.

- **At most one piece of client-side validation**, chosen deliberately and defended in the PR. One
  is the assignment; do not validate all four fields.

- **A `409` that is not treated as a field error.** On `POST /api/vehicles` a duplicate plate is a
  `409` with no `errors` object, and it is tempting to pin it on the plate field - it *is* about
  the plate, after all. Resist, or at least argue for it: `400` means "this request is malformed",
  which the client could have known; `409` means "this request is fine and the world disagrees with
  it", which it could not. Collapsing the two teaches the user that a red field sometimes means
  "try again" and sometimes means "this is impossible".

**Do not add:** a form library, `zod` or any schema validator, optimistic updates, or file inputs.
React Hook Form and TanStack Form are both reasonable choices in a real codebase and both would
hide the thing being taught this week.

### The argument worth having: what is client-side validation for?

Every rule you check in the browser already exists on the server, in `RegisterVehicleRequestValidator`.
So the client's copy is a second implementation of a rule it does not own, and it will drift - the
server will tighten `MaximumLength(16)` to `12` and your form will cheerfully submit a 15-character
plate that now bounces.

That is a real cost, and it does not make client-side validation worthless. It makes it a
*latency and courtesy* feature rather than a correctness one. Marking a required field before a
round trip is kind. Deciding a plate is acceptable is not the client's call, and the moment you
write a regex that duplicates a server rule you have taken on a maintenance obligation nobody
asked you to.

The test to apply: if this check disagreed with the server tomorrow, would the user be stuck or
merely mildly inconvenienced? "Required" fails safe - the server agrees it is required. A format
rule does not.

Note also what the API deliberately does *not* validate in its validator, and read the comment in
`src/Fleet.Api/Validation/VehicleRequestValidators.cs` that says why: it never checks that the
plate is unique or that the depot exists, because those are questions about the state of the
system and only the module can answer them. The same line separates your form from the API. You
validate what is answerable from the form alone.

## What to hand in

A pull request from your own repository (created from the template), containing:

- The mapping helper, per-field display with `aria-invalid`/`aria-describedby`, the form-level
  fallback, error clearing on edit, and one deliberate client-side check.
- A short PR description answering four questions. First: which API and resource you targeted.
  Second: how you matched the `errors` keys to your fields, and what you saw before you handled the
  casing. Third: which single client-side rule you kept and why that one is safe to duplicate.
  Fourth: how you present a `409`, and your argument for it.
- Evidence it works: show at least five things - an empty submit with messages on the right
  fields; one field carrying two messages at once; a `409` duplicate presented as whatever you
  argued for; a rejection whose key matches no field surfacing at form level rather than
  disappearing; and the DOM or an accessibility inspector showing `aria-invalid` and
  `aria-describedby` on an invalid input.

To produce the unplaceable-key case without waiting for the server to misbehave, remove a field
from the submitted body while leaving it out of the list your helper knows about, or point at
`POST /api/drivers` and let `userId` do it for you.

**Reviewer checklist:**
- [ ] `npm run build` and `npm run lint` both pass
- [ ] The `errors` key matching is case-insensitive, and lives in one helper under `src/api/`
- [ ] The helper is not fooled by a `409`, a `500` or a network error
- [ ] An `errors` key matching no rendered field is displayed, not swallowed
- [ ] A field with two messages shows both
- [ ] Invalid inputs carry `aria-invalid` and `aria-describedby`
- [ ] Editing a field clears its stale message
- [ ] Exactly one client-side rule, and the PR defends it as safe to duplicate
- [ ] No form library and no schema validator
- [ ] The PR states which API (`:5100` or `:5101`) and which resource was used
