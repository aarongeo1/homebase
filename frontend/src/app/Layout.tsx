import { NavLink, Outlet } from 'react-router';
import { apiMode } from '@/shared/api';
import { domainModules } from './domain-registry';

export function Layout() {
  return (
    <div className="shell">
      <aside className="shell__nav">
        <NavLink to="/" className="brand" end>
          Homebase
        </NavLink>
        <nav aria-label="Areas">
          {domainModules.map((m) => (
            <div key={m.id} className="nav-group">
              <div className="nav-group__title">{m.title}</div>
              <div className="nav-group__audience">{m.audience}</div>
              {m.nav.map((item) => (
                <NavLink key={item.to} to={item.to} end className="nav-link">
                  {item.label}
                </NavLink>
              ))}
            </div>
          ))}
        </nav>
        <div className="api-mode" title="Set VITE_API_MODE=live to use the .NET backend">
          API: {apiMode}
        </div>
      </aside>
      <main className="shell__main">
        <Outlet />
      </main>
    </div>
  );
}
