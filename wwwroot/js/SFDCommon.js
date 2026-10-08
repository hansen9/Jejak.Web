// Helpers shared by the dashboard and the device sender page.

export const boot = window.SFD;

export const esc = value => String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

/** Colours end up in style attributes, so only plain hex values are allowed through. */
export const safeColor = value => (/^#[0-9a-f]{6}$/i.test(value) ? value : '#7c8398');

export function icon(name, size = 18, stroke = 2, cls = '') {
  const body = window.SFD_ICONS[name] ?? '';
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="${stroke}" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"${cls ? ` class="${cls}"` : ''}>${body}</svg>`;
}

export const num = (value, digits = 1) => value.toLocaleString('id-ID', { minimumFractionDigits: digits, maximumFractionDigits: digits });

export const direction = bearing => ['Utara', 'Timur laut', 'Timur', 'Tenggara', 'Selatan', 'Barat daya', 'Barat', 'Barat laut'][Math.round(bearing / 45) % 8];

export const todayWib = () => new Intl.DateTimeFormat('en-CA', { year: 'numeric', month: '2-digit', day: '2-digit', timeZone: 'Asia/Jakarta' }).format(new Date());

const csrfMeta = () => document.querySelector('meta[name="csrf-token"]');

export class ApiError extends Error {
  constructor(status, body) { super(body?.error || `HTTP ${status}`); this.status = status; this.body = body; }
}

/** fetch wrapper: sends the antiforgery token, throws ApiError for non-2xx, returns parsed JSON (or null). */
export async function api(url, { method = 'GET', json, form } = {}) {
  const headers = { 'X-CSRF-TOKEN': csrfMeta().content };
  let body;
  if (json !== undefined) { headers['Content-Type'] = 'application/json'; body = JSON.stringify(json); }
  else if (form) body = form;
  const response = await fetch(url, { method, headers, body, credentials: 'same-origin' });
  const text = await response.text();
  let data = null;
  try { data = text ? JSON.parse(text) : null; } catch { /* non-JSON error page */ }
  if (!response.ok) throw new ApiError(response.status, data);
  return data;
}

// Antiforgery tokens are tied to the signed-in identity, so login and logout hand back a fresh one.
function applySession(session) {
  if (session?.csrf) csrfMeta().content = session.csrf;
  return session?.user ?? null;
}

export const auth = {
  login: async (username, password) => applySession(await api('/akun/masuk', { method: 'POST', json: { username, password } })),
  logout: async () => applySession(await api('/akun/keluar', { method: 'POST' })),
};

export const initialsOf = user => (user?.name || user?.email || 'L').slice(0, 2).toUpperCase();
