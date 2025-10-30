// renderer.js - Three.js 初期化とシーン構築（描画責務を分離）
import * as THREE from 'https://unpkg.com/three@0.160.0/build/three.module.js';

export function setupRenderer(canvas) {
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true });
  renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
  renderer.setSize(window.innerWidth, window.innerHeight);

  const scene = new THREE.Scene();

  const camera = new THREE.PerspectiveCamera(60, window.innerWidth / window.innerHeight, 0.1, 100);
  camera.position.set(0, 3, 8);
  camera.lookAt(0, 1, 0);

  const hemi = new THREE.HemisphereLight(0xffffff, 0x444444, 1.0);
  hemi.position.set(0, 20, 0);
  scene.add(hemi);

  const dir = new THREE.DirectionalLight(0xffffff, 0.8);
  dir.position.set(5, 10, 5);
  scene.add(dir);
  // 埋め込み iframe 内で動作する場合は、UI 上の混整を避けるため
  // ボールとフィールドの描画をスキップする（親ページ側で表示する想定）
  const isEmbedded = (window.self !== window.top) || /[?&]embedded=1/.test(window.location.search);
  let field = null;
  let ball = null;
  if (!isEmbedded) {
    // フィールド平面
    const fieldGeo = new THREE.PlaneGeometry(20, 40);
    const fieldMat = new THREE.MeshStandardMaterial({ color: 0x1b5e20, roughness: 1.0, metalness: 0.0 });
    field = new THREE.Mesh(fieldGeo, fieldMat);
    field.rotation.x = -Math.PI / 2;
    field.position.y = 0 - 0.001;
    scene.add(field);

    // ボール（物理モジュールへ参照を渡すためエクスポートする）
    const ballGeo = new THREE.SphereGeometry(0.3, 32, 16);
    const ballMat = new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.6, metalness: 0.1 });
    ball = new THREE.Mesh(ballGeo, ballMat);
    ball.position.set(0, 0.3, 0);
    scene.add(ball);
  }

  const clock = new THREE.Clock();

  window.addEventListener('resize', () => resizeRendererToDisplaySize(renderer, camera));

  return { scene, camera, renderer, clock, ball, field, isEmbedded };
}

export function resizeRendererToDisplaySize(renderer, camera) {
  const w = window.innerWidth;
  const h = window.innerHeight;
  const needResize = renderer.domElement.width !== w || renderer.domElement.height !== h;
  if (needResize) {
    renderer.setSize(w, h, false);
    camera.aspect = w / h;
    camera.updateProjectionMatrix();
  }
}
