import { useState } from 'react';
import { api, ApiError, type Scene } from '@/shared/api';
import { Button } from '@/shared/ui';

/** Plain words for the per-action error codes in SceneActivation. */
const PROBLEM: Record<string, string> = {
  device_offline: 'a light is offline',
  read_only_device: 'that device is read-only',
  validation_failed: 'a light does not support that',
  not_found: 'not found',
};

export function SceneBar({ scenes, onActivated }: { scenes: Scene[]; onActivated: () => void }) {
  const [message, setMessage] = useState<string | null>(null);

  async function activate(scene: Scene) {
    try {
      const { results } = await api.scenes.activate(scene.id);
      const failed = results.filter((r) => !r.ok);
      setMessage(
        failed.length === 0
          ? `${scene.name}: done.`
          : `${scene.name}: not everything worked. ${failed.map((f) => `${f.action.summary} (${PROBLEM[f.errorCode ?? ''] ?? 'error'})`).join('; ')}.`,
      );
    } catch (e) {
      setMessage(e instanceof ApiError ? e.message : 'Something went wrong.');
    }
    onActivated();
  }

  return (
    <div className="stack">
      <div className="row" role="group" aria-label="Scenes">
        {scenes.map((s) => (
          <Button key={s.id} variant="secondary" title={s.description} onClick={() => activate(s)}>
            {s.name}
          </Button>
        ))}
      </div>
      {message && (
        <p role="status" className="muted">
          {message}
        </p>
      )}
    </div>
  );
}
