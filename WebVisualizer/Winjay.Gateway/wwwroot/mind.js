import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';

// ---------------------------------------------------------------------------
// WINJAY XR — living mind map renderer.
// The scene is a pure function of the event journal: every node, edge and
// strength value on screen is rebuilt by folding events — nothing is faked,
// and the rewind slider is the same fold over a shorter log prefix.
// ---------------------------------------------------------------------------

const COLORS = {
  Explorer: 0x4FC3F7,
  Connector: 0x66BB6A,
  Challenger: 0xFF8A65,
  Human: 0xFFD54F,
  Root: 0xECEFF1
};

const EDGE_STYLE = {
  Branch:   { color: 0x78909C, radius: 0.014, glow: 0.10 },
  Support:  { color: 0x66BB6A, radius: 0.028, glow: 0.22 },
  Conflict: { color: 0xEF5350, radius: 0.030, glow: 0.26 },
  Bridge:   { color: 0x26A69A, radius: 0.026, glow: 0.20 }
};

const EDGE_VERB = {
  Branch: 'branches',
  Support: 'supports',
  Conflict: 'challenges',
  Bridge: 'bridges'
};

const DAY_COLORS = { 'Day 1': 0x81D4FA, 'Day 2': 0xA5D6A7, 'Day 3': 0xFFE082 };
const KNOWN_DAYS = ['Day 1', 'Day 2', 'Day 3'];

const BASE_STRENGTH = 0.50;
const HUMAN_BASE_STRENGTH = 0.70;
const GHOST_STRENGTH = 0.25;

// ------------------------------------------------------------------ state

const journal = [];
let viewSeq = Infinity;        // rewind pointer; fold covers journal[0..viewSeq)
let live = true;
let speedMs = 700;
let running = false;
let connected = false;
let mergeSourceId = null;
let selectedId = null;
let ws = null;

const nodes = new Map();
const links = new Map();

// ------------------------------------------------------------------ scene

const wrap = document.getElementById('canvas-wrap');
const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
renderer.setSize(window.innerWidth, window.innerHeight);
wrap.appendChild(renderer.domElement);

const scene = new THREE.Scene();
scene.background = new THREE.Color(0x0b0e14);
scene.fog = new THREE.Fog(0x0b0e14, 16, 30);

const camera = new THREE.PerspectiveCamera(48, window.innerWidth / window.innerHeight, 0.1, 100);
camera.position.set(0.5, 8.2, 8.4);

const controls = new OrbitControls(camera, renderer.domElement);
controls.target.set(0, 0, 0);
controls.enableDamping = true;
controls.dampingFactor = 0.08;
controls.maxPolarAngle = Math.PI * 0.49;
controls.minDistance = 3;
controls.maxDistance = 22;

scene.add(new THREE.HemisphereLight(0x9fc4ff, 0x0b0f14, 0.85));
const key = new THREE.DirectionalLight(0xffffff, 1.15);
key.position.set(5, 9, 4);
scene.add(key);
const rim = new THREE.DirectionalLight(0x4da3ff, 0.35);
rim.position.set(-6, 4, -5);
scene.add(rim);

const grid = new THREE.GridHelper(14, 28, 0x1b2a3a, 0x111c28);
grid.position.y = -0.02;
scene.add(grid);

const nodeObjs = new Map();   // id -> visual bundle
const linkObjs = new Map();   // linkId -> visual bundle
const raycaster = new THREE.Raycaster();
const pointer = new THREE.Vector2();
const clock = new THREE.Clock();

// ------------------------------------------------------------------ fold

function fold() {
  nodes.clear();
  links.clear();
  const upto = viewSeq === Infinity ? journal.length : Math.min(viewSeq, journal.length);
  for (let i = 0; i < upto; i++) applyEvent(journal[i], i);
}

function applyEvent(e, index) {
  switch (e.type) {
    case 'NodeAdded':
    case 'GhostSuggested':
    case 'HumanInput':
      nodes.set(e.nodeId, {
        id: e.nodeId,
        label: e.label || e.nodeId,
        kind: e.kind || 'Idea',
        contributor: e.contributor || 'Explorer',
        parentId: e.parentId || null,
        pos: new THREE.Vector3(e.x || 0, e.y || 0, e.z || 0),
        ghost: e.type === 'GhostSuggested',
        locked: false,
        pruned: false,
        phase: e.phase || null,
        strength: e.type === 'GhostSuggested'
          ? GHOST_STRENGTH
          : (e.contributor === 'Human' ? HUMAN_BASE_STRENGTH : BASE_STRENGTH),
        born: index
      });
      break;
    case 'LinkCreated':
      links.set(e.linkId, {
        id: e.linkId,
        from: e.fromId,
        to: e.toId,
        type: e.linkType || 'Branch',
        originator: e.originator || 'Connector',
        weight: e.weight == null ? 1 : e.weight,
        detail: e.detail || '',
        active: true
      });
      break;
    case 'LinkRewired': {
      const l = links.get(e.linkId);
      if (l) { l.from = e.fromId; l.to = e.toId; }
      break;
    }
    case 'StrengthChanged': {
      const n = nodes.get(e.nodeId);
      if (n) n.strength = e.newStrength;
      break;
    }
    case 'NodePruned': { const n = nodes.get(e.nodeId); if (n) n.pruned = true; break; }
    case 'LinkDeactivated': { const l = links.get(e.linkId); if (l) l.active = false; break; }
    case 'NodeLocked': { const n = nodes.get(e.nodeId); if (n) n.locked = true; break; }
    case 'NodeUnlocked': { const n = nodes.get(e.nodeId); if (n) n.locked = false; break; }
    case 'GhostAccepted': { const n = nodes.get(e.nodeId); if (n) n.ghost = false; break; }
    case 'GhostRejected': { const n = nodes.get(e.nodeId); if (n) n.pruned = true; break; }
    case 'NodeMerged': { const n = nodes.get(e.nodeId); if (n) n.pruned = true; break; }
    case 'PhaseAssigned': { const n = nodes.get(e.nodeId); if (n) n.phase = e.phase; break; }
    default: break;
  }
}

// ------------------------------------------------------------------ visuals

function nodeColor(n) {
  if (n.kind === 'Root') return COLORS.Root;
  return COLORS[n.contributor] || COLORS.Explorer;
}

function makeLabelSprite(text, color, accent) {
  const pad = 18;
  const font = '600 40px "Segoe UI", sans-serif';
  const canvas = document.createElement('canvas');
  const ctx = canvas.getContext('2d');
  ctx.font = font;
  const width = Math.ceil(ctx.measureText(text).width) + pad * 2;
  const height = 74;
  canvas.width = width;
  canvas.height = height;

  const c2 = canvas.getContext('2d');
  c2.fillStyle = 'rgba(8, 12, 18, 0.78)';
  roundRect(c2, 2, 6, width - 4, height - 12, 14);
  c2.fill();
  c2.strokeStyle = '#' + new THREE.Color(accent).getHexString();
  c2.lineWidth = 3;
  roundRect(c2, 2, 6, width - 4, height - 12, 14);
  c2.stroke();
  c2.font = font;
  c2.fillStyle = '#' + new THREE.Color(color).getHexString();
  c2.textBaseline = 'middle';
  c2.fillText(text, pad, height / 2 + 1);

  const texture = new THREE.CanvasTexture(canvas);
  texture.anisotropy = 4;
  const material = new THREE.SpriteMaterial({ map: texture, transparent: true, depthTest: false });
  const sprite = new THREE.Sprite(material);
  const scale = 0.0042;
  sprite.scale.set(width * scale, height * scale, 1);
  sprite.renderOrder = 30;
  return sprite;
}

function roundRect(ctx, x, y, w, h, r) {
  ctx.beginPath();
  ctx.moveTo(x + r, y);
  ctx.arcTo(x + w, y, x + w, y + h, r);
  ctx.arcTo(x + w, y + h, x, y + h, r);
  ctx.arcTo(x, y + h, x, y, r);
  ctx.arcTo(x, y, x + w, y, r);
  ctx.closePath();
}

function createNodeObject(n) {
  const group = new THREE.Group();
  group.position.copy(n.pos);

  const color = nodeColor(n);
  const radius = n.kind === 'Root' ? 0.40 : n.kind === 'Branch' ? 0.30 : 0.26;
  const sphere = new THREE.Mesh(
    new THREE.SphereGeometry(radius, 26, 18),
    new THREE.MeshStandardMaterial({
      color: color, roughness: 0.35, metalness: 0.15,
      emissive: color, emissiveIntensity: 0.28,
      transparent: true, opacity: 1
    })
  );
  group.add(sphere);

  const ring = new THREE.Mesh(
    new THREE.TorusGeometry(0.46, 0.016, 8, 40),
    new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.55 })
  );
  ring.rotation.x = Math.PI / 2;
  group.add(ring);

  const lock = new THREE.Mesh(
    new THREE.TorusGeometry(0.56, 0.022, 8, 44),
    new THREE.MeshBasicMaterial({ color: 0xffd700, transparent: true, opacity: 0.0 })
  );
  lock.rotation.x = Math.PI / 2;
  group.add(lock);

  const dayDisc = new THREE.Mesh(
    new THREE.CircleGeometry(0.62, 36),
    new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.0 })
  );
  dayDisc.rotation.x = -Math.PI / 2;
  dayDisc.position.y = -0.34;
  group.add(dayDisc);

  const label = makeLabelSprite(n.label, color, 0x2a3850);
  label.position.set(0, radius + 0.42, 0);
  group.add(label);

  const bundle = {
    group, sphere, ring, lock, dayDisc, label,
    radius,
    spawnT: -1,
    spawnFrom: new THREE.Vector3(),
    targetScale: 1,
    scale: 1,
    ringScale: 1
  };
  group.userData.nodeId = n.id;
  sphere.userData.nodeId = n.id;
  return bundle;
}

function positionNodeObject(bundle, n, isNew) {
  if (isNew) {
    const parent = n.parentId ? nodes.get(n.parentId) : null;
    bundle.spawnT = 0;
    bundle.spawnFrom.copy(parent ? parent.pos : n.pos);
    bundle.scale = 0.01;
    bundle.group.position.copy(bundle.spawnFrom);
    bundle.label.material.opacity = 0;
  }
}

function updateNodeObject(bundle, n, dt, time) {
  if (bundle.spawnT >= 0 && bundle.spawnT < 1) {
    bundle.spawnT = Math.min(1, bundle.spawnT + dt / 0.9);
    const t = easeOutCubic(bundle.spawnT);
    bundle.group.position.lerpVectors(bundle.spawnFrom, n.pos, t);
    bundle.label.material.opacity = Math.max(0, (bundle.spawnT - 0.45) / 0.55);
  } else {
    bundle.group.position.copy(n.pos);
    bundle.label.material.opacity = 1;
  }

  const target = n.pruned ? 0 : 1;
  bundle.targetScale = target;
  bundle.scale += (bundle.targetScale - bundle.scale) * Math.min(1, dt * 6);
  let pulse = 1;
  if (n.ghost) pulse = 1 + 0.09 * Math.sin(time * 4.2);
  bundle.group.scale.setScalar(Math.max(0.001, bundle.scale * pulse));
  bundle.group.visible = bundle.scale > 0.02;

  const color = nodeColor(n);
  const mat = bundle.sphere.material;
  mat.color.setHex(color);
  mat.emissive.setHex(color);
  mat.opacity = n.ghost ? 0.42 : n.pruned ? Math.max(0, bundle.scale) : 1;
  mat.emissiveIntensity = n.ghost ? 0.5 : 0.28;

  // strength ring: shrinks with the idea's strength, reddens when weak
  const ringTarget = 0.55 + 0.75 * clamp01(n.strength);
  bundle.ringScale += (ringTarget - bundle.ringScale) * Math.min(1, dt * 4);
  bundle.ring.scale.setScalar(bundle.ringScale);
  const low = n.strength < 0.35;
  bundle.ring.material.color.setHex(low ? 0xEF5350 : 0xffffff);
  bundle.ring.material.opacity = n.ghost ? 0.2 : 0.55;

  bundle.lock.material.opacity += ((n.locked ? 0.95 : 0) - bundle.lock.material.opacity) * Math.min(1, dt * 5);
  bundle.lock.scale.setScalar(bundle.scale);

  const day = KNOWN_DAYS.includes(n.phase) ? n.phase : null;
  bundle.dayDisc.material.opacity += ((day ? 0.3 : 0) - bundle.dayDisc.material.opacity) * Math.min(1, dt * 4);
  if (day) bundle.dayDisc.material.color.setHex(DAY_COLORS[day]);

  if (selectedId === n.id) {
    bundle.sphere.material.emissiveIntensity = 0.85;
  }
}

function easeOutCubic(t) {
  return 1 - Math.pow(1 - t, 3);
}

function clamp01(v) {
  return Math.max(0, Math.min(1, v));
}

function beamBetween(a, b, radius, material) {
  const dir = new THREE.Vector3().subVectors(b, a);
  const len = dir.length();
  const beam = new THREE.Mesh(new THREE.CylinderGeometry(radius, radius, 1, 8), material);
  beam.position.copy(a).add(b).multiplyScalar(0.5);
  beam.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), dir.clone().normalize());
  beam.scale.set(1, len, 1);
  return beam;
}

function createLinkObject(l) {
  const group = new THREE.Group();
  const style = EDGE_STYLE[l.type] || EDGE_STYLE.Branch;
  const material = new THREE.MeshBasicMaterial({
    color: style.color, transparent: true, opacity: 0
  });
  const glowMaterial = new THREE.MeshBasicMaterial({
    color: style.color, transparent: true, opacity: 0, blending: THREE.AdditiveBlending, depthWrite: false
  });

  const bundle = { group, material, glowMaterial, style, curve: null, vibration: 0, fade: 0 };
  group.userData.linkId = l.id;
  return bundle;
}

function rebuildLinkGeometry(bundle, l) {
  const a = nodes.get(l.from), b = nodes.get(l.to);
  if (!a || !b) return false;
  disposeChildren(bundle.group);

  if (l.type === 'Bridge') {
    const mid = new THREE.Vector3().addVectors(a.pos, b.pos).multiplyScalar(0.5);
    mid.y += 0.55 + a.pos.distanceTo(b.pos) * 0.14;
    const curve = new THREE.QuadraticBezierCurve3(a.pos, mid, b.pos);
    const tube = new THREE.Mesh(new THREE.TubeGeometry(curve, 36, bundle.style.radius, 6, false), bundle.material);
    const glowTube = new THREE.Mesh(new THREE.TubeGeometry(curve, 36, bundle.style.radius * 2.6, 6, false), bundle.glowMaterial);
    bundle.group.add(tube, glowTube);
    bundle.curve = curve;
  } else {
    const beam = beamBetween(a.pos, b.pos, bundle.style.radius, bundle.material);
    const glow = beamBetween(a.pos, b.pos, bundle.style.radius * 2.6, bundle.glowMaterial);
    bundle.group.add(beam, glow);
    bundle.curve = null;
  }
  return true;
}

function syncScene() {
  // nodes
  for (const [id, n] of nodes) {
    let bundle = nodeObjs.get(id);
    if (!bundle) {
      bundle = createNodeObject(n);
      nodeObjs.set(id, bundle);
      scene.add(bundle.group);
      positionNodeObject(bundle, n, true);
    }
  }
  for (const [id, bundle] of [...nodeObjs]) {
    if (!nodes.has(id)) {
      scene.remove(bundle.group);
      disposeChildren(bundle.group);
      bundle.label.material.map.dispose();
      bundle.label.material.dispose();
      nodeObjs.delete(id);
    }
  }

  // links
  for (const [id, l] of links) {
    const signature = l.from + '>' + l.to + ':' + l.type;
    let bundle = linkObjs.get(id);
    if (!bundle || bundle.signature !== signature) {
      if (bundle) {
        scene.remove(bundle.group);
        disposeChildren(bundle.group);
      }
      bundle = createLinkObject(l);
      bundle.signature = signature;
      if (!rebuildLinkGeometry(bundle, l)) continue;
      linkObjs.set(id, bundle);
      scene.add(bundle.group);
      bundle.popT = 0;
    }
  }
  for (const [id, bundle] of [...linkObjs]) {
    const l = links.get(id);
    if (!l || !nodes.has(l.from) || !nodes.has(l.to)) {
      scene.remove(bundle.group);
      disposeChildren(bundle.group);
      linkObjs.delete(id);
    }
  }
}

function disposeChildren(group) {
  for (const child of [...group.children]) {
    group.remove(child);
    if (child.geometry) child.geometry.dispose();
  }
}

function updateLinks(dt, time) {
  for (const [id, bundle] of linkObjs) {
    const l = links.get(id);
    if (!l) continue;
    const target = l.active ? 1 : 0;
    bundle.fade += (target - bundle.fade) * Math.min(1, dt * 3.5);
    if (bundle.popT !== undefined && bundle.popT < 1) bundle.popT = Math.min(1, bundle.popT + dt * 3);

    const alpha = bundle.fade * (bundle.popT === undefined ? 1 : bundle.popT);
    bundle.material.opacity = alpha * 0.9;
    bundle.glowMaterial.opacity = alpha * bundle.style.glow;
    bundle.group.visible = alpha > 0.02;

    if (l.type === 'Conflict' && bundle.group.children.length >= 2 && alpha > 0.02) {
      const a = nodes.get(l.from), b = nodes.get(l.to);
      if (a && b) {
        const dir = new THREE.Vector3().subVectors(b.pos, a.pos).normalize();
        const perp = new THREE.Vector3().crossVectors(dir, new THREE.Vector3(0, 1, 0)).normalize();
        const shake = Math.sin(time * 26 + id.length * 1.7) * 0.05 + Math.sin(time * 41) * 0.012;
        bundle.group.position.copy(perp.multiplyScalar(shake));
      }
    } else {
      bundle.group.position.set(0, 0, 0);
    }
  }
}

// ------------------------------------------------------------------ feed

const feedLines = document.getElementById('feedLines');

const ACTOR_CLASS = {
  Explorer: 'a-explorer',
  Connector: 'a-connector',
  Challenger: 'a-challenger',
  Human: 'a-human',
  engine: 'a-engine'
};

function feedPush(html, cls, idx) {
  const div = document.createElement('div');
  div.className = 'feed-line ' + (cls || '');
  div.innerHTML = html;
  if (idx != null) div.dataset.idx = idx;
  feedLines.prepend(div);
  while (feedLines.children.length > 120) feedLines.removeChild(feedLines.lastChild);
}

function updateFeedDim() {
  const upto = viewSeq === Infinity ? journal.length : Math.min(viewSeq, journal.length);
  for (const div of feedLines.children) {
    const idx = div.dataset.idx;
    div.classList.toggle('future', idx != null && Number(idx) >= upto);
  }
}

function actorChip(actor) {
  const label = actor === 'Human' ? 'you' : actor === 'engine' ? '~' : actor.charAt(0);
  return `<span class="chip-actor ${ACTOR_CLASS[actor] || 'a-engine'}">${escapeHtml(label)}</span>`;
}

function linkText(l) {
  const from = nodes.get(l.from), to = nodes.get(l.to);
  const fl = from ? from.label : l.from;
  const tl = to ? to.label : l.to;
  return `${EDGE_VERB[l.type] || l.type} \u201C${tl}\u201D ${l.type === 'Branch' ? 'under' : l.type === 'Conflict' ? '\u00D7' : '\u2192'} \u201C${fl}\u201D`;
}

function eventLine(e) {
  switch (e.type) {
    case 'NodeAdded': return `grows \u201C${e.label}\u201D`;
    case 'GhostSuggested': return `suggests ghost \u201C${e.label}\u201D`;
    case 'HumanInput': return `you speak: \u201C${e.label}\u201D`;
    case 'LinkCreated': return linkText({ from: e.fromId, to: e.toId, type: e.linkType });
    case 'StrengthChanged': return `ripple: \u201C${labelOf(e.nodeId)}\u201D ${fmt(e.oldStrength)} \u2192 ${fmt(e.newStrength)}`;
    case 'NodePruned': return `prunes \u201C${labelOf(e.nodeId)}\u201D`;
    case 'LinkDeactivated': return `link fades: ${labelOf(e.fromId)} \u00D7 ${labelOf(e.toId)}`;
    case 'LinkRewired': return `link rewired: ${labelOf(e.fromId)} \u2192 ${labelOf(e.toId)}`;
    case 'NodeLocked': return `locks \u201C${labelOf(e.nodeId)}\u201D (gold \u2014 agents must work around it)`;
    case 'NodeUnlocked': return `unlocks \u201C${labelOf(e.nodeId)}\u201D`;
    case 'GhostAccepted': return `accepts \u201C${labelOf(e.nodeId)}\u201D`;
    case 'GhostRejected': return `rejects \u201C${labelOf(e.nodeId)}\u201D`;
    case 'NodeMerged': return `merges \u201C${labelOf(e.nodeId)}\u201D into \u201C${labelOf(e.mergedIntoId)}\u201D`;
    case 'PhaseAssigned': return `settles \u201C${labelOf(e.nodeId)}\u201D into ${e.phase}`;
    default: return e.type;
  }
}

function labelOf(id) {
  const n = nodes.get(id);
  return n ? n.label : id;
}

function fmt(v) {
  return v == null ? '?' : Number(v).toFixed(2);
}

function escapeHtml(s) {
  return String(s).replace(/[&<>"']/g, c => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
  })[c]);
}

// ------------------------------------------------------------------ ws

const connEl = document.getElementById('conn');
const stateEl = document.getElementById('state');
const topicEl = document.getElementById('topic');

function connect() {
  ws = new WebSocket(`ws://${location.host}/ws/mind`);
  ws.onopen = () => { connected = true; setConn(true); };
  ws.onclose = () => {
    connected = false; running = false; setConn(false);
    setTimeout(connect, 1500);
  };
  ws.onmessage = evt => handleMessage(JSON.parse(evt.data));
}

function setConn(on) {
  connEl.className = 'conn ' + (on ? 'on' : 'off');
  connEl.textContent = on ? 'connected' : 'disconnected \u2014 reconnecting\u2026';
}

function send(obj) {
  if (ws && ws.readyState === 1) ws.send(JSON.stringify(obj));
}

function handleMessage(msg) {
  switch (msg.type) {
    case 'hello':
      journal.length = 0;
      if (msg.journal) journal.push(...msg.journal);
      viewSeq = Infinity;
      live = true;
      running = false;
      selectedId = null;
      mergeSourceId = null;
      topicEl.textContent = msg.scenario || '';
      feedLines.innerHTML = '';
      for (let i = 0; i < journal.length; i++) {
        feedPush(actorChip(journal[i].actor) + escapeHtml(eventLine(journal[i])), clsFor(journal[i]), i);
      }
      updateRewindRange();
      fold();
      syncScene();
      updateFeedDim();
      updateState(msg.state);
      break;

    case 'step': {
      const incoming = msg.events || [];
      const baseIdx = journal.length;
      journal.push(...incoming);
      for (const a of msg.actions || []) {
        if (!a.ok) {
          feedPush(actorChip(a.actor) + `<span class="rej">REJECTED (${escapeHtml(a.code || '?')})</span> \u2014 ${escapeHtml(a.note)}`, 'rejected');
        }
      }
      for (let k = 0; k < incoming.length; k++) {
        feedPush(actorChip(incoming[k].actor) + escapeHtml(eventLine(incoming[k])), clsFor(incoming[k]), baseIdx + k);
      }
      if (live) {
        viewSeq = Infinity;
      }
      updateRewindRange();
      fold();
      syncScene();
      updateFeedDim();
      updateState(msg.state, msg.settledReason);
      break;
    }

    case 'done':
      running = false;
      updateState('settled', msg.reason);
      break;
  }
}

function clsFor(e) {
  if (e.type === 'StrengthChanged' || e.type === 'LinkDeactivated' || e.type === 'LinkRewired') return 'derived';
  if (e.type === 'NodePruned' || e.type === 'GhostRejected') return 'tense';
  if (e.type === 'LinkCreated' && e.linkType === 'Conflict') return 'tense';
  return '';
}

function updateState(state, reason) {
  if (state === 'busy') {
    stateEl.textContent = 'agents working\u2026';
    stateEl.className = 'state busy';
  } else if (state === 'settled') {
    stateEl.textContent = 'settled \u2014 ' + (reason || 'map at rest');
    stateEl.className = 'state settled';
  } else if (state === 'idle') {
    stateEl.textContent = 'AWAITING CURATOR \u2014 click a node to prune or lock, accept a ghost, or speak';
    stateEl.className = 'state idle';
  } else {
    stateEl.textContent = 'ready \u2014 press RUN for the scripted demo';
    stateEl.className = 'state';
  }
}

// ------------------------------------------------------------------ controls

document.getElementById('btnRun').addEventListener('click', () => {
  if (running) { send({ type: 'step' }); running = false; }
  send({ type: 'run' });
  running = true;
});
document.getElementById('btnStep').addEventListener('click', () => send({ type: 'step' }));
document.getElementById('btnReset').addEventListener('click', () => send({ type: 'reset' }));
document.getElementById('speed').addEventListener('input', e => {
  speedMs = Number(e.target.value);
  send({ type: 'speed', ms: speedMs });
});

document.getElementById('btnSay').addEventListener('click', speak);
document.getElementById('sayInput').addEventListener('keydown', e => {
  if (e.key === 'Enter') speak();
});

function speak() {
  const input = document.getElementById('sayInput');
  const label = input.value.trim();
  if (!label) return;
  send({ type: 'say', label });
  input.value = '';
}

// rewind slider: scrub the fold through the journal prefix
const range = document.getElementById('rewindRange');
const rewindLabel = document.getElementById('rewindLabel');

function updateRewindRange() {
  range.max = journal.length;
  if (live) range.value = journal.length;
  rewindLabel.textContent = live ? 'live' : `viewing #${range.value} / ${journal.length}`;
}

range.addEventListener('input', () => {
  const v = Number(range.value);
  live = v >= journal.length;
  viewSeq = live ? Infinity : v;
  rewindLabel.textContent = live ? 'live' : `viewing #${v} / ${journal.length}`;
  fold();
  syncScene();
  updateFeedDim();
});

document.getElementById('btnLive').addEventListener('click', () => {
  live = true;
  viewSeq = Infinity;
  range.value = journal.length;
  rewindLabel.textContent = 'live';
  fold();
  syncScene();
  updateFeedDim();
});

// ------------------------------------------------------------------ picking

const inspector = document.getElementById('inspector');
const inspTitle = document.getElementById('inspTitle');
const inspBody = document.getElementById('inspBody');
const inspActions = document.getElementById('inspActions');
const whyBox = document.getElementById('whyBox');

renderer.domElement.addEventListener('pointerdown', e => {
  if (e.button !== 0) return;
  pointer.x = (e.clientX / window.innerWidth) * 2 - 1;
  pointer.y = -(e.clientY / window.innerHeight) * 2 + 1;
  raycaster.setFromCamera(pointer, camera);

  const nodeMeshes = [];
  for (const bundle of nodeObjs.values()) {
    if (bundle.scale > 0.5 && bundle.group.visible) nodeMeshes.push(bundle.sphere);
  }
  const nodeHits = raycaster.intersectObjects(nodeMeshes, false);
  if (nodeHits.length > 0) {
    const id = nodeHits[0].object.userData.nodeId;
    if (mergeSourceId && mergeSourceId !== id) {
      send({ type: 'merge', fromId: mergeSourceId, intoId: id });
      mergeSourceId = null;
      hideInspector();
      return;
    }
    selectNode(id, e.clientX, e.clientY);
    return;
  }

  const linkMeshes = [];
  for (const bundle of linkObjs.values()) for (const child of bundle.group.children) linkMeshes.push(child);
  const linkHits = raycaster.intersectObjects(linkMeshes, false);
  if (linkHits.length > 0) {
    const linkId = linkHits[0].object.parent.userData.linkId;
    showWhy(linkId, e.clientX, e.clientY);
    return;
  }

  selectedId = null;
  hideInspector();
  whyBox.classList.add('hidden');
});

function selectNode(id, x, y) {
  selectedId = id;
  const n = nodes.get(id);
  if (!n) return;
  inspTitle.textContent = n.kind === 'Root' ? 'ROOT IDEA' : n.kind.toUpperCase();
  const linksOf = [...links.values()].filter(l => l.from === id || l.to === id);
  const rows = linksOf.map(l => {
    if (l.type === 'Branch') return '';
    const other = l.from === id ? l.to : l.from;
    const arrow = l.type === 'Conflict' ? '\u00D7' : '\u2192';
    return `<div class="insp-link">${l.type.toLowerCase()} ${arrow} \u201C${escapeHtml(labelOf(other))}\u201D` +
      (l.detail ? `<div class="insp-detail">${escapeHtml(l.detail)}</div>` : '') + '</div>';
  }).join('');

  inspBody.innerHTML =
    `<div class="insp-label">\u201C${escapeHtml(n.label)}\u201D</div>` +
    `<div class="insp-meta">made by ${escapeHtml(n.contributor)} \u00B7 strength <b>${n.strength.toFixed(2)}</b>` +
    (n.phase ? ` \u00B7 ${escapeHtml(n.phase)}` : '') +
    (n.locked ? ' \u00B7 <span class="gold">locked</span>' : '') +
    (n.ghost ? ' \u00B7 <span class="ghost">ghost suggestion</span>' : '') +
    (n.pruned ? ' \u00B7 pruned' : '') + '</div>' + rows;

  const actions = [];
  if (!n.pruned) {
    if (n.ghost) {
      actions.push(`<button data-act="acceptGhost">ACCEPT</button>`);
      actions.push(`<button data-act="rejectGhost">REJECT</button>`);
    } else {
      if (n.locked) actions.push(`<button data-act="unlock">UNLOCK</button>`);
      else actions.push(`<button data-act="lock">LOCK (GOLD)</button>`);
      actions.push(`<button data-act="prune">PRUNE</button>`);
      if (n.kind !== 'Root') actions.push(`<button data-act="merge">MERGE INTO\u2026</button>`);
    }
  }
  inspActions.innerHTML = actions.join('');

  inspActions.querySelectorAll('button').forEach(btn => {
    btn.addEventListener('click', () => {
      const act = btn.dataset.act;
      if (act === 'merge') {
        mergeSourceId = id;
        inspBody.innerHTML = `<div class="insp-label">merging \u201C${escapeHtml(n.label)}\u201D</div>` +
          '<div class="insp-meta">click the node that should absorb it\u2026</div>';
        inspActions.innerHTML = '';
        return;
      }
      send({ type: act, id });
      hideInspector();
    });
  });

  inspector.classList.remove('hidden');
  inspector.style.left = Math.min(x + 16, window.innerWidth - 300) + 'px';
  inspector.style.top = Math.min(y + 16, window.innerHeight - 260) + 'px';
}

function hideInspector() {
  inspector.classList.add('hidden');
  selectedId = null;
}

function showWhy(linkId, x, y) {
  const l = links.get(linkId);
  if (!l) return;
  const verb = EDGE_VERB[l.type] || l.type;
  whyBox.innerHTML =
    `<div class="why-type" style="color:#${new THREE.Color((EDGE_STYLE[l.type] || EDGE_STYLE.Branch).color).getHexString()}">${verb}</div>` +
    `<div>\u201C${escapeHtml(labelOf(l.from))}\u201D \u2192 \u201C${escapeHtml(labelOf(l.to))}\u201D</div>` +
    (l.detail ? `<div class="why-detail">${escapeHtml(l.detail)}</div>` : '') +
    `<div class="why-actor">found by ${escapeHtml(l.originator)}</div>`;
  whyBox.classList.remove('hidden');
  whyBox.style.left = Math.min(x + 14, window.innerWidth - 320) + 'px';
  whyBox.style.top = Math.min(y + 14, window.innerHeight - 160) + 'px';
  clearTimeout(showWhy.timer);
  showWhy.timer = setTimeout(() => whyBox.classList.add('hidden'), 5000);
}

// ------------------------------------------------------------------ loop

window.addEventListener('resize', () => {
  camera.aspect = window.innerWidth / window.innerHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(window.innerWidth, window.innerHeight);
});

function animate() {
  requestAnimationFrame(animate);
  const dt = Math.min(clock.getDelta(), 0.05);
  const time = clock.elapsedTime;

  for (const [id, n] of nodes) {
    const bundle = nodeObjs.get(id);
    if (!bundle) continue;
    updateNodeObject(bundle, n, dt, time);
  }

  updateLinks(dt, time);
  controls.update();
  renderer.render(scene, camera);
}

connect();
animate();
