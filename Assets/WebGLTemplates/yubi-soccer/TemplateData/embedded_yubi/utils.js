// utils.js - シグナル処理ユーティリティ

export class RingBuffer {
  constructor(capacity) {
    this.capacity = capacity;
    this.arr = new Array(capacity);
    this.start = 0;
    this.length = 0;
  }
  push(v) {
    const idx = (this.start + this.length) % this.capacity;
    this.arr[idx] = v;
    if (this.length < this.capacity) this.length++;
    else this.start = (this.start + 1) % this.capacity;
  }
  toArray() {
    const out = [];
    for (let i = 0; i < this.length; i++) out.push(this.arr[(this.start + i) % this.capacity]);
    return out;
  }
  clear() { this.start = 0; this.length = 0; }
}

export function clamp(x, a, b) { return Math.max(a, Math.min(b, x)); }
export function lerp(a, b, t) { return a + (b - a) * t; }

export function MovingAvg(n = 5) {
  const buf = [];
  return {
    push(x) { buf.push(x); if (buf.length > n) buf.shift(); },
    value() { if (buf.length === 0) return 0; return buf.reduce((s, v) => s + v, 0) / buf.length; },
    clear() { buf.length = 0; },
  };
}

// 時間配列 t と値配列 v から一階差分（速度）を計算（単位: v 単位/秒）
export function diffSeries(t, v) {
  const out = [];
  for (let i = 1; i < t.length; i++) {
    const dt = t[i] - t[i - 1];
    const dv = v[i] - v[i - 1];
    out.push(dt > 1e-6 ? dv / dt : 0);
  }
  // 長さを合わせるため、先頭に同じ値を複製
  if (out.length > 0) out.unshift(out[0]); else out.push(0);
  return out;
}

export function rms(arr) {
  if (!arr || arr.length === 0) return 0;
  let s = 0;
  for (const x of arr) s += x * x;
  return Math.sqrt(s / arr.length);
}

// 正規化相互相関（-1..1）。長さが異なる場合は短い方に合わせる。
export function normalizedCrossCorrelation(a, b) {
  const n = Math.min(a.length, b.length);
  if (n === 0) return 0;
  let ma = 0, mb = 0;
  for (let i = 0; i < n; i++) { ma += a[i]; mb += b[i]; }
  ma /= n; mb /= n;
  let num = 0, da = 0, db = 0;
  for (let i = 0; i < n; i++) {
    const xa = a[i] - ma;
    const xb = b[i] - mb;
    num += xa * xb;
    da += xa * xa;
    db += xb * xb;
  }
  const den = Math.sqrt(da * db);
  if (den < 1e-12) return 0;
  const r = num / den;
  if (!isFinite(r)) return 0;
  return Math.max(-1, Math.min(1, r));
}

export function angleBetween(ax, ay, bx, by) {
  const dot = ax * bx + ay * by;
  const na = Math.hypot(ax, ay);
  const nb = Math.hypot(bx, by);
  const den = na * nb;
  if (den < 1e-9) return 0;
  const c = clamp(dot / den, -1, 1);
  return Math.acos(c);
}

// 速度系列 v と時刻 t から、0 交差（符号反転）時刻を検出
// ampThresh > 0 の場合、|v| がしきい値以上の区間での交差のみ採用（ノイズ抑制）
export function zeroCrossingTimes(t, v, ampThresh = 0) {
  const out = [];
  if (!t || !v || t.length !== v.length || t.length < 2) return out;
  for (let i = 1; i < v.length; i++) {
    const v1 = v[i - 1], v2 = v[i];
    if (Math.sign(v1) === 0 || Math.sign(v2) === 0) continue;
    if (Math.sign(v1) === Math.sign(v2)) continue;
    if (ampThresh > 0 && (Math.abs(v1) < ampThresh || Math.abs(v2) < ampThresh)) continue;
    const t1 = t[i - 1], t2 = t[i];
    const dv = v2 - v1;
    if (Math.abs(dv) < 1e-9) continue;
    // 線形補間で交差時刻を推定
    const alpha = (0 - v1) / dv;
    const tc = t1 + alpha * (t2 - t1);
    if (isFinite(tc)) out.push(tc);
  }
  return out;
}
