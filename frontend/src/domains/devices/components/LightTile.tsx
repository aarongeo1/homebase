import { useState } from 'react';
import { api, ApiError, type Device, type DeviceCommand, type Room } from '@/shared/api';
import { Badge, Button } from '@/shared/ui';
import { describeLight, DRIVER_LABEL, offlineReason, WARMTH_MAX_K, WARMTH_MIN_K } from '../model/presentation';
import { LightSettings } from './LightSettings';

export function LightTile({ device, rooms, onChange }: { device: Device; rooms: Room[]; onChange: (d: Device) => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState(false);
  // Where the sliders start when the light doesn't report a value.
  const startBrightness = device.state.brightness ?? 100;
  const startWarmth = device.state.colorTempK ?? 2700;
  const [brightness, setBrightness] = useState(startBrightness);
  const [warmth, setWarmth] = useState(startWarmth);
  const disabled = !device.online || busy;

  async function send(command: DeviceCommand) {
    setBusy(true);
    setError(null);
    try {
      onChange(await api.devices.command(device.id, command));
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Something went wrong.');
    } finally {
      setBusy(false);
    }
  }

  // Sliders send once they are let go (pointer or key), and only when they were actually moved —
  // tabbing onto a slider also fires keyup.
  function commitBrightness() {
    if (brightness !== startBrightness) void send({ brightness });
  }
  function commitWarmth() {
    if (warmth !== startWarmth) void send({ colorTempK: warmth });
  }

  return (
    <article className={`card tile${device.state.on ? ' tile--active' : ''}`} aria-label={device.name}>
      <header className="tile__header">
        <span className="tile__icon" aria-hidden="true">
          💡
        </span>
        <div>
          <h3 className="tile__name">{device.name}</h3>
          <div className="muted tile__state">{describeLight(device)}</div>
        </div>
      </header>
      <div className="row tile__badges">
        {!device.online && <Badge tone="danger">Offline</Badge>}
        <Badge tone="info">{DRIVER_LABEL[device.driver]}</Badge>
      </div>
      {!device.online && <p className="muted tile__reason">{offlineReason(device)}</p>}

      <Button variant={device.state.on ? 'secondary' : 'primary'} disabled={disabled} onClick={() => send({ on: !device.state.on })}>
        Turn {device.state.on ? 'off' : 'on'}
      </Button>
      <label className="field">
        Brightness {brightness}%
        <input
          type="range"
          min={1}
          max={100}
          value={brightness}
          disabled={disabled}
          onChange={(e) => setBrightness(Number(e.target.value))}
          onPointerUp={commitBrightness}
          onKeyUp={commitBrightness}
        />
      </label>
      <label className="field">
        Warmth {warmth} K
        <input
          type="range"
          min={WARMTH_MIN_K}
          max={WARMTH_MAX_K}
          step={100}
          value={warmth}
          disabled={disabled}
          onChange={(e) => setWarmth(Number(e.target.value))}
          onPointerUp={commitWarmth}
          onKeyUp={commitWarmth}
        />
      </label>
      {error && (
        <p role="alert" className="tile__error">
          {error}
        </p>
      )}

      {editing ? (
        <LightSettings
          device={device}
          rooms={rooms}
          onSaved={(next) => {
            setEditing(false);
            onChange(next);
          }}
          onCancel={() => setEditing(false)}
        />
      ) : (
        <Button variant="ghost" className="tile__edit" onClick={() => setEditing(true)}>
          Rename or move
        </Button>
      )}
    </article>
  );
}
