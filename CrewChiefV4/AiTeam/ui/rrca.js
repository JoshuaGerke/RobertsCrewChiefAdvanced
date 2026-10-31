// Oberfläche nach Robs Designentwurf. Alle Werte kommen aus der App (RrcaForm), nichts ist Beispieldaten.
(() => {
  const root = document.getElementById('rrca-design');
  const $ = s => root.querySelector(s), $$ = s => Array.from(root.querySelectorAll(s));
  const bridge = window.chrome && window.chrome.webview;
  const send = (cmd, data) => { if (bridge) bridge.postMessage(Object.assign({ cmd }, data || {})); };
  const members = ['ben', 'mik', 'sara'];
  const idle = { ben: 'Beobachtet die Strategie', mik: 'Beobachtet das Fahrzeug', sara: 'Hat die Seiten im Blick' };
  let state = { roles: { ben: true, mik: true, sara: true } }, noticeTimer;

  function notify(text) {
    const n = $('#rrca-notice'); n.textContent = text; n.hidden = false;
    clearTimeout(noticeTimer); noticeTimer = setTimeout(() => { n.hidden = true; }, 12000);
  }
  function page(name) {
    $$('[data-page]').forEach(b => b.setAttribute('aria-selected', b.dataset.page === name ? 'true' : 'false'));
    $$('[role=tabpanel]').forEach(p => { p.hidden = p.id !== 'rrca-' + name; });
  }
  function syncRoles(speaking) {
    $$('[data-member]').forEach(i => { i.checked = !!state.roles[i.dataset.member]; });
    $$('[data-card]').forEach(c => {
      const m = c.dataset.card;
      c.classList.toggle('off', !state.roles[m]);
      c.classList.toggle('sending', state.roles[m] && speaking === m);
    });
    $$('[data-status]').forEach(el => {
      const m = el.dataset.status, on = state.roles[m];
      el.textContent = !on ? 'Ausgeschaltet' : speaking === m ? '● Funkt gerade' : m === 'sara' && state.voiceReadiness && !state.voiceReadiness.ready ? 'Noch nicht bereit, siehe Hinweis oben' : idle[m];
      el.classList.toggle('lime', on && speaking === m);
    });
  }

  function fillSelect(sel, options, value) {
    sel.innerHTML = '';
    options.forEach(o => { const opt = document.createElement('option'); opt.value = o.id; opt.textContent = o.name; sel.appendChild(opt); });
    if (value != null && !options.some(o => o.id === value)) {
      const opt = document.createElement('option'); opt.value = value; opt.textContent = value; sel.appendChild(opt);
    }
    if (value != null) sel.value = value;
  }
  function fillVoices(list, current) {
    members.forEach(m => {
      const sel = $('[data-voice-select=' + m + ']');
      const opts = list && list.length ? list : [{ id: current[m], name: 'Gespeicherte Stimme' }];
      fillSelect(sel, opts, current[m]);
    });
  }

  function buildSchedule(topics) {
    const box = $('#rrca-schedule'); box.innerHTML = '';
    $('#rrca-topic-count').textContent = topics.length + ' Themen';
    topics.forEach(t => {
      const row = document.createElement('div'); row.className = 'schedule-row'; row.dataset.topic = t.id; row.title = t.hint;
      row.innerHTML = '<span></span><label class="switch"><input type="checkbox" data-part="on"></label>' +
        '<input type="number" min="' + t.minimum + '" max="3600" data-part="seconds"><input type="number" min="0" max="300" data-part="minus"><input type="number" min="0" max="300" data-part="plus">';
      row.querySelector('span').textContent = t.label;
      row.querySelector('[data-part=on]').checked = t.on;
      row.querySelector('[data-part=seconds]').value = t.seconds;
      row.querySelector('[data-part=minus]').value = t.minus;
      row.querySelector('[data-part=plus]').value = t.plus;
      const update = () => { const on = row.querySelector('[data-part=on]').checked; row.classList.toggle('inactive', !on); row.querySelectorAll('input[type=number]').forEach(i => { i.disabled = !on; }); };
      row.querySelector('[data-part=on]').addEventListener('change', update); update();
      box.appendChild(row);
    });
  }

  function applyInit(d) {
    state = d;
    $('#rrca-version').textContent = d.version;
    fillSelect($('#rrca-game'), d.games.map(g => ({ id: g, name: g })), null);
    $('#rrca-auto').checked = d.autoDetect;
    syncRoles(null);
    fillVoices(null, d.voices);
    fillSelect($('#rrca-mic'), [{ id: '', name: 'Windows-Standardgerät' }].concat(d.microphones.map(m => ({ id: m, name: m }))), d.microphone);
    $('#rrca-ptt').value = d.ptt;
    $('#rrca-radio-effect').checked = d.radioEffect;
    $('#rrca-recorder').checked = d.recorder;
    $('#rrca-diag-status').textContent = d.recorderStatus;
    $('#key-openrouter').textContent = d.keys.openrouter || 'kein Schlüssel hinterlegt';
    $('#key-elevenlabs').textContent = d.keys.elevenlabs || 'kein Schlüssel hinterlegt';
    if (!d.keys.openrouter || !d.keys.elevenlabs) $('#rrca-keys').open = true;
    $$('[data-setting]').forEach(i => {
      const v = d.settings[i.dataset.setting];
      if (i.tagName === 'SELECT' && v != null && !Array.from(i.options).some(o => o.value === String(v))) {
        const opt = document.createElement('option'); opt.value = v; opt.textContent = v; i.appendChild(opt);
      }
      i.value = v == null ? '' : v;
    });
    buildSchedule(d.topics);
  }

  function applyLive(d) {
    state.voiceReadiness = d.voiceReadiness;
    if (typeof d.recorder === "boolean") $('#rrca-recorder').checked = d.recorder;
    if (d.recorderStatus != null) $('#rrca-diag-status').textContent = d.recorderStatus;
    $('#rrca-conn').textContent = d.connected ? d.game : (d.sourceStatus || 'Kein Spiel verbunden');
    const run = $('#rrca-run'), game = $('#rrca-game');
    run.textContent = d.running ? 'Stopp' : 'Start';
    run.classList.toggle('primary', !d.running); run.classList.toggle('danger', !!d.running);
    game.disabled = !!d.running;
    if (d.game && document.activeElement !== game) game.value = d.game;
    $('#rrca-conn-dot').classList.toggle('off', !d.connected);
    $('#rrca-rail-hint').textContent = d.connected ? 'Live-Daten vom Spiel' : (d.sourceStatus ? 'Quelle erreichbar · Fahrdaten fehlen' : 'Wartet auf das Spiel');
    $('#rrca-where').textContent = d.connected ? [d.game, d.track, d.session].filter(Boolean).join(' · ') : (d.sourceStatus || 'Kein Spiel verbunden');
    $('#rrca-sub').textContent = d.connected ? [d.driver, d.carClass, d.lastLap ? 'Letzte Runde ' + d.lastLap : ''].filter(Boolean).join(' · ')
      : (d.sourceStatus ? d.sourceHint : 'Spiel starten und ins Auto setzen, die Crew legt von selbst los.');
    $('#m-pos').textContent = d.position > 0 ? 'P' + d.position : '–';
    $('#m-pos-sub').textContent = d.position > 0 && d.cars > 0 ? 'von ' + d.cars + ' in deiner Klasse' : ' ';
    $('#m-lap-label').textContent = d.timed ? 'Restzeit' : 'Runde';
    $('#m-lap').textContent = d.lapValue || '–';
    $('#m-lap-sub').textContent = d.lapSub || ' ';
    $('#m-gap').textContent = d.gapValue || '–';
    $('#m-gap-sub').textContent = d.gapSub || ' ';

    const warn = d.warning;
    $('#rrca-warn').hidden = !warn;
    if (warn) {
      $('#rrca-warn-text').textContent = warn.text;
      const b = $('#rrca-warn-button'); b.hidden = !warn.cmd; b.textContent = warn.button || ''; b.dataset.cmd = warn.cmd || '';
    }

    const radio = d.radio || [];
    const tl = $('#rrca-timeline'); tl.innerHTML = '';
    if (!radio.length) tl.innerHTML = '<div class="timeline empty"><span>Noch nichts gefunkt.</span></div>';
    radio.slice().reverse().forEach(r => {
      const row = document.createElement('div'); row.className = 'timeline';
      row.innerHTML = '<time></time><b></b><span></span>';
      row.children[0].textContent = r.time; row.children[1].textContent = r.member.toUpperCase(); row.children[2].textContent = r.text;
      tl.appendChild(row);
    });
    const last = radio[radio.length - 1];
    $('#rrca-speaker').textContent = last ? last.member + (d.speaking ? ' · funkt gerade' : ' · zuletzt ' + last.time) : 'Funk';
    $('#rrca-quote').textContent = last ? last.text : 'Noch kein Funkspruch.';
    syncRoles(d.speaking);
  }

  function readTopics() {
    return $$('#rrca-schedule .schedule-row').map(r => ({
      id: r.dataset.topic, on: r.querySelector('[data-part=on]').checked,
      seconds: Number(r.querySelector('[data-part=seconds]').value), minus: Number(r.querySelector('[data-part=minus]').value), plus: Number(r.querySelector('[data-part=plus]').value)
    }));
  }

  $$('[data-page]').forEach(b => b.addEventListener('click', () => page(b.dataset.page)));
  $$('[data-goto]').forEach(b => b.addEventListener('click', () => page(b.dataset.goto)));
  $$('[data-member]').forEach(i => i.addEventListener('change', () => {
    state.roles[i.dataset.member] = i.checked; syncRoles(null); send('setMember', { member: i.dataset.member, on: i.checked });
  }));
  $('#rrca-sara-only').addEventListener('click', () => { state.roles = { ben: false, mik: false, sara: true }; syncRoles(null); send('saraOnly'); });
  $$('[data-voice-select]').forEach(s => s.addEventListener('change', () => send('setVoice', { member: s.dataset.voiceSelect, id: s.value })));
  $$('[data-voice]').forEach(b => b.addEventListener('click', () => send('testVoice', { member: b.dataset.voice })));
  root.addEventListener('click', e => {
    const b = e.target.closest('[data-cmd]'); if (!b || !b.dataset.cmd) return;
    e.preventDefault();
    if (b.dataset.cmd === 'openKeys') { page('team'); $('#rrca-keys').open = true; $('#rrca-keys').scrollIntoView(); return; }
    send(b.dataset.cmd);
  });
  $('#rrca-game').addEventListener('change', e => send('setGame', { name: e.target.value }));
  $('#rrca-auto').addEventListener('change', e => send('setAutoDetect', { on: e.target.checked }));
  $('#rrca-mic').addEventListener('change', e => send('setMicrophone', { name: e.target.value }));
  $('#rrca-radio-effect').addEventListener('change', e => send('setRadioEffect', { on: e.target.checked }));
  $('#rrca-recorder').addEventListener('change', e => send('setRecorder', { on: e.target.checked }));
  $$('[data-key-save]').forEach(b => b.addEventListener('click', () => {
    const input = $('#key-' + b.dataset.keySave + '-input'); const key = input.value.trim();
    if (!key) { notify('Erst den Schlüssel einfügen.'); return; }
    input.value = ''; send('saveKey', { provider: b.dataset.keySave, key });
  }));
  $$('[data-key-delete]').forEach(b => b.addEventListener('click', () => send('deleteKey', { provider: b.dataset.keyDelete })));
  $('#rrca-save').addEventListener('click', () => {
    const settings = {}; $$('[data-setting]').forEach(i => { settings[i.dataset.setting] = i.value; });
    send('saveSettings', { settings, topics: readTopics() });
  });
  const ask = () => { const q = $('#rrca-question').value.trim(); if (!q) return; $('#rrca-question').value = ''; notify('Frage an die Crew: ' + q); send('ask', { text: q }); };
  $('#rrca-send').addEventListener('click', ask);
  $('#rrca-question').addEventListener('keydown', e => { if (e.key === 'Enter') ask(); });

  if (bridge) {
    bridge.addEventListener('message', e => {
      const d = e.data;
      if (d.type === 'init') applyInit(d);
      else if (d.type === 'live') applyLive(d);
      else if (d.type === 'voices') fillVoices(d.list, d.current);
      else if (d.type === 'notice') notify(d.text);
    });
    send('ready');
  } else {
    notify('Diese Seite läuft nur im RRCA-Fenster von Crew Chief.');
  }
  page('cockpit');
})();
