import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';

// ------------------------------------------------------------------ layout

const STATE_COLORS = {
  Created: '#9E9E9E',
  Investigating: '#42A5F5',
  Supported: '#66BB6A',
  Resolved: '#FFCA28',
  Conflicted: '#FF7043',
  Falsifying: '#AB47BC',
  Failed: '#EF5350',
};

const AGENTS = [
  { name: 'MockResearcher', pos: new THREE.Vector3(-5.5, 1.7, 0), color: '#4FC3F7' },
  { name: 'MockAnalyzer', pos: new THREE.Vector3(0, 1.7, -4.8), color: '#81C784' },
  { name: 'MockFalsifier', pos: new THREE.Vector3(5.5, 1.7, 0), color: '#FF8A65' },
  { name: 'MockDecision', pos: new THREE.Vector3(0, 5.4, 0), color: '#CE93D8' },
];

const SLOTS = {
  E001: new THREE.Vector3(-2.6, 1.3, 1.4),
  E002: new THREE.Vector3(-2.6, 1.3, -1.4),
  E003: new THREE.Vector3(2.8, 1.3, 1.2),
  E004: new THREE.Vector3(2.8, 1.3, -1.2),
  H001: new THREE.Vector3(0, 2.0, 0),
  CF001: new THREE.Vector3(1.7, 1.35, 0.5),
  DC001: new THREE.Vector3(0, 4.1, 0),
};

function slotPos(id) {
  if (SLOTS[id]) return SLOTS[id].clone();
  const n = id.length + id.charCodeAt(id.length - 1);
  return new THREE.Vector3(((n % 5) - 2) * 1.6, 1.3, ((n % 3) - 1) * 1.6);
}

function agentByName(name) {
  return AGENTS.find((a) => a.name === name);
}

function evidenceColor(source) {
  const s = (source || '').toLowerCase();
  if (s.includes('falsifier')) return '#FF8A65';
  if (s.includes('researcher')) return '#4FC3F7';
  return '#B0BEC5';
}

// ------------------------------------------------------------------ scene

const wrap = document.getElementById('canvas-wrap');
const scene = new THREE.Scene();
scene.background = new THREE.Color(0x0b0e14);
scene.fog = new THREE.Fog(0x0b0e14, 16, 44);

const camera = new THREE.PerspectiveCamera(55, 1, 0.1, 100);
camera.position.set(7.5, 5.2, 11.5);

const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.5));
renderer.setSize(window.innerWidth, window.innerHeight);
wrap.appendChild(renderer.domElement);

const controls = new OrbitControls(camera, renderer.domElement);
controls.target.set(0, 2, 0);
controls.enableDamping = true;
controls.dampingFactor = 0.08;
controls.minDistance = 4;
controls.maxDistance = 30;
controls.minPolarAngle = 0.15;
controls.maxPolarAngle = Math.PI * 0.55;

scene.add(new THREE.AmbientLight(0x8899bb, 1.5));
const dirLight = new THREE.DirectionalLight(0xffffff, 1.4);
dirLight.position.set(6, 10, 6);
scene.add(dirLight);

const grid = new THREE.GridHelper(26, 26, 0x24304a, 0x131a28);
scene.add(grid);

const edgeGroup = new THREE.Group();
const entityGroup = new THREE.Group();
const agentGroup = new THREE.Group();
const effectGroup = new THREE.Group();
scene.add(edgeGroup, entityGroup, agentGroup, effectGroup);

// ------------------------------------------------------------------ helpers

function roundRectPath(x, px, py, w, h, r) {
  x.beginPath();
  x.moveTo(px + r, py);
  x.arcTo(px + w, py, px + w, py + h, r);
  x.arcTo(px + w, py + h, px, py + h, r);
  x.arcTo(px, py + h, px, py, r);
  x.arcTo(px, py, px + w, py, r);
  x.closePath();
}

function textSprite(text, opts = {}) {
  const w = opts.width || 768;
  const h = opts.height || 128;
  const fs = opts.fontSize || 44;
  const color = opts.color || '#dfe6f0';
  const pad = 12;
  const canvas = document.createElement('canvas');
  canvas.width = w;
  canvas.height = h;
  const sprite = new THREE.Sprite(new THREE.SpriteMaterial({
    map: new THREE.CanvasTexture(canvas),
    transparent: true,
    depthTest: false,
    depthWrite: false,
  }));
  sprite.renderOrder = 50;

  function draw(t) {
    const x = canvas.getContext('2d');
    x.clearRect(0, 0, w, h);
    x.font = `700 ${fs}px "Segoe UI", system-ui, sans-serif`;
    x.textAlign = 'center';
    x.textBaseline = 'middle';
    if (opts.bg) {
      roundRectPath(x, 4, 4, w - 8, h - 8, 14);
      x.fillStyle = 'rgba(11,14,20,0.80)';
      x.fill();
    }
    x.fillStyle = color;
    const maxW = w - 2 * pad;
    let line = t;
    if (x.measureText(t).width > maxW) {
      while (line.length > 1 && x.measureText(line + '…').width > maxW) line = line.slice(0, -1);
      line += '…';
    }
    x.fillText(line, w / 2, h / 2 + 2);
    sprite.material.map.needsUpdate = true;
  }

  draw(text);
  sprite.userData.setText = draw;
  const sy = opts.worldH || 0.3;
  sprite.scale.set((sy * w) / h, sy, 1);
  return sprite;
}

function drawConfidenceBar(conf) {
  const w = 300;
  const h = 64;
  const c = document.createElement('canvas');
  c.width = w;
  c.height = h;
  const x = c.getContext('2d');
  roundRectPath(x, 2, 2, w - 4, h - 4, 10);
  x.fillStyle = 'rgba(11,14,20,0.92)';
  x.fill();
  x.strokeStyle = '#26334a';
  x.lineWidth = 2;
  x.stroke();
  const fw = (w - 16) * Math.min(1, Math.max(0, conf));
  if (fw > 4) {
    roundRectPath(x, 8, 8, fw, h - 16, 7);
    x.fillStyle = `hsl(${Math.round(conf * 120)},70%,48%)`;
    x.fill();
  }
  x.fillStyle = '#ffffff';
  x.font = '700 26px Consolas, monospace';
  x.textAlign = 'center';
  x.textBaseline = 'middle';
  x.fillText(conf.toFixed(2), w / 2, h / 2 + 1);
  return c;
}

function confidenceSprite(conf) {
  const sprite = new THREE.Sprite(new THREE.SpriteMaterial({
    map: new THREE.CanvasTexture(drawConfidenceBar(conf)),
    transparent: true,
    depthTest: false,
    depthWrite: false,
  }));
  sprite.renderOrder = 51;
  sprite.scale.set(1.3, 1.3 * (64 / 300), 1);
  sprite.userData.setConf = (v) => {
    const ctx = sprite.material.map.image.getContext('2d');
    const fresh = drawConfidenceBar(v);
    ctx.clearRect(0, 0, 300, 64);
    ctx.drawImage(fresh, 0, 0);
    sprite.material.map.needsUpdate = true;
  };
  return sprite;
}

function anchorLine(pos, color) {
  const geo = new THREE.BufferGeometry().setFromPoints([
    new THREE.Vector3(pos.x, 0.02, pos.z),
    new THREE.Vector3(pos.x, pos.y - 0.15, pos.z),
  ]);
  return new THREE.Line(geo, new THREE.LineBasicMaterial({
    color: new THREE.Color(color),
    transparent: true,
    opacity: 0.22,
  }));
}

// ------------------------------------------------------------------ world model

const world = {
  entities: new Map(),   // id -> { id, type, state, confidence, ... , group }
  supportEdges: new Map(),    // "E001->H001" -> line
  contradictEdges: new Map(), // "E003->H001" -> { line, from, to }
  conflictX: null,
  flights: [],
  effects: [],
};

function resetWorld() {
  for (const e of world.entities.values()) {
    entityGroup.remove(e.group);
    disposeGroup(e.group);
  }
  world.entities.clear();
  for (const line of world.supportEdges.values()) {
    edgeGroup.remove(line);
    line.geometry.dispose();
    line.material.dispose();
  }
  world.supportEdges.clear();
  for (const entry of world.contradictEdges.values()) {
    edgeGroup.remove(entry.line);
    entry.line.geometry.dispose();
    entry.line.material.dispose();
  }
  world.contradictEdges.clear();
  if (world.conflictX) {
    entityGroup.remove(world.conflictX);
    disposeGroup(world.conflictX);
    world.conflictX = null;
  }
  world.flights = [];
  world.effects = [];
  document.getElementById('trailChips').innerHTML = '';
  document.getElementById('logLines').innerHTML = '';
  document.getElementById('tick').textContent = 'tick 0';
  document.getElementById('status').textContent = '';
}

function disposeGroup(g) {
  g.traverse((o) => {
    if (o.geometry) o.geometry.dispose();
    if (o.material) {
      const mats = Array.isArray(o.material) ? o.material : [o.material];
      mats.forEach((m) => {
        if (m.map) m.map.dispose();
        m.dispose();
      });
    }
  });
}

// ------------------------------------------------------------------ agents

const agentMeshes = new Map();

function buildAgents() {
  for (const agent of AGENTS) {
    const group = new THREE.Group();
    group.position.copy(agent.pos);

    const core = new THREE.Mesh(
      new THREE.SphereGeometry(0.24, 24, 24),
      new THREE.MeshStandardMaterial({
        color: new THREE.Color(agent.color),
        emissive: new THREE.Color(agent.color),
        emissiveIntensity: 0.55,
        roughness: 0.4,
      })
    );
    group.add(core);

    const ring = new THREE.Mesh(
      new THREE.TorusGeometry(0.36, 0.02, 8, 48),
      new THREE.MeshBasicMaterial({ color: new THREE.Color(agent.color), transparent: true, opacity: 0.8 })
    );
    group.add(ring);

    const label = textSprite(agent.name, {
      color: agent.color, fontSize: 40, width: 420, height: 80, worldH: 0.26,
    });
    label.position.y = -0.62;
    group.add(label);

    const glow = new THREE.Sprite(new THREE.SpriteMaterial({
      color: new THREE.Color(agent.color),
      transparent: true,
      opacity: 0.28,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
    }));
    glow.scale.set(1.9, 1.9, 1);
    group.add(glow);

    group.userData = { base: 1, pulse: 0, ring };
    agentGroup.add(group);
    agentMeshes.set(agent.name, group);
  }
}

function pulseAgent(name) {
  const g = agentMeshes.get(name);
  if (g) g.userData.pulse = 1;
}

// ------------------------------------------------------------------ entities

function makeEvidence(ent) {
  const pos = slotPos(ent.id);
  const color = evidenceColor(ent.source);
  const group = new THREE.Group();
  group.position.copy(pos);

  const mesh = new THREE.Mesh(
    new THREE.OctahedronGeometry(0.2, 0),
    new THREE.MeshStandardMaterial({
      color: new THREE.Color(color),
      emissive: new THREE.Color(color),
      emissiveIntensity: 0.45,
      roughness: 0.35,
    })
  );
  group.add(mesh);
  group.add(anchorLine(pos, color));

  const label = textSprite(`${ent.id} — ${ent.content || ''}`, {
    color, fontSize: 34, width: 560, height: 80, worldH: 0.24,
  });
  label.position.y = -0.55;
  group.add(label);

  group.userData = { mesh, pulse: 0 };
  entityGroup.add(group);
  return group;
}

function makeHypothesis(ent) {
  const pos = slotPos(ent.id);
  const group = new THREE.Group();
  group.position.copy(pos);

  const state = ent.state || 'Created';
  const color = STATE_COLORS[state] || '#9E9E9E';
  const mesh = new THREE.Mesh(
    new THREE.SphereGeometry(0.36, 32, 32),
    new THREE.MeshStandardMaterial({
      color: new THREE.Color(color),
      emissive: new THREE.Color(color),
      emissiveIntensity: 0.5,
      roughness: 0.3,
    })
  );
  group.add(mesh);
  group.add(anchorLine(pos, color));

  const bar = confidenceSprite(ent.confidence ?? 0.5);
  bar.position.y = 0.82;
  group.add(bar);

  const label = textSprite(`${ent.id} — ${ent.statement || ''}`, {
    color: '#dfe6f0', fontSize: 36, width: 640, height: 84, worldH: 0.26, bg: true,
  });
  label.position.y = -0.72;
  group.add(label);

  group.userData = { mesh, bar, base: 1, pulse: 0, state, tremble: 0 };
  entityGroup.add(group);
  return group;
}

function makeConflictX(ent) {
  if (world.conflictX) return world.conflictX;
  const pos = slotPos(ent.id);
  const group = new THREE.Group();
  group.position.copy(pos);
  const barGeo = new THREE.BoxGeometry(0.07, 0.07, 0.55);
  const mat = new THREE.MeshBasicMaterial({ color: 0xEF5350 });
  const b1 = new THREE.Mesh(barGeo, mat);
  b1.rotation.z = Math.PI / 4;
  const b2 = new THREE.Mesh(barGeo, mat);
  b2.rotation.z = -Math.PI / 4;
  group.add(b1, b2);
  group.add(anchorLine(pos, '#EF5350'));
  const label = textSprite(ent.id, { color: '#EF5350', fontSize: 34, width: 220, height: 70, worldH: 0.2 });
  label.position.y = -0.5;
  group.add(label);
  group.userData = { flash: 0 };
  group.visible = false;
  entityGroup.add(group);
  world.conflictX = group;
  return group;
}

function makeDecision(ent) {
  const pos = slotPos(ent.id);
  const group = new THREE.Group();
  group.position.copy(pos);
  const accept = (ent.verdict || '').toUpperCase().includes('ACCEPT');
  const color = accept ? '#66BB6A' : '#EF5350';
  const mesh = new THREE.Mesh(
    new THREE.BoxGeometry(0.42, 0.42, 0.42),
    new THREE.MeshStandardMaterial({
      color: new THREE.Color(color),
      emissive: new THREE.Color(color),
      emissiveIntensity: 0.5,
      roughness: 0.3,
    })
  );
  group.add(mesh);
  group.add(anchorLine(pos, color));
  const label = textSprite(`${ent.id} — ${ent.verdict || ''} ${(ent.confidence ?? 0).toFixed(2)}`, {
    color, fontSize: 34, width: 560, height: 80, worldH: 0.24,
  });
  label.position.y = -0.55;
  group.add(label);
  group.userData = { mesh, pulse: 0 };
  entityGroup.add(group);
  return group;
}

function applyEntityPayload(ent) {
  const existing = world.entities.get(ent.id);
  if (existing) {
    if (ent.state) setHypothesisState(ent.id, ent.state);
    if (ent.confidence != null && existing.type === 'Hypothesis') setConfidence(ent.id, ent.confidence);
    return;
  }

  const model = { id: ent.id, type: ent.type, state: ent.state, confidence: ent.confidence };
  switch (ent.type) {
    case 'Evidence':
      model.group = makeEvidence(ent);
      break;
    case 'Hypothesis':
      model.group = makeHypothesis(ent);
      break;
    case 'Conflict':
      model.group = makeConflictX(ent);
      break;
    case 'Decision':
      model.group = makeDecision(ent);
      break;
    default:
      model.group = null;
  }
  world.entities.set(ent.id, model);
}

function setHypothesisState(id, state) {
  const model = world.entities.get(id);
  if (!model) return;
  const prev = model.state;
  model.state = state;
  const color = STATE_COLORS[state] || '#9E9E9E';
  const mesh = model.group.userData.mesh;
  mesh.material.color.set(color);
  mesh.material.emissive.set(color);
  model.group.userData.state = state;
  addTrailChip(id, state, model.confidence, prev !== state);
}

function setConfidence(id, conf) {
  const model = world.entities.get(id);
  if (!model) return;
  model.confidence = conf;
  const bar = model.group.userData.bar;
  if (bar) bar.userData.setConf(conf);
  const chips = document.querySelectorAll('#trailChips .chip');
  if (chips.length) {
    const last = chips[chips.length - 1];
    const confSpan = last.querySelector('.conf');
    if (confSpan) confSpan.textContent = conf.toFixed(2);
  }
}

// ------------------------------------------------------------------ edges

function addSupportEdge(evidenceId, hypothesisId) {
  const key = `${evidenceId}->${hypothesisId}`;
  if (world.supportEdges.has(key)) return;
  const from = slotPos(evidenceId);
  const to = slotPos(hypothesisId);
  const mid = from.clone().add(to).multiplyScalar(0.5);
  mid.y += 0.5;
  const curve = new THREE.QuadraticBezierCurve3(from, mid, to);
  const line = new THREE.Line(
    new THREE.BufferGeometry().setFromPoints(curve.getPoints(28)),
    new THREE.LineBasicMaterial({ color: 0x66BB6A, transparent: true, opacity: 0.75 })
  );
  edgeGroup.add(line);
  world.supportEdges.set(key, line);
  addPulse(to, 0x66BB6A, 0.35);
}

function addContradictEdge(evidenceId, hypothesisId) {
  const key = `${evidenceId}->${hypothesisId}`;
  if (world.contradictEdges.has(key)) return;
  const from = slotPos(evidenceId);
  const to = slotPos(hypothesisId);
  const line = new THREE.Line(
    new THREE.BufferGeometry(),
    new THREE.LineBasicMaterial({ color: 0xEF5350, transparent: true, opacity: 0.95 })
  );
  line.userData = { from, to, seed: Math.random() * 10 };
  edgeGroup.add(line);
  world.contradictEdges.set(key, { line, from, to });
  if (world.conflictX) world.conflictX.visible = true;
  addPulse(to, 0xEF5350, 0.5);
}

function updateContradictEdges(t, active) {
  for (const entry of world.contradictEdges.values()) {
    const { line, from, to } = entry;
    const pts = [];
    const n = 7;
    for (let i = 0; i <= n; i++) {
      const k = i / n;
      const p = new THREE.Vector3().lerpVectors(from, to, k);
      if (active && i > 0 && i < n) {
        const jitter = 0.09 + 0.05 * Math.sin(t * 18 + entry.line.userData.seed + i * 1.7);
        p.x += jitter;
        p.y += Math.sin(t * 21 + i * 2.3 + entry.line.userData.seed) * 0.06;
      }
      pts.push(p);
    }
    line.geometry.dispose();
    line.geometry = new THREE.BufferGeometry().setFromPoints(pts);
  }
}

// ------------------------------------------------------------------ flights & effects

function spawnFlight(actor, entityId) {
  const agent = agentByName(actor);
  const from = agent ? agent.pos.clone() : new THREE.Vector3(0, 4, 0);
  const to = slotPos(entityId);
  const color = agent ? agent.color : '#ffffff';
  const mesh = new THREE.Mesh(
    new THREE.SphereGeometry(0.07, 12, 12),
    new THREE.MeshBasicMaterial({ color: new THREE.Color(color), transparent: true, opacity: 0.95 })
  );
  effectGroup.add(mesh);
  const dur = 0.75;
  world.flights.push({
    mesh, from, to, t: 0, dur,
    onArrive: () => {
      addPulse(to, color, 0.55);
      const model = world.entities.get(entityId);
      if (model && model.group) model.group.userData.pulse = 1;
    },
  });
}

function addPulse(pos, color, size = 0.4) {
  const ring = new THREE.Mesh(
    new THREE.RingGeometry(0.35, 0.42, 32),
    new THREE.MeshBasicMaterial({
      color: new THREE.Color(color),
      transparent: true,
      opacity: 0.9,
      side: THREE.DoubleSide,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    })
  );
  ring.position.copy(pos);
  ring.lookAt(camera.position);
  effectGroup.add(ring);
  world.effects.push({ mesh: ring, t: 0, dur: 0.7, kind: 'ring', size, color });
}

function addBurst(pos, color) {
  const p = new THREE.Points(
    new THREE.BufferGeometry(),
    new THREE.PointsMaterial({
      color: new THREE.Color(color),
      size: 0.07,
      transparent: true,
      opacity: 1,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    })
  );
  const n = 14;
  const positions = new Float32Array(n * 3);
  const dirs = [];
  for (let i = 0; i < n; i++) {
    const dir = new THREE.Vector3(
      Math.random() - 0.5,
      Math.random() - 0.5,
      Math.random() - 0.5
    ).normalize();
    dirs.push(dir);
  }
  p.geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  p.position.copy(pos);
  effectGroup.add(p);
  world.effects.push({ mesh: p, t: 0, dur: 0.6, kind: 'burst', dirs, positions });
}

function updateEffects(dt) {
  world.flights = world.flights.filter((f) => {
    f.t += dt / f.dur;
    if (f.t >= 1) {
      effectGroup.remove(f.mesh);
      f.mesh.geometry.dispose();
      f.mesh.material.dispose();
      if (f.onArrive) f.onArrive();
      return false;
    }
    const k = f.t;
    f.mesh.position.lerpVectors(f.from, f.to, k);
    f.mesh.position.y += Math.sin(k * Math.PI) * 1.1;
    return true;
  });

  world.effects = world.effects.filter((e) => {
    e.t += dt / e.dur;
    if (e.t >= 1) {
      effectGroup.remove(e.mesh);
      if (e.mesh.geometry) e.mesh.geometry.dispose();
      if (e.mesh.material) e.mesh.material.dispose();
      return false;
    }
    const k = e.t;
    if (e.kind === 'ring') {
      const s = e.size * (0.6 + k * 1.6);
      e.mesh.scale.set(s, s, 1);
      e.mesh.material.opacity = 0.9 * (1 - k);
      e.mesh.lookAt(camera.position);
    } else if (e.kind === 'burst') {
      e.mesh.material.opacity = 1 - k;
      for (let i = 0; i < e.dirs.length; i++) {
        e.positions[i * 3] = e.dirs[i].x * k * 0.8;
        e.positions[i * 3 + 1] = e.dirs[i].y * k * 0.8;
        e.positions[i * 3 + 2] = e.dirs[i].z * k * 0.8;
      }
      e.mesh.geometry.attributes.position.needsUpdate = true;
    }
    return true;
  });
}

// ------------------------------------------------------------------ trail chips

function addTrailChip(id, state, conf, isNew) {
  if (!isNew) return;
  const wrapEl = document.getElementById('trailChips');
  const color = STATE_COLORS[state] || '#9E9E9E';
  for (const chip of wrapEl.querySelectorAll('.chip.active')) chip.classList.remove('active');
  const chip = document.createElement('span');
  chip.className = 'chip active';
  chip.style.background = color;
  chip.textContent = state;
  const confSpan = document.createElement('span');
  confSpan.className = 'conf';
  if (conf != null) confSpan.textContent = conf.toFixed(2);
  chip.appendChild(confSpan);
  wrapEl.appendChild(chip);
  if (wrapEl.children.length > 40) wrapEl.removeChild(wrapEl.firstChild);
}

// ------------------------------------------------------------------ log

function esc(s) {
  return String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function logLine(html, cls) {
  const box = document.getElementById('logLines');
  const div = document.createElement('div');
  div.className = `log-line ${cls || ''}`;
  div.innerHTML = html;
  box.appendChild(div);
  while (box.children.length > 220) box.removeChild(box.firstChild);
  box.scrollTop = box.scrollHeight;
}

function logStep(step) {
  const target = step.target ? ` ${esc(step.target)}` : '';
  const desc = step.description ? ` <span style="color:#7d8aa0">— ${esc(step.description)}</span>` : '';
  const err = !step.ok && step.errorCode ? ` <span class="err">REJECTED ${esc(step.errorCode)}</span>` : '';
  logLine(
    `<span class="tick">[t${step.tick}]</span><span class="agent">${esc(step.agent)}</span> → ${esc(step.kind)}${target}${desc}${err}`,
    step.ok ? 'ok' : 'bad'
  );
}

function logEvent(ev) {
  const id = ev.entityId ? ` ${esc(ev.entityId)}` : '';
  const detail = ev.detail ? ` — ${esc(ev.detail)}` : '';
  logLine(
    `<span class="tick">#${ev.seq}</span><span style="color:#5d6b82">${esc(ev.type)}${id}${detail}</span>`
  );
}

// ------------------------------------------------------------------ messages

function applyEvent(ev) {
  switch (ev.type) {
    case 'EntityCreated':
      spawnFlight(ev.actor, ev.entityId);
      break;
    case 'StateChanged': {
      const model = world.entities.get(ev.entityId);
      if (!model) break;
      for (const change of ev.delta.changes || []) {
        if (change.field === 'state') {
          setHypothesisState(ev.entityId, change.to);
        } else if (change.field === 'confidence') {
          setConfidence(ev.entityId, parseFloat(change.to));
        } else if (change.field === 'supporting_evidence') {
          const ids = (change.to || '').split(',').filter(Boolean);
          for (const evId of ids) addSupportEdge(evId, ev.entityId);
        } else if (change.field === 'contradicting_evidence') {
          const ids = (change.to || '').split(',').filter(Boolean);
          for (const evId of ids) addContradictEdge(evId, ev.entityId);
        }
      }
      break;
    }
    case 'ConflictDetected':
      if (world.conflictX) {
        world.conflictX.visible = true;
        world.conflictX.userData.flash = 1;
        addPulse(slotPos(ev.entityId || 'CF001'), 0xEF5350, 0.6);
      }
      break;
    case 'FalsificationStarted':
      pulseAgent(ev.actor);
      addBurst(slotPos(ev.entityId || 'H001'), 0xAB47BC);
      break;
    case 'DecisionProposed':
      pulseAgent(ev.actor);
      break;
    case 'DecisionCommitted': {
      const model = world.entities.get(ev.entityId);
      if (model && model.group) model.group.userData.pulse = 1;
      addBurst(slotPos(ev.entityId || 'DC001'), 0xFFD54F);
      break;
    }
    default:
      break;
  }
}

function processTick(msg) {
  document.getElementById('tick').textContent = `tick ${msg.tick}`;
  for (const ent of msg.entities || []) applyEntityPayload(ent);
  for (const ev of msg.events || []) {
    if (ev.type !== 'ProposalRejected') logEvent(ev);
    applyEvent(ev);
  }
  for (const step of msg.steps || []) {
    logStep(step);
    pulseAgent(step.agent);
  }
}

// ------------------------------------------------------------------ websocket

const $ = (id) => document.getElementById(id);
let ws = null;
let reconnectTimer = null;
let running = false;

function setConn(on) {
  const el = $('conn');
  el.className = `conn ${on ? 'on' : 'off'}`;
  el.textContent = on ? 'connected' : 'disconnected — reconnecting…';
}

function send(obj) {
  if (ws && ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify(obj));
}

function stopRun() {
  send({ type: 'speed', ms: parseInt($('speed').value, 10) });
  running = false;
  $('btnRun').textContent = 'RUN';
}

function connect() {
  if (ws) { ws.onclose = null; ws.close(); }
  setConn(false);
  ws = new WebSocket(`ws://${location.host}/ws`);

  ws.onopen = () => setConn(true);

  ws.onmessage = (evt) => {
    let msg;
    try { msg = JSON.parse(evt.data); } catch { return; }
    switch (msg.type) {
      case 'hello':
        resetWorld();
        $('status').textContent = msg.scenario;
        $('speed').value = msg.speed;
        running = false;
        $('btnRun').textContent = 'RUN';
        break;
      case 'tick':
        processTick(msg);
        break;
      case 'done':
        running = false;
        $('btnRun').textContent = 'RUN';
        $('status').textContent = `DONE — ${msg.ticks} ticks`;
        break;
      default:
        break;
    }
  };

  ws.onclose = () => {
    setConn(false);
    if (!reconnectTimer) reconnectTimer = setTimeout(() => { reconnectTimer = null; connect(); }, 1500);
  };
}

// ------------------------------------------------------------------ ui wiring

$('btnRun').addEventListener('click', () => {
  if (running) { stopRun(); return; }
  send({ type: 'run' });
  running = true;
  $('btnRun').textContent = 'STOP';
  $('status').textContent = 'running…';
});

$('btnStep').addEventListener('click', () => {
  stopRun();
  send({ type: 'step' });
});

$('btnReset').addEventListener('click', () => {
  stopRun();
  resetWorld();
  $('status').textContent = 'resetting…';
  send({ type: 'reset' });
});

$('speed').addEventListener('change', (e) => {
  send({ type: 'speed', ms: parseInt(e.target.value, 10) });
});

$('scenario').addEventListener('change', (e) => {
  stopRun();
  send({ type: 'scenario', name: e.target.value });
});

function onResize() {
  const w = window.innerWidth;
  const h = window.innerHeight;
  if (w === 0 || h === 0) return;
  camera.aspect = w / h;
  camera.updateProjectionMatrix();
  renderer.setSize(w, h);
}

window.addEventListener('resize', onResize);
document.addEventListener('visibilitychange', () => {
  if (!document.hidden) onResize();
});

// ------------------------------------------------------------------ animate

const clock = new THREE.Clock();

function animate() {
  requestAnimationFrame(animate);
  const dt = Math.min(clock.getDelta(), 0.1);
  const t = clock.elapsedTime;

  controls.update();
  updateEffects(dt);

  for (const agent of AGENTS) {
    const g = agentMeshes.get(agent.name);
    if (!g) continue;
    g.userData.pulse = Math.max(0, g.userData.pulse - dt * 2.2);
    const p = g.userData.pulse;
    const s = 1 + p * 0.45 * Math.sin(p * Math.PI * 3);
    g.scale.setScalar(s);
    g.userData.ring.rotation.z += dt * 0.8;
  }

  let conflictActive = false;
  for (const model of world.entities.values()) {
    if (model.type !== 'Hypothesis' || !model.group) continue;
    const ud = model.group.userData;
    const st = ud.state || model.state;
    if (st === 'Conflicted' || st === 'Falsifying') {
      conflictActive = true;
      const j = 0.045;
      model.group.position.set(
        slotPos(model.id).x + (Math.random() - 0.5) * j,
        slotPos(model.id).y + (Math.random() - 0.5) * j,
        slotPos(model.id).z + (Math.random() - 0.5) * j
      );
    } else {
      model.group.position.copy(slotPos(model.id));
    }
    ud.pulse = Math.max(0, ud.pulse - dt * 1.8);
    const base = st === 'Investigating' ? 1 + 0.05 * Math.sin(t * 4.2) : 1;
    ud.mesh.scale.setScalar(base * (1 + ud.pulse * 0.5));
  }

  updateContradictEdges(t, conflictActive);

  if (world.conflictX) {
    const flash = world.conflictX.userData.flash;
    if (flash > 0) {
      world.conflictX.userData.flash = Math.max(0, flash - dt * 2.5);
      world.conflictX.scale.setScalar(1 + flash * 0.6);
    } else if (world.conflictX.scale.x !== 1) {
      world.conflictX.scale.setScalar(1);
    }
  }

  for (const model of world.entities.values()) {
    if (model.type !== 'Decision' || !model.group) continue;
    const ud = model.group.userData;
    ud.pulse = Math.max(0, ud.pulse - dt * 2);
    ud.mesh.scale.setScalar(1 + ud.pulse * 0.35);
    ud.mesh.rotation.y += dt * (0.6 + ud.pulse * 3);
  }

  for (const model of world.entities.values()) {
    if (model.type !== 'Evidence' || !model.group) continue;
    const ud = model.group.userData;
    ud.pulse = Math.max(0, ud.pulse - dt * 1.8);
    ud.mesh.scale.setScalar(1 + ud.pulse * 0.6);
    ud.mesh.rotation.y += dt * 0.5;
  }

  renderer.render(scene, camera);
}

// ------------------------------------------------------------------ boot

buildAgents();
connect();
animate();
