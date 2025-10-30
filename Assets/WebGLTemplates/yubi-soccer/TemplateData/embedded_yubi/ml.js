// ml.js - Kick 判定用の軽量 ML（ロジスティック回帰）と特徴量抽出

import { diffSeries, rms, clamp } from './utils.js';

// windowData: [{t, lm:[{x,y,z}*21]}, ...]（0..1 正規化座標, 鏡適用済み）
export function computeKickFeatures(windowData) {
  const n = windowData.length;
  if (!n || n < 4) return null;
  const t = windowData.map(e => e.t);
  const lm = windowData.map(e => e.lm);

  // 便利参照
  const idx = { wrist: 0, thumb4: 4, idx5: 5, idx8: 8, mid9: 9, mid12: 12, ring16: 16, pinky20: 20 };

  // 手のスケール（距離の正規化用）: wrist(0)→middle_mcp(9)
  const scaleArr = lm.map(f => Math.hypot(f[idx.mid9].x - f[idx.wrist].x, f[idx.mid9].y - f[idx.wrist].y));
  const handScale = Math.max(1e-4, scaleArr.reduce((s,v)=>s+v,0)/scaleArr.length);

  // 指先群
  const tipsI = [idx.idx8, idx.mid12, idx.ring16, idx.pinky20];

  // y: 指先が手首より下（画面座標で y が大きい）
  const ydiffMeans = tipsI.map(k => {
    const arr = lm.map(f => f[k].y - f[idx.wrist].y);
    return arr.reduce((s,v)=>s+v,0)/arr.length;
  });

  // 速度（正規化 -> 手スケールで割る → px依存を低減）
  function speedNorm(series) {
    const vx = diffSeries(t, series.map(p=>p.x));
    const vy = diffSeries(t, series.map(p=>p.y));
    return vx.map((v,i)=> Math.hypot(v, vy[i]) / handScale);
  }

  const tipSpeeds = tipsI.map(k => speedNorm(lm.map(f => f[k])));
  const tipSpeedRms = tipSpeeds.map(s => rms(s));
  const tipSpeedMax = tipSpeeds.map(s => Math.max(...s));

  // 手首速度
  const wristSpeed = speedNorm(lm.map(f => f[idx.wrist]));
  const wristRms = rms(wristSpeed);
  const wristMax = Math.max(...wristSpeed);

  // 親指(4)-人差(8)の角度速度
  const ang = lm.map(f => Math.atan2(f[idx.idx8].y - f[idx.thumb4].y, f[idx.idx8].x - f[idx.thumb4].x));
  const angVel = diffSeries(t, ang).map(v => Math.abs(v));
  const angVelMax = Math.max(...angVel);

  // 窓長（秒）
  const duration = t[n-1] - t[0];

  // 手の向き（wrist->mid9）
  const ori = lm.map(f => Math.atan2(f[idx.mid9].y - f[idx.wrist].y, f[idx.mid9].x - f[idx.wrist].x));
  const oriMean = ori.reduce((s,v)=>s+v,0)/ori.length;

  // 特徴量ベクトル
  const feat = [
    ...ydiffMeans,           // 4
    ...tipSpeedRms,          // 4
    ...tipSpeedMax,          // 4
    wristRms, wristMax,      // 2
    angVelMax,               // 1
    duration,                // 1
    Math.cos(oriMean), Math.sin(oriMean) // 2（向きの循環表現）
  ];
  return Float32Array.from(feat);
}

export class LogisticKickModel {
  constructor() {
    this.w = null; // Float32Array
    this.b = 0;
    this.mean = null; // Float32Array
    this.std = null;  // Float32Array
  }

  toJSON() { return { w: Array.from(this.w||[]), b: this.b, mean: Array.from(this.mean||[]), std: Array.from(this.std||[]) }; }
  static fromJSON(obj) { const m = new LogisticKickModel(); m.w = Float32Array.from(obj.w||[]); m.b = obj.b||0; m.mean = Float32Array.from(obj.mean||new Array(m.w.length).fill(0)); m.std = Float32Array.from(obj.std||new Array(m.w.length).fill(1)); return m; }

  saveToLocalStorage(key='kickModelV1') { localStorage.setItem(key, JSON.stringify(this.toJSON())); }
  static loadFromLocalStorage(key='kickModelV1') { try { const s = localStorage.getItem(key); if (!s) return null; return LogisticKickModel.fromJSON(JSON.parse(s)); } catch { return null; } }

  static async loadFromUrl(url) {
    try { const res = await fetch(url); if (!res.ok) return null; const obj = await res.json(); return LogisticKickModel.fromJSON(obj); } catch { return null; }
  }

  predictProba(xArr) {
    // xArr: Float32Array（未標準化）
    if (!this.w || !this.mean || !this.std) return 0;
    const d = this.w.length;
    let z = this.b;
    for (let i=0;i<d;i++) {
      const xi = (xArr[i] - this.mean[i]) / (this.std[i] || 1);
      z += this.w[i] * xi;
    }
    const p = 1 / (1 + Math.exp(-z));
    return clamp(p, 0, 1);
  }

  fit(X, y, { epochs=120, lr=0.1, l2=1e-4 }={}) {
    // X: Array<Float32Array>, y: Array<number 0/1>
    if (!X || X.length === 0) throw new Error('empty dataset');
    const n = X.length; const d = X[0].length;
    // 標準化統計
    const mean = new Float32Array(d); const std = new Float32Array(d);
    for (let j=0;j<d;j++) {
      let s=0; for (let i=0;i<n;i++) s += X[i][j]; mean[j] = s/n;
      let v=0; for (let i=0;i<n;i++) { const a = X[i][j]-mean[j]; v += a*a; }
      std[j] = Math.sqrt(v/n) || 1;
    }
    this.mean = mean; this.std = std;
    // パラメタ初期化
    this.w = new Float32Array(d); this.b = 0;

    // 学習
    for (let ep=0; ep<epochs; ep++) {
      let gw = new Float32Array(d); let gb = 0;
      for (let i=0;i<n;i++) {
        // 標準化
        const xs = new Float32Array(d);
        for (let j=0;j<d;j++) xs[j] = (X[i][j]-mean[j])/(std[j]||1);
        // 予測
        let z = this.b; for (let j=0;j<d;j++) z += this.w[j]*xs[j];
        const p = 1/(1+Math.exp(-z));
        const err = p - y[i];
        for (let j=0;j<d;j++) gw[j] += err*xs[j];
        gb += err;
      }
      // L2 正則化
      for (let j=0;j<d;j++) gw[j] = gw[j]/n + l2*this.w[j];
      gb = gb/n;
      for (let j=0;j<d;j++) this.w[j] -= lr*gw[j];
      this.b -= lr*gb;
    }

    // 訓練精度（参考）
    let correct=0; for (let i=0;i<n;i++){ const p=this.predictProba(X[i]); const yhat=p>=0.5?1:0; if (yhat===y[i]) correct++; }
    return { acc: correct/n };
  }
}

// ゲート条件: 「手の甲（手首付近）よりも指先が下で、指先速度が十分大きい」
export function movementGateForKick(windowData, { minTipBelow=0.02, minTipSpeed=1.8 }={}) {
  const n = windowData.length; if (n<4) return false;
  const t = windowData.map(e=>e.t); const lm = windowData.map(e=>e.lm);
  const wrist = lm.map(f=>f[0]);
  const tips = [8,12,16,20].map(k => lm.map(f=>f[k]));
  const scaleArr = lm.map(f=> Math.hypot(f[9].x - f[0].x, f[9].y - f[0].y));
  const scale = Math.max(1e-4, scaleArr.reduce((s,v)=>s+v,0)/scaleArr.length);

  // 指が下
  let belowCnt=0, total=n*tips.length;
  for (const tip of tips) for (let i=0;i<n;i++) if (tip[i].y - wrist[i].y > minTipBelow) belowCnt++;
  const belowRatio = belowCnt/total;

  // 指先速度（手スケール正規化）の最大
  let maxNormSpeed=0;
  for (const tip of tips){
    const vx = diffSeries(t, tip.map(p=>p.x));
    const vy = diffSeries(t, tip.map(p=>p.y));
    for (let i=0;i<vx.length;i++) maxNormSpeed = Math.max(maxNormSpeed, Math.hypot(vx[i],vy[i])/scale);
  }
  return belowRatio>0.5 && maxNormSpeed>=minTipSpeed;
}
