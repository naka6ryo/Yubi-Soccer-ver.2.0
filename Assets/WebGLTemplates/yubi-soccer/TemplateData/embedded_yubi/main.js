// main.js - Three.js シーンと簡易物理
import * as THREE from 'https://unpkg.com/three@0.160.0/build/three.module.js';

// main.js now contains only physics and game-state logic.
let ball = null;
let velocity = new THREE.Vector3(0, 0, 0);

const PARAMS = {
  gravity: -9.8,
  friction: 0.98,
  airDrag: 0.995,
  floorY: 0,
  runAccel: 2.0, // RUN 強度に比例して前進加速
  runMaxSpeed: 6.0,
  kickUp: 6.0,   // KICK の上方向係数
  kickForward: 8.0, // KICK の前方向係数
  restitution: 0.5, // 反発係数
};

// ball は外部（renderer.js）が作成して setBall() で渡す
export function setBall(mesh) {
  ball = mesh;
}

// resize は renderer 側で実行するため main.js 側では実装しない

export function updatePhysics(dt) {
  if (!dt) return;

  // ball が存在しない（埋め込みモード等）の場合は物理更新をスキップ
  if (!ball) return;

  // 重力
  velocity.y += PARAMS.gravity * dt;

  // 空気抵抗
  velocity.multiplyScalar(PARAMS.airDrag);

  // 位置更新
  ball.position.addScaledVector(velocity, dt);

  // 床衝突
  const radius = 0.3;
  if (ball.position.y - radius < PARAMS.floorY) {
    ball.position.y = PARAMS.floorY + radius;
    if (velocity.y < 0) velocity.y = -velocity.y * PARAMS.restitution;
    // 接地摩擦
    velocity.x *= PARAMS.friction;
    velocity.z *= PARAMS.friction;
  }

  // 前進方向を -Z とする
  // 制限
  const horizontalSpeed = Math.hypot(velocity.x, velocity.z);
  if (horizontalSpeed > PARAMS.runMaxSpeed) {
    const scale = PARAMS.runMaxSpeed / horizontalSpeed;
    velocity.x *= scale;
    velocity.z *= scale;
  }
}

export function setRunBoost(conf) {
  // conf(0-1) に比例した前進加速度を与える（基礎加速は無し）
  const accel = PARAMS.runAccel * conf;
  velocity.z -= accel * (1 / 60); // フレーム単位で弱く積む（updatePhysics 内で dt で積むので微調整）
}

export function kickImpulse(conf) {
  // 瞬間インパルス
  const up = PARAMS.kickUp * (0.5 + 0.5 * conf);
  const forward = PARAMS.kickForward * (0.5 + 0.5 * conf);
  velocity.y += up;
  velocity.z -= forward;
}

// Exported symbols are only the physics API. Scene/camera/renderer are owned by renderer.js
export { ball };
