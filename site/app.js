// Web de Parpa: demo del notch, idioma (es/en) y enlace al último instalador.
(() => {
  const REPO = 'Xzorez/Parpa';
  const ON = '#f2f2f0';

  // ---- Textos en inglés (el HTML trae los de español) ----
  const EN = {
    nav_features: 'FEATURES',
    nav_privacy: 'PRIVACY',
    nav_specs: 'SPECS',
    nav_download: 'DOWNLOAD',
    hero_eyebrow: 'FREE · OPEN SOURCE · WINDOWS 10 & 11',
    hero_title: 'Your Windows, with a notch that blinks.',
    hero_lead: 'Music, notifications, files and notes in an island at the top of the screen. It opens when you hover it and hides when you don’t need it.',
    hero_download: 'DOWNLOAD FOR WINDOWS',
    hero_demo: 'TRY THE DEMO →',
    nav_demo: 'DEMO',
    demo_try: 'TRY IT YOURSELF →',
    demo_state: 'STATE',
    demo_now: 'NOW',
    demo_message: 'Coming to dinner on Friday? I booked for 9 pm.',
    demo_later: 'LATER',
    demo_open: 'OPEN',
    demo_volume: 'VOLUME',
    demo_speakers: 'SPEAKERS',
    tick_size: '43 MB',
    tick_license: 'MIT LICENSE',
    tick_admin: 'NO ADMIN RIGHTS',
    tick_free: 'FREE',
    tick_privacy: 'NO ACCOUNTS · NO TRACKING',
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
    f6_t: 'Tasks, notes and system',
    f6_d: 'Jot something down, tick off tasks and check CPU, GPU and RAM usage from the top.',
    privacy_eyebrow: 'PRIVACY',
    privacy_title: 'Everything stays on your PC.',
    privacy_lead: 'Parpa reads your notifications and clipboard to show them at the top. That’s why it has no accounts, sends nothing to any server and its code is open for anyone to read.',
    p1_t: 'ACCOUNTS',
    p1_d: 'Install and use. No sign-up, no email.',
    p2_t: 'SERVERS',
    p2_d: 'Notifications, clipboard and notes are stored only on your PC.',
    p3_t: 'TRACKING',
    p3_d: 'No ads, no analytics. Parpa never goes online on its own.',
    p4_t: 'OPEN SOURCE',
    p4_d: 'Anyone can check what it does. MIT license.',
    privacy_link: 'READ THE PRIVACY POLICY ↗',
    specs_eyebrow: 'SPECS',
    specs_title: 'Small on the outside. Light on the inside.',
    s_os: 'SYSTEM',
    s_os_v: 'Windows 10 (2004+) and Windows 11',
    s_arch: 'ARCHITECTURE',
    s_installer: 'INSTALLER',
    s_installer_v: '43 MB · per user, no admin rights',
    s_lang: 'LANGUAGES',
    s_startup: 'STARTUP',
    s_startup_v: 'With Windows (can be turned off)',
    s_shortcut: 'SHORTCUT',
    s_updates: 'UPDATES',
    s_updates_v: 'From the app, in one click',
    s_data: 'DATA',
    s_license: 'LICENSE',
    download_eyebrow: 'DOWNLOAD',
    download_title: 'Install it in a minute.',
    download_exe: 'DOWNLOAD INSTALLER (.EXE)',
    download_all: 'ALL VERSIONS',
    download_terminal: 'OR FROM THE TERMINAL',
    download_note: 'If Windows warns about an “unknown publisher”, click More info › Run anyway. The code is on GitHub for anyone to review.',
    copy: 'COPY',
    copied: 'COPIED',
    footer_license: 'OPEN SOURCE UNDER THE MIT LICENSE',
    footer_privacy: 'PRIVACY',
    footer_contribute: 'CONTRIBUTE',
    footer_bug: 'REPORT A BUG',
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
    stopDecoding();
    document.querySelectorAll('[data-i18n]').forEach(el => { el.textContent = t(el.dataset.i18n); });
    decode(document.querySelector('h1'), 1100);
    document.title = t('title');
    document.querySelector('meta[name=description]').content = t('description');
    const button = document.getElementById('lang');
    button.textContent = lang === 'en' ? '[ES]' : '[EN]';
    button.setAttribute('aria-label', lang === 'en' ? 'Español' : 'English');
    renderDots();
    show();
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
      if (ch === 'X') dot.dataset.on = '';
      return dot;
    }));
  }
  // En la demo los puntos apagados no se ven; en las tarjetas se dibujan en gris, como los glifos de la app.
  document.querySelectorAll('[data-glyph]').forEach(grid => fill(grid, G[grid.dataset.glyph], grid.classList.contains('g7l') ? '#1d1d20' : 'transparent'));
  fill(document.getElementById('eyes'), ['XX...XX', 'XX...XX', 'XX...XX']);

  // ---- Demo: Reposo → Música → Aviso → Volumen ----
  const S = [
    { k: 'idle', w: 120, h: 34, r: 16, es: 'Reposo', en: 'Idle' },
    { k: 'music', w: 290, h: 34, r: 16, es: 'Música', en: 'Music' },
    { k: 'peek', w: 420, h: 112, r: 26, es: 'Aviso', en: 'Notification' },
    { k: 'osd', w: 340, h: 40, r: 16, es: 'Volumen', en: 'Volume' },
  ];
  const notch = document.getElementById('notch');
  const body = document.getElementById('notch-body');
  const screen = notch.parentElement;
  const states = [...body.querySelectorAll('[data-state]')];
  const stateName = document.getElementById('state-name');
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
    // Quitar y volver a poner "on" reinicia el relleno rojo del estado actual.
    const all = dots.querySelectorAll('.dot');
    all.forEach(d => d.classList.remove('on'));
    void dots.offsetWidth;
    all[i]?.classList.add('on');
    stateName.textContent = s[lang].toUpperCase();
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

  // El notch escala con la pantalla de la demo (el aviso, de 420 px, siempre cabe).
  function scale() {
    notch.style.setProperty('--scale', Math.max(0.7, Math.min(1.6, screen.clientWidth / 520)).toFixed(2));
  }

  setInterval(() => {
    tick++;
    if (!reduced && tick % 13 === 0) { i = (i + 1) % S.length; show(); }
    frame();
  }, 200);
  new ResizeObserver(scale).observe(screen);

  // ---- Copiar "winget install Parpa" ----
  const copy = document.getElementById('copy');
  const copyState = copy.querySelector('.copy-state');
  copy.addEventListener('click', async () => {
    try { await navigator.clipboard.writeText('winget install Parpa'); } catch { /* sin permiso: se ve igualmente */ }
    copy.classList.add('done');
    copyState.textContent = t('copied');
    setTimeout(() => { copy.classList.remove('done'); copyState.textContent = t('copy'); }, 1500);
  });

  // ---- Última versión: número y enlace al instalador (si no hay, se queda en la página de versiones) ----
  fetch(`https://api.github.com/repos/${REPO}/releases/latest`)
    .then(r => (r.ok ? r.json() : null))
    .then(release => {
      if (!release) return;
      document.querySelectorAll('.version').forEach(el => { el.textContent = release.tag_name.replace(/^v/, ''); });
      const asset = release.assets?.find(a => /^Parpa-Setup-.*-x64\.exe$/.test(a.name));
      if (asset) document.getElementById('installer').href = asset.browser_download_url;
    })
    .catch(() => { /* sin conexión con GitHub: enlace a Releases */ });

  // ---- Titulares que se "descifran" (Doto es monoespaciada: el ancho no baila) ----
  const NOISE = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789#%+=*';
  function decode(el, duration = 800) {
    if (reduced || !el) return;
    const text = el.textContent;
    cancelAnimationFrame(el._decode);
    el.classList.add('decoding');
    const start = performance.now();
    const run = now => {
      const p = Math.min(1, (now - start) / duration);
      const done = Math.floor(p * text.length);
      let out = '';
      for (let k = 0; k < text.length; k++) {
        const ch = text[k];
        out += k < done || ch === ' ' ? ch : NOISE[(Math.random() * NOISE.length) | 0];
      }
      el.textContent = out;
      if (p < 1) el._decode = requestAnimationFrame(run);
      else { el.textContent = text; el.classList.remove('decoding'); }
    };
    el._decode = requestAnimationFrame(run);
  }
  function stopDecoding() {
    document.querySelectorAll('.decoding').forEach(el => { cancelAnimationFrame(el._decode); el.classList.remove('decoding'); });
  }

  // ---- Glifos que se encienden punto a punto ----
  function lightUp(grid, step = 9) {
    if (reduced || !grid) return;
    const cells = [...grid.children];
    const order = cells.map((_, k) => k).sort(() => Math.random() - 0.5);
    cells.forEach(c => { clearTimeout(c._t); c.style.background = '#1d1d20'; });
    order.forEach((k, n) => { cells[k]._t = setTimeout(() => { cells[k].style.background = cells[k].dataset.on !== undefined ? ON : '#1d1d20'; }, 80 + n * step); });
  }
  document.querySelectorAll('.cell').forEach(cell => cell.addEventListener('pointerenter', () => lightUp(cell.querySelector('.g7l'), 4)));

  // ---- Contadores: los ceros "buscan" su valor y el 100 sube ----
  function count(el) {
    if (reduced) return;
    const to = Number(el.dataset.to);
    const start = performance.now(), duration = 1000;
    const run = now => {
      const p = Math.min(1, (now - start) / duration);
      el.textContent = p >= 1 ? to : to === 0 ? (Math.random() * 10) | 0 : Math.round(to * (1 - Math.pow(1 - p, 3)));
      if (p < 1) requestAnimationFrame(run);
    };
    requestAnimationFrame(run);
  }

  // ---- Entradas al hacer scroll ----
  const REVEAL = '.section-head, .section h2, .section-lead, .cell, .zero, .text-link, .specs > div, .download > *';
  document.querySelectorAll(REVEAL).forEach(el => {
    const siblings = [...el.parentElement.children].filter(c => c.matches(REVEAL));
    el.style.setProperty('--d', Math.min(siblings.indexOf(el), 8) * 70 + 'ms');
    el.classList.add('reveal');
  });
  function onReveal(el) {
    el.classList.add('in');
    if (el.matches('h2')) decode(el, 700);
    if (el.matches('.cell')) setTimeout(() => lightUp(el.querySelector('.g7l')), parseInt(el.style.getPropertyValue('--d')) || 0);
    if (el.matches('.zero')) setTimeout(() => count(el.querySelector('.count')), parseInt(el.style.getPropertyValue('--d')) || 0);
  }
  if (reduced || !('IntersectionObserver' in window)) {
    document.querySelectorAll('.reveal').forEach(el => el.classList.add('in'));
  } else {
    const io = new IntersectionObserver(entries => entries.forEach(e => {
      if (!e.isIntersecting) return;
      io.unobserve(e.target);
      onReveal(e.target);
    }), { threshold: 0.15, rootMargin: '0px 0px -40px 0px' });
    document.querySelectorAll('.reveal').forEach(el => io.observe(el));
  }

  // ---- La franja se duplica para deslizarse sin cortes ----
  const ticker = document.getElementById('ticker');
  [...ticker.children].forEach(item => { const copy = item.cloneNode(true); copy.setAttribute('aria-hidden', 'true'); ticker.appendChild(copy); });

  // ---- Los ojos del logo y del icono parpadean de vez en cuando ----
  if (!reduced) {
    const eyes = [document.querySelector('.logo'), document.querySelector('.app-icon')];
    const blink = () => {
      eyes.forEach(e => e.classList.add('blink'));
      setTimeout(() => eyes.forEach(e => e.classList.remove('blink')), 140);
      setTimeout(blink, 2800 + Math.random() * 3200);
    };
    setTimeout(blink, 2000);
  }

  // ---- Rejilla de puntos de la portada: onda suave y luz alrededor del ratón ----
  const hero = document.getElementById('hero');
  const field = document.getElementById('field');
  const ctx = field.getContext('2d');
  const GAP = 16, LEVELS = 12;
  const pointer = { x: -1e4, y: -1e4 };
  let fw = 0, fh = 0, heroVisible = true, raf = 0;

  function resizeField() {
    const dpr = Math.min(2, window.devicePixelRatio || 1);
    fw = hero.clientWidth; fh = hero.clientHeight;
    field.width = Math.round(fw * dpr); field.height = Math.round(fh * dpr);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    drawField(performance.now());
  }

  function drawField(now) {
    ctx.clearRect(0, 0, fw, fh);
    const time = reduced ? 0 : now / 1000;
    // Se agrupan los puntos por brillo: una sola ruta por nivel en vez de miles de fillStyle.
    const buckets = Array.from({ length: LEVELS }, () => new Path2D());
    const hot = new Path2D();
    for (let y = GAP / 2; y < fh; y += GAP) {
      for (let x = GAP / 2; x < fw; x += GAP) {
        const dx = x - pointer.x, dy = y - pointer.y;
        const dist = Math.sqrt(dx * dx + dy * dy);
        const near = Math.max(0, 1 - dist / 150);
        const wave = reduced ? 0 : Math.max(0, Math.sin(x * 0.011 + y * 0.007 - time * 1.1)) ** 6;
        const glow = Math.min(1, near * near + wave * 0.35);
        const radius = 1.15 + glow * 0.75;
        if (dist < 11) { hot.moveTo(x + radius, y); hot.arc(x, y, radius + 0.4, 0, Math.PI * 2); continue; }
        const path = buckets[Math.min(LEVELS - 1, Math.round(glow * (LEVELS - 1)))];
        path.moveTo(x + radius, y);
        path.arc(x, y, radius, 0, Math.PI * 2);
      }
    }
    buckets.forEach((path, k) => {
      const c = Math.round(29 + (k / (LEVELS - 1)) * (150 - 29));
      ctx.fillStyle = `rgb(${c},${c},${c + 4})`;
      ctx.fill(path);
    });
    ctx.fillStyle = '#e5383b';
    ctx.fill(hot);
  }

  // ~40 fps bastan para una onda lenta: en pantallas de 144 Hz se ahorran dos de cada tres dibujados.
  let lastDraw = 0;
  function loop(now) {
    if (now - lastDraw >= 24) { lastDraw = now; drawField(now); }
    raf = heroVisible && !document.hidden ? requestAnimationFrame(loop) : 0;
  }
  function start() { if (!raf && !reduced) raf = requestAnimationFrame(loop); }

  hero.addEventListener('pointermove', e => {
    const r = hero.getBoundingClientRect();
    pointer.x = e.clientX - r.left; pointer.y = e.clientY - r.top;
    if (reduced) drawField(0);
  });
  hero.addEventListener('pointerleave', () => { pointer.x = pointer.y = -1e4; if (reduced) drawField(0); });
  new ResizeObserver(resizeField).observe(hero);
  new IntersectionObserver(([e]) => { heroVisible = e.isIntersecting; if (heroVisible) start(); }).observe(hero);
  document.addEventListener('visibilitychange', () => { if (!document.hidden) start(); });

  scale();
  applyLang();
  frame();
  start();
})();
