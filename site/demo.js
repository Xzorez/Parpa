// Demo de Parpa en el navegador: la misma máquina de estados que la app (Soltar > Abierto > Aviso >
// Indicador > Actividad > Reposo), con las medidas y tiempos de data/tokens.json. Nada sale del navegador.
(() => {
  const reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;

  // ---- Textos ----
  const T = {
    es: {
      nav_back: '← Inicio', nav_download: 'Descargar', eyebrow: 'Pruébalo en el navegador', title: 'Juega con el notch.',
      lead: 'Es la misma isla que verás arriba de tu pantalla en Windows, con sus medidas y animaciones. Pasa el ratón por encima, abre sus ventanas y usa los botones de abajo para simular lo que pasa en tu PC.',
      hint: 'Pasa el ratón por el notch', simulate: 'Simular', local: 'Nada sale de tu navegador',
      sim_play: 'Poner música', sim_pause: 'Pausar música', sim_notify: 'Recibir un aviso', sim_voldown: 'Volumen −', sim_volup: 'Volumen +',
      sim_bright: 'Brillo', sim_copy: 'Copiar un texto', sim_file: 'Soltar un archivo', sim_toggle: 'Win + alt + n',
      tip1: 'Pasa el ratón por el notch para abrirlo; al salir se cierra.', tip2: 'La rueda del ratón cambia de ventana. Esc lo cierra.',
      tip3: 'Arrastra archivos, un enlace o un texto de esta página hasta el notch: según en qué mitad lo sueltes, hace una cosa u otra.', tip4: 'Selecciona cualquier texto de esta página y pulsa Ctrl + C: aparecerá en el Portapapeles.',
      tip5: 'Las barras de volumen se pulsan o se arrastran. En Audio, toca el nombre de una app para silenciarla.',
      footer: 'Código abierto con licencia MIT', footer_home: 'Web', footer_download: 'Descargar',
      home: 'Inicio', clip: 'Portapapeles', shelf: 'Bandeja', todo: 'Tareas', notes: 'Notas', audio: 'Audio', quick: 'Ajustes rápidos', menu: 'Menú',
      output: 'Salida', outputs: ['Auriculares', 'Altavoces'], mic: 'Micrófono', mic_on: 'Activo', mic_off: 'Silenciado', vol: 'Vol', muted: 'Mudo',
      sim_shot: 'Hacer una captura', screenshot: 'Captura añadida', shot_name: 'Captura de pantalla {time}.png',
      z_shelf: 'A la bandeja', z_ocr: 'Extraer texto', z_resize: 'Redimensionar', z_note: 'Crear nota', z_task: 'Crear tarea', z_title: 'Guardar con título', z_clip: 'Al portapapeles',
      flash_ocr: 'En la app, su texto iría al portapapeles', flash_resize: 'En la app, {n} imágenes a 1920 px en la bandeja', flash_note: '✓ Nota creada', flash_task: '✓ Tarea creada',
      music: 'Música', system: 'Sistema', nothing: 'Nada sonando', nothing_d: 'Dale al play en cualquier app y aparecerá aquí.',
      now: 'Ahora', later: 'Más tarde', open: 'Abrir', view: 'Ver', ok: 'OK', copied: 'Copiado',
      clip_empty: 'Copia algo con', clip_empty2: 'Y aparecerá aquí', shelf_empty: 'Arrastra archivos hasta el notch', shelf_empty2: 'Se guardan aquí hasta que los saques',
      drop: 'Suelta para guardar', drop2: 'En la bandeja', new_task: '+ Nueva tarea', all_done: 'Todo hecho', note_ph: 'Escribe una nota y pulsa Intro…',
      notes_empty: 'Sin notas todavía', notes_empty2: 'Escribe arriba o arrastra texto al notch',
      dnd: 'No molestar', mascot: 'Mascota', minimal: 'Minimal', on: 'Activado', off: 'No', volume: 'Volumen', speakers: 'Altavoces', brightness: 'Brillo', screen: 'Pantalla',
      flash_copied: '✓ Copiado al portapapeles', flash_open: 'Se abriría {app}', flash_dnd: 'No molestar: el aviso espera', flash_shelf: '✓ Guardado en la bandeja', flash_removed: 'Quitado de la bandeja',
      copy_text: 'Reunión movida a las 17:30, sala 2',
      tasks: ['Enviar el informe', 'Llamar al dentista', 'Comprar café'],
      notify: [['WhatsApp · Ana', '¿Vienes a la cena del viernes? Reservé a las 21:00.', 'WhatsApp'], ['Discord · #General', 'Leo: ¿alguien se apunta a una partida esta noche?', 'Discord'], ['Calendario', 'Revisión del proyecto en 10 minutos.', 'Calendario']],
    },
    en: {
      nav_back: '← Home', nav_download: 'Download', eyebrow: 'Try it in your browser', title: 'Play with the notch.',
      lead: 'It’s the same island you’ll see at the top of your screen on Windows, with its real sizes and animations. Hover it, open its windows and use the buttons below to simulate what happens on your PC.',
      hint: 'Hover the notch', simulate: 'Simulate', local: 'Nothing leaves your browser',
      sim_play: 'Play music', sim_pause: 'Pause music', sim_notify: 'Get a notification', sim_voldown: 'Volume −', sim_volup: 'Volume +',
      sim_bright: 'Brightness', sim_copy: 'Copy some text', sim_file: 'Drop a file', sim_toggle: 'Win + alt + n',
      tip1: 'Hover the notch to open it; it closes when you leave.', tip2: 'The mouse wheel switches windows. Esc closes it.',
      tip3: 'Drag files, a link or some text from this page onto the notch: which half you drop it on decides what happens.', tip4: 'Select any text on this page and press Ctrl + C: it shows up in the Clipboard.',
      tip5: 'Volume bars can be clicked or dragged. In Audio, tap an app’s name to mute it.',
      footer: 'Open source under the MIT license', footer_home: 'Website', footer_download: 'Download',
      home: 'Home', clip: 'Clipboard', shelf: 'Shelf', todo: 'Tasks', notes: 'Notes', audio: 'Audio', quick: 'Quick settings', menu: 'Menu',
      output: 'Output', outputs: ['Headphones', 'Speakers'], mic: 'Microphone', mic_on: 'On', mic_off: 'Muted', vol: 'Vol', muted: 'Muted',
      sim_shot: 'Take a screenshot', screenshot: 'Screenshot added', shot_name: 'Screenshot {time}.png',
      z_shelf: 'To shelf', z_ocr: 'Extract text', z_resize: 'Resize', z_note: 'New note', z_task: 'New task', z_title: 'Save with title', z_clip: 'To clipboard',
      flash_ocr: 'In the app, its text would go to the clipboard', flash_resize: 'In the app, {n} images at 1920 px on the shelf', flash_note: '✓ Note created', flash_task: '✓ Task created',
      music: 'Music', system: 'System', nothing: 'Nothing playing', nothing_d: 'Press play in any app and it shows up here.',
      now: 'Now', later: 'Later', open: 'Open', view: 'View', ok: 'OK', copied: 'Copied',
      clip_empty: 'Copy something with', clip_empty2: 'And it shows up here', shelf_empty: 'Drag files onto the notch', shelf_empty2: 'They stay here until you take them out',
      drop: 'Drop to keep', drop2: 'On the shelf', new_task: '+ New task', all_done: 'All done', note_ph: 'Write a note and press Enter…',
      notes_empty: 'No notes yet', notes_empty2: 'Type above or drag text onto the notch',
      dnd: 'Do not disturb', mascot: 'Mascot', minimal: 'Minimal', on: 'On', off: 'Off', volume: 'Volume', speakers: 'Speakers', brightness: 'Brightness', screen: 'Display',
      flash_copied: '✓ Copied to clipboard', flash_open: '{app} would open', flash_dnd: 'Do not disturb: it waits', flash_shelf: '✓ Kept on the shelf', flash_removed: 'Removed from the shelf',
      copy_text: 'Meeting moved to 5:30 pm, room 2',
      tasks: ['Send the report', 'Call the dentist', 'Buy coffee'],
      notify: [['WhatsApp · Ana', 'Coming to dinner on Friday? I booked for 9 pm.', 'WhatsApp'], ['Discord · #General', 'Leo: anyone up for a game tonight?', 'Discord'], ['Calendar', 'Project review in 10 minutes.', 'Calendar']],
    },
  };
  let lang;
  try { lang = new URLSearchParams(location.search).get('lang') || localStorage.getItem('parpa.lang'); } catch { /* sin almacenamiento */ }
  if (lang !== 'es' && lang !== 'en') lang = (navigator.language || 'en').toLowerCase().startsWith('es') ? 'es' : 'en';
  const t = (k, vars = {}) => String(T[lang][k] ?? k).replace(/\{(\w+)\}/g, (_, v) => vars[v] ?? '');

  // ---- Iconos (icons.js; en la app, Segoe Fluent Icons) ----
  const gl = (name, size = 's') => `<span class="gl gl-${size}">${window.PARPA_ICONS(name)}</span>`;
  const esc = s => String(s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]);
  const two = n => String(n).padStart(2, '0');
  const mmss = s => `${Math.floor(s / 60)}:${two(Math.floor(s % 60))}`;
  const clock = () => { const d = new Date(); return `${two(d.getHours())}:${two(d.getMinutes())}`; };

  // ---- Medidas (tokens.json › notch, radius) ----
  const SIZE = { idle: [120, 34], music: [290, 34], minimal: [96, 24], osd: [340, 40], peek: [420, 112], drop: [420, 84],
    menu: [460, 166], home: [580, 214], clip: [580, 214], shelf: [580, 272], todo: [580, 280], notes: [580, 222], audio: [580, 300], quick: [580, 246] };
  const PAGES = ['home', 'clip', 'shelf', 'todo', 'notes', 'audio', 'quick'];
  const PAGE_GLYPH = { home: 'inicio', clip: 'copias', shelf: 'bandeja', todo: 'tareas', notes: 'notas', audio: 'sonido', quick: 'general', menu: 'menu' };
  const TRACKS = [
    { title: 'Luz de neón', artist: 'Parpa FM', len: 208, art: 'linear-gradient(135deg,#e5383b,#5b1622)' },
    { title: 'Puntos suspensivos', artist: 'Matriz', len: 187, art: 'linear-gradient(135deg,#2f7bf5,#14284f)' },
    { title: 'Isla', artist: 'Notch', len: 231, art: 'linear-gradient(135deg,#00a64f,#0d3a22)' },
  ];

  // ---- Estado ----
  const S = {
    open: false, page: 'home', last: 'home', peek: null, osd: null, drop: null,
    dnd: false, mascot: true, minimal: false, volume: 70, brightness: 60,
    music: { playing: false, i: 0, pos: 42 }, sys: { CPU: 18, GPU: 7, RAM: 54 },
    clip: [], shelf: [], tasks: [], notes: [], notifyIndex: 0,
    // Audio: la primera app es la que suena en la tarjeta MÚSICA.
    audio: { out: 0, mic: true, apps: [{ name: 'Spotify', v: 80, muted: false }, { name: 'Discord', v: 100, muted: false }, { name: 'Brave', v: 60, muted: false }] },
  };
  // Tareas de ejemplo en el idioma actual (al cambiarlo se traducen y conservan si están hechas).
  function seedTasks() {
    const done = S.tasks.map(x => x.done);
    const extra = S.tasks.slice(3);
    S.tasks = T[lang].tasks.map((text, k) => ({ text, done: done[k] ?? k === 2, time: ['10:00', '12:30', '18:00'][k] })).concat(extra);
  }
  seedTasks();
  S.clip = [{ text: 'https://xzorez.github.io/Parpa/', at: Date.now() - 120000 }, { text: 'winget install Parpa', at: Date.now() - 600000 }];

  // ---- Vista según el estado (prioridades como en NotchViewModel) ----
  function current() {
    if (S.drop) return { key: 'drop', size: SIZE.drop, r: 26, html: dropView };
    if (S.open) return { key: 'open:' + S.page, size: SIZE[S.page], r: 30, html: openView };
    if (S.peek) return { key: 'peek:' + S.peek.id, size: SIZE.peek, r: 26, html: peekView };
    if (S.osd) return { key: 'osd:' + S.osd.kind, size: SIZE.osd, r: 16, html: osdView };
    if (S.minimal) return { key: 'minimal', size: SIZE.minimal, r: 12, html: () => '<div class="c-min"><i></i></div>' };
    if (S.music.playing) return { key: 'music', size: SIZE.music, r: 16, html: musicView };
    return { key: 'idle', size: SIZE.idle, r: 16, html: idleView };
  }

  const nshape = document.getElementById('nshape');
  const nbody = document.getElementById('nbody');
  const ncontent = document.getElementById('ncontent');
  let shownKey = '';

  function render() {
    const v = current();
    nshape.style.width = v.size[0] + 'px';
    nshape.style.height = v.size[1] + 'px';
    nbody.style.borderRadius = `0 0 ${v.r}px ${v.r}px`;
    const focused = document.activeElement?.closest?.('#ncontent') ? document.activeElement.dataset.keep : null;
    ncontent.style.width = v.size[0] + 'px';
    ncontent.innerHTML = v.html();
    if (v.key !== shownKey) {
      ncontent.classList.remove('enter');
      void ncontent.offsetWidth;
      if (!reduced) ncontent.classList.add('enter');
      shownKey = v.key;
    }
    if (focused) ncontent.querySelector(`[data-keep="${focused}"]`)?.focus();
    tickParts();
    document.getElementById('sim-music').textContent = t(S.music.playing ? 'sim_pause' : 'sim_play');
    document.querySelector('[data-sim="music"]').classList.toggle('on', S.music.playing);
  }

  // ---- Cerrado ----
  function idleView() {
    if (!S.mascot) return '<div class="c-idle"></div>';
    return `<div class="c-idle"><div class="mascot" id="mascot"><b></b><b></b></div></div>`;
  }
  const track = () => TRACKS[S.music.i];
  function musicView() {
    return `<div class="c-row">${gl('musica')}<span class="c-label">${esc(track().title)} <span class="c-sub">${esc(track().artist)}</span></span><div class="eq" id="eq">${'<i></i>'.repeat(5)}</div></div>`;
  }
  function peekView() {
    const p = S.peek;
    return `<div class="peek" data-peek><div class="peek-h">${gl(p.glyph)}<span class="c-label">${esc(p.head)}</span><span class="peek-when">${t('now')}</span></div>
      <div class="peek-b">${esc(p.body)}</div>
      <div class="peek-a"><button class="np" data-act="peek-secondary">${p.secondary}</button><button class="np np-on" data-act="peek-primary">${p.primary}</button></div>
      <div class="peek-bar" style="--dur:${p.ms}ms"><i></i></div></div>`;
  }
  function osdView() {
    const o = S.osd, value = o.kind === 'volume' ? S.volume : S.brightness;
    const bars = `<b style="width:${value}%"></b>`;
    return `<div class="osd">${gl(o.kind === 'volume' ? 'sonido' : 'brillo')}<div class="osd-t"><span class="c-label">${t(o.kind === 'volume' ? 'volume' : 'brightness')}</span><span>${t(o.kind === 'volume' ? 'speakers' : 'screen')}</span></div><div class="osd-bars">${bars}</div><span class="osd-v">${value}%</span></div>`;
  }
  // Lo que se arrastra decide las zonas (como DropActions en la app); la mitad bajo el ratón, cuál se usa.
  const ZONE_GLYPH = { shelf: 'bandeja', ocr: 'copias', resize: 'bandeja', note: 'notas', task: 'tareas', title: 'notas', clip: 'copias' };
  function zones(d) {
    if (d.kind === 'text') return ['note', 'task'];
    if (d.kind === 'link') return ['title', 'clip'];
    if (d.images && d.n === 1) return ['shelf', 'ocr'];
    if (d.images && d.n > 1) return ['shelf', 'resize'];
    return ['shelf'];
  }
  function dropView() {
    const zs = zones(S.drop);
    if (zs.length === 1) return `<div class="drop">${gl('bandeja', 'l')}<div class="drop-t"><span>${t('drop')}</span><span>${t('drop2')}</span></div></div>`;
    return `<div class="dz">${zs.map((z, k) => `<div class="dz-z ${k === S.drop.sel ? 'on' : ''}">${gl(ZONE_GLYPH[z], 'm')}<span>${t('z_' + z)}</span></div>`).join('')}</div>`;
  }

  // ---- Abierto ----
  function head(title, extra = '') {
    return `<div class="o-head"><div class="o-title">${gl(PAGE_GLYPH[S.page], 's')}<span>${title}</span>${extra}</div>
      <button class="o-menu ${S.page === 'menu' ? 'on' : ''}" data-act="menu" aria-label="${t('menu')}">${gl('menu', 's')}</button>
      <span class="o-clock" id="oclock">${clock()}</span></div>`;
  }
  function openView() {
    switch (S.page) {
      case 'menu': return menuView();
      case 'clip': return clipView();
      case 'shelf': return shelfView();
      case 'todo': return todoView();
      case 'notes': return notesView();
      case 'audio': return audioView();
      case 'quick': return quickView();
      default: return homeView();
    }
  }
  function homeView() {
    // Como HomeView: la música arriba, con la carátula de fondo, y una fila de píldoras (CPU, GPU, RAM y volumen).
    const m = S.music, tr = track();
    const music = m.playing || m.pos > 42 || m.i > 0
      ? `<div class="mc" style="--art:${tr.art}"><div class="mc-bg"></div>
          <div class="m-art">${gl('musica', 'm')}</div>
          <div class="mc-body"><div class="mc-top"><div class="m-txt"><span class="m-title">${esc(tr.title)}</span><span class="m-src">${esc(tr.artist)} · Spotify</span></div>
            <span class="m-btns"><button data-act="prev" aria-label="prev">${CTL.prev}</button><button data-act="play" aria-label="play">${m.playing ? CTL.pause : CTL.play}</button><button data-act="next" aria-label="next">${CTL.next}</button></span></div>
           <div class="mc-prog"><span id="mpos">${mmss(m.pos)}</span><div class="m-prog" id="mprog">${progDots()}</div><span>${mmss(tr.len)}</span></div></div></div>`
      : `<div class="mc mc-empty"><div class="m-art">${gl('musica', 'm')}</div><div class="mc-body" style="justify-content:center;gap:4px"><span class="card-l">${t('music')}</span><span class="m-title">${t('nothing')}</span><span class="m-src">${t('nothing_d')}</span></div><button class="np np-on" data-act="play">${CTL.play} Play</button></div>`;
    const vol = m.playing || m.pos > 42 || m.i > 0 ? `<div class="chip chip-vol" data-wheel>${gl('sonido', 's')}${slider('app:0', 3, 8)}${valueOf('app:0')}</div>` : '';
    return `<div class="open">${head(t('home'))}
      ${music}
      <div class="chips"><div id="sys" style="display:contents">${sysRows()}</div>${vol}</div></div>`;
  }
  // CPU, GPU y RAM: en la app son las de tu PC; aquí, valores de ejemplo que se mueven cada segundo.
  function sysRows() {
    const color = { CPU: '#30d158', GPU: '#0a84ff', RAM: '#ff9f0a' };
    return Object.entries(S.sys).map(([k, v]) => `<div class="chip"><i style="background:${color[k]}"></i><span>${k}</span><b>${v}%</b></div>`).join('');
  }
  function progDots() {
    return `<b style="width:${(S.music.pos / track().len * 100).toFixed(1)}%"></b>`;
  }
  const CTL = {
    play: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M7 4.5v15a1 1 0 0 0 1.5.86l12-7.5a1 1 0 0 0 0-1.72l-12-7.5A1 1 0 0 0 7 4.5z"/></svg>',
    pause: '<svg viewBox="0 0 24 24" fill="currentColor"><rect x="5.5" y="4" width="4.5" height="16" rx="1.5"/><rect x="14" y="4" width="4.5" height="16" rx="1.5"/></svg>',
    prev: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M11 6v12L2.5 12zM21 6v12l-8.5-6z"/></svg>',
    next: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M3 6v12l8.5-6zM13 6v12l8.5-6z"/></svg>',
  };
  const ago = at => {
    const min = Math.floor((Date.now() - at) / 60000);
    return min < 1 ? t('now') : `${min} min`;
  };
  function clipView() {
    const body = S.clip.length
      ? `<div class="list" style="height:146px">${S.clip.map((c, k) => `<div class="row" data-act="copy" data-i="${k}"><span class="idx">${two(k + 1)}</span><span class="txt">${esc(c.text)}</span><span class="meta">${ago(c.at)}</span></div>`).join('')}</div>`
      : `<div class="empty" style="height:146px"><span class="empty-t">${t('clip_empty')} <kbd class="k">CTRL</kbd> <kbd class="k">C</kbd> ${t('clip_empty2')}</span></div>`;
    return `<div class="open">${head(t('clip'), `<span class="count">${two(S.clip.length)}</span>`)}${body}</div>`;
  }
  function shelfView() {
    const body = S.shelf.length
      ? `<div class="files">${S.shelf.slice(0, 8).map((f, k) => `<div class="file"><span class="file-ext">${esc(f.ext)}</span><span class="file-n">${esc(f.name)}</span><span class="file-s">${f.size}</span><button class="file-x" data-act="unshelf" data-i="${k}" aria-label="×">×</button></div>`).join('')}</div>`
      : `<div class="empty" style="height:168px"><div class="mascot happy"><b></b><b></b></div><span class="empty-t">${t('shelf_empty')}</span><span class="empty-s">${t('shelf_empty2')}</span></div>`;
    return `<div class="open">${head(t('shelf'), S.shelf.length ? `<span class="count">${two(S.shelf.length)}</span>` : '')}${body}</div>`;
  }
  function todoView() {
    const done = S.tasks.filter(x => x.done).length, all = S.tasks.length;
    const count = `<span class="count ${all && done === all ? 'green' : ''}">${done} / ${all}</span>`;
    const body = all && done === all
      ? `<div class="empty" style="height:170px"><div class="mascot happy"><b></b><b></b></div><span class="allgood">${t('all_done')}</span></div>`
      : `<div class="list" style="height:170px">${S.tasks.map((x, k) => `<div class="row ${x.done ? 'done' : ''}" data-act="task" data-i="${k}"><span class="check"></span><span class="txt">${esc(x.text)}</span><span class="meta">${x.time}</span></div>`).join('')}</div>`;
    return `<div class="open">${head(t('todo'), count)}${body}<input class="input" data-keep="task" data-act-enter="add-task" placeholder="${t('new_task')}" maxlength="80"></div>`;
  }
  function notesView() {
    const list = S.notes.length
      ? `<div class="list" style="height:112px">${S.notes.map((n, k) => `<div class="row"><span class="idx">${two(S.notes.length - k)}</span><span class="txt">${esc(n.text)}</span><span class="meta">${ago(n.at)}</span></div>`).join('')}</div>`
      : `<div class="empty" style="height:112px"><span class="empty-t">${t('notes_empty')}</span><span class="empty-s">${t('notes_empty2')}</span></div>`;
    return `<div class="open">${head(t('notes'), `<span class="count">${two(S.notes.length)}</span>`)}<input class="input" data-keep="note" data-act-enter="add-note" placeholder="${t('note_ph')}" maxlength="120">${list}</div>`;
  }
  const tileGl = glyph => gl(glyph, 'm');
  function quickView() {
    const tile = (key, glyph, on) => `<button class="tile ${on ? 'on' : ''}" data-act="tile" data-k="${key}">${tileGl(glyph, on)}<span>${t(key)}</span><span>${t(on ? 'on' : 'off')}</span></button>`;
    return `<div class="open">${head(t('quick'))}<div class="tiles">${tile('dnd', 'luna', S.dnd)}${tile('mascot', 'cara', S.mascot)}${tile('minimal', 'linea', S.minimal)}</div>
      <div class="volrow" data-wheel><span class="card-l">${t('volume')}</span>${slider('vol', 5, 14)}<span class="osd-v">${valueOf('vol')}</span></div></div>`;
  }
  // Audio: salida, micrófono, volumen general y una fila por app (tocar el nombre la silencia).
  function audioView() {
    const a = S.audio;
    const outs = T[lang].outputs.map((o, k) => `<button class="${k === a.out ? 'on' : ''}" data-act="out" data-i="${k}">${o}</button>`).join('');
    const apps = a.apps.map((p, k) => `<div class="a-row ${p.muted ? 'muted' : ''}" data-wheel><span class="txt" data-act="appmute" data-i="${k}">${esc(p.name)}</span>${slider('app:' + k, 4, 10)}<span class="meta">${valueOf('app:' + k)}</span></div>`).join('');
    return `<div class="open">${head(t('audio'))}
      <div class="a-top"><div class="card a-out"><span class="card-l">${t('output')}</span><div class="seg">${outs}</div></div>
        <button class="tile a-mic ${a.mic ? 'on' : ''}" data-act="mic">${tileGl('micro', a.mic)}<span>${t('mic')}</span><span>${t(a.mic ? 'mic_on' : 'mic_off')}</span></button></div>
      <div class="volrow" data-wheel><span class="card-l">${t('volume')}</span>${slider('vol', 5, 14)}<span class="osd-v">${valueOf('vol')}</span></div>
      <div class="a-apps">${apps}</div></div>`;
  }

  // ---- Barras de volumen: 20 barras (5 % cada una), como BarSlider en la app ----
  const BARS = 20;
  const appOf = key => S.audio.apps[Number(key.split(':')[1])];
  const level = key => key === 'vol' ? S.volume : appOf(key).muted ? 0 : appOf(key).v;
  const valueOf = key => `<span data-val="${key}">${key !== 'vol' && appOf(key).muted ? t('muted') : level(key) + '%'}</span>`;
  const barsHtml = v => `<b style="width:${v}%"></b>`;
  const slider = (key, min, max) => `<div class="vbars" data-slider="${key}" data-min="${min}" data-max="${max}">${barsHtml(level(key), min, max)}</div>`;
  function setLevel(key, v) {
    v = Math.max(0, Math.min(100, Math.round(v / 5) * 5));
    if (key === 'vol') S.volume = v;
    else { const app = appOf(key); app.v = v; app.muted = false; }
    // Se repintan las barras y el valor sin redibujar la ventana (el arrastre sigue en marcha).
    ncontent.querySelectorAll(`[data-slider="${key}"]`).forEach(el => { el.innerHTML = barsHtml(v, Number(el.dataset.min), Number(el.dataset.max)); });
    ncontent.querySelectorAll(`[data-val="${key}"]`).forEach(el => { el.textContent = `${v}%`; });
    ncontent.querySelector(`[data-slider="${key}"]`)?.closest('.a-row')?.classList.remove('muted');
  }
  // x sobre las barras → la barra que hay debajo (a la izquierda de la primera, 0 %). Con el notch escalado, en proporción.
  function slideTo(el, e) {
    const r = el.getBoundingClientRect(), scale = r.width / el.offsetWidth, pad = 6 * scale;
    const x = e.clientX - r.left - pad;
    setLevel(el.dataset.slider, x < 0 ? 0 : Math.min(BARS, Math.floor(x / (r.width - pad) * BARS) + 1) * (100 / BARS));
  }
  function menuView() {
    // En forma de Dock, como LauncherView: Inicio (la mascota) y una baldosa de color por ventana.
    const badge = p => p === 'clip' && S.clip.length ? `<span class="badge">${S.clip.length}</span>` : '';
    return `<div class="launcher">${head(t('menu'))}
      <div class="dock-area"><div class="dock">${PAGES.map((p, k) => `<button class="dk dk-${p}" data-act="page" data-p="${p}" style="--dl:${30 + k * 25}ms" aria-label="${t(p)}">${p === 'home' ? '<div class="mascot" id="lmascot"><b></b><b></b></div>' : gl(PAGE_GLYPH[p], 'm')}${badge(p)}<span class="dk-l">${t(p)}</span></button>`).join('')}</div></div>
    </div>`;
  }

  // ---- Partes vivas (sin redibujar la vista) ----
  let tick = 0;
  // La mascota: dos ojos que parpadean y miran a un lado (la contenta, en "^", está quieta).
  function eyes(el, happy = false) {
    if (!el || happy) return;
    el.classList.toggle('blink', tick % 18 === 0);
    el.classList.toggle('look', Math.sin(tick / 6) > 0.6);
  }
  function tickParts() {
    eyes(document.getElementById('mascot'));
    eyes(document.getElementById('lmascot'));
    document.querySelectorAll('.mascot.happy').forEach(m => eyes(m, true));
    const eq = document.getElementById('eq');
    if (eq) [...eq.children].forEach((bar, k) => { bar.style.height = (3 + 9 * Math.abs(Math.sin(tick * (0.9 + k * 0.37) + k))).toFixed(1) + 'px'; });
    const oc = document.getElementById('oclock'); if (oc) oc.textContent = clock();
    document.getElementById('tbclock').textContent = clock();
  }
  setInterval(() => { tick++; tickParts(); }, 200);
  setInterval(() => {
    let dirty = false;
    if (S.music.playing) {
      S.music.pos++;
      if (S.music.pos >= track().len) { S.music.i = (S.music.i + 1) % TRACKS.length; S.music.pos = 0; dirty = true; }
      const prog = document.getElementById('mprog'); if (prog) prog.innerHTML = progDots();
      const pos = document.getElementById('mpos'); if (pos) pos.textContent = mmss(S.music.pos);
    }
    const sys = document.getElementById('sys');
    if (sys) {
      const walk = (v, lo, hi, step) => Math.min(hi, Math.max(lo, v + Math.round((Math.random() - 0.5) * step)));
      S.sys = { CPU: walk(S.sys.CPU, 6, 64, 14), GPU: walk(S.sys.GPU, 2, 48, 10), RAM: walk(S.sys.RAM, 48, 62, 2) };
      sys.innerHTML = sysRows();
    }
    if (dirty) render();
  }, 1000);

  // ---- Abrir / cerrar (hover openTime 250 ms, salida closeDelay 400 ms) ----
  let openTimer = 0, closeTimer = 0, pointerInside = false;
  const hint = document.getElementById('hint');
  function open(page = S.last) {
    clearTimeout(openTimer); clearTimeout(closeTimer);
    if (S.peek) { clearTimeout(S.peek.timer); S.peek = null; }
    S.osd = null;
    S.open = true; S.page = page; if (page !== 'menu') S.last = page;
    hint.classList.add('gone');
    render();
  }
  function close() {
    clearTimeout(openTimer); clearTimeout(closeTimer);
    if (!S.open) return;
    if (ncontent.contains(document.activeElement)) document.activeElement.blur();
    S.open = false;
    render();
  }
  const wrap = document.getElementById('nwrap');
  wrap.addEventListener('pointerenter', e => {
    pointerInside = true;
    clearTimeout(closeTimer);
    if (e.pointerType === 'touch' || S.open || S.peek || S.drop) return;
    openTimer = setTimeout(() => open('home'), 250);
  });
  wrap.addEventListener('pointerleave', () => {
    pointerInside = false;
    clearTimeout(openTimer);
    if (!S.open || ncontent.contains(document.activeElement)) return;
    closeTimer = setTimeout(close, 400);
  });
  wrap.addEventListener('click', e => {
    if (!S.open && !S.peek && !e.target.closest('[data-act]')) open();
  });
  wrap.addEventListener('wheel', e => {
    e.preventDefault();
    // Sobre una fila con barras, la rueda sube o baja 5 % en vez de cambiar de ventana.
    const bars = S.open && e.target.closest?.('[data-wheel]')?.querySelector('[data-slider]');
    if (bars) { const key = bars.dataset.slider; setLevel(key, level(key) + (e.deltaY < 0 ? 5 : -5)); return; }
    if (wheelLock) return;
    wheelLock = true; setTimeout(() => { wheelLock = false; }, 250);
    if (!S.open) { if (!S.peek) open('home'); return; }
    const k = PAGES.indexOf(S.page);
    const next = PAGES[(Math.max(0, k) + (e.deltaY > 0 ? 1 : -1) + PAGES.length) % PAGES.length];
    S.page = S.last = next; render();
  }, { passive: false });
  let wheelLock = false;
  document.addEventListener('keydown', e => {
    if (e.key === 'Escape') close();
    if (e.altKey && (e.key === 'n' || e.key === 'N')) { e.preventDefault(); S.open ? close() : open(); }
  });
  // Clic fuera del notch con él abierto: se cierra (como OutsideClickWatcher).
  document.getElementById('desk').addEventListener('pointerdown', e => { if (S.open && !wrap.contains(e.target)) close(); });

  // ---- Acciones dentro del notch ----
  ncontent.addEventListener('click', e => {
    const a = e.target.closest('[data-act]');
    if (!a) return;
    e.stopPropagation();
    const i = Number(a.dataset.i);
    switch (a.dataset.act) {
      case 'menu': S.page = S.page === 'menu' ? S.last : 'menu'; break;
      case 'page': S.page = S.last = a.dataset.p; break;
      case 'play': S.music.playing = !S.music.playing; break;
      case 'prev': S.music.pos > 3 ? (S.music.pos = 0) : (S.music.i = (S.music.i + TRACKS.length - 1) % TRACKS.length, S.music.pos = 0); break;
      case 'next': S.music.i = (S.music.i + 1) % TRACKS.length; S.music.pos = 0; break;
      case 'copy': navigator.clipboard?.writeText(S.clip[i].text).catch(() => {}); flash(t('flash_copied')); return;
      case 'unshelf': S.shelf.splice(i, 1); flash(t('flash_removed')); break;
      case 'task': S.tasks[i].done = !S.tasks[i].done; break;
      case 'tile': S[a.dataset.k] = !S[a.dataset.k]; break;
      case 'out': S.audio.out = i; break;
      case 'mic': S.audio.mic = !S.audio.mic; break;
      case 'appmute': S.audio.apps[i].muted = !S.audio.apps[i].muted; break;
      case 'peek-primary': S.peek.onPrimary?.(); dismissPeek(); return;
      case 'peek-secondary': dismissPeek(); return;
      default: return;
    }
    render();
  });
  // Barras: pulsar fija el valor bajo el ratón y, sin soltar, se arrastra.
  ncontent.addEventListener('pointerdown', e => {
    const el = e.target.closest('[data-slider]');
    if (!el || e.button !== 0) return;
    e.preventDefault();
    el.setPointerCapture(e.pointerId);
    slideTo(el, e);
    const move = ev => slideTo(el, ev);
    const up = () => { el.removeEventListener('pointermove', move); el.removeEventListener('pointerup', up); el.removeEventListener('pointercancel', up); };
    el.addEventListener('pointermove', move);
    el.addEventListener('pointerup', up);
    el.addEventListener('pointercancel', up);
  });
  ncontent.addEventListener('keydown', e => {
    const input = e.target.closest('[data-act-enter]');
    if (!input || e.key !== 'Enter' || !input.value.trim()) return;
    const text = input.value.trim();
    if (input.dataset.actEnter === 'add-task') { const d = new Date(Date.now() + 3600000); S.tasks.push({ text, done: false, time: `${two(d.getHours())}:00` }); }
    else S.notes.unshift({ text, at: Date.now() });
    render();
  });
  ncontent.addEventListener('focusout', () => setTimeout(() => {
    if (S.open && !pointerInside && !ncontent.contains(document.activeElement)) closeTimer = setTimeout(close, 400);
  }));
  // Pasar el ratón por un aviso lo pausa (y la barra se detiene).
  ncontent.addEventListener('pointerenter', () => { if (S.peek) clearTimeout(S.peek.timer); }, true);
  ncontent.addEventListener('pointerleave', e => { if (S.peek && e.target === ncontent) armPeek(1500); }, true);

  // ---- Avisos, indicadores y soltar ----
  let peekId = 0;
  function showPeek(p) {
    if (S.open) { flash(p.head); return; }
    if (S.dnd && p.respectDnd) { flash(t('flash_dnd')); return; }
    if (S.peek) clearTimeout(S.peek.timer);
    S.osd = null;
    S.peek = { ...p, id: ++peekId, ms: 4500 };
    render();
    armPeek(4500);
  }
  function armPeek(ms) { clearTimeout(S.peek.timer); S.peek.timer = setTimeout(dismissPeek, ms); }
  function dismissPeek() { if (!S.peek) return; clearTimeout(S.peek.timer); S.peek = null; render(); }
  let osdTimer = 0;
  function showOsd(kind) {
    if (S.open || S.peek) { render(); return; }
    S.osd = { kind }; render();
    clearTimeout(osdTimer); osdTimer = setTimeout(() => { S.osd = null; render(); }, 1600);
  }
  function addClip(text) {
    text = text.trim().replace(/\s+/g, ' ').slice(0, 200);
    if (!text) return;
    S.clip = [{ text, at: Date.now() }, ...S.clip.filter(c => c.text !== text)].slice(0, 20);
    showPeek({ glyph: 'copias', head: `${t('clip')} · ${t('copied')}`, body: text, primary: t('view'), secondary: t('ok'), onPrimary: () => open('clip') });
    if (S.open) render();
  }
  function addFiles(files) {
    const fmt = b => b < 1024 ? `${b} B` : b < 1048576 ? `${(b / 1024).toFixed(0)} KB` : `${(b / 1048576).toFixed(1)} MB`;
    files.forEach(f => {
      const dot = f.name.lastIndexOf('.');
      S.shelf.unshift({ name: f.name, ext: (dot > 0 ? f.name.slice(dot + 1) : 'DIR').slice(0, 4).toUpperCase(), size: fmt(f.size || 0) });
    });
    S.drop = null;
    flash(t('flash_shelf'));
    open('shelf');
  }

  // Arrastrar archivos reales, enlaces o texto (de los archivos se leen solo nombre y tamaño; nada se sube a ningún sitio).
  const desk = document.getElementById('desk');
  let dragDepth = 0;
  function dragKind(dt) {
    const types = [...(dt?.types || [])];
    if (types.includes('Files')) {
      const files = [...(dt.items || [])].filter(i => i.kind === 'file');
      return { kind: 'files', n: files.length || 1, images: files.length > 0 && files.every(i => i.type.startsWith('image/')), sel: 0 };
    }
    if (types.includes('text/uri-list')) return { kind: 'link', sel: 0 };
    if (types.includes('text/plain')) return { kind: 'text', sel: 0 };
    return null;
  }
  desk.addEventListener('dragenter', e => {
    const kind = dragKind(e.dataTransfer);
    if (!kind) return;
    e.preventDefault();
    if (dragDepth++ === 0) { desk.classList.add('dragging'); S.drop = kind; render(); }
  });
  desk.addEventListener('dragover', e => {
    if (!S.drop) return;
    e.preventDefault();
    // Solo se cambia la zona encendida: redibujar quitaría el elemento bajo el ratón a mitad del arrastre.
    const r = nshape.getBoundingClientRect(), sel = zones(S.drop).length > 1 && e.clientX >= r.left + r.width / 2 ? 1 : 0;
    if (sel === S.drop.sel) return;
    S.drop.sel = sel;
    ncontent.querySelectorAll('.dz-z').forEach((z, k) => z.classList.toggle('on', k === sel));
  });
  desk.addEventListener('dragleave', () => {
    if (!S.drop || --dragDepth > 0) return;
    dragDepth = 0; desk.classList.remove('dragging'); S.drop = null; render();
  });
  desk.addEventListener('drop', e => {
    const d = S.drop;
    if (!d) return;
    e.preventDefault(); dragDepth = 0; desk.classList.remove('dragging');
    const zone = zones(d)[d.sel];
    const text = e.dataTransfer.getData('text/plain').trim();
    const url = e.dataTransfer.getData('text/uri-list').split(/\r?\n/).find(l => l && !l.startsWith('#'))?.trim() || text;
    S.drop = null;
    switch (zone) {
      case 'shelf': addFiles([...e.dataTransfer.files]); return;
      case 'ocr': flash(t('flash_ocr')); break;
      case 'resize': flash(t('flash_resize', { n: d.n })); break;
      case 'note':
        if (!text) break;
        S.notes.unshift({ text: text.replace(/\s+/g, ' ').slice(0, 300), at: Date.now() });
        flash(t('flash_note')); open('notes'); return;
      case 'task': {
        if (!text) break;
        const due = new Date(Date.now() + 3600000);
        S.tasks.push({ text: text.split('\n')[0].slice(0, 160), done: false, time: `${two(due.getHours())}:00` });
        flash(t('flash_task')); open('todo'); return;
      }
      // La app lee el título de la página; aquí (sin conectar con otras webs) se usa el dominio, como hace la app si no lo encuentra.
      case 'title': {
        let host = url;
        try { host = new URL(url).hostname; } catch { /* no es una URL */ }
        S.notes.unshift({ text: `${host} · ${url}`, at: Date.now() });
        flash(t('flash_note')); open('notes'); return;
      }
      case 'clip': addClip(url); break;
    }
    render();
  });

  // Ctrl + C de cualquier texto de la página → Portapapeles del notch.
  document.addEventListener('copy', () => {
    const text = String(window.getSelection() || '');
    if (text.trim()) setTimeout(() => addClip(text), 0);
  });

  // ---- Panel de simulación ----
  document.querySelector('.panel-grid').addEventListener('click', e => {
    const b = e.target.closest('[data-sim]');
    if (!b) return;
    switch (b.dataset.sim) {
      case 'music': S.music.playing = !S.music.playing; render(); break;
      case 'notify': {
        const [head, body, app] = T[lang].notify[S.notifyIndex++ % T[lang].notify.length];
        showPeek({ glyph: /^cal/i.test(head) ? 'calendario' : 'mensaje', head, body, primary: t('open'), secondary: t('later'), respectDnd: true, onPrimary: () => flash(t('flash_open', { app })) });
        break;
      }
      case 'voldown': S.volume = Math.max(0, S.volume - 10); showOsd('volume'); break;
      case 'volup': S.volume = Math.min(100, S.volume + 10); showOsd('volume'); break;
      case 'shot': {
        // Como ScreenshotWatcher: la captura que guarda Windows entra en la bandeja con un aviso.
        const d = new Date(), name = t('shot_name', { time: `${d.getFullYear()}-${two(d.getMonth() + 1)}-${two(d.getDate())} ${two(d.getHours())}${two(d.getMinutes())}${two(d.getSeconds())}` });
        S.shelf.unshift({ name, ext: 'PNG', size: '214 KB' });
        showPeek({ glyph: 'bandeja', head: `${t('shelf')} · ${t('screenshot')}`, body: name, primary: t('view'), secondary: t('ok'), onPrimary: () => open('shelf') });
        if (S.open) render();
        break;
      }
      case 'copy': addClip(t('copy_text')); break;
      case 'file': addFiles([{ name: lang === 'es' ? 'Presupuesto 2026.pdf' : 'Budget 2026.pdf', size: 482113 }]); break;
      case 'toggle': S.open ? close() : open(); break;
    }
  });

  // ---- Aviso breve en el escritorio ----
  const flashEl = document.getElementById('flash');
  let flashTimer = 0;
  function flash(text) {
    flashEl.textContent = text; flashEl.classList.add('on');
    clearTimeout(flashTimer); flashTimer = setTimeout(() => flashEl.classList.remove('on'), 1800);
  }

  // ---- Escala: el notch abierto (580 px) siempre cabe en el escritorio ----
  new ResizeObserver(() => {
    wrap.style.setProperty('--ns', Math.min(1, (desk.clientWidth - 32) / 620).toFixed(3));
  }).observe(desk);

  // ---- Idioma ----
  function applyLang() {
    document.documentElement.lang = lang;
    document.querySelectorAll('[data-t]').forEach(el => { el.textContent = t(el.dataset.t); });
    const b = document.getElementById('lang');
    b.textContent = lang === 'en' ? '[ES]' : '[EN]';
    b.setAttribute('aria-label', lang === 'en' ? 'Español' : 'English');
    render();
  }
  document.getElementById('lang').addEventListener('click', () => {
    lang = lang === 'en' ? 'es' : 'en';
    try { localStorage.setItem('parpa.lang', lang); } catch { /* sin almacenamiento */ }
    seedTasks();
    applyLang();
  });

  document.querySelectorAll('.sim [data-g]').forEach(el => { el.outerHTML = gl(el.dataset.g, 'm'); });
  applyLang();
})();
