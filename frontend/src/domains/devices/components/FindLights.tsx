import { useState } from 'react';
import { api, ApiError, type Discovery } from '@/shared/api';
import { Button, Card } from '@/shared/ui';

/** Asks the server to look for Govee lights; new ones arrive without a room. */
export function FindLights({ onFound }: { onFound: () => void }) {
  const [searching, setSearching] = useState(false);
  const [result, setResult] = useState<Discovery | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function search() {
    setSearching(true);
    setError(null);
    try {
      const discovery = await api.devices.discover();
      setResult(discovery);
      if (discovery.added.length > 0 || discovery.updated.length > 0) onFound();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Something went wrong.');
    } finally {
      setSearching(false);
    }
  }

  const names = (devices: Discovery['added']) => devices.map((d) => d.name).join(', ');

  return (
    <Card
      title="Find lights"
      actions={
        <Button onClick={search} disabled={searching}>
          {searching ? 'Searching…' : 'Find lights'}
        </Button>
      }
    >
      <p className="muted">
        Searches your local network, then the Govee cloud if a Govee API key is set on the Mac. New lights show up under “Not in a
        room”.
      </p>
      {error && (
        <p role="alert" className="tile__error">
          {error}
        </p>
      )}
      {result && (
        <div role="status" className="discovery">
          <p>{result.added.length > 0 ? `Added: ${names(result.added)}.` : 'No new lights added.'}</p>
          {result.updated.length > 0 && <p>Updated: {names(result.updated)}.</p>}
          {result.notes.length > 0 && (
            <ul className="muted">
              {result.notes.map((note) => (
                <li key={note}>{note}</li>
              ))}
            </ul>
          )}
        </div>
      )}
    </Card>
  );
}
