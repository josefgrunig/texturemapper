using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WayExperience.Editor
{
    // ── Enums ────────────────────────────────────────────────────────────────

    public enum ChannelSource
    {
        R, G, B, A,
        OneMinusR, OneMinusG, OneMinusB, OneMinusA,
        Zero, One
    }

    public enum ChannelTarget { None, R, G, B, A }

    public enum OutputTexture { BaseMap, MAHS, Normal }

    // ── Config ScriptableObject ───────────────────────────────────────────────

    [Serializable]
    public class ChannelMapping
    {
        public ChannelSource source = ChannelSource.R;
        public ChannelTarget target = ChannelTarget.R;
    }

    [Serializable]
    public class SuffixConfig
    {
        public string         suffix        = "_Suffix";
        public OutputTexture  outputTexture = OutputTexture.BaseMap;
        public bool           isNormalMap   = false;
        public List<ChannelMapping> channelMappings = new List<ChannelMapping>();

        [NonSerialized] public bool foldout;
    }

    [CreateAssetMenu(menuName = "WayExperience/Texture Mapper Config", fileName = "TextureMapperConfig")]
    public class TextureMapperConfig : ScriptableObject
    {
        public List<SuffixConfig> suffixConfigs        = new List<SuffixConfig>();
        public string             outputPrefix         = "REMAPPED_";
        public bool               deleteSourceTextures = true;
        public bool               autoAssignTextures   = true;
    }

    // ── Editor Window ─────────────────────────────────────────────────────────

    public class TextureMapperWindow : EditorWindow
    {
        const string k_ConfigPath = "Assets/Editor/WayExperience/TextureMapperConfig.asset";

        TextureMapperConfig _config;
        Vector2             _scroll;

        [MenuItem("Window/WayExperience/TextureMapper")]
        public static void Open()
        {
            var win = GetWindow<TextureMapperWindow>("Texture Mapper");
            win.minSize = new Vector2(440, 520);
        }

        void OnEnable() => LoadOrCreateConfig();

        // ── Config ────────────────────────────────────────────────────────────

        void LoadOrCreateConfig()
        {
            _config = AssetDatabase.LoadAssetAtPath<TextureMapperConfig>(k_ConfigPath);
            if (_config != null) return;

            string dir = Path.GetDirectoryName(k_ConfigPath);
            if (!AssetDatabase.IsValidFolder(dir))
                Directory.CreateDirectory(dir);

            _config = CreateInstance<TextureMapperConfig>();
            _config.suffixConfigs = BuildDefaultConfigs();
            AssetDatabase.CreateAsset(_config, k_ConfigPath);
            AssetDatabase.SaveAssets();
        }

        static List<SuffixConfig> BuildDefaultConfigs() => new List<SuffixConfig>
        {
            // Albedo / Diffuse → BaseMap RGB
            Cfg("_Diffuse", OutputTexture.BaseMap, false,
                (ChannelSource.R, ChannelTarget.R),
                (ChannelSource.G, ChannelTarget.G),
                (ChannelSource.B, ChannelTarget.B)),

            Cfg("_Albedo", OutputTexture.BaseMap, false,
                (ChannelSource.R, ChannelTarget.R),
                (ChannelSource.G, ChannelTarget.G),
                (ChannelSource.B, ChannelTarget.B)),

            // Opacity → BaseMap Alpha
            Cfg("_Opacity", OutputTexture.BaseMap, false,
                (ChannelSource.R, ChannelTarget.A)),

            // Metallic → MAHS R
            Cfg("_Metallic", OutputTexture.MAHS, false,
                (ChannelSource.R, ChannelTarget.R)),

            // Roughness inverted → MAHS Alpha (Smoothness = 1 - Roughness)
            Cfg("_Roughness", OutputTexture.MAHS, false,
                (ChannelSource.OneMinusR, ChannelTarget.A)),

            // Ambient Occlusion → MAHS Green
            Cfg("_AmbientOcclusion", OutputTexture.MAHS, false,
                (ChannelSource.R, ChannelTarget.G)),

            Cfg("_Occlusion", OutputTexture.MAHS, false,
                (ChannelSource.R, ChannelTarget.G)),

            // Normal → Normal RGB (mark as normal map)
            Cfg("_Normal", OutputTexture.Normal, true,
                (ChannelSource.R, ChannelTarget.R),
                (ChannelSource.G, ChannelTarget.G),
                (ChannelSource.B, ChannelTarget.B)),
        };

        static SuffixConfig Cfg(string suffix, OutputTexture output, bool isNormal,
            params (ChannelSource src, ChannelTarget dst)[] mappings)
        {
            var c = new SuffixConfig { suffix = suffix, outputTexture = output, isNormalMap = isNormal };
            foreach (var (src, dst) in mappings)
                c.channelMappings.Add(new ChannelMapping { source = src, target = dst });
            return c;
        }

        // ── GUI ───────────────────────────────────────────────────────────────

        void OnGUI()
        {
            if (_config == null) { LoadOrCreateConfig(); return; }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Texture Channel Mapper", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();
            _config.outputPrefix = EditorGUILayout.TextField(
                new GUIContent("Output Texture Prefix",
                    "Prepended to every generated texture name (e.g. REMAPPED_Slim_Jeans_BaseMap.png)"),
                _config.outputPrefix);

            _config.deleteSourceTextures = EditorGUILayout.Toggle(
                new GUIContent("Delete Source Textures",
                    "Remove original source textures once they have been baked into the output maps"),
                _config.deleteSourceTextures);

            _config.autoAssignTextures = EditorGUILayout.Toggle(
                new GUIContent("Auto-Assign to Materials",
                    "Automatically assign generated _BaseMap / _MAHS / _Normal textures to the URP/Lit material slots"),
                _config.autoAssignTextures);

            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(_config);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Suffix → Channel Mappings", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            DrawSuffixList();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Add Suffix"))
            {
                _config.suffixConfigs.Add(new SuffixConfig());
                EditorUtility.SetDirty(_config);
            }
            if (GUILayout.Button("Reset Defaults"))
            {
                if (EditorUtility.DisplayDialog("Reset", "Reset all suffix configs to defaults?", "Reset", "Cancel"))
                {
                    _config.suffixConfigs = BuildDefaultConfigs();
                    EditorUtility.SetDirty(_config);
                }
            }
            if (GUILayout.Button("Save Config"))
                AssetDatabase.SaveAssets();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            var selMats = Selection.objects.OfType<Material>().ToList();
            string hint = selMats.Count == 0
                ? "Select materials in the Project window to remap."
                : $"{selMats.Count} material(s) selected.";
            EditorGUILayout.HelpBox(hint, selMats.Count == 0 ? MessageType.Info : MessageType.None);

            GUI.enabled = selMats.Count > 0;
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.35f, 0.75f, 0.35f);
            if (GUILayout.Button("Remap Selected Materials", GUILayout.Height(36)))
                RemapSelectedMaterials(selMats);
            GUI.backgroundColor = prevBg;
            GUI.enabled = true;
        }

        void DrawSuffixList()
        {
            int toRemove = -1;

            for (int i = 0; i < _config.suffixConfigs.Count; i++)
            {
                var cfg = _config.suffixConfigs[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                cfg.foldout = EditorGUILayout.Foldout(cfg.foldout,
                    $"{cfg.suffix}  →  {cfg.outputTexture}", true, EditorStyles.foldoutHeader);
                if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
                    toRemove = i;
                EditorGUILayout.EndHorizontal();

                if (cfg.foldout)
                {
                    EditorGUI.indentLevel++;
                    EditorGUI.BeginChangeCheck();

                    cfg.suffix        = EditorGUILayout.TextField("Suffix",           cfg.suffix);
                    cfg.outputTexture = (OutputTexture)EditorGUILayout.EnumPopup("Output Texture", cfg.outputTexture);
                    cfg.isNormalMap   = EditorGUILayout.Toggle("Mark as Normal Map",  cfg.isNormalMap);

                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Channel Mappings", EditorStyles.miniBoldLabel);

                    int toRemoveCh = -1;
                    for (int j = 0; j < cfg.channelMappings.Count; j++)
                    {
                        var ch = cfg.channelMappings[j];
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(EditorGUI.indentLevel * 15f);
                        EditorGUILayout.LabelField("src", GUILayout.Width(26));
                        ch.source = (ChannelSource)EditorGUILayout.EnumPopup(ch.source, GUILayout.Width(115));
                        EditorGUILayout.LabelField("→", GUILayout.Width(16));
                        EditorGUILayout.LabelField("dst", GUILayout.Width(26));
                        ch.target = (ChannelTarget)EditorGUILayout.EnumPopup(ch.target, GUILayout.Width(62));
                        if (GUILayout.Button("−", GUILayout.Width(22)))
                            toRemoveCh = j;
                        EditorGUILayout.EndHorizontal();
                    }

                    if (toRemoveCh >= 0) cfg.channelMappings.RemoveAt(toRemoveCh);

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(EditorGUI.indentLevel * 15f + 4f);
                    if (GUILayout.Button("+ Add Channel Mapping", GUILayout.Height(20)))
                        cfg.channelMappings.Add(new ChannelMapping());
                    EditorGUILayout.EndHorizontal();

                    if (EditorGUI.EndChangeCheck())
                        EditorUtility.SetDirty(_config);

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            if (toRemove >= 0)
            {
                _config.suffixConfigs.RemoveAt(toRemove);
                EditorUtility.SetDirty(_config);
            }
        }

        // ── Remap ─────────────────────────────────────────────────────────────

        void RemapSelectedMaterials(List<Material> materials)
        {
            Debug.Log($"[TextureMapper] ══ Starting remap for {materials.Count} material(s) ══");

            var toDelete = new List<string>(); // paths to remove after all materials processed

            foreach (var mat in materials)
                ProcessMaterial(mat, toDelete);

            if (_config.deleteSourceTextures)
                foreach (var p in toDelete)
                {
                    AssetDatabase.DeleteAsset(p);
                    Debug.Log($"[TextureMapper] Deleted source: {p}");
                }

            AssetDatabase.Refresh();

            if (_config.autoAssignTextures)
            {
                foreach (var mat in materials)
                    AutoAssignTextures(mat);
                AssetDatabase.SaveAssets();
            }

            Debug.Log("[TextureMapper] ══ Remap complete ══");
            EditorUtility.DisplayDialog("Texture Mapper", "Remap complete. Check Console for details.", "OK");
        }

        void ProcessMaterial(Material mat, List<string> toDelete)
        {
            string matPath = AssetDatabase.GetAssetPath(mat);
            if (string.IsNullOrEmpty(matPath))
            {
                Debug.LogError($"[TextureMapper] [{mat.name}] Cannot find asset path — skipping.");
                return;
            }

            string texFolder = FindTextureFolderForMaterial(mat);
            if (string.IsNullOrEmpty(texFolder))
            {
                Debug.LogError($"[TextureMapper] [{mat.name}] Cannot locate texture folder — skipping.");
                return;
            }

            string prefix = mat.name;
            Debug.Log($"[TextureMapper] [{mat.name}] Texture folder: {texFolder}");

            // Find all textures whose filename starts with the material prefix
            var matched = new List<(SuffixConfig cfg, string path)>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { texFolder }))
            {
                string path  = AssetDatabase.GUIDToAssetPath(guid);
                string fname = Path.GetFileNameWithoutExtension(path);
                if (!fname.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

                string tail = fname.Substring(prefix.Length);
                foreach (var cfg in _config.suffixConfigs)
                {
                    if (string.Equals(tail, cfg.suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        matched.Add((cfg, path));
                        break;
                    }
                }
            }

            if (matched.Count == 0)
            {
                Debug.LogWarning($"[TextureMapper] [{mat.name}] No source textures found — skipping.");
                return;
            }

            // Determine output resolution (largest source wins)
            int outW = 4, outH = 4;
            foreach (var (_, path) in matched)
            {
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                imp.GetSourceTextureWidthAndHeight(out int w, out int h);
                if (w > outW) outW = w;
                if (h > outH) outH = h;
            }

            // Output pixel buffers with sensible defaults:
            //   BaseMap  → opaque black  (A=1)
            //   MAHS     → R=0 G=1 A=1  (no metal, full AO, full smooth)
            //   Normal   → flat (0.5, 0.5, 1.0)
            var buffers = new Dictionary<OutputTexture, Color[]>
            {
                [OutputTexture.BaseMap] = Fill(outW * outH, new Color(0f,   0f,   0f,   1f)),
                [OutputTexture.MAHS]    = Fill(outW * outH, new Color(0f,   1f,   0f,   1f)),
                [OutputTexture.Normal]  = Fill(outW * outH, new Color(0.5f, 0.5f, 1f,   1f)),
            };
            var written = new HashSet<OutputTexture>();

            // Apply each matched source to its output buffer
            foreach (var (cfg, srcPath) in matched)
            {
                Color[] srcPx = ReadPixels(srcPath, cfg.isNormalMap, outW, outH);
                if (srcPx == null)
                {
                    Debug.LogError($"[TextureMapper] [{mat.name}] Failed to read '{srcPath}' — skipping this source.");
                    continue;
                }

                Color[] dstPx = buffers[cfg.outputTexture];
                var activeMappings = cfg.channelMappings.Where(c => c.target != ChannelTarget.None).ToList();

                foreach (var ch in activeMappings)
                    BlitChannel(srcPx, dstPx, ch.source, ch.target);

                written.Add(cfg.outputTexture);
                Debug.Log($"[TextureMapper] [{mat.name}]  {Path.GetFileName(srcPath)} [{cfg.suffix}] → " +
                          $"{cfg.outputTexture}  " +
                          $"({string.Join(", ", activeMappings.Select(c => $"{c.source}→{c.target}"))})");
            }

            // Save output PNGs and collect their base names so we don't delete them
            var outputBaseNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var outType in written)
            {
                string outSuffix = outType == OutputTexture.BaseMap ? "_BaseMap"
                                 : outType == OutputTexture.MAHS    ? "_MAHS"
                                 :                                    "_Normal";

                string saved = SavePng(buffers[outType], outW, outH, texFolder,
                    _config.outputPrefix + prefix + outSuffix,
                    isNormal: outType == OutputTexture.Normal,
                    isSRGB:   outType == OutputTexture.BaseMap);

                if (saved != null)
                    outputBaseNames.Add(Path.GetFileNameWithoutExtension(saved));
            }

            // Queue sources for deletion (skip any whose base name matches an output we just wrote)
            if (_config.deleteSourceTextures)
                foreach (var (_, srcPath) in matched)
                {
                    string srcBase = Path.GetFileNameWithoutExtension(srcPath);
                    if (!outputBaseNames.Contains(srcBase))
                        toDelete.Add(srcPath);
                }
        }

        // ── Pixel helpers ─────────────────────────────────────────────────────

        Color[] ReadPixels(string assetPath, bool isSrcNormal, int targetW, int targetH)
        {
            var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (imp == null)
            {
                Debug.LogError($"[TextureMapper] No TextureImporter for '{assetPath}'.");
                return null;
            }

            // We need Read/Write access and raw (unswizzled) data
            bool wasReadable = imp.isReadable;
            var  wasType     = imp.textureType;
            bool needReimport = !wasReadable || wasType == TextureImporterType.NormalMap;

            if (needReimport)
            {
                imp.isReadable  = true;
                if (wasType == TextureImporterType.NormalMap)
                    imp.textureType = TextureImporterType.Default; // access raw RGB, not reconstructed XYZ
                imp.SaveAndReimport();
            }

            var tex    = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            var pixels = tex != null ? tex.GetPixels() : null;

            // Restore original import settings
            if (needReimport)
            {
                imp.isReadable  = wasReadable;
                imp.textureType = wasType;
                imp.SaveAndReimport();
            }

            if (pixels == null)
            {
                Debug.LogError($"[TextureMapper] GetPixels() returned null for '{assetPath}'.");
                return null;
            }

            // Resize to output resolution if needed
            if (tex.width != targetW || tex.height != targetH)
                pixels = BilinearResize(pixels, tex.width, tex.height, targetW, targetH);

            return pixels;
        }

        string SavePng(Color[] pixels, int w, int h, string folder, string name, bool isNormal, bool isSRGB)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, !isSRGB);
            tex.SetPixels(pixels);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            DestroyImmediate(tex);

            string assetPath = $"{folder}/{name}.png";
            string projectRoot = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
            string absPath = Path.Combine(projectRoot, assetPath);

            Directory.CreateDirectory(Path.GetDirectoryName(absPath));
            File.WriteAllBytes(absPath, png);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                imp.sRGBTexture = isSRGB;
                imp.isReadable  = false;
                if (!isNormal && !isSRGB) // MAHS is linear
                    imp.sRGBTexture = false;
                imp.SaveAndReimport();
            }

            Debug.Log($"[TextureMapper] Saved: {assetPath}");
            return assetPath;
        }

        static Color[] Fill(int count, Color fill)
        {
            var arr = new Color[count];
            for (int i = 0; i < count; i++) arr[i] = fill;
            return arr;
        }

        static void BlitChannel(Color[] src, Color[] dst, ChannelSource srcCh, ChannelTarget dstCh)
        {
            for (int i = 0; i < src.Length; i++)
            {
                float v = srcCh switch
                {
                    ChannelSource.R         => src[i].r,
                    ChannelSource.G         => src[i].g,
                    ChannelSource.B         => src[i].b,
                    ChannelSource.A         => src[i].a,
                    ChannelSource.OneMinusR => 1f - src[i].r,
                    ChannelSource.OneMinusG => 1f - src[i].g,
                    ChannelSource.OneMinusB => 1f - src[i].b,
                    ChannelSource.OneMinusA => 1f - src[i].a,
                    ChannelSource.Zero      => 0f,
                    ChannelSource.One       => 1f,
                    _                       => 0f,
                };
                switch (dstCh)
                {
                    case ChannelTarget.R: dst[i].r = v; break;
                    case ChannelTarget.G: dst[i].g = v; break;
                    case ChannelTarget.B: dst[i].b = v; break;
                    case ChannelTarget.A: dst[i].a = v; break;
                }
            }
        }

        static Color[] BilinearResize(Color[] src, int sw, int sh, int dw, int dh)
        {
            var dst   = new Color[dw * dh];
            float scX = (float)sw / dw;
            float scY = (float)sh / dh;
            for (int y = 0; y < dh; y++)
            for (int x = 0; x < dw; x++)
            {
                float sx = (x + 0.5f) * scX - 0.5f;
                float sy = (y + 0.5f) * scY - 0.5f;
                int x0 = Mathf.Clamp(Mathf.FloorToInt(sx), 0, sw - 1);
                int y0 = Mathf.Clamp(Mathf.FloorToInt(sy), 0, sh - 1);
                int x1 = Mathf.Min(x0 + 1, sw - 1);
                int y1 = Mathf.Min(y0 + 1, sh - 1);
                float tx = sx - x0, ty = sy - y0;
                dst[y * dw + x] = Color.Lerp(
                    Color.Lerp(src[y0 * sw + x0], src[y0 * sw + x1], tx),
                    Color.Lerp(src[y1 * sw + x0], src[y1 * sw + x1], tx), ty);
            }
            return dst;
        }

        // ── Auto-assign ───────────────────────────────────────────────────────

        void AutoAssignTextures(Material mat)
        {
            string folder = FindTextureFolderForMaterial(mat);
            if (folder == null)
            {
                Debug.LogWarning($"[TextureMapper] [{mat.name}] Cannot locate texture folder for auto-assign.");
                return;
            }

            string p = _config.outputPrefix + mat.name;

            AssignSlot(mat, "_BaseMap",          folder, p + "_BaseMap");
            AssignSlot(mat, "_MetallicGlossMap", folder, p + "_MAHS");
            AssignSlot(mat, "_OcclusionMap",     folder, p + "_MAHS");
            AssignSlot(mat, "_BumpMap",          folder, p + "_Normal");

            EditorUtility.SetDirty(mat);
        }

        void AssignSlot(Material mat, string shaderProp, string folder, string nameNoExt)
        {
            string path = FindTexturePath(folder, nameNoExt);
            if (path == null) return;

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) return;

            mat.SetTexture(shaderProp, tex);
            Debug.Log($"[TextureMapper] [{mat.name}] {shaderProp} ← {path}");
        }

        // ── Path helpers ──────────────────────────────────────────────────────

        string FindTextureFolderForMaterial(Material mat)
        {
            string matPath = AssetDatabase.GetAssetPath(mat);
            string matDir  = Path.GetDirectoryName(matPath)?.Replace('\\', '/') ?? "";

            // Common: sibling "textures" folder (handles /materials → /textures)
            string[] candidates =
            {
                matDir.Replace("/materials", "/textures"),
                matDir.Replace("/Materials", "/textures"),
                Path.GetFullPath(matDir + "/../textures").Replace('\\', '/'),
                matDir,
            };

            string projectRoot = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length)
                                              .Replace('\\', '/');

            foreach (var candidate in candidates)
            {
                string rel = candidate.StartsWith(projectRoot)
                    ? candidate.Substring(projectRoot.Length)
                    : candidate;
                if (AssetDatabase.IsValidFolder(rel)) return rel;
            }

            // Fallback: folder of any texture already on the material
            int count = ShaderUtil.GetPropertyCount(mat.shader);
            for (int i = 0; i < count; i++)
            {
                if (ShaderUtil.GetPropertyType(mat.shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                    continue;
                var tex = mat.GetTexture(ShaderUtil.GetPropertyName(mat.shader, i)) as Texture2D;
                if (tex == null) continue;
                return Path.GetDirectoryName(AssetDatabase.GetAssetPath(tex))?.Replace('\\', '/');
            }

            return null;
        }

        string FindTexturePath(string folder, string nameNoExt)
        {
            foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".tga", ".tif", ".psd" })
            {
                string p = $"{folder}/{nameNoExt}{ext}";
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(p) != null) return p;
            }
            return null;
        }
    }
}
