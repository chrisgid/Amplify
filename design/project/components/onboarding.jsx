// onboarding.jsx — first-run welcome + Spotify developer-app setup.
const REDIRECT_URI = "http://127.0.0.1:49737/callback";

function Onboarding({ phase, denied, clientId, setClientId, onConnect }) {
  // phase: "welcome" | "authorizing" | "verifying"
  const busy = phase === "authorizing" || phase === "verifying";
  const ready = (clientId || "").trim().length > 0;

  return (
    <div className="screen" style={{ padding: "8px 4px 4px" }}>
      <div style={{ display: "flex", flexDirection: "column", alignItems: "center", textAlign: "center", paddingTop: 8 }}>
        <div style={{ marginBottom: 14 }}><AmplifyMark s={60} /></div>
        <div className="t-title">Amplify</div>
        <div className="t-body t-2" style={{ marginTop: 6, maxWidth: 360, textWrap: "balance" }}>
          Control Spotify's volume with hotkeys of your choice.
        </div>
      </div>

      {denied &&
      <div className="infobar warning" style={{ marginTop: 22 }}>
        <span className="ib-ico"><IconAlert s={18} /></span>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div className="t-body-strong" style={{ marginBottom: 2 }}>Access wasn't granted</div>
          <div className="t-caption">You declined the permission request on Spotify. Connect again and choose <b>Agree</b> to continue.</div>
        </div>
      </div>
      }

      <div className="t-caption" style={{ fontWeight: 600, color: "var(--text-2)", margin: "22px 2px 10px" }}>
        Set up your Spotify connection
      </div>
      <div className="amp-card" style={{ padding: "16px 16px 16px 15px" }}>
        <ol className="setup-steps">
          <li>
            <span className="sx-num">1</span>
            <div className="sx-text">Open the <a className="amp-link" href="https://developer.spotify.com/dashboard" target="_blank" rel="noopener noreferrer">Spotify Developer Dashboard</a> and sign in.</div>
          </li>
          <li>
            <span className="sx-num">2</span>
            <div className="sx-text">Click <b>Create app</b>.</div>
          </li>
          <li>
            <span className="sx-num">3</span>
            <div className="sx-text">Set the app name and description to <b>Amplify</b></div>
          </li>
          <li>
            <span className="sx-num">4</span>
            <div className="sx-text">
              Add this redirect URI:
              <CopyField value={REDIRECT_URI} label="Copy redirect URI" />
            </div>
          </li>
          <li>
            <span className="sx-num">5</span>
            <div className="sx-text">Under <b>Which API/SDKs are you planning to use?</b> tick <b>Web API</b>, then <b>Save</b>.</div>
          </li>
          <li>
            <span className="sx-num">6</span>
            <div className="sx-text">Copy your app's <b>Client ID</b> and paste it below.</div>
          </li>
        </ol>
      </div>

      <div style={{ marginTop: 16 }}>
        <label className="t-caption" style={{ fontWeight: 600, display: "block", marginBottom: 6, color: "var(--text-2)" }}>Client ID</label>
        <input
          className="amp-input"
          value={clientId}
          onChange={(e) => setClientId(e.target.value)}
          placeholder="e.g. 4f9a2c7be1d04b3a8c6f0e9d2b5a1c8e"
          spellCheck={false}
          autoComplete="off"
          disabled={busy} />
      </div>

      <div style={{ marginTop: 18 }}>
        {phase === "verifying" ?
        <button className="btn accent block" disabled>
            <IconSpinner s={16} className="spin" /> Verifying connection…
          </button> :
        phase === "authorizing" ?
        <button className="btn accent block" disabled>
            <IconSpinner s={16} className="spin" /> Waiting for Spotify…
          </button> :

        <button className="btn accent block" onClick={onConnect} disabled={!ready}>
            <IconExternal s={16} /> Connect Spotify account
          </button>
        }
        <div className="t-caption" style={{ textAlign: "center", marginTop: 10, color: "var(--text-3)" }}>
          {phase === "authorizing" ?
          "Finish signing in on the page that opened in your browser." :
          !ready ?
          "Paste your Client ID above to continue." :
          "Opens your browser to sign in securely with Spotify. An account with Spotify Premium is required."}
        </div>
      </div>

      <div className="infobar info" style={{ marginTop: 18 }}>
        <span className="ib-ico"><IconLock s={16} /></span>
        <div className="t-caption">Amplify only requests permission to read and change your playback state. You can revoke access from your Spotify account at any time.</div>
      </div>
    </div>);

}
window.Onboarding = Onboarding;
