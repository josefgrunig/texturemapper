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

    // ── Config ───────────────────────────────────────────────────────────────
    // Plain serializable classes saved as JSON — avoids Unity script-GUID issues
    // when the code lives in a package rather than the project's Assets folder.

    [Serializable]
    public class ChannelMapping
    {
        public ChannelSource source = ChannelSource.R;
        public ChannelTarget target = ChannelTarget.R;
    }

    [Serializable]
    public class SuffixConfig
    {
        public string               suffix          = "_Suffix";
        public OutputTexture        outputTexture   = OutputTexture.BaseMap;
        public bool                 isNormalMap     = false;
        public List<ChannelMapping> channelMappings = new List<ChannelMapping>();

        [NonSerialized] public bool foldout;
    }

    [Serializable]
    public class ShaderTextureMapping
    {
        public OutputTexture outputTexture  = OutputTexture.BaseMap;
        public string        shaderProperty = "_BaseMap";
    }

    [Serializable]
    public class ShaderAssignConfig
    {
        public bool   enabled    = true;
        public string shaderName = "Universal Render Pipeline/Lit";
        public List<ShaderTextureMapping> textureMappings = new List<ShaderTextureMapping>();
    }

    [Serializable]
    public class TextureMapperConfig
    {
        public List<SuffixConfig> suffixConfigs        = new List<SuffixConfig>();
        public string             outputPrefix         = "REMAPPED_";
        public bool               deleteSourceTextures = true;
        public ShaderAssignConfig shaderAssign         = new ShaderAssignConfig();
    }

    // ── Editor Window ─────────────────────────────────────────────────────────

    public class TextureMapperWindow : EditorWindow
    {
        // JSON file in the project (not the package) — immune to script-GUID changes
        const string k_ConfigAssetPath = "Assets/Editor/WayExperience/TextureMapperConfig.json";

        // Known shader presets: shader name → default slot mappings
        static readonly Dictionary<string, List<ShaderTextureMapping>> k_ShaderPresets =
            new Dictionary<string, List<ShaderTextureMapping>>
        {
            ["Universal Render Pipeline/Lit"] = new List<ShaderTextureMapping>
            {
                new ShaderTextureMapping { outputTexture = OutputTexture.BaseMap, shaderProperty = "_BaseMap" },
                new ShaderTextureMapping { outputTexture = OutputTexture.MAHS,    shaderProperty = "_MetallicGlossMap" },
                new ShaderTextureMapping { outputTexture = OutputTexture.MAHS,    shaderProperty = "_OcclusionMap" },
                new ShaderTextureMapping { outputTexture = OutputTexture.Normal,  shaderProperty = "_BumpMap" },
            },
            ["Shader Graphs/DefaultMasterShader"] = new List<ShaderTextureMapping>
            {
                new ShaderTextureMapping { outputTexture = OutputTexture.BaseMap, shaderProperty = "_BaseMap" },
                new ShaderTextureMapping { outputTexture = OutputTexture.MAHS,    shaderProperty = "_MAHS" },
                new ShaderTextureMapping { outputTexture = OutputTexture.Normal,  shaderProperty = "_BumpMap" },
            },
        };

        TextureMapperConfig _config;
        Vector2             _scroll;
        Shader              _shaderObj;   // transient — not serialized, resolved from shaderName

        [MenuItem("Window/WayExperience/TextureMapper")]
        public static void Open()
        {
            var win = GetWindow<TextureMapperWindow>("Texture Mapper");
            win.minSize = new Vector2(460, 560);
        }

        void OnEnable()
        {
            LoadOrCreateConfig();
            SyncShaderObj();
        }

        // ── Config ────────────────────────────────────────────────────────────

        string ConfigAbsPath()
        {
            string root = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
            return Path.Combine(root, k_ConfigAssetPath).Replace('\\', '/');
        }

        void LoadOrCreateConfig()
        {
            string absPath = ConfigAbsPath();
            if (File.Exists(absPath))
            {
                try
                {
                    _config = JsonUtility.FromJson<TextureMapperConfig>(File.ReadAllText(absPath));
                    if (_config != null)
                    {
                        // Ensure nested objects are never null after deserialization
                        if (_config.shaderAssign == null)
                            _config.shaderAssign = BuildDefaultShaderAssign();
                        return;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[TextureMapper] Could not parse config — resetting to defaults. ({e.Message})");
                }
            }

            _config = new TextureMapperConfig
            {
                suffixConfigs = BuildDefaultSuffixConfigs(),
                shaderAssign  = BuildDefaultShaderAssign(),
            };
            SaveConfig();
        }

        void SaveConfig()
        {
            string absPath = ConfigAbsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(absPath));
            File.WriteAllText(absPath, JsonUtility.ToJson(_config, prettyPrint: true));
        }

        // Resolve shader name → Shader object (for the ObjectField)
        void SyncShaderObj()
        {
            string name = _config?.shaderAssign?.shaderName ?? "";
            _shaderObj = string.IsNullOrEmpty(name) ? null : Shader.Find(name);
        }

        // ── Default builders ──────────────────────────────────────────────────

        static List<SuffixConfig> BuildDefaultSuffixConfigs() => new List<SuffixConfig>
        {
            Sfx("_Diffuse", OutputTexture.BaseMap, false,
                (ChannelSource.R, ChannelTarget.R),
                (ChannelSource.G, ChannelTarget.G),
                (ChannelSource.B, ChannelTarget.B)),

            Sfx("_Albedo", OutputTexture.BaseMap, false,
                (ChannelSource.R, ChannelTarget.R),
                (ChannelSource.G, ChannelTarget.G),
                (ChannelSource.B, ChannelTarget.B)),

            Sfx("_Opacity", OutputTexture.BaseMap, false,
                (ChannelSource.R, ChannelTarget.A)),

            Sfx("_Metallic", OutputTexture.MAHS, false,
                (ChannelSource.R, ChannelTarget.R)),

            Sfx("_Roughness", OutputTexture.MAHS, false,
                (ChannelSource.OneMinusR, ChannelTarget.A)),

            Sfx("_AmbientOcclusion", OutputTexture.MAHS, false,
                (ChannelSource.R, ChannelTarget.G)),

            Sfx("_Occlusion", OutputTexture.MAHS, false,
                (ChannelSource.R, ChannelTarget.G)),

            Sfx("_Normal", OutputTexture.Normal, true,
                (ChannelSource.R, ChannelTarget.R),
                (ChannelSource.G, ChannelTarget.G),
                (ChannelSource.B, ChannelTarget.B)),
        };

        static ShaderAssignConfig BuildDefaultShaderAssign()
        {
            const string defaultShader = "Universal Render Pipeline/Lit";
            return new ShaderAssignConfig
            {
                enabled        = true,
                shaderName     = defaultShader,
                textureMappings = ClonePreset(defaultShader),
            };
        }

        static List<ShaderTextureMapping> ClonePreset(string shaderName)
        {
            if (!k_ShaderPresets.TryGetValue(shaderName, out var src))
                return new List<ShaderTextureMapping>();
            return src.Select(m => new ShaderTextureMapping
                { outputTexture = m.outputTexture, shaderProperty = m.shaderProperty }).ToList();
        }

        static SuffixConfig Sfx(string suffix, OutputTexture output, bool isNormal,
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
            if (_config == null) { LoadOrCreateConfig(); SyncShaderObj(); return; }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Texture Channel Mapper", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Top-level settings (outside scroll)
            EditorGUI.BeginChangeCheck();
            _config.outputPrefix = EditorGUILayout.TextField(
                new GUIContent("Output Texture Prefix",
                    "Prepended to every generated texture name (e.g. REMAPPED_Slim_Jeans_BaseMap.png)"),
                _config.outputPrefix);
            _config.deleteSourceTextures = EditorGUILayout.Toggle(
                new GUIContent("Delete Source Textures",
                    "Remove original source textures once they have been baked into the output maps"),
                _config.deleteSourceTextures);
            if (EditorGUI.EndChangeCheck())
                SaveConfig();

            EditorGUILayout.Space(6);

            // Scrollable content: both sections
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

            DrawSuffixSection();
            EditorGUILayout.Space(10);
            DrawShaderAssignSection();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6);

            // Gather materials from Project selection and from scene MeshRenderers
            var selMats = Selection.objects.OfType<Material>().ToList();

            var selRenderers = Selection.gameObjects
                .Select(go => go.GetComponent<Renderer>())
                .Where(r => r != null)
                .ToList();

            var rendererMats = selRenderers
                .SelectMany(r => r.sharedMaterials)
                .Where(m => m != null)
                .Distinct()
                .Except(selMats)
                .ToList();

            var allMats = selMats.Concat(rendererMats).ToList();

            string buttonLabel;
            string hint;
            bool hasRenderers = selRenderers.Count > 0;

            if (allMats.Count == 0)
            {
                hint        = "Select materials in the Project window or Renderers in the scene.";
                buttonLabel = "Remap Selected Materials";
            }
            else if (hasRenderers)
            {
                buttonLabel = "Remap Selected Renderers' Materials";
                hint        = $"{allMats.Count} material(s) on {selRenderers.Count} Renderer(s)";
            }
            else
            {
                buttonLabel = "Remap Selected Materials";
                hint        = $"{selMats.Count} material(s) selected.";
            }

            EditorGUILayout.HelpBox(hint, allMats.Count == 0 ? MessageType.Info : MessageType.None);

            GUI.enabled = allMats.Count > 0;
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.35f, 0.75f, 0.35f);
            if (GUILayout.Button(buttonLabel, GUILayout.Height(36)))
                RemapSelectedMaterials(allMats);
            GUI.backgroundColor = prevBg;
            GUI.enabled = true;

            EditorGUILayout.Space(4);

            // Save / Reset — pinned to bottom
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Defaults"))
            {
                if (EditorUtility.DisplayDialog("Reset", "Reset all settings to defaults?", "Reset", "Cancel"))
                {
                    _config.suffixConfigs = BuildDefaultSuffixConfigs();
                    _config.shaderAssign  = BuildDefaultShaderAssign();
                    SaveConfig();
                    SyncShaderObj();
                }
            }
            if (GUILayout.Button("Save Config"))
                SaveConfig();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
        }

        // ── Suffix section ────────────────────────────────────────────────────

        void DrawSuffixSection()
        {
            EditorGUILayout.LabelField("Suffix → Channel Mappings", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

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

                    if (toRemoveCh >= 0)
                    {
                        cfg.channelMappings.RemoveAt(toRemoveCh);
                        SaveConfig();
                    }

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(EditorGUI.indentLevel * 15f + 4f);
                    if (GUILayout.Button("+ Add Channel Mapping", GUILayout.Height(20)))
                    {
                        cfg.channelMappings.Add(new ChannelMapping());
                        SaveConfig();
                    }
                    EditorGUILayout.EndHorizontal();

                    if (EditorGUI.EndChangeCheck())
                        SaveConfig();

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            if (toRemove >= 0)
            {
                _config.suffixConfigs.RemoveAt(toRemove);
                SaveConfig();
            }

            EditorGUILayout.Space(2);
            if (GUILayout.Button("+ Add Suffix"))
            {
                _config.suffixConfigs.Add(new SuffixConfig());
                SaveConfig();
            }
        }

        // ── Shader assign section ─────────────────────────────────────────────

        void DrawShaderAssignSection()
        {
            var sa = _config.shaderAssign;

            EditorGUILayout.LabelField("Auto-Assign to Materials", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            sa.enabled = EditorGUILayout.Toggle(
                new GUIContent("Enabled", "Assign generated textures to material shader slots after remapping"),
                sa.enabled);
            if (EditorGUI.EndChangeCheck())
                SaveConfig();

            GUI.enabled = sa.enabled;

            // Shader row: ObjectField + Preset dropdown
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            var newShader = (Shader)EditorGUILayout.ObjectField(
                new GUIContent("Shader", "Target shader. Use the Preset button to apply a known mapping template."),
                _shaderObj, typeof(Shader), false);
            if (EditorGUI.EndChangeCheck() && newShader != _shaderObj)
            {
                _shaderObj    = newShader;
                sa.shaderName = newShader != null ? newShader.name : "";
                if (newShader != null && k_ShaderPresets.ContainsKey(newShader.name))
                {
                    sa.textureMappings = ClonePreset(newShader.name);
                    Debug.Log($"[TextureMapper] Shader preset applied: {newShader.name}");
                }
                SaveConfig();
            }

            // Preset dropdown — works even when the shader is not imported in this project
            if (GUILayout.Button("Preset ▾", GUILayout.Width(70)))
            {
                var menu = new GenericMenu();
                foreach (var presetName in k_ShaderPresets.Keys)
                {
                    string captured = presetName;
                    menu.AddItem(new GUIContent(captured), sa.shaderName == captured, () =>
                    {
                        sa.shaderName      = captured;
                        sa.textureMappings = ClonePreset(captured);
                        _shaderObj         = Shader.Find(captured); // may be null if not in project
                        SaveConfig();
                        Debug.Log($"[TextureMapper] Shader preset applied: {captured}");
                        Repaint();
                    });
                }
                menu.ShowAsContext();
            }
            EditorGUILayout.EndHorizontal();

            // Show shader name as read-only text when ObjectField is null (shader not in project)
            if (_shaderObj == null && !string.IsNullOrEmpty(sa.shaderName))
                EditorGUILayout.HelpBox($"Shader \"{sa.shaderName}\" not found in project — mappings still apply at runtime.", MessageType.Info);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Texture → Shader Property", EditorStyles.miniBoldLabel);

            int toRemove = -1;
            for (int i = 0; i < sa.textureMappings.Count; i++)
            {
                var m = sa.textureMappings[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                m.outputTexture  = (OutputTexture)EditorGUILayout.EnumPopup(m.outputTexture, GUILayout.Width(90));
                EditorGUILayout.LabelField("→", GUILayout.Width(16));
                m.shaderProperty = EditorGUILayout.TextField(m.shaderProperty);
                if (EditorGUI.EndChangeCheck())
                    SaveConfig();
                if (GUILayout.Button("−", GUILayout.Width(22)))
                    toRemove = i;
                EditorGUILayout.EndHorizontal();
            }

            if (toRemove >= 0)
            {
                sa.textureMappings.RemoveAt(toRemove);
                SaveConfig();
            }

            EditorGUILayout.Space(2);
            if (GUILayout.Button("+ Add Shader Mapping"))
            {
                sa.textureMappings.Add(new ShaderTextureMapping());
                SaveConfig();
            }

            GUI.enabled = true;
            EditorGUILayout.EndVertical();
        }

        // ── Remap ─────────────────────────────────────────────────────────────

        void RemapSelectedMaterials(List<Material> materials)
        {
            Debug.Log($"[TextureMapper] ══ Starting remap for {materials.Count} material(s) ══");

            var toDelete   = new List<string>();
            var outputMaps = new Dictionary<Material, Dictionary<OutputTexture, string>>();

            foreach (var mat in materials)
            {
                var paths = ProcessMaterial(mat, toDelete);
                if (paths != null) outputMaps[mat] = paths;
            }

            if (_config.deleteSourceTextures)
                foreach (var p in toDelete)
                {
                    AssetDatabase.DeleteAsset(p);
                    Debug.Log($"[TextureMapper] Deleted source: {p}");
                }

            AssetDatabase.Refresh();

            if (_config.shaderAssign.enabled && _config.shaderAssign.textureMappings.Count > 0)
            {
                foreach (var mat in materials)
                {
                    outputMaps.TryGetValue(mat, out var paths);
                    AutoAssignTextures(mat, paths);
                }
                AssetDatabase.SaveAssets();
            }

            Debug.Log("[TextureMapper] ══ Remap complete ══");
            EditorUtility.DisplayDialog("Texture Mapper", "Remap complete. Check Console for details.", "OK");
        }

        // Returns asset paths of saved output textures keyed by OutputTexture type, or null on failure.
        Dictionary<OutputTexture, string> ProcessMaterial(Material mat, List<string> toDelete)
        {
            string matPath = AssetDatabase.GetAssetPath(mat);
            if (string.IsNullOrEmpty(matPath))
            {
                Debug.LogError($"[TextureMapper] [{mat.name}] Cannot find asset path — skipping.");
                return null;
            }

            string texFolder = FindTextureFolderForMaterial(mat);
            if (string.IsNullOrEmpty(texFolder))
            {
                Debug.LogError($"[TextureMapper] [{mat.name}] Cannot locate texture folder — skipping.");
                return null;
            }

            string prefix = mat.name;
            Debug.Log($"[TextureMapper] [{mat.name}] Texture folder: {texFolder}");

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
                Debug.Log($"[TextureMapper] [{mat.name}] No source textures to remap — will look for existing outputs.");

            int outW = 4, outH = 4;
            foreach (var (_, path) in matched)
            {
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                imp.GetSourceTextureWidthAndHeight(out int w, out int h);
                if (w > outW) outW = w;
                if (h > outH) outH = h;
            }

            var buffers = new Dictionary<OutputTexture, Color[]>
            {
                [OutputTexture.BaseMap] = Fill(outW * outH, new Color(0f,   0f,   0f,   1f)),
                [OutputTexture.MAHS]    = Fill(outW * outH, new Color(0f,   1f,   0f,   1f)),
                [OutputTexture.Normal]  = Fill(outW * outH, new Color(0.5f, 0.5f, 1f,   1f)),
            };
            var written       = new HashSet<OutputTexture>();
            // First source folder that writes to each output type — used as that output's save location.
            var outputFolders = new Dictionary<OutputTexture, string>();

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
                if (!outputFolders.ContainsKey(cfg.outputTexture))
                    outputFolders[cfg.outputTexture] = Path.GetDirectoryName(srcPath)?.Replace('\\', '/') ?? texFolder;

                Debug.Log($"[TextureMapper] [{mat.name}]  {Path.GetFileName(srcPath)} [{cfg.suffix}] → " +
                          $"{cfg.outputTexture}  " +
                          $"({string.Join(", ", activeMappings.Select(c => $"{c.source}→{c.target}"))})");
            }

            var outputPaths    = new Dictionary<OutputTexture, string>();
            var outputBaseNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var outType in written)
            {
                string outSuffix  = outType == OutputTexture.BaseMap ? "_BaseMap"
                                  : outType == OutputTexture.MAHS    ? "_MAHS"
                                  :                                    "_Normal";
                string saveFolder = outputFolders.TryGetValue(outType, out var f) ? f : texFolder;

                string saved = SavePng(buffers[outType], outW, outH, saveFolder,
                    _config.outputPrefix + prefix + outSuffix,
                    isNormal: outType == OutputTexture.Normal,
                    isSRGB:   outType == OutputTexture.BaseMap);

                if (saved != null)
                {
                    outputPaths[outType] = saved;
                    outputBaseNames.Add(Path.GetFileNameWithoutExtension(saved));
                }
            }

            if (_config.deleteSourceTextures)
                foreach (var (_, srcPath) in matched)
                {
                    string srcBase = Path.GetFileNameWithoutExtension(srcPath);
                    if (!outputBaseNames.Contains(srcBase))
                        toDelete.Add(srcPath);
                }

            // Fill in any output types that weren't generated this run but already exist on disk.
            foreach (OutputTexture outType in Enum.GetValues(typeof(OutputTexture)))
            {
                if (outputPaths.ContainsKey(outType)) continue;

                string outSuffix = outType == OutputTexture.BaseMap ? "_BaseMap"
                                 : outType == OutputTexture.MAHS    ? "_MAHS"
                                 :                                    "_Normal";

                // Check prefixed name first, then bare material name.
                string found = FindExistingOutputTexture(texFolder, _config.outputPrefix + prefix + outSuffix)
                            ?? FindExistingOutputTexture(texFolder, prefix + outSuffix);
                if (found != null)
                {
                    outputPaths[outType] = found;
                    Debug.Log($"[TextureMapper] [{mat.name}] Found existing {outType}: {found}");
                }
            }

            return outputPaths;
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

            bool wasReadable  = imp.isReadable;
            var  wasType      = imp.textureType;
            bool needReimport = !wasReadable || wasType == TextureImporterType.NormalMap;

            if (needReimport)
            {
                imp.isReadable  = true;
                if (wasType == TextureImporterType.NormalMap)
                    imp.textureType = TextureImporterType.Default;
                imp.SaveAndReimport();
            }

            var tex    = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            var pixels = tex != null ? tex.GetPixels() : null;

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

            string assetPath  = $"{folder}/{name}.png";
            string projectRoot = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
            string absPath    = Path.Combine(projectRoot, assetPath);

            Directory.CreateDirectory(Path.GetDirectoryName(absPath));
            File.WriteAllBytes(absPath, png);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                imp.sRGBTexture = isSRGB;
                imp.isReadable  = false;
                if (!isNormal && !isSRGB)
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

        string FindExistingOutputTexture(string preferredFolder, string nameNoExt)
        {
            // 1. Exact path in the expected folder.
            if (!string.IsNullOrEmpty(preferredFolder))
            {
                foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".tga", ".tif", ".psd" })
                {
                    string p = $"{preferredFolder}/{nameNoExt}{ext}";
                    if (AssetDatabase.LoadAssetAtPath<Texture2D>(p) != null) return p;
                }
            }

            // 2. Recursive search within the parent folder (covers sibling sub-folders like textures/, materials/).
            string parentFolder = string.IsNullOrEmpty(preferredFolder)
                ? null
                : Path.GetDirectoryName(preferredFolder)?.Replace('\\', '/');

            if (!string.IsNullOrEmpty(parentFolder) && AssetDatabase.IsValidFolder(parentFolder))
            {
                foreach (var guid in AssetDatabase.FindAssets($"t:Texture2D {nameNoExt}", new[] { parentFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.Equals(Path.GetFileNameWithoutExtension(path), nameNoExt, StringComparison.OrdinalIgnoreCase))
                        return path;
                }
            }

            // 3. Project-wide search as last resort.
            foreach (var guid in AssetDatabase.FindAssets($"t:Texture2D {nameNoExt}"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(Path.GetFileNameWithoutExtension(path), nameNoExt, StringComparison.OrdinalIgnoreCase))
                    return path;
            }

            return null;
        }

        // ── Auto-assign ───────────────────────────────────────────────────────

        void AutoAssignTextures(Material mat, Dictionary<OutputTexture, string> outputPaths)
        {
            // Assign shader first so property names are valid before setting textures
            if (!string.IsNullOrEmpty(_config.shaderAssign.shaderName))
            {
                var shader = Shader.Find(_config.shaderAssign.shaderName);
                if (shader != null)
                {
                    mat.shader = shader;
                    Debug.Log($"[TextureMapper] [{mat.name}] Shader ← {_config.shaderAssign.shaderName}");
                }
                else
                {
                    Debug.LogWarning($"[TextureMapper] [{mat.name}] Shader \"{_config.shaderAssign.shaderName}\" not found — shader not changed.");
                }
            }

            if (outputPaths == null || outputPaths.Count == 0)
            {
                Debug.LogWarning($"[TextureMapper] [{mat.name}] No output textures to assign.");
                EditorUtility.SetDirty(mat);
                return;
            }

            foreach (var m in _config.shaderAssign.textureMappings)
            {
                if (string.IsNullOrEmpty(m.shaderProperty)) continue;
                if (!outputPaths.TryGetValue(m.outputTexture, out var path)) continue;

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;

                mat.SetTexture(m.shaderProperty, tex);
                Debug.Log($"[TextureMapper] [{mat.name}] {m.shaderProperty} ← {path}");
            }

            EditorUtility.SetDirty(mat);
        }

        // ── Path helpers ──────────────────────────────────────────────────────

        string FindTextureFolderForMaterial(Material mat)
        {
            string matPath = AssetDatabase.GetAssetPath(mat);
            string matDir  = Path.GetDirectoryName(matPath)?.Replace('\\', '/') ?? "";

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

    }
}
