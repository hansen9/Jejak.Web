import { esc, safeColor } from './SFDCommon.js';

const TILES = {
  standard: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
  detail: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
};
const ATTRIBUTION = '© <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noreferrer">OpenStreetMap</a> contributors';

/** Leaflet map of member positions, GPS traces and uncovered route segments. Wraps the static markup in Dashboard.cshtml. */
export class TeamMap {
  constructor(root, { onSelect }) {
    this.root = root;
    this.onSelect = onSelect;
    this.detail = false;
    this.autoFitted = false;
    this.last = { members: [], routes: [], selected: null, showTraces: true, showRemaining: true, demo: true };
    const $ = selector => root.querySelector(selector);
    this.els = { slot: $('#map-slot'), loading: $('#map-loading'), error: $('#map-error'), empty: $('#map-empty'), style: $('#map-style'), full: $('#map-full'), place: $('#map-place'), region: $('#map-region') };

    $('#map-zoom-in').addEventListener('click', () => this.map?.zoomIn());
    $('#map-zoom-out').addEventListener('click', () => this.map?.zoomOut());
    $('#map-fit').addEventListener('click', () => this.fit());
    this.els.full.addEventListener('click', () => {
      const on = root.classList.toggle('map-fullscreen');
      this.els.full.setAttribute('aria-label', on ? 'Keluar dari peta penuh' : 'Perluas peta');
      this.refreshSize();
    });
    this.els.style.addEventListener('click', () => {
      this.detail = !this.detail;
      this.els.style.querySelector('span').textContent = this.detail ? 'Peta standar' : 'Detail jalan';
      this.setTiles();
    });
  }

  /** Created on first use so Leaflet measures a visible container. */
  ensure() {
    if (this.map) return true;
    if (!window.L || !this.els.slot.offsetParent) return false;
    const L = window.L;
    this.map = L.map(this.els.slot, { center: [-6.236, 106.8175], zoom: 13, zoomControl: false, attributionControl: true });
    this.map.attributionControl.setPrefix(false);
    this.layer = L.layerGroup().addTo(this.map);
    this.setTiles();
    this.els.loading.hidden = true;
    return true;
  }

  setTiles() {
    if (!this.map) return;
    this.tiles?.remove();
    this.els.error.hidden = true;
    this.tiles = window.L.tileLayer(this.detail ? TILES.detail : TILES.standard, { maxZoom: 19, attribution: ATTRIBUTION }).addTo(this.map);
    this.tiles.on('tileerror', () => { this.els.error.hidden = false; });
    this.tiles.on('load', () => { this.els.error.hidden = true; });
  }

  /** Leaflet caches its size; call after the container is shown, resized or made fullscreen. */
  refreshSize() { setTimeout(() => this.map?.invalidateSize(), 100); }

  resetAutoFit() { this.autoFitted = false; }

  fit() {
    const positions = this.last.members.filter(m => m.trace.length).map(m => m.position);
    if (positions.length && this.map) this.map.fitBounds(window.L.latLngBounds(positions).pad(0.22), { maxZoom: 14 });
  }

  update(state) {
    this.last = state;
    const { members, routes, selected, showTraces, showRemaining, demo } = state;
    this.els.place.textContent = demo ? 'Jakarta Selatan' : 'Lokasi tim';
    this.els.region.textContent = demo ? 'DKI Jakarta' : 'GPS perangkat';
    if (!this.ensure()) return;
    this.els.empty.hidden = members.length > 0;

    const L = window.L;
    const group = this.layer;
    group.clearLayers();

    if (showRemaining) {
      for (const route of routes) {
        for (const segment of route.uncovered) {
          L.polyline(segment, { color: '#bf9980', weight: 3, dashArray: '5 7', opacity: 0.9, lineCap: 'round' }).addTo(group);
        }
      }
    }

    for (const member of members) {
      const muted = selected !== null && selected !== member.id;
      const color = safeColor(member.color);
      if (showTraces && member.trace.length > 1) {
        L.polyline(member.trace, { color: '#ffffff', weight: selected === member.id ? 8 : 6, opacity: muted ? 0.35 : 0.9 }).addTo(group);
        L.polyline(member.trace, { color, weight: selected === member.id ? 5 : 3.5, opacity: muted ? 0.25 : 0.88, lineCap: 'round', lineJoin: 'round' }).addTo(group);
        L.circleMarker(member.trace[0], { radius: 4, color, weight: 2, fillColor: '#ffffff', fillOpacity: 1, opacity: muted ? 0.3 : 1 }).addTo(group);
      }
      if (!member.trace.length) continue;
      const initials = esc(member.initials.replace(/[^A-Z0-9]/g, '').slice(0, 2));
      const icon = L.divIcon({
        className: `team-map-marker ${muted ? 'muted' : ''} ${selected === member.id ? 'selected' : ''}`,
        html: `<span class="marker-arrow" style="transform:rotate(${Number(member.bearing) || 0}deg);border-bottom-color:${color}"></span><span class="marker-face" style="background:${color}">${initials}</span><span class="marker-status ${esc(member.status)}"></span>`,
        iconSize: [38, 38], iconAnchor: [19, 19],
      });
      const marker = L.marker(member.position, { icon, opacity: muted ? 0.55 : 1, zIndexOffset: selected === member.id ? 1000 : 0 }).addTo(group);
      const tip = document.createElement('span');
      tip.textContent = `${member.name} · ${member.speed} km/jam`;
      marker.bindTooltip(tip, { direction: 'top', offset: [0, -22], className: 'team-tooltip' });
      marker.on('click', () => this.onSelect(member.id));
    }

    if (!demo && !this.autoFitted) {
      const positions = members.filter(m => m.trace.length).map(m => m.position);
      if (positions.length) { this.map.fitBounds(L.latLngBounds(positions).pad(0.25), { maxZoom: 14 }); this.autoFitted = true; }
    }
    if (selected) {
      const member = members.find(m => m.id === selected);
      if (member?.trace.length) this.map.panTo(member.position, { animate: true, duration: 0.25 });
    }
  }
}
