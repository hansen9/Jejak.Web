import { ApiError, api, auth, boot, direction, esc, icon, initialsOf, num, safeColor, todayWib } from './common.js';
import { TeamMap } from './map.js';

const HEADINGS = {
  dashboard: ['Ringkasan hari ini', 'Pantau pergerakan tim. Pastikan setiap wilayah terjangkau.'],
  map: ['Peta pergerakan tim', 'Posisi, arah, dan jejak perjalanan dalam satu peta.'],
  team: ['Anggota tim', 'Kenali aktivitas dan progres setiap anggota di lapangan.'],
  routes: ['Wilayah & rute', 'Lihat cakupan wilayah dan jalur yang masih perlu dilalui.'],
  analytics: ['Analitik perjalanan', 'Ubah setiap perjalanan menjadi wawasan yang berarti.'],
  reports: ['Laporan operasional', 'Ringkasan aktivitas tim yang siap diunduh dan dibagikan.'],
};
const STATUS_LABEL = { bergerak: 'Bergerak', berhenti: 'Berhenti', offline: 'Offline' };
const POLL_MS = 15000;

const state = {
  view: 'dashboard', date: boot.today, demo: true,
  globalQuery: '', teamQuery: '', status: 'all', selected: null,
  showTraces: true, showRemaining: true, dismissDemo: false, sortByDistance: false, metric: 'distance',
  user: boot.user, loading: false, error: '', connected: false, updatedAt: '', members: [], routes: [],
};

const $ = selector => document.querySelector(selector);
const $$ = selector => [...document.querySelectorAll(selector)];
const show = (el, visible) => { el.hidden = !visible; };

// ---------------------------------------------------------------- derived data

const matches = (query, text) => text.toLowerCase().includes(query.toLowerCase());

function derived() {
  const members = state.members.filter(m => matches(state.globalQuery, `${m.name} ${m.area}`));
  const mapMembers = members.filter(m => matches(state.teamQuery, `${m.name} ${m.area}`) && (state.status === 'all' || m.status === state.status));
  const routes = state.routes.filter(r => matches(state.globalQuery, `${r.area} ${r.name}`));
  return { members, mapMembers, routes };
}

function coverage(routes) {
  const total = routes.reduce((sum, r) => sum + r.total, 0);
  const done = routes.reduce((sum, r) => sum + r.done, 0);
  return { total, done, remaining: total - done, percent: total ? done / total * 100 : 0 };
}

// ---------------------------------------------------------------- data loading

let loadSeq = 0;

async function load({ silent = false } = {}) {
  const seq = ++loadSeq;
  if (!state.demo && !state.user) {
    Object.assign(state, { members: [], routes: [], error: '', connected: false, loading: false });
    render();
    return;
  }
  if (!silent) { state.loading = true; render(); }
  try {
    const data = await api(`/api/tracking?date=${encodeURIComponent(state.date)}&demo=${state.demo}`);
    if (seq !== loadSeq) return;
    Object.assign(state, { members: data.members, routes: data.routes, updatedAt: data.updatedAt, error: '', connected: !state.demo });
  } catch (err) {
    if (seq !== loadSeq) return;
    if (err instanceof ApiError && err.status === 401) state.user = null;
    state.error = 'Data belum dapat dimuat. Periksa koneksi Anda dan coba lagi.';
    state.connected = false;
  } finally {
    if (seq === loadSeq) { state.loading = false; render(); }
  }
}

// The original dashboard held a PocketBase realtime subscription; here the page re-reads on a timer while it is visible.
setInterval(() => { if (!state.demo && state.user && !document.hidden) void load({ silent: true }); }, POLL_MS);
document.addEventListener('visibilitychange', () => { if (!document.hidden && !state.demo && state.user) void load({ silent: true }); });

function useLive() {
  Object.assign(state, { demo: false, date: todayWib(), selected: null, dismissDemo: false });
  map.resetAutoFit();
  $('#date-input').value = state.date;
  void load();
}

function useDemo() {
  Object.assign(state, { demo: true, date: todayWib(), selected: null });
  $('#date-input').value = state.date;
  void load();
}

// ---------------------------------------------------------------- rendering

const map = new TeamMap($('#map-canvas'), { onSelect: id => { state.selected = state.selected === id ? null : id; render(); } });

function render() {
  const { members, mapMembers, routes } = derived();
  renderChrome();
  renderBanners();
  renderMetrics(members, routes);
  renderTeamList(mapMembers);
  renderCharts(members, routes);
  renderMembers(members);
  renderRoutes(members, routes);
  renderReport(members, routes);
  renderNotifications();
  renderAccount();

  const v = state.view;
  const mapVisible = v === 'dashboard' || v === 'map';
  show($('#map-panel'), mapVisible);
  $('#map-panel').classList.toggle('expanded', v === 'map');
  show($('#charts-grid'), v === 'dashboard' || v === 'analytics');
  show($('#members-panel'), v !== 'map' && v !== 'routes');
  show($('#routes-panel'), v === 'routes' || v === 'analytics');
  show($('#reports-panel'), v === 'reports');
  if (mapVisible) { map.update({ members: mapMembers, routes, selected: state.selected, showTraces: state.showTraces, showRemaining: state.showRemaining, demo: state.demo }); map.refreshSize(); }
}

function renderChrome() {
  const { demo, user, view } = state;
  const name = String(user?.name || 'Leader');
  const [title, sub] = HEADINGS[view];
  $('#page-title').textContent = title;
  $('#page-sub').textContent = sub;
  show($('#demo-tag'), demo);
  $('#btn-refresh').disabled = state.loading;
  $('#btn-refresh').classList.toggle('spinning', state.loading);
  $('#crumb').textContent = $(`[data-nav="${view}"]`).dataset.label;
  $$('[data-nav]').forEach(btn => btn.classList.toggle('active', btn.dataset.nav === view));

  $('#side-avatar').textContent = demo ? 'RL' : name.slice(0, 2).toUpperCase();
  $('#top-avatar').textContent = demo ? 'RL' : name.slice(0, 2).toUpperCase();
  $('#side-name').textContent = demo ? 'Ruang Leader' : name;
  $('#side-sub').textContent = demo ? 'Pratinjau dashboard' : 'Leader tim';
  $('#conn-dot').className = `status-dot ${demo ? 'copper' : state.connected ? '' : 'gray'}`;
  $('#conn-title').textContent = demo ? 'Mode demonstrasi' : state.connected ? 'GPS terhubung' : 'Menunggu koneksi GPS';
  $('#conn-sub').textContent = demo ? 'Jelajahi dengan data contoh' : 'Data tersimpan secara privat';

  $('#live-badge-text').textContent = demo ? 'SIMULASI' : 'LANGSUNG';
  $('#updated-at').textContent = state.updatedAt || '—';
  $('#map-panel .map-panel-actions button').setAttribute('aria-label', view === 'map' ? 'Tutup tampilan peta' : 'Buka tampilan peta');
  show($('#demo-banner'), demo && !state.dismissDemo);
  $('#footer-status').textContent = demo ? 'Data demonstrasi · bukan GPS langsung' : state.connected ? 'Pembaruan GPS langsung' : 'Menunggu pembaruan GPS';
}

function renderBanners() {
  const parts = [];
  if (state.error) parts.push(`<div class="error-banner" role="alert">${esc(state.error)}<button data-action="retry">Coba lagi</button></div>`);
  if (!state.demo && !state.user) parts.push('<div class="error-banner">Masuk untuk melihat data tim Anda.<button data-dialog="account">Masuk sebagai leader</button></div>');
  $('#banners').innerHTML = parts.join('');
}

function renderMetrics(members, routes) {
  const active = members.filter(m => m.status !== 'offline').length;
  const distance = members.reduce((sum, m) => sum + m.distance, 0);
  const cov = coverage(routes);
  const count = status => members.filter(m => m.status === status).length;
  const items = [
    { type: 'active', label: 'Anggota aktif', value: active, unit: `/ ${members.length}`, icon: 'users', foot: `${count('bergerak')} bergerak`, detail: `${count('berhenti')} berhenti` },
    { type: 'distance', label: 'Total jarak tempuh', value: num(distance), unit: 'km', icon: 'route', foot: 'Akumulasi perjalanan tim', detail: 'hari ini' },
    { type: 'coverage', label: 'Cakupan wilayah', value: num(cov.percent, 0), unit: '%', icon: 'waypoints', foot: `${num(cov.done)} km terjangkau`, detail: 'dari rute rencana' },
    { type: 'remaining', label: 'Jalur belum dilalui', value: num(cov.remaining), unit: 'km', icon: 'footprints', foot: `${num(100 - cov.percent, 0)}% dari total rute`, detail: 'perlu dijangkau' },
  ];
  $('#metrics').innerHTML = items.map(item => {
    const foot = item.type === 'active' ? `<span class="status-dot"></span>${item.foot}<i></i>${item.detail}`
      : item.type === 'coverage' ? `${icon('check', 13)}${item.foot}<span class="metric-detail">${item.detail}</span>`
        : `${item.type === 'distance' ? icon('arrow-up-right', 14) : '<span class="status-dot copper"></span>'}${item.foot}<span class="metric-detail">${item.detail}</span>`;
    return `<article class="metric metric-${item.type}"><div class="metric-label"><span>${item.label}</span><span class="metric-icon">${icon(item.icon, 19, 1.7)}</span></div>${state.loading ? '<div class="metric-skeleton"></div>' : `<div class="metric-value">${item.value}<span>${item.unit}</span></div>`}<div class="metric-foot">${foot}</div></article>`;
  }).join('');
}

function renderTeamList(members) {
  $('#active-count').textContent = members.filter(m => m.status !== 'offline').length;
  $('#team-count').textContent = members.length;
  $('#team-list').innerHTML = members.length ? members.map(m => `<button class="live-member ${state.selected === m.id ? 'selected' : ''}" data-select="${esc(m.id)}"><span class="member-avatar" style="--member-color:${safeColor(m.color)}">${esc(m.initials)}<span class="avatar-status ${esc(m.status)}"></span></span><div class="live-member-info"><strong>${esc(m.name)}</strong><small>${esc(m.area)}</small></div><div class="live-member-speed"><strong>${m.status === 'offline' ? '—' : m.speed}<small>${m.status === 'offline' ? 'Offline' : 'km/jam'}</small></strong><span class="member-state ${esc(m.status)}">${m.status === 'bergerak' ? `${icon('navigation-2', 10, 2)}${direction(m.bearing)}`.replace('<svg ', `<svg style="transform:rotate(${m.bearing - 45}deg)" `) : m.status === 'berhenti' ? 'Berhenti' : esc(m.lastSeen)}</span></div></button>`).join('')
    : '<div class="list-empty">Tidak ada anggota yang sesuai dengan filter.</div>';

  const selected = members.find(m => m.id === state.selected);
  show($('#selected-detail'), !!selected);
  if (selected) {
    $('#selected-detail').innerHTML = `<div class="selected-distance">Jarak hari ini<strong>${num(selected.distance)} km</strong></div><div class="selected-distance">Kecepatan &amp; arah<strong>${selected.speed} km/jam · ${direction(selected.bearing)}</strong></div><div class="selected-coordinates">${selected.trace.length ? `${num(selected.position[0], 5)}, ${num(selected.position[1], 5)}` : 'Belum ada posisi GPS'}</div>`;
  }
}

function renderCharts(members, routes) {
  // distance per member
  const ranked = [...members].sort((a, b) => b.distance - a.distance).slice(0, 8);
  const max = Math.max(10, Math.ceil(Math.max(0, ...ranked.map(m => m.distance)) / 10) * 10);
  $('#distance-chart').innerHTML = ranked.length
    ? ranked.map(m => `<div class="bar-row"><span class="bar-name" title="${esc(m.name)}">${esc(m.name.split(' ')[0])}</span><div class="bar-track"><div class="bar-fill" style="width:${Math.min(100, m.distance / max * 100)}%;background:${safeColor(m.color)}"></div></div><strong>${num(m.distance)}<span> km</span></strong></div>`).join('')
      + `<div class="bar-axis">${[0, 1, 2, 3].map(i => `<span>${num(max / 3 * i, 0)}</span>`).join('')}</div>`
    : `<div class="chart-empty">${icon('route', 25)}Belum ada jarak tempuh untuk tanggal ini.</div>`;

  // coverage donut
  const cov = coverage(routes);
  const circumference = 2 * Math.PI * 62;
  $('#coverage-chart').innerHTML = `<div class="donut-content"><div class="donut"><svg viewBox="0 0 152 152" role="img" aria-label="${num(cov.percent, 0)} persen rute terjangkau"><circle cx="76" cy="76" r="62" fill="none" stroke="var(--donut-rest)" stroke-width="13"/><circle cx="76" cy="76" r="62" fill="none" stroke="var(--plum)" stroke-width="13" stroke-linecap="round" stroke-dasharray="${Math.max(0, circumference * cov.percent / 100 - 6)} ${circumference}" transform="rotate(-90 76 76)"/></svg><div class="donut-label"><strong>${num(cov.percent, 0)}<span>%</span></strong><small>terjangkau</small></div></div><div class="coverage-key"><div><span><i class="key-dot plum"></i>Sudah dilalui</span><strong>${num(cov.done)} <small>km</small></strong></div><div><span><i class="key-dot blush"></i>Belum dilalui</span><strong>${num(cov.remaining)} <small>km</small></strong></div></div></div><div class="coverage-total">Total panjang rute rencana<strong>${num(cov.total)} km</strong></div>`;

  renderActivity(members);
}

let activity = { data: [], unit: '' };

function renderActivity(members) {
  const distanceMode = state.metric === 'distance';
  const data = Array.from({ length: 12 }, (_, i) => distanceMode
    ? members.reduce((sum, m) => sum + (m.hours[i] ?? 0), 0)
    : members.filter(m => (m.hours[i] ?? 0) > 0).length);
  const max = distanceMode ? Math.max(30, ...data) : Math.max(8, ...data);
  const unit = distanceMode ? 'km' : 'anggota';
  activity = { data, unit };
  const coords = data.map((v, i) => [34 + i * 28.7, 143 - v / max * 110]);
  const grid = [0, 1, 2, 3].map(i => `<line x1="34" x2="350" y1="${143 - i * 36.6}" y2="${143 - i * 36.6}" stroke="var(--line)" stroke-dasharray="3 4"/><text x="23" y="${147 - i * 36.6}" text-anchor="end">${num(max / 3 * i, 0)}</text>`).join('');
  const points = coords.map(([x, y], i) => `<circle class="act-dot" data-i="${i}" cx="${x}" cy="${y}" r="2" fill="var(--plum)" stroke="white" stroke-width="1.5"/><rect class="act-hit" data-i="${i}" x="${x - 14}" y="25" width="28" height="122" fill="transparent" tabindex="0" aria-label="${i + 6}.00: ${num(data[i])} ${unit}"/>${i % 2 === 0 ? `<text x="${x}" y="164" text-anchor="middle">${String(i + 6).padStart(2, '0')}.00</text>` : ''}`).join('');
  $('#activity-chart').innerHTML = `<div class="plot-key"><i class="key-dot plum"></i>${distanceMode ? 'Jarak tempuh (km)' : 'Anggota dengan perjalanan'}</div><svg viewBox="0 0 368 173" role="img" aria-label="Grafik aktivitas tim pukul 06 sampai 17 WIB"><defs><linearGradient id="actGradient" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="var(--plum)" stop-opacity="0.19"/><stop offset="100%" stop-color="var(--plum)" stop-opacity="0.01"/></linearGradient></defs>${grid}<path d="M 34 143 L ${coords.map(([x, y]) => `${x} ${y}`).join(' L ')} L 350 143 Z" fill="url(#actGradient)"/><polyline points="${coords.map(([x, y]) => `${x},${y}`).join(' ')}" fill="none" stroke="var(--plum)" stroke-width="2.3" stroke-linejoin="round" stroke-linecap="round"/>${points}</svg><div class="chart-tooltip" id="activity-tip" hidden></div>`;
}

function hoverActivity(index) {
  $$('#activity-chart .act-dot').forEach(dot => dot.setAttribute('r', Number(dot.dataset.i) === index ? 4 : 2));
  const tip = $('#activity-tip');
  if (!tip) return;
  show(tip, index !== null);
  if (index !== null) tip.innerHTML = `${String(index + 6).padStart(2, '0')}.00 WIB · <strong>${num(activity.data[index])} ${activity.unit}</strong>`;
}

function renderMembers(members) {
  const sorted = state.sortByDistance ? [...members].sort((a, b) => b.distance - a.distance) : members;
  const compact = state.view === 'dashboard';
  const visible = compact ? sorted.slice(0, 5) : sorted;
  $('#member-rows').innerHTML = visible.map(m => `<tr><td><div class="table-person"><span class="member-avatar small" style="--member-color:${safeColor(m.color)}">${esc(m.initials)}</span><div><strong>${esc(m.name)}</strong><small>Terakhir ${esc(m.lastSeen)}</small></div></div></td><td>${esc(m.area)}</td><td><span class="status-pill ${esc(m.status)}"><i></i>${STATUS_LABEL[m.status]}</span></td><td>${m.status === 'offline' ? '—' : `${m.speed}<small class="table-unit"> km/jam</small>`}</td><td><span class="table-direction">${icon('navigation-2', 13).replace('<svg ', `<svg style="transform:rotate(${m.bearing - 45}deg)" `)}${direction(m.bearing)}</span></td><td><strong>${num(m.distance)}</strong><small class="table-unit"> km</small></td><td><span class="battery ${m.battery !== null && m.battery < 20 ? 'low' : ''}">${icon('battery', 16)}${m.battery === null ? '—' : `${m.battery}%`}</span></td><td><button class="icon-button" data-show="${esc(m.id)}" aria-label="Lihat ${esc(m.name)} di peta">${icon('arrow-up-right', 17)}</button></td></tr>`).join('');
  show($('#member-empty'), !visible.length);
  show($('#member-all'), compact);
  $('#member-all-count').textContent = members.length;
}

function renderRoutes(members, routes) {
  $('#route-rows').innerHTML = routes.map(r => `<tr><td><div class="route-name">${icon('map-pin', 18)}<div><strong>${esc(r.area || r.name)}</strong><small>${esc(r.name)}</small></div></div></td><td>${num(r.total)} km</td><td>${num(r.done)} km</td><td class="text-copper">${num(r.remaining)} km</td><td><div class="route-progress"><div><span style="width:${r.percent}%"></span></div><strong>${num(r.percent, 0)}%</strong></div></td><td><button class="icon-button" data-nav-to="map" aria-label="Lihat rute ${esc(r.name)} di peta">${icon('arrow-up-right', 16)}</button></td></tr>`).join('');
  show($('#route-empty'), !routes.length);
  $('#route-demo-note').textContent = state.demo ? 'Pada mode demo, cakupan merupakan ilustrasi.' : '';
}

function renderReport(members, routes) {
  const cov = coverage(routes);
  $('#report-facts').innerHTML = `<span><strong>${members.length}</strong> anggota</span><span><strong>${num(members.reduce((sum, m) => sum + m.distance, 0))}</strong> km tempuh</span><span><strong>${num(cov.percent, 0)}%</strong> cakupan</span>`;
  $('#report-download-label').textContent = `Unduh CSV · ${state.date}`;
  $('#report-disclaimer').textContent = state.demo ? 'Laporan ini menggunakan data demonstrasi, bukan perjalanan sebenarnya.' : 'Berkas berisi lokasi tim. Bagikan hanya kepada pihak yang berwenang.';
}

// ---------------------------------------------------------------- dialogs

const dialogs = { account: $('#dlg-account'), member: $('#dlg-member'), import: $('#dlg-import'), help: $('#dlg-help'), notifications: $('#dlg-notifications') };
const live = () => !state.demo && !!state.user;

function openDialog(name) {
  for (const dialog of Object.values(dialogs)) if (dialog.open) dialog.close();
  if (name === 'member') { show($('#member-locked'), !live()); show($('#member-form'), live()); }
  if (name === 'import') {
    show($('#import-locked'), !live()); show($('#import-form'), live());
    const options = state.members.map(m => `<option value="${esc(m.id)}">${esc(m.name)}</option>`).join('');
    $('#import-member').innerHTML = `<option value="">Pilih anggota</option>${options}`;
    syncImportForm();
  }
  if (name === 'notifications') renderNotifications();
  if (name === 'account') renderAccount();
  dialogs[name].showModal();
}

// Clicking the backdrop (outside the dialog box) closes it.
for (const dialog of Object.values(dialogs)) {
  dialog.addEventListener('click', event => {
    if (event.target !== dialog) return;
    const r = dialog.getBoundingClientRect();
    if (event.clientX < r.left || event.clientX > r.right || event.clientY < r.top || event.clientY > r.bottom) dialog.close();
  });
}

function renderAccount() {
  const body = $('#account-body');
  // Rebuilt only when what it shows changes, so a background refresh never wipes a half-typed login form.
  const signature = `${!!state.user}|${state.demo}`;
  if (body.dataset.rendered === signature) return;
  body.dataset.rendered = signature;
  const user = state.user;
  const bottom = `<div class="dialog-bottom">${state.demo ? 'Anda sedang menggunakan data demonstrasi.' : 'Data tim hanya dapat diakses oleh akun pemilik.'}${!state.demo ? '<button class="button-text" data-action="demo">Kembali ke demo</button>' : ''}</div>`;
  body.innerHTML = `<div class="dialog-symbol">${icon('lock-keyhole', 23)}</div><h2 id="account-title">${user ? 'Ruang kerja Anda' : 'Masuk sebagai leader'}</h2><p>${user ? 'Kelola tim dengan data privat yang tersimpan di ruang kerja Anda.' : 'Gunakan akun leader untuk menghubungkan GPS dan mengelola data tim yang sebenarnya.'}</p>`
    + (user
      ? `<div class="account-summary"><span class="profile-avatar">${esc(initialsOf(user))}</span><div><strong>${esc(user.name || 'Leader')}</strong><small>${esc(user.email)}</small></div>${icon('check', 18)}</div><button class="button-primary wide" data-action="go-live">${icon('radio', 17)}Gunakan data tim asli</button><a class="button-secondary wide" href="/perangkat">${icon('smartphone', 17)}Hubungkan GPS perangkat</a><button class="button-text" data-action="logout">${icon('log-out', 15)}Keluar dari akun</button>`
      : `<form class="app-form" id="login-form"><label>Username leader<input name="username" type="text" required placeholder="Username SoloFleet" autocomplete="username"></label><label>Kata sandi<input name="password" type="password" required placeholder="Masukkan kata sandi" autocomplete="current-password"></label><p class="form-error" id="login-error" role="alert" hidden></p><button class="button-primary wide" id="login-submit" type="submit">Masuk ke ruang kerja</button><div class="info-box">${icon('lock-keyhole', 17)}<p>Masuk dengan akun SoloFleet Anda. Hanya leader DMO yang dapat mengakses dashboard ini.</p></div></form>`)
    + bottom;
}

function renderNotifications() {
  $('#notif-sub').textContent = state.demo ? 'Notifikasi berikut berasal dari data demonstrasi.' : 'Kondisi anggota berdasarkan pembaruan GPS terakhir.';
  const alerts = state.members.filter(m => m.status !== 'bergerak');
  $('#notif-list').innerHTML = alerts.length
    ? alerts.map(m => `<button data-show="${esc(m.id)}"><span class="alert-icon ${esc(m.status)}">${icon(m.status === 'offline' ? 'wifi-off' : 'circle-pause', 20)}</span><div><strong>${esc(m.name)} ${m.status === 'offline' ? 'tidak terhubung' : 'sedang berhenti'}</strong><p>${m.status === 'offline' ? 'Belum menerima GPS baru selama lebih dari 5 menit.' : 'Kecepatan terakhir kurang dari 1 km/jam.'}</p><small>${esc(m.area)} · ${esc(m.lastSeen)}</small></div></button>`).join('')
    : `<div class="locked-action">${icon('bell', 25)}<h3>Tidak ada peringatan</h3><p>Anggota yang berhenti atau offline akan ditampilkan di sini.</p></div>`;
}

function syncImportForm() {
  const gps = $('#import-type').value === 'gps';
  show($('#import-member-label'), gps);
  $('#import-member').required = gps;
  $('#import-file').accept = gps ? '.csv' : '.geojson,.json';
  $('#import-file-hint').textContent = gps ? '.csv · maks. 5 MB' : '.geojson atau .json · maks. 5 MB';
  $('#import-help').textContent = gps
    ? 'CSV: latitude, longitude, recorded_at (ISO dengan zona waktu); speed (km/jam) dan bearing (0–360°) opsional. Satu berkas untuk satu anggota.'
    : 'GeoJSON: FeatureCollection, Feature, atau geometri LineString/MultiLineString. Koordinat mengikuti urutan [longitude, latitude]. Properti name dan area bersifat opsional.';
  const file = $('#import-file').files[0];
  $('#import-file-name').textContent = file ? file.name : 'Pilih berkas untuk diimpor';
  $('#import-submit').disabled = !file || (gps && !$('#import-member').value);
}

// ---------------------------------------------------------------- toast, CSV export

let toastTimer;
function toast(message) {
  $('#toast-text').textContent = message;
  show($('#toast'), true);
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => show($('#toast'), false), 4500);
}

function exportCsv() {
  const { members, routes } = derived();
  if (!members.length) { toast('Belum ada data anggota untuk diunduh.'); return; }
  const rows = [
    ['LAPORAN SFD', state.date, state.demo ? 'DATA DEMONSTRASI — BUKAN GPS LANGSUNG' : 'DATA TIM PRIVAT'],
    [],
    ['PERJALANAN ANGGOTA'],
    ['Nama', 'Wilayah', 'Status', 'Jarak (km)', 'Kecepatan (km/jam)', 'Arah', 'Latitude', 'Longitude', 'Terakhir diperbarui'],
    ...members.map(m => [m.name, m.area, m.status, m.distance.toFixed(2), m.speed, direction(m.bearing), m.trace.length ? m.position[0] : '', m.trace.length ? m.position[1] : '', m.lastSeen]),
    [],
    ['CAKUPAN RUTE RENCANA'],
    ['Nama rute', 'Wilayah', 'Total (km)', 'Sudah dilalui (km)', 'Belum dilalui (km)', 'Cakupan (%)'],
    ...routes.map(r => [r.name, r.area, r.total.toFixed(2), r.done.toFixed(2), r.remaining.toFixed(2), r.percent.toFixed(1)]),
  ];
  // Cells that start with a formula character get a leading apostrophe so spreadsheets do not execute them.
  const csv = '﻿' + rows.map(row => row.map(value => {
    const raw = String(value);
    const safe = typeof value === 'string' && /^[=+@\-\t\r]/.test(raw) ? "'" + raw : raw;
    return '"' + safe.replaceAll('"', '""') + '"';
  }).join(';')).join('\r\n');
  const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8;' }));
  const anchor = document.createElement('a');
  anchor.href = url; anchor.download = `leader-dashboard-laporan-${state.date}.csv`; anchor.click();
  URL.revokeObjectURL(url);
  toast('Laporan CSV berhasil diunduh.');
}

// ---------------------------------------------------------------- events

function setView(view) {
  state.view = view;
  closeNav();
  render();
}

function showMember(id) {
  state.view = 'map'; state.selected = id; state.teamQuery = ''; state.status = 'all';
  $('#team-search').value = ''; $('#status-filter').value = 'all';
  closeNav();
  render();
}

function closeNav() {
  $('#sidebar').classList.remove('is-open');
  $('#nav-scrim-slot').innerHTML = '';
}

function openNav() {
  $('#sidebar').classList.add('is-open');
  $('#nav-scrim-slot').innerHTML = '<button class="sidebar-scrim" aria-label="Tutup navigasi" data-action="close-nav"></button>';
}

async function goLiveFromDialog() {
  dialogs.account.close();
  useLive();
}

const actions = {
  retry: () => void load(),
  'close-nav': closeNav,
  'go-live': goLiveFromDialog,
  demo: () => { dialogs.account.close(); useDemo(); },
  logout: async () => {
    try { await auth.logout(); } catch { /* the cookie may already be gone; fall through to the signed-out UI */ }
    state.user = null;
    dialogs.account.close();
    useDemo();
  },
};

document.addEventListener('click', event => {
  const target = event.target.closest('[data-action], [data-dialog], [data-view], [data-nav], [data-nav-to], [data-select], [data-show], [data-close]');
  if (!target) return;
  const d = target.dataset;
  if (d.action) actions[d.action]?.();
  else if (d.dialog) openDialog(d.dialog);
  else if (d.view) setView(d.view);
  else if (d.nav) setView(d.nav);
  else if (d.navTo) setView(d.navTo);
  else if (d.select) { state.selected = state.selected === d.select ? null : d.select; render(); }
  else if (d.show !== undefined) { target.closest('dialog')?.close(); showMember(d.show); }
  else if ('close' in d) target.closest('dialog').close();
});

document.addEventListener('submit', async event => {
  const form = event.target;
  event.preventDefault();
  if (form.id === 'login-form') {
    const error = $('#login-error'); const submit = $('#login-submit');
    show(error, false); submit.disabled = true; submit.textContent = 'Memproses…';
    try {
      state.user = await auth.login(form.username.value, form.password.value);
      dialogs.account.close();
      useLive();
    } catch (e) {
      if (e?.status === 403) {
        alert('Pengguna ini bukan leader. Hanya leader DMO yang dapat masuk ke Leader Dashboard.');
        location.replace('/');
        return;
      }
      error.textContent = e?.status === 502 ? 'Layanan SoloFleet tidak dapat dihubungi. Coba lagi nanti.' : 'Username atau kata sandi tidak sesuai.'; show(error, true);
      submit.disabled = false; submit.textContent = 'Masuk ke ruang kerja';
    }
  } else if (form.id === 'member-form') {
    const error = $('#member-error'); const submit = $('#member-submit');
    show(error, false); submit.disabled = true; submit.textContent = 'Menyimpan…';
    try {
      await api('/api/members', { method: 'POST', json: { name: form.name.value, area: form.area.value, phone: form.phone.value, color: $('#color-picker .selected').dataset.color } });
      form.reset();
      dialogs.member.close();
      toast('Anggota berhasil ditambahkan.');
      void load();
    } catch { error.textContent = 'Anggota belum dapat disimpan. Silakan coba lagi.'; show(error, true); }
    finally { submit.disabled = false; submit.textContent = 'Simpan anggota'; }
  } else if (form.id === 'import-form') {
    await submitImport(form);
  }
});

async function submitImport(form) {
  const error = $('#import-error'); const success = $('#import-success'); const submit = $('#import-submit');
  const gps = form.type.value === 'gps';
  show(error, false); show(success, false); submit.disabled = true; submit.textContent = 'Mengimpor…';
  try {
    const data = new FormData();
    data.append('file', form.file.files[0]);
    if (gps) data.append('member', form.member.value);
    const result = await api(gps ? '/api/import/gps' : '/api/import/routes', { method: 'POST', form: data });
    success.textContent = `${result.imported} ${gps ? 'titik GPS' : 'rute'} berhasil diimpor.`; show(success, true);
    form.file.value = '';
    void load();
  } catch (err) {
    error.textContent = err instanceof ApiError && err.status === 400 && err.body?.error ? err.body.error : 'Impor belum berhasil. Periksa data dan koneksi, lalu coba lagi.';
    show(error, true);
  } finally { submit.textContent = 'Impor & simpan data'; syncImportForm(); }
}

$('#import-type').addEventListener('change', () => { $('#import-file').value = ''; show($('#import-error'), false); show($('#import-success'), false); syncImportForm(); });
$('#import-member').addEventListener('change', syncImportForm);
$('#import-file').addEventListener('change', () => { show($('#import-error'), false); show($('#import-success'), false); syncImportForm(); });

$('#color-picker').addEventListener('click', event => {
  const button = event.target.closest('[data-color]');
  if (!button) return;
  $$('#color-picker button').forEach(b => { b.classList.toggle('selected', b === button); b.setAttribute('aria-pressed', String(b === button)); });
});

$('#global-search').addEventListener('input', event => { state.globalQuery = event.target.value; render(); });
$('#team-search').addEventListener('input', event => { state.teamQuery = event.target.value; render(); });
$('#status-filter').addEventListener('change', event => { state.status = event.target.value; render(); });
$('#toggle-traces').addEventListener('change', event => { state.showTraces = event.target.checked; render(); });
$('#toggle-remaining').addEventListener('change', event => { state.showRemaining = event.target.checked; render(); });
$('#activity-metric').addEventListener('change', event => { state.metric = event.target.value; render(); });
$('#sort-distance').addEventListener('click', () => { state.sortByDistance = !state.sortByDistance; render(); });
$('#btn-expand').addEventListener('click', () => setView(state.view === 'map' ? 'dashboard' : 'map'));
$('#btn-export').addEventListener('click', exportCsv);
$('#btn-export-report').addEventListener('click', exportCsv);
$('#btn-refresh').addEventListener('click', () => { if (state.demo) toast(`Data demonstrasi diperbarui · ${state.updatedAt}`); else void load(); });
$('#dismiss-demo').addEventListener('click', () => { state.dismissDemo = true; render(); });
$('#toast-close').addEventListener('click', () => show($('#toast'), false));
$('#nav-open').addEventListener('click', openNav);
$('#nav-close').addEventListener('click', closeNav);
$('#date-input').addEventListener('change', event => { if (!event.target.value) return; state.date = event.target.value; state.selected = null; void load(); });

const activityRoot = $('#activity-chart');
activityRoot.addEventListener('mouseover', event => { const hit = event.target.closest('.act-hit'); if (hit) hoverActivity(Number(hit.dataset.i)); });
activityRoot.addEventListener('mouseout', event => { if (event.target.closest('.act-hit')) hoverActivity(null); });
activityRoot.addEventListener('focusin', event => { const hit = event.target.closest('.act-hit'); if (hit) hoverActivity(Number(hit.dataset.i)); });
activityRoot.addEventListener('focusout', () => hoverActivity(null));

document.addEventListener('keydown', event => {
  if (event.key === '/' && !(event.target instanceof HTMLInputElement) && !(event.target instanceof HTMLTextAreaElement) && !(event.target instanceof HTMLSelectElement)) {
    event.preventDefault();
    $('#global-search').focus();
  }
});

// ---------------------------------------------------------------- start

$('#date-input').value = state.date;
render();
void load();
