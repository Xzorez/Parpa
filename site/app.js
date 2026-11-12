// Web de Parpa: demo del notch, idioma (es/en) y enlace al último instalador.
(() => {
  const REPO = 'Xzorez/Parpa';

  // ---- Textos en inglés (el HTML trae los de español) ----
  const EN = {
    nav_features: 'Features',
    nav_privacy: 'Privacy',
    nav_specs: 'Specs',
    nav_download: 'Download',
    hero_eyebrow: 'Free · Open source · Windows 10 & 11',
    hero_title: 'Your Windows, with a notch that blinks.',
    hero_lead: 'Music, notifications, files and notes in an island at the top of the screen. It opens when you hover it and hides when you don’t need it.',
    hero_download: 'Download for Windows',
    hero_demo: 'Try the demo →',
    nav_demo: 'Demo',
    demo_try: 'Try it yourself →',
    demo_state: 'State',
    demo_now: 'Now',
    demo_message: 'Coming to dinner on Friday? I booked for 9 pm.',
    demo_later: 'Later',
    demo_open: 'Open',
    demo_volume: 'Volume',
    demo_speakers: 'Speakers',
    tick_size: '43 MB',
    tick_license: 'MIT license',
    tick_admin: 'No admin rights',
    tick_free: 'Free',
    tick_privacy: 'No accounts · No tracking',
    features_eyebrow: 'What it does',
    features_title: 'Everything you check often, in one place.',
    f1_t: 'What’s playing',
    f1_d: 'Song, artwork and controls from Spotify or any player, without switching windows.',
    f2_t: 'Notifications in sight',
    f2_d: 'WhatsApp, Discord or calls at the top, with buttons to open them or leave them for later. Plus Do Not Disturb.',
    f3_t: 'File shelf',
    f3_d: 'Drag files onto the notch and drop them wherever you want later. Pull the text out of a screenshot, resize several images at once, and your screenshots show up on their own.',
    f4_t: 'Audio and volume',
    f4_d: 'Switch from speakers to headphones, mute the mic and set each app’s volume, including whatever’s playing. Plus clean volume, brightness and battery indicators.',
    f5_t: 'Clipboard',
    f5_d: 'The last things you copied, always at hand. Passwords are never saved.',
    f6_t: 'Tasks, notes and system',
    f6_d: 'Drop text to turn it into a note or task, save links with their title and check CPU, GPU and RAM usage from the top.',
    privacy_eyebrow: 'Privacy',
    privacy_title: 'Everything stays on your PC.',
    privacy_lead: 'Parpa reads your notifications and clipboard to show them at the top. That’s why it has no accounts, sends nothing to any server and its code is open for anyone to read.',
    p1_t: 'Accounts',
    p1_d: 'Install and use. No sign-up, no email.',
    p2_t: 'Servers',
    p2_d: 'Notifications, clipboard and notes are stored only on your PC.',
    p3_t: 'Tracking',
    p3_d: 'No ads, no analytics. Parpa never goes online on its own.',
    p4_t: 'Open source',
    p4_d: 'Anyone can check what it does. MIT license.',
    privacy_link: 'Read the privacy policy ↗',
    specs_eyebrow: 'Specs',
    specs_title: 'Small on the outside. Light on the inside.',
    s_os: 'System',
    s_os_v: 'Windows 10 (2004+) and Windows 11',
    s_arch: 'Architecture',
    s_installer: 'Installer',
    s_installer_v: '43 MB · per user, no admin rights',
    s_lang: 'Languages',
    s_startup: 'Startup',
    s_startup_v: 'With Windows (can be turned off)',
    s_shortcut: 'Shortcut',
    s_updates: 'Updates',
    s_updates_v: 'From the app, in one click',
    s_data: 'Data',
    s_license: 'License',
    download_eyebrow: 'Download',
    download_title: 'Install it in a minute.',
    download_exe: 'Download installer (.exe)',
    download_all: 'All versions',
    download_terminal: 'Or from the terminal',
    download_note: 'If Windows warns about an “unknown publisher”, click More info › Run anyway. The code is on GitHub for anyone to review.',
    copy: 'Copy',
    copied: 'Copied',
    footer_license: 'Open source under the MIT license',
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

  // ---- Iconos (icons.js) ----
  document.querySelectorAll('[data-glyph]').forEach(el => { el.innerHTML = window.PARPA_ICONS(el.dataset.glyph); });

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
  const eq = [...document.querySelectorAll('#eq i')];
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
    stateName.textContent = s[lang];
  }

  function frame() {
    // La mascota mira a los lados y parpadea; el ecualizador sigue la "música".
    mascot.classList.toggle('blink', tick % 18 === 0);
    mascot.classList.toggle('look', Math.sin(tick / 6) > 0.6);
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
