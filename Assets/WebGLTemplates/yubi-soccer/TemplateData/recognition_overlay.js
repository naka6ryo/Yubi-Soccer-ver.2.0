// recognition_overlay.js
// 親ページ上で JSON の認識結果を表示する簡易オーバーレイ受信器。
// 埋め込み iframe から postMessage で受け取るか、Unity 側や他スクリプトから
// window.updateRecognition(json) を呼び出して使用します。
(function(){
  function ensurePanel(){
    if (window.__recognitionPanelInited) return;
    window.__recognitionPanelInited = true;
    const panel = document.getElementById('recognition-panel');
    if (!panel) return;
    const content = document.createElement('pre');
    content.id = 'recognition-content';
    content.style.margin = '0';
    content.style.whiteSpace = 'pre-wrap';
    content.style.wordBreak = 'break-word';
    panel.appendChild(content);
  }

  window.updateRecognition = function(jsonOrString){
    try{
      ensurePanel();
      const content = document.getElementById('recognition-content');
      if (!content) return;
      let obj = jsonOrString;
      if (typeof jsonOrString === 'string') {
        try { obj = JSON.parse(jsonOrString); } catch(e) { obj = jsonOrString; }
      }
      if (typeof obj === 'object') {
        content.textContent = JSON.stringify(obj, null, 2);
      } else {
        content.textContent = String(obj);
      }
    }catch(e){ console.warn('[recognition_overlay] update error', e); }
  };

  window.clearRecognition = function(){
    try{ const content = document.getElementById('recognition-content'); if(content) content.textContent = ''; }catch(e){}
  };

  // fetch で URL から定期取得して表示する補助関数（デバッグ用）
  window.fetchRecognitionFromUrl = function(url, intervalMs=1000){
    if (!url) return; let tid = null;
    async function tick(){
      try{ const r = await fetch(url); if (r.ok){ const t = await r.text(); window.updateRecognition(t); } }
      catch(e){ /* ignore */ }
    }
    tick();
    tid = setInterval(tick, intervalMs);
    return () => { if (tid) clearInterval(tid); };
  };

  // iframe からの postMessage を受けて自動表示
  window.addEventListener('message', (ev)=>{
    try{
      const d = ev.data || {};
      if (d && d.type === 'state') {
        window.updateRecognition(d);
      }
    }catch(e){}
  }, false);

})();
