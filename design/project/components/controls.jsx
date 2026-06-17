// controls.jsx — reusable Fluent controls.
const { useState, useRef, useEffect, useCallback } = React;

function Toggle({ on, onChange }) {
  return (
    <div className={"toggle" + (on ? " on" : "")} role="switch" aria-checked={on}
      onClick={() => onChange(!on)}>
      <div className="knob" />
    </div>
  );
}

function Slider({ value, min = 0, max = 100, step = 1, onChange, width }) {
  const ref = useRef(null);
  const dragging = useRef(false);
  const pct = ((value - min) / (max - min)) * 100;

  const setFromX = useCallback((clientX) => {
    const el = ref.current; if (!el) return;
    const r = el.getBoundingClientRect();
    let p = (clientX - r.left) / r.width;
    p = Math.max(0, Math.min(1, p));
    let v = min + p * (max - min);
    v = Math.round(v / step) * step;
    onChange(Math.max(min, Math.min(max, v)));
  }, [min, max, step, onChange]);

  useEffect(() => {
    const move = (e) => { if (dragging.current) setFromX(e.clientX); };
    const up = () => { dragging.current = false; };
    window.addEventListener("pointermove", move);
    window.addEventListener("pointerup", up);
    return () => { window.removeEventListener("pointermove", move); window.removeEventListener("pointerup", up); };
  }, [setFromX]);

  return (
    <div className="slider" ref={ref} style={width ? { flex: "none", width } : undefined}
      onPointerDown={(e) => { dragging.current = true; setFromX(e.clientX); }}>
      <div className="track" />
      <div className="fill" style={{ width: `calc(${pct}% )` }} />
      <div className="thumb" style={{ left: `${pct}%` }} />
    </div>
  );
}

function Combo({ value, options, onChange, width }) {
  const [open, setOpen] = useState(false);
  const ref = useRef(null);
  useEffect(() => {
    if (!open) return;
    const h = (e) => { if (ref.current && !ref.current.contains(e.target)) setOpen(false); };
    window.addEventListener("pointerdown", h);
    return () => window.removeEventListener("pointerdown", h);
  }, [open]);
  const cur = options.find((o) => o.value === value) || options[0];
  return (
    <div className="combo" ref={ref} style={width ? { width } : undefined}>
      <button className="combo-btn" onClick={() => setOpen((o) => !o)}>
        <span className="cv">{cur.label}</span>
        <IconChevron s={12} style={{ transform: "rotate(90deg)", opacity: .7 }} />
      </button>
      {open && (
        <div className="combo-menu">
          {options.map((o) => (
            <div key={o.value} className={"combo-opt" + (o.value === value ? " sel" : "")}
              onClick={() => { onChange(o.value); setOpen(false); }}>
              <span>{o.label}</span>
              <IconCheck s={14} className="chk" />
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function InfoBar({ kind = "info", icon, title, children, action }) {
  return (
    <div className={"infobar " + kind}>
      <span className="ib-ico">{icon}</span>
      <div style={{ flex: 1, minWidth: 0 }}>
        {title && <div className="t-body-strong" style={{ marginBottom: children ? 2 : 0 }}>{title}</div>}
        {children && <div className="t-caption">{children}</div>}
      </div>
      {action}
    </div>
  );
}

// Render a hotkey combo as keycaps. combo = array of label strings.
function KeyCombo({ combo, listening }) {
  if (listening) {
    return <span className="keys listening"><span className="keycap" style={{ minWidth: 96, color: "var(--text-2)", fontWeight: 400 }}>Press keys…</span></span>;
  }
  return (
    <span className="keys">
      {combo.map((k, i) => (
        <React.Fragment key={i}>
          {i > 0 && <span className="plus">+</span>}
          <span className="keycap">{k}</span>
        </React.Fragment>
      ))}
    </span>
  );
}

function CopyField({ value, label = "Copy" }) {
  const [copied, setCopied] = useState(false);
  const copy = () => {
    try { navigator.clipboard && navigator.clipboard.writeText(value); } catch (e) {}
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  };
  return (
    <div className="copy-field">
      <code>{value}</code>
      <button className="copy-btn" onClick={copy} title={copied ? "Copied" : label}>
        {copied ? <IconCheck s={15} style={{ color: "var(--success)" }} /> : <IconCopy s={15} />}
      </button>
    </div>
  );
}

function Dialog({ icon, title, children, actions }) {
  return (
    <div className="dialog-scrim">
      <div className="dialog" role="dialog" aria-modal="true">
        <div className="dialog-body">
          {(icon || title) && (
            <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 10 }}>
              {icon}
              <div className="t-bodylg">{title}</div>
            </div>
          )}
          <div className="t-body t-2" style={{ lineHeight: "20px" }}>{children}</div>
        </div>
        <div className="dialog-foot">{actions}</div>
      </div>
    </div>
  );
}

Object.assign(window, { Toggle, Slider, Combo, InfoBar, KeyCombo, Dialog, CopyField });
