// mediapipe_unity_bridge.js (shim)
// WebGLBuildPostprocessor がテンプレートに自動挿入するスクリプト参照用ファイル。
// ここでは、埋め込みした Yubi-Soccer の vendor 配下にある MediaPipe を優先して使う
// ために modelBase を上書きします。Netlify 等に配置されたときのパス補正用です。
(function(){
  try{
    window.MPUBridgeConfig = window.MPUBridgeConfig || { gameObject: 'HandTracker', method: 'OnLandmarkJson' };
    // 親テンプレートの TemplateData 配下に埋め込まれた embedded_yubi を使う形に強制
    // 例: <site-root>/TemplateData/embedded_yubi/vendor/mediapipe/
    var basePath = '';
    try{
      var p = location.pathname.replace(/[^/]*$/, '');
      basePath = location.origin + p + 'TemplateData/embedded_yubi/vendor/mediapipe/';
    }catch(e){
      basePath = window.MPUBridgeConfig && window.MPUBridgeConfig.modelBase ? window.MPUBridgeConfig.modelBase : null;
    }
    if (basePath) {
      window.MPUBridgeConfig.modelBase = basePath;
      console.debug && console.debug('[MPUBridge shim] modelBase forced to', basePath);
    }
  }catch(e){ console.warn('[MPUBridge shim] error', e); }
})();
