// app.jsx — orchestrator: flow, theming, accent, tweaks.
const ACCENTS = [
{ value: "#0078D4", label: "Blue" },
{ value: "#038387", label: "Teal" },
{ value: "#107C10", label: "Green" },
{ value: "#8764B8", label: "Purple" },
{ value: "#CA5010", label: "Orange" }];


const TWEAK_DEFAULTS = /*EDITMODE-BEGIN*/{
  "themePref": "Light",
  "accent": "#0078D4",
  "status": "Connected",
  "oauthOutcome": "Approve"
} /*EDITMODE-END*/;

const DEFAULT_COMBOS = {
  up: { tokens: ["Ctrl", "Alt", "↑"], sig: "ctrl+alt+arrowup" },
  down: { tokens: ["Ctrl", "Alt", "↓"], sig: "ctrl+alt+arrowdown" }
};
const ACCOUNT = { name: "Alex Rivera", initials: "AR", plan: "Premium", device: "DESKTOP-AX31" };
const SAMPLE_CLIENT_ID = "4f9a2c7be1d04b3a8c6f0e9d2b5a1c8e";

function hexToRgb(hex) {
  let h = hex.replace("#", "");
  if (h.length === 3) h = h.split("").map((c) => c + c).join("");
  return { r: parseInt(h.slice(0, 2), 16), g: parseInt(h.slice(2, 4), 16), b: parseInt(h.slice(4, 6), 16) };
}
// Mix `pct`% of base toward `toward` ('white' | 'black' | 'transparent') — concrete rgb/rgba.
function mix(base, pct, toward) {
  const { r, g, b } = hexToRgb(base);
  if (toward === "transparent") return `rgba(${r},${g},${b},${(pct / 100).toFixed(3)})`;
  const t = toward === "white" ? 255 : 0;
  const w = pct / 100;
  const ch = (x) => Math.round(x * w + t * (1 - w));
  return `rgb(${ch(r)},${ch(g)},${ch(b)})`;
}

function accentVars(base, dark) {
  if (dark) return {
    "--accent-fill": mix(base, 62, "white"),
    "--accent-fill-hover": mix(base, 55, "white"),
    "--accent-fill-pressed": mix(base, 72, "white"),
    "--accent-edge": mix(base, 60, "white"),
    "--on-accent": "rgba(0,0,0,.92)",
    "--on-accent-dim": "rgba(0,0,0,.6)",
    "--accent-text": mix(base, 42, "white"),
    "--accent-soft": mix(base, 24, "transparent")
  };
  return {
    "--accent-fill": base,
    "--accent-fill-hover": mix(base, 90, "white"),
    "--accent-fill-pressed": mix(base, 80, "white"),
    "--accent-edge": mix(base, 86, "black"),
    "--on-accent": "#ffffff",
    "--on-accent-dim": "rgba(255,255,255,.78)",
    "--accent-text": mix(base, 76, "black"),
    "--accent-soft": mix(base, 12, "transparent")
  };
}

function App() {
  const [t, setTweak] = useTweaks(TWEAK_DEFAULTS);

  // system theme listener
  const [sysDark, setSysDark] = useState(() => window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches);
  useEffect(() => {
    if (!window.matchMedia) return;
    const m = window.matchMedia("(prefers-color-scheme: dark)");
    const h = (e) => setSysDark(e.matches);
    m.addEventListener ? m.addEventListener("change", h) : m.addListener(h);
    return () => {m.removeEventListener ? m.removeEventListener("change", h) : m.removeListener(h);};
  }, []);

  const theme = t.themePref === "System" ? sysDark ? "dark" : "light" : t.themePref.toLowerCase();
  const status = (t.status || "Connected").toLowerCase();
  // Free accounts are connected but can't control playback volume.
  const account = { ...ACCOUNT, plan: status === "free" ? "Free" : ACCOUNT.plan };

  // flow state
  const [route, setRoute] = useState("onboarding"); // onboarding | main | settings
  const [onbPhase, setOnbPhase] = useState("welcome"); // welcome | authorizing | verifying
  const [browserOpen, setBrowserOpen] = useState(false);
  const [browserOutcome, setBrowserOutcome] = useState("success"); // success | denied
  const [denied, setDenied] = useState(false); // show "access not granted" notice on onboarding
  const [resetOpen, setResetOpen] = useState(false);

  // shared app state
  const [combos, setCombos] = useState(DEFAULT_COMBOS);
  const [volume, setVolume] = useState(62);
  const [step, setStep] = useState(5);
  const [settings, setSettings] = useState({ startup: true, tray: true, notify: false });
  const [clientId, setClientId] = useState("");

  const connect = () => {
    setDenied(false);
    setBrowserOutcome(t.oauthOutcome === "Deny" ? "denied" : "success");
    setBrowserOpen(true);
    setOnbPhase("authorizing");
  };
  const returnFromBrowser = () => {
    setBrowserOpen(false);
    if (browserOutcome === "denied") {
      // permission refused — return to onboarding so the user can retry
      setOnbPhase("welcome");
      setDenied(true);
      return;
    }
    setOnbPhase("verifying");
    setTweak("status", "Connected");
    setTimeout(() => {setRoute("main");setOnbPhase("welcome");}, 1600);
  };
  const doReset = () => {
    setResetOpen(false);
    setCombos(DEFAULT_COMBOS);setVolume(62);setStep(5);
    setSettings({ startup: true, tray: true, notify: false });
    setClientId("");
    setRoute("onboarding");setOnbPhase("welcome");
  };
  const disconnect = () => {setRoute("onboarding");setOnbPhase("welcome");};

  const rootStyle = accentVars(t.accent, theme === "dark");

  return (
    <div id="desktop" data-wp={theme}>
      <div className="amp-root" data-theme={theme} style={rootStyle}>
        <div className="amp-window">
          {/* title bar */}
          <div className="titlebar">
            <AmplifyMark s={16} />
            <span className="tb-title">Amplify</span>
            <div className="caption-btns">
              <button className="cap-btn"><CapMin /></button>
              <button className="cap-btn"><CapMax /></button>
              <button className="cap-btn close"><CapClose /></button>
            </div>
          </div>

          {/* content */}
          <div className="amp-scroll">
            {route === "onboarding" &&
            <div className="amp-content" data-comment-anchor="cf34c48f7f-div-123-15"><Onboarding phase={onbPhase} denied={denied} clientId={clientId} setClientId={setClientId} onConnect={connect} /></div>
            }
            {route === "main" &&
            <MainApp
              status={status} account={account}
              combos={combos} setCombos={setCombos}
              volume={volume} setVolume={setVolume} step={step}
              onOpenSettings={() => setRoute("settings")}
              setStatus={(s) => setTweak("status", s.charAt(0).toUpperCase() + s.slice(1))} />
            }
            {route === "settings" &&
            <Settings
              onBack={() => setRoute("main")}
              settings={settings} setSettings={setSettings}
              theme={t.themePref} setTheme={(v) => setTweak("themePref", v)}
              step={step} setStep={setStep}
              account={account} status={status}
              clientId={clientId || SAMPLE_CLIENT_ID}
              onReset={() => setResetOpen(true)} onDisconnect={disconnect} />
            }
          </div>

          {/* reset confirm dialog */}
          {resetOpen &&
          <Dialog icon={<IconWarningTri s={20} style={{ color: "var(--error)" }} />} title="Reset Amplify?"
          actions={<>
                <button className="btn danger" onClick={doReset}>Reset everything</button>
                <button className="btn" onClick={() => setResetOpen(false)}>Cancel</button>
              </>}>
              This removes your keyboard shortcuts and Client ID, and disconnects your Spotify account. You'll need to
              set up Amplify again. This can't be undone.
            </Dialog>
          }
        </div>
      </div>

      {/* external browser overlay (OAuth success) */}
      {browserOpen &&
      <div className="amp-root" data-theme={theme} style={rootStyle}>
          <OAuthBrowser dark={theme === "dark"} outcome={browserOutcome} onReturn={returnFromBrowser} />
        </div>
      }

      {/* Tweaks */}
      <TweaksPanel>
        <TweakSection label="Theme" />
        <TweakRadio label="Appearance" value={t.themePref} options={["System", "Light", "Dark"]}
        onChange={(v) => setTweak("themePref", v)} />
        <TweakColor label="Accent" value={t.accent} options={ACCENTS.map((a) => a.value)}
        onChange={(v) => setTweak("accent", v)} />
        <TweakSection label="Connection status" />
        <TweakRadio label="Spotify" value={t.status} options={["Connected", "Free", "Connecting", "Error"]}
        onChange={(v) => {setTweak("status", v);if (route === "onboarding") setRoute("main");}} />
        <TweakSection label="OAuth flow" />
        <TweakRadio label="Authorization" value={t.oauthOutcome} options={["Approve", "Deny"]}
        onChange={(v) => setTweak("oauthOutcome", v)} />
        <TweakSection label="Demo" />
        <TweakButton label="Restart onboarding" onClick={() => {setRoute("onboarding");setOnbPhase("welcome");setDenied(false);setClientId("");}} />
      </TweaksPanel>
    </div>);

}

ReactDOM.createRoot(document.getElementById("root")).render(<App />);