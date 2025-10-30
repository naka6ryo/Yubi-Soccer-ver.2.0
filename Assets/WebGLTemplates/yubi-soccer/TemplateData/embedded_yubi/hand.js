// hand.js - MediaPipe HandLandmarker + ジェスチャ判定
import { RingBuffer, MovingAvg, normalizedCrossCorrelation, angleBetween, diffSeries, rms, clamp, lerp, zeroCrossingTimes } from './utils.js';

// MediaPipe tasks-vision の読み込みは動的 import でローカル/CDN をフォールバック
// HTTPS or localhost が必要。iOS Safari はユーザ操作後にカメラ可。

const CFG = {
  inputTargetSize: 320, // 処理落ち時は 256 に下げる
  minFPSForHighRes: 26,
  windowSec: 0.7, // RUN 相関窓
  debounceSec: 0.3,
  hysteresis: { on: 0.65, off: 0.45 },
  run: {
    minAbsCorr: 0.5,
    minSpeedAmp: 200, // px/s 相当（指振りの速度閾値）
    // 代替: 手首の上下速度のゼロ交差から走動作（周期運動）を検出
    freqBandHz: [1.6, 4.0], // 許容する歩幅/走行の周波数帯（1/s）
    zeroXMinAmp: 80,       // px/s ゼロ交差判定に用いる最小速度（ノイズ抑制）
    minTipSpeedPxPerSec: 200, // 甲から離れた領域での指先速度の下限（RUN 用）
  },
  kick: {
    minAngVel: 10.0, // rad/s
    minWristSpeed: 500.0, // px/s （10 px/frame @30fps 相当）
    // KICK は指先速度ピークのみで判定
    minTipSpeedPxPerSec: 1800, // 指先速度による KICK しきい値
    // 前方向（カメラ方向）への z 速度の最小値 (normalized z units per sec)
    // MediaPipe の z はカメラに近づくと通常負の値になるため、
    // ここでは負方向の速度（値が小さくなる＝より負）を期待する。
    minTipForwardZ: 0.5,
  },
  charge: {
    // PIP 関節の角度しきい値 (rad)。angleBetween(PIP->MCP, PIP->DIP) がこの値未満なら曲がっていると判定
    angleThresholdRad: 1.5,
    // CHARGE を開始するまでのホールド時間（秒）
    holdSec: 0.1,
    // MCP（第1関節）の角度もしきい値として考慮する（angle at MCP between wrist->MCP and PIP->MCP）
    mcpAngleThresholdRad: 1.5,
    // KICK を抑止するための「わずかな曲がり」検出用しきい値（CHARGE の閾値は変更しない）
    // 直立（約 pi rad = 3.14）に近い値で小さな曲がりを検出する。デフォルトは 2.9rad（約166°）。
    anyBendAngleRad: 2.9,
    anyBendMcpAngleRad: 2.9,
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
  // 検出スロットル: ミリ秒単位。デフォルトは 15 FPS 相当
  this.detectIntervalMs = 1000 / 15;
  this.lastDetectTime = 0;
  this.lastDetectResult = null;

    // 時系列バッファ
    this.landmarksBuf = new RingBuffer(90); // 約3秒分@30fps
    this.state = 'NONE';
    this.stateConf = 0;
    // ゲーム側へ渡す統一されたアクション状態オブジェクト
    this.actionState = {
      state: this.state,
      confidence: this.stateConf,
      charge: false,
      fps: 0,
      ts: this.lastTs / 1000,
      tipSpeedPeak: 0,
      tipForwardMin: 0,
      runConf: 0,
      palmSize: 0,
      lastSeenTime: 0,
      chargeHeld: false,
      chargePending: false,
    };
    // CHARGE ホールド開始時刻（秒）。null の場合は未ホールド
    this.chargeStartTime = null;
  // CHARGE 後に次の非 NONE を KICK に変換するフラグ
  this.chargePending = false;
  // chargePending が有効な最終時刻（秒）
  this.chargePendingUntil = 0;
  // CHARGE が holdSec を満たして確定したかを表す内部フラグ
  this.chargeHeld = false;
  // KICK を最低限保持するための有効期限（秒）
  this.kickHoldUntil = 0;
  this.lastTriggerTime = 0;
  this.lastSeenTime = 0; // 最後に手を検出した時刻（sec）
  this.noHandCount = 0;  // 連続で検出できなかったフレーム数

    // 推論入力用のオフスクリーン Canvas
    this.procCanvas = document.createElement('canvas');
    this.procCtx = this.procCanvas.getContext('2d', { willReadFrequently: true });

    // ジョイスティック機能を削除して片手検出に簡素化
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
        // 軽量化: デフォルトで numHands=1 にして負荷を抑える
        this.handLandmarker = await HandLandmarker.createFromOptions(filesetResolver, {
          baseOptions: { modelAssetPath: modelPath },
          numHands: 1,
          runningMode: 'VIDEO',
          minHandDetectionConfidence: 0.35,
          minHandPresenceConfidence: 0.35,
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
      // 検出はスロットルして実行。検出は遅延実行されるが、描画は直前の結果を使う。
      const shouldDetect = (now - this.lastDetectTime) >= this.detectIntervalMs;
      if (shouldDetect) {
        try {
          const res = await this.handLandmarker.detectForVideo(this.procCanvas, now);
          this.lastDetectTime = now;
          this.lastDetectResult = res;
          lmResult = res;
        } catch (e) {
          // 検出失敗時は前回の結果を使用
          lmResult = this.lastDetectResult;
        }
      } else {
        // スロットル中はキャッシュされた結果を使う
        lmResult = this.lastDetectResult;
      }
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
  let isCharge = false;
  // isAnyBend: CHARGE より緩い閾値でわずかな曲がりを検出し、KICK を抑止するために使う
  let isAnyBend = false;
  if (lmResult && lmResult.landmarks && lmResult.landmarks[0]) {
      // 0..1 正規化座標（鏡反転のみ適用、ピクセル変換は描画・分類時に行う）
      const hands = lmResult.landmarks.map(lm => this.normalizeLandmarks01(lm, this.mirror));
      normalizedLandmarks = hands[0];
      this.landmarksBuf.push({ t: now / 1000, lm: normalizedLandmarks });
      this.lastSeenTime = now / 1000;
      // 2D 描画（片手のみ表示）
      this.drawLandmarks(ctx, normalizedLandmarks, cssW, cssH, video.videoWidth, video.videoHeight);
      // CHARGE 判定: 人差し指の PIP(6) を基準に MCP(5) と DIP(7) との角度を測る
      // さらに中指(PIP 10, MCP 9, DIP 11) も同様に CHARGE として扱う
      try {
  const pMCP = this.project01ToPx(normalizedLandmarks[5], cssW, cssH, video.videoWidth, video.videoHeight);
  const pPIP = this.project01ToPx(normalizedLandmarks[6], cssW, cssH, video.videoWidth, video.videoHeight);
  const pDIP = this.project01ToPx(normalizedLandmarks[7], cssW, cssH, video.videoWidth, video.videoHeight);
  // PIP の角度 (PIP を中心に MCP->PIP と DIP->PIP の角度)
  const ax = pMCP.x - pPIP.x; const ay = pMCP.y - pPIP.y;
  const bx = pDIP.x - pPIP.x; const by = pDIP.y - pPIP.y;
  const ang = angleBetween(ax, ay, bx, by);
  if (ang < CFG.charge.angleThresholdRad) isCharge = true;
  // anyBend: PIP の角度が CHARGE より緩い閾値を下回れば "わずかに曲がっている" とみなす
  if (ang < (CFG.charge.anyBendAngleRad || CFG.charge.angleThresholdRad)) isAnyBend = true;
  // MCP の角度 (MCP を中心に 手首->MCP と PIP->MCP の角度)
  const pWrist = this.project01ToPx(normalizedLandmarks[0], cssW, cssH, video.videoWidth, video.videoHeight);
  const mx1 = pWrist.x - pMCP.x; const my1 = pWrist.y - pMCP.y;
  const mx2 = pPIP.x - pMCP.x; const my2 = pPIP.y - pMCP.y;
  const mcpAng = angleBetween(mx1, my1, mx2, my2);
  if (mcpAng < (CFG.charge.mcpAngleThresholdRad || CFG.charge.angleThresholdRad)) isCharge = true;
        // 中指もチェック
  const mMCP = this.project01ToPx(normalizedLandmarks[9], cssW, cssH, video.videoWidth, video.videoHeight);
  const mPIP = this.project01ToPx(normalizedLandmarks[10], cssW, cssH, video.videoWidth, video.videoHeight);
  const mDIP = this.project01ToPx(normalizedLandmarks[11], cssW, cssH, video.videoWidth, video.videoHeight);
  const mx = mMCP.x - mPIP.x; const my = mMCP.y - mPIP.y;
  const bx2 = mDIP.x - mPIP.x; const by2 = mDIP.y - mPIP.y;
  const mang = angleBetween(mx, my, bx2, by2);
  if (mang < CFG.charge.angleThresholdRad) isCharge = true;
  if (mang < (CFG.charge.anyBendAngleRad || CFG.charge.angleThresholdRad)) isAnyBend = true;
  // 中指の MCP 角度も評価
  const mWrist = this.project01ToPx(normalizedLandmarks[0], cssW, cssH, video.videoWidth, video.videoHeight);
  const mmx1 = mWrist.x - mMCP.x; const mmy1 = mWrist.y - mMCP.y;
  const mmx2 = mPIP.x - mMCP.x; const mmy2 = mPIP.y - mMCP.y;
  const mmcpAng = angleBetween(mmx1, mmy1, mmx2, mmy2);
  if (mmcpAng < (CFG.charge.mcpAngleThresholdRad || CFG.charge.angleThresholdRad)) isCharge = true;
  if (mmcpAng < (CFG.charge.anyBendMcpAngleRad || CFG.charge.mcpAngleThresholdRad || CFG.charge.angleThresholdRad)) isAnyBend = true;
      } catch (e) {
        // ignore errors in charge calc
        isCharge = false;
      }
      this.noHandCount = 0;
    } else {
      // 手が見えない → NONE へ収束
      normalizedLandmarks = null;
      this.noHandCount++;
    }

  // ジェスチャ分類（最新のバッファから） -- classify はメトリクスも返す
  // 引数 suppressKick を通じて、指が曲がっている場合は KICK 判定を抑止する
  // suppressKick フラグには、CHARGE の閾値はそのままに「わずかな曲がり」を示す isAnyBend を渡す
  const { state, confidence, tipSpeedPeak, tipForwardMin, runConf, palmSize } = this.classify(now / 1000, isAnyBend);
    // CHARGE 表示フラグ (isCharge) は HUD/actionState 用であり，
    // 直接 this.state を書き換えない（RUN/NONE/KICK の判定に影響を与えない）
    // ただし，CHARGE が所定時間保持された（chargeHeld）あとに解除されたら
    // 次の非 NONE を KICK に変換する既存の挙動は維持する。
    const nowSecFloat = now / 1000;
    if (isCharge) {
      if (this.chargeStartTime === null) this.chargeStartTime = nowSecFloat;
      const held = (nowSecFloat - this.chargeStartTime) >= (CFG.charge.holdSec || 0.5);
      if (held) this.chargeHeld = true;
    } else {
      // CHARGE が解除されたとき、hold が成立していたら次の非 NONE を KICK にするフラグを立てる
      if (this.chargeHeld) {
        this.chargePending = true;
  // 1.0 秒間だけ有効にする
  this.chargePendingUntil = nowSecFloat + 1.0;
      }
      this.chargeHeld = false;
      this.chargeStartTime = null;
    }

    // state は基本的には classify() の結果を使うが、
    // CHARGE が確定（hold 成立）している間は HUD/状態として 'CHARGE' を優先表示する
    const desiredState = this.chargeHeld ? 'CHARGE' : state;
    const desiredConf = this.chargeHeld ? 1.0 : confidence;

    // 既に KICK 中であれば、kickHoldUntil を尊重して一定時間は KICK を継続する
    if (this.state === 'KICK' && nowSecFloat <= (this.kickHoldUntil || 0)) {
      // 維持: 何もしない（ただし表示確度は最大にしておく）
      this.state = 'KICK';
      this.stateConf = 1.0;
    } else if (desiredState === 'KICK' && this.state !== 'KICK') {
      // 新たに KICK へ遷移した -> 保持期限を設定
      this.state = 'KICK';
      this.stateConf = desiredConf;
      this.kickHoldUntil = nowSecFloat + 1.0;
      window.parent.postMessage({ type: 'kick', confidence: this.stateConf }, '*');
    } else {
      this.state = desiredState;
      this.stateConf = desiredConf;

      // 状態が変化したときのみメッセージ送信
      if (this.prevState !== this.state) {
        if (this.state === 'KICK') {
          window.parent.postMessage({ type: 'kick', confidence: this.stateConf }, '*');
        } else if (this.state === 'RUN') {
          window.parent.postMessage({ type: 'run', confidence: this.stateConf }, '*');
        } else if (this.state === 'CHARGE') {
          window.parent.postMessage({ type: 'charge', confidence: this.stateConf }, '*');
        } else if (this.state === 'IDLE' || this.state === 'NONE') {
          window.parent.postMessage({ type: 'idle', confidence: this.stateConf }, '*');
        }
      }
      this.prevState = this.state;
    }

     // chargePending が立っていれば、次のフレームで必ず KICK に遷移
    if (this.chargePending) {
      if (nowSecFloat > (this.chargePendingUntil || 0)) {
        // 期限切れ
        this.chargePending = false;
        this.chargePendingUntil = 0;
      } else {
        // 強制 KICK（chargePending）: KICK に上書きし、保持期限を設定
        this.state = 'KICK';
        this.stateConf = 1.0;
        this.kickHoldUntil = nowSecFloat + 1.0;
        this.chargePending = false;
        this.chargePendingUntil = 0;
      }
    }
// 更新されたアクション状態を組み立てて onResult に渡す
  this.actionState.state = this.state;
  this.actionState.confidence = this.stateConf;
  this.actionState.charge = isCharge;
  this.actionState.bent = !!isCharge;
  this.actionState.fps = this.fps;
  this.actionState.ts = now / 1000;
  this.actionState.tipSpeedPeak = tipSpeedPeak || 0;
  this.actionState.tipForwardMin = tipForwardMin || 0;
  this.actionState.runConf = runConf || 0;
  this.actionState.palmSize = palmSize || 0;
  this.actionState.lastSeenTime = this.lastSeenTime;
  this.actionState.chargeHeld = !!this.chargeHeld;
  this.actionState.chargePending = !!(this.chargePending && nowSecFloat <= this.chargePendingUntil);

  this.onResult && this.onResult({ fps: this.fps, state: this.state, confidence: this.stateConf, charge: isCharge, actionState: this.actionState });
  // デバッグ HUD 表示
  this.drawHUD(this.ctx, this.overlay, this.fps, !!normalizedLandmarks, isCharge);

    // 次フレーム
    requestAnimationFrame(() => this.processLoop());
  }

  drawHUD(ctx, canvas, fps, hasLm, charge) {
    const cssW = canvas.clientWidth || window.innerWidth;
    const cssH = canvas.clientHeight || window.innerHeight;
    ctx.save();
    ctx.fillStyle = 'rgba(0,0,0,0.35)';
    ctx.strokeStyle = 'rgba(255,255,255,0.25)';
    ctx.lineWidth = 1;
  ctx.fillRect(8, 8, 180, 56);
  ctx.strokeRect(8, 8, 180, 56);
    ctx.fillStyle = '#fff';
    ctx.font = '12px system-ui, sans-serif';
    ctx.fillText(`MP: ${this.handLandmarker ? 'OK' : 'NG'}`, 14, 25);
  ctx.fillText(`FPS: ${Math.round(fps)}`, 14, 40);
  // current state
  ctx.fillStyle = 'rgba(200,220,255,0.95)';
  ctx.font = '12px system-ui, sans-serif';
  ctx.fillText(`STATE: ${this.state}`, 14, 54);
    // CHARGE 表示は UI 側で削除：何も描かない
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

  classify(nowSec, suppressKick = false) {
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

    // 指先（人差し指 8 と 中指 12）
    const tipIndex = arr.map((e) => ({ x: offX + e.lm[idx8].x * drawW, y: offY + e.lm[idx8].y * drawH }));
  const tipIndexZ = arr.map((e) => (e.lm[idx8].z ?? 0));
    const tipIndexVx = diffSeries(time, tipIndex.map(p => p.x));
    const tipIndexVy = diffSeries(time, tipIndex.map(p => p.y));
    const tipIndexSpeed = tipIndexVx.map((v, i) => Math.hypot(v, tipIndexVy[i]));
  const tipIndexVz = diffSeries(time, tipIndexZ);

    const tipMid = arr.map((e) => ({ x: offX + e.lm[idx12].x * drawW, y: offY + e.lm[idx12].y * drawH }));
  const tipMidZ = arr.map((e) => (e.lm[idx12].z ?? 0));
    const tipMidVx = diffSeries(time, tipMid.map(p => p.x));
    const tipMidVy = diffSeries(time, tipMid.map(p => p.y));
    const tipMidSpeed = tipMidVx.map((v, i) => Math.hypot(v, tipMidVy[i]));
  const tipMidVz = diffSeries(time, tipMidZ);

    // combine: pick the maximum peak among index and middle
    const tipSpeed = tipIndexSpeed.concat(tipMidSpeed);
    const tipVz = tipIndexVz.concat(tipMidVz);
    const tipSpeedPeak = Math.max(...tipIndexSpeed, ...tipMidSpeed);
    const tipForwardMin = Math.min(...tipIndexVz, ...tipMidVz);

  // KICK（簡素化）: 指先の速度ピークのみで判定
  // tipSpeedPeak と tipForwardMin は index/middle 両方のピーク/最小値を既に計算済み
    let kickScore = 0;
    // suppressKick が指定されている場合は KICK 判定を抑止する
    if (!suppressKick) {
      // 2D の速度ピークが閾値を超え、かつ前方への z 速度が閾値以上であることを要求する
      if (tipSpeedPeak > CFG.kick.minTipSpeedPxPerSec && tipForwardMin <= -CFG.kick.minTipForwardZ) {
        kickScore = clamp((tipSpeedPeak - CFG.kick.minTipSpeedPxPerSec) / CFG.kick.minTipSpeedPxPerSec, 0, 1);
      }
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

    return { state: nextState, confidence: clamp(conf, 0, 1), tipSpeedPeak, tipForwardMin, runConf, palmSize: palmCenters.length ? palmCenters[palmCenters.length-1].size : 0 };
  }
}
