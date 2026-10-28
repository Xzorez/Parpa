// Web de Parpa: demo del notch, idioma (es/en) y enlace al último instalador.
(() => {
  const REPO = 'Xzorez/Parpa';
  const ON = '#f2f2f0';

  // ---- Textos en inglés (el HTML trae los de español) ----
  const EN = {
    nav_features: 'Features',
    nav_privacy: 'Privacy',
    hero_eyebrow: 'FREE · OPEN SOURCE · WINDOWS 10 & 11',
    hero_title: 'Your Windows, with a notch that blinks.',
    hero_lead: 'Music, notifications, files and notes in an island at the top of the screen. It opens when you hover it and hides when you don’t need it.',
    hero_download: 'Download for Windows',
    hero_code: 'View the code',
    demo_now: 'NOW',
    demo_message: 'Coming to dinner on Friday? I booked for 9 pm.',
    demo_later: 'LATER',
    demo_open: 'OPEN',
    demo_volume: 'VOLUME',
    demo_speakers: 'SPEAKERS',
    features_eyebrow: 'WHAT IT DOES',
    features_title: 'Everything you check often, in one place.',
    f1_t: 'What’s playing',
    f1_d: 'Song, artwork and controls from Spotify or any player, without switching windows.',
    f2_t: 'Notifications in sight',
    f2_d: 'WhatsApp, Discord or calls at the top, with buttons to open them or leave them for later. Plus Do Not Disturb.',
    f3_t: 'File shelf',
    f3_d: 'Drag files onto the notch and drop them wherever you want later. Compress, convert or copy the path.',
    f4_t: 'Volume and battery',
    f4_d: 'Clean volume, brightness, battery, Wi‑Fi and headphone indicators instead of the Windows ones.',
    f5_t: 'Clipboard',
    f5_d: 'The last things you copied, always at hand. Passwords are never saved.',
    f6_t: 'Tasks, notes and focus',
    f6_d: 'Jot something down, tick off tasks and start a pomodoro from the top.',
    privacy_eyebrow: 'PRIVACY',
    privacy_title: 'Everything stays on your PC.',
    privacy_lead: 'Parpa reads your notifications and clipboard to show them at the top. That’s why it has no accounts, sends nothing to any server and its code is open for anyone to read.',
    p1_t: 'No account',
    p1_d: 'Install and use. No sign-up, no email.',
    p2_t: 'No servers',
    p2_d: 'Notifications, clipboard and notes are stored only on your PC.',
    p3_t: 'No tracking',
    p3_d: 'No ads, no analytics. Parpa never goes online on its own.',
    p4_t: 'Open source',
    p4_d: 'Anyone can check what it does. MIT license.',
    download_title: 'Install it in a minute',
    download_exe: 'Download installer (.exe)',
    download_all: 'All versions',
    download_terminal: 'or from the terminal:',
    copy: 'COPY',
    copied: 'COPIED',
    download_req: 'WINDOWS 10 (2004+) & 11 · 64-BIT · ENGLISH & SPANISH',
    footer_license: 'Open source under the MIT license.',
    footer_privacy: 'Privacy',
    footer_contribute: 'Contribute',
    footer_bug: 'Report a bug',
    title: 'Parpa · a notch for Windows',
    description: 'Music, notifications, files and notes in an island at the top of the screen. Free and open source for Windows 10 and 11.',
  };
  const ES = { copied: 'COPIADO', title: document.title, description: document.querySelector('meta[name=description]').content };
  document.querySelectorAll('[data-i18n]').forEach(el => { ES[el.dataset.i18n] = el.textContent; });

  // "auto" como la app: español si el navegador está en español, inglés en cualquier otro caso.
  let lang;
  try { lang = new URLSearchParams(location.search).get('lang') || localStorage.getItem('parpa.lang'); } catch { /* sin almacenamiento */ }
  if (lang !== 'es' && lang !== 'en') lang = (navigator.language || 'en').toLowerCase().startsWith('es') ? 'es' : 'en';
  const t = key => (lang === 'en' ? EN : ES)[key];

  function applyLang() {
    document.documentElement.lang = lang;
    document.querySelectorAll('[data-i18n]').forEach(el => { el.textContent = t(el.dataset.i18n); });
    document.title = t('title');
    document.querySelector('meta[name=description]').content = t('description');
    const button = document.getElementById('lang');
    button.textContent = lang === 'en' ? 'ES' : 'EN';
    button.setAttribute('aria-label', lang === 'en' ? 'Español' : 'English');
    renderDots();
  }
  document.getElementById('lang').addEventListener('click', () => {
    lang = lang === 'en' ? 'es' : 'en';
    try { localStorage.setItem('parpa.lang', lang); } catch { /* sin almacenamiento */ }
    applyLang();
  });

  // ---- Glifos de puntos 7×7 (data/glyphs.json) ----
  const G = {
    musica: ['..XXXXX', '..XXXXX', '..X...X', '..X...X', 'XXX.XXX', 'XXX.XXX', '.......'],
    mensaje: ['.XXXXX.', 'XXXXXXX', 'X.X.X.X', 'XXXXXXX', '.XXXXX.', '.XX....', '.X.....'],
    sonido: ['...X...', '..XX..X', 'XXXX.X.', 'XXXX.X.', 'XXXX.X.', '..XX..X', '...X...'],
    bandeja: ['XXX....', 'XXXXXXX', 'X.....X', 'X.....X', 'X.....X', 'XXXXXXX', '.......'],
    copias: ['XXXXX..', 'X...X..', 'X.XXXXX', 'X.X...X', 'XXX...X', '..X...X', '..XXXXX'],
    tareas: ['XX.XXXX', 'XX.....', '.......', 'XX.XXXX', 'XX.....', '.......', 'XX.XXX.'],
  };
  function fill(grid, rows, off = 'transparent') {
    grid.replaceChildren(...rows.join('').split('').map(ch => {
      const dot = document.createElement('i');
      dot.style.background = ch === 'X' ? ON : off;
      return dot;
    }));
  }
  document.querySelectorAll('[data-glyph]').forEach(grid => fill(grid, G[grid.dataset.glyph]));
  const eyes = document.getElementById('eyes');
  fill(eyes, ['XX...XX', 'XX...XX', 'XX...XX']);

  // ---- Demo: Reposo → Música → Aviso → Volumen ----
  const S = [
    { k: 'idle', w: 120, h: 34, r: 16, es: 'Reposo', en: 'Idle' },
    { k: 'music', w: 290, h: 34, r: 16, es: 'Música', en: 'Music' },
    { k: 'peek', w: 420, h: 112, r: 26, es: 'Aviso', en: 'Notification' },
    { k: 'osd', w: 340, h: 40, r: 16, es: 'Volumen', en: 'Volume' },
  ];
  const notch = document.getElementById('notch');
  const body = document.getElementById('notch-body');
  const states = [...body.querySelectorAll('[data-state]')];
  const mascot = document.getElementById('mascot');
  const mascotDots = Array.from({ length: 65 }, () => mascot.appendChild(document.createElement('i')));
  const eq = [...document.querySelectorAll('#eq i')];
  const bars = document.getElementById('bars');
  for (let k = 0; k < 16; k++) {
    const bar = bars.appendChild(document.createElement('i'));
    bar.style.height = (4 + k * 0.75) + 'px';
    bar.style.background = k < 11 ? ON : '#2a2a2e';
  }
  const dots = document.getElementById('dots');
  const reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;
  let i = 0, tick = 0;

  function renderDots() {
    dots.replaceChildren(...S.map((s, k) => {
      const dot = document.createElement('button');
      dot.type = 'button';
      dot.className = 'dot' + (k === i ? ' on' : '');
      dot.title = s[lang];
      dot.setAttribute('aria-label', s[lang]);
      dot.addEventListener('click', () => { i = k; tick = 1; show(); });
      return dot;
    }));
  }

  function show() {
    const s = S[i];
    notch.style.width = s.w + 'px';
    notch.style.height = s.h + 'px';
    body.style.borderRadius = `0 0 ${s.r}px ${s.r}px`;
    states.forEach(el => { el.hidden = el.dataset.state !== s.k; });
    dots.querySelectorAll('.dot').forEach((d, k) => d.classList.toggle('on', k === i));
  }

  function frame() {
    // La mascota mira a los lados y parpadea; el ecualizador sigue la "música".
    const blink = tick % 18 === 0, look = Math.sin(tick / 6) > 0.6 ? 1 : 0;
    for (let r = 0; r < 5; r++) for (let c = 0; c < 13; c++) {
      const on = [3, 8].some(x => (c === x + look || c === x + 1 + look) && r >= 1 && r <= 3 && (!blink || r === 2));
      mascotDots[r * 13 + c].style.background = on ? ON : '#1b1b1e';
    }
    eq.forEach((bar, k) => { bar.style.height = (3 + 9 * Math.abs(Math.sin(tick * (0.9 + k * 0.37) + k))).toFixed(1) + 'px'; });
  }

  function scale() {
    const width = Math.min(960, window.innerWidth - 48) - 28;
    notch.style.setProperty('--scale', Math.max(0.7, Math.min(1.9, width / 520)).toFixed(2));
  }

  setInterval(() => {
    tick++;
    if (!reduced && tick % 13 === 0) { i = (i + 1) % S.length; show(); }
    frame();
  }, 200);
  window.addEventListener('resize', scale);

  // ---- Copiar "winget install Parpa" ----
  const copy = document.getElementById('copy');
  const copyState = copy.querySelector('.copy-state');
  copy.addEventListener('click', async () => {
    try { await navigator.clipboard.writeText('winget install Parpa'); } catch { /* sin permiso: se ve igualmente */ }
    copy.classList.add('done');
    copyState.textContent = t('copied');
    setTimeout(() => { copy.classList.remove('done'); copyState.textContent = t('copy'); }, 1500);
  });

  // ---- Instalador de la última versión (si no hay, se queda en la página de versiones) ----
  fetch(`https://api.github.com/repos/${REPO}/releases/latest`)
    .then(r => (r.ok ? r.json() : null))
    .then(release => {
      const asset = release?.assets?.find(a => /^Parpa-Setup-.*-x64\.exe$/.test(a.name));
      if (asset) document.getElementById('installer').href = asset.browser_download_url;
    })
    .catch(() => { /* sin conexión con GitHub: enlace a Releases */ });

  scale();
  applyLang();
  show();
  frame();
})();
