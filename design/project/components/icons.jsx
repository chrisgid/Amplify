// icons.jsx — Segoe Fluent-style line icons + Amplify brand mark.
// All stroke-based, sized via the `s` prop, inherit currentColor.

const Ico = ({ s = 16, children, fill = "none", sw = 1.5, style }) => (
  <svg
    width={s}
    height={s}
    viewBox="0 0 24 24"
    fill={fill}
    stroke={fill === "none" ? "currentColor" : "none"}
    strokeWidth={sw}
    strokeLinecap="round"
    strokeLinejoin="round"
    style={{ display: "block", flexShrink: 0, ...style }}
  >
    {children}
  </svg>
);

const IconSettings = ({ s = 16, style }) => (
  <svg width={s} height={s} viewBox="0 0 24 24" style={{ display: "block", flexShrink: 0, ...style }}>
    <path fill="currentColor" fillRule="evenodd" clipRule="evenodd" d="M10.6 2.2a1 1 0 00-.95.69l-.48 1.5a7.6 7.6 0 00-1.6.93l-1.54-.35a1 1 0 00-1.09.47l-1.4 2.4a1 1 0 00.14 1.18l1.07 1.14a7.7 7.7 0 000 1.86l-1.07 1.14a1 1 0 00-.14 1.18l1.4 2.4a1 1 0 001.09.47l1.54-.35c.5.38 1.03.7 1.6.93l.48 1.5a1 1 0 00.95.69h2.8a1 1 0 00.95-.69l.48-1.5c.57-.23 1.1-.55 1.6-.93l1.54.35a1 1 0 001.09-.47l1.4-2.4a1 1 0 00-.14-1.18l-1.07-1.14a7.7 7.7 0 000-1.86l1.07-1.14a1 1 0 00.14-1.18l-1.4-2.4a1 1 0 00-1.09-.47l-1.54.35a7.6 7.6 0 00-1.6-.93l-.48-1.5a1 1 0 00-.95-.69h-2.8zM12 8.4a3.6 3.6 0 100 7.2 3.6 3.6 0 000-7.2z" />
  </svg>
);

const IconKeyboard = (p) => (
  <Ico {...p}>
    <rect x="2.5" y="6" width="19" height="12" rx="2" />
    <path d="M6 9.5h.01M9 9.5h.01M12 9.5h.01M15 9.5h.01M18 9.5h.01M7.5 14.5h9" />
  </Ico>
);

const IconCheck = (p) => (
  <Ico {...p}>
    <path d="M4.5 12.5l5 5 10-11" />
  </Ico>
);

const IconCheckCircle = (p) => (
  <Ico {...p} fill="currentColor" sw={0}>
    <path d="M12 2a10 10 0 100 20 10 10 0 000-20zm4.7 7.7l-5.6 6.2a1 1 0 01-1.5 0L6.8 13a1 1 0 011.5-1.3l2 2.2 4.9-5.4a1 1 0 011.5 1.3z" />
  </Ico>
);

const IconAlert = (p) => (
  <Ico {...p} fill="currentColor" sw={0}>
    <path d="M12 2a10 10 0 100 20 10 10 0 000-20zm0 4.6a1.1 1.1 0 011.1 1.2l-.3 5.2a.8.8 0 01-1.6 0l-.3-5.2A1.1 1.1 0 0112 6.6zm0 9a1.2 1.2 0 110 2.4 1.2 1.2 0 010-2.4z" />
  </Ico>
);

const IconSpinner = (p) => (
  <Ico {...p}>
    <path d="M12 3a9 9 0 109 9" />
  </Ico>
);

const IconChevron = (p) => (
  <Ico {...p}>
    <path d="M9 6l6 6-6 6" />
  </Ico>
);

const IconBack = (p) => (
  <Ico {...p}>
    <path d="M15 6l-6 6 6 6" />
  </Ico>
);

const IconGlobe = (p) => (
  <Ico {...p}>
    <circle cx="12" cy="12" r="9" />
    <path d="M3 12h18M12 3c2.5 2.5 2.5 15 0 18M12 3c-2.5 2.5-2.5 15 0 18" />
  </Ico>
);

const IconRefresh = (p) => (
  <Ico {...p}>
    <path d="M20 11a8 8 0 10-1.8 6.3M20 20v-4.5h-4.5" />
  </Ico>
);

const IconExternal = (p) => (
  <Ico {...p}>
    <path d="M14 4h6v6M20 4l-8.5 8.5M18 14v4a2 2 0 01-2 2H6a2 2 0 01-2-2V8a2 2 0 012-2h4" />
  </Ico>
);

const IconCopy = (p) => (
  <Ico {...p}>
    <rect x="8" y="8" width="12" height="12" rx="2" />
    <path d="M16 8V6a2 2 0 00-2-2H6a2 2 0 00-2 2v8a2 2 0 002 2h2" />
  </Ico>
);

const IconLock = (p) => (
  <Ico {...p}>
    <rect x="5" y="11" width="14" height="9" rx="2" />
    <path d="M8 11V8a4 4 0 018 0v3" />
  </Ico>
);

const IconShield = (p) => (
  <Ico {...p}>
    <path d="M12 3l7 3v5c0 4.5-3 8-7 10-4-2-7-5.5-7-10V6z" />
  </Ico>
);

const IconPlus = (p) => (
  <Ico {...p}>
    <path d="M12 5v14M5 12h14" />
  </Ico>
);

const IconMinusCircle = (p) => (
  <Ico {...p}>
    <circle cx="12" cy="12" r="9" />
    <path d="M8 12h8" />
  </Ico>
);

const IconPlusCircle = (p) => (
  <Ico {...p}>
    <circle cx="12" cy="12" r="9" />
    <path d="M12 8v8M8 12h8" />
  </Ico>
);

const IconEdit = (p) => (
  <Ico {...p}>
    <path d="M16.5 4.5l3 3L9 18l-4 1 1-4z" />
  </Ico>
);

const IconWarningTri = (p) => (
  <Ico {...p}>
    <path d="M12 4l9 16H3z" />
    <path d="M12 10v4M12 17.2v.01" />
  </Ico>
);

const IconPower = (p) => (
  <Ico {...p}>
    <path d="M12 3v8" />
    <path d="M6.5 7a8 8 0 1011 0" />
  </Ico>
);

// --- Volume glyphs ---
const IconVolume = ({ level = 2, ...p }) => (
  <Ico {...p}>
    <path d="M4 9.5h3l4-3.5v12l-4-3.5H4z" fill="currentColor" stroke="none" />
    {level >= 1 && <path d="M15 9.5a3.5 3.5 0 010 5" />}
    {level >= 2 && <path d="M17.5 7a7 7 0 010 10" />}
  </Ico>
);

// --- Window caption buttons ---
const CapMin = ({ s = 10 }) => (
  <svg width={s} height={s} viewBox="0 0 10 10"><path d="M0 5h10" stroke="currentColor" strokeWidth="1" /></svg>
);
const CapMax = ({ s = 10 }) => (
  <svg width={s} height={s} viewBox="0 0 10 10"><rect x="0.5" y="0.5" width="9" height="9" fill="none" stroke="currentColor" strokeWidth="1" /></svg>
);
const CapClose = ({ s = 10 }) => (
  <svg width={s} height={s} viewBox="0 0 10 10"><path d="M0 0l10 10M10 0L0 10" stroke="currentColor" strokeWidth="1" /></svg>
);

// --- Amplify brand mark: speaker + sound waves in a filled circle ---
const AmplifyMark = ({ s = 32, ring = true }) => (
  <svg width={s} height={s} viewBox="0 0 48 48" style={{ display: "block", flexShrink: 0 }}>
    {ring && <circle cx="24" cy="24" r="24" fill="var(--accent-fill)" />}
    <g transform="translate(0,0)">
      <path
        d="M15 20.5h4l5-4.5v17l-5-4.5h-4a1.5 1.5 0 01-1.5-1.5v-5a1.5 1.5 0 011.5-1.5z"
        fill={ring ? "var(--on-accent)" : "var(--accent-fill)"}
      />
      <path
        d="M28 19.5a6.5 6.5 0 010 9M31.5 16a11 11 0 010 16"
        fill="none"
        stroke={ring ? "var(--on-accent)" : "var(--accent-fill)"}
        strokeWidth="2.4"
        strokeLinecap="round"
      />
    </g>
  </svg>
);

Object.assign(window, {
  IconSettings, IconKeyboard, IconCheck, IconCheckCircle, IconAlert, IconSpinner,
  IconChevron, IconBack, IconGlobe, IconRefresh, IconExternal, IconLock, IconShield,
  IconPlus, IconMinusCircle, IconPlusCircle, IconEdit, IconWarningTri, IconPower,
  IconVolume, CapMin, CapMax, CapClose, AmplifyMark, IconCopy,
});
