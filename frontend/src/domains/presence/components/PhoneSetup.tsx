import { Card, DataTable, type Column } from '@/shared/ui';
import { PHONE_REPORTS, reportUrl } from '../model/presentation';
import { CopyButton } from './CopyButton';

const COLUMNS: ReadonlyArray<Column<(typeof PHONE_REPORTS)[number]>> = [
  { id: 'event', header: 'When the phone…', cell: (r) => r.event },
  { id: 'body', header: 'Send this body', cell: (r) => <code className="presence-code">{r.body}</code> },
];

/** How to make the phone's automation app call POST /api/devices/{id}/report. */
export function PhoneSetup({ phoneDeviceId, reportToken }: { phoneDeviceId: string | null; reportToken: string | null }) {
  const url = phoneDeviceId ? reportUrl(window.location.origin, phoneDeviceId) : null;
  return (
    <div id="phone-setup">
      <Card title="Set up your phone">
        {!url ? (
          <p className="muted">No phone is set up on the Mac.</p>
        ) : (
          <div className="stack">
            <p>
              The phone tells Homebase when you leave, come home, plug it in and go to sleep. In its automation app (Tasker,
              MacroDroid or Automate), add an “HTTP request” action for each event below, started by whichever of the app’s
              triggers matches that event.
            </p>
            <dl className="setup">
              <dt>Method</dt>
              <dd>
                <code className="presence-code">POST</code>
              </dd>
              <dt>URL</dt>
              <dd>
                <code className="presence-code">{url}</code>
                <CopyButton text={url} label="Copy URL" />
              </dd>
              <dt>Token</dt>
              <dd>
                {reportToken ? (
                  <>
                    <code className="presence-code">{reportToken}</code>
                    <CopyButton text={reportToken} label="Copy token" />
                  </>
                ) : (
                  <span className="muted">Open this page on the Mac itself to see the token.</span>
                )}
              </dd>
              <dt>Header</dt>
              <dd>
                <code className="presence-code">Authorization: Bearer {reportToken ?? '<token>'}</code>
              </dd>
              <dt>Body</dt>
              <dd>
                JSON (<code className="presence-code">Content-Type: application/json</code>), one per event:
              </dd>
            </dl>
            <DataTable caption="What the phone sends for each event" columns={COLUMNS} rows={PHONE_REPORTS} rowKey={(r) => r.event} />
            <p className="muted">
              Away from home the phone is on mobile data and can’t reach the Mac on your Wi-Fi. Install Tailscale on the phone and
              the Mac so the “left home” request still gets through, and use the Mac’s Tailscale address in the URL (open this page
              through that address and the URL above already uses it).
            </p>
          </div>
        )}
      </Card>
    </div>
  );
}
