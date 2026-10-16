# Member Client Simulator — Page Override

This file overrides `../MASTER.md` only for the member machine simulator.

## Product direction

- Desktop WPF client displayed on a gaming station, optimized for 1366x768 and 1920x1080.
- Functional dark gaming interface; no 3D, WebGL, parallax, decorative video, or heavy animation.
- Reuse the CyberGame semantic status colors and typography available on Windows. Do not download fonts at runtime.
- Information priority: remaining usable balance, elapsed time, current usage charge, service charge, machine/session state.

## Tokens

| Role | Value |
|---|---|
| Background | `#0F172A` |
| Surface | `#172033` |
| Elevated surface | `#1E293B` |
| Primary | `#3B82F6` |
| Accent action | `#F97316` |
| Success | `#22C55E` |
| Warning | `#F59E0B` |
| Danger | `#EF4444` |
| Primary text | `#F8FAFC` |
| Secondary text | `#CBD5E1` |
| Border | `#334155` |

- 8px spacing grid; card radius 10px; input/button height at least 40px.
- Body text at least 14px; timer 36–48px; critical balance and charge figures at least 24px.
- Use Segoe UI and Segoe Fluent Icons where available. Text labels remain present for primary actions.

## Screens

### Login

- Fixed machine name is read from local configuration and shown read-only.
- Member code and PIN fields, explicit validation, visible keyboard focus, login button.
- Never expose machine selection, staff credentials, seed credentials, or API settings in the member UI.

### Active member session

- Header: member name/code, configured machine, connection state, sign-out/end-session action.
- Summary cards: wallet balance, elapsed time, usage charge, service charge, available balance.
- Main navigation: Session overview, Order food/drink, Top-up request.
- Server values are authoritative. A one-second UI timer may interpolate elapsed time only between server refreshes.
- Low balance warning uses icon + text + color; it must not rely on color alone.

### Order

- Only active products with positive stock.
- Product name, unit price, stock availability, quantity, note, cart total.
- Confirm modal lists items and total once. Prevent duplicate submit while the command is running.

### Top-up request

- Cash-at-counter request only: amount and optional note.
- Explain clearly that the wallet changes only after cashier approval.
- Display Pending, Approved, and Rejected states and refresh the real wallet after approval.

## Interaction and accessibility

- MVVM commands only; no business logic in code-behind.
- Use `DispatcherTimer` on the UI thread and cancellation tokens for HTTP refresh loops.
- Visible focus, logical tab order, Enter to submit forms, Escape to close dialogs.
- Loading, empty, error, disconnected, and success states must be explicit.
- Disable transactional controls during submission; every POST carries an idempotency key.
- No emojis as structural icons and no layout-shifting animations.
