import { useState, type FormEvent } from 'react';
import { api, ApiError, type Device, type Room } from '@/shared/api';
import { Button } from '@/shared/ui';

const NO_ROOM = '';

/** Rename a light or move it to another room (or out of every room). The server validates. */
export function LightSettings({
  device,
  rooms,
  onSaved,
  onCancel,
}: {
  device: Device;
  rooms: Room[];
  onSaved: (d: Device) => void;
  onCancel: () => void;
}) {
  const [name, setName] = useState(device.name);
  const [roomId, setRoomId] = useState(device.roomId ?? NO_ROOM);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function save(e: FormEvent) {
    e.preventDefault();
    setSaving(true);
    setError(null);
    try {
      onSaved(await api.devices.update(device.id, { name, roomId: roomId === NO_ROOM ? null : roomId }));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Something went wrong.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <form className="stack tile__settings" aria-label={`Rename or move ${device.name}`} onSubmit={save}>
      <label className="field">
        Name
        <input value={name} maxLength={100} onChange={(e) => setName(e.target.value)} />
      </label>
      <label className="field">
        Room
        <select value={roomId} onChange={(e) => setRoomId(e.target.value)}>
          {rooms.map((r) => (
            <option key={r.id} value={r.id}>
              {r.name}
            </option>
          ))}
          <option value={NO_ROOM}>Not in a room</option>
        </select>
      </label>
      {error && (
        <p role="alert" className="tile__error">
          {error}
        </p>
      )}
      <div className="row">
        <Button type="submit" disabled={saving}>
          Save
        </Button>
        <Button variant="secondary" onClick={onCancel}>
          Cancel
        </Button>
      </div>
    </form>
  );
}
