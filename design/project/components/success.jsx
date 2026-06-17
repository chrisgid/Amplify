// success.jsx — the localhost OAuth success page, shown inside a Windows (Edge-style) browser.

function BrowserChrome({ dark, url, tabTitle = "Amplify", children, onClose }) {
  const c = dark ?
  { strip: "#202020", active: "#2b2b2b", text: "rgba(255,255,255,.85)", subt: "rgba(255,255,255,.55)",
    bar: "#2b2b2b", field: "#3a3a3a", fstroke: "rgba(255,255,255,.08)", ico: "rgba(255,255,255,.8)", edge: "rgba(255,255,255,.08)" } :
  { strip: "#dde1e7", active: "#f3f4f6", text: "rgba(0,0,0,.82)", subt: "rgba(0,0,0,.5)",
    bar: "#f3f4f6", field: "#ffffff", fstroke: "rgba(0,0,0,.10)", ico: "rgba(0,0,0,.65)", edge: "rgba(0,0,0,.10)" };
  return (
    <div style={{
      width: 760, borderRadius: 9, overflow: "hidden", boxShadow: "0 40px 90px rgba(0,0,0,.45), 0 8px 24px rgba(0,0,0,.25)",
      background: c.active, border: `1px solid ${c.edge}`, color: c.text,
      fontFamily: "var(--font)", animation: "winIn .4s var(--ease) both"
    }}>
      {/* tab strip */}
      <div style={{ background: c.strip, height: 40, display: "flex", alignItems: "flex-end", paddingLeft: 10, gap: 6 }}>
        <div style={{ display: "flex", alignItems: "center", gap: 8, background: c.active, height: 32, borderRadius: "8px 8px 0 0",
          padding: "0 12px", maxWidth: 240, minWidth: 180 }}>
          <AmplifyMark s={15} />
          <span style={{ fontSize: 12.5, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{tabTitle}</span>
          <span style={{ marginLeft: "auto", color: c.subt, fontSize: 14, lineHeight: 1 }}>×</span>
        </div>
        <div style={{ color: c.subt, fontSize: 18, paddingBottom: 4, lineHeight: 1 }}>+</div>
        <div style={{ marginLeft: "auto", display: "flex", color: c.subt, fontSize: 12, height: 40 }}>
          {["—", "▢", "✕"].map((g, i) =>
          <div key={i} onClick={i === 2 ? onClose : undefined}
          style={{ width: 44, display: "grid", placeItems: "center", cursor: i === 2 ? "pointer" : "default" }}>{g}</div>
          )}
        </div>
      </div>
      {/* toolbar */}
      <div style={{ background: c.bar, height: 44, display: "flex", alignItems: "center", gap: 4, padding: "0 10px", borderBottom: `1px solid ${c.edge}` }}>
        {[<IconBack s={17} />, <IconChevron s={17} />, <IconRefresh s={16} />].map((ic, i) =>
        <div key={i} style={{ width: 30, height: 30, display: "grid", placeItems: "center", color: i === 1 ? c.subt : c.ico, opacity: i === 1 ? .5 : 1, borderRadius: 6 }}>{ic}</div>
        )}
        <div style={{ flex: 1, height: 30, background: c.field, border: `1px solid ${c.fstroke}`, borderRadius: 15,
          display: "flex", alignItems: "center", gap: 8, padding: "0 12px", marginLeft: 4, color: c.text }}>
          <IconLock s={13} style={{ opacity: .65 }} />
          <span style={{ fontSize: 13, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{url}</span>
        </div>
        <div style={{ width: 30, height: 30, display: "grid", placeItems: "center", color: c.ico }}>★</div>
        <div style={{ width: 26, height: 26, borderRadius: "50%", background: "var(--accent-fill)", color: "var(--on-accent)",
          display: "grid", placeItems: "center", fontSize: 12, fontWeight: 600, marginLeft: 2 }}>A</div>
      </div>
      {/* page */}
      {children}
    </div>);

}

function SuccessPage({ dark, onReturn }) {
  const pageBg = dark ? "#1b1b1b" : "#fbfbfc";
  return (
    <div style={{ position: "relative", background: pageBg, height: 380, display: "grid", placeItems: "center", overflow: "hidden" }}>
      <div style={{ position: "absolute", inset: 0, background:
        "radial-gradient(60% 50% at 50% 0%, var(--accent-soft), transparent 70%)" }} />
      <div style={{ position: "relative", textAlign: "center", maxWidth: 420, padding: "0 32px" }}>
        <div style={{ position: "relative", display: "inline-grid", placeItems: "center", marginBottom: 22 }}>
          <div style={{ width: 76, height: 76, borderRadius: "50%", background: "var(--success-bg)",
            border: "1px solid var(--success-stroke)", display: "grid", placeItems: "center", animation: "winIn .5s var(--ease) both" }}>
            <IconCheck s={38} style={{ color: "var(--success)" }} sw={2.2} />
          </div>
        </div>
        <div style={{ fontSize: 26, fontWeight: 600, fontFamily: "var(--font-display)", color: "var(--text-1)", letterSpacing: "-.2px" }}>
          You're connected
        </div>
        <div style={{ fontSize: 14.5, lineHeight: "21px", color: "var(--text-2)", marginTop: 10 }}>Amplify is now linked to your Spotify account. You can close this tab and return to the app.


        </div>
        <button className="btn accent" style={{ marginTop: 24, padding: "8px 22px" }} onClick={onReturn}>
          Return to Amplify
        </button>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 7, marginTop: 26, color: "var(--text-3)", fontSize: 12 }}>
          <AmplifyMark s={15} />
          <span>Amplify · It's safe to close this window</span>
        </div>
      </div>
    </div>);

}

function DeniedPage({ dark, onReturn }) {
  const pageBg = dark ? "#1b1b1b" : "#fbfbfc";
  return (
    <div style={{ position: "relative", background: pageBg, height: 380, display: "grid", placeItems: "center", overflow: "hidden" }}>
      <div style={{ position: "absolute", inset: 0, background:
        "radial-gradient(60% 50% at 50% 0%, var(--warning-bg), transparent 70%)" }} />
      <div style={{ position: "relative", textAlign: "center", maxWidth: 430, padding: "0 32px" }}>
        <div style={{ position: "relative", display: "inline-grid", placeItems: "center", marginBottom: 22 }}>
          <div style={{ width: 76, height: 76, borderRadius: "50%", background: "var(--warning-bg)",
            border: "1px solid var(--warning-stroke)", display: "grid", placeItems: "center", animation: "winIn .5s var(--ease) both" }}>
            <svg width={36} height={36} viewBox="0 0 24 24" fill="none" stroke="var(--warning)" strokeWidth={2.4} strokeLinecap="round">
              <path d="M6 6l12 12M18 6L6 18" />
            </svg>
          </div>
        </div>
        <div style={{ fontSize: 26, fontWeight: 600, fontFamily: "var(--font-display)", color: "var(--text-1)", letterSpacing: "-.2px" }}>
          Access not granted
        </div>
        <div style={{ fontSize: 14.5, lineHeight: "21px", color: "var(--text-2)", marginTop: 10 }}>
          Amplify wasn't given permission to connect to your Spotify account. Nothing has changed — you can
          close this tab and try again from the app whenever you're ready.
        </div>
        <button className="btn accent" style={{ marginTop: 24, padding: "8px 22px" }} onClick={onReturn}>
          Return to Amplify
        </button>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 7, marginTop: 26, color: "var(--text-3)", fontSize: 12 }}>
          <AmplifyMark s={15} />
          <span>Amplify · It's safe to close this window</span>
        </div>
      </div>
    </div>);

}

function OAuthBrowser({ dark, outcome = "success", onReturn }) {
  const denied = outcome === "denied";
  return (
    <div style={{ position: "fixed", inset: 0, zIndex: 200, display: "grid", placeItems: "center",
      background: "rgba(0,0,0,.42)" }}>
      <BrowserChrome dark={dark}
        tabTitle={denied ? "Amplify — Not connected" : "Amplify — Connected"}
        url={denied ? "127.0.0.1:49737/callback?error=access_denied&state=amplify" : "127.0.0.1:49737/callback?code=AQB…&state=amplify"}
        onClose={onReturn}>
        {denied ? <DeniedPage dark={dark} onReturn={onReturn} /> : <SuccessPage dark={dark} onReturn={onReturn} />}
      </BrowserChrome>
    </div>);

}

Object.assign(window, { BrowserChrome, SuccessPage, DeniedPage, OAuthBrowser });