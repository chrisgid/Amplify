// mainapp.jsx — main window: status + hotkey binding + live volume.
const MOD_KEYS = ["Control", "Alt", "Shift", "Meta", "OS"];

function comboFromEvent(e) {
  if (MOD_KEYS.includes(e.key)) return null; // wait for a non-modifier key
  const tokens = [];
  const sigParts = [];
  if (e.ctrlKey) {tokens.push("Ctrl");sigParts.push("ctrl");}
  if (e.altKey) {tokens.push("Alt");sigParts.push("alt");}
  if (e.shiftKey) {tokens.push("Shift");sigParts.push("shift");}
  if (e.metaKey) {tokens.push("Win");sigParts.push("win");}

  const map = { ArrowUp: "↑", ArrowDown: "↓", ArrowLeft: "←", ArrowRight: "→", " ": "Space", Escape: "Esc" };
  let disp = map[e.key] || (e.key.length === 1 ? e.key.toUpperCase() : e.key);
  const base = e.key === " " ? "space" : e.key.toLowerCase();
  tokens.push(disp);
  sigParts.push(base);
  return { tokens, sig: sigParts.join("+") };
}

function StatusBlock({ status, account, onReconnect }) {
  if (status === "connecting") {
    return (
      <InfoBar kind="info" icon={<IconSpinner s={18} className="spin" />} title="Connecting to Spotify…">
        Re-establishing the link to your account.
      </InfoBar>);

  }
  if (status === "error") {
    return (
      <InfoBar kind="error" icon={<IconAlert s={18} />} title="Can't reach Spotify"
      action={<button className="btn sm" onClick={onReconnect} style={{ alignSelf: "center" }}><IconRefresh s={14} /> Reconnect</button>}>
        Open Spotify on a device, then reconnect.
      </InfoBar>);

  }
  // connected
  return (
    <div className="amp-card" style={{ display: "flex", alignItems: "center", gap: 13, padding: "12px 14px" }}>
      <div style={{ width: 38, height: 38, borderRadius: "50%", background: "var(--accent-fill)", color: "var(--on-accent)",
        display: "grid", placeItems: "center", fontWeight: 600, fontSize: 14, flexShrink: 0 }}>{account.initials}</div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
          <span className="t-body-strong" style={{ whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{account.name}</span>
          <IconCheckCircle s={15} style={{ color: "var(--success)", flexShrink: 0 }} />
        </div>
        <div className="t-caption" style={{ color: "var(--text-3)" }}>{account.plan} · {account.device}</div>
      </div>
      <span className="t-caption" style={{ color: "var(--success)", fontWeight: 600, display: "flex", alignItems: "center", gap: 4 }}>
        Connected
      </span>
    </div>);

}

function HotkeyRow({ icon, title, sub, combo, listening, onRecord, onCancel, flash }) {
  return (
    <div className={"amp-list-row" + (flash ? " flash" : "")} style={{ background: flash ? "var(--subtle-hover)" : "transparent", transition: "background .25s" }}>
      <span className="row-ico">{icon}</span>
      <div className="row-main">
        <div className="row-title">{title}</div>
        <div className="row-sub">{sub}</div>
      </div>
      <KeyCombo combo={combo.tokens} listening={listening} />
      {listening ?
      <button className="icon-btn" title="Cancel" onClick={onCancel}><IconChevron s={16} style={{ transform: "rotate(180deg)" }} /></button> :

      <button className="icon-btn" title="Change shortcut" onClick={onRecord}><IconEdit s={16} /></button>
      }
    </div>);

}

function MainApp({ status, account, combos, setCombos, volume, setVolume, step, onOpenSettings, setStatus }) {
  const [listening, setListening] = useState(null); // 'up' | 'down' | null
  const [flash, setFlash] = useState(null); // 'up' | 'down'

  // Capture keys while recording a binding.
  useEffect(() => {
    if (!listening) return;
    const onKey = (e) => {
      e.preventDefault();
      if (e.key === "Escape") {setListening(null);return;}
      const c = comboFromEvent(e);
      if (!c) return;
      setCombos((prev) => ({ ...prev, [listening]: c }));
      setListening(null);
    };
    window.addEventListener("keydown", onKey, true);
    return () => window.removeEventListener("keydown", onKey, true);
  }, [listening, setCombos]);

  // Live: pressing the bound hotkeys actually moves the volume (feels real).
  useEffect(() => {
    if (listening || status !== "connected") return;
    const onKey = (e) => {
      if (MOD_KEYS.includes(e.key)) return;
      const c = comboFromEvent(e);
      if (!c) return;
      let dir = null;
      if (c.sig === combos.up.sig) dir = 1;else
      if (c.sig === combos.down.sig) dir = -1;
      if (dir === null) return;
      e.preventDefault();
      setVolume((v) => Math.max(0, Math.min(100, v + dir * step)));
      setFlash(dir > 0 ? "up" : "down");
      setTimeout(() => setFlash(null), 240);
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [listening, status, combos, step, setVolume]);

  const muted = volume === 0;
  const level = volume === 0 ? 0 : volume < 50 ? 1 : 2;

  return (
    <div className="screen amp-content" style={{ paddingTop: 8 }} data-comment-anchor="ca28b1b96c-div-117-5">
      <StatusBlock status={status} account={account} onReconnect={() => setStatus("connecting")} />

      {/* Shortcuts */}
      <div className="t-caption" style={{ fontWeight: 600, color: "var(--text-2)", margin: "20px 2px 8px" }}>Keyboard shortcuts</div>
      <div className="amp-list">
        <HotkeyRow icon={<IconPlusCircle s={19} />} title="Volume up" sub={`Raises Spotify volume by ${step}%`}
        combo={combos.up} listening={listening === "up"} flash={flash === "up"}
        onRecord={() => setListening("up")} onCancel={() => setListening(null)} />
        <HotkeyRow icon={<IconMinusCircle s={19} />} title="Volume down" sub={`Lowers Spotify volume by ${step}%`}
        combo={combos.down} listening={listening === "down"} flash={flash === "down"}
        onRecord={() => setListening("down")} onCancel={() => setListening(null)} />
      </div>
      <div className="t-caption" style={{ color: "var(--text-3)", margin: "8px 2px 0", minHeight: 16 }}>
        {listening ?
        "Press any key combination · Esc to cancel" :
        status === "connected" ? "Tip: try your shortcuts now — the meter below responds live." : "Shortcuts work globally, even when Amplify is in the background."}
      </div>

      {/* Live volume */}
      <div className="amp-card" style={{ marginTop: 20, padding: 16, opacity: status === "connected" ? 1 : .5, pointerEvents: status === "connected" ? "auto" : "none" }}>
        <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
          <IconVolume s={20} level={level} />
          <div style={{ flex: 1, minWidth: 0 }}>
            <div className="t-body-strong" style={{ lineHeight: "16px" }}>Now controlling</div>
            <div className="t-caption" style={{ color: "var(--text-3)" }}>{account.device}</div>
          </div>
          <div className="t-subtitle" style={{ fontVariantNumeric: "tabular-nums", color: muted ? "var(--text-3)" : "var(--text-1)" }}>{volume}%</div>
        </div>
        <div style={{ display: "flex", alignItems: "center", gap: 8, marginTop: 12 }}>
          <button className="icon-btn" onClick={() => setVolume((v) => Math.max(0, v - step))}><IconMinusCircle s={20} /></button>
          <Slider value={volume} min={0} max={100} step={1} onChange={setVolume} />
          <button className="icon-btn" onClick={() => setVolume((v) => Math.min(100, v + step))}><IconPlusCircle s={20} /></button>
        </div>
      </div>

      {/* Footer */}
      <div style={{ display: "flex", alignItems: "center", marginTop: 18 }}>
        <span className="t-caption" style={{ color: "var(--text-3)", display: "flex", alignItems: "center", gap: 6 }}>
          <span style={{ width: 7, height: 7, borderRadius: "50%", background: "var(--success)", boxShadow: "0 0 0 3px var(--success-bg)" }} />
          Running in the background
        </span>
        <button className="btn subtle sm" style={{ marginLeft: "auto" }} onClick={onOpenSettings}>
          <IconSettings s={16} /> Settings
        </button>
      </div>
    </div>);

}

window.MainApp = MainApp;