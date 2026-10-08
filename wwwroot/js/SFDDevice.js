import { ApiError, api, auth, boot, esc, icon, num } from './SFDCommon.js';

// Sends this device's GPS position to the signed-in leader's workspace, at most once every 10 seconds.
const SEND_INTERVAL_MS = 10000;
const MAX_ACCURACY_M = 150;

const state = { user: boot.user, members: [], member: '', loading: false, error: '', running: false, count: 0, last: null };
let watchId = null;
let lastSent = 0;
let sending = false;
let started = false;

const card = document.getElementById('device-card');

const timeWib = ms => new Intl.DateTimeFormat('id-ID', { hour: '2-digit', minute: '2-digit', second: '2-digit', timeZone: 'Asia/Jakarta' }).format(new Date(ms));

function render() {
  if (!state.user) {
    card.innerHTML = `<div class="dialog-symbol">${icon('smartphone', 25)}</div><h2>Masuk ke ruang kerja</h2><p class="device-description">Gunakan akun leader untuk perangkat yang Anda kelola.</p><form class="app-form" id="login-form"><label>Email leader<input name="email" type="email" required autocomplete="email" placeholder="nama@perusahaan.com"></label><label>Kata sandi<input name="password" type="password" required minlength="8" autocomplete="current-password" placeholder="Masukkan kata sandi"></label>${state.error ? `<p class="form-error" role="alert">${esc(state.error)}</p>` : ''}<button class="button-primary wide" ${state.loading ? 'disabled' : ''}>${icon('lock-keyhole', 16)}${state.loading ? 'Memproses…' : 'Masuk'}</button><div class="info-box">${icon('shield-check', 17)}<p>Akun dibuat oleh pemilik dashboard. Jangan membagikan kata sandi leader ke anggota; pengirim ini ditujukan untuk perangkat yang dikelola leader.</p></div></form>`;
    return;
  }
  const { last } = state;
  const options = state.members.map(m => `<option value="${esc(m.id)}" ${m.id === state.member ? 'selected' : ''}>${esc(m.name)} · ${esc(m.area)}</option>`).join('');
  card.innerHTML = `<div class="dialog-symbol">${icon('smartphone', 25)}</div><div class="device-card-title"><h2>Pengirim lokasi</h2><span class="status-pill ${state.running ? 'bergerak' : 'offline'}"><i></i>${state.running ? 'Aktif' : 'Tidak aktif'}</span></div><p class="device-description">${esc(state.user.email)}</p>
    <label class="device-member-label">Anggota untuk perangkat ini<select id="member-select" ${state.running || state.loading ? 'disabled' : ''}><option value="">${state.loading ? 'Memuat anggota…' : 'Pilih anggota'}</option>${options}</select></label>
    ${!state.loading && !state.members.length ? '<div class="info-box"><p>Belum ada anggota. <a href="/">Tambahkan anggota di dashboard</a> terlebih dahulu.</p></div>' : ''}
    ${last ? `<div class="gps-readings"><div>${icon('map-pin', 17)}<small>Koordinat terakhir</small><strong>${num(last.lat, 5)}, ${num(last.lon, 5)}</strong></div><div>${icon('navigation', 17)}<small>Kecepatan</small><strong>${num(last.speed)} km/jam</strong></div><div>${icon('locate-fixed', 17)}<small>Akurasi lokasi</small><strong>± ${num(last.accuracy, 0)} m</strong></div><div>${icon('check', 17)}<small>Data terkirim</small><strong>${state.count} titik · ${esc(last.time)} WIB</strong></div></div>` : ''}
    ${state.running && !last ? `<div class="gps-wait">${icon('locate-fixed', 24, 2, 'spin')}<p>Menunggu lokasi GPS pertama…</p></div>` : ''}
    ${state.error ? `<p class="form-error" role="alert">${esc(state.error)}</p>` : ''}
    <button class="${state.running ? 'button-secondary wide stop-button' : 'button-primary wide'}" id="toggle" ${!state.member || state.loading ? 'disabled' : ''}>${state.running ? `${icon('pause', 17)}Hentikan pengiriman` : `${icon('radio', 17)}Mulai kirim GPS`}</button>
    <div class="info-box">${icon('shield-check', 17)}<p>Posisi dikirim paling sering setiap 10 detik saat lokasi berubah. Halaman harus tetap terbuka; pelacakan latar belakang tidak didukung. Pastikan anggota memberi izin sebelum memulai.</p></div>
    <button class="button-text" id="logout">${icon('log-out', 15)}Keluar dari akun</button>`;
}

async function loadMembers() {
  state.loading = true; render();
  try { state.members = await api('/api/members'); state.error = ''; }
  catch { state.error = 'Anggota belum dapat dimuat. Kembali ke dashboard dan tambahkan anggota.'; }
  finally { state.loading = false; render(); }
}

function stop() {
  if (watchId !== null) navigator.geolocation.clearWatch(watchId);
  watchId = null; started = false; state.running = false;
  render();
}

function start() {
  if (!state.member || !state.user) return;
  if (!navigator.geolocation) { state.error = 'Perangkat ini tidak mendukung GPS browser.'; render(); return; }
  state.error = ''; state.running = true; started = true; lastSent = 0;
  render();
  watchId = navigator.geolocation.watchPosition(async position => {
    if (!started || sending || Date.now() - lastSent < SEND_INTERVAL_MS) return;
    const c = position.coords;
    if (c.accuracy > MAX_ACCURACY_M) { state.error = 'Akurasi GPS masih rendah (>150 m). Cari area terbuka; titik ini tidak disimpan.'; render(); return; }
    sending = true;
    try {
      const point = { memberId: state.member, latitude: c.latitude, longitude: c.longitude, speed: Math.max(0, (c.speed || 0) * 3.6), bearing: c.heading || 0, accuracy: c.accuracy, recordedAt: new Date(position.timestamp).toISOString() };
      await api('/api/gps', { method: 'POST', json: point });
      if (!started) return;
      lastSent = Date.now(); state.count += 1; state.error = '';
      state.last = { lat: point.latitude, lon: point.longitude, speed: point.speed, accuracy: point.accuracy, time: timeWib(position.timestamp) };
      render();
    } catch (err) {
      if (!started) return;
      if (err instanceof ApiError && err.status === 401) state.user = null;
      state.error = 'Titik GPS belum terkirim. Periksa koneksi internet atau masuk kembali.';
      render();
    } finally { sending = false; }
  }, geoError => {
    state.error = geoError.code === 1 ? 'Izin lokasi ditolak. Aktifkan izin lokasi di pengaturan browser lalu coba lagi.'
      : geoError.code === 2 ? 'Lokasi belum tersedia. Pastikan GPS perangkat aktif.' : 'GPS membutuhkan waktu lebih lama. Coba di area terbuka.';
    if (geoError.code === 1) stop(); else render();
  }, { enableHighAccuracy: true, timeout: 20000, maximumAge: 5000 });
}

card.addEventListener('submit', async event => {
  if (event.target.id !== 'login-form') return;
  event.preventDefault();
  const { email, password } = event.target;
  state.loading = true; state.error = ''; render();
  try { state.user = await auth.login(email.value, password.value); state.loading = false; await loadMembers(); }
  catch { state.error = 'Email atau kata sandi salah, atau akun belum tersedia.'; state.loading = false; render(); }
});

card.addEventListener('change', event => { if (event.target.id === 'member-select') { state.member = event.target.value; render(); } });

card.addEventListener('click', async event => {
  if (event.target.closest('#toggle')) { if (state.running) stop(); else start(); }
  else if (event.target.closest('#logout')) {
    stop();
    try { await auth.logout(); } catch { /* signed out locally either way */ }
    Object.assign(state, { user: null, members: [], member: '', last: null, count: 0, error: '' });
    render();
  }
});

window.addEventListener('pagehide', () => { if (watchId !== null) navigator.geolocation.clearWatch(watchId); });

render();
if (state.user) void loadMembers();
