// Demo de Parpa en el navegador: la misma máquina de estados que la app (Soltar > Abierto > Aviso >
// Indicador > Actividad > Reposo), con las medidas y tiempos de data/tokens.json. Nada sale del navegador.
(() => {
  const ON = '#f2f2f0', OFF = '#1d1d20';
  const reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;

  // ---- Textos ----
  const T = {
    es: {
      nav_back: '← INICIO', nav_download: 'DESCARGAR', eyebrow: 'PRUÉBALO EN EL NAVEGADOR', title: 'Juega con el notch.',
      lead: 'Es la misma isla que verás arriba de tu pantalla en Windows, con sus medidas y animaciones. Pasa el ratón por encima, abre sus ventanas y usa los botones de abajo para simular lo que pasa en tu PC.',
      hint: 'PASA EL RATÓN POR EL NOTCH', simulate: 'SIMULAR', local: 'NADA SALE DE TU NAVEGADOR',
      sim_play: 'PONER MÚSICA', sim_pause: 'PAUSAR MÚSICA', sim_notify: 'RECIBIR UN AVISO', sim_voldown: 'VOLUMEN −', sim_volup: 'VOLUMEN +',
      sim_bright: 'BRILLO', sim_copy: 'COPIAR UN TEXTO', sim_file: 'SOLTAR UN ARCHIVO', sim_toggle: 'WIN + ALT + N',
      tip1: 'Pasa el ratón por el notch para abrirlo; al salir se cierra.', tip2: 'La rueda del ratón cambia de ventana. Esc lo cierra.',
      tip3: 'Arrastra archivos, un enlace o un texto de esta página hasta el notch: según en qué mitad lo sueltes, hace una cosa u otra.', tip4: 'Selecciona cualquier texto de esta página y pulsa Ctrl + C: aparecerá en el Portapapeles.',
      tip5: 'Las barras de volumen se pulsan o se arrastran. En Audio, toca el nombre de una app para silenciarla.',
      footer: 'CÓDIGO ABIERTO CON LICENCIA MIT', footer_home: 'WEB', footer_download: 'DESCARGAR',
      home: 'INICIO', clip: 'PORTAPAPELES', shelf: 'BANDEJA', todo: 'TAREAS', notes: 'NOTAS', audio: 'AUDIO', quick: 'AJUSTES RÁPIDOS', menu: 'MENÚ',
      output: 'SALIDA', outputs: ['AURICULARES', 'ALTAVOCES'], mic: 'MICRÓFONO', mic_on: 'ACTIVO', mic_off: 'SILENCIADO', vol: 'VOL', muted: 'MUDO',
      sim_shot: 'HACER UNA CAPTURA', screenshot: 'CAPTURA AÑADIDA', shot_name: 'Captura de pantalla {time}.png',
      z_shelf: 'A LA BANDEJA', z_ocr: 'EXTRAER TEXTO', z_resize: 'REDIMENSIONAR', z_note: 'CREAR NOTA', z_task: 'CREAR TAREA', z_title: 'GUARDAR CON TÍTULO', z_clip: 'AL PORTAPAPELES',
      flash_ocr: 'EN LA APP, SU TEXTO IRÍA AL PORTAPAPELES', flash_resize: 'EN LA APP, {n} IMÁGENES A 1920 PX EN LA BANDEJA', flash_note: '✓ NOTA CREADA', flash_task: '✓ TAREA CREADA',
      music: 'MÚSICA', system: 'SISTEMA', nothing: 'Nada sonando', nothing_d: 'Dale al play en cualquier app y aparecerá aquí.',
      now: 'AHORA', later: 'MÁS TARDE', open: 'ABRIR', view: 'VER', ok: 'OK', copied: 'COPIADO',
      clip_empty: 'COPIA ALGO CON', clip_empty2: 'Y APARECERÁ AQUÍ', shelf_empty: 'ARRASTRA ARCHIVOS HASTA EL NOTCH', shelf_empty2: 'SE GUARDAN AQUÍ HASTA QUE LOS SAQUES',
      drop: 'SUELTA PARA GUARDAR', drop2: 'EN LA BANDEJA', new_task: '+ NUEVA TAREA', all_done: 'TODO HECHO', note_ph: 'Escribe una nota y pulsa Intro…',
      notes_empty: 'SIN NOTAS TODAVÍA', notes_empty2: 'ESCRIBE ARRIBA O ARRASTRA TEXTO AL NOTCH',
      dnd: 'NO MOLESTAR', mascot: 'MASCOTA', minimal: 'MINIMAL', on: 'ACTIVADO', off: 'NO', volume: 'VOLUMEN', speakers: 'ALTAVOCES', brightness: 'BRILLO', screen: 'PANTALLA',
      flash_copied: '✓ COPIADO AL PORTAPAPELES', flash_open: 'SE ABRIRÍA {app}', flash_dnd: 'NO MOLESTAR: EL AVISO ESPERA', flash_shelf: '✓ GUARDADO EN LA BANDEJA', flash_removed: 'QUITADO DE LA BANDEJA',
      copy_text: 'Reunión movida a las 17:30, sala 2',
      tasks: ['Enviar el informe', 'Llamar al dentista', 'Comprar café'],
      notify: [['WHATSAPP · ANA', '¿Vienes a la cena del viernes? Reservé a las 21:00.', 'WhatsApp'], ['DISCORD · #GENERAL', 'Leo: ¿alguien se apunta a una partida esta noche?', 'Discord'], ['CALENDARIO', 'Revisión del proyecto en 10 minutos.', 'Calendario']],
    },
    en: {
      nav_back: '← HOME', nav_download: 'DOWNLOAD', eyebrow: 'TRY IT IN YOUR BROWSER', title: 'Play with the notch.',
      lead: 'It’s the same island you’ll see at the top of your screen on Windows, with its real sizes and animations. Hover it, open its windows and use the buttons below to simulate what happens on your PC.',
      hint: 'HOVER THE NOTCH', simulate: 'SIMULATE', local: 'NOTHING LEAVES YOUR BROWSER',
      sim_play: 'PLAY MUSIC', sim_pause: 'PAUSE MUSIC', sim_notify: 'GET A NOTIFICATION', sim_voldown: 'VOLUME −', sim_volup: 'VOLUME +',
      sim_bright: 'BRIGHTNESS', sim_copy: 'COPY SOME TEXT', sim_file: 'DROP A FILE', sim_toggle: 'WIN + ALT + N',
      tip1: 'Hover the notch to open it; it closes when you leave.', tip2: 'The mouse wheel switches windows. Esc closes it.',
      tip3: 'Drag files, a link or some text from this page onto the notch: which half you drop it on decides what happens.', tip4: 'Select any text on this page and press Ctrl + C: it shows up in the Clipboard.',
      tip5: 'Volume bars can be clicked or dragged. In Audio, tap an app’s name to mute it.',
      footer: 'OPEN SOURCE UNDER THE MIT LICENSE', footer_home: 'WEBSITE', footer_download: 'DOWNLOAD',
      home: 'HOME', clip: 'CLIPBOARD', shelf: 'SHELF', todo: 'TASKS', notes: 'NOTES', audio: 'AUDIO', quick: 'QUICK SETTINGS', menu: 'MENU',
      output: 'OUTPUT', outputs: ['HEADPHONES', 'SPEAKERS'], mic: 'MICROPHONE', mic_on: 'ON', mic_off: 'MUTED', vol: 'VOL', muted: 'MUTED',
      sim_shot: 'TAKE A SCREENSHOT', screenshot: 'SCREENSHOT ADDED', shot_name: 'Screenshot {time}.png',
      z_shelf: 'TO SHELF', z_ocr: 'EXTRACT TEXT', z_resize: 'RESIZE', z_note: 'NEW NOTE', z_task: 'NEW TASK', z_title: 'SAVE WITH TITLE', z_clip: 'TO CLIPBOARD',
      flash_ocr: 'IN THE APP, ITS TEXT WOULD GO TO THE CLIPBOARD', flash_resize: 'IN THE APP, {n} IMAGES AT 1920 PX ON THE SHELF', flash_note: '✓ NOTE CREATED', flash_task: '✓ TASK CREATED',
      music: 'MUSIC', system: 'SYSTEM', nothing: 'Nothing playing', nothing_d: 'Press play in any app and it shows up here.',
      now: 'NOW', later: 'LATER', open: 'OPEN', view: 'VIEW', ok: 'OK', copied: 'COPIED',
      clip_empty: 'COPY SOMETHING WITH', clip_empty2: 'AND IT SHOWS UP HERE', shelf_empty: 'DRAG FILES ONTO THE NOTCH', shelf_empty2: 'THEY STAY HERE UNTIL YOU TAKE THEM OUT',
      drop: 'DROP TO KEEP', drop2: 'ON THE SHELF', new_task: '+ NEW TASK', all_done: 'ALL DONE', note_ph: 'Write a note and press Enter…',
      notes_empty: 'NO NOTES YET', notes_empty2: 'TYPE ABOVE OR DRAG TEXT ONTO THE NOTCH',
      dnd: 'DO NOT DISTURB', mascot: 'MASCOT', minimal: 'MINIMAL', on: 'ON', off: 'OFF', volume: 'VOLUME', speakers: 'SPEAKERS', brightness: 'BRIGHTNESS', screen: 'DISPLAY',
      flash_copied: '✓ COPIED TO CLIPBOARD', flash_open: '{app} WOULD OPEN', flash_dnd: 'DO NOT DISTURB: IT WAITS', flash_shelf: '✓ KEPT ON THE SHELF', flash_removed: 'REMOVED FROM THE SHELF',
      copy_text: 'Meeting moved to 5:30 pm, room 2',
      tasks: ['Send the report', 'Call the dentist', 'Buy coffee'],
      notify: [['WHATSAPP · ANA', 'Coming to dinner on Friday? I booked for 9 pm.', 'WhatsApp'], ['DISCORD · #GENERAL', 'Leo: anyone up for a game tonight?', 'Discord'], ['CALENDAR', 'Project review in 10 minutes.', 'Calendar']],
    },
  };
  let lang;
  try { lang = new URLSearchParams(location.search).get('lang') || localStorage.getItem('parpa.lang'); } catch { /* sin almacenamiento */ }
  if (lang !== 'es' && lang !== 'en') lang = (navigator.language || 'en').toLowerCase().startsWith('es') ? 'es' : 'en';
  const t = (k, vars = {}) => String(T[lang][k] ?? k).replace(/\{(\w+)\}/g, (_, v) => vars[v] ?? '');

  // ---- Glifos (data/glyphs.json) ----
  const G = {
    musica: ['..XXXXX', '..XXXXX', '..X...X', '..X...X', 'XXX.XXX', 'XXX.XXX', '.......'],
    mensaje: ['.XXXXX.', 'XXXXXXX', 'X.X.X.X', 'XXXXXXX', '.XXXXX.', '.XX....', '.X.....'],
    sonido: ['...X...', '..XX..X', 'XXXX.X.', 'XXXX.X.', 'XXXX.X.', '..XX..X', '...X...'],
    brillo: ['X..X..X', '.......', '..XXX..', 'X.XXX.X', '..XXX..', '.......', 'X..X..X'],
    bandeja: ['XXX....', 'XXXXXXX', 'X.....X', 'X.....X', 'X.....X', 'XXXXXXX', '.......'],
    copias: ['XXXXX..', 'X...X..', 'X.XXXXX', 'X.X...X', 'XXX...X', '..X...X', '..XXXXX'],
    tareas: ['XX.XXXX', 'XX.....', '.......', 'XX.XXXX', 'XX.....', '.......', 'XX.XXX.'],
    notas: ['XXXXXXX', 'XXXXXXX', 'X.....X', 'X.XXX.X', 'X....XX', 'X...XX.', 'XXXXX..'],
    inicio: ['..XXX..', '.XXXXX.', 'XXXXXXX', 'XXXXXXX', 'XXXXXXX', '.XXXXX.', '..XXX..'],
    general: ['X..X..X', '.XXXXX.', '.XX.XX.', 'XX...XX', '.XX.XX.', '.XXXXX.', 'X..X..X'],
    menu: ['X..X..X', '.......', '.......', 'X..X..X', '.......', '.......', 'X..X..X'],
    luna: ['..XXX..', '.XX....', 'XX.....', 'XX.....', 'XX.....', '.XX....', '..XXX..'],
    cara: ['.......', 'XX...XX', 'XX...XX', '.......', '.X...X.', '..XXX..', '.......'],
    linea: ['.......', '.......', '.......', 'XXXXXXX', '.......', '.......', '.......'],
    calendario: ['X.....X', 'XXXXXXX', 'X.....X', 'X.X.X.X', 'X.....X', 'X.X.X.X', 'XXXXXXX'],
    micro: ['..XXX..', '..XXX..', 'X.XXX.X', 'X.XXX.X', '.X...X.', '..XXX..', '..XXX..'],
  };
  const gl = (name, size = 's', off = 'transparent') =>
    `<span class="gl gl-${size}">${G[name].join('').split('').map(c => `<i style="background:${c === 'X' ? ON : off}"></i>`).join('')}</span>`;
  const esc = s => String(s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]);
  const two = n => String(n).padStart(2, '0');
  const mmss = s => `${Math.floor(s / 60)}:${two(Math.floor(s % 60))}`;
  const clock = () => { const d = new Date(); return `${two(d.getHours())}:${two(d.getMinutes())}`; };

  // ---- Medidas (tokens.json › notch, radius) ----
  const SIZE = { idle: [120, 34], music: [290, 34], minimal: [96, 24], osd: [340, 40], peek: [420, 112], drop: [420, 84],
    menu: [580, 264], home: [580, 200], clip: [580, 214], shelf: [580, 272], todo: [580, 280], notes: [580, 222], audio: [580, 300], quick: [580, 246] };
  const PAGES = ['home', 'clip', 'shelf', 'todo', 'notes', 'audio', 'quick'];
  const PAGE_GLYPH = { home: 'inicio', clip: 'copias', shelf: 'bandeja', todo: 'tareas', notes: 'notas', audio: 'sonido', quick: 'general', menu: 'menu' };
  const TRACKS = [
    { title: 'LUZ DE NEÓN', artist: 'Parpa FM', len: 208, art: 'linear-gradient(135deg,#e5383b,#5b1622)' },
    { title: 'PUNTOS SUSPENSIVOS', artist: 'Matriz', len: 187, art: 'linear-gradient(135deg,#2f7bf5,#14284f)' },
    { title: 'ISLA', artist: 'Notch', len: 231, art: 'linear-gradient(135deg,#00a64f,#0d3a22)' },
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
    return `<div class="c-idle"><div class="mascot" id="mascot">${'<i></i>'.repeat(65)}</div></div>`;
  }
  const track = () => TRACKS[S.music.i];
  function musicView() {
    return `<div class="c-row">${gl('musica')}<span class="c-label">${esc(track().title)} <span class="c-sub">${esc(track().artist.toUpperCase())}</span></span><div class="eq" id="eq">${'<i></i>'.repeat(5)}</div></div>`;
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
    const lit = Math.round(value / 100 * 16);
    const bars = Array.from({ length: 16 }, (_, k) => `<i class="${k < lit ? 'on' : ''}" style="height:${4 + k * 0.75}px"></i>`).join('');
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
    const m = S.music, tr = track();
    const music = m.playing || m.pos > 42 || m.i > 0
      ? `<div class="m-top"><div class="m-art" style="--art:${tr.art}">${gl('musica', 'm')}</div><div class="m-txt"><span class="m-title">${esc(tr.title)}</span><span class="m-src">${esc(tr.artist.toUpperCase())}</span></div></div>
         <div class="m-prog" id="mprog">${progDots()}</div>
         <div class="m-ctl"><span id="mpos">${mmss(m.pos)}</span><span class="m-btns"><button data-act="prev">◂◂</button><button data-act="play">${m.playing ? '▮▮' : '▸'}</button><button data-act="next">▸▸</button></span><span>${mmss(tr.len)}</span></div>
         <div class="m-vol" data-wheel><span>${t('vol')}</span>${slider('app:0', 3, 8)}${valueOf('app:0')}</div>`
      : `<span class="card-l">${t('music')}</span><div class="m-empty"><span>${t('nothing')}</span><span>${t('nothing_d')}</span></div><button class="np np-on" data-act="play">▸ PLAY</button>`;
    return `<div class="open">${head(t('home'))}
      <div class="cards">
        <div class="card">${music}</div>
        <div class="card"><span class="card-l">${t('system')}</span><div id="sys">${sysRows()}</div></div>
      </div></div>`;
  }
  // CPU, GPU y RAM: en la app son las de tu PC; aquí, valores de ejemplo que se mueven cada segundo.
  function sysRows() {
    const bar = v => `<span class="s-bar">${Array.from({ length: 10 }, (_, k) => `<i class="${k < Math.round(v / 10) ? 'on' : ''}"></i>`).join('')}</span>`;
    return Object.entries(S.sys).map(([k, v]) => `<div class="s-row"><span>${k}</span>${bar(v)}<b>${v}%</b></div>`).join('');
  }
  function progDots() {
    const n = 28, lit = Math.round(S.music.pos / track().len * n);
    return Array.from({ length: n }, (_, k) => `<i class="${k < lit ? 'on' : ''}"></i>`).join('');
  }
  const ago = at => {
    const min = Math.floor((Date.now() - at) / 60000);
    return min < 1 ? t('now') : `${min} MIN`;
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
      : `<div class="empty" style="height:168px"><div class="mascot happy">${'<i></i>'.repeat(65)}</div><span class="empty-t">${t('shelf_empty')}</span><span class="empty-s">${t('shelf_empty2')}</span></div>`;
    return `<div class="open">${head(t('shelf'), S.shelf.length ? `<span class="count">${two(S.shelf.length)}</span>` : '')}${body}</div>`;
  }
  function todoView() {
    const done = S.tasks.filter(x => x.done).length, all = S.tasks.length;
    const count = `<span class="count ${all && done === all ? 'green' : ''}">${done} / ${all}</span>`;
    const body = all && done === all
      ? `<div class="empty" style="height:170px"><div class="mascot happy">${'<i></i>'.repeat(65)}</div><span class="allgood">${t('all_done')}</span></div>`
      : `<div class="list" style="height:170px">${S.tasks.map((x, k) => `<div class="row ${x.done ? 'done' : ''}" data-act="task" data-i="${k}"><span class="check"></span><span class="txt">${esc(x.text)}</span><span class="meta">${x.time}</span></div>`).join('')}</div>`;
    return `<div class="open">${head(t('todo'), count)}${body}<input class="input" data-keep="task" data-act-enter="add-task" placeholder="${t('new_task')}" maxlength="80"></div>`;
  }
  function notesView() {
    const list = S.notes.length
      ? `<div class="list" style="height:112px">${S.notes.map((n, k) => `<div class="row"><span class="idx">${two(S.notes.length - k)}</span><span class="txt">${esc(n.text)}</span><span class="meta">${ago(n.at)}</span></div>`).join('')}</div>`
      : `<div class="empty" style="height:112px"><span class="empty-t">${t('notes_empty')}</span><span class="empty-s">${t('notes_empty2')}</span></div>`;
    return `<div class="open">${head(t('notes'), `<span class="count">${two(S.notes.length)}</span>`)}<input class="input" data-keep="note" data-act-enter="add-note" placeholder="${t('note_ph')}" maxlength="120">${list}</div>`;
  }
  const tileGl = (glyph, on) => gl(glyph, 's', 'transparent').replaceAll(ON, on ? '#0b0b0c' : ON);
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
  const barsHtml = (v, min, max) => Array.from({ length: BARS }, (_, k) => {
    const on = k < Math.round(v / (100 / BARS));
    return `<i class="${on ? 'on' : ''}" style="height:${on ? min + Math.round(k / (BARS - 1) * (max - min)) : min}px"></i>`;
  }).join('');
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
    // En arco, como LauncherView: de 180° a 360° (radio 192 × 94 alrededor de la píldora).
    const pages = PAGES.filter(p => p !== 'home');
    const bubbles = pages.map((p, k) => {
      const a = (180 + k * 180 / (pages.length - 1)) * Math.PI / 180;
      return [p, Math.round(290 + Math.cos(a) * 192), Math.round(120 - Math.sin(a) * 94)];
    });
    return `<div class="launcher">${head(t('menu'))}
      <button class="l-pill" data-act="page" data-p="home" aria-label="${t('home')}"><div class="mascot" id="lmascot">${'<i></i>'.repeat(65)}</div></button>
      ${bubbles.map(([p, x, y], k) => `<button class="bub" data-act="page" data-p="${p}" style="left:${x}px;top:${y}px;--dl:${40 + k * 30}ms;--fx:${(290 - x) * 0.7}px;--fy:${(132 - y) * 0.7}px" aria-label="${t(p)}">${gl(PAGE_GLYPH[p], 'm')}${p === 'clip' && S.clip.length ? `<span class="badge">${S.clip.length}</span>` : ''}<span class="bub-l">${t(p)}</span></button>`).join('')}
    </div>`;
  }

  // ---- Partes vivas (sin redibujar la vista) ----
  let tick = 0;
  function eyes(grid, happy = false) {
    if (!grid) return;
    const blink = !happy && tick % 18 === 0, look = happy ? 0 : Math.sin(tick / 6) > 0.6 ? 1 : 0;
    [...grid.children].forEach((dot, n) => {
      const r = Math.floor(n / 13), c = n % 13;
      // Contenta: ojos en "^" (arriba los dos centrales, debajo los de fuera).
      const on = happy
        ? [3, 8].some(x => (r === 1 && (c === x || c === x + 1)) || (r === 2 && (c === x - 1 || c === x + 2)))
        : [3, 8].some(x => (c === x + look || c === x + 1 + look) && r >= 1 && r <= 3 && (!blink || r === 2));
      dot.style.background = on ? ON : '#1b1b1e';
    });
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
        showPeek({ glyph: head.startsWith('CAL') ? 'calendario' : 'mensaje', head, body, primary: t('open'), secondary: t('later'), respectDnd: true, onPrimary: () => flash(t('flash_open', { app: app.toUpperCase() })) });
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
