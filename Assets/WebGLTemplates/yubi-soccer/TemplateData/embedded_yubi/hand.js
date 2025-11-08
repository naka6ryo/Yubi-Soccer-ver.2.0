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
    minSpeedAmp: 80, // px/s 相当（指振りの速度閾値）
    // 代替: 手首の上下速度のゼロ交差から走動作（周期運動）を検出
    freqBandHz: [1.6, 4.0], // 許容する歩幅/走行の周波数帯（1/s）
    zeroXMinAmp: 80,       // px/s ゼロ交差判定に用いる最小速度（ノイズ抑制）
    minTipSpeedPxPerSec: 80, // 甲から離れた領域での指先速度の下限（RUN 用）
    // RUN を即座に終了させるための低い閾値（通常の off より低く設定）
    immediateOffThreshold: 80, // px/s これ以下になったら即座に NONE へ
  },
  kick: {
    minAngVel: 10.0, // rad/s
    minWristSpeed: 500.0, // px/s （10 px/frame @30fps 相当）
    // KICK は指先速度ピークのみで判定
    minTipSpeedPxPerSec: 3500, // 指先速度による KICK しきい値
    // 前方向（カメラ方向）への z 速度の最小値 (normalized z units per sec)
    // MediaPipe の z はカメラに近づくと通常負の値になるため、
    // ここでは負方向の速度（値が小さくなる＝より負）を期待する。
    minTipForwardZ: 0.5,
  },
  charge: {
    // 厳しめの判定に変更: より大きく曲げないと CHARGE とならないようにする
    // PIP 関節の角度しきい値 (rad)。angleAt(...) < angleThresholdRad -> 曲がっていると判定
    // 小さな曲がりを誤検出しないよう、PI(≈3.1416) から少し離れた値に設定する
  // しきい値を少し緩めて、軽い曲げでも検出しやすくする
  angleThresholdRad: 2.85,
    // CHARGE を開始するまでのホールド時間（秒）
    holdSec: 0.06,
    // MCP（第1関節）の角度もしきい値として考慮する
    mcpAngleThresholdRad: 2.85,
    // anyBend は KICK 抑止に使う閾値。主閾値に近づけて若干緩くする
    anyBendAngleRad: 2.70,
    anyBendMcpAngleRad: 2.70,
  },
};


async function loadTasksVision() {
  // ローカルUnityサーバは .mjs の MIME を正しく返さないため、CDN優先→ローカルの順に変更
  const candidates = [
    'https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.11/vision_bundle.mjs',
    'https://unpkg.com/@mediapipe/tasks-vision@0.10.11/vision_bundle.mjs',
    './vendor/mediapipe/vision_bundle.mjs',
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
  // 検出スロットル: ミリ秒単位。高速化: 30 FPS 相当に変更
  this.detectIntervalMs = 1000 / 30;
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
    // Scene transform smoothing state
    this.sceneTransform = { tx: 0, ty: 0, scale: 1 };
    // 0..1 smoothing factor (higher = snappier, lower = smoother/slower)
    this.sceneSmoothFactor = 0.12;
    // Debug DOM element for CHARGE tuning (created lazily)
    this._dbgDiv = null;
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
        // Use normalized 3D coordinates for angle checks (more robust / scale-invariant)
        const n = normalizedLandmarks;
        const angle3 = (ax, ay, az, bx, by, bz) => {
          const da = Math.hypot(ax, ay, az);
          const db = Math.hypot(bx, by, bz);
          if (da < 1e-6 || db < 1e-6) return Math.PI;
          let dot = (ax * bx + ay * by + az * bz) / (da * db);
          dot = Math.max(-1, Math.min(1, dot));
          return Math.acos(dot);
        };

        // helper to compute angle at center between a->center and b->center using normalized coords
        const angleAt = (idxA, idxCenter, idxB) => {
          const a = n[idxA] || { x: 0, y: 0, z: 0 };
          const c = n[idxCenter] || { x: 0, y: 0, z: 0 };
          const b = n[idxB] || { x: 0, y: 0, z: 0 };
          const ax = a.x - c.x, ay = a.y - c.y, az = (a.z || 0) - (c.z || 0);
          const bx = b.x - c.x, by = b.y - c.y, bz = (b.z || 0) - (c.z || 0);
          return angle3(ax, ay, az, bx, by, bz);
        };

        const idx = {
          wrist: 0, idxMCP: 5, idxPIP: 6, idxDIP: 7, idxTIP: 8,
          midMCP: 9, midPIP: 10, midDIP: 11, midTIP: 12
        };

  const indexPipAng = angleAt(idx.idxMCP, idx.idxPIP, idx.idxDIP);
  const indexMcpAng = angleAt(idx.wrist, idx.idxMCP, idx.idxPIP);
  const midPipAng = angleAt(idx.midMCP, idx.midPIP, idx.midDIP);
  const midMcpAng = angleAt(idx.wrist, idx.midMCP, idx.midPIP);

  // Finger-level bend: use PIP (第2関節) のみで判定する（ユーザ指定）
  const indexBent = (indexPipAng < CFG.charge.angleThresholdRad);
  const midBent = (midPipAng < CFG.charge.angleThresholdRad);
  // CHARGE if either index or middle finger PIP is bent
  isCharge = indexBent || midBent;

  // anyBend: use PIP only for suppressing KICK
  const anyBendIndex = (indexPipAng < (CFG.charge.anyBendAngleRad || CFG.charge.angleThresholdRad));
  const anyBendMid = (midPipAng < (CFG.charge.anyBendAngleRad || CFG.charge.angleThresholdRad));
  isAnyBend = anyBendIndex || anyBendMid;

        // Debugging: removed embedded overlay per user request (HUD removed).
      } catch (e) {
        // ignore errors in charge calc but keep flag false
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
      // KICK 遷移時は即座に送信
      if (this.prevState !== 'KICK') {
        window.parent.postMessage({ type: 'kick', confidence: this.stateConf }, '*');
      }
    } else {
      this.state = desiredState;
      this.stateConf = desiredConf;

      // 状態が変化したときのみメッセージ送信（重複送信を回避）
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
        const wasKick = this.state === 'KICK';
        this.state = 'KICK';
        this.stateConf = 1.0;
        this.kickHoldUntil = nowSecFloat + 1.0;
        this.chargePending = false;
        this.chargePendingUntil = 0;
        // 新規KICK遷移時のみメッセージ送信
        if (!wasKick) {
          window.parent.postMessage({ type: 'kick', confidence: this.stateConf }, '*');
        }
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
  // デバッグ HUD 表示 (DOM 側へ移動)。
  this.drawHUD(this.ctx, this.overlay, this.fps, !!normalizedLandmarks, isCharge);

  // update scene transform to center/zoom on the hand (gentler tuning)
  try {
    this.updateSceneTransform(normalizedLandmarks);
  } catch (e) { /* ignore */ }

    // 次フレーム
    requestAnimationFrame(() => this.processLoop());
  }

  // hand preview intentionally removed to simplify UI (was previously drawHandPreview)

  updateSceneTransform(normalizedLandmarks) {
    const scene = document.getElementById('scene');
    if (!scene) return;
    const video = this.video;
    const overlay = this.overlay;
    const cssW = overlay.clientWidth || window.innerWidth;
    const cssH = overlay.clientHeight || window.innerHeight;

    if (!normalizedLandmarks || !normalizedLandmarks[0]) {
      // smoothly reset transform to identity
      const targetTx = 0, targetTy = 0, targetScale = 1;
      this.sceneTransform.tx = lerp(this.sceneTransform.tx, targetTx, this.sceneSmoothFactor);
      this.sceneTransform.ty = lerp(this.sceneTransform.ty, targetTy, this.sceneSmoothFactor);
      this.sceneTransform.scale = lerp(this.sceneTransform.scale, targetScale, this.sceneSmoothFactor);
      scene.style.transform = `translate(${this.sceneTransform.tx}px, ${this.sceneTransform.ty}px) scale(${this.sceneTransform.scale})`;
      return;
    }
    // compute mapping used by drawLandmarks (object-fit: cover handling)
    const videoW = video.videoWidth || cssW;
    const videoH = video.videoHeight || cssH;
    const aspectV = videoW / Math.max(1, videoH);
    const aspectC = cssW / Math.max(1, cssH);
    const s = aspectC >= aspectV ? (cssW / Math.max(1, videoW)) : (cssH / Math.max(1, videoH));
    const drawW = videoW * s;
    const drawH = videoH * s;
    const offX = (cssW - drawW) / 2;
    const offY = (cssH - drawH) / 2;

    // Build bounding box of landmarks (ensure fingertips included)
    const indices = Array.from({ length: 21 }, (_, i) => i); // 0..20
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const i of indices) {
      const p = normalizedLandmarks[i];
      if (!p) continue;
      const px = offX + p.x * drawW;
      const py = offY + p.y * drawH;
      if (px < minX) minX = px;
      if (py < minY) minY = py;
      if (px > maxX) maxX = px;
      if (py > maxY) maxY = py;
    }
    if (!isFinite(minX) || !isFinite(minY) || !isFinite(maxX) || !isFinite(maxY)) {
      // fallback to identity
      const targetTx = 0, targetTy = 0, targetScale = 1;
      this.sceneTransform.tx = lerp(this.sceneTransform.tx, targetTx, this.sceneSmoothFactor);
      this.sceneTransform.ty = lerp(this.sceneTransform.ty, targetTy, this.sceneSmoothFactor);
      this.sceneTransform.scale = lerp(this.sceneTransform.scale, targetScale, this.sceneSmoothFactor);
      scene.style.transform = `translate(${this.sceneTransform.tx}px, ${this.sceneTransform.ty}px) scale(${this.sceneTransform.scale})`;
      return;
    }

    const bboxW = Math.max(1, maxX - minX);
    const bboxH = Math.max(1, maxY - minY);
    const bboxCX = (minX + maxX) / 2;
    const bboxCY = (minY + maxY) / 2;

    // Use the smaller viewport dimension to keep square cropping behavior
    const viewportSize = Math.min(cssW, cssH);
    // margin fraction around the bbox (10-12%) to ensure fingertips aren't flush to edge
    const margin = 0.12;
    const available = viewportSize * (1 - margin * 2);
    let scale = available / Math.max(bboxW, bboxH);

    // clamp: do not zoom out below 1 (keep original or zoom in), and cap zoom-in to 2x
    scale = Math.max(1, Math.min(scale, 2));

    // compute translate so that bbox center maps to viewport center after scaling
    const centerX = cssW / 2;
    const centerY = cssH / 2;
    const targetTx = centerX - bboxCX * scale;
    const targetTy = centerY - bboxCY * scale;

    // smooth the motion using lerp toward target values
    this.sceneTransform.tx = lerp(this.sceneTransform.tx, targetTx, this.sceneSmoothFactor);
    this.sceneTransform.ty = lerp(this.sceneTransform.ty, targetTy, this.sceneSmoothFactor);
    this.sceneTransform.scale = lerp(this.sceneTransform.scale, scale, this.sceneSmoothFactor);

    scene.style.transform = `translate(${this.sceneTransform.tx}px, ${this.sceneTransform.ty}px) scale(${this.sceneTransform.scale})`;
  }

  drawHUD(ctx, canvas, fps, hasLm, charge) {
    // HUD drawing moved to DOM (#fps). Keep this function as a no-op to avoid
    // double-rendering the FPS text on the overlay canvas.
    return;
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
      } else if (tipAmp < CFG.run.immediateOffThreshold) {
        // 走るのをやめたら即座に NONE へ（指先速度が閾値以下）
        nextState = 'NONE';
        conf = 0;
      } else if (runOff) {
        // 通常のヒステリシスによる OFF
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
