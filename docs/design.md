# UI conventions

The client uses Angular standalone components, PrimeNG 22, PrimeIcons and Tailwind CSS. PrimeNG is governed by [PrimeUI terms](licensing.md).

- Keep clinical and administrative screens legible, with clear labels, validation errors, loading states and empty states.
- Show server-confirmed financial/stock state after writes. A failed request must not imply a payment or dispense succeeded.
- Disable duplicate submissions while a request is pending and preserve useful error feedback.
- Do not rely on route visibility for authorization; enforce access on the API.
- Treat asynchronous context changes carefully: older responses must not replace a newer patient, schedule or account context.
- Mark static interface illustrations as fictional. Never put real patient details in design examples or screenshots.
- Use reserved example email domains and non-contactable input hints. Do not prefill login credentials or attribute clinical entries to an arbitrary real-looking person.

These conventions guide implementation; they are not a claim of a completed accessibility certification or clinical usability study. See the [screenshot guide](screenshots/README.md) for safe portfolio captures.
