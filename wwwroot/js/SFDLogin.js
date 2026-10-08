import { auth, ApiError } from './SFDCommon.js';

const form = document.getElementById('login-form');
const error = document.getElementById('login-error');
const submit = document.getElementById('login-submit');
const password = form.elements.password;

const setError = text => { error.textContent = text ?? ''; error.hidden = !text; };

document.getElementById('password-toggle').addEventListener('click', event => {
  const visible = password.type === 'password';
  password.type = visible ? 'text' : 'password';
  event.currentTarget.textContent = visible ? 'Sembunyikan' : 'Tampilkan';
  event.currentTarget.setAttribute('aria-pressed', String(visible));
});

form.addEventListener('submit', async event => {
  event.preventDefault();
  setError();
  if (!form.reportValidity()) return;
  submit.disabled = true;
  submit.textContent = 'Memeriksa…';
  try {
    await auth.login(form.elements.username.value, password.value);
    // The server only hands out local paths, but the attribute is still checked before navigating.
    const target = form.dataset.return || '/';
    location.assign(/^\/(?![/\\])/.test(target) ? target : '/');
  } catch (e) {
    if (e instanceof ApiError && e.status === 403) {
      alert('Pengguna ini bukan leader. Hanya leader DMO yang dapat masuk ke Leader Dashboard.');
      location.replace('/masuk');
      return;
    }
    setError(e instanceof ApiError && e.status === 401 ? 'Username atau kata sandi tidak sesuai.'
      : e instanceof ApiError && e.status === 429 ? 'Terlalu banyak percobaan. Coba lagi dalam satu menit.'
      : 'Tidak dapat masuk saat ini. Periksa koneksi Anda dan coba lagi.');
    password.select();
    submit.disabled = false;
    submit.textContent = 'Masuk ke ruang kerja';
  }
});
