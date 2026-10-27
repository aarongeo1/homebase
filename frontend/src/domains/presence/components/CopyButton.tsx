import { useState } from 'react';
import { Button } from '@/shared/ui';

export function CopyButton({ text, label }: { text: string; label: string }) {
  const [result, setResult] = useState<'copied' | 'failed' | null>(null);

  async function copy() {
    try {
      // Missing outside secure contexts (plain http on another device), so this can throw.
      await navigator.clipboard.writeText(text);
      setResult('copied');
    } catch {
      setResult('failed');
    }
  }

  return (
    <>
      <Button variant="secondary" onClick={copy}>
        {label}
      </Button>
      {result && (
        <span role="status" className="muted">
          {result === 'copied' ? 'Copied' : 'Could not copy — select the text and copy it'}
        </span>
      )}
    </>
  );
}
