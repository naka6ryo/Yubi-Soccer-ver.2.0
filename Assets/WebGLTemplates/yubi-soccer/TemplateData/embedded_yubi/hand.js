// hand.js - MediaPipe HandLandmarker + ジェスチャ判定
import { RingBuffer, MovingAvg, normalizedCrossCorrelation, angleBetween, diffSeries, rms, clamp, lerp, zeroCrossingTimes } from './utils.js';

// MediaPipe tasks-vision の読み込みは動的 import でローカル/CDN をフォールバック
// HTTPS or localhost が必要。iOS Safari はユーザ操作後にカメラ可。

const CFG = {
  inputTargetSize: 320, // 処理落ち時は 256 に下げる
  minFPSForHighRes: 26,
  windowSec: 0.6, // RUN 相関窓
  debounceSec: 0.3,
  hysteresis: { on: 0.65, off: 0.45 },
  run: {
    minAbsCorr: 0.5,
    minSpeedAmp: 200, // px/s 相当（指振りの速度閾値）
    // 代替: 手首の上下速度のゼロ交差から走動作（周期運動）を検出
    freqBandHz: [1.6, 4.0], // 許容する歩幅/走行の周波数帯（1/s）
    zeroXMinAmp: 80,       // px/s ゼロ交差判定に用いる最小速度（ノイズ抑制）
    minTipSpeedPxPerSec: 400, // 甲から離れた領域での指先速度の下限（RUN 用）
  },
  kick: {
    minAngVel: 10.0, // rad/s
    minWristSpeed: 500.0, // px/s （10 px/frame @30fps 相当）
    // KICK は指先速度ピークのみで判定
    minTipSpeedPxPerSec: 3000, // 指先速度による KICK しきい値
  },
  joystick: {
    // グーの手をジョイスティック化（左右）
    deadzonePalmRatio: 0.5,  // デッドゾーン = palmSize * ratio
    maxRangePalmRatio: 2.0,  // フルレンジ = palmSize * ratio（これ以上は±1にクランプ）
    smoothAlpha: 0.25,       // 値のローパス係数（0..1）
    resetDelaySec: 0.5,      // こぶし未検出になってから原点をリセットする遅延
  },
  fist: {
    // グー判定: 指先(4,8,12,16,20)が掌中心に近い（palmSize 比）
    // 緩め設定: 指先が掌中心からやや離れていてもグーとみなす
    maxTipPalmRatio: 1.6, // 平均距離/掌サイズ がこの値以下ならグー寄り
    minTipsClose: 3,      // 近いとみなす指の最小本数
  },
};

async function loadTasksVision() {
  const candidates = [
    './vendor/mediapipe/vision_bundle.mjs',
    'https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.11/vision_bundle.mjs',
    'https://unpkg.com/@mediapipe/tasks-vision@0.10.11/vision_bundle.mjs',
  ];
  let lastErr;
  for (const url of candidates) {
    try {
      const mod = await import(url);
      if (mod?.FilesetResolver && mod?.HandLandmarker) return mod;
    } catch (e) {
      lastErr = e;
    }
  }
  throw lastErr || new Error('Failed to load @mediapipe/tasks-vision');
}

export class HandTracker {
  constructor({ video, overlay, mirror = false, onResult }) {
    this.video = video;
    this.overlay = overlay;
  this.ctx = overlay.getContext('2d');
    this.mirror = mirror;
    this.onResult = onResult;

    this.running = false;
    this.lastTs = performance.now();
    this.fps = 0;

    // 時系列バッファ
    this.landmarksBuf = new RingBuffer(90); // 約3秒分@30fps
    this.state = 'NONE';
    this.stateConf = 0;
  this.lastTriggerTime = 0;
  this.lastSeenTime = 0; // 最後に手を検出した時刻（sec）
  this.noHandCount = 0;  // 連続で検出できなかったフレーム数

    // 推論入力用のオフスクリーン Canvas
    this.procCanvas = document.createElement('canvas');
    this.procCtx = this.procCanvas.getContext('2d', { willReadFrequently: true });

    // ジョイスティック（グーの手）用状態
    this.fistOrigin = null;     // {x,y} ピクセル座標
    this.joystickX = 0;         // -1..1 左右
    this.joystickActive = false;
  this.lastFistSeenTs = 0;    // 最後にグーを検出した時刻（sec）
  }

  async init() {
    const { FilesetResolver, HandLandmarker } = await loadTasksVision();
    // ベース URL は末尾スラッシュを保証
    const ensureSlash = (s) => s.endsWith('/') ? s : (s + '/');
    const bases = [
      ensureSlash('./vendor/mediapipe/wasm'),
      ensureSlash('https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.11/wasm'),
      ensureSlash('https://unpkg.com/@mediapipe/tasks-vision@0.10.11/wasm'),
    ];
    let lastErr = null;
    for (const base of bases) {
      try {
        const filesetResolver = await FilesetResolver.forVisionTasks(base);
        // モデルはローカル優先で候補を試す
        const modelCandidates = [
          `${bases[0]}hand_landmarker.task`,
          `${base}hand_landmarker.task`,
          // 公式 GCS の安定ミラー
          'https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task',
        ];
        let modelPath = null;
        for (const m of modelCandidates) {
          try {
            // fetch HEAD で存在確認（CORS 許容のため GET ではなく HEAD を試行）。失敗時は次へ。
            const res = await fetch(m, { method: 'HEAD' });
            if (res.ok) { modelPath = m; break; }
          } catch (_) { /* try next */ }
        }
        if (!modelPath) throw new Error('No accessible hand_landmarker.task');
        this.handLandmarker = await HandLandmarker.createFromOptions(filesetResolver, {
          baseOptions: { modelAssetPath: modelPath },
          numHands: 2,
          runningMode: 'VIDEO',
          minHandDetectionConfidence: 0.3,
          minHandPresenceConfidence: 0.3,
          minTrackingConfidence: 0.5,
        });
        console.info('[HandLandmarker] initialized with base:', base, 'model:', modelPath);
        return; // 成功
      } catch (e) {
        lastErr = e;
      }
    }
    throw lastErr || new Error('HandLandmarker の初期化に失敗しました');
  }

  start() {
    if (this.running) return;
    this.running = true;
    this.processLoop();
  }

  stop() {
    this.running = false;
  }

  setMirror(m) {
    this.mirror = m;
  }

  getInputSize() {
    // 動的スケーリング: FPS が落ちたら入力を縮小
    return this.fps >= CFG.minFPSForHighRes ? 320 : 256;
  }

  async processLoop() {
    if (!this.running) return;
    const now = performance.now();
    const dt = (now - this.lastTs) / 1000;
    this.lastTs = now;
    this.fps = lerp(this.fps || 30, 1 / Math.max(dt, 1e-3), 0.1);

  const video = this.video;
  const size = this.getInputSize();
    let lmResult = null;
    if (video.videoWidth > 0 && video.videoHeight > 0) {
      // オフスクリーン Canvas にダウンサンプリングして渡す
      const vw = video.videoWidth;
      const vh = video.videoHeight;
      const aspect = vw / vh;
      let pw, ph;
      if (aspect >= 1) { // 横長
        pw = size; ph = Math.round(size / aspect);
      } else { // 縦長
        ph = size; pw = Math.round(size * aspect);
      }
      this.procCanvas.width = pw;
      this.procCanvas.height = ph;
      this.procCtx.drawImage(video, 0, 0, pw, ph);
      try {
        lmResult = await this.handLandmarker.detectForVideo(this.procCanvas, now);
      } catch (_) { lmResult = null; }
    }

    const canvas = this.overlay;
    const ctx = this.ctx;
    // DPR 対応: 実描画サイズを CSS ピクセルに一致させる
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    const cssW = canvas.clientWidth || window.innerWidth;
    const cssH = canvas.clientHeight || window.innerHeight;
    if (canvas.width !== Math.floor(cssW * dpr) || canvas.height !== Math.floor(cssH * dpr)) {
      canvas.width = Math.floor(cssW * dpr);
      canvas.height = Math.floor(cssH * dpr);
    }
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0); // 以降は CSS ピクセル系で描く
    ctx.clearRect(0, 0, cssW, cssH);

    let normalizedLandmarks = null;
  if (lmResult && lmResult.landmarks && lmResult.landmarks[0]) {
      // 0..1 正規化座標（鏡反転のみ適用、ピクセル変換は描画・分類時に行う）
      const hands = lmResult.landmarks.map(lm => this.normalizeLandmarks01(lm, this.mirror));
      normalizedLandmarks = hands[0];
      this.landmarksBuf.push({ t: now / 1000, lm: normalizedLandmarks });
      this.lastSeenTime = now / 1000;
      // 2D 描画（1本目は通常、2本目があれば薄色）
      this.drawLandmarks(ctx, normalizedLandmarks, cssW, cssH, video.videoWidth, video.videoHeight);
      if (hands[1]) {
        ctx.save(); ctx.globalAlpha = 0.6;
        this.drawLandmarks(ctx, hands[1], cssW, cssH, video.videoWidth, video.videoHeight);
        ctx.restore();
      }

      // グーの手を自動検出してジョイスティック値を更新（優先: 2本目、次点: 1本目）
      let joyIdx = -1;
      const candidates = hands[1] ? [1, 0] : [0];
      for (const i of candidates) {
        if (this.isFist(hands[i], cssW, cssH, video.videoWidth, video.videoHeight)) { joyIdx = i; break; }
      }
      if (joyIdx >= 0) {
        const { center, palmSize } = this.getPalmCenterAndSize(hands[joyIdx], cssW, cssH, video.videoWidth, video.videoHeight);
        // グー検出 → 最終検出時刻更新
        this.lastFistSeenTs = now / 1000;
        if (!this.fistOrigin) this.fistOrigin = { x: center.x, y: center.y };
        const dx = center.x - this.fistOrigin.x;
        const dead = CFG.joystick.deadzonePalmRatio * palmSize;
        const full = CFG.joystick.maxRangePalmRatio * palmSize;
        let xRaw = 0;
        if (Math.abs(dx) > dead) {
          const sign = Math.sign(dx);
          const mag = Math.min(1, (Math.abs(dx) - dead) / Math.max(1, (full - dead)));
          xRaw = sign * mag;
        }
        this.joystickX = lerp(this.joystickX, xRaw, CFG.joystick.smoothAlpha);
        this.joystickActive = true;
      } else {
        // グー未検出 → ラベルは即座に通常表示へ。原点は 0.5s 経過でリセット。
        const nowSec = now / 1000;
        this.joystickActive = false; // HUD ラベルは非アクティブ
        if (this.lastFistSeenTs > 0 && (nowSec - this.lastFistSeenTs) >= CFG.joystick.resetDelaySec) {
          this.fistOrigin = null; // 原点リセット
        }
        // 値自体は徐々に 0 へ収束
        this.joystickX = lerp(this.joystickX, 0, CFG.joystick.smoothAlpha);
      }
      // HUD に常時出す（FIST ラベルはアクティブ時）
      ctx.save();
      ctx.fillStyle = this.joystickActive ? 'rgba(255,200,0,0.95)' : 'rgba(255,255,255,0.75)';
      ctx.font = '14px system-ui, sans-serif';
      const label = this.joystickActive ? 'FIST JS' : 'JS';
      ctx.fillText(`${label}: ${this.joystickX.toFixed(2)}`, cssW - 160, 24);
      ctx.restore();
      this.noHandCount = 0;
    } else {
      // 手が見えない → NONE へ収束
      normalizedLandmarks = null;
      this.noHandCount++;
    }

  // ジェスチャ分類（最新のバッファから）
  const { state, confidence } = this.classify(now / 1000);
    this.state = state;
    this.stateConf = confidence;

    this.onResult && this.onResult({ fps: this.fps, state, confidence });

  // デバッグ HUD 表示
  this.drawHUD(this.ctx, this.overlay, this.fps, !!normalizedLandmarks);

    // 次フレーム
    requestAnimationFrame(() => this.processLoop());
  }

  drawHUD(ctx, canvas, fps, hasLm) {
    const cssW = canvas.clientWidth || window.innerWidth;
    const cssH = canvas.clientHeight || window.innerHeight;
    ctx.save();
    ctx.fillStyle = 'rgba(0,0,0,0.35)';
    ctx.strokeStyle = 'rgba(255,255,255,0.25)';
    ctx.lineWidth = 1;
    ctx.fillRect(8, 8, 140, 40);
    ctx.strokeRect(8, 8, 140, 40);
    ctx.fillStyle = '#fff';
    ctx.font = '12px system-ui, sans-serif';
    ctx.fillText(`MP: ${this.handLandmarker ? 'OK' : 'NG'}`, 14, 25);
    ctx.fillText(`FPS: ${Math.round(fps)}`, 14, 40);
    if (!hasLm) {
      ctx.fillStyle = 'rgba(255,255,255,0.9)';
      ctx.fillText('No hand', 80, 25);
    }
    ctx.restore();
  }

  normalizeLandmarks01(lm, mirror) {
    // 0..1 正規化のまま保持。ミラー時は x を反転。
    return lm.map(({ x, y, z }) => ({
      x: mirror ? (1 - x) : x,
      y: y,
      z: z,
    }));
  }

  drawLandmarks(ctx, lm, cssW, cssH, videoW, videoH) {
    // video の object-fit: cover を考慮して正しく投影
    const aspectV = videoW / Math.max(1, videoH);
    const aspectC = cssW / Math.max(1, cssH);
    const s = aspectC >= aspectV ? (cssW / Math.max(1, videoW)) : (cssH / Math.max(1, videoH));
    const drawW = videoW * s;
    const drawH = videoH * s;
    const offX = (cssW - drawW) / 2;
    const offY = (cssH - drawH) / 2;

    // スクリーン座標に変換
    const screen = lm.map(p => ({ x: offX + p.x * drawW, y: offY + p.y * drawH }));

    // 簡易スケルトン描画（点のみ + 数本の線）
    ctx.save();
    ctx.lineWidth = 2.5;
    ctx.strokeStyle = 'rgba(0,255,180,0.9)';
    ctx.fillStyle = 'rgba(0,255,180,0.9)';

    const pairs = [
      [0, 1], [1, 2], [2, 3], [3, 4],
      [0, 5], [5, 6], [6, 7], [7, 8],
      [5, 9], [9, 10], [10, 11], [11, 12],
      [9, 13], [13, 14], [14, 15], [15, 16],
      [13, 17], [17, 18], [18, 19], [19, 20],
      [0, 17]
    ];
    ctx.beginPath();
    for (const [a, b] of pairs) {
      ctx.moveTo(screen[a].x, screen[a].y);
      ctx.lineTo(screen[b].x, screen[b].y);
    }
    ctx.stroke();

    for (const p of screen) {
      ctx.beginPath();
      ctx.arc(p.x, p.y, 3, 0, Math.PI * 2);
      ctx.fill();
    }
    // No hand の簡易表示
    if (this.noHandCount > 15) {
      ctx.fillStyle = 'rgba(255,255,255,0.9)';
      ctx.font = '12px system-ui, sans-serif';
      ctx.fillText('No hand detected', 10, cssH - 12);
    }
    ctx.restore();
  }

  // もう一方の手が "グー" かを判定
  isFist(lm01, cssW, cssH, videoW, videoH) {
    const { center, palmSize } = this.getPalmCenterAndSize(lm01, cssW, cssH, videoW, videoH);

    // 指先
    const P = (i) => this.project01ToPx(lm01[i], cssW, cssH, videoW, videoH);
    const tips = [4, 8, 12, 16, 20].map(P);
    const closeCount = tips.reduce((cnt, p) => cnt + (Math.hypot(p.x - center.x, p.y - center.y) <= CFG.fist.maxTipPalmRatio * palmSize ? 1 : 0), 0);
    return closeCount >= CFG.fist.minTipsClose;
  }

  // 0..1 正規化座標を画面ピクセルへ投影（object-fit: cover 前提）
  project01ToPx(pt01, cssW, cssH, videoW, videoH) {
    const aspectV = videoW / Math.max(1, videoH);
    const aspectC = cssW / Math.max(1, cssH);
    const s = aspectC >= aspectV ? (cssW / Math.max(1, videoW)) : (cssH / Math.max(1, videoH));
    const drawW = videoW * s;
    const drawH = videoH * s;
    const offX = (cssW - drawW) / 2;
    const offY = (cssH - drawH) / 2;
    return { x: offX + pt01.x * drawW, y: offY + pt01.y * drawH };
  }

  // 掌中心とサイズ（px）を取得
  getPalmCenterAndSize(lm01, cssW, cssH, videoW, videoH) {
    const idx = [0, 5, 9, 13, 17];
    const pts = idx.map(i => this.project01ToPx(lm01[i], cssW, cssH, videoW, videoH));
    const cx = pts.reduce((s, p) => s + p.x, 0) / pts.length;
    const cy = pts.reduce((s, p) => s + p.y, 0) / pts.length;
    const palmSize = pts.map(p => Math.hypot(p.x - cx, p.y - cy)).reduce((s, v) => s + v, 0) / pts.length;
    return { center: { x: cx, y: cy }, palmSize: Math.max(1, palmSize) };
  }

  classify(nowSec) {
    // 直近 windowSec のデータを抽出
    const windowLen = CFG.windowSec;
    const arr = this.landmarksBuf.toArray().filter((e) => nowSec - e.t <= windowLen);
    if (arr.length < 4) return { state: 'NONE', confidence: 0 };
    // 最終観測が古い場合は NONE
    const lastT = arr[arr.length - 1].t;
    if (nowSec - lastT > 0.25) return { state: 'NONE', confidence: 0 };

    // 画面への投影スケール（px）を計算（object-fit: cover 対応）
    const cssW = this.overlay.clientWidth || window.innerWidth;
    const cssH = this.overlay.clientHeight || window.innerHeight;
    const vw = this.video.videoWidth || cssW;
    const vh = this.video.videoHeight || cssH;
    const s = (cssW / Math.max(1, vw)) >= (cssH / Math.max(1, vh)) ? (cssW / Math.max(1, vw)) : (cssH / Math.max(1, vh));
    const drawW = vw * s;
    const drawH = vh * s;
    const offX = (cssW - drawW) / 2;
    const offY = (cssH - drawH) / 2;

    // ピクセル座標系列を構築
    const idx8 = 8, idx12 = 12, idx0 = 0, idx4 = 4, idx5 = 5, idx9 = 9, idx13 = 13, idx17 = 17;
    const time = arr.map((e) => e.t);
    const y8 = arr.map((e) => offY + e.lm[idx8].y * drawH);
    const y12 = arr.map((e) => offY + e.lm[idx12].y * drawH);
    const wrist = arr.map((e) => ({ x: offX + e.lm[idx0].x * drawW, y: offY + e.lm[idx0].y * drawH }));
    const thumb4 = arr.map((e) => ({ x: offX + e.lm[idx4].x * drawW, y: offY + e.lm[idx4].y * drawH }));

    // 手の甲中心とサイズ（掌基部 5,9,13,17 + 手首0 の重心と広がり）
    const palmCenters = arr.map((e) => {
      const pts = [idx0, idx5, idx9, idx13, idx17].map(i => ({ x: offX + e.lm[i].x * drawW, y: offY + e.lm[i].y * drawH }));
      const cx = pts.reduce((s, p) => s + p.x, 0) / pts.length;
      const cy = pts.reduce((s, p) => s + p.y, 0) / pts.length;
      const d = pts.map(p => Math.hypot(p.x - cx, p.y - cy));
      const palmSize = d.reduce((s, v) => s + v, 0) / Math.max(1, d.length);
      return { x: cx, y: cy, size: Math.max(1, palmSize) };
    });

    // RUN の周期測定は廃止（ユーザー要望）。

    // 指先（人差し指 8）
    const tip = arr.map((e) => ({ x: offX + e.lm[idx8].x * drawW, y: offY + e.lm[idx8].y * drawH }));
    const tipVx = diffSeries(time, tip.map(p => p.x));
    const tipVy = diffSeries(time, tip.map(p => p.y));
    const tipSpeed = tipVx.map((v, i) => Math.hypot(v, tipVy[i]));

    // KICK（簡素化）: 指先の速度ピークのみで判定
    const tipSpeedPeak = Math.max(...tipSpeed);
    let kickScore = 0;
    if (tipSpeedPeak > CFG.kick.minTipSpeedPxPerSec) {
      kickScore = clamp((tipSpeedPeak - CFG.kick.minTipSpeedPxPerSec) / CFG.kick.minTipSpeedPxPerSec, 0, 1);
    }

    // RUN（簡素化）: KICK でない限りすべて RUN。加速用の confidence は指先速度RMSから算出。
    const tipAmp = rms(tipSpeed);
    const runConf = clamp((tipAmp - CFG.run.minTipSpeedPxPerSec) / CFG.run.minTipSpeedPxPerSec, 0, 1);

    // ヒステリシス + デバウンス
    const now = nowSec;
    const since = now - this.lastTriggerTime;
    let nextState = this.state;
    let conf = 0;

    const kickOn = kickScore >= CFG.hysteresis.on;
    const kickOff = kickScore <= CFG.hysteresis.off;
    const runOn = runConf >= CFG.hysteresis.on;
    const runOff = runConf <= CFG.hysteresis.off;

    if (this.state === 'KICK') {
      if (kickOff) {
        // KICK を抜けたら、走りが十分であれば RUN、そうでなければ NONE
        if (runOn) { nextState = 'RUN'; conf = runConf; }
        else { nextState = 'NONE'; conf = 0; }
      } else {
        nextState = 'KICK';
        conf = kickScore;
      }
    } else if (this.state === 'RUN') {
      if (kickOn && since > CFG.debounceSec) {
        nextState = 'KICK';
        conf = kickScore;
        this.lastTriggerTime = now;
      } else if (runOff) {
        nextState = 'NONE';
        conf = 0;
      } else {
        nextState = 'RUN';
        conf = runConf;
      }
    } else { // NONE
      if (kickOn && since > CFG.debounceSec) {
        nextState = 'KICK';
        conf = kickScore;
        this.lastTriggerTime = now;
      } else if (runOn) {
        nextState = 'RUN';
        conf = runConf;
      } else {
        nextState = 'NONE';
        conf = 0;
      }
    }

    return { state: nextState, confidence: clamp(conf, 0, 1) };
  }
}
