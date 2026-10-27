import { useState, type FormEvent } from 'react';
import { api, ApiError, type Device } from '@/shared/api';
import { Badge, Button, Card } from '@/shared/ui';

/** Two steps: ask the TV to show a code, then send that code back. Errors are the server's. */
export function TvPairing({ tv, onPaired }: { tv: Device | null; onPaired: () => void }) {
  return (
    <div id="pairing">
      <Card title="Pair the TV" actions={tv && !tv.needsPairing ? <Badge tone="success">Paired</Badge> : undefined}>
        {!tv ? (
          <p className="muted">No TV is set up on the Mac.</p>
        ) : tv.needsPairing ? (
          <PairingSteps tv={tv} onPaired={onPaired} />
        ) : (
          <p>
            {tv.name} is paired{tv.online ? '.' : ", but Homebase can't reach it right now."}
          </p>
        )}
      </Card>
    </div>
  );
}

function PairingSteps({ tv, onPaired }: { tv: Device; onPaired: () => void }) {
  const [address, setAddress] = useState('');
  const [prompt, setPrompt] = useState<string | null>(null);
  const [code, setCode] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function attempt(step: () => Promise<void>) {
    setBusy(true);
    setError(null);
    try {
      await step();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Something went wrong.');
    } finally {
      setBusy(false);
    }
  }

  const start = (e: FormEvent) => {
    e.preventDefault();
    void attempt(async () => {
      const pairing = await api.devices.startPairing(tv.id, { address: address.trim() || null });
      setPrompt(pairing.message);
    });
  };

  const confirm = (e: FormEvent) => {
    e.preventDefault();
    void attempt(async () => {
      await api.devices.confirmPairing(tv.id, code.trim());
      onPaired();
    });
  };

  return (
    <div className="stack">
      <p className="muted">
        {tv.name} isn’t paired yet, so Homebase can’t tell when it turns on or off. Turn the TV on, then ask it to show a pairing
        code.
      </p>
      <form className="row pairing__form" aria-label="Show a pairing code" onSubmit={start}>
        <label className="field">
          TV address (optional — only if the Mac doesn’t know it yet)
          <input value={address} placeholder="192.168.1.40" autoComplete="off" onChange={(e) => setAddress(e.target.value)} />
        </label>
        <Button type="submit" variant={prompt ? 'secondary' : 'primary'} disabled={busy}>
          {prompt ? 'Show a new code' : 'Show code on TV'}
        </Button>
      </form>
      {prompt && (
        <>
          <p role="status">{prompt}</p>
          <form className="row pairing__form" aria-label="Enter the pairing code" onSubmit={confirm}>
            <label className="field">
              Code shown on the TV
              <input value={code} maxLength={6} autoComplete="off" spellCheck={false} onChange={(e) => setCode(e.target.value)} />
            </label>
            <Button type="submit" disabled={busy}>
              Pair
            </Button>
          </form>
        </>
      )}
      {error && (
        <p role="alert" className="presence__error">
          {error}
        </p>
      )}
    </div>
  );
}
