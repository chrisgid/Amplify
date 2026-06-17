// settings.jsx — settings page with reset-to-default.
function SectionLabel({ children }) {
  return <div className="t-caption" style={{ fontWeight: 600, color: "var(--text-2)", margin: "18px 2px 8px" }}>{children}</div>;
}

function SettingRow({ title, sub, control, icon, danger }) {
  return (
    <div className="amp-list-row">
      {icon && <span className="row-ico" style={danger ? { color: "var(--error)" } : undefined}>{icon}</span>}
      <div className="row-main">
        <div className="row-title" style={danger ? { color: "var(--error)" } : undefined}>{title}</div>
        {sub && <div className="row-sub">{sub}</div>}
      </div>
      {control}
    </div>
  );
}

function Settings({ onBack, settings, setSettings, theme, setTheme, step, setStep, account, status, clientId, onReset, onDisconnect }) {
  const set = (k) => (v) => setSettings((s) => ({ ...s, [k]: v }));
  return (
    <div className="screen amp-content" style={{ paddingTop: 4 }}>
      <div style={{ display: "flex", alignItems: "center", gap: 6, margin: "0 0 6px -8px" }}>
        <button className="icon-btn" onClick={onBack} title="Back"><IconBack s={18} /></button>
        <span className="t-bodylg">Settings</span>
      </div>

      <SectionLabel>General</SectionLabel>
      <div className="amp-list">
        <SettingRow title="Launch at startup" sub="Start Amplify when you sign in to Windows"
          control={<Toggle on={settings.startup} onChange={set("startup")} />} />
        <SettingRow title="Start minimized to the tray" sub="Open quietly in the notification area"
          control={<Toggle on={settings.tray} onChange={set("tray")} />} />
        <SettingRow title="Notify on volume change" sub="Show a brief toast when a shortcut fires"
          control={<Toggle on={settings.notify} onChange={set("notify")} />} />
      </div>

      <SectionLabel>Appearance</SectionLabel>
      <div className="amp-list">
        <SettingRow title="App theme"
          control={<Combo value={theme} width={138} onChange={setTheme}
            options={[{ value: "system", label: "Use system" }, { value: "light", label: "Light" }, { value: "dark", label: "Dark" }]} />} />
      </div>

      <SectionLabel>Volume</SectionLabel>
      <div className="amp-card" style={{ padding: "13px 16px" }}>
        <div style={{ display: "flex", alignItems: "baseline" }}>
          <div className="row-title">Volume step size</div>
          <div className="t-body-strong accent-text" style={{ marginLeft: "auto", fontVariantNumeric: "tabular-nums" }}>{step}%</div>
        </div>
        <div className="row-sub" style={{ marginBottom: 4 }}>Each key press changes the volume by this amount</div>
        <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
          <span className="t-caption" style={{ color: "var(--text-3)" }}>1%</span>
          <Slider value={step} min={1} max={25} step={1} onChange={setStep} />
          <span className="t-caption" style={{ color: "var(--text-3)" }}>25%</span>
        </div>
      </div>

      <SectionLabel>Account</SectionLabel>
      <div className="amp-list">
        <SettingRow
          icon={status === "connected" ? <IconCheckCircle s={19} style={{ color: "var(--success)" }} /> : <IconAlert s={19} style={{ color: "var(--warning)" }} />}
          title={status === "connected" ? account.name : "Not connected"}
          sub={status === "connected" ? `${account.plan} · Spotify` : "Reconnect to control volume"}
          control={<button className="btn sm" onClick={onDisconnect}>{status === "connected" ? "Disconnect" : "Reconnect"}</button>} />
      </div>
      <div className="amp-card" style={{ padding: "13px 16px", marginTop: 8 }}>
        <div className="row-title">Spotify Client ID</div>
        <div className="row-sub" style={{ marginBottom: 9 }}>From your Spotify Developer app. Reset Amplify to change it.</div>
        <div className="readonly-field">{clientId}</div>
      </div>

      <SectionLabel>Reset</SectionLabel>
      <div className="amp-list">
        <SettingRow danger icon={<IconWarningTri s={19} />}
          title="Reset Amplify" sub="Clears your shortcuts and disconnects Spotify"
          control={<button className="btn danger sm" onClick={onReset}>Reset…</button>} />
      </div>

      <div className="t-caption" style={{ color: "var(--text-3)", textAlign: "center", marginTop: 20 }}>
        Amplify 1.0.0 · Not affiliated with Spotify
      </div>
    </div>
  );
}

window.Settings = Settings;
