---
applyTo: "frontend/src/**/*.ts,frontend/src/**/*.tsx"
description: React + TypeScript conventions for the Homebase frontend.
---
# Frontend (React 19, TypeScript 5.9, Vite 8, React Router 8)

- **Structure:** `src/app` (shell, routes) → `src/domains/<area>` (features) → `src/shared/{api,lib,ui}`.
  Import other areas only through `@/domains/<area>` and shared code only through its index. ESLint enforces this (ADR-0001).
- **Data:** call the backend only via `api` from `@/shared/api` (typed from the contract; no `fetch`, no axios).
  Load with `useQuery(key, fn)` and render with `<QueryView>` — it owns loading, error and empty states.
- **Types:** API shapes come from `@/shared/api` aliases of generated types. Never hand-write an API interface.
- **UI:** reuse `@/shared/ui` (`Button`, `Card`, `DataTable`, `Badge`, `StatCard`, `PageHeader`). Check `docs/CODEMAP.md` before creating a component.
- **Routing:** `import { Link, useParams } from 'react-router'`; `RouterProvider` comes from `react-router/dom`. There is no `react-router-dom` in v8.
- **Display only:** format with `formatDateTime`, `formatTime`, `formatRelative`. Never compute darkness, home/away or automation outcomes in the browser — render what the API says.
- **Time:** use `useNow()` for "now" in components; never call `Date.now()` / `new Date()` during render.
- **Accessibility:** every input has a `<label>`; icon-only buttons get `aria-label`; tables via `DataTable` (it adds a caption).
- **Styling:** use existing classes and CSS variables in `src/shared/ui/ui.css`; area-specific styles go in `<area>.css` next to `module.tsx`.

```tsx
// ✅ Preferred
const query = useQuery(`device:${id}`, () => api.devices.get(id));
return <QueryView query={query}>{(device) => <LightTile device={device} rooms={rooms} onChange={setDevice} />}</QueryView>;
// ❌ Avoid: useEffect + fetch('/api/devices/' + id) + hand-rolled spinner
```
