import { Link } from 'react-router';
import { PageHeader } from '@/shared/ui';
import { domainModules } from './domain-registry';

export function HomePage() {
  return (
    <>
      <PageHeader
        title="Homebase"
        subtitle="Your flat, run from your Mac: lights that follow you home, out and to bed."
      />
      <div className="grid grid--cards">
        {domainModules.map((m) => (
          <Link key={m.id} to={`/${m.basePath}`} className="card home-card">
            <h2 className="card__title">{m.title}</h2>
            <p className="muted">{m.description}</p>
            <dl>
              <dt>Serves</dt>
              <dd>{m.audience}</dd>
            </dl>
          </Link>
        ))}
      </div>
    </>
  );
}
