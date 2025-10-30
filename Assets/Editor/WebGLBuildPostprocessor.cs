using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Postprocess WebGL builds: copy Assets/WebGL files into the build's TemplateData and inject the bridge tag
public class WebGLBuildPostprocessor : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL) return;

        string outPath = report.summary.outputPath; // folder containing index.html
        string templateData = Path.Combine(outPath, "TemplateData");
        try
        {
            Directory.CreateDirectory(templateData);

            // Copy mediapipe_unity_bridge.js
            string srcBridge = Path.Combine(Application.dataPath, "WebGL", "mediapipe_unity_bridge.js");
            if (File.Exists(srcBridge))
            {
                File.Copy(srcBridge, Path.Combine(templateData, "mediapipe_unity_bridge.js"), true);
                Debug.Log("[WebGLBuildPostprocessor] copied mediapipe_unity_bridge.js to TemplateData");
            }

            // Copy vendor folder (if exists)
            string srcVendor = Path.Combine(Application.dataPath, "WebGL", "vendor");
            string dstVendor = Path.Combine(templateData, "vendor");
            if (Directory.Exists(srcVendor))
            {
                CopyDirectory(srcVendor, dstVendor);
                Debug.Log("[WebGLBuildPostprocessor] copied vendor/ to TemplateData/vendor/");
            }

            // Inject script tag into index.html if not already present
            string indexPath = Path.Combine(outPath, "index.html");
            if (File.Exists(indexPath))
            {
                string html = File.ReadAllText(indexPath);
                if (!html.Contains("mediapipe_unity_bridge.js"))
                {
                    string snippet = "\n<!-- MPUBridge auto-insert -->\n<script>\n  // Resolve modelBase to a same-origin absolute URL at runtime based on the injected bridge script src.\n  (function(){\n    window.MPUBridgeConfig = window.MPUBridgeConfig || { gameObject: 'HandTracker', method: 'OnLandmarkJson' };\n    try {\n      var scripts = document.getElementsByTagName('script');\n      var found = null;\n      for (var i = scripts.length - 1; i >= 0; --i) {\n        var s = scripts[i].src || '';\n        if (s.indexOf('mediapipe_unity_bridge.js') != -1) { found = s; break; }\n      }\n      var modelBase = null;\n      if (found) {\n        var u = new URL(found, location.href);\n        var dir = u.pathname.replace(/[^/]*$/, '');\n        modelBase = u.origin + dir + 'vendor/mediapipe/';\n      } else {\n        // fallback: TemplateData path relative to current location\n        var basePath = location.pathname.replace(/[^/]*$/, '');\n        modelBase = location.origin + basePath + 'TemplateData/vendor/mediapipe/';\n      }\n      window.MPUBridgeConfig.modelBase = modelBase;\n      console.log('[MPUBridge] modelBase resolved to', modelBase);\n    } catch (e) { console.warn('[MPUBridge] modelBase resolution failed', e); }\n  })();\n</script>\n<script src='TemplateData/mediapipe_unity_bridge.js'></script>\n<!-- /MPUBridge auto-insert -->\n";
                    int idx = html.LastIndexOf("</body>");
                    if (idx >= 0)
                    {
                        html = html.Insert(idx, snippet);
                        File.WriteAllText(indexPath, html);
                        Debug.Log("[WebGLBuildPostprocessor] injected MPUBridge snippet into index.html");
                    }
                }
            }

        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[WebGLBuildPostprocessor] failed: " + e);
        }
    }

    static void CopyDirectory(string srcDir, string dstDir)
    {
        Directory.CreateDirectory(dstDir);
        foreach (var file in Directory.GetFiles(srcDir))
        {
            var dest = Path.Combine(dstDir, Path.GetFileName(file));
            File.Copy(file, dest, true);
        }
        foreach (var dir in Directory.GetDirectories(srcDir))
        {
            CopyDirectory(dir, Path.Combine(dstDir, Path.GetFileName(dir)));
        }
    }
}
