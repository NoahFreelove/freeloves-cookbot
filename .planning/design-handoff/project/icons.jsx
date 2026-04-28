// Lightweight inline icons. Material-ish but custom-drawn outlines.
// stroke-based, currentColor, sized via the `s` prop.
const Icon = ({ d, s = 18, fill = false }) => (
  <svg width={s} height={s} viewBox="0 0 24 24"
    fill={fill ? 'currentColor' : 'none'}
    stroke={fill ? 'none' : 'currentColor'}
    strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"
    style={{ flexShrink: 0, verticalAlign: 'middle' }}>
    {d}
  </svg>
);

const Icons = {
  home:    (p) => <Icon {...p} d={<><path d="M3 11.5L12 4l9 7.5"/><path d="M5 10.5V20h14v-9.5"/><path d="M10 20v-5h4v5"/></>} />,
  book:    (p) => <Icon {...p} d={<><path d="M5 4h11a3 3 0 0 1 3 3v13H8a3 3 0 0 1-3-3V4z"/><path d="M5 17h14"/></>} />,
  pantry:  (p) => <Icon {...p} d={<><rect x="4" y="3" width="16" height="18" rx="2"/><path d="M4 9h16M4 15h16M12 3v18"/></>} />,
  cart:    (p) => <Icon {...p} d={<><path d="M3 4h2l2.5 11h11l2-8H6"/><circle cx="9" cy="19" r="1.4"/><circle cx="17" cy="19" r="1.4"/></>} />,
  spark:   (p) => <Icon {...p} d={<><path d="M12 3v4M12 17v4M3 12h4M17 12h4M5.6 5.6l2.8 2.8M15.6 15.6l2.8 2.8M5.6 18.4l2.8-2.8M15.6 8.4l2.8-2.8"/></>} />,
  prompt:  (p) => <Icon {...p} d={<><path d="M4 6h16M4 12h10M4 18h16"/></>} />,
  user:    (p) => <Icon {...p} d={<><circle cx="12" cy="8" r="4"/><path d="M4 21c1.5-4 5-6 8-6s6.5 2 8 6"/></>} />,
  menu:    (p) => <Icon {...p} d={<><path d="M4 7h16M4 12h16M4 17h16"/></>} />,
  search:  (p) => <Icon {...p} d={<><circle cx="11" cy="11" r="6"/><path d="M16 16l4 4"/></>} />,
  plus:    (p) => <Icon {...p} d={<><path d="M12 5v14M5 12h14"/></>} />,
  check:   (p) => <Icon {...p} d={<><path d="M5 12.5l4 4 10-10"/></>} />,
  clock:   (p) => <Icon {...p} d={<><circle cx="12" cy="12" r="8.5"/><path d="M12 7.5V12l3 2"/></>} />,
  flame:   (p) => <Icon {...p} d={<><path d="M12 3s5 4 5 9a5 5 0 0 1-10 0c0-1.5.5-2.5 1.5-3.5C8 10 9 11 9 12c0-3 1.5-6 3-9z"/></>} />,
  pause:   (p) => <Icon {...p} d={<><rect x="7" y="5" width="3.5" height="14" rx="1"/><rect x="13.5" y="5" width="3.5" height="14" rx="1"/></>} />,
  play:    (p) => <Icon {...p} d={<><path d="M7 5v14l12-7-12-7z"/></>} />,
  arrowR:  (p) => <Icon {...p} d={<><path d="M5 12h14M13 6l6 6-6 6"/></>} />,
  arrowL:  (p) => <Icon {...p} d={<><path d="M19 12H5M11 6l-6 6 6 6"/></>} />,
  bell:    (p) => <Icon {...p} d={<><path d="M6 17V11a6 6 0 1 1 12 0v6l1.5 2H4.5L6 17z"/><path d="M10 21h4"/></>} />,
  sun:     (p) => <Icon {...p} d={<><circle cx="12" cy="12" r="3.5"/><path d="M12 3v2M12 19v2M3 12h2M19 12h2M5.6 5.6l1.4 1.4M17 17l1.4 1.4M5.6 18.4l1.4-1.4M17 7l1.4-1.4"/></>} />,
  share:   (p) => <Icon {...p} d={<><circle cx="6" cy="12" r="2.5"/><circle cx="18" cy="6" r="2.5"/><circle cx="18" cy="18" r="2.5"/><path d="M8.2 11l7.6-3.6M8.2 13l7.6 3.6"/></>} />,
  download:(p) => <Icon {...p} d={<><path d="M12 4v12M6 11l6 6 6-6M5 20h14"/></>} />,
  copy:    (p) => <Icon {...p} d={<><rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 8V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h2"/></>} />,
  pencil:  (p) => <Icon {...p} d={<><path d="M4 20l4-1 11-11-3-3L5 16l-1 4z"/></>} />,
  more:    (p) => <Icon {...p} d={<><circle cx="6" cy="12" r="1.4" fill="currentColor"/><circle cx="12" cy="12" r="1.4" fill="currentColor"/><circle cx="18" cy="12" r="1.4" fill="currentColor"/></>} />,
  trash:   (p) => <Icon {...p} d={<><path d="M5 7h14M9 7V5a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2v2M7 7v12a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2V7"/></>} />,
  scale:   (p) => <Icon {...p} d={<><path d="M5 19h14M12 4v15M6 9l6-5 6 5M3 14a3 3 0 0 0 6 0l-3-5-3 5zM15 14a3 3 0 0 0 6 0l-3-5-3 5z"/></>} />,
  bolt:    (p) => <Icon {...p} d={<><path d="M13 3L4 14h7l-2 7 9-11h-7l2-7z"/></>} />,
  filter:  (p) => <Icon {...p} d={<><path d="M3 5h18M6 12h12M10 19h4"/></>} />,
  grid:    (p) => <Icon {...p} d={<><rect x="4" y="4" width="7" height="7" rx="1"/><rect x="13" y="4" width="7" height="7" rx="1"/><rect x="4" y="13" width="7" height="7" rx="1"/><rect x="13" y="13" width="7" height="7" rx="1"/></>} />,
  list:    (p) => <Icon {...p} d={<><path d="M8 6h12M8 12h12M8 18h12"/><circle cx="4" cy="6" r="1.2" fill="currentColor"/><circle cx="4" cy="12" r="1.2" fill="currentColor"/><circle cx="4" cy="18" r="1.2" fill="currentColor"/></>} />,
  chevD:   (p) => <Icon {...p} d={<><path d="M6 9l6 6 6-6"/></>} />,
  chevR:   (p) => <Icon {...p} d={<><path d="M9 6l6 6-6 6"/></>} />,
  flag:    (p) => <Icon {...p} d={<><path d="M5 3v18M5 4h12l-2 4 2 4H5"/></>} />,
  send:    (p) => <Icon {...p} d={<><path d="M4 12L20 4l-7 16-2-7-7-1z"/></>} />,
  save:    (p) => <Icon {...p} d={<><path d="M5 5h11l3 3v11H5z"/><path d="M8 5v5h7V5M8 19v-6h8v6"/></>} />,
  link:    (p) => <Icon {...p} d={<><path d="M9 15a4 4 0 0 1 0-6l3-3a4 4 0 0 1 6 6l-1 1"/><path d="M15 9a4 4 0 0 1 0 6l-3 3a4 4 0 0 1-6-6l1-1"/></>} />,
};

window.Icons = Icons;
window.CBIcon = Icon;
