using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using GLTFast.Logging;
using System.Collections.Generic;
using System.Text;
using GLTFast.Export;
#if UNITY_MATH_1_3_OR_NEWER
    using Unity.Mathematics;
#endif
using System.Reflection;
using UnityEngine.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using System.Globalization;

#if UNITY_SHADER_GRAPH
    using UnityEditor.ShaderGraph.Internal;
#endif
#if USING_URP
    using UnityEngine.Rendering.Universal;
#endif
#if USING_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

public class UnityToGodot : EditorWindow
{

#if UNITY_EDITOR

    public static string Version = "1.03";
    [MenuItem("UnityToGodot/Export...")]
    static public void ShowWindow()
    {
        string Title = "UnityToGodot " + Version;
        EditorWindow window = EditorWindow.GetWindow(typeof(UnityToGodot), true, Title);

        Rect NewPosition = new Rect(new Vector2(300, 400), new Vector2(400, 365));

        window.position = NewPosition;
    }
    public static string ExportFolder = GetDefaultOutputFolder();
    static bool ExportTerrain = true;
    static bool TerrainRescale = true;
    static int MaxTerrainTriangles = 1000000;
    static bool ExportTerrainTrees = true;
    static bool ExportTerrainDetails = true;
    static bool MergeDetailPlanes = true;
    static float DetailDensityScale = 1.0f;
    static bool ConvertSkinnedToStatic = true;
    static bool ExportVertexShaders = true;
    static bool EmbedTexturesInGLB = false;
    static bool RemoveEmptyNodes = true;
    static float EmissionMultiplier = 100000;

#if USING_HDRP
    static float PointLightIntensityMultiplier = 1;
    static float SpotLightIntensityMultiplier = 1;
    static float DirectionalLightIntensityMultiplier = 1;//10000;
#else
    static float PointLightIntensityMultiplier = 10000;
    static float SpotLightIntensityMultiplier = 5000;
    static float DirectionalLightIntensityMultiplier = 10000;
#endif
    public static string GetDefaultOutputFolder()
    {
        RuntimePlatform platform = Application.platform;
        #if UNITY_EDITOR_WIN
            return "C:\\UnityToGodot";
        #elif UNITY_STANDALONE_OSX
            return "/Users/Shared/UnityToGodot";
        #else//UNITY_STANDALONE_LINUX
            return "~/.local/share/UnityToGodot";
        #endif
    }
    List<string> ScenePaths = new List<string>();
    public void OnGUI()
    {
        var SavedExportFolder = EditorUserSettings.GetConfigValue("UTG.ExportFolder");
        if (SavedExportFolder == null || SavedExportFolder.Length == 0)
            SavedExportFolder = GetDefaultOutputFolder();
        
        EditorGUILayout.BeginHorizontal();

        ExportFolder = EditorGUILayout.TextField("ExportFolder", SavedExportFolder);

        // Browse button
        if (GUILayout.Button("Browse...", GUILayout.MaxWidth(80)))
        {
            string selected = EditorUtility.OpenFolderPanel("Select folder", ExportFolder, "");
            if (!string.IsNullOrEmpty(selected))
            {
                ExportFolder = selected;
                GUI.FocusControl(null); // unfocus text field to update immediately
            }
        }

        EditorGUILayout.EndHorizontal();
        
        ExportTerrain = EditorGUILayout.Toggle("ExportTerrain", ExportTerrain);
        TerrainRescale = EditorGUILayout.Toggle("RescaleTerrainHeightmap", TerrainRescale);
        MaxTerrainTriangles = EditorGUILayout.IntField("MaxTerrainTriangles", MaxTerrainTriangles);
        ExportTerrainTrees = EditorGUILayout.Toggle("ExportTerrainTrees", ExportTerrainTrees);
        ExportTerrainDetails = EditorGUILayout.Toggle("ExportTerrainDetails", ExportTerrainDetails);
        MergeDetailPlanes = EditorGUILayout.Toggle("MergeDetailPlanes", MergeDetailPlanes);
        DetailDensityScale = EditorGUILayout.FloatField("DetailDensityScale", DetailDensityScale);
        ConvertSkinnedToStatic = EditorGUILayout.Toggle("ConvertSkinnedToStatic", ConvertSkinnedToStatic);
        ExportVertexShaders = EditorGUILayout.Toggle("ExportVertexShaders", ExportVertexShaders);
        EmbedTexturesInGLB = EditorGUILayout.Toggle("EmbedTexturesInGLB", EmbedTexturesInGLB);
        RemoveEmptyNodes = EditorGUILayout.Toggle("RemoveEmptyNodes", RemoveEmptyNodes);
        
        EmissionMultiplier = EditorGUILayout.FloatField("EmissionMultiplier", EmissionMultiplier);

        PointLightIntensityMultiplier = EditorGUILayout.FloatField("PointLightMultiplier", PointLightIntensityMultiplier);
        SpotLightIntensityMultiplier = EditorGUILayout.FloatField("SpotLightMultiplier", SpotLightIntensityMultiplier);
        DirectionalLightIntensityMultiplier = EditorGUILayout.FloatField("DirectionalLightMultiplier", DirectionalLightIntensityMultiplier);        

        bool ExportCurrentScene = GUILayout.Button("ExportCurrentScene");
        bool ExportSelectedScenes = GUILayout.Button("ExportSelectedScenes");
        if (ExportCurrentScene || ExportSelectedScenes )
        {
            ScenePaths.Clear();

            if ( ExportSelectedScenes )
            {
                var SelectedAssets = GetSelectedAssets();
                foreach (UnityEngine.Object obj in SelectedAssets)
                {
                    SceneAsset scene = obj as SceneAsset;
                    if ( scene != null )
                    {
                        string path = AssetDatabase.GetAssetPath(obj);
                        if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        {
                            ScenePaths.Add( path );
                        }
                    }
                }

                //UnloadCurrentScenes();
            }
            else
            {
                Scene scene = SceneManager.GetActiveScene();
                ScenePaths.Add( scene.path );
            }

            IterateThroughScenes( ExportCurrentScene, ExportSelectedScenes, 0 );
        }

        EditorUserSettings.SetConfigValue("UTG.ExportFolder", ExportFolder);
    }
    public void IterateThroughScenes( bool ExportCurrentScene, bool ExportSelectedScenes, int SceneIndex )
    {
        if ( SceneIndex >= ScenePaths.Count )
            return;

        Scene CurrentScene;
        if ( ExportSelectedScenes )
        {
            try
            {
                CurrentScene = EditorSceneManager.OpenScene( ScenePaths[ SceneIndex ], OpenSceneMode.Single );
            }
            catch (Exception E)
            {
                Debug.LogError(E.Message + "\n" + E.StackTrace );
            }
        }
        else
        {
             CurrentScene = SceneManager.GetActiveScene();
        }
        if (ExportTerrain)
        {
            TerrainTools.DoRescale = TerrainRescale;
            TerrainTools.MaxTriangles = MaxTerrainTriangles;
            TerrainTools.ExportTextures = true;
            TerrainTools.ExportGeometry = true;
            TerrainTools.DetailDensityScale = DetailDensityScale;
            TerrainTools.ConvertTerrainToStaticMesh();
        }
        if (ExportTerrainTrees)
        {
            TerrainTools.DoExtractTreesWOptions();
        }
        if (ExportTerrainDetails)
        {
            TerrainTools.DoExtractDetails( MergeDetailPlanes );
        }

        try
        {
            ExporterMain( SceneIndex == ScenePaths.Count - 1 ).ContinueWith( task =>
                {
                    if (ExportSelectedScenes)
                        IterateThroughScenes( ExportCurrentScene, ExportSelectedScenes, SceneIndex + 1 );
                }
                , TaskScheduler.FromCurrentSynchronizationContext());
        }
        catch (Exception E)
        {
            Debug.LogError(E.Message + "\n" + E.StackTrace );
        }            
    }
    static GLTFast.Export.ExportSettings GetDefaultSettings(bool binary)
    {
        var settings = new ExportSettings
        {
            Format = binary ? GltfFormat.Binary : GltfFormat.Json
        };
        return settings;
    }
    static string GetAssetNameFromPath(string AssetPath, bool DoRemoveExtension = true)
    {
        int LastSlash = AssetPath.LastIndexOf("\\");
        if (LastSlash == -1)
            LastSlash = AssetPath.LastIndexOf("/");

        if (LastSlash != -1)
        {
            string Name = AssetPath.Substring(LastSlash + 1, AssetPath.Length - LastSlash - 1);
            if (DoRemoveExtension)
                Name = RemoveExtension(Name);
            return Name;
        }
        else
            return null;
    }
    static string GetGodotPathFromUnityPath(string AssetPath)
    {
        if (AssetPath.StartsWith("Assets"))
        {
            AssetPath = AssetPath.Substring("Assets".Length + 1);
        }
        int LastSlash = AssetPath.LastIndexOf("\\");
        if (LastSlash == -1)
            LastSlash = AssetPath.LastIndexOf("/");

        if (LastSlash != -1)
        {
            string Name = AssetPath.Substring(0, LastSlash + 1);
            return Name;
        }
        else
            return null;
    }
    static string GetFolderFromFilePath(string AssetPath)
    {
        int LastSlash = AssetPath.LastIndexOf("\\");
        int LastSlash2 = AssetPath.LastIndexOf("/");
        if (LastSlash == -1 || LastSlash2 > LastSlash)
            LastSlash = LastSlash2;

        if (LastSlash != -1)
        {
            string Name = AssetPath.Substring(0, LastSlash + 1);
            return Name;
        }
        else
            return null;
    }
    const uint char_count = ('z' - 'a');
    const uint base_ = char_count + ('9' - '0');
    static string GodotInt64ToTextGUID(Int64 Value)
    {
        Int64 p_id = Value;
        p_id &= 0x7FFFFFFFFFFFFFFF;

        if (p_id < 0)
        {
            return "<invalid>";
        }
        string txt = "";

        while (p_id > 0)
        {
            uint c = (uint)(p_id % base_);
            if (c < char_count)
            {
                char NewChar = (char)('a' + (char)c);
                string prepend = "";
                prepend += NewChar;
                txt = prepend + txt;
            }
            else
            {
                char NewChar = (char)('0' + (c - (char_count)));
                string prepend = "";
                prepend += NewChar;
                txt = prepend + txt;
            }
            p_id /= base_;
        }

        return txt;
    }
    static Int64 GetRandomInt64()
    {
        System.Random rng = new System.Random();
        byte[] buffer = new byte[8];
        rng.NextBytes(buffer);
        long result = BitConverter.ToInt64(buffer, 0);
        return result;
    }
    static string GetGUID5(UnityEngine.Object Obj)
    {
        Int64 InputHash = Obj.GetHashCode();
        string GUID13 = GodotInt64ToTextGUID(InputHash);
        int MaxLength = 5;
        if (GUID13.Length < MaxLength)
            MaxLength = GUID13.Length;
        string GUID5 = GUID13.Substring(0, MaxLength);
        return GUID5;
    }
    static string GetGodotGUID(UnityEngine.Object Obj)
    {
        Int64 InputHash = 0;
        if (Obj != null)
            InputHash = Obj.GetHashCode();
        else
            InputHash = GetRandomInt64();

        string GUID13 = GodotInt64ToTextGUID(InputHash);
        return GUID13;
    }
    static string GetGodotGUID(Int64 InputHash)
    {
        string GUID13 = GodotInt64ToTextGUID(InputHash);
        return GUID13;
    }
    static string GenerateLocalGUID(UnityEngine.Object Obj, int Index)
    {
        string GUID5 = GetGUID5(Obj);

        string LocalGUID = string.Format( CultureInfo.InvariantCulture,"{0}_{1}", Index, GUID5);
        return LocalGUID;
    }
    static string GenerateMeshMeta(string MeshName, string MeshGUID, string ResourcePath)
    {
        string Ret = string.Format( CultureInfo.InvariantCulture,
                    "[remap]\n" +
                    "\n" +
                    "importer=\"scene\"\n" +
                    "importer_version=1\n" +
                    "type=\"PackedScene\"\n" +
                    "uid=\"uid://{0}\"\n" +
                    //"path=\"res://.godot/imported/SM_Chair.glb-3816969e5e4fb8bf618da4c850bc350f.scn\"\n"
                    "\n" +
                    "[deps]\n" +
                    "\n" +
                    "source_file=\"res://{1}\"\n" +
                    //"dest_files=[\"res://.godot/imported/SM_Chair.glb-3816969e5e4fb8bf618da4c850bc350f.scn\"]\n"
                    "\n" +
                    "[params]\n" +
                    "\n" +
                    "nodes/root_type=\"\"\n" +
                    "nodes/root_name=\"\"\n" +
                    "nodes/apply_root_scale=true\n" +
                    "nodes/root_scale=1.0\n" +
                    "meshes/ensure_tangents=true\n" +
                    "meshes/generate_lods=true\n" +
                    "meshes/create_shadow_meshes=true\n" +
                    "meshes/light_baking=1\n" +
                    "meshes/lightmap_texel_size=0.2\n" +
                    "meshes/force_disable_compression=false\n" +
                    "skins/use_named_skins=true\n" +
                    "animation/import=true\n" +
                    "animation/fps=30\n" +
                    "animation/trimming=false\n" +
                    "animation/remove_immutable_tracks=true\n" +
                    "import_script/path = \"\"\n" +
                    "_subresources={{}}\n" +
                    "gltf/naming_version=1\n" +
                    "gltf/embedded_image_handling=1\n",
                    MeshGUID,
                    ResourcePath
                    );

        return Ret;
    }
    static string GenerateTextureMeta(ExportTexture Tex, bool IsNormalMap)
    {
        int NormalMap = IsNormalMap ? 1 : 0;
        bool IsCubemap = (Tex.Tex as Cubemap != null);
        Texture3D Is3D = Tex.Tex as Texture3D;
        Texture2DArray Is2DArray = Tex.Tex as Texture2DArray;
        string Importer = "texture";
        string Type = "CompressedTexture2D";
        string vram_texture = "true";
        string imported_formats = "\"imported_formats\": [\"s3tc_bptc\"],\n";
        string ExtraParams = "";
        if (IsCubemap)
        {
            Importer = "cubemap_texture";
            Type = "CompressedCubemap";
            vram_texture = "false";
            imported_formats = "";
            ExtraParams = "slices/arrangement=3";
        }
        if (Is3D != null)
        {
            var Cells = GetTextureCells(Is3D);

            Importer = "3d_texture";
            Type = "CompressedTexture3D";
            vram_texture = "true";
            ExtraParams = string.Format( CultureInfo.InvariantCulture,
                          "slices/horizontal={0}\r\n" +
                          "slices/vertical={1}\n", Cells.X, Cells.Y);
        }
        if (Is2DArray != null)
        {
            var Cells = GetTextureCells(Is2DArray);

            Importer = "2d_array_texture";
            Type = "CompressedTexture2DArray";
            vram_texture = "true";
            ExtraParams = string.Format( CultureInfo.InvariantCulture,
                          "slices/horizontal={0}\r\n" +
                          "slices/vertical={1}\n", Cells.X, Cells.Y);
        }
        string Text = string.Format( CultureInfo.InvariantCulture,
                "[remap]\n" +
                "\n" +
                "importer=\"{3}\"\n" +
                "type=\"{4}\"\n" +
                "uid=\"uid://{0}\"\n" +
                //path.s3tc="res://.godot/imported/SM_TableRound_M_TableRound_BaseColor.png-6ecf1d789266e5a7ef5485da95c3001f.s3tc.ctex"
                "metadata={{\n" +
                "{6}" +
                "\"vram_texture\": {5}\n" +
                "}}\n" +
                "generator_parameters={{}}\n" +
                "\n" +
                "[deps]\n" +
                "\n" +
                "source_file=\"res://{1}\"\n" +
                //dest_files=["res://.godot/imported/SM_TableRound_M_TableRound_BaseColor.png-6ecf1d789266e5a7ef5485da95c3001f.s3tc.ctex"]
                "\n" +
                "[params]\n" +
                "\n" +
                "compress/mode=2\n" +
                "compress/high_quality=false\n" +//put it to true once Godot fixes its BC7 compression!
                "compress/lossy_quality=0.7\n" +
                "compress/hdr_compression=1\n" +
                "compress/normal_map={2}\n" +
                "compress/channel_pack=0\n" +
                "mipmaps/generate=true\n" +
                "mipmaps/limit=-1\n" +
                "roughness/mode=0\n" +
                "roughness/src_normal=\"\"\n" +
                "process/fix_alpha_border=false\n" +
                "process/premult_alpha=false\n" +
                "process/normal_map_invert_y=false\n" +
                "process/hdr_as_srgb=false\n" +
                "process/hdr_clamp_exposure=false\n" +
                "process/size_limit=0\n" +
                "detect_3d/compress_to=0\n" +
                ExtraParams,
                Tex.GUID, Tex.ResourcePath, NormalMap, Importer, Type, vram_texture, imported_formats
            );

        return Text;
    }
    public static string GetTextureTypeForResource(Texture Tex)
    {
        if (Tex as Cubemap)
            return "CompressedCubemap";
        if (Tex as Texture3D)
            return "CompressedTexture3D";
        if (Tex as Texture2DArray)
            return "Texture2DArray";

        return "Texture2D";
    }
    public static List<Component> GetPhysicsShapes(GameObject GO)
    {
        List<Component> ShapeComponents = new List<Component>();
        List<Component> Components = new List<Component>();
        GO.GetComponents(Components);

        for (int i = 0; i < Components.Count; i++)
        {
            Collider collider = Components[i] as Collider;
            if (collider != null && !collider.enabled)
                continue;
            BoxCollider Box = Components[i] as BoxCollider;            
            if (Box != null)
                ShapeComponents.Add(Box);
            SphereCollider Sphere = Components[i] as SphereCollider;
            if (Sphere != null)
                ShapeComponents.Add(Sphere);
            CapsuleCollider Capsule = Components[i] as CapsuleCollider;
            if (Capsule != null)
                ShapeComponents.Add(Capsule);
        }

        return ShapeComponents;
    }
    public static string GeneratePhysicsSubresources(List<Component> PhysicsShapes, ref Dictionary<Component, string> ShapesSubresourceIDs)
    {
        string Ret = "";
        if (PhysicsShapes.Count > 0)
        {
            Ret += "\n";
            for (int s = 0; s < PhysicsShapes.Count; s++)
            {
                Component Shape = PhysicsShapes[s];

                string HashSource = string.Format( CultureInfo.InvariantCulture,"Shape{0}", ShapesSubresourceIDs.Count);
                string LocalGUID = GenerateLocalGUID(Shape, ShapesSubresourceIDs.Count);
                ShapesSubresourceIDs.Add(Shape, LocalGUID);

                BoxCollider Box = Shape as BoxCollider;
                SphereCollider Sphere = Shape as SphereCollider;
                CapsuleCollider Capsule = Shape as CapsuleCollider;
                if (Box != null)
                {
                    Vector3 Size = Box.size;
                    if (Box.size.x < 0 || Box.size.y < 0 || Box.size.z < 0)
                    {
                        Size.x = Math.Abs(Box.size.x);
                        Size.y = Math.Abs(Box.size.y);
                        Size.z = Math.Abs(Box.size.z);
                    }
                    Ret += string.Format( CultureInfo.InvariantCulture,"[sub_resource type=\"BoxShape3D\" id=\"{0}\"]\n" +
                                              "size = Vector3( {1}, {2}, {3})\n",
                             LocalGUID, Size.x, Size.y, Size.z);
                }
                else if (Sphere != null)
                {
                    Ret += string.Format( CultureInfo.InvariantCulture,"[sub_resource type=\"SphereShape3D\" id=\"{0}\"]\n" +
                             "radius = {1}\n",
                             LocalGUID, Sphere.radius);
                }
                else if (Capsule != null)
                {
                    Ret += string.Format( CultureInfo.InvariantCulture,"[sub_resource type=\"CapsuleShape3D\" id=\"{0}\"]\n" +
                             "radius = {1}\n" +
                             "height = {2}\n", LocalGUID, Capsule.radius, Capsule.height);
                }

            }
        }
        return Ret;
    }
    public static string GenerateStaticBody(List<Component> PhysicsShapes, Dictionary<Component, string> ShapesSubresourceIDs, string NodeName = "StaticBody",
        string ParentName = null)
    {
        string Ret = "";

        if (PhysicsShapes.Count > 0)
        {
            string PhysicsBodyNode = GenerateNodeText(NodeName, "StaticBody3D", ParentName, null);
            Ret += string.Format( CultureInfo.InvariantCulture,"\n{0}\n", PhysicsBodyNode);

            for (int s = 0; s < PhysicsShapes.Count; s++)
            {
                string ShapeParent = NodeName;
                if (ParentName != null)
                    ShapeParent = ParentName + "/" + NodeName;

                Component Shape = PhysicsShapes[s];
                string ShapeNode = GenerateNodeText(Shape.GetType().Name + "_" + s, "CollisionShape3D", ShapeParent, null);
                Ret += string.Format( CultureInfo.InvariantCulture,"{0}", ShapeNode);

                Transform ShapeTransform = Shape.gameObject.transform;
                BoxCollider Box = Shape as BoxCollider;
                SphereCollider Sphere = Shape as SphereCollider;
                CapsuleCollider Capsule = Shape as CapsuleCollider;
                Vector3 Position = ShapeTransform.localPosition;
                Quaternion Rotation = ShapeTransform.localRotation;
                Vector3 Scale = ShapeTransform.localScale;

                //if (!IsPrefab)//shapes would get the transform twice otherwise
                {
                    Position = Vector3.zero;
                    Rotation = Quaternion.identity;
                    Scale = Vector3.one;
                }

                if (Box != null)
                {
                    Position = Box.center;
                }
                else if (Sphere != null)
                {
                    Position = Sphere.center;
                }
                else if (Capsule != null)
                {
                    Position = Capsule.center;
                }
                string TransformText = GenerateTransformText(Position, Rotation, Scale);
                Ret += string.Format( CultureInfo.InvariantCulture,"{0}", TransformText);
                string LocalGUID = "null";
                LocalGUID = ShapesSubresourceIDs[Shape];
                Ret += string.Format( CultureInfo.InvariantCulture,"shape = SubResource(\"{0}\")\n\n", LocalGUID);
                //Disable the transparent cyan box
                Ret += "debug_fill = false\n";
            }
        }
        return Ret;
    }
    static void WritePrefab(string PrefabFolder, ExportMesh exportMesh)
    {
        int NumMaterials = exportMesh.ExportMaterials.Length;
        int LoadSteps = NumMaterials + 2;
        exportMesh.PrefabGUID = GetGodotGUID(exportMesh.GO);
        string FileData = string.Format( CultureInfo.InvariantCulture,"[gd_scene load_steps={0} format=3 uid=\"uid://{1}\"]\n\n", LoadSteps, exportMesh.PrefabGUID);

        string MeshLocalGUID = GetGUID5(exportMesh.mesh);
        FileData += string.Format( CultureInfo.InvariantCulture,"[ext_resource type=\"PackedScene\" uid=\"uid://{0}\" path=\"res://{1}\" id=\"1_{2}\"]\n",
                 exportMesh.MeshGUID, exportMesh.ResourcePath, MeshLocalGUID);

        List<string> MaterialLocalGUIDs = new List<string>();
        for (int m = 0; m < exportMesh.ExportMaterials.Length; m++)
        {
            string MaterialLocalGUID = "";
            ExportMaterial Mat = exportMesh.ExportMaterials[m];
            if (Mat != null)
            {
                MaterialLocalGUID = GetGUID5(Mat.material);

                FileData += string.Format( CultureInfo.InvariantCulture,"[ext_resource type=\"Material\" uid=\"uid://{0}\" path=\"res://{1}\" id=\"{2}_{3}\"]\n",
                     Mat.GUID, Mat.ResourcePath, 2 + m, MaterialLocalGUID);
            }
            MaterialLocalGUIDs.Add(MaterialLocalGUID);
        }

        List<Component> PhysicsShapes = GetPhysicsShapes(exportMesh.GO);
        Dictionary<Component, string> ShapesSubresourceIDs = new Dictionary<Component, string>();
        if (PhysicsShapes.Count > 0)
        {
            string PhysicsSubResources = GeneratePhysicsSubresources(PhysicsShapes, ref ShapesSubresourceIDs);
            FileData += PhysicsSubResources;
        }
        //Asset can contain multiple meshes
        FileData += string.Format( CultureInfo.InvariantCulture,"\n[node name=\"{0}\" instance=ExtResource(\"1_{1}\")]\n\n" +
                         "[node name=\"{2}\" parent=\".\" index=\"0\"]\n",
                            exportMesh.AssetName, MeshLocalGUID,
                            exportMesh.MeshName);

        for (int m = 0; m < exportMesh.ExportMaterials.Length; m++)
        {
            if (MaterialLocalGUIDs[m].Length > 0)
            {
                FileData += string.Format( CultureInfo.InvariantCulture,"surface_material_override/{0} = ExtResource(\"{1}_{2}\")\n",
                         m, 2 + m, MaterialLocalGUIDs[m]);
            }
        }

        if (PhysicsShapes.Count > 0)
        {
            string PhysicsShapesReferences = GenerateStaticBody(PhysicsShapes, ShapesSubresourceIDs);
            FileData += PhysicsShapesReferences;
        }

        exportMesh.PrefabResourcePath = PrefabFolder + "/" + exportMesh.AssetName + ".tscn";
        string FinalPrefabPath = ExportFolder + exportMesh.PrefabResourcePath;
        CreateDirectoriesForFile(FinalPrefabPath);
        File.WriteAllText(FinalPrefabPath, FileData);
    }
    static string RemoveExtension(string Path)
    {
        int LastDot = Path.LastIndexOf(".");
        if (LastDot != -1)
            return Path.Substring(0, LastDot);
        else
            return Path;
    }
    static string GetExtension(string Path)
    {
        int LastDot = Path.LastIndexOf(".");
        if (LastDot != -1)
            return Path.Substring(LastDot, Path.Length - LastDot);
        return null;
    }
    static void GetMeshPathForGameObject(GameObject GO, ref string OutName, ref string OutPath)
    {
        OutName = GO.name;
        MeshFilter MF = GO.GetComponent<MeshFilter>();// GetMeshFilter(GO);
        if (MF != null && MF.sharedMesh != null)
        {
            var SourceGO = PrefabUtility.GetCorrespondingObjectFromSource(MF.sharedMesh);
            var OriginalGO = PrefabUtility.GetCorrespondingObjectFromOriginalSource(MF.sharedMesh);
            //var Original2 = PrefabUtility.GetCorrespondingObjectFromSource( OriginalGO );
            OutPath = AssetDatabase.GetAssetPath(OriginalGO);
            if (OutPath.Length > 0)
            {
                OutName = GetAssetNameFromPath(OutPath);
                OutPath = GetGodotPathFromUnityPath(OutPath);
            }
            if (OutName == null)
                OutName = MF.sharedMesh.name;
        }

    }
    public static void GetAllNodesInHierarchy(GameObject Root, ref List<GameObject> AllGOs)
    {
        AllGOs.Add(Root);

        for (int i = 0; i < Root.transform.childCount; i++)
        {
            GameObject GO = Root.transform.GetChild(i).gameObject;
            GetAllNodesInHierarchy(GO, ref AllGOs);
        }
    }
    static async Task SetNameBack(GameObject GO, string OriginalName)
    {
        GO.name = OriginalName;
    }
    static async Task ExportGLTFAsync(ExportMesh exportMesh)
    {
        #if UNITY_MATH_1_3_OR_NEWER
        bool binary = true;
        var extension = ".glb";
        string DestinationFolder = ExportFolder + "/" + exportMesh.AssetPath;
        string path = DestinationFolder + "/" + exportMesh.AssetName + extension;

        GLTFast.Export.GameObjectExport.AddChildren = false;
        GLTFast.Export.GameObjectExport.ExportObject = exportMesh.GO;
        GLTFast.Export.StandardMaterialExport.EmbedTextures = EmbedTexturesInGLB;
        GltfWriter.TransformIsIdentity = true;
        int NumObjects = 1;
        List<GameObject> SkinGOs = new List<GameObject>();
        bool AddEntireHierarchy = false;
        if (exportMesh.bones != null)
        {
            if (AddEntireHierarchy)
            {
                SkinnedMeshRenderer SMR = exportMesh.GO.GetComponent<SkinnedMeshRenderer>();
                Transform Root = SMR.rootBone;
                GetAllNodesInHierarchy(Root.gameObject, ref SkinGOs);

                NumObjects = 1 + SkinGOs.Count;
            }
            else
                NumObjects = 1 + exportMesh.bones.Length;

            GLTFast.Export.GameObjectExport.AddChildren = true;
            GltfWriter.TransformIsIdentity = false;

            exportMesh.GO.transform.localRotation *= Quaternion.Euler(90, 0, 0);
            if (exportMesh.RootBone == null)
            {
            }
            if (exportMesh.RootBone != null)
            {
                //Make GLB be world centered
                exportMesh.RootBone.position = new Vector3(0, 0, 0);
                exportMesh.RootBone.localScale = new Vector3(1, 1, 1);
            }
        }
        GameObject[] GameObjectArray = new GameObject[NumObjects];
        GameObjectArray[0] = exportMesh.GO;

        if (exportMesh.bones != null)
        {
            if (AddEntireHierarchy)
            {
                for (int i = 0; i < SkinGOs.Count; i++)
                {
                    GameObjectArray[1 + i] = SkinGOs[i];
                }
            }
            else
            {
                for (int i = 0; i < exportMesh.bones.Length; i++)
                {
                    GameObjectArray[1 + i] = exportMesh.bones[i].gameObject;
                }
            }
        }

        if (!Directory.Exists(DestinationFolder))
            Directory.CreateDirectory(DestinationFolder);

        string NameBefore = exportMesh.GO.name;
        exportMesh.GO.name = exportMesh.MeshName;
        Func<Task> RenameTask = () => SetNameBack(exportMesh.GO, NameBefore);
        if (!string.IsNullOrEmpty(path))
        {
            //saveFolderPath = Directory.GetParent( path )?.FullName;
            GLTFast.Export.ExportSettings settings = GetDefaultSettings(binary);
            var export = new GLTFast.Export.GameObjectExport(settings, logger: new ConsoleLogger());
            export.AddScene(GameObjectArray, exportMesh.AssetName);
            var Task = export.SaveToFileAndDispose(path);
            await Task;
#if GLTF_VALIDATOR
            var report = Validator.Validate(path);
            report.Log();
#endif
        }

        await RenameTask();

        string MetaContents = GenerateMeshMeta(exportMesh.AssetName, exportMesh.MeshGUID, exportMesh.ResourcePath);
        string MetaFile = path + ".import";
        File.WriteAllText(MetaFile, MetaContents);
        #endif
    }
    static MeshRenderer GetMeshRenderer(GameObject GO)
    {
        MeshRenderer MR = GO.GetComponentInChildren<MeshRenderer>();

        return MR;
    }
    //static MeshFilter GetMeshFilter( GameObject GO )
    //{
    //    MeshFilter MF = GO.GetComponentInChildren<MeshFilter>();
    //
    //    return MF;
    //}
    static Mesh GetMesh(GameObject GO)
    {
        Mesh mesh = null;
        MeshFilter TargetMF = GO.GetComponent<MeshFilter>();
        MeshRenderer TargetMR = GO.GetComponent<MeshRenderer>();
        SkinnedMeshRenderer TargetSMR = GO.GetComponent<SkinnedMeshRenderer>();

        if (TargetMF != null && TargetMR != null && TargetMR.enabled && TargetMR.shadowCastingMode != ShadowCastingMode.ShadowsOnly)//Without a MeshRenderer it's not visible anyway
        {
            if ( TargetMF.sharedMesh != null && TargetMF.sharedMesh.vertexCount > 0 )
                mesh = TargetMF.sharedMesh;
        }
        if (TargetSMR != null && TargetSMR.enabled && TargetSMR.shadowCastingMode != ShadowCastingMode.ShadowsOnly)
        {
            if ( TargetSMR.sharedMesh != null && TargetSMR.sharedMesh.vertexCount > 0 )
                mesh = TargetSMR.sharedMesh;
        }

        return mesh;
    }
    static Material[] GetMaterials(GameObject GO)
    {
        MeshRenderer TargetMR = GO.GetComponent<MeshRenderer>();
        SkinnedMeshRenderer TargetSMR = GO.GetComponent<SkinnedMeshRenderer>();

        Material[] Ret = null;
        if (TargetMR != null)
            Ret = TargetMR.sharedMaterials;
        if (TargetSMR != null)
            Ret = TargetSMR.sharedMaterials;

        if (Ret == null)//happens when we have a mesh filter but not mesh renderer
            Ret = new Material[0];

        return Ret;
    }
    static void GetBones(GameObject GO, ref Transform[] bones, ref Transform RootBone)
    {
        SkinnedMeshRenderer TargetSMR = GO.GetComponent<SkinnedMeshRenderer>();
        if (TargetSMR != null)
        {
            if (TargetSMR.rootBone == null)
            {
                TargetSMR.rootBone = GO.transform;
            }
            //if ( TargetSMR.bones == null || TargetSMR.bones.Length == 0)
            //{
            //    Transform[] NewBones = new Transform[1];
            //    NewBones[0] = GO.transform;
            //    TargetSMR.bones = NewBones;
            //}

            bones = TargetSMR.bones;
            RootBone = TargetSMR.rootBone;
        }
    }

    static bool AreMaterialsEqual(Material[] A, Material[] B)
    {
        if (A == null && B == null)
            return true;
        else if (A == null || B == null)
            return false;

        if (A.Length != B.Length)
            return false;

        for (int i = 0; i < A.Length; i++)
        {
            if (A[i] != B[i])
                return false;
        }

        return true;
    }
    static bool IsAlreadyInExportList(List<ExportMesh> MeshesToExport, GameObject GO)
    {
        Mesh mesh = GetMesh(GO);
        Material[] Materials = GetMaterials(GO);

        if (mesh == null)
            return false;

        for (int i = 0; i < MeshesToExport.Count; i++)
        {
            ExportMesh Entry = MeshesToExport[i];
            bool SameMaterials = AreMaterialsEqual(Entry.OriginalMaterials, Materials);
            if (Entry.mesh == mesh && SameMaterials)
                return true;
        }
        return false;
    }
    static string GenerateTransformText(Transform transform, bool ForDirectionalLight = false, bool ForDecal = false)
    {
        return GenerateTransformText(transform.localPosition, transform.localRotation, transform.localScale, ForDirectionalLight, ForDecal);
    }
    static string GenerateTransformText(Vector3 Position, Quaternion InitialQuat, Vector3 Scale, bool ForDirectionalLight = false, bool ForDecal = false)
    {
        //Vector3 Position = transform.position;
        //Quaternion InitialQuat = transform.rotation;
        bool InLocalSpace = true;
        //if (InLocalSpace)
        //{
        //    Position = transform.localPosition;
        //    InitialQuat = transform.localRotation;
        //}
        Quaternion OutQuat = InitialQuat;
        if (ForDecal)
        {
            OutQuat *= Quaternion.Euler(90, 0, 0);
        }
        Vector3 Eulers = OutQuat.eulerAngles;
        if (ForDirectionalLight)
        {
            Eulers.x *= -1;
            Eulers.z *= -1;
            Eulers.y *= -1;
            Eulers.y -= 180;
        }
        else
        {
            Eulers.z *= -1;
            Eulers.y *= -1;
        }
        Position.x *= -1;

        //Transform TheTransform = new Transform();
        Vector3 PositionForMatrix = Position;
        if (InLocalSpace)
        {
            //transform.localPosition = Position;
        }
        else
        {
            //transform.position = Position;
        }
        OutQuat = Quaternion.Euler(Eulers);

        Quaternion QuatForMatrix = OutQuat;

        if (InLocalSpace)
        {
            //transform.localRotation = OutQuat;
        }
        else
        {
            //transform.rotation = OutQuat;
        }
        //UnityEngine.Matrix4x4 mat = transform.localToWorldMatrix;
        UnityEngine.Matrix4x4 mat = Matrix4x4.TRS(PositionForMatrix, QuatForMatrix, Scale);

        //if ( InLocalSpace )
        //{
        //    mat = Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale );
        //}

        string Ret = string.Format( CultureInfo.InvariantCulture,
            "transform = Transform3D({0:0.#####}, {1:0.#####}, {2:0.#####}, {3:0.#####}, {4:0.#####}, {5:0.#####}, {6:0.#####}, {7:0.#####}, {8:0.#####}, {9:0.#####}, {10:0.#####}, {11:0.#####})\n",
                  mat.m00, mat.m01, mat.m02,
                  mat.m10, mat.m11, mat.m12,
                  mat.m20, mat.m21, mat.m22,
                  mat.m03, mat.m13, mat.m23);

        //revert so unity scene doesn't change
        Position.x *= -1;
        //if (InLocalSpace)
        //{
        //    transform.localPosition = Position;
        //    transform.localRotation = InitialQuat;
        //}
        //else
        //{
        //    transform.position = Position;
        //    transform.rotation = InitialQuat;
        //}

        return Ret;
    }
    static string GenerateParentsText(GameObject GO)
    {
        Canvas canvas = GO.GetComponent<Canvas>();
        if ( canvas != null )
        {
            return "";
        }

        if (GO.transform.parent != null)
        {
            string ParentText = GenerateParentsText(GO.transform.parent.gameObject);
            if ( ParentText.Equals(""))
                return ".";
            if (!ParentText.Equals("."))
                return ParentText + "/" + GO.transform.parent.name;
            else
                return GO.transform.parent.name;
        }
        else
            return ".";
    }
    static string GenerateNodeText(string Name, string Type, string ParentName, string PrefabLocalID, bool ForCanvas = false )
    {
        string Text = "";
        string InstanceText = "";
        if (PrefabLocalID != null && PrefabLocalID.Length > 0)
        {
            InstanceText = string.Format( CultureInfo.InvariantCulture,
                      "instance=ExtResource(\"{0}\")", PrefabLocalID);
        }
        string ParentText = "parent=\".\"";
        if (ParentName != null && ParentName.Length > 0)
        {

            ParentText = string.Format( CultureInfo.InvariantCulture,
                      "parent=\"{0}\"",
                      ParentName);
        }
        else if (ForCanvas)
        {
            ParentText = "";
        }
        string TypeText = "";
        if (Type != null && Type.Length > 0)
        {
            TypeText = string.Format( CultureInfo.InvariantCulture,"type=\"{0}\"", Type);
        }

        Text = string.Format( CultureInfo.InvariantCulture,"[node name=\"{0}\" {1} {2} {3}]\n", Name, TypeText, ParentText, InstanceText);

        return Text;
    }
    public static string SanitizeMeshName(string OriginalName)
    {
        string InvalidCharacters = ".:@/\"%";
        int InvalidCharsLen = InvalidCharacters.Length;

        for (int c = 0; c < InvalidCharsLen; c++)
        {
            OriginalName = OriginalName.Replace(InvalidCharacters[c].ToString(), "_");
        }
        if (OriginalName.Length == 0)
        {
            OriginalName = "Mesh";
        }
        //Godot thinks an object ending in _wheel is a Vehicle or something
        if (OriginalName.Contains("wheel"))
        {
            OriginalName = OriginalName.Replace("wheel", "wh3el");
        }

        return OriginalName;
    }
    public static void SanitizeParamName(ref string OriginalName)
    {
        string[] ShaderKeywords = {
                "ALBEDO",
                "ALPHA",
                "METALLIC",
                "ROUGHNESS",
                "SPECULAR",
                "EMISSION",
                "AO",
                "AO_LIGHT_AFFECT",
                "NORMAL",
                "NORMAL_MAP",
                "NORMAL_MAP_DEPTH",
                "RIM",
                "RIM_TINT",
                "CLEARCOAT",
                "CLEARCOAT_ROUGHNESS",
                "ANISOTROPY",
                "ANISOTROPY_FLOW",
                "SSS_STRENGTH",
                "BACKLIGHT",
                "ALPHA_SCISSOR_THRESHOLD",
                "ALPHA_HASH_SCALE",
                "ALPHA_ANTIALIASING_EDGE",
                "ALPHA_TEXTURE_COORDINATE",
                "DEPTH",
                "E",//for some reason it says it's a redefinition
			    "UV",
                "UV2",
                "TANGENT",
                "BINORMAL",
                "COLOR",
                "VIEW",
                "FRAGCOORD",
                "TIME",
                "EXPOSURE",
                "EYE_OFFSET",
                "CAMERA_POSITION_WORLD",
                "CAMERA_DIRECTION_WORLD",
                "smooth",
                "int",
                "float",
                "double"
        };
        int Occurances = 0;
        int NumKeywords = ShaderKeywords.Length;
        for (int i = 0; i < NumKeywords; i++)
        {
            if (OriginalName.Equals(ShaderKeywords[i]))
            {
                OriginalName += "_";
                OriginalName += Occurances;
                Occurances++;
            }
        }

        string Str = OriginalName;
        //Param names can't start with a figure
        if (Str[0] >= '0' && Str[0] <= '9')
        {
            OriginalName = "UTG_" + OriginalName;
            Str = OriginalName;
        }

        string InvalidCharacters = "~`!@#$%^&*()+-=[]{}|\\;:\'\"<>/?,.\t\n ";
        int InvalidCharsLen = InvalidCharacters.Length;

        for (int c = 0; c < InvalidCharsLen; c++)
        {
            Str = Str.Replace(InvalidCharacters[c].ToString(), "_");
        }

        OriginalName = Str;
    }
    public static void SanitizeMaterialName( ref string MatName )
    {
        string InvalidCharacters = "~`!@#$%^&*()+-=[]{}|\\;:\'\"<>/?,.\t\n ";
        int InvalidCharsLen = InvalidCharacters.Length;

        for (int c = 0; c < InvalidCharsLen; c++)
        {
            MatName = MatName.Replace(InvalidCharacters[c].ToString(), "_");
        }
    }
    public static void ProcessMaterialParameter(ShaderParameter Param, Material material, string UnityParamName, string GodotParamName, ref string Text)
    {
        if (Param.Type == ShaderParameterType.SPT_FLOAT)
        {
            float Value = Param.DefaultFloat;
            if (material.HasFloat(UnityParamName))
                Value = material.GetFloat(UnityParamName);
            Text = string.Format( CultureInfo.InvariantCulture,
                        "shader_parameter/{0} = {1:0.000}\n",
                        GodotParamName, Value);
        }
        else if (Param.Type == ShaderParameterType.SPT_VECTOR2)
        {
            Vector4 Value = Param.DefaultVector;
            if (material.HasVector(UnityParamName))
                Value = material.GetVector(UnityParamName);
            Text = string.Format( CultureInfo.InvariantCulture,
                        "shader_parameter/{0} = Vector2( {1:0.000}, {2:0.000} )\n",
                        GodotParamName, Value.x, Value.y);
        }
        else if (Param.Type == ShaderParameterType.SPT_VECTOR3)
        {
            Vector4 Value = Param.DefaultVector;
            if (material.HasVector(UnityParamName))
                Value = material.GetVector(UnityParamName);
            Text = string.Format( CultureInfo.InvariantCulture,
                        "shader_parameter/{0} = Vector3( {1:0.000}, {2:0.000}, {3:0.000} )\n",
                        GodotParamName, Value.x, Value.y, Value.z);
        }
        else if (Param.Type == ShaderParameterType.SPT_VECTOR4)
        {            
            Vector4 Value = Param.DefaultVector;
            if ( Param.Flag != ShaderParameterFlag.SPF_IS_TILING_OFFSET )
            {
                if (material.HasVector(UnityParamName))
                    Value = material.GetVector(UnityParamName);
            }
            Text = string.Format( CultureInfo.InvariantCulture,
                        "shader_parameter/{0} = Vector4( {1:0.000}, {2:0.000}, {3:0.000}, {4:0.000})\n",
                        GodotParamName, Value.x, Value.y, Value.z, Value.w);
        }
        else if (Param.Type == ShaderParameterType.SPT_COLOR)
        {
            Vector4 Value = Param.DefaultVector;
            if (material.HasVector(UnityParamName))
                Value = material.GetVector(UnityParamName);
            if (Value.x > 1.0f || Value.y > 1.0f || Value.z > 1.0f || Value.w > 1.0f)
            {
                Color storedLinear = Value;
                float r = Mathf.LinearToGammaSpace(storedLinear.r);
                float g = Mathf.LinearToGammaSpace(storedLinear.g);
                float b = Mathf.LinearToGammaSpace(storedLinear.b);
                Color inspectorColor = new Color( r,g,b, storedLinear.a );
                Value = inspectorColor;
            }
            Text = string.Format( CultureInfo.InvariantCulture,
                        "shader_parameter/{0} = Color( {1:0.000}, {2:0.000}, {3:0.000}, {4:0.000})\n",
                        GodotParamName, Value.x, Value.y, Value.z, Value.w);
        }
        else if (Param.Type == ShaderParameterType.SPT_INT)
        {
            int Value = Param.DefaultInt;
            if (material.HasInt(UnityParamName))
                Value = material.GetInt(UnityParamName);

            Text = string.Format( CultureInfo.InvariantCulture,
                        "shader_parameter/{0} = {1}\n",
                        GodotParamName, Value);
        }
        else if (Param.Type == ShaderParameterType.SPT_BOOL)
        {
            int Value = Param.DefaultBool == true ? 1 : 0;
            if ( UnityParamName.Equals("_AlphaClipping"))
            {
                if ( material.IsKeywordEnabled( "_ALPHATEST_ON"))
                    Value = 1;
                else
                    Value = 0;//override the default
            }
            else
            {
                if (material.HasInt(UnityParamName))
                    Value = material.GetInt(UnityParamName);
            }
            bool BoolValue = false;
            if (Value == 1)
                BoolValue = true;

            Text = string.Format( CultureInfo.InvariantCulture,
                        "shader_parameter/{0} = {1}\n",
                        GodotParamName, BoolValue.ToString().ToLower());
        }
    }
    public static Texture2D DefaultBlackTexture = null;
    public static Texture2D GetDefaultBlackTexture()
    {
        #if UNITY_2020_2_OR_NEWER
            return Texture2D.blackTexture;
        #else
            if ( DefaultBlackTexture == null )
            {
                DefaultBlackTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                DefaultBlackTexture.SetPixel(0, 0, UnityEngine.Color.black);
                DefaultBlackTexture.name = "UnityBlack";
                DefaultBlackTexture.Apply();
            }
            return DefaultBlackTexture;
        #endif
    }
    public static Texture2D DefaultGreyTexture = null;
    public static Texture2D GetDefaultGreyTexture()
    {
        #if UNITY_2020_2_OR_NEWER
            return Texture2D.grayTexture;
        #else
            if ( DefaultGreyTexture == null )
            {
                DefaultGreyTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                DefaultGreyTexture.SetPixel(0, 0, UnityEngine.Color.grey);
                DefaultGreyTexture.name = "UnityGrey";
                DefaultGreyTexture.Apply();
            }
            return DefaultGreyTexture;
        #endif
    }
    public static Texture2D DefaultRedTexture = null;
    public static Texture2D GetDefaultRedTexture()
    {
        #if UNITY_2020_2_OR_NEWER
            return Texture2D.grayTexture;
        #else
            if ( DefaultRedTexture == null )
            {
                DefaultRedTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                DefaultRedTexture.SetPixel(0, 0, UnityEngine.Color.red);
                DefaultRedTexture.name = "UnityRed";
                DefaultRedTexture.Apply();
            }
            return DefaultRedTexture;
        #endif
    }
    public static void ExtractMaterialTextures(ExportMaterial Material)
    {
        if (Material.exportShader != null )
        {
            if ( Material.exportShader.DefinitionType == ShaderDefinitionType.SDT_SURFACESHADER )
            {
                string[] TextureNames = Material.material.GetTexturePropertyNames();
                for (int i = 0; i < TextureNames.Length; i++)
                {
                    Texture MaterialTexture = Material.material.GetTexture(TextureNames[i]);
                    if ( MaterialTexture == null )
                    {
                        string DefaultType = GetDefaultTextureType( Material.material.shader, TextureNames[i] );
                        MaterialTexture = GetDefaultTexureFromString( DefaultType );
                    }
                    if (MaterialTexture != null)
                    {
                        if (Material.GetTexture( MaterialTexture) == null )
                        {
                            ExportTexture NewExportTexture = CreateExportTexture(MaterialTexture, Material.Textures.Count + 1);
                            Material.Textures.Add(NewExportTexture);
                        }
                    }
                }
            }
            if (Material.exportShader != null && Material.exportShader.ShaderData != null)
            {
                for (int i = 0; i < Material.exportShader.ShaderData.ShaderParameters.Count; i++)
                {
                    ShaderParameter Param = Material.exportShader.ShaderData.ShaderParameters[i];
                    string UnityParamName = Param.ReferenceName;
                    string GodotParamName = Param.SanitizedName;
                    if (Param.Type == ShaderParameterType.SPT_TEXTURE)
                    {
                        Texture MaterialTexture = Param.DefaultTexture;
                        if (Material.material.HasTexture(UnityParamName))
                        {
                            Texture FetchedTexture = Material.material.GetTexture(UnityParamName);
                            if ( FetchedTexture != null )
                                MaterialTexture = FetchedTexture;
                        }
                        else
                        {
                            string[] TextureNames = Material.material.GetTexturePropertyNames();
                            for (int t = 0; t < TextureNames.Length; t++)
                            {

                            }
                        }
                        if (MaterialTexture != null)
                        {
                            ExportTexture NewExportTexture = Material.GetTexture(MaterialTexture);
                            if (NewExportTexture == null)
                            {
                                NewExportTexture = CreateExportTexture(MaterialTexture, Material.Textures.Count + 1);
                                Material.Textures.Add(NewExportTexture);
                            }
                        }
                    }
                }
            }
        }
        else
        {
            string[] TextureNames = Material.material.GetTexturePropertyNames();
            for (int i = 0; i < TextureNames.Length; i++)
            {
                string TextureName = TextureNames[i];
                Texture MaterialTexture = Material.material.GetTexture(TextureName);
                if (MaterialTexture != null)
                {
                    ExportTexture NewExportTexture = Material.GetTexture( MaterialTexture);
                    if (NewExportTexture == null )
                    {
                        NewExportTexture = CreateExportTexture(MaterialTexture, Material.Textures.Count + 1);

                        if ( TextureName.Equals("_MetallicGlossMap") ||//URP
                            TextureName.Equals("_MaskMap") )//HDRP
                        {
                            NewExportTexture.IsSmoothnessTexture = true;
                        }

                        Material.Textures.Add(NewExportTexture);
                    }
                }
            }
        }
    }
    public static string GetDefaultTextureType( Shader shader, string PropertyName )
    {
        int propertyCount = ShaderUtil.GetPropertyCount( shader );
        for (int i = 0; i < propertyCount; i++)
        {
            string propName = ShaderUtil.GetPropertyName(shader, i);

            if ( propName.Equals( PropertyName ))
            {
                string DefaultName = shader.GetPropertyTextureDefaultName( i );
                var propType = ShaderUtil.GetPropertyType(shader, i);

                return DefaultName;
            }
        }

        return null;
    }
    public static Texture GetDefaultTexureFromString( string Name )
    {
        switch(Name)
        {
            case "white" : return Texture2D.whiteTexture;
            case "grey" : return GetDefaultGreyTexture();
            case "red" : return GetDefaultRedTexture();
            case "bump": return Texture2D.normalTexture;
            default:
            case "black" : return GetDefaultBlackTexture();
        }
    }
    public static string GenerateMaterial(ExportMaterial Material)
    {
        string Ret = "";

        string Text = "";
        Material.GUID = GetGodotGUID(Material.material.GetHashCode());

        string Params = "";
        string ResourceType = "StandardMaterial3D";

        if (Material.exportShader != null )
        {
            ResourceType = "ShaderMaterial";

            if ( Material.exportShader.DefinitionType == ShaderDefinitionType.SDT_SURFACESHADER )
            {
                string[] TextureNames = Material.material.GetTexturePropertyNames();
                for (int i = 0; i < TextureNames.Length; i++)
                {
                    Texture MaterialTexture = Material.material.GetTexture(TextureNames[i]);
                    if ( MaterialTexture == null )
                    {
                        string DefaultType = GetDefaultTextureType( Material.material.shader, TextureNames[i] );
                        MaterialTexture = GetDefaultTexureFromString( DefaultType );
                    }
                    if (MaterialTexture != null)
                    {
                        Vector2 Offset = Material.material.GetTextureOffset(TextureNames[i]);
                        Vector2 Scale = Material.material.GetTextureScale(TextureNames[i]);
                        Vector4 TillingOffset = new Vector4(Scale.x, Scale.y, Offset.x, Offset.y);

                        string UnityParamName = TextureNames[i];
                        string GodotParamName = UnityParamName;
                        SanitizeParamName(ref GodotParamName);

                        ExportTexture exportTexture = Material.GetTexture( MaterialTexture );
                        if ( exportTexture == null )
                        {
                            Debug.LogError("exportTexture == null ");
                        }

                        string LocalID = exportTexture.LocalGUID;

                        Text = string.Format( CultureInfo.InvariantCulture,
                              "shader_parameter/{0} = ExtResource(\"{1}\")\n",
                              GodotParamName, LocalID);
                        Params += Text;
                    }
                }
                int propertyCount = ShaderUtil.GetPropertyCount(Material.exportShader.SourceShader);
                for (int i = 0; i < propertyCount; i++)
                {
                    var propType = ShaderUtil.GetPropertyType(Material.exportShader.SourceShader, i);
                    string propName = ShaderUtil.GetPropertyName(Material.exportShader.SourceShader, i);

                    string UnityParamName = propName;
                    string GodotParamName = UnityParamName;
                    SanitizeParamName(ref GodotParamName);

                    ShaderParameter TempShaderParameter = new ShaderParameter();
                    TempShaderParameter.Type = ShaderParameterType.SPT_UNKNOWN;
                    switch (propType)
                    {
                        case ShaderUtil.ShaderPropertyType.Float:
                        case ShaderUtil.ShaderPropertyType.Range:
                            TempShaderParameter.Type = ShaderParameterType.SPT_FLOAT;
                            break;
                        case ShaderUtil.ShaderPropertyType.Int:
                            TempShaderParameter.Type = ShaderParameterType.SPT_INT;
                            break;
                        case ShaderUtil.ShaderPropertyType.Color:
                            //TempShaderParameter.Type = ShaderParameterType.SPT_COLOR;
                            //break;
                        case ShaderUtil.ShaderPropertyType.Vector:
                            TempShaderParameter.Type = ShaderParameterType.SPT_VECTOR4;
                            break;
                    }

                    if (TempShaderParameter.Type != ShaderParameterType.SPT_UNKNOWN)
                    {
                        Text = "";
                        ProcessMaterialParameter(TempShaderParameter, Material.material, UnityParamName, GodotParamName, ref Text);
                        Params += Text;
                    }
                }
            }
            if (Material.exportShader != null && Material.exportShader.ShaderData != null)
            {
                for (int i = 0; i < Material.exportShader.ShaderData.ShaderParameters.Count; i++)
                {
                    ShaderParameter Param = Material.exportShader.ShaderData.ShaderParameters[i];
                    string UnityParamName = Param.ReferenceName;
                    //SanitizeParamNameForUnity( ref UnityParamName );
                    string GodotParamName = Param.SanitizedName;
                    if (Param.Type == ShaderParameterType.SPT_TEXTURE)
                    {
                        string LocalID = "";
                        Texture MaterialTexture = Param.DefaultTexture;
                        Vector4 TillingOffset = new Vector4(1,1,0,0);
                        if (Material.material.HasTexture(UnityParamName))
                        {
                            Texture FetchedTexture = Material.material.GetTexture(UnityParamName);
                            if ( FetchedTexture != null )
                            {
                                MaterialTexture = FetchedTexture;
                                Vector2 Scale = Material.material.GetTextureScale(UnityParamName);
                                Vector2 Offset = Material.material.GetTextureOffset(UnityParamName);
                                TillingOffset = new Vector4( Scale.x, Scale.y, Offset.x, Offset.y );
                            }
                        }
                        else
                        {
                            string[] TextureNames = Material.material.GetTexturePropertyNames();
                            for (int t = 0; t < TextureNames.Length; t++)
                            {

                            }
                        }
                        if (MaterialTexture != null)
                        {
                            ExportTexture NewExportTexture = Material.GetTexture(MaterialTexture);
                            if (NewExportTexture == null)
                            {
                                NewExportTexture = CreateExportTexture(MaterialTexture, Material.Textures.Count + 1);
                                Material.Textures.Add(NewExportTexture);
                            }
                            LocalID = NewExportTexture.LocalGUID;

                            Text = string.Format( CultureInfo.InvariantCulture,
                                  "shader_parameter/{0} = ExtResource(\"{1}\")\n",
                                  GodotParamName, LocalID);

                            Params += Text;

                            //Assumes TilingOffsetParameter always comes AFTER its texture but usually this is the case
                            if ( Param.TilingOffsetParameter != null  )
                            {
                                Param.TilingOffsetParameter.DefaultVector = TillingOffset;
                            }
                        }
                        else
                            Text = "";
                    }
                    else
                    {
                        ProcessMaterialParameter(Param, Material.material, UnityParamName, GodotParamName, ref Text);
                        Params += Text;
                    }
                }

                if ( Material.exportShader.ShaderData.AlphaClipping )
                {
                    ShaderParameter VirtualParam = new ShaderParameter();
                    VirtualParam.ReferenceName = "_AlphaClipping";
                    VirtualParam.Name = "EnableAlphaClipping";
                    VirtualParam.Type = ShaderParameterType.SPT_BOOL;
                    VirtualParam.DefaultBool = true;
                    ProcessMaterialParameter( VirtualParam, Material.material, VirtualParam.ReferenceName, VirtualParam.Name, ref Text);
                    Params += Text;
                }
            }
        }
        else
        {
            string ShaderName = "";
            if ( Material.material.shader != null )
                ShaderName = Material.material.shader.name;
            UnityEngine.Color BaseColor = new UnityEngine.Color(1,1,1,1);

            bool InLinear = true;            
            int Mode = -1;

            if ( GetCurrentRenderPipeline() == "Built-In")//ProRacer project + Particles/Additive requires TintColor to not convert to linear
                InLinear = false;
            if ( GetCurrentRenderPipeline() == "Universal")
            {
                if ( Material.material.IsKeywordEnabled( "_ALPHATEST_ON"))
                    Mode = 1;
            }

            if ( Material.material.HasProperty("_TintColor") )
            { 
                BaseColor = Material.material.GetColor("_TintColor");
                if ( InLinear )
                    BaseColor = BaseColor.linear;
            }
            else if ( Material.material.HasProperty("_Color") )
            {
                BaseColor = Material.material.GetColor("_Color");
                if ( InLinear )
                    BaseColor = BaseColor.linear;
            }

            Text = string.Format( CultureInfo.InvariantCulture,
                "albedo_color = {0}\n",
                    ToString( BaseColor, false  ) );
            Params += Text;

            GodotBlendMode BlendMode = GodotBlendMode.GBM_MIX;
            if ( Material.material.HasProperty("_Mode") )
                Mode = Material.material.GetInt("_Mode");

            if ( Mode == 1 || ShaderName.Contains("Tree Creator Leaves"))
            {
                Params += "transparency = 2\n";//Transparency CutOut
            }
            if ( Mode >= 2 || ShaderName.Contains("Transparent") ||
                              ShaderName.Contains("Particles/Additive"))
            {
                Params += "transparency = 1\n";//Transparency Alpha
                Params += "cull_mode = 2\n";//cull none
            }
            if ( ShaderName.Contains("Unlit") )
            {
                Params += "shading_mode = 0\n";//unshaded
            }
            if ( ShaderName.Contains("Particles/Additive"))
            {
                BlendMode = GodotBlendMode.GBM_ADD;
            }
            Params += string.Format( CultureInfo.InvariantCulture, "blend_mode = {0} ", (int)BlendMode);
            
            string[] TextureNames = Material.material.GetTexturePropertyNames();
            for (int i = 0; i < TextureNames.Length; i++)
            {
                string TextureName = TextureNames[i];
                Texture MaterialTexture = Material.material.GetTexture(TextureName);
                if (MaterialTexture != null)
                {
                    ExportTexture NewExportTexture = Material.GetTexture( MaterialTexture );
                    
                    string LocalID = NewExportTexture.LocalGUID;

                    if ( TextureName.Equals( "_MainTex" ) || TextureName.Equals( "_BaseMap" ))
                    {
                        Text = string.Format( CultureInfo.InvariantCulture,
                                "albedo_texture = ExtResource(\"{0}\")\n",
                              LocalID );
                        Params += Text;
                    }
                    else if ( TextureName.Equals( "_MetallicGlossMap" ) ||
                              TextureName.Equals( "_MaskMap" ))
                    {
                        float Metallic = 1.0f;
                        //In BuiltIn/Standard there's no metallic multiplier !
                        //if ( Material.material.HasProperty("_Metallic"))
                            //Metallic = Material.material.GetFloat("_Metallic");
                        float Roughness = 1.0f;
                        if ( Material.material.HasProperty("_Smoothness"))
                            Roughness = 1.0f - Material.material.GetFloat("_Smoothness");
                        Text = string.Format( CultureInfo.InvariantCulture,
                              "metallic = {1:0.0#}\n" +
						        "metallic_texture = ExtResource(\"{0}\")\n" +
						        "metallic_texture_channel = 0\n" +
                                "roughness = {2:0.0#}\n" +
						        "roughness_texture = ExtResource(\"{0}\")\n" +
						        "roughness_texture_channel = 3\n",
                                LocalID, Metallic, Roughness );
                        Params += Text;

                        if ( TextureName.Equals( "_MaskMap" ))
                        {
                            Text = string.Format( CultureInfo.InvariantCulture,
                                "ao_enabled = true\n" +
						        "ao_texture = ExtResource(\"{0}\")\n" +
                                "ao_texture_channel = 1\n",
                                LocalID);
                            Params += Text;
                        }
                    }
                    else if ( TextureName.Equals( "_BumpMap", StringComparison.OrdinalIgnoreCase) ||
                              TextureName.Equals( "_NormalMap", StringComparison.OrdinalIgnoreCase))
                    {
                        Text = string.Format( CultureInfo.InvariantCulture,
                                "normal_enabled = true\n" +
						        "normal_texture = ExtResource(\"{0}\")\n",
                                LocalID);
                        Params += Text;
                    }
                    else if ( TextureName.Equals( "_EmissionMap" ))
                    {
                        var emissionColor = Material.material.GetColor("_EmissionColor");
                        Text = string.Format( CultureInfo.InvariantCulture,
                                "emission_enabled = true\n" +
                                //"emission = {1}\n" +
						        "emission_texture = ExtResource(\"{0}\")\n",
                                LocalID, ToString( emissionColor, false ));
                        Params += Text;
                    }
                    else if ( TextureName.Equals( "_OcclusionMap" ))
                    {
                        //var OcclusionStrength = Material.material.GetFloat("_OcclusionStrength");

                        Text = string.Format( CultureInfo.InvariantCulture,
                                "ao_enabled = true\n" +
						        "ao_texture = ExtResource(\"{0}\")\n" +
                                "ao_texture_channel = 1\n",
                                LocalID);
                        Params += Text;
                    }
                    else if ( TextureName.Equals( "_DetailMask" ))
                    {
                        Text = string.Format( CultureInfo.InvariantCulture,
						        "detail_mask = ExtResource(\"{0}\")\n",
                                LocalID);
                        Params += Text;
                    }
                    else if ( TextureName.Equals( "_DetailAlbedoMap" ))
                    {
                        Text = string.Format( CultureInfo.InvariantCulture,
                                //"detail_enabled = true" +
                                "detail_blend_mode = 3" +
						        "detail_albedo = ExtResource(\"{0}\")\n",
                                LocalID);
                        Params += Text;
                    }
                    else if ( TextureName.Equals( "_DetailNormalMap" ))
                    {
                        Text = string.Format( CultureInfo.InvariantCulture,
						        "detail_normal = ExtResource(\"{0}\")\n",
                                LocalID);
                        Params += Text;
                    }
                }
            }

            if ( (!Material.material.HasProperty("_EmissionMap") || !Material.material.GetTexture("_EmissionMap"))
                && Material.material.HasProperty("_EmissionColor"))
            {
                bool EnableEmission = true;
                if ( GetCurrentRenderPipeline() == "Built-In" || 
                     GetCurrentRenderPipeline() == "Universal" )
                {
                    EnableEmission = Material.material.IsKeywordEnabled( "_EMISSION");
                }
                if ( EnableEmission )
                {
                    var emissionColor = Material.material.GetColor("_EmissionColor");
                    Text = string.Format( CultureInfo.InvariantCulture,
                                "emission_enabled = true\n" +
                                "emission = {0}\n",
						        ToString( emissionColor, false ) );
                     Params += Text;
                }
            }
            if ( (!Material.material.HasProperty("_MaskMap") || !Material.material.GetTexture("_MaskMap"))
                && Material.material.HasProperty("_Smoothness"))
            {
                float Smoothness = Material.material.GetFloat("_Smoothness");
                Text = string.Format( CultureInfo.InvariantCulture,
                            "roughness = {0:0.0#}\n",
						    1.0 - Smoothness );
                 Params += Text;
            }
        }
        int LoadSteps = Material.Textures.Count + 1;
        
        Text = string.Format( CultureInfo.InvariantCulture,
                  "[gd_resource type = \"{0}\" load_steps={1} format=3 uid=\"uid://{2}\"]\n\n",
                  ResourceType, LoadSteps, Material.GUID);
        Ret += Text;

        string ExtResources = "";

        string ShaderGUID = "";
        if ( Material.exportShader != null )
        { 
            if (Material.exportShader.DefinitionType == ShaderDefinitionType.SDT_SHADERGRAPH)
                ShaderGUID = Material.exportShader.ShaderData.GUID;
            else if (Material.exportShader.DefinitionType == ShaderDefinitionType.SDT_SURFACESHADER)
                ShaderGUID = Material.exportShader.GUID;
        }
        if ( Material.exportShader != null )
        {
            string LocalGUID = GetGUID5(Material.exportShader.SourceShader);

            string LocalID = "";
            LocalID = string.Format( CultureInfo.InvariantCulture,"{0}_{1}", 1, LocalGUID);
            Material.exportShader.LocalGUID = LocalID;

            string NewExtResource = GenerateExternalResourceReference("Shader", ShaderGUID, Material.exportShader.ResourcePath, LocalID);
            ExtResources += NewExtResource;
        }
        for (int i = 0; i < Material.Textures.Count; i++)
        {
            ExportTexture Tex = Material.Textures[i];
            if (Tex == null)
                continue;
            if (Tex.GUID.Length == 0)
                Tex.GUID = GetGodotGUID(Tex.Tex);
            string Type = GetTextureTypeForResource(Tex.Tex);
            string NewExtResource = GenerateExternalResourceReference(Type, Tex.GUID, Tex.ResourcePath, Tex.LocalGUID);
            ExtResources += NewExtResource;
        }

        Ret += ExtResources;
        Ret += "\n";

        Text = string.Format( CultureInfo.InvariantCulture,
                  "[resource]\n" +
                  "resource_name = \"{0}\"\n",
                  Material.material.name
                );
        Ret += Text;

        if ( Material.exportShader != null )
        {
            Text = string.Format( CultureInfo.InvariantCulture,
                      "render_priority = 0\n" +
                      "shader = ExtResource(\"{0}\")\n",
                      Material.exportShader.LocalGUID
                          );
            Ret += Text;
        }

        Ret += Params;

        return Ret;
    }
    static void WriteUIs(ExportScene Scene)
    {        
        try
        {
            for (int i = 0; i < Scene.CanvasRoots.Count; i++)
            {
                GameObject CanvasGO = Scene.CanvasRoots[i];
                StringBuilder SceneText = new StringBuilder();
                string Text = Scene.GetUISceneText( CanvasGO );
                SceneText.Append( Text );

                //for (int c = 0; c < CanvasGO.transform.childCount; c++)
                //{
                //    string ChildText = Scene.GetUISceneText( CanvasGO.transform.GetChild( c ).gameObject );
                //    SceneText.Append( ChildText );
                //}
                
                StringBuilder FileData = new StringBuilder();
                string SceneGUID = GetGodotGUID(CanvasGO.GetHashCode());
                int LoadSteps = 1;
                FileData.Append(string.Format( CultureInfo.InvariantCulture,"[gd_scene load_steps={0} format=3 uid=\"uid://{1}\"]\n\n", LoadSteps, SceneGUID));
                
                //FileData.Append( string.Format( CultureInfo.InvariantCulture,"\n" +
                //        "[node name=\"{0}\" type=\"Control\"]\n" +
                //        "layout_mode = 3\r\n" +
                //        "anchors_preset = 15\r\n" +
                //        "anchor_right = 1.0\r\n" +
                //        "anchor_bottom = 1.0\r\n" +
                //        "grow_horizontal = 2\r\n" +
                //        "grow_vertical = 2\n" +
                //        "\n", CanvasGO.name ));

                FileData.Append( SceneText );

                string SceneName = CanvasGO.name;
                if (SceneName == null || SceneName.Length == 0)
                    SceneName = "Untitled";
                File.WriteAllText(ExportFolder + "/" + SceneName + ".tscn", FileData.ToString());
            }
        }
        catch (Exception E)
        {
            Debug.LogError(E.Message + "\n" + E.StackTrace );
        }
    }
    static void WriteScene(ExportScene Scene)
    {
        Scene UnityScene = SceneManager.GetActiveScene();
        GameObject[] gameObjects = GetAllRootGameObjects();

        for (int i = 0; i < Scene.MeshesToExport.Count; i++)
        {
            ExportMesh exportMesh = Scene.MeshesToExport[i];
            int LocalIndex = 1 + Scene.Textures.Count + i;
            exportMesh.LocalGUID = GenerateLocalGUID(exportMesh.mesh, LocalIndex);
        }

        StringBuilder GameObjectsSceneData = new StringBuilder();
        Scene.CurrentNodes = 0;
        try
        {
            for (int i = 0; i < gameObjects.Length; i++)
            {
                GameObject GO = gameObjects[i];
                string GOSceneText = Scene.GetSceneText(GO);
                GameObjectsSceneData.Append( GOSceneText );
            }
        }
        catch (Exception E)
        {
            Debug.LogError(E.Message + "\n" + E.StackTrace );
        }

        EditorUtility.ClearProgressBar();

        string TextureResourceReferences = "";
        ExportTextures(ref TextureResourceReferences, Scene.Textures);

        bool AddWorldEnvironment = false;
        int LoadSteps = Scene.MeshesToExport.Count + Scene.Textures.Count + 1;
        if (AddWorldEnvironment)
            LoadSteps++;

        string SceneGUID = GetGodotGUID(UnityScene.GetHashCode());
        StringBuilder FileData = new StringBuilder();
        FileData.Append(string.Format( CultureInfo.InvariantCulture,"[gd_scene load_steps={0} format=3 uid=\"uid://{1}\"]\n\n", LoadSteps, SceneGUID));

        for (int i = 0; i < Scene.MeshesToExport.Count; i++)
        {
            ExportMesh exportMesh = Scene.MeshesToExport[i];
            string Type = "PackedScene";
            string ExtResource = string.Format( CultureInfo.InvariantCulture,"[ext_resource type=\"{0}\" uid=\"uid://{1}\" path=\"res://{2}\" id=\"{3}\"]\n",
                Type, exportMesh.PrefabGUID, exportMesh.PrefabResourcePath, exportMesh.LocalGUID);
            FileData.Append(ExtResource);
        }

        FileData.Append(TextureResourceReferences);

        FileData.Append(Scene.SubresourcesText);

        FileData.Append("\n" +
                "[node name=\"Root\" type=\"Node3D\"]\n" +
                "\n");

        FileData.Append(GameObjectsSceneData);

        string SceneName = UnityScene.name;
        if (UnityScene.name == null || UnityScene.name.Length == 0)
            SceneName = "Untitled";
        File.WriteAllText(ExportFolder + "/" + SceneName + ".tscn", FileData.ToString());
    }
    public enum ShaderDefinitionType
    {
        //SDT_STANDARD,
        SDT_SHADERGRAPH,
        SDT_SURFACESHADER
    };
    public class ExportShader
    {
        public Shader SourceShader;
        public GodotShaderData ShaderData;
        public string LocalGUID;
        public string ResourcePath;
        public string GUID;

        public ShaderDefinitionType DefinitionType = ShaderDefinitionType.SDT_SHADERGRAPH;
    }
    public class ExportMaterial
    {
        public ExportShader exportShader;
        public Material material;
        public string File;
        public string GUID;
        public string ResourcePath;
        public List<ExportTexture> Textures = new List<ExportTexture>();
        public ExportTexture GetTexture(Texture Tex)
        {
            ExportTexture Ret = Textures.Find(Element => Element.Tex == Tex);
            return Ret;
        }
    }
    public class ExportMesh
    {
        public GameObject GO;
        public Mesh mesh;
        public Transform[] bones;
        public Transform RootBone;
        public string MeshGUID;
        public string MeshName;
        public string ResourcePath;
        public string AssetName;
        public string AssetPath;
        public string LocalGUID;
        public Material[] OriginalMaterials;
        public ExportMaterial[] ExportMaterials;

        public string PrefabGUID;
        public string PrefabResourcePath;
        public string GetMaterialNames()
        {
            StringBuilder sb = new StringBuilder();
            if ( OriginalMaterials != null )
            {
                for(int i =0; i<OriginalMaterials.Length; i++)
                {
                    if ( i > 0 )
                        sb.Append("_");
                    if ( OriginalMaterials[i] == null )
                        sb.Append( "null" );
                    else
                    {
                        string MatName = OriginalMaterials[i].name;
                        SanitizeMaterialName( ref MatName );
                        sb.Append( MatName );
                    }
                }

                return sb.ToString();
            }
            else
                return "null";
        }
    }

    public static int MaxAssetLength = 100;
    public class ExportScene
    {
        public List<ExportMesh> MeshesToExport = new List<ExportMesh>();
        public List<ExportShader> ExportShaders = new List<ExportShader>();
        public List<ExportMaterial> AllMaterials = new List<ExportMaterial>();
        public List<ExportTexture> Textures = new List<ExportTexture>();
        public Stack<GameObject> ProcessingStack = new Stack<GameObject>();

        public Dictionary<Component, string> ShapesSubresourceIDs = new Dictionary<Component, string>();
        public string SubresourcesText = "";
        public int TotalNodes = 0;
        public int CurrentNodes = 0;

        public List<GameObject> CanvasRoots = new List<GameObject>();

        public ExportMesh GetPrefabTemplate(GameObject GO)
        {
            Mesh mesh = GetMesh(GO);
            Material[] Materials = GetMaterials(GO);
            if (mesh == null)
                return null;

            for (int i = 0; i < MeshesToExport.Count; i++)
            {
                ExportMesh Entry = MeshesToExport[i];
                bool SameMaterials = AreMaterialsEqual(Entry.OriginalMaterials, Materials);
                if (Entry.mesh == mesh && SameMaterials)
                    return Entry;
            }
            return null;
        }
        string AppendStrToFileName(string Str, string Append)
        {
            string Ext = GetExtension(Str);
            string Result = RemoveExtension(Str);
            Result += Append;
            Result += GetExtension(Str);
            return Result;
        }
        public string FixDuplicateMeshGUIDs(string GUID)
        {
            for (int i = 0; i < MeshesToExport.Count; i++)
            {
                var PrefabA = MeshesToExport[i];
                if (PrefabA.MeshGUID.Equals(GUID))
                {
                    return GetGodotGUID(null);
                }
            }
            return GUID;
        }
        public Texture GetTextureWithPotentialNames(Material Mat, params string[] PotentialNames)
        {
            foreach (var n in PotentialNames)
            {
                string Name = n;
                if (Mat.HasProperty(Name))
                {
                    Texture T = Mat.GetTexture(Name);
                    if (T != null)
                        return T;
                }
                Name = "_" + Name;
                if (Mat.HasProperty(Name))
                {
                    Texture T = Mat.GetTexture(Name);
                    if (T != null)
                        return T;
                }
            }

            return null;
        }
        
        public void FixDuplicatePrefabNames()
        {
            if (MeshesToExport.Count == 0)
                return;
            for (int i = 0; i < MeshesToExport.Count - 1; i++)
            {
                var PrefabA = MeshesToExport[i];
                int Instances = 1;
                for (int u = i + 1; u < MeshesToExport.Count; u++)
                {
                    var PrefabB = MeshesToExport[u];
                    //compare disregarding case
                    if (PrefabA.AssetName.Equals(PrefabB.AssetName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        string MaterialNames = PrefabB.GetMaterialNames();
                        if ( MaterialNames.Length > MaxAssetLength )
                        {
                            MaterialNames = "_" + Instances;
                        }
                        if ( PrefabB.AssetName.Length > MaxAssetLength )
                        {
                            PrefabB.AssetName = PrefabB.AssetName.Substring( 0, MaxAssetLength ) + "_Length_Max" + Instances;
                        }
                        PrefabB.AssetName += MaterialNames;//"_" + Instances;
                        
                        PrefabB.ResourcePath = AppendStrToFileName(PrefabB.ResourcePath, "_" + Instances);
                        PrefabB.MeshGUID = GetGodotGUID(null);//generates fresh new GUID
                        Instances++;
                    }
                }
            }
        }
        #if USING_URP
        public static UnityEngine.Rendering.Universal.DecalProjector ProcessDecalURP( GameObject GO, ref string NodeType, ref bool ForDecal )
        {
            UnityEngine.Rendering.Universal.DecalProjector Decal = GO.GetComponent<UnityEngine.Rendering.Universal.DecalProjector>();
            if (Decal != null)
            {
                //In Unity decal scale has no effect but in Godot it does so treat it as unit
                Vector3 Scale = GO.transform.localScale;
                if (Decal.scaleMode == Decal.scaleMode)
                    Scale = new Vector3(1, 1, 1);

                Scale.x *= -1;//Flips UVs
                GO.transform.localScale = Scale;

                NodeType = "Decal";
                ForDecal = true;
                Vector3 PivotX = GO.transform.right * Decal.pivot.x;
                Vector3 PivotY = GO.transform.up * Decal.pivot.y;
                Vector3 PivotZ = GO.transform.forward * Decal.pivot.z;
                GO.transform.position += PivotX;
                GO.transform.position += PivotY;
                GO.transform.position += PivotZ;
                Decal.pivot = Vector3.zero;
            }
            return Decal;
        }
        #endif
        #if USING_HDRP
        public static UnityEngine.Rendering.HighDefinition.DecalProjector ProcessDecalHDRP( GameObject GO, ref string NodeType, ref bool ForDecal )
        {
            UnityEngine.Rendering.HighDefinition.DecalProjector Decal = GO.GetComponent<UnityEngine.Rendering.HighDefinition.DecalProjector>();
            if (Decal != null)
            {
                //In Unity decal scale has no effect but in Godot it does so treat it as unit
                Vector3 Scale = GO.transform.localScale;
                if (Decal.scaleMode == Decal.scaleMode)
                    Scale = new Vector3(1, 1, 1);

                Scale.x *= -1;//Flips UVs
                GO.transform.localScale = Scale;

                NodeType = "Decal";
                ForDecal = true;
                Vector3 PivotX = GO.transform.right * Decal.pivot.x;
                Vector3 PivotY = GO.transform.up * Decal.pivot.y;
                Vector3 PivotZ = GO.transform.forward * Decal.pivot.z;
                GO.transform.position += PivotX;
                GO.transform.position += PivotY;
                GO.transform.position += PivotZ;
                Decal.pivot = Vector3.zero;
            }
            return Decal;
        }
        #endif
        public const float SphereSolidAngle = 4.0f * Mathf.PI;
        public static float GetSolidAngleFromPointLight()
        {
            return SphereSolidAngle;
        }

        /// <summary>
        /// Get the solid angle of a Spot light.
        /// </summary>
        /// <param name="spotAngle">The spot angle in degrees.</param>
        /// <returns>Solid angle in steradians.</returns>
        public static float GetSolidAngleFromSpotLight(float spotAngle)
        {
            double angle = Math.PI * spotAngle / 180.0;
            double solidAngle = 2.0 * Math.PI * (1.0 - Math.Cos(angle * 0.5));
            return (float)solidAngle;
        }

        /// <summary>
        /// Get the solid angle of a Pyramid light.
        /// </summary>
        /// <param name="spotAngle">The spot angle in degrees.</param>
        /// <param name="aspectRatio">The aspect ratio of the pyramid.</param>
        /// <returns>Solid angle in steradians.</returns>
        public static float GetSolidAngleFromPyramidLight(float spotAngle, float aspectRatio)
        {
            if (aspectRatio < 1.0f)
            {
                aspectRatio = (float)(1.0 / aspectRatio);
            }

            double angleA = Math.PI * spotAngle / 180.0;
            double length = Math.Tan(0.5 * angleA) * aspectRatio;
            double angleB = Math.Atan(length) * 2.0;
            double solidAngle = 4.0 * Math.Asin(Math.Sin(angleA * 0.5) * Math.Sin(angleB * 0.5));
            return (float)solidAngle;
        }
        internal static float GetSolidAngle(LightType lightType, bool spotReflector, float spotAngle, float aspectRatio)
        {
            return lightType switch
            {
                LightType.Spot => spotReflector ? GetSolidAngleFromSpotLight(spotAngle) : SphereSolidAngle,
                #if UNITY_2023_3_OR_NEWER
                LightType.Pyramid => spotReflector ? GetSolidAngleFromPyramidLight(spotAngle, aspectRatio) : SphereSolidAngle,
                #endif
                LightType.Point => GetSolidAngleFromPointLight(),
                _ => throw new ArgumentException("Solid angle is undefined for lights of type " + lightType)
            };
        }
        public static float CandelaToLumen(float candela, float solidAngle)
        {
            return candela * solidAngle;
        }
        public static float ConvertIntensity_CandelaToLumen(Light light, float intensity )
        {
            LightType lightType = light.type;
            
            bool spotReflector = false;
            #if UNITY_2023_3_OR_NEWER
                spotReflector = light.enableSpotReflector;
            #endif
            float solidAngle = lightType switch
            {
                LightType.Spot or 
                #if UNITY_2023_3_OR_NEWER
                LightType.Pyramid or
                #endif
                LightType.Point => GetSolidAngle(lightType, spotReflector,
                    light.spotAngle, light.areaSize.x), // Pyramid aspect ratio is store in areaSize.x
                _ => 0.0f
            };

            return CandelaToLumen(intensity, solidAngle);
        }
        public int GetExportableComponents(GameObject GO)
        {
            int Sum = 0;
            ExportMesh PrefabTemplate = GetPrefabTemplate(GO);
            if (PrefabTemplate != null)
            {
                Sum++;
            }

            #if USING_URP
            UnityEngine.Rendering.Universal.DecalProjector Decal1 = GO.GetComponent<UnityEngine.Rendering.Universal.DecalProjector>();
            if (Decal1 != null)
            {
                Sum++;
            }
            #endif
            #if USING_HDRP
            UnityEngine.Rendering.HighDefinition.DecalProjector Decal2 = GO.GetComponent<UnityEngine.Rendering.HighDefinition.DecalProjector>();
            if (Decal2 != null)
            {
                Sum++;
            }
            #endif
            Light L = GO.GetComponent<Light>();
            if (L != null)
            {
                Sum += 1;
            }
            List<Component> PhysicsShapes = GetPhysicsShapes(GO);
            if (PhysicsShapes.Count > 0)
            {
                Sum += PhysicsShapes.Count;
            }
            for (int i = 0; i < GO.transform.childCount; i++)
            {
                GameObject ChildGO = GO.transform.GetChild(i).gameObject;
                if (!ChildGO.activeInHierarchy)
                    continue;
                Sum += GetExportableComponents(ChildGO);
            }
            return Sum;
        }
        public string GetUISceneText(GameObject GO)
        {
            string ParentsText = GenerateParentsText(GO);
            StringBuilder Ret = new StringBuilder();
            string NodeType = "Control";
            string Text = "";
            Canvas canvas = GO.GetComponent<Canvas>();
            RectTransform ParentRT = null;
            if ( GO.transform.parent != null )
            {
                ParentRT = GO.transform.parent.GetComponent<RectTransform>();
            }
            RectTransform rt = GO.GetComponent<RectTransform>();
            Button button = GO.GetComponent<Button>();
            Image image = GO.GetComponent<Image>();
            if (canvas != null)
            {
                Vector2 Size = rt.rect.size;
                Text += string.Format( CultureInfo.InvariantCulture, 
                        "layout_mode = 3\r\n" +
                        "anchors_preset = 15\r\n" +
                        "anchor_right = 1.0\r\n" +
                        "anchor_bottom = 1.0\r\n" +
                        "grow_horizontal = 2\r\n" +
                        "grow_vertical = 2\n" +
                        "offset_right = {0}\r\n" +
                        "offset_bottom = {1}\r\n",
                        Size.x / 2.5, Size.y / 2.5 );// no idea why the 2.5 really
            }
            else if ( rt != null )
            {
                Vector2 ParentSize = new Vector2(0,0);
                if ( ParentRT != null )
                    ParentSize = ParentRT.rect.size;
                Vector2 Pos = rt.rect.position + new Vector2( ParentSize.x/2, ParentSize.y/2 );
                Vector2 Size = rt.rect.size;
                Text += string.Format( CultureInfo.InvariantCulture, "offset_left = {0}\n", Pos.x );
                Text += string.Format( CultureInfo.InvariantCulture, "offset_top = {0}\n", Pos.y );
                Text += string.Format( CultureInfo.InvariantCulture, "offset_right = {0}\n", Pos.x + Size.x );
                Text += string.Format( CultureInfo.InvariantCulture, "offset_bottom = {0}\n", Pos.y + Size.y );
            }

            if ( button != null )
            {
                NodeType = "Button";
                //button.image.material;
            }
            
            if ( image != null )
            {
                NodeType = "TextureRect";
                //image.material;
            }
            
            string NodeText = GenerateNodeText( GO.name, NodeType, ParentsText, null, true );
            Ret.Append(NodeText);
            Ret.Append(Text);

            for (int i = 0; i < GO.transform.childCount; i++)
            {
                GameObject ChildGO = GO.transform.GetChild(i).gameObject;
                if (!ChildGO.activeInHierarchy)
                    continue;
                string SceneText = GetUISceneText(ChildGO);
                Ret.Append(SceneText);
            }

            return Ret.ToString();
        }
        #endif
        public string GetSceneText(GameObject GO)
        {
            CurrentNodes++;
            float Completion = (float)CurrentNodes / (float)TotalNodes;
            if ( EditorUtility.DisplayCancelableProgressBar("Exporting Scene... ", "Exporting Scene " + CurrentNodes + " / " + TotalNodes + " GameObjects", Completion))
            {
                EditorUtility.ClearProgressBar();
                return "";
            }

            string ParentsText = GenerateParentsText(GO);

            StringBuilder Ret = new StringBuilder();
            ExportMesh PrefabTemplate = GetPrefabTemplate(GO);
            if (PrefabTemplate != null)
            {
                string NodeText = GenerateNodeText(GO.name, null, ParentsText, PrefabTemplate.LocalGUID);
                string TransformText = GenerateTransformText(GO.transform);
                Ret.Append(NodeText);
                Ret.Append(TransformText);
            }
            else
            {
                int ExportableComponents = GetExportableComponents( GO );
                if ( RemoveEmptyNodes && ExportableComponents == 0 )
                    return "";
                //string NodeName = "";
                string NodeType = "Node3D";
                string Text = "";

                bool ForDecal = false;
                #if USING_URP
                    var Decal = ProcessDecalURP( GO, ref NodeType, ref ForDecal );
                #elif USING_HDRP
                    var Decal = ProcessDecalHDRP( GO, ref NodeType, ref ForDecal );
                #endif

                bool ForDirectionalLight = false;
                Light L = GO.GetComponent<Light>();
                if (L != null)
                {
                    ForDirectionalLight = true;
                    NodeType = "OmniLight3D";

                    if (L.type == LightType.Directional)
                    {
                        NodeType = "DirectionalLight3D";
                    }
                    else if (L.type == LightType.Spot)
                    {
                        NodeType = "SpotLight3D";
                    }
                }
                List<Component> PhysicsShapes = GetPhysicsShapes(GO);
                if (PhysicsShapes.Count > 0)
                {
                    string PhysicsSubResources = GeneratePhysicsSubresources(PhysicsShapes, ref ShapesSubresourceIDs);
                    SubresourcesText += PhysicsSubResources;
                }

                string NodeText = GenerateNodeText(GO.name, NodeType, ParentsText, null);
                string TransformText = GenerateTransformText(GO.transform, ForDirectionalLight, ForDecal);
                Ret.Append(NodeText);
                Ret.Append(TransformText);

                if (L != null)
                {
                    if (L.shadows != LightShadows.None)
                    {
                        Ret.Append("shadow_enabled = true\n");
                    }
                    float Energy = 16.0f;
                    float FinalIntensity = L.intensity;
                    Vector4 ColorFloat = L.color;
#if USING_HDRP
                    HDAdditionalLightData hdLight = GO.GetComponent<HDAdditionalLightData>();
                    if (hdLight != null && (L.type == LightType.Point || L.type == LightType.Spot))
                    {
                        //LightUnitUtils.ConvertIntensity was used
                        float IntensityInLumens = ConvertIntensity_CandelaToLumen(L, hdLight.intensity );
                        FinalIntensity = IntensityInLumens;
                    }
#endif
                    if ( L.useColorTemperature )
                    {
                        Color TemperatureRGB = Mathf.CorrelatedColorTemperatureToRGB(L.colorTemperature);
                        TemperatureRGB = TemperatureRGB.gamma;
                        ColorFloat *= TemperatureRGB;
                    }

                    if (L.type == LightType.Point)
                    {
                        FinalIntensity *= PointLightIntensityMultiplier;

                        Text = string.Format( CultureInfo.InvariantCulture,
                                  "omni_range = {0}\n",
                                  L.range);
                        Ret.Append(Text);
                    }
                    if (L.type == LightType.Spot)
                    {
                        FinalIntensity *= SpotLightIntensityMultiplier;

                        Text = string.Format( CultureInfo.InvariantCulture,
                                  "spot_range = {0}\n" +
                                  "spot_angle = {1}\n"
                                  ,
                                  L.range,
                                  L.spotAngle * 0.5);
                        Ret.Append(Text);
                    }

                    if (L.type == LightType.Directional)
                    {
                        FinalIntensity *= DirectionalLightIntensityMultiplier;
#if USING_HDRP
                        //Seems to match well
                        Energy = 1.0f;
#endif
                    }

                    Text = string.Format( CultureInfo.InvariantCulture,
                             "light_intensity_lumens = {0}\n" +
                             "light_color = Color( {1}, {2}, {3}, {4} )\n" +
                             "light_energy = {5}\n",
                              //light_temperature
                              FinalIntensity,
                              ColorFloat.x, ColorFloat.y, ColorFloat.z, ColorFloat.w,
                              Energy);

                    Ret.Append(Text);
                }
#if USING_URP || USING_HDRP
                if (Decal != null)
                {
                    Vector3 Size = Decal.size;

                    Decal.pivot = Vector3.zero;
                    bool SwapAxis = true;
                    if (SwapAxis)
                    {
                        float temp = Size.y;
                        Size.y = Size.z;
                        Size.z = temp;
                    }
                    Text = string.Format( CultureInfo.InvariantCulture,"size = Vector3( {0}, {1}, {2} )\n", Size.x, Size.y, Size.z);
                    Ret.Append(Text);

                    Texture Albedo = null;
                    Texture Normal = null;
                    if (Decal.material != null)
                    {
                        Albedo = GetTextureWithPotentialNames(Decal.material, "Base_Map", "BaseMap", "Albedo");
                        Normal = GetTextureWithPotentialNames(Decal.material, "Normal", "NormalMap");
                    }
                    if (Albedo != null)
                    {
                        ExportTexture Tex = AddExportTexture(Albedo, ref Textures);

                        Text = string.Format( CultureInfo.InvariantCulture,"texture_albedo = ExtResource(\"{0}\")\n", Tex.LocalGUID);
                        Ret.Append(Text);
                    }
                    if (Normal != null)
                    {
                        ExportTexture Tex = AddExportTexture(Normal, ref Textures);

                        Text = string.Format( CultureInfo.InvariantCulture,"texture_normal = ExtResource(\"{0}\")\n", Tex.LocalGUID);
                        Ret.Append(Text);
                    }
                }
#endif
                if (PhysicsShapes.Count > 0)
                {
                    string PhysicsShapesReferences = GenerateStaticBody(PhysicsShapes, ShapesSubresourceIDs, GO.name + "_StaticBody",
                        ParentsText + "/" + GO.name);
                    Ret.Append(PhysicsShapesReferences);
                }
            }

            Ret.Append("\n");

            for (int i = 0; i < GO.transform.childCount; i++)
            {
                GameObject ChildGO = GO.transform.GetChild(i).gameObject;
                if (!ChildGO.activeInHierarchy)
                    continue;
                string SceneText = GetSceneText(ChildGO);
                Ret.Append(SceneText);
            }

            return Ret.ToString();
        }

        Dictionary<string, GameObject> Names = new Dictionary<string, GameObject>();
        public void PreprocessScene(GameObject GO, ref int TotalNodes )
        {
            TotalNodes++;

            if (Names.ContainsKey(GO.name) )
            {
                GO.name += "_" + Names.Count;
            }
            if ( GO.name.Equals("") )//FileStream doesn't accept null or _ named file
            {
                GO.name += "Unknown_" + Names.Count;
            }
            Canvas canvas = GO.GetComponent<Canvas>();
            if ( canvas != null )
            {
                //Make it Root so Parents string is correct
                GO.transform.SetParent( null, true );
                CanvasRoots.Add( GO );
            }

            Names.Add(GO.name, GO);

            for (int i = 0; i < GO.transform.childCount; i++)
            {
                var Child = GO.transform.GetChild(i);
                PreprocessScene( Child.gameObject, ref TotalNodes );
            }
        }
        public int PreprocessScene()
        {
            Names.Clear();

            int TotalNodes = 0;
            GameObject[] Roots = GetAllRootGameObjects();
            foreach (GameObject go in Roots)
            {
                PreprocessScene( go, ref TotalNodes );
            }

            return TotalNodes;
        }
    }
    static void PreprocessGameObject(GameObject GO)
    {
        if (ConvertSkinnedToStatic)
        {
            SkinnedMeshRenderer TargetSMR = GO.GetComponent<SkinnedMeshRenderer>();
            Cloth ClothComp = GO.GetComponent<Cloth>();
            if (TargetSMR != null)
            {
                MeshFilter TargetMF = GO.GetComponent<MeshFilter>();
                if (TargetMF == null)
                    TargetMF = GO.AddComponent<MeshFilter>();
                TargetMF.mesh = TargetSMR.sharedMesh;

                MeshRenderer TargetMR = GO.GetComponent<MeshRenderer>();
                if (TargetMR == null)
                    TargetMR = GO.AddComponent<MeshRenderer>();
                TargetMR.sharedMaterials = TargetSMR.sharedMaterials;

                TargetSMR.enabled = false;
                if (ClothComp != null)
                {
                    ClothComp.enabled = false;
                    DestroyImmediate(ClothComp);
                }
                DestroyImmediate(TargetSMR);
            }
        }
    }
    public static void ExportGameObject(GameObject GO, ref ExportScene Scene)
    {
        //Ignore disabled GOs
        if (!GO.activeInHierarchy)
        {
            return;
        }
        Scene.CurrentNodes++;
        if( EditorUtility.DisplayCancelableProgressBar("Gathering GameObjects to export...", "Gathering GameObjects to export " +  Scene.CurrentNodes + "/" + Scene.TotalNodes,
            (float)Scene.CurrentNodes /(float)Scene.TotalNodes ) )
        {
            EditorUtility.ClearProgressBar();
            return;
        }

        Scene.ProcessingStack.Push(GO);
        PreprocessGameObject(GO);
        Mesh mesh = GetMesh(GO);
        if (mesh != null)
        {
            Material[] Materials = GetMaterials(GO);

            if (!IsAlreadyInExportList(Scene.MeshesToExport, GO))
            {
                ExportMesh NewExportMesh = new ExportMesh();
                NewExportMesh.GO = GO;
                NewExportMesh.mesh = GetMesh(GO);
                GetBones(GO, ref NewExportMesh.bones, ref NewExportMesh.RootBone);
                NewExportMesh.MeshGUID = GetGodotGUID(NewExportMesh.mesh);
                NewExportMesh.MeshGUID = Scene.FixDuplicateMeshGUIDs(NewExportMesh.MeshGUID);
                NewExportMesh.AssetName = "";
                NewExportMesh.MeshName = SanitizeMeshName(mesh.name);
                NewExportMesh.AssetPath = "";
                GetMeshPathForGameObject(GO, ref NewExportMesh.AssetName, ref NewExportMesh.AssetPath);
                if ( NewExportMesh.AssetName.Length > MaxAssetLength )
                {
                    NewExportMesh.AssetName = NewExportMesh.AssetName.Substring( 0, MaxAssetLength ) + "_Length_Max!";
                }
                NewExportMesh.ResourcePath = NewExportMesh.AssetPath + NewExportMesh.AssetName + ".glb";
                NewExportMesh.OriginalMaterials = Materials;

                ExportMaterial[] ExportMaterials = new ExportMaterial[Materials.Length];
                for (int m = 0; m < Materials.Length; m++)
                {
                    Material Mat = Materials[m];
                    if (Mat == null)
                        continue;
                    Shader shader = Mat.shader;
                    if ( shader == null )
                        continue;
                    
                    string path = AssetDatabase.GetAssetPath(shader);
                    if ( path.Contains("unity_builtin_extra"))
                    {
                        Debug.LogWarning("Legacy shader " + shader.name + " cannot be exported");
                    }
                    if ( EmbedTexturesInGLB )
                    { 
                        if (string.IsNullOrEmpty(path) || !File.Exists(path))
                            continue;
                    }

                    bool IsShaderGraph = path.Contains(".shadergraph");
                    bool SurfaceShader = IsSurfaceShader(path);
                    //if (IsShaderGraph || SurfaceShader)
                    {
                        ExportMaterial NewExportMaterial = new ExportMaterial();
                        ExportShader exportShader = Scene.ExportShaders.Find(Element => Element.SourceShader == shader);
                        if (exportShader == null && (IsShaderGraph || SurfaceShader) )
                        {
                            exportShader = new ExportShader();
                            exportShader.SourceShader = shader;
                            exportShader.GUID = GetGodotGUID(path.GetHashCode());
                            exportShader.ResourcePath = GetResourcePath(shader);
                            if (IsShaderGraph)
                            { 
                                exportShader.ResourcePath = exportShader.ResourcePath.Replace(".shadergraph", ".tres");
                                exportShader.DefinitionType = ShaderDefinitionType.SDT_SHADERGRAPH;
                            }
                            if (SurfaceShader)
                            {
                                exportShader.ResourcePath = exportShader.ResourcePath.Replace(".shader", ".tres");
                                exportShader.DefinitionType = ShaderDefinitionType.SDT_SURFACESHADER;
                            }
                            Scene.ExportShaders.Add(exportShader);
                        }
                        NewExportMaterial.material = Mat;
                        NewExportMaterial.ResourcePath = GetResourcePath(Mat);
                        string MatFile = GetAssetPath(Mat);                        
                        NewExportMaterial.File = MatFile.Replace(".mat", ".tres");
                        NewExportMaterial.ResourcePath = NewExportMaterial.ResourcePath.Replace(".mat", ".tres");                                                
                        NewExportMaterial.exportShader = exportShader;
                        ExportMaterials[m] = NewExportMaterial;
                        Scene.AllMaterials.Add(NewExportMaterial);
                    }
                }

                NewExportMesh.ExportMaterials = ExportMaterials;

                Scene.MeshesToExport.Add(NewExportMesh);
            }
        }

        bool ProcessEntireHierarchy = true;
        LODGroup ChildLODGroup = GO.GetComponent<LODGroup>();
        if (ChildLODGroup != null)
        {
            LOD[] lods = ChildLODGroup.GetLODs();
            //Only Process LOD0 since LODs behave differently in Godot
            if (lods != null && lods.Length > 0)
            {
                //ProcessEntireHierarchy = false;

                //Disable LOD1+ so it doesn't get exported
                for (int l = 1; l < lods.Length; l++)
                {
                    LOD lod = lods[l];

                    for (int i = 0; i < lod.renderers.Length; i++)
                    {
                        Renderer renderer = lod.renderers[i];
                        if (renderer != null)
                            renderer.enabled = false;// gameObject.SetActive( false );
                    }
                }
                //Reactivate LOD0 in cases where LOD1+ share GameObjects with LOD0
                for (int i = 0; i < lods[0].renderers.Length; i++)
                {
                    Renderer renderer = lods[0].renderers[i];
                    if (renderer != null)
                        renderer.enabled = true;// gameObject.SetActive( true );
                }
            }
        }

        if (ProcessEntireHierarchy)
        {
            for (int i = 0; i < GO.transform.childCount; i++)
            {
                GameObject ChildGO = GO.transform.GetChild(i).gameObject;

                ExportGameObject(ChildGO, ref Scene);
            }
        }

        Scene.ProcessingStack.Pop();
    }
    public static string GetAssetPath( UnityEngine.Object obj )
    {
        string AssetPath = AssetDatabase.GetAssetPath( obj );
        Type ObjType = obj.GetType();
        string Extension = "";
        if ( ObjType == typeof(Material))
            Extension = ".mat";
        //This happens with speedtree component materials
        if ( AssetPath.Contains(".prefab"))
        {
            AssetPath = AssetPath.Replace(".prefab", "." + obj.name + Extension );
        }
                                      //All builtin resources have the same path
        if ( AssetPath.Length > 0 && !AssetPath.Contains("Resources/unity_builtin_extra"))
        {
            return AssetPath;
        }
        AssetPath = "Assets/MemoryAssets/" + obj.name + "_" + obj.GetInstanceID() + Extension;
        return AssetPath;
    }
    public static bool UnloadCurrentScenes()
    {
        int sceneCount = SceneManager.sceneCount;
        bool Result = false;
        for (int i = 0; i < sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            Result = EditorSceneManager.UnloadScene( scene ) && Result;
        }
        return Result;
    }
    public static GameObject[] GetAllRootGameObjects()
    {
        List<GameObject> allRoots = new List<GameObject>();

        int sceneCount = SceneManager.sceneCount;
        for (int i = 0; i < sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            allRoots.AddRange(scene.GetRootGameObjects());
        }

        return allRoots.ToArray();
    }
    static async Task ExporterMain( bool IsLastScene )
    {
        GameObject[] gameObjects = GetAllRootGameObjects();
        ExportScene Scene = new ExportScene();

        MissingNodeTypes.Clear();
        
        Scene.TotalNodes = Scene.PreprocessScene();
        Scene.CurrentNodes = 0;
        try
        {
            for (int i = 0; i < gameObjects.Length; i++)
            {
                GameObject GO = gameObjects[i];
                ExportGameObject(GO, ref Scene);
            }
        }
        catch (Exception E)
        {
            Debug.LogError(E.Message + "\n" + E.StackTrace );
        }

        Scene.FixDuplicatePrefabNames();

        List<Task> AllTasks = new List<Task>();
        for (int i = 0; i < Scene.MeshesToExport.Count; i++)
        {
            ExportMesh exportMesh = Scene.MeshesToExport[i];
            float Completion = (i + 1) / (float)Scene.MeshesToExport.Count;
            if (EditorUtility.DisplayCancelableProgressBar("Gathering Meshes...", "Gathering Meshes... " + i + "/" + Scene.MeshesToExport.Count, Completion))
            {
                EditorUtility.ClearProgressBar();
                return;
            }
            Task NewTask =
                ExportGLTFAsync(Scene.MeshesToExport[i]);
            AllTasks.Add(NewTask);
        }
        EditorUtility.ClearProgressBar();

        for (int i = 0; i < Scene.ExportShaders.Count; i++)
        {
            ExportShader shader = Scene.ExportShaders[i];
            float Completion = (i + 1) / (float)Scene.ExportShaders.Count;
            if( EditorUtility.DisplayCancelableProgressBar("Exporting Shaders...", "Exporting Shaders... " + i + "/" + Scene.ExportShaders.Count, Completion) )
            {
                EditorUtility.ClearProgressBar();
                return;
            }

            if (shader.SourceShader != null)
            {
                if (shader.DefinitionType == ShaderDefinitionType.SDT_SHADERGRAPH)
                {
                    string path = AssetDatabase.GetAssetPath(shader.SourceShader);
#if UNITY_SHADER_GRAPH
                    try
                    {
                        shader.ShaderData = ExportShaderGraph(path);
                    }
                    catch (Exception E)
                    {
                        Debug.LogError(E.Message + "\n" + E.StackTrace );
                    }
#endif
                }
                else if (shader.DefinitionType == ShaderDefinitionType.SDT_SURFACESHADER)
                {
                    string path = AssetDatabase.GetAssetPath(shader.SourceShader);
                    DoExportSurfaceShader(path);
                    string GodotShaderImportPath = GetGodotFilePath(path, ".gdshader");
                    GodotShaderImportPath += ".uid";
                    string Text = string.Format( CultureInfo.InvariantCulture,"uid://{0}", shader.GUID);
                    File.WriteAllText(GodotShaderImportPath, Text);
                }
            }
        }
        EditorUtility.ClearProgressBar();

        try
        {
            for (int m = 0; m < Scene.AllMaterials.Count; m++)
            {
                ExportMaterial exportMaterial = Scene.AllMaterials[m];
                float Completion = (m + 1) / (float)Scene.AllMaterials.Count;
                if ( EditorUtility.DisplayCancelableProgressBar("Exporting Materials...", "Exporting Materials... " + m + "/" + Scene.AllMaterials.Count, Completion) )
                {
                    EditorUtility.ClearProgressBar();
                    return;
                }

                //ExportTextures changes extensions so need to extract them first
                ExtractMaterialTextures( exportMaterial );
                string ResourceReferences = "";
                ExportTextures(ref ResourceReferences, exportMaterial.Textures);

                string MatFileData = GenerateMaterial(exportMaterial);
                string MatFileUnityPath = GetAssetPath(exportMaterial.material);
                if ( MatFileUnityPath.Contains(".mat"))
                    MatFileUnityPath = MatFileUnityPath.Replace(".mat", ".tres");
                else
                    MatFileUnityPath += ".tres";

                string MatFilePath = GetGodotPathFromUnityAssetPath(MatFileUnityPath);
                CreateDirectoriesForFile(MatFilePath);
                File.WriteAllText(MatFilePath, MatFileData);
            }
        }
        catch (Exception E)
        {
            Debug.LogError(E.Message + "\n" + E.StackTrace );
        }
        EditorUtility.ClearProgressBar();

        try
        {
            int TasksLeft = AllTasks.Count;
            while (TasksLeft > 0)
            {
                TasksLeft = 0;
                for (int i = 0; i < AllTasks.Count; i++)
                {
                    if (!AllTasks[i].IsCompleted)
                        TasksLeft++;
                }

                int TasksCompleted = AllTasks.Count - TasksLeft;
                float Completion = (float)TasksCompleted / (float)AllTasks.Count;
                if( EditorUtility.DisplayCancelableProgressBar("Tasks...", "Waiting for export meshes to finish... " + TasksCompleted + "/" + AllTasks.Count, Completion) )
                {
                    EditorUtility.ClearProgressBar();
                    return;
                }
                await Task.Yield();
            }
        }
        catch (Exception E)
        {
            Debug.LogError(E.Message + "\n" + E.StackTrace );
        }

        EditorUtility.ClearProgressBar();

        for (int i = 0; i < Scene.MeshesToExport.Count; i++)
        {
            float Completion = (i + 1) / (float)Scene.MeshesToExport.Count;
            if( EditorUtility.DisplayCancelableProgressBar("Writing Prefabs...", "Writing Prefabs... " + i + "/" + Scene.MeshesToExport.Count, Completion) )
            {
                EditorUtility.ClearProgressBar();
                return;
            }

            try
            {
                WritePrefab("/Prefabs", Scene.MeshesToExport[i]);
            }
            catch (Exception E)
            {
                Debug.LogError(E.Message + "\n" + E.StackTrace );
            }
        }

        WriteScene(Scene);
        WriteUIs(Scene);

        CopyGlobalFiles();

        EditorUtility.ClearProgressBar();

        int sceneCount = SceneManager.sceneCount;
        for (int i = 0; i < sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            EditorSceneManager.MarkSceneDirty(scene);
        }

        string Message = "Export Complete !\n\n" +
                         "Exported project must be opened in Godot 4.4.1+\n" +
                         "Don't save the current scene because the export process modifies some assets in memory\n";

        if (MissingNodeTypes.Count > 0)
        {
            string Text = "\nMissing ShaderGraph Node Types :\n";
            for (int i = 0; i < MissingNodeTypes.Count; i++)
            {
                Text += MissingNodeTypes[i] + "\n";
            }
            Message += Text;
        }

        if ( IsLastScene )
        {
            bool Result = EditorUtility.DisplayDialog("Export Complete", Message, "OK");
        }
    }

    static public List<object> GetList(object obj)
    {
        if (obj is System.Collections.IEnumerable)
        {
            List<object> copy = new List<object>();

            foreach (var item in obj as System.Collections.IEnumerable)
                copy.Add(item);

            return copy;
        }

        return null;
    }
    static public List<object> GetListThroughReflection(string name, object obj)
    {
        Type ObjectType = obj.GetType();

        return GetListThroughReflection(name, obj, ObjectType);
    }
    static public List<object> GetListThroughReflection(string name, object obj, Type ObjectType)
    {
        FieldInfo DataField = ObjectType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        var ListObject = DataField.GetValue(obj);

        List<object> TheList = GetList(ListObject);
        return TheList;
    }
    static public object GetValueForFieldOrProperty(object obj, string FieldName, BindingFlags Flags)
    {
        try
        {
            return GetValueForFieldOrProperty(obj, obj.GetType(), FieldName, Flags);
        }
        catch
        {
            return null;
        }
    }
    static public object GetValueForFieldOrProperty(object obj, Type TheType, string FieldName, BindingFlags Flags)
    {
        if (obj == null || TheType == null)
            return null;
        FieldInfo Field = TheType.GetField(FieldName, Flags);
        if (Field != null)
        {
            object Value = Field.GetValue(obj);
            return Value;
        }
        PropertyInfo Property = TheType.GetProperty(FieldName);
        if (Property == null)
            Property = TheType.GetProperty(FieldName, Flags);
        if (Property != null)
        {
            object Value = Property.GetValue(obj);
            return Value;
        }

        return null;
    }
    public class ShaderConnection
    {
        public object SourceNodeObject;
        public object DestinationNodeObject;

        public int SourceNode = -1;
        public int SourceNodeOutput = -1;
        public int DestinationNode = -1;
        public int DestinationNodeInput = -1;

        public ShaderType ShaderType = ShaderType.ST_UNKNOWN;
    }
    public enum ShaderType
    {
        ST_UNKNOWN,

        VERTEX,
        FRAGMENT
    };
    public enum ShaderParameterType
    {
        SPT_UNKNOWN,

        SPT_FLOAT,
        SPT_VECTOR2,
        SPT_VECTOR3,
        SPT_VECTOR4,
        SPT_COLOR,
        SPT_TEXTURE,
        SPT_BOOL,
        SPT_INT,
    };
    public enum ShaderParameterFlag
    {
        SPF_NONE,
        //SPF_HAS_TILING_OFFSET,
        SPF_IS_TILING_OFFSET
    };
    public class ShaderParameter
    {
        public string Name;
        public string SanitizedName;
        public string ReferenceName;
        public ShaderParameterType Type;

        public Vector4 DefaultVector;
        public float DefaultFloat;
        public Texture DefaultTexture = null;
        public bool DefaultBool = false;
        public int DefaultInt = 0;

        public ShaderType ShaderType = ShaderType.ST_UNKNOWN;
        public GodotShaderNode Node = null;
        public ShaderParameterFlag Flag = ShaderParameterFlag.SPF_NONE;
        public ShaderParameter TilingOffsetParameter = null;
    };
    public class GodotShaderNode
    {
        public string DefinitionText;
        public string GraphConfigText = "";

        public string GUID;
        public int ID = 0;
        public Vector2 Position;
        public ShaderType ShaderType = ShaderType.ST_UNKNOWN;

        public int OutputIndex = -1;
        public object NodeObject;
        public List<UTGSlot> ProcessedSlots;
        public object GraphObject;
        public GodotShaderNode ParentGraphNode;
        //The texture parameter when it's a property node, used for TilingOffset
        public ShaderParameter Parameter;
        public UTGSlot GetSlot(string Name)
        {
            return GetSlot( Name, ProcessedSlots );
        }
        public static UTGSlot GetSlot(string Name, List<UTGSlot> Slots )
        {
            for (int i = 0; i < Slots.Count; i++)
            {
                if (Slots[i].Name == Name)
                    return Slots[i];
            }

            return null;
        }
        public UTGSlot GetSlot(int OutputIndex)
        {
            for (int i = 0; i < ProcessedSlots.Count; i++)
            {
                if (!ProcessedSlots[i].Input &&
                     ProcessedSlots[i].OutputIndex == OutputIndex)
                    return ProcessedSlots[i];
            }

            return null;
        }
        public UTGSlot GetSlotByNameAndInput( string Name, bool Input )
        {
            for (int i = 0; i < ProcessedSlots.Count; i++)
            {
                if ( ProcessedSlots[i].Name.Equals( Name ) &&
                     ProcessedSlots[i].Input == Input)
                    return ProcessedSlots[i];
            }

            return null;
        }
        public UTGSlot GetSlot(object SlotObject)
        {
            int InputIndex = -1;
            int OutputIndex = -1;

            UTGSlot TempSlot = GenerateUTGSlotForInner(SlotObject, ref InputIndex, ref OutputIndex, null);

            for (int i = 0; i < ProcessedSlots.Count; i++)
            {
                UTGSlot ProcessedSlot = ProcessedSlots[i];
                if (ProcessedSlot == null || SlotObject == null)
                    continue;

                if (ProcessedSlot.Slot == SlotObject)
                    return ProcessedSlot;

                if (ProcessedSlot.Name.Equals(TempSlot.Name) &&
                    ProcessedSlot.ValueType.Equals(TempSlot.ValueType))
                    return ProcessedSlot;
            }

            return null;
        }
        public string GetParentGraphName()
        {
            if ( ParentGraphNode != null )
            {
                object Subgraph = GetValueForFieldOrProperty(ParentGraphNode.NodeObject, "asset", BindingFlags.Instance | BindingFlags.NonPublic);
                object SubgraphName = GetValueForFieldOrProperty(Subgraph, "name", BindingFlags.Instance | BindingFlags.NonPublic);
                return SubgraphName.ToString();
            }

            return "";
        }
    };
    public enum GodotBlendMode
    {
        GBM_MIX,
        GBM_ADD,
        GBM_SUB,
        GBM_MUL,
    };
    public enum GodotCullMode
    {
        GCM_BACK,
        GCM_FRONT,
        GCM_OFF
    }
    public enum ExportTextureType
    {
        GTT_DATA = 0,
        GTT_COLOR = 1,
        GTT_NORMAL = 2
    };
    public enum GodotShaderVariableType
    {
        GSVT_FLOAT,
        GSVT_INT,
        GSVT_UINT,
        GSVT_VECTOR2,
        GSVT_VECTOR3,
        GSVT_VECTOR4,
        GSVT_BOOL,
        GSVT_TRANSFORM,
        GSVT_SAMPLER,

        GSVT_UNKNOWN,
    };
    public enum GodotShaderVariableType_Mix
    {
        VTM_SCALAR,
        VTM_VECTOR2,
        VTM_VECTOR2SCALAR,
        VTM_VECTOR3,
        VTM_VECTOR3SCALAR,
        VTM_VECTOR4,
        VTM_VECTOR4SCALAR,
    };
    public enum GodotFloatOperation
    {
        GFO_ADD = 0,
        GFO_SUBTRACT,
        GFO_MULTIPLY,
        GFO_DIVIDE,
        GFO_REMAINDER,
        GFO_POWER,
        GFO_MAX,
        GFO_MIN,
        GFO_ATAN2,
        GFO_STEP,
        GFO_UNKNOWN
    };
    public enum GodotVectorOperation
    {
        GVO_ADD = 0,
        GVO_SUBTRACT,
        GVO_MULTIPLY,
        GVO_DIVIDE,
        GVO_REMAINDER,
        GVO_POWER,
        GVO_MAX,
        GVO_MIN,
        GVO_CROSS,
        GVO_ATAN2,
        GVO_REFLECT,
        GVO_STEP,
    };
    public enum GodotFloatFunction
    {
        GFF_SIN = 0,
        GFF_COS,
        GFF_TAN,
        GFF_ASIN,
        GFF_ACOS,
        GFF_ATAN,
        GFF_SINH,
        GFF_COSH,
        GFF_TANH,
        GFF_LOG,
        GFF_EXP,
        GFF_SQRT,
        GFF_ABS,
        GFF_SIGN,
        GFF_FLOOR,
        GFF_ROUND,
        GFF_CEIL,
        GFF_FRACT,
        GFF_SATURATE,
        GFF_NEGATE,
        GFF_ACOSH,
        GFF_ASINH,
        GFF_ATANH,
        GFF_DEGREES,
        GFF_EXP2,
        GFF_INVSQRT,
        GFF_LOG2,
        GFF_RADIANS,
        GFF_RECIP,
        GFF_ROUNDEVEN,
        GFF_TRUNC,
        GFF_ONE_MINUS = 31,
        GFF_UNKNOWN
    };
    public enum GodotVectorFunction
    {
        GVF_NORMALIZE,
        GVF_SATURATE,
        GVF_NEGATE,
        GVF_RECIP,
        GVF_ABS,
        GVF_ACOS,
        GVF_ACOSH,
        GVF_ASIN,
        GVF_ASINH,
        GVF_ATAN,
        GVF_ATANH,
        GVF_CEIL,
        GVF_COS,
        GVF_COSH,
        GVF_DEGREES,
        GVF_EXP,
        GVF_EXP2,
        GVF_FLOOR,
        GVF_FRACT,
        GVF_INVSQRT,
        GVF_LOG,
        GVF_LOG2,
        GVF_RADIANS,
        GVF_ROUND,
        GVF_ROUNDEVEN,
        GVF_SIGN,
        GVF_SIN,
        GVF_SINH,
        GVF_SQRT,
        GVF_TAN,
        GVF_TANH,
        GVF_TRUNC,
        GVF_ONE_MINUS
    };
    public enum GodotShaderVariableType_VectorOp
    {
        VOP_VECTOR2,
        VOP_VECTOR3,
        VOP_VECTOR4,
    };
    public enum GodotShaderVariableType_DerivateFunc
    {
        OP_TYPE_SCALAR,
        OP_TYPE_VECTOR_2D,
        OP_TYPE_VECTOR_3D,
        OP_TYPE_VECTOR_4D,
    };
    public enum GodotFilterMode
    {
        TEXTURE_FILTER_DEFAULT,
        TEXTURE_FILTER_NEAREST,
        TEXTURE_FILTER_LINEAR,
        TEXTURE_FILTER_NEAREST_WITH_MIPMAPS,
        TEXTURE_FILTER_LINEAR_WITH_MIPMAPS,
        TEXTURE_FILTER_NEAREST_WITH_MIPMAPS_ANISOTROPIC,
        TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC,
        TEXTURE_FILTER_MAX
    }
    public enum GodotRepeatMode
    {
        TEXTURE_REPEAT_DEFAULT,
        TEXTURE_REPEAT_ENABLED,
        TEXTURE_REPEAT_DISABLED
    }
    public enum GodotColorDefault
    {
        GCD_WHITE,
        GCD_BLACK,
    }
    public class GodotShaderExpressionParameter
    {
        public bool IsInput = true;
        public string Name;
        public GodotShaderVariableType Type;
        public GodotShaderExpressionParameter(bool pIsInput, string pName, GodotShaderVariableType pType)
        {
            IsInput = pIsInput;
            Name = pName;
            Type = pType;
        }
    };

    public static GodotFilterMode GetFilterMode(string filter)
    {
        if (filter == "Linear")
        {
            return GodotFilterMode.TEXTURE_FILTER_LINEAR;
        }
        else if (filter == "Point")
        {
            return GodotFilterMode.TEXTURE_FILTER_NEAREST;
        }
        else if (filter == "Trilinear")
        {
            return GodotFilterMode.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC;
        }

        return GodotFilterMode.TEXTURE_FILTER_DEFAULT;
    }
    public static GodotRepeatMode GetRepeatMode(string wrap)
    {
        if (wrap == "Repeat")
        {
            return GodotRepeatMode.TEXTURE_REPEAT_ENABLED;
        }
        else if (wrap == "Clamp")
        {
            return GodotRepeatMode.TEXTURE_REPEAT_DISABLED;
        }
        else if (wrap == "Mirror")
        {
            //return GodotRepeatMode.TEXTURE_REPEAT_ENABLED;
        }
        else if (wrap == "MirrorOnce")
        {
            //return GodotRepeatMode.TEXTURE_REPEAT_ENABLED;
        }

        return GodotRepeatMode.TEXTURE_REPEAT_DEFAULT;
    }
    public static string GetShaderTypeText(ShaderType Type)
    {
        if (Type == ShaderType.VERTEX)
            return "vertex";
        else if (Type == ShaderType.FRAGMENT)
            return "fragment";
        else
            return "unimplemented";
    }
    public static string GenerateFloatOpNode(GodotFloatOperation Operation, float DefaultA, float DefaultB, ref string Params)
    {
        string Text = string.Format( CultureInfo.InvariantCulture,
                  "operator = {0}\n" +
                  "default_input_values = [ 0, {1:0.0#}, 1, {2:0.0#} ]\n",
                  (int)Operation, DefaultA, DefaultB
                  );
        Params = Text;

        return "VisualShaderNodeFloatOp";
    }
    public static string GenerateFloatFuncNode(GodotFloatFunction Function, float DefaultValue, ref string Params)
    {
        string Text = string.Format( CultureInfo.InvariantCulture,
                  "default_input_values = [0, {0:0.0#}]\n" +
                  "function = {1}",
                  DefaultValue, (int)Function);
        Params = Text;

        return "VisualShaderNodeFloatFunc";
    }
    public static string GenerateVectorFuncNode(GodotVectorFunction Function, GodotShaderVariableType DataType, Vector4 DefaultValue, ref string Params)
    {
        int VectorFuncDataEnum = (int)DataType - (int)GodotShaderVariableType.GSVT_VECTOR2;
        string DefaultInputValues = GenerateDefaultInputValues(DataType, DefaultValue);
        string Text = string.Format( CultureInfo.InvariantCulture,
                  "{0}\n" +
                  "op_type = {1}\n" +
                  "function = {2}\n",
                  DefaultInputValues, VectorFuncDataEnum, (int)Function);
        Params = Text;

        return "VisualShaderNodeVectorFunc";
    }
    public static string GenerateInputNode(string InputName, ref string Params)
    {
        Params = string.Format( CultureInfo.InvariantCulture,"input_name = \"{0}\"\n", InputName);

        return "VisualShaderNodeInput";
    }
    public static string GenerateRerouteNode(GodotShaderVariableType Type, ref string Params)
    {
        Params = string.Format( CultureInfo.InvariantCulture,"port_type = {0}\n", (int)Type);

        return "VisualShaderNodeReroute";
    }
    public static string GenerateConstantNode(float Constant, ref string Params)
    {
        string Text = string.Format( CultureInfo.InvariantCulture,
                  "constant = {0}\n",
                  Constant);

        Params += Text;
        return "VisualShaderNodeFloatConstant";
    }
    public static string GenerateConstant2Node(Vector2 Constant, ref string Params)
    {
        string Text = string.Format( CultureInfo.InvariantCulture,
                    "expanded_output_ports = [0]\n" +
                    "constant = Vector2({0}, {1})\n",
                    Constant.x, Constant.y);
        Params += Text;
        return "VisualShaderNodeVec2Constant";
    }
    public static string GenerateConstant3Node(Vector3 Constant, ref string Params)
    {
        string Text = string.Format( CultureInfo.InvariantCulture,
                    "expanded_output_ports = [0]\n" +
                    "constant = Vector3({0}, {1}, {2})\n",
                    Constant.x, Constant.y, Constant.z);
        Params += Text;
        return "VisualShaderNodeVec3Constant";
    }
    public static string GenerateConstant4Node(Vector4 Constant, ref string Params)
    {
        string Text = string.Format( CultureInfo.InvariantCulture,
                    "expanded_output_ports = [0]\n" +
                    "constant = Quaternion({0}, {1}, {2}, {3})\n",
                    Constant.x, Constant.y, Constant.z, Constant.w);
        Params += Text;
        return "VisualShaderNodeVec4Constant";
    }
    public static string GenerateVectorComposeNode(GodotShaderVariableType_VectorOp OpType, ref string Params)
    {
        string Text = string.Format( CultureInfo.InvariantCulture,
                  "op_type = {0}\n",
                    (int)OpType
                );
        Params = Text;

        return "VisualShaderNodeVectorCompose";
    }
    public static GodotShaderVariableType GetShaderTypeFromParameterType(ShaderParameterType Type)
    {
        switch(Type)
        {
            default:
            case ShaderParameterType.SPT_FLOAT   : return GodotShaderVariableType.GSVT_FLOAT;
            case ShaderParameterType.SPT_VECTOR2 : return GodotShaderVariableType.GSVT_VECTOR2;
            case ShaderParameterType.SPT_VECTOR3 : return GodotShaderVariableType.GSVT_VECTOR3;
            case ShaderParameterType.SPT_VECTOR4 : return GodotShaderVariableType.GSVT_VECTOR4;
        }
    }
    public static string GenerateParameterNode(bool DefaultBool, int DefaultInt, Vector4 DefaultVector, ShaderParameterType Type, string parameterName, ref string Params)
    {
        string valueStr = DefaultBool ? "true" : "false";
        if (Type == ShaderParameterType.SPT_INT)
            valueStr = DefaultInt.ToString();
        else if (Type >= ShaderParameterType.SPT_VECTOR2 && Type <= ShaderParameterType.SPT_VECTOR4)
        {
            GodotShaderVariableType ShaderType = GetShaderTypeFromParameterType( Type );
            valueStr = GenerateVectorString( ShaderType, DefaultVector, GetDataTypeMode.GDTM_FOR_PARAMS );
        }

        string Text = string.Format( CultureInfo.InvariantCulture,
            "parameter_name = \"{0}\"\n" +
            "default_value_enabled = true\n" +
            "default_value = {1}\n",
            parameterName,
            valueStr
            );

        Params += Text;
        switch( Type )
        {
            case ShaderParameterType.SPT_BOOL    : return "VisualShaderNodeBooleanParameter"; 
            case ShaderParameterType.SPT_INT     : return "VisualShaderNodeIntParameter"; 
            case ShaderParameterType.SPT_VECTOR2 : return "VisualShaderNodeVec2Parameter"; 
            case ShaderParameterType.SPT_VECTOR3 : return "VisualShaderNodeVec3Parameter";
            case ShaderParameterType.SPT_VECTOR4 : return "VisualShaderNodeVec4Parameter";
        }
        
        return null;
    }
    public static ShaderParameter GenerateNewParameter( GodotShaderData ShaderData, string NameStr, bool BoolValue, int IntValue, Vector4 Vec4Value, ShaderParameterType ParameterType,
        ref GodotShaderNode DependentNode, ShaderType CurrentShaderType, object GraphObject, GodotShaderNode ParentGraphNode,
        int PositionX, int PositionY )
    {
        string DependentNodeParams = "";
        string DependentNodeType = "";

        SanitizeParamName( ref NameStr );
        DependentNodeType = GenerateParameterNode( BoolValue, IntValue, Vec4Value, ParameterType, NameStr, ref DependentNodeParams );

        DependentNode = GenerateShaderNode( ShaderData, null, null,
            CurrentShaderType, DependentNodeType, GraphObject, ParentGraphNode, PositionX, PositionY );
        DependentNode.DefinitionText += DependentNodeParams;

        ShaderParameter Parameter = ShaderData.AddShaderParameter( ref NameStr, NameStr, ParameterType, CurrentShaderType, 0.0f, Vector4.zero, null, BoolValue, IntValue, DependentNode );
        return Parameter;
    }
    public static string GenerateTextureParameterNode(string ParameterName, GodotFilterMode FilterMode, GodotRepeatMode RepeatMode, ExportTextureType TextureType, GodotColorDefault ColorDefault,
                                                        ref string Params)
    {
        string Text = string.Format( CultureInfo.InvariantCulture,
                "parameter_name = \"{0}\"\n" +
                "texture_type = {1}\n", ParameterName, (int)TextureType);
        Text += string.Format( CultureInfo.InvariantCulture,
            "texture_filter = {0}\n" +
            "texture_repeat = {1}\n" +
            "color_default = {2}\n", (int)FilterMode, (int)RepeatMode, (int)ColorDefault);

        Params += Text;
        return "VisualShaderNodeTexture2DParameter";
    }
    public static string GenerateBoolConstantNode(bool value, ref string Params)
    {
        string valueStr = value ? "true" : "false";

        string Text = string.Format( CultureInfo.InvariantCulture,
            "constant = {0}\n",
            valueStr
             );

        Params += Text;
        return "VisualShaderNodeBooleanConstant";
    }
    public static int GetNumLines(string Expression)
    {
        if (Expression == null || Expression == "")
            return 0;

        int len = Expression.Length;
        int Lines = 0;
        for (int i = 0; i < len; i++)
        {
            if (Expression[i] == '\n')
                Lines++;
        }

        return Lines;
    }
    public static string GenerateShaderExpression(ref List<GodotShaderExpressionParameter> Parameters, string Expression, ref string Params, GodotShaderNode ShaderNode)
    {
        if ( Expression.Contains( "\""))
        {
            Debug.LogError("Expression contains \" this is invalid in Godot");
        }
        int Lines = GetNumLines(Expression);
        int Height = 80 + Parameters.Count * 80 + Lines * 50;
        string Text = string.Format( CultureInfo.InvariantCulture,
                  "size = Vector2(980, {0})\n" +
                  "expression = \"{1}\"\n", Height, Expression
                    );
        Params += Text;

        string InputsText = "";
        string OutputsText = "";
        int InputIndex = 0;
        int OutputIndex = 0;
        for (int i = 0; i < Parameters.Count; i++)
        {
            GodotShaderExpressionParameter Param = Parameters[i];

            if (Param.IsInput)
            {
                Text = string.Format( CultureInfo.InvariantCulture,
                            "{0},{1},{2};",
                            InputIndex, (int)Param.Type, Param.Name
                            );

                InputsText += Text;
                InputIndex++;
            }
            else
            {
                Text = string.Format( CultureInfo.InvariantCulture,
                            "{0},{1},{2};",
                          OutputIndex, (int)Param.Type, Param.Name
                            );

                OutputsText += Text;
                OutputIndex++;
            }
        }

        string ShaderTypeText = GetShaderTypeText(ShaderNode.ShaderType);
        Text = string.Format( CultureInfo.InvariantCulture,
                  "nodes/{0}/{1}/size = Vector2(980, {2})\n" +
                  "nodes/{3}/{4}/input_ports = \"{5}\"\n" +
                  "nodes/{6}/{7}/output_ports = \"{8}\"\n" +
                  "nodes/{9}/{10}/expression = \"{11}\"\n",
                  ShaderTypeText, ShaderNode.ID, Height,
                  ShaderTypeText, ShaderNode.ID, InputsText,
                  ShaderTypeText, ShaderNode.ID, OutputsText,
                  ShaderTypeText, ShaderNode.ID, Expression
        );

        ShaderNode.GraphConfigText += Text;
        return "VisualShaderNodeExpression";
    }
    public static string GenerateShaderExpression(
                                      string Expression, GodotShaderVariableType OutType, ref string Params, GodotShaderNode ShaderNode)
    {
        GodotShaderExpressionParameter OutputParameter = new GodotShaderExpressionParameter(false, "output0", OutType);
        List<GodotShaderExpressionParameter> Parameters = new List<GodotShaderExpressionParameter>();
        Parameters.Add(OutputParameter);
        string Ret = GenerateShaderExpression(ref Parameters, Expression, ref Params, ShaderNode);

        return Ret;
    }
    public static string GenerateShaderExpression(GodotShaderVariableType InType0,
                                      string Expression, GodotShaderVariableType OutType, ref string Params, GodotShaderNode ShaderNode)
    {

        GodotShaderExpressionParameter InputParameter0 = new GodotShaderExpressionParameter(true, "input0", InType0);
        GodotShaderExpressionParameter OutputParameter = new GodotShaderExpressionParameter(false, "output0", OutType);
        List<GodotShaderExpressionParameter> Parameters = new List<GodotShaderExpressionParameter>();
        Parameters.Add(InputParameter0);
        Parameters.Add(OutputParameter);
        string Ret = GenerateShaderExpression(ref Parameters, Expression, ref Params, ShaderNode);

        return Ret;
    }
    public static string GenerateShaderExpression(GodotShaderVariableType InType0,
                                      string Expression, GodotShaderVariableType OutType0, GodotShaderVariableType OutType1, ref string Params, GodotShaderNode ShaderNode)
    {

        GodotShaderExpressionParameter InputParameter0 = new GodotShaderExpressionParameter(true, "input0", InType0);
        GodotShaderExpressionParameter OutputParameter0 = new GodotShaderExpressionParameter(false, "output0", OutType0);
        GodotShaderExpressionParameter OutputParameter1 = new GodotShaderExpressionParameter(false, "output1", OutType1);
        List<GodotShaderExpressionParameter> Parameters = new List<GodotShaderExpressionParameter>();
        Parameters.Add(InputParameter0);
        Parameters.Add(OutputParameter0);
        Parameters.Add(OutputParameter1);
        string Ret = GenerateShaderExpression(ref Parameters, Expression, ref Params, ShaderNode);

        return Ret;
    }
    public static string GenerateShaderExpression(string Expression, GodotShaderVariableType OutType0, GodotShaderVariableType OutType1,
                                                ref string Params, GodotShaderNode ShaderNode)
    {
        GodotShaderExpressionParameter OutputParameter0 = new GodotShaderExpressionParameter(false, "output0", OutType0);
        GodotShaderExpressionParameter OutputParameter1 = new GodotShaderExpressionParameter(false, "output1", OutType1);
        List<GodotShaderExpressionParameter> Parameters = new List<GodotShaderExpressionParameter>();
        Parameters.Add(OutputParameter0);
        Parameters.Add(OutputParameter1);
        string Ret = GenerateShaderExpression(ref Parameters, Expression, ref Params, ShaderNode);

        return Ret;
    }
    public static string GenerateShaderExpression(string Expression, GodotShaderVariableType OutType0, GodotShaderVariableType OutType1, GodotShaderVariableType OutType2,
                                                ref string Params, GodotShaderNode ShaderNode)
    {
        GodotShaderExpressionParameter OutputParameter0 = new GodotShaderExpressionParameter(false, "output0", OutType0);
        GodotShaderExpressionParameter OutputParameter1 = new GodotShaderExpressionParameter(false, "output1", OutType1);
        GodotShaderExpressionParameter OutputParameter2 = new GodotShaderExpressionParameter(false, "output2", OutType2);
        List<GodotShaderExpressionParameter> Parameters = new List<GodotShaderExpressionParameter>();
        Parameters.Add(OutputParameter0);
        Parameters.Add(OutputParameter1);
        Parameters.Add(OutputParameter2);
        string Ret = GenerateShaderExpression(ref Parameters, Expression, ref Params, ShaderNode);

        return Ret;
    }
    public static string GenerateShaderExpression(GodotShaderVariableType InType0, GodotShaderVariableType InType1,
                                      string Expression, GodotShaderVariableType OutType0, ref string Params, GodotShaderNode ShaderNode)
    {

        GodotShaderExpressionParameter InputParameter0 = new GodotShaderExpressionParameter(true, "input0", InType0);
        GodotShaderExpressionParameter InputParameter1 = new GodotShaderExpressionParameter(true, "input1", InType1);
        GodotShaderExpressionParameter OutputParameter0 = new GodotShaderExpressionParameter(false, "output0", OutType0);
        List<GodotShaderExpressionParameter> Parameters = new List<GodotShaderExpressionParameter>();
        Parameters.Add(InputParameter0);
        Parameters.Add(InputParameter1);
        Parameters.Add(OutputParameter0);
        string Ret = GenerateShaderExpression(ref Parameters, Expression, ref Params, ShaderNode);

        return Ret;
    }
    public static string GenerateShaderExpression(GodotShaderVariableType InType0,
                                      string Expression, GodotShaderVariableType OutType0, GodotShaderVariableType OutType1, GodotShaderVariableType OutType2,
                                      ref string Params, GodotShaderNode ShaderNode)
    {

        GodotShaderExpressionParameter InputParameter0 = new GodotShaderExpressionParameter(true, "input0", InType0);
        GodotShaderExpressionParameter OutputParameter0 = new GodotShaderExpressionParameter(false, "output0", OutType0);
        GodotShaderExpressionParameter OutputParameter1 = new GodotShaderExpressionParameter(false, "output1", OutType1);
        GodotShaderExpressionParameter OutputParameter2 = new GodotShaderExpressionParameter(false, "output2", OutType2);
        List<GodotShaderExpressionParameter> Parameters = new List<GodotShaderExpressionParameter>();
        Parameters.Add(InputParameter0);
        Parameters.Add(OutputParameter0);
        Parameters.Add(OutputParameter1);
        Parameters.Add(OutputParameter2);
        string Ret = GenerateShaderExpression(ref Parameters, Expression, ref Params, ShaderNode);

        return Ret;
    }
    public static string GenerateShaderExpression(GodotShaderVariableType InType0,
                                      string Expression, GodotShaderVariableType OutType0, GodotShaderVariableType OutType1, GodotShaderVariableType OutType2,
                                      GodotShaderVariableType OutType3, ref string Params, GodotShaderNode ShaderNode)
    {

        GodotShaderExpressionParameter InputParameter0 = new GodotShaderExpressionParameter(true, "input0", InType0);
        GodotShaderExpressionParameter OutputParameter0 = new GodotShaderExpressionParameter(false, "output0", OutType0);
        GodotShaderExpressionParameter OutputParameter1 = new GodotShaderExpressionParameter(false, "output1", OutType1);
        GodotShaderExpressionParameter OutputParameter2 = new GodotShaderExpressionParameter(false, "output2", OutType2);
        GodotShaderExpressionParameter OutputParameter3 = new GodotShaderExpressionParameter(false, "output3", OutType3);
        List<GodotShaderExpressionParameter> Parameters = new List<GodotShaderExpressionParameter>();
        Parameters.Add(InputParameter0);
        Parameters.Add(OutputParameter0);
        Parameters.Add(OutputParameter1);
        Parameters.Add(OutputParameter2);
        Parameters.Add(OutputParameter3);
        string Ret = GenerateShaderExpression(ref Parameters, Expression, ref Params, ShaderNode);

        return Ret;
    }
    public static string GenerateShaderExpression(GodotShaderVariableType InType0, GodotShaderVariableType InType1, GodotShaderVariableType InType2,
                                      string Expression, GodotShaderVariableType OutType0, ref string Params, GodotShaderNode ShaderNode)
    {

        GodotShaderExpressionParameter InputParameter0 = new GodotShaderExpressionParameter(true, "input0", InType0);
        GodotShaderExpressionParameter InputParameter1 = new GodotShaderExpressionParameter(true, "input1", InType1);
        GodotShaderExpressionParameter InputParameter2 = new GodotShaderExpressionParameter(true, "input2", InType2);
        GodotShaderExpressionParameter OutputParameter0 = new GodotShaderExpressionParameter(false, "output0", OutType0);
        List<GodotShaderExpressionParameter> Parameters = new List<GodotShaderExpressionParameter>();
        Parameters.Add(InputParameter0);
        Parameters.Add(InputParameter1);
        Parameters.Add(InputParameter2);
        Parameters.Add(OutputParameter0);
        string Ret = GenerateShaderExpression(ref Parameters, Expression, ref Params, ShaderNode);

        return Ret;
    }
    public static string GenerateShaderExpression(GodotShaderVariableType InType0, GodotShaderVariableType InType1, GodotShaderVariableType InType2,
        GodotShaderVariableType InType3, string Expression, GodotShaderVariableType OutType0,
        ref string Params, GodotShaderNode ShaderNode)
    {

        GodotShaderExpressionParameter InputParameter0 = new GodotShaderExpressionParameter(true, "input0", InType0);
        GodotShaderExpressionParameter InputParameter1 = new GodotShaderExpressionParameter(true, "input1", InType1);
        GodotShaderExpressionParameter InputParameter2 = new GodotShaderExpressionParameter(true, "input2", InType2);
        GodotShaderExpressionParameter InputParameter3 = new GodotShaderExpressionParameter(true, "input3", InType3);
        GodotShaderExpressionParameter OutputParameter0 = new GodotShaderExpressionParameter(false, "output0", OutType0);
        List<GodotShaderExpressionParameter> Parameters = new List<GodotShaderExpressionParameter>();
        Parameters.Add(InputParameter0);
        Parameters.Add(InputParameter1);
        Parameters.Add(InputParameter2);
        Parameters.Add(InputParameter3);
        Parameters.Add(OutputParameter0);
        string Ret = GenerateShaderExpression(ref Parameters, Expression, ref Params, ShaderNode);

        return Ret;
    }
    public static string GenerateShaderExpression(GodotShaderVariableType InType0, GodotShaderVariableType InType1, GodotShaderVariableType InType2,
        string Expression, GodotShaderVariableType OutType0, GodotShaderVariableType OutType1,
        ref string Params, GodotShaderNode ShaderNode)
    {

        GodotShaderExpressionParameter InputParameter0 = new GodotShaderExpressionParameter(true, "input0", InType0);
        GodotShaderExpressionParameter InputParameter1 = new GodotShaderExpressionParameter(true, "input1", InType1);
        GodotShaderExpressionParameter InputParameter2 = new GodotShaderExpressionParameter(true, "input2", InType2);
        GodotShaderExpressionParameter OutputParameter0 = new GodotShaderExpressionParameter(false, "output0", OutType0);
        GodotShaderExpressionParameter OutputParameter1 = new GodotShaderExpressionParameter(false, "output1", OutType1);
        List<GodotShaderExpressionParameter> Parameters = new List<GodotShaderExpressionParameter>();
        Parameters.Add(InputParameter0);
        Parameters.Add(InputParameter1);
        Parameters.Add(InputParameter2);
        Parameters.Add(OutputParameter0);
        Parameters.Add(OutputParameter1);
        string Ret = GenerateShaderExpression(ref Parameters, Expression, ref Params, ShaderNode);

        return Ret;
    }
    public static string GenerateShaderExpression(GodotShaderVariableType InType0, GodotShaderVariableType InType1, GodotShaderVariableType InType2,
        GodotShaderVariableType InType3, GodotShaderVariableType InType4, string Expression, GodotShaderVariableType OutType0,
        ref string Params, GodotShaderNode ShaderNode)
    {

        GodotShaderExpressionParameter InputParameter0 = new GodotShaderExpressionParameter(true, "input0", InType0);
        GodotShaderExpressionParameter InputParameter1 = new GodotShaderExpressionParameter(true, "input1", InType1);
        GodotShaderExpressionParameter InputParameter2 = new GodotShaderExpressionParameter(true, "input2", InType2);
        GodotShaderExpressionParameter InputParameter3 = new GodotShaderExpressionParameter(true, "input3", InType3);
        GodotShaderExpressionParameter InputParameter4 = new GodotShaderExpressionParameter(true, "input4", InType4);
        GodotShaderExpressionParameter OutputParameter0 = new GodotShaderExpressionParameter(false, "output0", OutType0);
        List<GodotShaderExpressionParameter> Parameters = new List<GodotShaderExpressionParameter>();
        Parameters.Add(InputParameter0);
        Parameters.Add(InputParameter1);
        Parameters.Add(InputParameter2);
        Parameters.Add(InputParameter3);
        Parameters.Add(InputParameter4);
        Parameters.Add(OutputParameter0);
        string Ret = GenerateShaderExpression(ref Parameters, Expression, ref Params, ShaderNode);

        return Ret;
    }
    public static string GenerateShaderExpression(GodotShaderVariableType InType0, GodotShaderVariableType InType1, GodotShaderVariableType InType2,
        GodotShaderVariableType InType3, GodotShaderVariableType InType4, GodotShaderVariableType InType5, string Expression, GodotShaderVariableType OutType0,
        ref string Params, GodotShaderNode ShaderNode)
    {

        GodotShaderExpressionParameter InputParameter0 = new GodotShaderExpressionParameter(true, "input0", InType0);
        GodotShaderExpressionParameter InputParameter1 = new GodotShaderExpressionParameter(true, "input1", InType1);
        GodotShaderExpressionParameter InputParameter2 = new GodotShaderExpressionParameter(true, "input2", InType2);
        GodotShaderExpressionParameter InputParameter3 = new GodotShaderExpressionParameter(true, "input3", InType3);
        GodotShaderExpressionParameter InputParameter4 = new GodotShaderExpressionParameter(true, "input4", InType4);
        GodotShaderExpressionParameter InputParameter5 = new GodotShaderExpressionParameter(true, "input5", InType5);
        GodotShaderExpressionParameter OutputParameter0 = new GodotShaderExpressionParameter(false, "output0", OutType0);
        List<GodotShaderExpressionParameter> Parameters = new List<GodotShaderExpressionParameter>();
        Parameters.Add(InputParameter0);
        Parameters.Add(InputParameter1);
        Parameters.Add(InputParameter2);
        Parameters.Add(InputParameter3);
        Parameters.Add(InputParameter4);
        Parameters.Add(InputParameter5);
        Parameters.Add(OutputParameter0);
        string Ret = GenerateShaderExpression(ref Parameters, Expression, ref Params, ShaderNode);

        return Ret;
    }
    public static string GenerateShaderExpression(GodotShaderVariableType InType0, GodotShaderVariableType InType1, GodotShaderVariableType InType2,
        GodotShaderVariableType InType3, GodotShaderVariableType InType4, GodotShaderVariableType InType5, GodotShaderVariableType InType6, GodotShaderVariableType InType7,
        GodotShaderVariableType InType8, GodotShaderVariableType InType9, string Expression, GodotShaderVariableType OutType0, GodotShaderVariableType OutType1,
        ref string Params, GodotShaderNode ShaderNode)
    {

        GodotShaderExpressionParameter InputParameter0 = new GodotShaderExpressionParameter(true, "input0", InType0);
        GodotShaderExpressionParameter InputParameter1 = new GodotShaderExpressionParameter(true, "input1", InType1);
        GodotShaderExpressionParameter InputParameter2 = new GodotShaderExpressionParameter(true, "input2", InType2);
        GodotShaderExpressionParameter InputParameter3 = new GodotShaderExpressionParameter(true, "input3", InType3);
        GodotShaderExpressionParameter InputParameter4 = new GodotShaderExpressionParameter(true, "input4", InType4);
        GodotShaderExpressionParameter InputParameter5 = new GodotShaderExpressionParameter(true, "input5", InType5);
        GodotShaderExpressionParameter InputParameter6 = new GodotShaderExpressionParameter(true, "input6", InType6);
        GodotShaderExpressionParameter InputParameter7 = new GodotShaderExpressionParameter(true, "input7", InType7);
        GodotShaderExpressionParameter InputParameter8 = new GodotShaderExpressionParameter(true, "input8", InType8);
        GodotShaderExpressionParameter InputParameter9 = new GodotShaderExpressionParameter(true, "input9", InType9);
        GodotShaderExpressionParameter OutputParameter0 = new GodotShaderExpressionParameter(false, "output0", OutType0);
        GodotShaderExpressionParameter OutputParameter1 = new GodotShaderExpressionParameter(false, "output1", OutType1);
        List<GodotShaderExpressionParameter> Parameters = new List<GodotShaderExpressionParameter>();
        Parameters.Add(InputParameter0);
        Parameters.Add(InputParameter1);
        Parameters.Add(InputParameter2);
        Parameters.Add(InputParameter3);
        Parameters.Add(InputParameter4);
        Parameters.Add(InputParameter5);
        Parameters.Add(InputParameter6);
        Parameters.Add(InputParameter7);
        Parameters.Add(InputParameter8);
        Parameters.Add(InputParameter9);
        Parameters.Add(OutputParameter0);
        Parameters.Add(OutputParameter1);
        string Ret = GenerateShaderExpression(ref Parameters, Expression, ref Params, ShaderNode);

        return Ret;
    }
    public static string GenerateShaderExpression(GodotShaderVariableType InType0, GodotShaderVariableType InType1, GodotShaderVariableType InType2,
        GodotShaderVariableType InType3, string Expression, GodotShaderVariableType OutType0, GodotShaderVariableType OutType1, GodotShaderVariableType OutType2,
        ref string Params, GodotShaderNode ShaderNode)
    {

        GodotShaderExpressionParameter InputParameter0 = new GodotShaderExpressionParameter(true, "input0", InType0);
        GodotShaderExpressionParameter InputParameter1 = new GodotShaderExpressionParameter(true, "input1", InType1);
        GodotShaderExpressionParameter InputParameter2 = new GodotShaderExpressionParameter(true, "input2", InType2);
        GodotShaderExpressionParameter InputParameter3 = new GodotShaderExpressionParameter(true, "input3", InType3);
        GodotShaderExpressionParameter OutputParameter0 = new GodotShaderExpressionParameter(false, "output0", OutType0);
        GodotShaderExpressionParameter OutputParameter1 = new GodotShaderExpressionParameter(false, "output1", OutType1);
        GodotShaderExpressionParameter OutputParameter2 = new GodotShaderExpressionParameter(false, "output2", OutType2);
        List<GodotShaderExpressionParameter> Parameters = new List<GodotShaderExpressionParameter>();
        Parameters.Add(InputParameter0);
        Parameters.Add(InputParameter1);
        Parameters.Add(InputParameter2);
        Parameters.Add(InputParameter3);
        Parameters.Add(OutputParameter0);
        Parameters.Add(OutputParameter1);
        Parameters.Add(OutputParameter2);
        string Ret = GenerateShaderExpression(ref Parameters, Expression, ref Params, ShaderNode);

        return Ret;
    }
    public static string GenerateShaderExpression(GodotShaderVariableType InType0, GodotShaderVariableType InType1, GodotShaderVariableType InType2,
         string Expression, GodotShaderVariableType OutType0, GodotShaderVariableType OutType1, GodotShaderVariableType OutType2,
        GodotShaderVariableType OutType3, GodotShaderVariableType OutType4,
        ref string Params, GodotShaderNode ShaderNode)
    {

        GodotShaderExpressionParameter InputParameter0 = new GodotShaderExpressionParameter(true, "input0", InType0);
        GodotShaderExpressionParameter InputParameter1 = new GodotShaderExpressionParameter(true, "input1", InType1);
        GodotShaderExpressionParameter InputParameter2 = new GodotShaderExpressionParameter(true, "input2", InType2);
        GodotShaderExpressionParameter OutputParameter0 = new GodotShaderExpressionParameter(false, "output0", OutType0);
        GodotShaderExpressionParameter OutputParameter1 = new GodotShaderExpressionParameter(false, "output1", OutType1);
        GodotShaderExpressionParameter OutputParameter2 = new GodotShaderExpressionParameter(false, "output2", OutType2);
        GodotShaderExpressionParameter OutputParameter3 = new GodotShaderExpressionParameter(false, "output3", OutType3);
        GodotShaderExpressionParameter OutputParameter4 = new GodotShaderExpressionParameter(false, "output4", OutType4);
        List<GodotShaderExpressionParameter> Parameters = new List<GodotShaderExpressionParameter>();
        Parameters.Add(InputParameter0);
        Parameters.Add(InputParameter1);
        Parameters.Add(InputParameter2);
        Parameters.Add(OutputParameter0);
        Parameters.Add(OutputParameter1);
        Parameters.Add(OutputParameter2);
        Parameters.Add(OutputParameter3);
        Parameters.Add(OutputParameter4);
        string Ret = GenerateShaderExpression(ref Parameters, Expression, ref Params, ShaderNode);

        return Ret;
    }
    public static string GenerateShaderExpression(GodotShaderVariableType InType0, GodotShaderVariableType InType1, GodotShaderVariableType InType2,
        GodotShaderVariableType InType3, string Expression, GodotShaderVariableType OutType0, GodotShaderVariableType OutType1, GodotShaderVariableType OutType2,
        GodotShaderVariableType OutType3, GodotShaderVariableType OutType4,
        ref string Params, GodotShaderNode ShaderNode)
    {

        GodotShaderExpressionParameter InputParameter0 = new GodotShaderExpressionParameter(true, "input0", InType0);
        GodotShaderExpressionParameter InputParameter1 = new GodotShaderExpressionParameter(true, "input1", InType1);
        GodotShaderExpressionParameter InputParameter2 = new GodotShaderExpressionParameter(true, "input2", InType2);
        GodotShaderExpressionParameter InputParameter3 = new GodotShaderExpressionParameter(true, "input3", InType3);
        GodotShaderExpressionParameter OutputParameter0 = new GodotShaderExpressionParameter(false, "output0", OutType0);
        GodotShaderExpressionParameter OutputParameter1 = new GodotShaderExpressionParameter(false, "output1", OutType1);
        GodotShaderExpressionParameter OutputParameter2 = new GodotShaderExpressionParameter(false, "output2", OutType2);
        GodotShaderExpressionParameter OutputParameter3 = new GodotShaderExpressionParameter(false, "output3", OutType3);
        GodotShaderExpressionParameter OutputParameter4 = new GodotShaderExpressionParameter(false, "output4", OutType4);
        List<GodotShaderExpressionParameter> Parameters = new List<GodotShaderExpressionParameter>();
        Parameters.Add(InputParameter0);
        Parameters.Add(InputParameter1);
        Parameters.Add(InputParameter2);
        Parameters.Add(InputParameter3);
        Parameters.Add(OutputParameter0);
        Parameters.Add(OutputParameter1);
        Parameters.Add(OutputParameter2);
        Parameters.Add(OutputParameter3);
        Parameters.Add(OutputParameter4);
        string Ret = GenerateShaderExpression(ref Parameters, Expression, ref Params, ShaderNode);

        return Ret;
    }
    public static string GenerateShaderExpression(string Expression, GodotShaderVariableType OutType0, GodotShaderVariableType OutType1, GodotShaderVariableType OutType2,
                                      GodotShaderVariableType OutType3, GodotShaderVariableType OutType4, ref string Params, GodotShaderNode ShaderNode)
    {
        GodotShaderExpressionParameter OutputParameter0 = new GodotShaderExpressionParameter(false, "output0", OutType0);
        GodotShaderExpressionParameter OutputParameter1 = new GodotShaderExpressionParameter(false, "output1", OutType1);
        GodotShaderExpressionParameter OutputParameter2 = new GodotShaderExpressionParameter(false, "output2", OutType2);
        GodotShaderExpressionParameter OutputParameter3 = new GodotShaderExpressionParameter(false, "output3", OutType3);
        GodotShaderExpressionParameter OutputParameter4 = new GodotShaderExpressionParameter(false, "output4", OutType4);
        List<GodotShaderExpressionParameter> Parameters = new List<GodotShaderExpressionParameter>();
        Parameters.Add(OutputParameter0);
        Parameters.Add(OutputParameter1);
        Parameters.Add(OutputParameter2);
        Parameters.Add(OutputParameter3);
        Parameters.Add(OutputParameter4);
        string Ret = GenerateShaderExpression(ref Parameters, Expression, ref Params, ShaderNode);

        return Ret;
    }
    public static string GenerateShaderExpression(string Expression, GodotShaderVariableType OutType0, GodotShaderVariableType OutType1, GodotShaderVariableType OutType2,
                                      GodotShaderVariableType OutType3, GodotShaderVariableType OutType4, GodotShaderVariableType OutType5, GodotShaderVariableType OutType6,
                                      GodotShaderVariableType OutType7, ref string Params, GodotShaderNode ShaderNode)
    {
        GodotShaderExpressionParameter OutputParameter0 = new GodotShaderExpressionParameter(false, "output0", OutType0);
        GodotShaderExpressionParameter OutputParameter1 = new GodotShaderExpressionParameter(false, "output1", OutType1);
        GodotShaderExpressionParameter OutputParameter2 = new GodotShaderExpressionParameter(false, "output2", OutType2);
        GodotShaderExpressionParameter OutputParameter3 = new GodotShaderExpressionParameter(false, "output3", OutType3);
        GodotShaderExpressionParameter OutputParameter4 = new GodotShaderExpressionParameter(false, "output4", OutType4);
        GodotShaderExpressionParameter OutputParameter5 = new GodotShaderExpressionParameter(false, "output5", OutType5);
        GodotShaderExpressionParameter OutputParameter6 = new GodotShaderExpressionParameter(false, "output6", OutType6);
        GodotShaderExpressionParameter OutputParameter7 = new GodotShaderExpressionParameter(false, "output7", OutType7);
        List<GodotShaderExpressionParameter> Parameters = new List<GodotShaderExpressionParameter>();
        Parameters.Add(OutputParameter0);
        Parameters.Add(OutputParameter1);
        Parameters.Add(OutputParameter2);
        Parameters.Add(OutputParameter3);
        Parameters.Add(OutputParameter4);
        Parameters.Add(OutputParameter5);
        Parameters.Add(OutputParameter6);
        Parameters.Add(OutputParameter7);
        string Ret = GenerateShaderExpression(ref Parameters, Expression, ref Params, ShaderNode);

        return Ret;
    }
    public enum GetDataTypeMode
    {
        GDTM_FOR_GLSL,
        GDTM_FOR_TRES,
        GDTM_FOR_PARAMS
    }
    public static string GetDataTypeName(GodotShaderVariableType DataType, GetDataTypeMode Mode = GetDataTypeMode.GDTM_FOR_TRES)
    {
        if ( Mode == GetDataTypeMode.GDTM_FOR_GLSL )
        {
            switch (DataType)
            {
                default: return null;
                case GodotShaderVariableType.GSVT_FLOAT  : return "float";
                case GodotShaderVariableType.GSVT_VECTOR2: return "vec2";
                case GodotShaderVariableType.GSVT_VECTOR3: return "vec3";
                case GodotShaderVariableType.GSVT_VECTOR4: return "vec4";
            }
        }
        else//For tres (shader) files
        {
            switch (DataType)
            {
                default: return null;
                case GodotShaderVariableType.GSVT_VECTOR2: return "Vector2";
                case GodotShaderVariableType.GSVT_VECTOR3: return "Vector3";
                case GodotShaderVariableType.GSVT_VECTOR4: if ( Mode == GetDataTypeMode.GDTM_FOR_PARAMS )
                                                                return "Vector4";
                                                           else
                                                                return "Quaternion";
            }
        }
    }
    public static string PromoteInputToVec4(string VarName, GodotShaderVariableType Type)
    {
        switch (Type)
        {
            default:
            case GodotShaderVariableType.GSVT_FLOAT: return string.Format( CultureInfo.InvariantCulture,"vec4( {0}, 0, 0, 0 )", VarName);
            case GodotShaderVariableType.GSVT_VECTOR2: return string.Format( CultureInfo.InvariantCulture,"vec4( {0}, 0, 0 )", VarName);
            case GodotShaderVariableType.GSVT_VECTOR3: return string.Format( CultureInfo.InvariantCulture,"vec4( {0}, 0 )", VarName);
            case GodotShaderVariableType.GSVT_VECTOR4: return VarName;
        }
    }
    public static string GetSwizzleFromVec4(string VarName, GodotShaderVariableType Type)
    {
        switch (Type)
        {
            default:
            case GodotShaderVariableType.GSVT_FLOAT: return string.Format( CultureInfo.InvariantCulture,"{0}.x", VarName);
            case GodotShaderVariableType.GSVT_VECTOR2: return string.Format( CultureInfo.InvariantCulture,"{0}.xy", VarName);
            case GodotShaderVariableType.GSVT_VECTOR3: return string.Format( CultureInfo.InvariantCulture,"{0}.xyz", VarName);
            case GodotShaderVariableType.GSVT_VECTOR4: return VarName;
        }
    }
    public static int GetNumComponentsFromVectorType(GodotShaderVariableType Type)
    {
        switch (Type)
        {
            default:
            case GodotShaderVariableType.GSVT_FLOAT: return 1;
            case GodotShaderVariableType.GSVT_VECTOR2: return 2;
            case GodotShaderVariableType.GSVT_VECTOR3: return 3;
            case GodotShaderVariableType.GSVT_VECTOR4: return 4;
            case GodotShaderVariableType.GSVT_TRANSFORM: return 16;
        }
    }
    public static GodotShaderVariableType NumComponentsToVectorType(int Components)
    {
        switch (Components)
        {
            default:
            case 1: return GodotShaderVariableType.GSVT_FLOAT;
            case 2: return GodotShaderVariableType.GSVT_VECTOR2;
            case 3: return GodotShaderVariableType.GSVT_VECTOR3;
            case 4: return GodotShaderVariableType.GSVT_VECTOR4;
            case 16: return GodotShaderVariableType.GSVT_TRANSFORM;
        }
    }
    public static GodotShaderVariableType GetVariableTypeFromString(string Type)
    {
        switch (Type)
        {
            case "Vector1": return GodotShaderVariableType.GSVT_FLOAT;
            case "Vector2": return GodotShaderVariableType.GSVT_VECTOR2;
            case "Vector3": return GodotShaderVariableType.GSVT_VECTOR3;
            case "Vector4": return GodotShaderVariableType.GSVT_VECTOR4;
            case "Texture2D": 
            case "Texture2DArray": 
            case "Cubemap": 
            case "Texture3D":return GodotShaderVariableType.GSVT_SAMPLER;
            case "Matrix3": return GodotShaderVariableType.GSVT_TRANSFORM;
            case "Matrix4": return GodotShaderVariableType.GSVT_TRANSFORM;
        }

        return GodotShaderVariableType.GSVT_UNKNOWN;
    }
    public static bool GetOperationsFromString(string NodeType, ref GodotFloatOperation FloatOp, ref GodotVectorOperation VecOp)
    {
        switch (NodeType)
        {

            case "UnityEditor.ShaderGraph.AddNode": FloatOp = GodotFloatOperation.GFO_ADD; VecOp = GodotVectorOperation.GVO_ADD; return true;
            case "UnityEditor.ShaderGraph.SubtractNode": FloatOp = GodotFloatOperation.GFO_SUBTRACT; VecOp = GodotVectorOperation.GVO_SUBTRACT; return true;
            case "UnityEditor.ShaderGraph.MultiplyNode": FloatOp = GodotFloatOperation.GFO_MULTIPLY; VecOp = GodotVectorOperation.GVO_MULTIPLY; return true;
            case "UnityEditor.ShaderGraph.DivideNode": FloatOp = GodotFloatOperation.GFO_DIVIDE; VecOp = GodotVectorOperation.GVO_DIVIDE; return true;
            //remainder
            case "UnityEditor.ShaderGraph.PowerNode": FloatOp = GodotFloatOperation.GFO_POWER; VecOp = GodotVectorOperation.GVO_POWER; return true;
            case "UnityEditor.ShaderGraph.MaximumNode": FloatOp = GodotFloatOperation.GFO_MAX; VecOp = GodotVectorOperation.GVO_MAX; return true;
            case "UnityEditor.ShaderGraph.MinimumNode": FloatOp = GodotFloatOperation.GFO_MIN; VecOp = GodotVectorOperation.GVO_MIN; return true;
            case "UnityEditor.ShaderGraph.CrossProductNode": FloatOp = GodotFloatOperation.GFO_UNKNOWN; VecOp = GodotVectorOperation.GVO_CROSS; return true;
            case "UnityEditor.ShaderGraph.Arctangent2Node": FloatOp = GodotFloatOperation.GFO_ATAN2; VecOp = GodotVectorOperation.GVO_ATAN2; return true;
            case "UnityEditor.ShaderGraph.ReflectionNode": FloatOp = GodotFloatOperation.GFO_UNKNOWN; VecOp = GodotVectorOperation.GVO_REFLECT; return true;
        }

        return false;
    }
    public static bool GetFuncsFromString(string NodeType, ref GodotFloatFunction FloatFunc, ref GodotVectorFunction VecFunc)
    {
        switch (NodeType)
        {
            case "UnityEditor.ShaderGraph.NormalizeNode": FloatFunc = GodotFloatFunction.GFF_UNKNOWN; VecFunc = GodotVectorFunction.GVF_NORMALIZE; return true;
            case "UnityEditor.ShaderGraph.SaturateNode": FloatFunc = GodotFloatFunction.GFF_SATURATE; VecFunc = GodotVectorFunction.GVF_SATURATE; return true;
            case "UnityEditor.ShaderGraph.NegateNode": FloatFunc = GodotFloatFunction.GFF_NEGATE; VecFunc = GodotVectorFunction.GVF_NEGATE; return true;
            case "UnityEditor.ShaderGraph.ReciprocalNode": FloatFunc = GodotFloatFunction.GFF_RECIP; VecFunc = GodotVectorFunction.GVF_RECIP; return true;
            case "UnityEditor.ShaderGraph.AbsoluteNode": FloatFunc = GodotFloatFunction.GFF_ABS; VecFunc = GodotVectorFunction.GVF_ABS; return true;
            case "UnityEditor.ShaderGraph.ArccosineNode": FloatFunc = GodotFloatFunction.GFF_ACOS; VecFunc = GodotVectorFunction.GVF_ACOS; return true;
            //ACOSH
            case "UnityEditor.ShaderGraph.ArcsineNode": FloatFunc = GodotFloatFunction.GFF_ASIN; VecFunc = GodotVectorFunction.GVF_ASIN; return true;
            //ASINH
            case "UnityEditor.ShaderGraph.ArctangentNode": FloatFunc = GodotFloatFunction.GFF_ATAN; VecFunc = GodotVectorFunction.GVF_ATAN; return true;
            //ATANH
            case "UnityEditor.ShaderGraph.CeilingNode": FloatFunc = GodotFloatFunction.GFF_CEIL; VecFunc = GodotVectorFunction.GVF_CEIL; return true;
            case "UnityEditor.ShaderGraph.CosineNode": FloatFunc = GodotFloatFunction.GFF_COS; VecFunc = GodotVectorFunction.GVF_COS; return true;
            //COSH
            //GVF_DEGREES
            case "UnityEditor.ShaderGraph.ExponentialNode": FloatFunc = GodotFloatFunction.GFF_EXP; VecFunc = GodotVectorFunction.GVF_EXP; return true;
            //EXP2
            case "UnityEditor.ShaderGraph.FloorNode": FloatFunc = GodotFloatFunction.GFF_FLOOR; VecFunc = GodotVectorFunction.GVF_FLOOR; return true;
            case "UnityEditor.ShaderGraph.FractionNode": FloatFunc = GodotFloatFunction.GFF_FRACT; VecFunc = GodotVectorFunction.GVF_FRACT; return true;
            case "UnityEditor.ShaderGraph.ReciprocalSquareRootNode": FloatFunc = GodotFloatFunction.GFF_INVSQRT; VecFunc = GodotVectorFunction.GVF_INVSQRT; return true;
            case "UnityEditor.ShaderGraph.LogNode": FloatFunc = GodotFloatFunction.GFF_LOG; VecFunc = GodotVectorFunction.GVF_LOG; return true;
            //LOG2
            //RADIANS
            case "UnityEditor.ShaderGraph.RoundNode": FloatFunc = GodotFloatFunction.GFF_ROUND; VecFunc = GodotVectorFunction.GVF_ROUND; return true;
            //ROUNDEVEN
            case "UnityEditor.ShaderGraph.SignNode": FloatFunc = GodotFloatFunction.GFF_SIGN; VecFunc = GodotVectorFunction.GVF_SIGN; return true;
            case "UnityEditor.ShaderGraph.SineNode": FloatFunc = GodotFloatFunction.GFF_SIN; VecFunc = GodotVectorFunction.GVF_SIN; return true;
            //SINH
            case "UnityEditor.ShaderGraph.SquareRootNode": FloatFunc = GodotFloatFunction.GFF_SQRT; VecFunc = GodotVectorFunction.GVF_SQRT; return true;
            case "UnityEditor.ShaderGraph.TangentNode": FloatFunc = GodotFloatFunction.GFF_TAN; VecFunc = GodotVectorFunction.GVF_TAN; return true;
            //TANH
            case "UnityEditor.ShaderGraph.TruncateNode": FloatFunc = GodotFloatFunction.GFF_TRUNC; VecFunc = GodotVectorFunction.GVF_TRUNC; return true;
            case "UnityEditor.ShaderGraph.OneMinusNode": FloatFunc = GodotFloatFunction.GFF_ONE_MINUS; VecFunc = GodotVectorFunction.GVF_ONE_MINUS; return true;
        }

        return false;
    }
    public static GodotShaderVariableType_Mix GetVariableType_Mix(GodotShaderVariableType Type)
    {
        switch (Type)
        {
            case GodotShaderVariableType.GSVT_FLOAT: return GodotShaderVariableType_Mix.VTM_SCALAR;
            case GodotShaderVariableType.GSVT_VECTOR2: return GodotShaderVariableType_Mix.VTM_VECTOR2;
            default:
            case GodotShaderVariableType.GSVT_VECTOR3: return GodotShaderVariableType_Mix.VTM_VECTOR3;
            case GodotShaderVariableType.GSVT_VECTOR4: return GodotShaderVariableType_Mix.VTM_VECTOR4;
        }
    }
    public static string GenerateVectorString(GodotShaderVariableType DataType, Vector4 DefaultValue, GetDataTypeMode Mode = GetDataTypeMode.GDTM_FOR_TRES )
    {
        string DataTypeStr = GetDataTypeName(DataType, Mode);
        string TypeAndValues = "";
        if (DataTypeStr != null)
        {
            TypeAndValues = DataTypeStr;
            TypeAndValues += "( ";
        }
        int NumComponents = GetNumComponentsFromVectorType(DataType);
        for (int i = 0; i < NumComponents; i++)
        {
            string Text = "";
            if (i > 0)
                TypeAndValues += ", ";
            float Val = DefaultValue[i];
            if ( DataType == GodotShaderVariableType.GSVT_INT || DataType == GodotShaderVariableType.GSVT_UINT )
                Text = Val.ToString();
            else
                Text = string.Format( CultureInfo.InvariantCulture, "{0:F2}", Val );
            TypeAndValues += Text;
        }

        if (DataTypeStr != null)
        {
            TypeAndValues += " )";
        }

        return TypeAndValues;
    }
    public static string GenerateVectorNode(GodotVectorOperation Operation, float DefaultA, float DefaultB, GodotShaderVariableType DataType, ref string Params)
    {
        string DefaultInputValues = GenerateDefaultInputValues2(DataType, new Vector4(DefaultA, DefaultA, DefaultA, DefaultA), new Vector4(DefaultB, DefaultB, DefaultB, DefaultB));

        GodotShaderVariableType_VectorOp OpType = (GodotShaderVariableType_VectorOp)((int)DataType - (int)GodotShaderVariableType.GSVT_VECTOR2);
        if (OpType > GodotShaderVariableType_VectorOp.VOP_VECTOR4)
        {
            Debug.Log("[UTG] GenerateVectorNode. invalid DataType=" + DataType);
            OpType = GodotShaderVariableType_VectorOp.VOP_VECTOR4;
        }

        string Text = string.Format( CultureInfo.InvariantCulture,
                  "operator = {0}\n" +
                  "{1}\n" +
                  "op_type = {2}\n",
                  (int)Operation, DefaultInputValues, (int)OpType
                );
        Params = Text;

        return "VisualShaderNodeVectorOp";
    }
    public static GodotShaderVariableType_DerivateFunc GetVariableType_DerivateFunc(GodotShaderVariableType Type)
    {
        switch (Type)
        {
            default:
            case GodotShaderVariableType.GSVT_FLOAT: return GodotShaderVariableType_DerivateFunc.OP_TYPE_SCALAR;
            case GodotShaderVariableType.GSVT_VECTOR2: return GodotShaderVariableType_DerivateFunc.OP_TYPE_VECTOR_2D;
            case GodotShaderVariableType.GSVT_VECTOR3: return GodotShaderVariableType_DerivateFunc.OP_TYPE_VECTOR_3D;
            case GodotShaderVariableType.GSVT_VECTOR4: return GodotShaderVariableType_DerivateFunc.OP_TYPE_VECTOR_4D;
            case GodotShaderVariableType.GSVT_TRANSFORM: return GodotShaderVariableType_DerivateFunc.OP_TYPE_VECTOR_4D;
        }
    }
    public static string GenerateOpNodeBasedOnType(GodotFloatOperation FloatOperation, GodotVectorOperation VectorOperation, GodotShaderVariableType DataType,
                                       float A, float B, ref string Params)
    {
        if (DataType == GodotShaderVariableType.GSVT_FLOAT)
            return GenerateFloatOpNode(FloatOperation, A, B, ref Params);
        else
            return GenerateVectorNode(VectorOperation, A, B, DataType, ref Params);
    }
    public static string GenerateFuncNodeBasedOnType(GodotFloatFunction FloatFunction, GodotVectorFunction VectorFunction, GodotShaderVariableType DataType,
                                       Vector4 DefaultValue, ref string Params)
    {
        if (DataType == GodotShaderVariableType.GSVT_FLOAT)
            return GenerateFloatFuncNode(FloatFunction, DefaultValue.x, ref Params);
        else
            return GenerateVectorFuncNode(VectorFunction, DataType, DefaultValue, ref Params);
    }
    public static string GenerateDefaultInputValues(GodotShaderVariableType DataType, Vector4 DefaultValue)
    {
        string Text;

        string DataValue = GenerateVectorString(DataType, DefaultValue);

        Text = string.Format( CultureInfo.InvariantCulture,
                  "default_input_values = [0, {0}]\n",
                  DataValue);

        return Text;
    }
    public static string GenerateDefaultInputValues2(GodotShaderVariableType DataType, Vector4 DefaultValue1, Vector4 DefaultValue2)
    {
        string Text;

        string DataValue1 = GenerateVectorString(DataType, DefaultValue1);
        string DataValue2 = GenerateVectorString(DataType, DefaultValue2);

        Text = string.Format( CultureInfo.InvariantCulture,
                  "default_input_values = [0, {0}, 1, {1}]",
                  DataValue1, DataValue2);

        return Text;
    }
    public static string GenerateDefaultInputValues3(GodotShaderVariableType DataType, Vector4 DefaultValue1, Vector4 DefaultValue2, Vector4 DefaultValue3)
    {
        string Text;

        string DataValue1 = GenerateVectorString(DataType, DefaultValue1);
        string DataValue2 = GenerateVectorString(DataType, DefaultValue2);
        string DataValue3 = GenerateVectorString(DataType, DefaultValue3);

        Text = string.Format( CultureInfo.InvariantCulture,
                        "default_input_values = [0, {0}, 1, {1}, 2, {2}]",
                    DataValue1, DataValue2, DataValue3);

        return Text;
    }
    public static GodotShaderVariableType Get3InputNodeDefaultValuesAndTypes(Vector4 DefaultValue1, Vector4 DefaultValue2, Vector4 DefaultValue3,
                                         GodotShaderVariableType DefaultType1, GodotShaderVariableType DefaultType2, GodotShaderVariableType DefaultType3,
                                         ref string DefaultInputValues)
    {
        GodotShaderVariableType Type1 = DefaultType1;
        GodotShaderVariableType Type2 = DefaultType2;
        GodotShaderVariableType Type3 = DefaultType3;

        GodotShaderVariableType FinalType = (GodotShaderVariableType)Math.Max((int)Math.Max((int)Type1, (int)Type2), (int)Type3);

        DefaultInputValues = GenerateDefaultInputValues3(FinalType, DefaultValue1, DefaultValue2, DefaultValue3);
        return FinalType;
    }
    public static GodotShaderVariableType Get2InputNodeDefaultValuesAndTypes(Vector4 DefaultValue1, Vector4 DefaultValue2,
                                                            GodotShaderVariableType DefaultType1, GodotShaderVariableType DefaultType2,
                                                            ref string DefaultInputValues)
    {
        GodotShaderVariableType Type1 = DefaultType1;
        GodotShaderVariableType Type2 = DefaultType2;

        GodotShaderVariableType FinalType = (GodotShaderVariableType)Math.Max((int)Type1, (int)Type2);

        DefaultInputValues = GenerateDefaultInputValues2(FinalType, DefaultValue1, DefaultValue2);
        return FinalType;
    }
    public static bool ExportVertexShader = true;
    public static void AddGlobalExpression(GodotShaderData ShaderData)
    {
        string shaderTypeStr = "vertex";
        ShaderType shaderType = ShaderType.VERTEX;

        if (!ExportVertexShader)
        {
            shaderType = ShaderType.FRAGMENT;
            shaderTypeStr = "fragment";
        }

        GodotShaderNode node = GenerateShaderNode(ShaderData, null, null, shaderType, "VisualShaderNodeGlobalExpression", null, null, 220, 240);

        string Signature = string.Format( CultureInfo.InvariantCulture, "//Exported with UnityToGodot {0} Get it from https://relativegames.gumroad.com/l/unitytogodot\n", Version );
        string expressionsContent = Signature +  @"
float saturate(float val)
{
    return clamp( val, 0.0, 1.0 );
}
vec2 saturate2(vec2 val)
{
    return clamp( val, 0.0, 1.0 );
}
vec3 saturate3(vec3 val)
{
    return clamp( val, 0.0, 1.0 );
}
void Unity_Hue(vec3 In, float pOffset, bool Normalized, out vec3 Out)
{
    // RGB to HSV
    vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    vec4 P = mix(vec4(In.bg, K.wz), vec4(In.gb, K.xy), step(In.b, In.g));
    vec4 Q = mix(vec4(P.xyw, In.r), vec4(In.r, P.yzx), step(P.x, In.r));
    float D = Q.x - min(Q.w, Q.y);
    float E2 = 1e-10;
    float V = (D == 0.0) ? Q.x : (Q.x + E2);
    vec3 hsv = vec3(abs(Q.z + (Q.w - Q.y)/(6.0 * D + E2)), D / (Q.x + E2), V);

    float hue = hsv.x + pOffset / 360.0;
    if ( Normalized )
        hue = hsv.x + pOffset;

    float temp = (hue > 1.0)
                ? hue - 1.0
                : hue;
    hsv.x = (hue < 0.0)
            ? hue + 1.0
            : temp;

    // HSV to RGB
    vec4 K2 = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    vec3 P2 = abs(fract(hsv.xxx + K2.xyz) * 6.0 - K2.www);
    Out = hsv.z * mix(K2.xxx, saturate3(P2 - K2.xxx), hsv.y);
}
void Unity_Remap_float(float In, vec2 InMinMax, vec2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}
float Unity_SimpleNoise_RandomValue_float (vec2 uv)
{
    float angle = dot(uv, vec2(12.9898, 78.233));
    //#if defined(SHADER_API_MOBILE) && (defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) || defined(SHADER_API_VULKAN))
        // 'sin()' has bad precision on Mali GPUs for inputs > 10000
        //angle = mod(angle, 2.0 * PI); // Avoid large inputs to sin()
    //#endif
    return fract(sin(angle)*43758.5453);
}
        
float Unity_SimpleNnoise_Interpolate_float (float a, float b, float t)
{
    return (1.0-t)*a + (t*b);
}
        
        
float Unity_SimpleNoise_ValueNoise_float (vec2 uv)
{
    vec2 i = floor(uv);
    vec2 f = fract(uv);
    f = f * f * (3.0 - 2.0 * f);
        
    uv = abs(fract(uv) - 0.5);
    vec2 c0 = i + vec2(0.0, 0.0);
    vec2 c1 = i + vec2(1.0, 0.0);
    vec2 c2 = i + vec2(0.0, 1.0);
    vec2 c3 = i + vec2(1.0, 1.0);
    float r0 = Unity_SimpleNoise_RandomValue_float(c0);
    float r1 = Unity_SimpleNoise_RandomValue_float(c1);
    float r2 = Unity_SimpleNoise_RandomValue_float(c2);
    float r3 = Unity_SimpleNoise_RandomValue_float(c3);
        
    float bottomOfGrid = Unity_SimpleNnoise_Interpolate_float(r0, r1, f.x);
    float topOfGrid = Unity_SimpleNnoise_Interpolate_float(r2, r3, f.x);
    float t = Unity_SimpleNnoise_Interpolate_float(bottomOfGrid, topOfGrid, f.y);
    return t;
}
void Unity_SimpleNoise_float(vec2 pUV, float pScale, out float Out)
{
    float t = 0.0;
        
    float freq = pow(2.0, float(0));
    float amp = pow(0.5, float(3-0));
    t += Unity_SimpleNoise_ValueNoise_float(vec2(pUV.x*pScale/freq, pUV.y*pScale/freq))*amp;
        
    freq = pow(2.0, float(1));
    amp = pow(0.5, float(3-1));
    t += Unity_SimpleNoise_ValueNoise_float(vec2(pUV.x*pScale/freq, pUV.y*pScale/freq))*amp;
        
    freq = pow(2.0, float(2));
    amp = pow(0.5, float(3-2));
    t += Unity_SimpleNoise_ValueNoise_float(vec2(pUV.x*pScale/freq, pUV.y*pScale/freq))*amp;
        
    Out = t;
}
void Unity_Blend_Burn_vec4(vec4 pBase, vec4 pBlend, float pOpacity, out vec4 Out)
{
    Out =  vec4(1,1,1,1) - (vec4(1,1,1,1) - pBlend)/pBase;
    Out = mix(pBase, Out, pOpacity);
}
void Unity_Blend_Darken_vec4(vec4 pBase, vec4 pBlend, float pOpacity, out vec4 Out)
{
    Out = min(pBlend, pBase);
    Out = mix(pBase, Out, pOpacity);
}
void Unity_Blend_Difference_vec4(vec4 pBase, vec4 pBlend, float pOpacity, out vec4 Out)
{
    Out = abs(pBlend - pBase);
    Out = mix(pBase, Out, pOpacity);
}
void Unity_Blend_Dodge_vec4(vec4 pBase, vec4 pBlend, float pOpacity, out vec4 Out)
{
    Out = pBase / (vec4(1,1,1,1) - pBlend);
    Out = mix(pBase, Out, pOpacity);
}
void Unity_Blend_Divide_vec4(vec4 pBase, vec4 pBlend, float pOpacity, out vec4 Out)
{
    Out = pBase / (pBlend + 0.000000000001);
    Out = mix(pBase, Out, pOpacity);
}
void Unity_Blend_Exclusion_vec4(vec4 pBase, vec4 pBlend, float pOpacity, out vec4 Out)
{
    Out = pBlend + pBase - (vec4(2.0,2.0,2.0,2.0) * pBlend * pBase);
    Out = mix(pBase, Out, pOpacity);
}
void Unity_Blend_HardLight_vec4(vec4 pBase, vec4 pBlend, float pOpacity, out vec4 Out)
{
    vec4 result1 = vec4(1,1,1,1) - vec4(2.0,2.0,2.0,2.0) * (vec4(1,1,1,1) - pBase) * (vec4(1,1,1,1) - pBlend);
    vec4 result2 = vec4(2.0,2.0,2.0,2.0) * pBase * pBlend;
    vec4 zeroOrOne = step(pBlend, vec4(0.5,0.5,0.5,0.5));
    Out = result2 * zeroOrOne + ( vec4(1,1,1,1) - zeroOrOne) * result1;
    Out = mix(pBase, Out, pOpacity);
}
void Unity_Blend_HardMix_vec4(vec4 pBase, vec4 pBlend, float pOpacity, out vec4 Out)
{
    Out = step( vec4(1,1,1,1) - pBase, pBlend);
    Out = mix(pBase, Out, pOpacity);
}
void Unity_Blend_Lighten_vec4(vec4 pBase, vec4 pBlend, float pOpacity, out vec4 Out)
{
    Out = max(pBlend, pBase);
    Out = mix(pBase, Out, pOpacity);
}
void Unity_Blend_LinearBurn_vec4(vec4 pBase, vec4 pBlend, float pOpacity, out vec4 Out)
{
    Out = pBase + pBlend - vec4(1,1,1,1);
    Out = mix(pBase, Out, pOpacity);
}
void Unity_Blend_LinearDodge_vec4(vec4 pBase, vec4 pBlend, float pOpacity, out vec4 Out)
{
    Out = pBase + pBlend;
    Out = mix(pBase, Out, pOpacity);
}
void Unity_Blend_LinearLight_vec4(vec4 pBase, vec4 pBlend, float pOpacity, out vec4 Out)
{
    //Out = pBlend < vec4(0.5,0.5,0.5,0.5) ? max(pBase + (vec4(2,2,2,2) * pBlend) - vec4(1,1,1,1), vec4(0,0,0,0)) : min(pBase + vec4(2,2,2,2) * (pBlend - vec4(0.5,0.5,0.5,0.5)), vec4(1,1,1,1));
    Out.x = pBlend.x < 0.5 ? max(pBase.x + (2.0 * pBlend.x) - 1.0, 0.0) : min(pBase.x + 2.0 * (pBlend.x - 0.5), 1.0);
    Out.y = pBlend.y < 0.5 ? max(pBase.y + (2.0 * pBlend.y) - 1.0, 0.0) : min(pBase.y + 2.0 * (pBlend.y - 0.5), 1.0);
    Out.z = pBlend.z < 0.5 ? max(pBase.z + (2.0 * pBlend.z) - 1.0, 0.0) : min(pBase.z + 2.0 * (pBlend.z - 0.5), 1.0);
    Out.w = pBlend.w < 0.5 ? max(pBase.w + (2.0 * pBlend.w) - 1.0, 0.0) : min(pBase.w + 2.0 * (pBlend.w - 0.5), 1.0);
    Out = mix(pBase, Out, pOpacity);
}
void Unity_Blend_LinearLightAddSub_vec4(vec4 pBase, vec4 pBlend, float pOpacity, out vec4 Out)
{
    Out = pBlend + vec4(2.0,2.0,2.0,2.0) * pBase - vec4(1,1,1,1);
    Out = mix(pBase, Out, pOpacity);
}
void Unity_Blend_Multiply_vec4(vec4 pBase, vec4 pBlend, float pOpacity, out vec4 Out)
{
    Out = pBase * pBlend;
    Out = mix(pBase, Out, pOpacity);
}
void Unity_Blend_Negation_vec4(vec4 pBase, vec4 pBlend, float pOpacity, out vec4 Out)
{
    Out = vec4(1,1,1,1) - abs(vec4(1,1,1,1) - pBlend - pBase);
    Out = mix(pBase, Out, pOpacity);
}
void Unity_Blend_Overlay_vec4(vec4 pBase, vec4 pBlend, float pOpacity, out vec4 Out)
{
    vec4 result1 = vec4(1,1,1,1) - vec4(2.0,2.0,2.0,2.0) * (vec4(1,1,1,1) - pBase) * (vec4(1,1,1,1) - pBlend);
    vec4 result2 = vec4(2.0,2.0,2.0,2.0) * pBase * pBlend;
    vec4 zeroOrOne = step(pBase, vec4(0.5,0.5,0.5,0.5));
    Out = result2 * zeroOrOne + ( vec4(1,1,1,1) - zeroOrOne) * result1;
    Out = mix(pBase, Out, pOpacity);
}
void Unity_Blend_PinLight_vec4(vec4 pBase, vec4 pBlend, float pOpacity, out vec4 Out)
{
    vec4 check = step( vec4(0.5,0.5,0.5,0.5), pBlend);
    vec4 result1 = check * max(vec4(2.0,2.0,2.0,2.0) * (pBase - vec4(0.5,0.5,0.5,0.5)), pBlend);
    Out = result1 + (vec4(1,1,1,1) - check) * min(vec4(2.0,2.0,2.0,2.0) * pBase, pBlend);
    Out = mix(pBase, Out, pOpacity);
}
void Unity_Blend_Screen_vec4(vec4 pBase, vec4 pBlend, float pOpacity, out vec4 Out)
{
    Out = vec4(1,1,1,1) - (vec4(1,1,1,1) - pBlend) * (vec4(1,1,1,1) - pBase);
    Out = mix(pBase, Out, pOpacity);
}
void Unity_Blend_SoftLight_vec4(vec4 pBase, vec4 pBlend, float pOpacity, out vec4 Out)
{
    vec4 result1 = vec4(2.0,2.0,2.0,2.0) * pBase * pBlend + pBase * pBase * (vec4(1,1,1,1) - vec4(2.0,2.0,2.0,2.0) * pBlend);
    vec4 result2 = sqrt(pBase) * (vec4(2.0,2.0,2.0,2.0) * pBlend - vec4(1,1,1,1)) + vec4(2.0,2.0,2.0,2.0) * pBase * (vec4(1,1,1,1) - pBlend);
    vec4 zeroOrOne = step( vec4(0.5,0.5,0.5,0.5), pBlend);
    Out = result2 * zeroOrOne + ( vec4(1,1,1,1) - zeroOrOne) * result1;
    Out = mix(pBase, Out, pOpacity);
}
void Unity_Blend_Subtract_vec4(vec4 pBase, vec4 pBlend, float pOpacity, out vec4 Out)
{
    Out = pBase - pBlend;
    Out = mix(pBase, Out, pOpacity);
}
void Unity_Blend_VividLight_vec4(vec4 pBase, vec4 pBlend, float pOpacity, out vec4 Out)
{
    vec4 result1 = vec4(1,1,1,1) - (vec4(1,1,1,1) - pBlend) / (vec4(2.0,2.0,2.0,2.0) * pBase);
    vec4 result2 = pBlend / (vec4(2.0,2.0,2.0,2.0) * (vec4(1,1,1,1) - pBase));
    vec4 zeroOrOne = step( vec4(0.5,0.5,0.5,0.5), pBase);
    Out = result2 * zeroOrOne + (vec4(1,1,1,1) - zeroOrOne) * result1;
    Out = mix(pBase, Out, pOpacity);
}
void Unity_Blend_Overwrite_vec4(vec4 pBase, vec4 pBlend, float pOpacity, out vec4 Out)
{
    Out = mix(pBase, pBlend, pOpacity);
}
void Unity_Rotate_About_Axis_Degrees_float(vec3 In, vec3 Axis, float pRotation, out vec3 Out)
{    
    float s = sin(pRotation);
    float c = cos(pRotation);
    float one_minus_c = 1.0 - c;
        
    Axis = normalize(Axis);
        
    mat3 rot_mat = mat3( vec3( one_minus_c * Axis.x * Axis.x + c,            one_minus_c * Axis.x * Axis.y - Axis.z * s,     one_minus_c * Axis.z * Axis.x + Axis.y * s ),
                        vec3( one_minus_c * Axis.x * Axis.y + Axis.z * s,   one_minus_c * Axis.y * Axis.y + c,              one_minus_c * Axis.y * Axis.z - Axis.x * s ),
                        vec3( one_minus_c * Axis.z * Axis.x - Axis.y * s,   one_minus_c * Axis.y * Axis.z + Axis.x * s,     one_minus_c * Axis.z * Axis.z + c )
                            );
        
    Out = In * rot_mat;
}
void Unity_Rotate_Radians_float(vec2 pUV, vec2 Center, float pRotation, out vec2 Out)
{
    //rotation matrix
    pUV -= Center;
    float s = sin(pRotation);
    float c = cos(pRotation);
        
    //center rotation matrix
    mat2 rMatrix = mat2( vec2(c, -s), vec2(s, c) );
    rMatrix *= 0.5;
    rMatrix += 0.5;
    rMatrix = rMatrix*2.0 - 1.0;
        
    //multiply the UVs by the rotation matrix
    pUV.xy = pUV.xy * rMatrix;
    pUV += Center;
        
    Out = pUV;
}
const float FLT_MIN = 1.175494351e-38F;
vec3 SafeNormalize(vec3 inVec)
{
    float dp3 = max(FLT_MIN, dot(inVec, inVec));
    return inVec * inversesqrt(dp3);
}
vec3 TransformWorldToTangent(vec3 normalWS, mat3 tangentToWorld )
{
    bool doNormalize = true;
    // Note matrix is in row major convention with left multiplication as it is build on the fly
    vec3 row0 = tangentToWorld[0];
    vec3 row1 = tangentToWorld[1];
    vec3 row2 = tangentToWorld[2];

    // these are the columns of the inverse matrix but scaled by the determinant
    vec3 col0 = cross(row1, row2);
    vec3 col1 = cross(row2, row0);
    vec3 col2 = cross(row0, row1);

    float determinant = dot(row0, col0);

    // inverse transposed but scaled by determinant
    // Will remove transpose part by using matrix as the first arg in the mul() below
    // this makes it the exact inverse of what TransformTangentToWorld() does.
    mat3 matTBN_I_T = mat3(col0, col1, col2);
    vec3 result = normalWS * matTBN_I_T;
    if (doNormalize)
    {
        float sgn = determinant < 0.0 ? (-1.0) : 1.0;
        return SafeNormalize(sgn * result);
    }
    else
        return result / determinant;
}
void Hash_Tchou_2_2_uint(uvec2 v, out uvec2 o)
{
    // ~8 alu (2 mul)
    v.y ^= 1103515245U;
    v.x += v.y;
    v.x *= v.y;
    v.x ^= v.x >> 5u;
    v.x *= 0x27d4eb2du;
    v.y ^= (v.x << 3u);
    o = v;
}
void Hash_Tchou_2_2_float(vec2 i, out vec2 o)
{
    uvec2 r;
    uvec2 v = uvec2(ivec2(round(i)));
    Hash_Tchou_2_2_uint(v, r);
    o.x = float(r.x >> 8u) * (1.0 / float(0x00ffffff));
    o.y = float(r.y >> 8u) * (1.0 / float(0x00ffffff));
}
vec2 Unity_Voronoi_RandomVector_Deterministic_float(vec2 uv, float offset)
{
    Hash_Tchou_2_2_float(uv, uv);
    return vec2(sin(uv.y * offset), cos(uv.x * offset)) * 0.5 + 0.5;
}
void Hash_LegacySine_2_2_float(vec2 i, out vec2 o)
{
    mat2 m = mat2( vec2( 15.27, 47.63), vec2( 99.41, 89.98) );
    vec2 angles = i * m;

    o = fract(sin(angles));
}
vec2 Unity_Voronoi_RandomVector_LegacySine_float(vec2 uv, float offset)
{
    Hash_LegacySine_2_2_float(uv, uv);
    return vec2(sin(uv.y * offset), cos(uv.x * offset)) * 0.5 + 0.5;
}
float SafePositivePow_float( float pBase, float power)
{
    float FLT_EPS =  5.960464478e-8;
    return pow(max(abs(pBase), float(FLT_EPS)), power);
}
struct Gradient
{
    int type;
    int colorsLength;
    int alphasLength;
    vec4 colors[8];
    vec2 alphas[8];
};

Gradient NewGradient(int type, int colorsLength, int alphasLength,
    vec4 colors0, vec4 colors1, vec4 colors2, vec4 colors3, vec4 colors4, vec4 colors5, vec4 colors6, vec4 colors7,
    vec2 alphas0, vec2 alphas1, vec2 alphas2, vec2 alphas3, vec2 alphas4, vec2 alphas5, vec2 alphas6, vec2 alphas7)
{
    vec4 colors[8] = { colors0, colors1, colors2, colors3, colors4, colors5, colors6, colors7 };
	vec2 alphas[8] = { alphas0, alphas1, alphas2, alphas3, alphas4, alphas5, alphas6, alphas7 };
    Gradient output = Gradient(    
        type, colorsLength, alphasLength,
        colors, alphas        
    );
    return output;
}
vec3 LinearToOklab(vec3 rgb)
{
    float l = 0.4122214708f * rgb.r + 0.5363325363f * rgb.g + 0.0514459929f * rgb.b;
    float m = 0.2119034982f * rgb.r + 0.6806995451f * rgb.g + 0.1073969566f * rgb.b;
    float s = 0.0883024619f * rgb.r + 0.2817188376f * rgb.g + 0.6299787005f * rgb.b;

    float l_ = pow(l, 0.333333f);
    float m_ = pow(m, 0.333333f);
    float s_ = pow(s, 0.333333f);

    return vec3(
        0.2104542553f * l_ + 0.7936177850f * m_ - 0.0040720468f * s_,
        1.9779984951f * l_ - 2.4285922050f * m_ + 0.4505937099f * s_,
        0.0259040371f * l_ + 0.7827717662f * m_ - 0.8086757660f * s_);
}

vec3 OklabToLinear(vec3 lab)
{
    float l_ = lab.r + 0.3963377774f * lab.g + 0.2158037573f * lab.b;
    float m_ = lab.r - 0.1055613458f * lab.g - 0.0638541728f * lab.b;
    float s_ = lab.r - 0.0894841775f * lab.g - 1.2914855480f * lab.b;

    float l = l_ * l_ * l_;
    float m = m_ * m_ * m_;
    float s = s_ * s_ * s_;

    return vec3(
         4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s,
		-1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s,
		-0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s);
}
void Hash_Tchou_2_1_uint(uvec2 v, out uint o)
{
    // ~6 alu (2 mul)
    v.y ^= 1103515245U;
    v.x += v.y;
    v.x *= v.y;
    v.x ^= v.x >> 5u;
    v.x *= 0x27d4eb2du;
    o = v.x;
}
void Hash_Tchou_2_1_float(vec2 i, out float o)
{
    uint r;
    uvec2 v = uvec2(ivec2( round(i) ));
    Hash_Tchou_2_1_uint(v, r);
    o = float(r >> 8u) * (1.0 / float(0x00ffffff));
}
vec2 Unity_GradientNoise_Deterministic_Dir_float(vec2 p)
{
    float x; Hash_Tchou_2_1_float(p, x);
    return normalize(vec2(x - floor(x + 0.5), abs(x) - 0.5));
}
vec3 UnpackNormalmap(vec4 packednormal)
{
    // This do the trick
   //packednormal.x *= packednormal.w;

    vec3 normal;
    normal.xy = packednormal.xy * vec2( 2.0, 2.0 ) - vec2( 1.0, 1.0 );
    normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy)));
    return normalize( normal );
}
vec4 SampleNormalMap( sampler2D pNormalMap, vec2 puv )
{
    vec4 Ret = texture( pNormalMap, puv );
    Ret.xyz = UnpackNormalmap( Ret );
    return Ret;
}
float ComputeFogFactorZ0ToFar(float z, float RenderSettingsDensity)
{
    float start = 0.0;
    float end = 1000.0;
    vec4 unity_FogParams = vec4( RenderSettingsDensity / sqrt(log(2.0)),
    RenderSettingsDensity / log(2.0),
    -1.0/(end-start),
    end/(end-start)
    );

    #if 1//defined(FOG_LINEAR)
    // factor = (end-z)/(end-start) = z * (-1/(end-start)) + (end/(end-start))
    float fogFactor = saturate(z * unity_FogParams.z + unity_FogParams.w);
    return float(fogFactor);
    #elif defined(FOG_EXP) || defined(FOG_EXP2)
    // factor = exp(-(density*z)^2)
    // -density * z computed at vertex
    return float(unity_FogParams.x * z);
    #else
        return float(0.0);
    #endif
}
float ComputeFogIntensity(float fogFactor)
{
    float fogIntensity = 0.0;
    #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
        #if defined(FOG_EXP)
            // factor = exp(-density*z)
            // fogFactor = density*z compute at vertex
            fogIntensity = saturate(exp2(-fogFactor));
        #elif defined(FOG_EXP2)
            // factor = exp(-(density*z)^2)
            // fogFactor = density*z compute at vertex
            fogIntensity = saturate(exp2(-fogFactor * fogFactor));
        #else//if defined(FOG_LINEAR)
            fogIntensity = fogFactor;
        #endif
    #endif
    return fogIntensity;
}
vec4 TransformWorldToShadowCoord(vec3 WorldPos)
{
	return vec4(0,0,0,0);
}
struct Light
{
    vec3 direction;
    vec3 color;
    float distanceAttenuation;
    float shadowAttenuation;
};
Light GetMainLight(vec4 shadowCoord)
{
    Light MainLight;
    MainLight.direction = vec3(0,-1,0);
    MainLight.color = vec3(1,1,1);
    MainLight.distanceAttenuation = 1.0f;
    MainLight.shadowAttenuation = 1.0f;
    return MainLight;
}
vec3 SHEvalLinearL0L1(vec3 N, vec4 shAr, vec4 shAg, vec4 shAb)
{
    vec4 vA = vec4(N, 1.0);

    vec3 x1;
    // Linear (L1) + constant (L0) polynomial terms
    x1.r = dot(shAr, vA);
    x1.g = dot(shAg, vA);
    x1.b = dot(shAb, vA);

    return x1;
}
vec3 SHEvalLinearL2(vec3 N, vec4 shBr, vec4 shBg, vec4 shBb, vec4 shC)
{
    vec3 x2;
    // 4 of the quadratic (L2) polynomials
    vec4 vB = N.xyzz * N.yzzx;
    x2.r = dot(shBr, vB);
    x2.g = dot(shBg, vB);
    x2.b = dot(shBb, vB);

    // Final (5th) quadratic (L2) polynomial
    float vC = N.x * N.x - N.y * N.y;
    vec3 x3 = shC.rgb * vC;

    return x2 + x3;
}
vec3 SampleSH9(vec4 SHCoefficients[7], vec3 N)
{
    vec4 shAr = SHCoefficients[0];
    vec4 shAg = SHCoefficients[1];
    vec4 shAb = SHCoefficients[2];
    vec4 shBr = SHCoefficients[3];
    vec4 shBg = SHCoefficients[4];
    vec4 shBb = SHCoefficients[5];
    vec4 shCr = SHCoefficients[6];

    // Linear + constant polynomial terms
    vec3 res = SHEvalLinearL0L1(N, shAr, shAg, shAb);

    // Quadratic polynomials
    res += SHEvalLinearL2(N, shBr, shBg, shBb, shCr);

#ifdef UNITY_COLORSPACE_GAMMA
    res = LinearToSRGB(res);
#endif

    return res;
}
";

        for (int i = 0; i < ShaderData.CustomFuncFiles.Count; i++)
        {
            expressionsContent += ShaderData.CustomFuncFiles[i];
            expressionsContent += "\n";
        }

        node.DefinitionText += $"size = Vector2(940, 380)\nexpression = \"{expressionsContent}\"\n";

        node.GraphConfigText += string.Format( CultureInfo.InvariantCulture,
            "nodes/{0}/{1}/size = Vector2(940, 380)\n" +
            "nodes/{0}/{1}/input_ports = \"\"\n" +
            "nodes/{0}/{1}/output_ports = \"\"\n" +
            "nodes/{0}/{1}/expression = \"{2}\"\n",
            shaderTypeStr, node.ID, expressionsContent
        );
    }
    public static string ToString( UnityEngine.Color C, bool ForShaders = true )
    {
        string Ret = "";
        if ( ForShaders )
        {     
            Ret = C.ToString().Replace("RGBA","vec4");
        }
        else//ForMaterials
        {
            Ret = C.ToString().Replace("RGBA","Color");
        }
        return Ret;
    }
    public static GodotShaderNode AddNoConnectionConstantNode(UTGSlot Slot, GodotShaderData ShaderData, GodotShaderNode ShaderNode, ShaderType ShaderType, int HeightIndex = 0)//, bool IgnoreTextures = false )
    {
        string DependentNodeParams = "";
        string NodeTypeName = "";
        GodotShaderNode DependentNode = null;
        if (Slot.ValueType == "Vector1")
            NodeTypeName = GenerateConstantNode(Slot.Vec4.x, ref DependentNodeParams);
        if (Slot.ValueType == "Vector2")
            NodeTypeName = GenerateConstant2Node(new Vector2(Slot.Vec4.x, Slot.Vec4.y), ref DependentNodeParams);
        if (Slot.ValueType == "Vector3")
        {
            if (Slot.Slot.GetType().Name.Contains("PositionMaterialSlot"))
            {
                NodeTypeName = "VisualShaderNodeExpression";
            }
            else if (Slot.Slot.GetType().Name.Contains("NormalMaterialSlot"))
            {
                NodeTypeName = "VisualShaderNodeExpression";
            }
            else
                NodeTypeName = GenerateConstant3Node(new Vector3(Slot.Vec4.x, Slot.Vec4.y, Slot.Vec4.z), ref DependentNodeParams);
        }
        if (Slot.ValueType == "Vector4")
            NodeTypeName = GenerateConstant4Node(Slot.Vec4, ref DependentNodeParams);
        if (Slot.ValueType == "UVMaterialSlot")
        {
            string InputName = "uv";
#if UNITY_SHADER_GRAPH
            if (Slot.Channel != UVChannel.UV0)
                InputName = "uv2";
#endif
            NodeTypeName = GenerateInputNode(InputName, ref DependentNodeParams);
        }
        if (Slot.ValueType == "Boolean")
            NodeTypeName = GenerateBoolConstantNode(Slot.Vec4.x > 0.5f, ref DependentNodeParams);

        //Skip these
        if (Slot.ValueType == "SamplerState" )
            return null;
        //This must be here otherwise default textures for subgraphs have no fix !
        if ( Slot.ValueType == "Texture2D" || Slot.ValueType == "TextureCube")
        {
            //It already adds a connection inside !
            DependentNode = GenerateTextureParameterForSlot(Slot, ShaderData, ShaderNode, ShaderNode.ShaderType);
            return DependentNode;
        }

        DependentNode = GenerateShaderNode(ShaderData, null, null,
                    ShaderType, NodeTypeName, ShaderNode.GraphObject, ShaderNode.ParentGraphNode, (int)ShaderNode.Position.x - 500, (int)ShaderNode.Position.y + 200 * (HeightIndex + 1));

        if (Slot.ValueType == "Vector3")
        {
            if (Slot.Slot.GetType().Name.Contains("PositionMaterialSlot"))
            {
                string Text = string.Format( CultureInfo.InvariantCulture,
                    "//PositionNode:{0}\n" +
                    "output0 = ( INV_VIEW_MATRIX * vec4( VERTEX, 1.0) ).xyz;\n"
                    , DependentNode.ID
                    );
                GenerateShaderExpression(Text, GodotShaderVariableType.GSVT_VECTOR3, ref DependentNodeParams, DependentNode);
            }
            else if (Slot.Slot.GetType().Name.Contains("NormalMaterialSlot"))
            {
                string Text = string.Format( CultureInfo.InvariantCulture,
                    "//NormalMaterialSlot:{0}\n" +
                    "output0 = NORMAL * mat3(INV_VIEW_MATRIX);\n"//this works well with the triplanar node
                                                                 //"output0 = vec3( 0.0, 0.0, 1.0 );\n"
                    , DependentNode.ID
                    );
                GenerateShaderExpression(Text, GodotShaderVariableType.GSVT_VECTOR3, ref DependentNodeParams, DependentNode);
            }
        }

        DependentNode.DefinitionText += DependentNodeParams;
        ShaderData.AddShaderConnection(DependentNode, 0, ShaderNode, Slot.InputIndex, ShaderType);

        return DependentNode;
    }
    public static GodotShaderNode GenerateTextureParameterForSlot(UTGSlot TextureSlot, GodotShaderData ShaderData, GodotShaderNode ShaderNode, ShaderType CurrentShaderType)
    {
        GodotShaderNode DependentNode = null;
        if (TextureSlot != null && !TextureSlot.isConnected)//&& TextureSlot.texture != null )
        {
            ExportTexture ExportTexture = AddExportTexture(TextureSlot.texture, ref ShaderData.Textures);            
            //Create parameter even when texture is null so the godot shader doesn't fail to compile when it's connected to an expression node
            //if ( ExportTexture == null || ExportTexture.Node == null )
            {
                string NodeType = "VisualShaderNodeTexture2DParameter";
                if (TextureSlot.texture as Cubemap)
                    NodeType = "VisualShaderNodeCubemapParameter";
                if (TextureSlot.texture as Texture2DArray)
                    NodeType = "VisualShaderNodeTexture2DArrayParameter";
                DependentNode = GenerateShaderNode(ShaderData, null, null,
                    CurrentShaderType, NodeType, ShaderNode.GraphObject, ShaderNode.ParentGraphNode, (int)ShaderNode.Position.x - 500, (int)ShaderNode.Position.y + 200);
                if (ExportTexture != null)
                    ExportTexture.Node = DependentNode;
                string DependentNodeParams = "";
                string ParameterName = "None";
                if (TextureSlot.texture != null)
                    ParameterName = TextureSlot.texture.name;

                ShaderParameter Parameter = ShaderData.AddShaderParameter(ref ParameterName, ParameterName, ShaderParameterType.SPT_TEXTURE, CurrentShaderType, 0.0f, Vector4.zero, TextureSlot.texture, false, 0, DependentNode);

                ExportTextureType TextureType = ExportTextureType.GTT_COLOR;
                if (TextureSlot.texture != null)
                    TextureType = IsNormalMap(TextureSlot.texture) ? ExportTextureType.GTT_NORMAL : ExportTextureType.GTT_COLOR;
                GenerateTextureParameterNode(ParameterName, GodotFilterMode.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC, GodotRepeatMode.TEXTURE_REPEAT_ENABLED, TextureType, GodotColorDefault.GCD_BLACK, ref DependentNodeParams);
                DependentNode.DefinitionText += DependentNodeParams;
            }

            ShaderData.AddShaderConnection(DependentNode, 0, ShaderNode, TextureSlot.InputIndex, CurrentShaderType);
        }
        return DependentNode;
    }
    public static void AddConstantNodesForUnconnectedInputs(GodotShaderData ShaderData, GodotShaderNode ShaderNode, ShaderType ShaderType)
    {
        //For vertex shader we run this again so don't create needless duplicates
        if ( ShaderType == ShaderType.VERTEX )
            return;

        int HeightIndex = 0;
        for (int i = 0; i < ShaderNode.ProcessedSlots.Count; i++)
        {
            UTGSlot Slot = ShaderNode.ProcessedSlots[i];
            if (Slot != null && Slot.Input && !Slot.isConnected)
            {
                AddNoConnectionConstantNode(Slot, ShaderData, ShaderNode, ShaderType, HeightIndex);
                HeightIndex++;
            }
        }
    }
    public static int Compare(float a, float b, float epsilon = 1e-5f)
    {
        if (Math.Abs(a - b) < epsilon)
            return 0;       // equal within tolerance
        return a < b ? -1 : 1;
    }
    public static void AddConstantNodesForUnconnectedMaterialOutputs(GodotShaderData ShaderData, GodotShaderNode ShaderNode, ShaderType ShaderType)
    {
        int HeightIndex = 0;
        for (int i = 0; i < ShaderNode.ProcessedSlots.Count; i++)
        {
            UTGSlot Slot = ShaderNode.ProcessedSlots[i];
            if (Slot != null && Slot.Input && !Slot.isConnected)
            {
                bool Create = false;
                if (Slot.Name == "Base Color")
                {
                    Create = true;
                }
                else if (Slot.Name == "Emission")
                {
                    if (Slot.Vec4 != new Vector4(0, 0, 0, 0))
                        Create = true;
                }
                else if (Slot.Name == "Alpha" || Slot.Name == "Ambient Occlusion")
                {
                    if (Compare(Slot.Vec4.x, 1.0f) != 0)
                        Create = true;
                }
                else if (Slot.Name == "Alpha Clip Threshold" || Slot.Name == "Smoothness")
                {
                    //For Alpha Clipping to work in Godot we need to link something to AlphaScissorThreshold
                    if (Slot.Name == "Alpha Clip Threshold" && ShaderData.AlphaClipping)
                        Create = true;
                    else
                    {
                        //Unity default is 0.5 while Godot is 1.0 Roughness
                        if (Compare(Slot.Vec4.x, 0.0f) != 0)
                            Create = true;
                    }
                }
                else if (Slot.Name == "Metallic")
                {
                    if (Compare(Slot.Vec4.x, 0.0f) != 0)
                        Create = true;
                }
                if (Create)
                {
                    AddNoConnectionConstantNode(Slot, ShaderData, ShaderNode, ShaderType, HeightIndex);
                    HeightIndex++;
                }
            }
        }
    }
    public static bool UsesOnlyOutputConnection(GodotShaderData ShaderData, GodotShaderNode ShaderNode, int OutputIndex = 0)
    {
        int OutConnections = 0;
        bool Ret = true;
        for (int i = 0; i < ShaderNode.ProcessedSlots.Count; i++)
        {
            UTGSlot Slot = ShaderNode.ProcessedSlots[i];
            if (!Slot.Input)
            {
                if (Slot.isConnected)
                {
                    OutConnections++;

                    if (Slot.OutputIndex != OutputIndex)
                    {
                        Ret = false;
                    }
                }
            }
        }

        return Ret;
    }
    public static int StringReplacement(ref string originalString, string what, string replace, int startPos = 0)
    {
        int Found = originalString.IndexOf(what);
        originalString = originalString.Replace(what, replace);
        if (Found == -1)
            return 0;
        else
            return 1;
    }
    public static void ReplaceHLSLSampleCall(ref string Exp, string strToFind, string ReplacementFunc)
    {
        int FindLen = strToFind.Length;

        int pos = -1;
        do
        {
            pos = Exp.IndexOf(strToFind);
            if (pos != -1)
            {
                string TexName = "";
                int TexBegin = -1;
                for (int i = pos - 1; i > 0; i--)
                {
                    char c = Exp[i];
                    if (c == ' ' || c == '\t' || c == '\n' || c == ',' || c == '(' || c == ')' || c == '{' || c == '}')
                    {
                        TexName = Exp.Substring(i + 1, pos - i - 1);
                        TexBegin = i + 1;
                        break;
                    }
                }
                if (TexBegin == -1)
                {
                    Console.WriteLine("[UTX] ERROR! ProcessCustomExpression couldn't detect TexBegin!");
                    return;
                }

                string Replacement = ReplacementFunc;
                Replacement += TexName;
                Replacement += ", ";
                Exp = Exp.Substring(0, TexBegin) + Replacement + Exp.Substring(TexBegin + TexName.Length + FindLen);
            }
        } while (pos != -1);
    }
    public static void FixCustomExpressionCode(ref string Expression)
    {
        Expression = "#ifndef GODOT\n\t#define GODOT\n#endif\n" + Expression;
        
        StringReplacement(ref Expression, "float2x2", "mat2");
        StringReplacement(ref Expression, "float3x3", "mat3");
        StringReplacement(ref Expression, "float4x4", "mat4");
        StringReplacement(ref Expression, "float2", "vec2");
        StringReplacement(ref Expression, "float3", "vec3");
        StringReplacement(ref Expression, "float4", "vec4");
        StringReplacement(ref Expression, "uint2", "uvec2");
        StringReplacement(ref Expression, "uint3", "uvec3");
        StringReplacement(ref Expression, "uint4", "uvec4");
        StringReplacement(ref Expression, "int2", "ivec2");
        StringReplacement(ref Expression, "int3", "ivec3");
        StringReplacement(ref Expression, "int4", "ivec4");
        StringReplacement(ref Expression, "lerp", "mix");
        StringReplacement(ref Expression, "DDX", "dFdx");
        StringReplacement(ref Expression, "DDY", "dFdy");
        StringReplacement(ref Expression, "atan2", "atan");
        StringReplacement(ref Expression, "rsqrt", "inversesqrt");
        StringReplacement(ref Expression, "asuint", "floatBitsToUint");
        StringReplacement(ref Expression, "asint", "floatBitsToInt");
        StringReplacement(ref Expression, "asfloat", "uintBitsToFloat");
        StringReplacement(ref Expression, "const", "");//GLSL is picky with saying stuff's const
        StringReplacement(ref Expression, "frac(", "fract(");
        StringReplacement(ref Expression, "fmod(", "mod(" );
        StringReplacement(ref Expression, "#if ", "#if 0//");//#if SHADOW_DEPTH_SHADER,#if LUMEN_CARD_CAPTURE
        StringReplacement(ref Expression, "\"", "_");//usually found in comments!        
        StringReplacement(ref Expression, "fixed2", "vec2");
        StringReplacement(ref Expression, "fixed3", "vec3");
        StringReplacement(ref Expression, "fixed4", "vec4");
        StringReplacement(ref Expression, "fixed", "float");
        StringReplacement(ref Expression, "half2", "vec2");
        StringReplacement(ref Expression, "half3", "vec3");
        StringReplacement(ref Expression, "half4", "vec4");
        StringReplacement(ref Expression, "_half", "_h4lf");//prevent an error when having multiple HLSL functions with _float and _half
        StringReplacement(ref Expression, "half", "float");//for variable types
        StringReplacement(ref Expression, "_h4lf", "_half");//fix back custom function names that need this suffix
        StringReplacement(ref Expression, "real2", "vec2");
        StringReplacement(ref Expression, "real3", "vec3");
        StringReplacement(ref Expression, "real4", "vec4");
        StringReplacement(ref Expression, "real", "float");

        StringReplacement(ref Expression, "unity_SHAr", "vec4(0.25, 0.0, 0.0, 0.0)");
        StringReplacement(ref Expression, "unity_SHAg", "vec4(0.35, 0.0, 0.0, 0.0)");
        StringReplacement(ref Expression, "unity_SHAb", "vec4(0.45, 0.0, 0.0, 0.0)");
        StringReplacement(ref Expression, "unity_SHBr", "vec4(0.0, 0.02, 0.0, 0.0)");
        StringReplacement(ref Expression, "unity_SHBg", "vec4(0.0, 0.03, 0.0, 0.0)");
        StringReplacement(ref Expression, "unity_SHBb", "vec4(0.0, 0.04, 0.0, 0.0)");
        StringReplacement(ref Expression, "unity_SHC",  "vec4(0.0, 0.0, 0.0, 1.0)");

        StringReplacement(ref Expression, "tex2D(", "texture( ");
        StringReplacement(ref Expression, "tex2Dlod(", "textureLod( ");
        StringReplacement(ref Expression, "tex2Dgrad(", "textureGrad( ");

        StringReplacement(ref Expression, "UNITY_BRANCH", "//UNITY_BRANCH");
        StringReplacement(ref Expression, "UNITY_FLATTEN", "//UNITY_FLATTEN");
        StringReplacement(ref Expression, "UNITY_UNROLL", "//UNITY_UNROLL");
        StringReplacement(ref Expression, "UNITY_LOOP", "//UNITY_LOOP");

        ReplaceHLSLSampleCall(ref Expression, ".SampleGrad(", "textureGrad( ");
        ReplaceHLSLSampleCall(ref Expression, ".Sample(", "texture( ");
        ReplaceHLSLSampleCall(ref Expression, ".SampleLevel(", "textureLod( ");
        ReplaceHLSLSampleCall(ref Expression, ".SampleBias(", "texture( ");
    }
    public static string GetGradientDeclaration(UnityEngine.Gradient gradient)
    {
        string InitializerData = "";
        for (int i = 0; i < 8; i++)
        {
            GradientColorKey c = new GradientColorKey();
            if (i < gradient.colorKeys.Length)
                c = gradient.colorKeys[i];

            string VecStr = string.Format( CultureInfo.InvariantCulture,
                    "vec4( {0}, {1}, {2}, {3} ),\n",
                c.color.r, c.color.g, c.color.b, c.time);
            InitializerData += VecStr;
        }
        for (int i = 0; i < 8; i++)
        {
            GradientAlphaKey a = new GradientAlphaKey();
            if (i < gradient.alphaKeys.Length)
                a = gradient.alphaKeys[i];

            string VecStr = string.Format( CultureInfo.InvariantCulture,
                 "vec2( {0}, {1} )",
                  a.alpha, a.time);
            if (i < 7)
                VecStr += ",\n";

            InitializerData += VecStr;
        }
        string Text = string.Format( CultureInfo.InvariantCulture,
            "Gradient gradient = NewGradient( {0}, {1}, {2},\n" +
            "{3} );\n",
            (int)gradient.mode, gradient.colorKeys.Length, gradient.alphaKeys.Length, InitializerData
        );

        return Text;
    }
    public static string GenerateSuffix( GodotShaderVariableType Type)
    {
        string Suffix = "xyzw";
        int NumComponents = GetNumComponentsFromVectorType( Type );
        Suffix = Suffix.Substring(0, NumComponents);
        if (Suffix.Length > 0)
            Suffix = "." + Suffix;
        else
            Suffix = "";

        return Suffix;
    }
    public static void CreateTilingNodeAndConnect( GodotShaderData ShaderData, GodotShaderNode ShaderNode, GodotShaderNode TextureNode )
    {
        GodotShaderNode TilingOffsetNode = GenerateShaderNode(ShaderData, null, null, ShaderNode.ShaderType, "VisualShaderNodeExpression", null, null, (int)ShaderNode.Position.x - 200, (int)ShaderNode.Position.y );

        string Text = string.Format( CultureInfo.InvariantCulture,
            "//TilingOffset:{0}:{1}\n" +
            "output0 = input0 * input1.xy + input1.zw;\n" 
            , TilingOffsetNode.ID, TilingOffsetNode.GetParentGraphName()
            );
        string DependentNodeParams = "";
        GenerateShaderExpression( GodotShaderVariableType.GSVT_VECTOR2,
        GodotShaderVariableType.GSVT_VECTOR4, Text, GodotShaderVariableType.GSVT_VECTOR2, ref DependentNodeParams, TilingOffsetNode);
        TilingOffsetNode.DefinitionText += DependentNodeParams;

        ShaderConnection TilingOffsetToSample = new ShaderConnection();
        TilingOffsetToSample.SourceNode = TilingOffsetNode.ID;
        TilingOffsetToSample.SourceNodeOutput = 0;
        TilingOffsetToSample.DestinationNode = ShaderNode.ID;
        TilingOffsetToSample.DestinationNodeInput = 0;
        TilingOffsetToSample.ShaderType = ShaderType.FRAGMENT;
        ShaderData.AddShaderConnection( TilingOffsetToSample );

        //What if the UV node is not already created ?
        ShaderConnection UVToSample = ShaderData.GetShaderConnection( ShaderNode.ID, 0, ShaderNode.ShaderType );
        if (UVToSample != null)
        {
            UVToSample.DestinationNode = TilingOffsetNode.ID;
            UVToSample.DestinationNodeInput = 0;
        }
        if ( TextureNode != null && TextureNode.Parameter != null && TextureNode.Parameter.TilingOffsetParameter != null &&
            TextureNode.Parameter.TilingOffsetParameter.Node != null )
        {
            ShaderConnection TilingOffsetParameterToMAD = new ShaderConnection();
            TilingOffsetParameterToMAD.SourceNode = TextureNode.Parameter.TilingOffsetParameter.Node.ID;
            TilingOffsetParameterToMAD.SourceNodeOutput = 0;
            TilingOffsetParameterToMAD.DestinationNode = TilingOffsetNode.ID;
            TilingOffsetParameterToMAD.DestinationNodeInput = 1;
            TilingOffsetParameterToMAD.ShaderType = ShaderNode.ShaderType;
            ShaderData.AddShaderConnection( TilingOffsetParameterToMAD );
        }
        else
        {
            Debug.LogError("CreateTilingNodeAndConnect->TextureNode.Parameter.TilingOffsetParameter.Node == null !");
        }
    }
    public static string GetShaderNodeType( GodotShaderData ShaderData, ref string Params, GodotShaderNode ShaderNode, object m_Value )
    {
        Type m_ValueType = m_Value.GetType();
        var m_TypeStr = m_ValueType.FullName;
        ShaderType CurrentShaderType = ShaderNode.ShaderType;// ShaderType.FRAGMENT;

        #if UNITY_SHADER_GRAPH
        if( m_TypeStr == "UnityEditor.ShaderGraph.PositionNode" )
        {
            Type GeometryNodeType = GetTypeInHierarchyByName(m_ValueType, "GeometryNode");
            object m_Space = GetValueForFieldOrProperty(m_Value, GeometryNodeType, "m_Space", BindingFlags.Instance | BindingFlags.NonPublic);
            UnityEditor.ShaderGraph.Internal.CoordinateSpace Space = (CoordinateSpace)m_Space;
            if (m_Space == null)
                Space = CoordinateSpace.World;
            string Text = "";
            if (CurrentShaderType == ShaderType.FRAGMENT)
            {
                if (Space == CoordinateSpace.Object)
                {
                    Text = string.Format( CultureInfo.InvariantCulture,
                        "//PositionNode {1}:{0}\n" +
                        "output0 = VERTEX;\n"
                        , ShaderNode.ID, Space.ToString()
                        );
                }
                else//if (Space == CoordinateSpace.World)
                Text = string.Format( CultureInfo.InvariantCulture,
                        "//PositionNode {1}:{0}\n" +
                        "output0 = ( INV_VIEW_MATRIX * vec4( VERTEX, 1.0) ).xyz;\n"
                        , ShaderNode.ID, Space.ToString()
                        );
            }
            else
            {

                if (Space == CoordinateSpace.Object)
                {
                    Text = string.Format( CultureInfo.InvariantCulture,
                            "//PositionNode {1}:{0}\n" +
                            "output0 = ( vec4( VERTEX, 1.0)).xyz;\n"
                            , ShaderNode.ID, Space.ToString()
                            );
                }
                else//World
                {
                    Text = string.Format( CultureInfo.InvariantCulture,
                            "//PositionNode {1}:{0}\n" +
                            "output0 = ( vec4( VERTEX, 1.0) * MODEL_MATRIX ).xyz;\n"
                            , ShaderNode.ID, Space.ToString()
                            );
                }
            }
            return GenerateShaderExpression(Text, GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode);
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.SampleTexture2DNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.SampleTexture2DLODNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.SampleRawCubemapNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.SampleTexture3DNode" 
            )
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            object m_TextureType = GetValueForFieldOrProperty(m_Value, "m_TextureType", BindingFlags.Instance | BindingFlags.NonPublic);
            object m_NormalMapSpace = GetValueForFieldOrProperty(m_Value, "m_NormalMapSpace", BindingFlags.Instance | BindingFlags.NonPublic);
            UTGSlot TextureSlot = ShaderNode.GetSlot("Texture");
            bool IsCubemap = false;
            if (m_TypeStr == "UnityEditor.ShaderGraph.SampleRawCubemapNode")
            {
                TextureSlot = ShaderNode.GetSlot("Cube");
                IsCubemap = true;
            }
            if (m_TypeStr == "UnityEditor.ShaderGraph.SampleTexture2DArrayNode")
            {
                TextureSlot = ShaderNode.GetSlot("Texture Array");
            }
            string Text = "";
            int SourceType = 5;//SamplerPort
            if (m_TypeStr == "UnityEditor.ShaderGraph.SampleRawCubemapNode" ||
                m_TypeStr == "UnityEditor.ShaderGraph.SampleTexture3DNode" ||
                m_TypeStr == "UnityEditor.ShaderGraph.SampleTexture2DArrayNode"
                )
            {
                SourceType = 1;//SamplerPort on Cube/Texture3D
            }
            ExportTextureType TextureType = ExportTextureType.GTT_COLOR;

            if (m_TextureType != null)
            {
                if (m_TextureType.ToString() == "Default")
                {

                }
                else if (m_TextureType.ToString() == "Normal")
                {
                    TextureType = ExportTextureType.GTT_NORMAL;
                }
            }
            if ( TextureSlot != null && !TextureSlot.isConnected )//&& TextureSlot.texture != null )
            {
                if (TextureType == ExportTextureType.GTT_NORMAL)
                {
                    GenerateTextureParameterForSlot(TextureSlot, ShaderData, ShaderNode, CurrentShaderType);
                }
                else
                {
                    ExportTexture SlotTexture = AddExportTexture(TextureSlot.texture, ref ShaderData.Textures);
                    if (SlotTexture != null)
                    {
                        string Type = "texture";
                        if (IsCubemap)
                            Type = "cube_map";
                        Text = string.Format( CultureInfo.InvariantCulture,
                            "{0} = ExtResource(\"{1}\")\n", Type, SlotTexture.LocalGUID);
                        Params += Text;
                    }
                    SourceType = 0;
                }
            }
            //UTGSlot UVSlot = ShaderNode.GetSlot("UV");
            //if( UVSlot != null && !UVSlot.isConnected && UVSlot.Channel != UVChannel.UV0 )
            //{
            //    GodotShaderNode DependentNode = GenerateShaderNode(ShaderData, null, null,
            //        CurrentShaderType, "VisualShaderNodeInput", ShaderNode.GraphObject, ShaderNode.ParentGraphNode, (int)ShaderNode.Position.x - 500, (int)ShaderNode.Position.y + 200);
            //    string DependentNodeParams = "";
            //    GenerateInputNode( "uv2", ref DependentNodeParams );
            //    DependentNode.DefinitionText += DependentNodeParams;
            //    ShaderData.AddShaderConnection( DependentNode, 0, ShaderNode, 0, CurrentShaderType );
            //}
            UTGSlot SamplerStateSlot = ShaderNode.GetSlot("Sampler");
            if( SamplerStateSlot != null && SamplerStateSlot.isConnected )
            {
                object ConnectingSlot = null;
                object SamplerNode = ShaderData.GetConnectingNodeAndSlot(SamplerStateSlot.Slot, ref ConnectingSlot );
                if( SamplerNode != null )
                {
                    object m_filter = GetValueForFieldOrProperty(SamplerNode, "m_filter", BindingFlags.Instance | BindingFlags.NonPublic);
                    object m_wrap = GetValueForFieldOrProperty(SamplerNode, "m_wrap", BindingFlags.Instance | BindingFlags.NonPublic);

                    if (m_filter != null)
                    {
                        GodotFilterMode FilterMode = GetFilterMode(m_filter.ToString());
                    }
                    if (m_wrap != null)
                    {
                        GodotRepeatMode RepeatMode = GetRepeatMode(m_wrap.ToString());
                    }
                }
            }           
            if ( TextureSlot.isConnected )
            {
                object ConnectingSlot = null;
                object TextureNodeGraphObject = ShaderData.GetConnectingNodeAndSlot( TextureSlot.Slot, ref ConnectingSlot );
                if (TextureNodeGraphObject != null )
                {
                    GodotShaderNode TextureNode = ShaderData.GetShaderNode( TextureNodeGraphObject );
                    if ( TextureNode == null )
                    {
                        //Create it here because we need data from it !
                        TextureNode = GenerateShaderNode( ShaderData, TextureNodeGraphObject, ShaderNode.ShaderType, ShaderNode.GraphObject, ShaderNode.ParentGraphNode );
                    }
                    if( TextureNode != null )
                    {
                        if (TextureType == ExportTextureType.GTT_NORMAL)
                        {
                            TextureNode.DefinitionText = TextureNode.DefinitionText.Replace("texture_type = 1", "texture_type = 2");
                        }
                    }
                    object m_Property = GetValueForFieldOrProperty(TextureNodeGraphObject, "m_Property", BindingFlags.Instance | BindingFlags.NonPublic);
                    object PropertyValue = GetValueForFieldOrProperty(m_Property, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);
                    object useTilingAndOffset = GetValueForFieldOrProperty(PropertyValue, "useTilingAndOffset", BindingFlags.Instance | BindingFlags.NonPublic);
                    if ( useTilingAndOffset != null )
                    {
                        bool TilingAndOffset = (bool)useTilingAndOffset;
                        if ( TilingAndOffset )
                        {
                            CreateTilingNodeAndConnect( ShaderData, ShaderNode, TextureNode );
                        }
                    }
                }
            }

            if (TextureType == ExportTextureType.GTT_NORMAL)
            {
                Text = string.Format( CultureInfo.InvariantCulture,
                    "//NormalTexture:{0}:{1}\n" +
                    "vec4 Normal{0} = texture( input2, input0 );\n" +
                    //"Normal{0} = Normal{0} * vec4(2.0, 2.0, 2.0, 2.0) - vec4(1.0, 1.0, 1.0, 1.0);\n" +
                    "Normal{0}.xyz = UnpackNormalmap( Normal{0} );\n" +
                    "output0 = Normal{0};\n"   +
                    "output1 = Normal{0}.r;\n" +
                    "output2 = Normal{0}.g;\n" +
                    "output3 = Normal{0}.b;\n" +
                    "output4 = Normal{0}.a;\n"
                    , ShaderNode.ID, ShaderNode.GetParentGraphName()
                    );
                return GenerateShaderExpression( GodotShaderVariableType.GSVT_VECTOR2,
                    GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_SAMPLER, Text, GodotShaderVariableType.GSVT_VECTOR4,
                    GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode);
            }
            else
            {
                Text = string.Format( CultureInfo.InvariantCulture,
                              "expanded_output_ports = [0]\n" +
                              "texture_type = {0}\n" +
                              "source = {1}\n",
                              (int)TextureType, SourceType);
                Params += Text;
                if (m_TypeStr == "UnityEditor.ShaderGraph.SampleRawCubemapNode")
                    return "VisualShaderNodeCubemap";
                else if (m_TypeStr == "UnityEditor.ShaderGraph.SampleTexture3DNode")
                    return "VisualShaderNodeTexture3D";
                else if (m_TypeStr == "UnityEditor.ShaderGraph.SampleTexture2DArrayNode")
                    return "VisualShaderNodeTexture2DArray";
                else
                    return "VisualShaderNodeTexture";
            }
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.SubGraphOutputNode" )
        {
            AddConstantNodesForUnconnectedInputs( ShaderData, ShaderNode, CurrentShaderType );

            return GenerateRerouteNode( GodotShaderVariableType.GSVT_VECTOR4, ref Params );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.PropertyNode" )
        {
            object m_Property = GetValueForFieldOrProperty(m_Value, "m_Property", BindingFlags.Instance | BindingFlags.NonPublic);
            object PropertyValue = GetValueForFieldOrProperty(m_Property, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);
            object displayName = GetValueForFieldOrProperty(PropertyValue, "displayName", BindingFlags.Instance | BindingFlags.NonPublic);
            object PropertyValue_m_Value = GetValueForFieldOrProperty(PropertyValue, PropertyValue.GetType().BaseType, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);

            UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty Tex2DProperty = PropertyValue as UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty;
            UnityEditor.ShaderGraph.Internal.CubemapShaderProperty   CubemapShaderProperty = PropertyValue as UnityEditor.ShaderGraph.Internal.CubemapShaderProperty;
            UnityEditor.ShaderGraph.Internal.Texture3DShaderProperty Texture3DShaderProperty = PropertyValue as UnityEditor.ShaderGraph.Internal.Texture3DShaderProperty;
            UnityEditor.ShaderGraph.Internal.Texture2DArrayShaderProperty Texture2DArrayShaderProperty = PropertyValue as UnityEditor.ShaderGraph.Internal.Texture2DArrayShaderProperty;
            

            if( IsSubGraph( ShaderNode.GraphObject ) )
            {
                GodotShaderVariableType RerouteType = GodotShaderVariableType.GSVT_VECTOR4;
                if ( Tex2DProperty != null ||
                     Texture3DShaderProperty != null ||
                     CubemapShaderProperty != null ||
                     Texture2DArrayShaderProperty != null )
                {
                    RerouteType = GodotShaderVariableType.GSVT_SAMPLER;
                }
                return GenerateRerouteNode( RerouteType, ref Params );
            }

            if ( Tex2DProperty != null ||
                 Texture3DShaderProperty != null ||
                 CubemapShaderProperty != null ||
                 Texture2DArrayShaderProperty != null )
            {
                object DefaultTexture = GetValueForFieldOrProperty(PropertyValue_m_Value, "texture", BindingFlags.Instance | BindingFlags.NonPublic);

                GodotFilterMode FilterMode = GodotFilterMode.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC;
                GodotRepeatMode RepeatMode = GodotRepeatMode.TEXTURE_REPEAT_DEFAULT;
                bool HasSamplerState = false;
                if( ShaderNode.ProcessedSlots.Count > 0 )
                {
                    UTGSlot OutSlot = ShaderNode.ProcessedSlots[0];
                    object ConnectingSlot = null;
                    object SampleNode = ShaderData.GetConnectingNodeAndSlot(OutSlot.Slot, ref ConnectingSlot );
                    if( SampleNode != null )
                    {
                        UTGSlot SamplerStateSlot = GetSlot(SampleNode, "Sampler", ShaderData);
                        if( SamplerStateSlot != null && SamplerStateSlot.isConnected )
                        {
                            object SamplerNode = ShaderData.GetConnectingNodeAndSlot(SamplerStateSlot.Slot, ref ConnectingSlot );
                            if( SamplerNode != null )
                            {
                                object m_filter = GetValueForFieldOrProperty(SamplerNode, "m_filter", BindingFlags.Instance | BindingFlags.NonPublic);
                                object m_wrap = GetValueForFieldOrProperty(SamplerNode, "m_wrap", BindingFlags.Instance | BindingFlags.NonPublic);

                                FilterMode = GetFilterMode( m_filter.ToString() );
                                RepeatMode = GetRepeatMode( m_wrap.ToString() );
                                HasSamplerState = true;
                            }
                        }
                    }
                }

                string ParameterName = "";
                string ReferenceName = "";
                GodotColorDefault color_default = GodotColorDefault.GCD_WHITE;//0-White,1-Black
                Texture2DShaderProperty.DefaultType TheType = Texture2DShaderProperty.DefaultType.White;
                if (Tex2DProperty != null)
                {
                    ParameterName = Tex2DProperty.displayName;
                    ReferenceName = Tex2DProperty.referenceName;
                    object m_DefaultType = GetValueForFieldOrProperty(PropertyValue, "m_DefaultType", BindingFlags.Instance | BindingFlags.NonPublic);
                    TheType = (Texture2DShaderProperty.DefaultType)m_DefaultType;
                    if ( TheType == Texture2DShaderProperty.DefaultType.Black )
                        color_default = GodotColorDefault.GCD_BLACK;
                    if ( TheType == Texture2DShaderProperty.DefaultType.Grey ||
                         TheType == Texture2DShaderProperty.DefaultType.LinearGrey )
                         {
                        DefaultTexture = GetDefaultGreyTexture();
                        }
                    if ( TheType == Texture2DShaderProperty.DefaultType.Red )
                    {
                        DefaultTexture = GetDefaultRedTexture();
                    }
                }
                else if (Texture3DShaderProperty != null)
                {
                    ParameterName = Texture3DShaderProperty.displayName;
                    ReferenceName = Texture3DShaderProperty.referenceName;
                }
                else if (CubemapShaderProperty != null)
                {
                    ParameterName = CubemapShaderProperty.displayName;
                    ReferenceName = CubemapShaderProperty.referenceName;
                }
                else if (Texture2DArrayShaderProperty != null)
                {
                    ParameterName = Texture2DArrayShaderProperty.displayName;
                    ReferenceName = Texture2DArrayShaderProperty.referenceName;
                }
                ShaderNode.Parameter = ShaderData.AddShaderParameter(ref ParameterName, ReferenceName, ShaderParameterType.SPT_TEXTURE, CurrentShaderType, 0.0f, Vector4.zero, DefaultTexture as Texture, false, 0, ShaderNode);

                if (Tex2DProperty != null)
                {
                    object useTilingAndOffset = GetValueForFieldOrProperty(PropertyValue, "useTilingAndOffset", BindingFlags.Instance | BindingFlags.NonPublic);
                    if ( useTilingAndOffset != null )
                    {
                        bool UseTilingAndOffset = (bool)useTilingAndOffset;
                        if ( UseTilingAndOffset)
                        {
                            //Parameter.Flag = ShaderParameterFlag.SPF_HAS_TILING_OFFSET;

                            string TillingOffsetParamName = ShaderNode.Parameter.SanitizedName + "_TilingOffset";
                            ShaderParameter ExistentParameter = ShaderData.GetShaderParameter( TillingOffsetParamName, ShaderParameterType.SPT_VECTOR4, ShaderNode.ShaderType );
                            if( ExistentParameter == null )
                            {
                                GodotShaderNode DependentNode = null;
                                Vector4 DefaultTilingOffset = new Vector4( 1,1,0,0);
                                ShaderNode.Parameter.TilingOffsetParameter = GenerateNewParameter( ShaderData, TillingOffsetParamName, false, 0, DefaultTilingOffset, ShaderParameterType.SPT_VECTOR4,
                                    ref DependentNode, ShaderNode.ShaderType, ShaderNode.GraphObject, ShaderNode.ParentGraphNode,
                                    (int)ShaderNode.Position.x - 500, (int)ShaderNode.Position.y + 200 );
                                ShaderNode.Parameter.TilingOffsetParameter.Flag = ShaderParameterFlag.SPF_IS_TILING_OFFSET;
                            }
                        }
                    }
                }

                ExportTextureType TextureType = ExportTextureType.GTT_COLOR;
                if ( TheType == Texture2DShaderProperty.DefaultType.NormalMap )
                    TextureType = ExportTextureType.GTT_NORMAL;
                string Text = string.Format( CultureInfo.InvariantCulture,
                "parameter_name = \"{0}\"\n" +
                "texture_type = {1}\n" +
                "color_default = {2}\n", ParameterName, (int)TextureType, (int)color_default );
                if( HasSamplerState )
                {
                    Text += string.Format( CultureInfo.InvariantCulture,
                        "texture_filter = {0}\n" +
                        "texture_repeat = {1}\n", (int)FilterMode, (int)RepeatMode );
                }
                Params += Text;
                if (Texture3DShaderProperty != null)
                    return "VisualShaderNodeTexture3DParameter";
                else if (CubemapShaderProperty != null)
                    return "VisualShaderNodeCubemapParameter";
                else
                    return "VisualShaderNodeTexture2DParameter";
            }            
            UnityEditor.ShaderGraph.Internal.Vector1ShaderProperty Vec1Property = PropertyValue as UnityEditor.ShaderGraph.Internal.Vector1ShaderProperty;
            if( Vec1Property != null )
            {
                string ParameterName = Vec1Property.displayName;
                var Parameter = ShaderData.AddShaderParameter( ref ParameterName, Vec1Property.referenceName, ShaderParameterType.SPT_FLOAT, CurrentShaderType, Vec1Property.value, Vector4.zero, null, false, 0, ShaderNode );

                string Text = string.Format( CultureInfo.InvariantCulture,
                  "parameter_name = \"{0}\"\n" +
                  "default_value_enabled = true\n" +
                  "default_value = {1}\n",
                  ParameterName, Vec1Property.value);
                Params += Text;
                return "VisualShaderNodeFloatParameter";
            }
            UnityEditor.ShaderGraph.Internal.Vector2ShaderProperty Vec2Property = PropertyValue as UnityEditor.ShaderGraph.Internal.Vector2ShaderProperty;
            if( Vec2Property != null )
            {
                string ParameterName = Vec2Property.displayName;
                var  Parameter = ShaderData.AddShaderParameter( ref ParameterName, Vec2Property.referenceName, ShaderParameterType.SPT_VECTOR2, CurrentShaderType, 0.0f, Vec2Property.value, null, false, 0, ShaderNode );

                string Text = string.Format( CultureInfo.InvariantCulture,
                  "parameter_name = \"{0}\"\n" +
                  "default_value_enabled = true\n" +
                  "default_value = Vector2({1},{2})\n",
                  ParameterName, Vec2Property.value.x, Vec2Property.value.y);
                Params += Text;
                return "VisualShaderNodeVec2Parameter";
            }
            UnityEditor.ShaderGraph.Internal.Vector3ShaderProperty Vec3Property = PropertyValue as UnityEditor.ShaderGraph.Internal.Vector3ShaderProperty;
            if( Vec3Property != null )
            {
                string ParameterName = Vec3Property.displayName;
                var  Parameter = ShaderData.AddShaderParameter( ref ParameterName, Vec3Property.referenceName, ShaderParameterType.SPT_VECTOR3, CurrentShaderType, 0.0f, Vec3Property.value, null, false, 0, ShaderNode );

                string Text = string.Format( CultureInfo.InvariantCulture,
                  "parameter_name = \"{0}\"\n" +
                  "default_value_enabled = true\n" +
                  "default_value = Vector3({1},{2},{3})\n",
                  ParameterName, Vec3Property.value.x, Vec3Property.value.y, Vec3Property.value.z);
                Params += Text;
                return "VisualShaderNodeVec3Parameter";
            }
            UnityEditor.ShaderGraph.Internal.Vector4ShaderProperty Vec4Property = PropertyValue as UnityEditor.ShaderGraph.Internal.Vector4ShaderProperty;
            if( Vec4Property != null )
            {
                string ParameterName = Vec4Property.displayName;
                var  Parameter = ShaderData.AddShaderParameter( ref ParameterName, Vec4Property.referenceName, ShaderParameterType.SPT_VECTOR4, CurrentShaderType, 0.0f, Vec4Property.value, null, false, 0, ShaderNode );

                string Text = string.Format( CultureInfo.InvariantCulture,
                  "parameter_name = \"{0}\"\n" +
                  "default_value_enabled = true\n" +
                  "default_value = Vector4({1},{2},{3},{4})\n",
                  ParameterName, Vec4Property.value.x, Vec4Property.value.y, Vec4Property.value.z, Vec4Property.value.w);
                Params += Text;
                return "VisualShaderNodeVec4Parameter";
            }
            UnityEditor.ShaderGraph.Internal.ColorShaderProperty ColorProperty = PropertyValue as UnityEditor.ShaderGraph.Internal.ColorShaderProperty;
            if( ColorProperty != null )
            {
                string ParameterName = ColorProperty.displayName;
                var  Parameter = ShaderData.AddShaderParameter( ref ParameterName, ColorProperty.referenceName, ShaderParameterType.SPT_COLOR, CurrentShaderType, 0.0f, 
                    new Vector4( ColorProperty.value.r, ColorProperty.value.g, ColorProperty.value.b, ColorProperty.value.a ), null, false, 0, ShaderNode );

                string Text = string.Format( CultureInfo.InvariantCulture,
                  "parameter_name = \"{0}\"\n" +
                  "expanded_output_ports = [0]\n" +
                  "default_value_enabled = true\n" +
                  "default_value = Color({1},{2},{3},{4})\n",
                  ParameterName, ColorProperty.value.r, ColorProperty.value.g, ColorProperty.value.b, ColorProperty.value.a);
                Params += Text;
                return "VisualShaderNodeColorParameter";
            }
            UnityEditor.ShaderGraph.Internal.BooleanShaderProperty BooleanProperty = PropertyValue as UnityEditor.ShaderGraph.Internal.BooleanShaderProperty;
            if( BooleanProperty != null )
            {
                string ParameterName = BooleanProperty.displayName;
                var  Parameter = ShaderData.AddShaderParameter( ref ParameterName, BooleanProperty.referenceName, ShaderParameterType.SPT_BOOL, CurrentShaderType, 0.0f, Vector4.zero, null, BooleanProperty.value, 0, ShaderNode );

                string Text = string.Format( CultureInfo.InvariantCulture,
                  "parameter_name = \"{0}\"\n" +
                  "expanded_output_ports = [0]\n" +
                  "default_value_enabled = true\n" +
                  "default_value = {1}\n",
                  ParameterName, BooleanProperty.value.ToString().ToLower());
                Params += Text;
                return "VisualShaderNodeBooleanParameter";
            }
            Type PropertyType = PropertyValue.GetType();
            if( PropertyType.Name.Contains( "GradientShaderProperty" ) )
            {

            }
            else if( PropertyType.Name.Contains( "MatrixShaderProperty" ) )
            {
            }
            else if( PropertyType.Name.Contains( "SamplerStateShaderProperty" ) )
            {

            }
            else if( PropertyType.Name.Contains( "VirtualTextureShaderProperty" ) )
            {

            }
            else if( PropertyType.Name.Contains( "UnknownShaderProperty" ) )
            {
                return null;
            }

            return null;
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.Texture2DAssetNode")
        {
            object DefaultTextureObject = GetValueForFieldOrProperty(m_Value, "texture", BindingFlags.Instance | BindingFlags.NonPublic);
            Texture DefaultTexture = DefaultTextureObject as Texture;
            GodotFilterMode FilterMode = GodotFilterMode.TEXTURE_FILTER_DEFAULT;
            GodotRepeatMode RepeatMode = GodotRepeatMode.TEXTURE_REPEAT_DEFAULT;
            
            string ParameterName = "Texture2DAssetNode_" + m_Value.GetHashCode();
            ShaderParameter Parameter = ShaderData.AddShaderParameter(ref ParameterName, ParameterName, ShaderParameterType.SPT_TEXTURE, CurrentShaderType, 0.0f, Vector4.zero, DefaultTexture, false, 0, ShaderNode);

            ExportTextureType TextureType = IsNormalMap( DefaultTexture ) ? ExportTextureType.GTT_NORMAL : ExportTextureType.GTT_COLOR;
            
            string Text = string.Format( CultureInfo.InvariantCulture,
            "parameter_name = \"{0}\"\n" +
            "texture_type = {1}\n", ParameterName, (int)TextureType);
            
            Text += string.Format( CultureInfo.InvariantCulture,
                "texture_filter = {0}\n" +
                "texture_repeat = {1}\n", (int)FilterMode, (int)RepeatMode);
            Params += Text;
            return "VisualShaderNodeTexture2DParameter";
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.Vector1Node" )
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
            return GenerateRerouteNode(GodotShaderVariableType.GSVT_FLOAT, ref Params);
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.Vector2Node" )
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
            return GenerateVectorComposeNode( GodotShaderVariableType_VectorOp.VOP_VECTOR2, ref Params );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.Vector3Node" )
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
            return GenerateVectorComposeNode(GodotShaderVariableType_VectorOp.VOP_VECTOR3, ref Params);
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.Vector4Node" )
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
            return GenerateVectorComposeNode(GodotShaderVariableType_VectorOp.VOP_VECTOR4, ref Params);
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.SplitNode" )
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
            string ParentGraph = GetParentGraphName( ShaderNode );
            string Text = string.Format( CultureInfo.InvariantCulture,
                  "//SplitNode:{0}({1})\n" +
                  "output0 =  input0.x;\n" +
                  "output1 =  input0.y;\n" +
                  "output2 =  input0.z;\n" +
                  "output3 =  input0.w;\n",
                  ShaderNode.ID, ParentGraph
                );

            return GenerateShaderExpression( GodotShaderVariableType.GSVT_VECTOR4, Text, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT,
                GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode );
        }
        //Godot doesn't have sampler states
        if( m_TypeStr == "UnityEditor.ShaderGraph.SamplerStateNode" )
        {
            return null;
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.LerpNode" )
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            UTGSlot SlotA = ShaderNode.GetSlot("A");
            UTGSlot SlotB = ShaderNode.GetSlot("B");
            UTGSlot SlotT = ShaderNode.GetSlot("T");
            GodotShaderVariableType TypeA = GetVariableTypeFromString(SlotA.ValueType);
            GodotShaderVariableType TypeB = GetVariableTypeFromString(SlotB.ValueType);
            GodotShaderVariableType TypeT = GetVariableTypeFromString(SlotT.ValueType);
            GodotShaderVariableType FinalType = (GodotShaderVariableType)Math.Min((int)Math.Min((int)TypeA, (int)TypeB), (int)TypeT);
            GodotShaderVariableType_Mix MixFinalType = GetVariableType_Mix(FinalType);

            Vector4 DefaultA, DefaultB, DefaultAlpha;
            DefaultA = DefaultB = DefaultAlpha = new Vector4( 0, 0, 0, 0 );
            string DefaultInputValues = GenerateDefaultInputValues3(FinalType, DefaultA, DefaultB, DefaultAlpha);
            string Text = string.Format( CultureInfo.InvariantCulture,
                      "{0}\n" +
                      "op_type = {1}\n",
                      DefaultInputValues,
                      (int)MixFinalType
            );
            Params = Text;

            return "VisualShaderNodeMix";
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.InverseLerpNode" )
        {
            string Text = string.Format( CultureInfo.InvariantCulture,
                  "//InverseLerpNode:{0}\n" +
                  "output0 =  (input2 - input0) / ( input1 - input0 );\n",
                  ShaderNode.ID
                );

            return GenerateShaderExpression( GodotShaderVariableType.GSVT_VECTOR4, GodotShaderVariableType.GSVT_VECTOR4, GodotShaderVariableType.GSVT_VECTOR4,
                Text, GodotShaderVariableType.GSVT_VECTOR4, ref Params, ShaderNode );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.MultiplyNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.AddNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.SubtractNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.MaximumNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.MinimumNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.DivideNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.PowerNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.Arctangent2Node" ||
            m_TypeStr == "UnityEditor.ShaderGraph.CrossProductNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.ReflectionNode"
          )
        {
            AddConstantNodesForUnconnectedInputs( ShaderData, ShaderNode, CurrentShaderType );

            UTGSlot SlotA = ShaderNode.GetSlot("A");
            UTGSlot SlotB = ShaderNode.GetSlot("B");
            if (m_TypeStr == "UnityEditor.ShaderGraph.ReflectionNode")
            {
                SlotA = ShaderNode.GetSlot("In");
                SlotB = ShaderNode.GetSlot("Normal");
            }

            UTGSlot AConnection = ShaderData.GetConnectingSlot( SlotA.Slot );
            UTGSlot BConnection = ShaderData.GetConnectingSlot( SlotB.Slot );

            GodotShaderVariableType TypeA = SlotA.GetVariableType();
            GodotShaderVariableType TypeB = SlotB.GetVariableType();

            if (AConnection != null )
                TypeA = GetVariableTypeFromString(AConnection.ValueType);
            if ( BConnection != null )
                TypeB = GetVariableTypeFromString(BConnection.ValueType);

            GodotShaderVariableType OperationType = (GodotShaderVariableType)Math.Max( (int)TypeA, (int)TypeB);

            GodotFloatOperation FloatOp = GodotFloatOperation.GFO_ADD;
            GodotVectorOperation VecOp = GodotVectorOperation.GVO_ADD;
            bool Fetched = GetOperationsFromString(m_TypeStr, ref FloatOp, ref VecOp);
            if( !Fetched )
                return null;

            if ( TypeA == GodotShaderVariableType.GSVT_TRANSFORM || TypeB == GodotShaderVariableType.GSVT_TRANSFORM )
            {
                if ( VecOp != GodotVectorOperation.GVO_MULTIPLY )
                {
                    Debug.LogError( "Shader " + ShaderData.FilePath + " wants a matrix operation that's not supported");
                }
                return "VisualShaderNodeTransformVecMult";
            }
            else
                return GenerateOpNodeBasedOnType( FloatOp, VecOp, OperationType, SlotA.Vec4.x, SlotB.Vec4.y, ref Params );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.DotProductNode" )
        {
            return "VisualShaderNodeDotProduct";
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.SaturateNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.OneMinusNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.FractionNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.SineNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.CosineNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.ArcsineNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.ArccosineNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.TangentNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.ArctangentNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.NegateNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.NormalizeNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.AbsoluteNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.RoundNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.SignNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.SquareRootNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.ReciprocalSquareRootNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.ExponentialNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.CeilingNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.FloorNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.ReciprocalNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.LogNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.TruncateNode" 
            )
         {
            AddConstantNodesForUnconnectedInputs( ShaderData, ShaderNode, CurrentShaderType );

            UTGSlot In = ShaderNode.GetSlot("In");
            GodotShaderVariableType InType = GetVariableTypeFromString(In.ValueType);

            GodotFloatFunction FloatFunc = GodotFloatFunction.GFF_SATURATE;
            GodotVectorFunction VecFunc = GodotVectorFunction.GVF_SATURATE;
            bool Fetched = GetFuncsFromString(m_TypeStr, ref FloatFunc, ref VecFunc);
            if( !Fetched )
                return null;

            return GenerateFuncNodeBasedOnType( FloatFunc, VecFunc, InType, In.Vec4, ref Params );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.UVNode" )
        {
            object m_OutputChannel = GetValueForFieldOrProperty(m_Value, "m_OutputChannel", BindingFlags.Instance | BindingFlags.NonPublic);
            
            UVChannel Channel = (UVChannel)m_OutputChannel;            
            string InputName = "uv";            
            if ( Channel == UVChannel.UV1 )
                InputName = "uv2";           

            return GenerateInputNode( InputName, ref Params );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.NormalVectorNode" )
        {
            Type GeometryNodeType = GetTypeInHierarchyByName(m_ValueType, "GeometryNode");
            object m_Space = GetValueForFieldOrProperty(m_Value, GeometryNodeType, "m_Space", BindingFlags.Instance | BindingFlags.NonPublic);
            UnityEditor.ShaderGraph.Internal.CoordinateSpace Space = (CoordinateSpace)m_Space;
            if( m_Space != null )
            {
                if( Space == CoordinateSpace.World )
                {

                }
            }
            string Text = string.Format( CultureInfo.InvariantCulture,
                      "//Vertex/PixelNormalWS:{0}\n" +
                      //This is the only way UTG_Terrain applies correct triplanar mapping
                      "output0 = ( INV_VIEW_MATRIX * vec4( NORMAL, 0.0) ).xyz;\n",
                      ShaderNode.ID
            );

            return GenerateShaderExpression( Text, GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.TangentVectorNode" )
        {
            return GenerateInputNode( "tangent", ref Params );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.SmoothstepNode" )
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            UTGSlot Edge1 = ShaderNode.GetSlot("Edge1");
            UTGSlot Edge2 = ShaderNode.GetSlot("Edge2");
            UTGSlot In = ShaderNode.GetSlot("In");

            GodotShaderVariableType InType = GetVariableTypeFromString(In.ValueType);
            GodotShaderVariableType_Mix MixType = GetVariableType_Mix(InType);

            string Text = string.Format( CultureInfo.InvariantCulture,                  
                  "op_type = {0}\n",
                  (int)MixType
                );
            Params = Text;

            return "VisualShaderNodeSmoothStep";
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.RedirectNodeData" ||
            m_TypeStr == "UnityEditor.ShaderGraph.PreviewNode" )
        {
            return GenerateRerouteNode(GodotShaderVariableType.GSVT_VECTOR4, ref Params);
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.DistanceNode" )
        {
            return "VisualShaderNodeVectorDistance";
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.VertexColorNode" )
        {
            return GenerateInputNode( "color", ref Params );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.TimeNode" )
        {
            if (UsesOnlyOutputConnection( ShaderData, ShaderNode, 0 ))
                return GenerateInputNode( "time", ref Params );
            else
            {
                string Text = string.Format( CultureInfo.InvariantCulture,
                  "//TimeNode:{0}\n" +
                  "output0 =  TIME;\n" +
                  "output1 =  sin( TIME );\n" +
                  "output2 =  cos( TIME );\n" +
                  "output3 =  1.0/60.0;//Delta\n" +
                  "output4 =  1.0/60.0;//SmoothDelta\n",
                  ShaderNode.ID           
                );

                return GenerateShaderExpression(Text, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT,
                    GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode);
            }
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.IsFrontFaceNode" )
        {
            return GenerateInputNode( "front_facing", ref Params );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.ColorNode" )
        {
            object m_Color = GetValueForFieldOrProperty(m_Value, "m_Color", BindingFlags.Instance | BindingFlags.NonPublic);
            object color = GetValueForFieldOrProperty(m_Color, "color", BindingFlags.Instance | BindingFlags.Public);
            UnityEngine.Color c = (UnityEngine.Color)color;
            Params = string.Format( CultureInfo.InvariantCulture, "constant = Color({0}, {1}, {2}, {3})\n", c.r, c.g, c.b, c.a );
            return "VisualShaderNodeColorConstant";
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.SwizzleNode" )
        {
            object convertedMask = GetValueForFieldOrProperty(m_Value, "convertedMask", BindingFlags.Instance | BindingFlags.Public);
            string mask = (string)convertedMask;
            int NumComponents = mask.Length;
            GodotShaderVariableType OutType = NumComponentsToVectorType(NumComponents);

            string Text = string.Format( CultureInfo.InvariantCulture,
                  "//SwizzleNode:{0}\n" +
                  "output0 =  input0.{1};\n",
                  ShaderNode.ID,
                  mask
                );

            return GenerateShaderExpression( GodotShaderVariableType.GSVT_VECTOR4, Text, OutType, ref Params, ShaderNode );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.HueNode" )
        {
            UTGSlot OffsetSlot = ShaderNode.GetSlot("Offset");
            if( !OffsetSlot.isConnected )
            {
                AddNoConnectionConstantNode( OffsetSlot, ShaderData, ShaderNode, CurrentShaderType );
            }
            bool Normalized = false;
            object m_HueMode = GetValueForFieldOrProperty(m_Value, "m_HueMode", BindingFlags.Instance | BindingFlags.NonPublic);
            if( m_HueMode.ToString() == "Normalized" )
                Normalized = true;

            string Text = string.Format( CultureInfo.InvariantCulture,
                  "//HueNode:{0}\n" +
                  "Unity_Hue( input0, input1, {1}, output0 );\n",
                  ShaderNode.ID,
                  Normalized.ToString().ToLower()
                );

            return GenerateShaderExpression( GodotShaderVariableType.GSVT_VECTOR3, GodotShaderVariableType.GSVT_FLOAT, Text, GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.TransformNode" )
        {
            UTGSlot InSlot = ShaderNode.GetSlot("In");
            if( !InSlot.isConnected )
            {
                AddNoConnectionConstantNode( InSlot, ShaderData, ShaderNode, CurrentShaderType );
            }

            object m_Conversion = GetValueForFieldOrProperty(m_Value, "m_Conversion", BindingFlags.Instance | BindingFlags.NonPublic);
            object m_ConversionType = GetValueForFieldOrProperty(m_Value, "m_ConversionType", BindingFlags.Instance | BindingFlags.Public);

            object from = GetValueForFieldOrProperty(m_Conversion, "from", BindingFlags.Instance | BindingFlags.Public);
            object to = GetValueForFieldOrProperty(m_Conversion, "to", BindingFlags.Instance | BindingFlags.Public);

            string Text = "";
            if( from.ToString() == "Object" && ( to.ToString() == "World" ||
                                                 to.ToString() == "AbsoluteWorld") )
            {
                Text = string.Format( CultureInfo.InvariantCulture,
                      "//TransformPosition Object->World:{0}\n" +
                        "mat4 ModelMatrix = MODEL_MATRIX;\n" +
                        "output0 = ( vec4(input0, 1.0) * ModelMatrix ).xyz;\n",
                       ShaderNode.ID
                );
            }
            else if( (from.ToString() == "World" || from.ToString() == "AbsoluteWorld" ) 
                     && to.ToString() == "Object" )
            {
                Text = string.Format( CultureInfo.InvariantCulture,
                      "//Transform World -> Object:{0}\n" +
                      "mat4 InvModelMatrix = inverse(MODEL_MATRIX);\n" +
                      "output0 = ( vec4(input0, 1.0) * InvModelMatrix ).xyz;\n",
                       ShaderNode.ID
                );
            }
            else if( from.ToString() == "Tangent" && ( to.ToString() == "World" ||
                                                       to.ToString() == "AbsoluteWorld") )
            {
                Text = string.Format( CultureInfo.InvariantCulture,
                        "//TransformNode Tangent->World:{0}\n" +
                        "vec3 WorldTangent  = ( INV_VIEW_MATRIX * vec4( TANGENT, 0.0)).xyz;\n" +
                        "vec3 WorldBinormal = ( INV_VIEW_MATRIX * vec4( BINORMAL, 0.0)).xyz;\n" +
                        "vec3 WorldNormal   = ( INV_VIEW_MATRIX * vec4( NORMAL, 0.0)).xyz;\n" +
                        "mat3 TBN = mat3( WorldTangent, WorldBinormal, WorldNormal );\n" +
                        "output0 = input0 * TBN;\n",
                         ShaderNode.ID
                );
            }
            else if( from.ToString() == "Tangent" && to.ToString() == "Object" )                                                       
            {
                Text = string.Format( CultureInfo.InvariantCulture,
                        "//TransformNode Tanget->Object:{0}\n" +
                        "vec3 WorldTangent  = ( INV_VIEW_MATRIX * vec4( TANGENT, 0.0)).xyz;\n" +
                        "vec3 WorldBinormal = ( INV_VIEW_MATRIX * vec4( BINORMAL, 0.0)).xyz;\n" +
                        "vec3 WorldNormal   = ( INV_VIEW_MATRIX * vec4( NORMAL, 0.0)).xyz;\n" +
                        "mat3 TBN = mat3( WorldTangent, WorldBinormal, WorldNormal );\n" +
                        "output0 = input0 * inverse( TBN );\n",
                         ShaderNode.ID
                );
            }
            else if((from.ToString() == "World" || from.ToString() == "AbsoluteWorld") 
                    && to.ToString() == "Tangent" )
            {
                Text = string.Format( CultureInfo.InvariantCulture,
                      "//TransformNode World -> Tangent:{0}\n" +
                      "vec3 WorldTangent  = ( INV_VIEW_MATRIX * vec4( TANGENT, 0.0)).xyz;\n" +
                      "vec3 WorldBinormal = ( INV_VIEW_MATRIX * vec4( BINORMAL, 0.0)).xyz;\n" +
                      "vec3 WorldNormal   = ( INV_VIEW_MATRIX * vec4( NORMAL, 0.0)).xyz;\n" +
                      "mat3 TBN = mat3( WorldTangent, WorldBinormal, WorldNormal );\n" +
                      "mat3 INV_TBN = inverse(TBN);\n" +
                      "output0 = input0 * INV_TBN;\n",
                       ShaderNode.ID
                        );
            }
            else if( from.ToString() == to.ToString() )
            {
                Text = string.Format( CultureInfo.InvariantCulture,
                    "//TransformNode SameSpace:{0}\n" +
                    "output0 =  input0;\n",
                  ShaderNode.ID
                );
            }
            else
            {
                Text = string.Format( CultureInfo.InvariantCulture,
                    "//TransformNode:{0}\n" +
                    "//IMPLEMENT ME " + from.ToString() + " -> " + to.ToString() + "\n" +
                    "output0 =  input0;\n",
                  ShaderNode.ID
                );
            }

            return GenerateShaderExpression( GodotShaderVariableType.GSVT_VECTOR3, Text, GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.KeywordNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.DropdownNode" )
        {
            UTGSlot FirstInputSlot = null;
            int HeightIndex = 0;
            for( int i = 0; i < ShaderNode.ProcessedSlots.Count; i++ )
            {
                UTGSlot Slot = ShaderNode.ProcessedSlots[i];
                if( Slot.Input )
                {
                    FirstInputSlot = Slot;
                    Slot.InputIndex += 1;
                    if( !Slot.isConnected )
                    {
                        AddNoConnectionConstantNode( Slot, ShaderData, ShaderNode, CurrentShaderType, HeightIndex );
                        HeightIndex++;
                    }
                }
            }

            ShaderParameter Parameter = null;
            ShaderParameterType ParameterType = ShaderParameterType.SPT_BOOL;
            string NameStr ="";
            bool BoolValue = false;
            int IntValue = -1;
            string DefaultValueString = BoolValue.ToString().ToLower();
            List<object> EntriesList = null;
            if (m_TypeStr == "UnityEditor.ShaderGraph.DropdownNode")
            {
                object m_Dropdown = GetValueForFieldOrProperty(m_Value, "m_Dropdown", BindingFlags.Instance | BindingFlags.NonPublic);
                object m_DropdownValue = GetValueForFieldOrProperty(m_Dropdown, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);
                object m_Entries = GetValueForFieldOrProperty(m_DropdownValue, "m_Entries", BindingFlags.Instance | BindingFlags.NonPublic);
                object Name = GetValueForFieldOrProperty(m_DropdownValue, m_DropdownValue.GetType().BaseType,"m_Name", BindingFlags.Instance | BindingFlags.NonPublic);
                object EntryValue = GetValueForFieldOrProperty(m_DropdownValue, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);
                object EntryName = GetValueForFieldOrProperty(m_DropdownValue, "entryName", BindingFlags.Instance | BindingFlags.NonPublic);
                NameStr = (string)Name;
                SanitizeParamName(ref NameStr);
                EntriesList = GetList(m_Entries);
                ParameterType = ShaderParameterType.SPT_INT;
                IntValue = (int)EntryValue;

                if ( ShaderNode.ParentGraphNode != null )
                {
                    object m_DropdownSelectedEntries = GetValueForFieldOrProperty(ShaderNode.ParentGraphNode.NodeObject, "m_DropdownSelectedEntries", BindingFlags.Instance | BindingFlags.NonPublic);
                    var DropdownSelectedEntriesList = GetList( m_DropdownSelectedEntries );
                    object m_Dropdowns = GetValueForFieldOrProperty(ShaderNode.ParentGraphNode.NodeObject, "m_Dropdowns", BindingFlags.Instance | BindingFlags.NonPublic);
                    var m_DropdownsList = GetList( m_Dropdowns );

                    for (int d = 0; d < m_DropdownsList.Count; d++)
                    {
                        string Compare = "_" + NameStr;
                        if (m_DropdownsList[d].Equals( Compare ) )
                        {
                            object SubGraphEntryName = DropdownSelectedEntriesList[d].ToString();
                            EntryName = SubGraphEntryName;
                            for(int e = 0; e < EntriesList.Count; e++)
                            {
                                object displayName = GetValueForFieldOrProperty(EntriesList[e], "displayName", BindingFlags.Instance | BindingFlags.Public);
                                if (displayName.Equals(EntryName))
                                {
                                    IntValue = e;
                                    break;
                                }
                            }                            
                        }
                    }
                }
            }
            else
            {
                object m_Keyword = GetValueForFieldOrProperty(m_Value, "m_Keyword", BindingFlags.Instance | BindingFlags.NonPublic);
                object Keyword_m_Value = GetValueForFieldOrProperty(m_Keyword, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);

                Type ShaderInputType = GetTypeInHierarchyByName(Keyword_m_Value.GetType(), "ShaderInput");
                object Name = GetValueForFieldOrProperty(Keyword_m_Value, ShaderInputType, "m_Name", BindingFlags.Instance | BindingFlags.NonPublic);
                NameStr = (string)Name;
                object KeywordValue = GetValueForFieldOrProperty(Keyword_m_Value, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);
                object m_Entries = GetValueForFieldOrProperty(Keyword_m_Value, "m_Entries", BindingFlags.Instance | BindingFlags.NonPublic);
                object KeywordType = GetValueForFieldOrProperty(Keyword_m_Value, "m_KeywordType", BindingFlags.Instance | BindingFlags.NonPublic);
                EntriesList = GetList(m_Entries);
                
                if (KeywordValue.ToString() == "1")
                    BoolValue = true;
                IntValue = Convert.ToInt32(KeywordValue);
                DefaultValueString = BoolValue.ToString().ToLower();
                if (KeywordType.ToString() == "Enum")
                {
                    ParameterType = ShaderParameterType.SPT_INT;
                    DefaultValueString = KeywordValue.ToString();
                }
            }
            
            Parameter = ShaderData.GetShaderParameter( NameStr, ParameterType, CurrentShaderType );
            if( Parameter == null )
            {
                string DependentNodeParams = "";
                string DependentNodeType = "";

                SanitizeParamName( ref NameStr );
                DependentNodeType = GenerateParameterNode( BoolValue, IntValue, Vector4.zero, ParameterType, NameStr, ref DependentNodeParams );

                GodotShaderNode DependentNode = GenerateShaderNode(ShaderData, null, null,
                    CurrentShaderType, DependentNodeType, ShaderNode.GraphObject, ShaderNode.ParentGraphNode, (int)ShaderNode.Position.x - 500, (int)ShaderNode.Position.y + 200);
                DependentNode.DefinitionText += DependentNodeParams;

                Parameter = ShaderData.AddShaderParameter( ref NameStr, NameStr, ParameterType, CurrentShaderType, 0.0f, Vector4.zero, null, BoolValue, IntValue, DependentNode );
            }

            ShaderData.AddShaderConnection( Parameter.Node, 0, ShaderNode, 0, CurrentShaderType );

            GodotShaderVariableType OpType = FirstInputSlot.GetVariableType();

            if( ParameterType == ShaderParameterType.SPT_BOOL )
            {
                string Text = string.Format( CultureInfo.InvariantCulture,
                      "default_input_values = [0, {0}, 1, Vector3(1, 1, 1), 2, Vector3(0, 0, 0)]\n" +
                      "op_type = {1}\n",
                      DefaultValueString,
                      (int)OpType
                    );
                Params += Text;
                return "VisualShaderNodeSwitch";
            }
            else
            {
                string Text = string.Format( CultureInfo.InvariantCulture,
                      "//{1}:{0}\n" +
                      "int Val = input0 ;\n" +
                      "switch( Val )\n" +
                      "{{\n",
                        ShaderNode.ID,
                        m_TypeStr );

                GodotShaderExpressionParameter ValueParameter = new GodotShaderExpressionParameter(true, "input0", GodotShaderVariableType.GSVT_INT);
                GodotShaderExpressionParameter OutputParameter = new GodotShaderExpressionParameter(false, "output0", OpType);
                List<GodotShaderExpressionParameter> Parameters = new List<GodotShaderExpressionParameter>();
                Parameters.Add( ValueParameter );
                Parameters.Add( OutputParameter );

                for( int i = 0; i < EntriesList.Count; i++ )
                {
                    object Entry = EntriesList[i];
                    object EntryName = GetValueForFieldOrProperty(Entry, "displayName", BindingFlags.Instance | BindingFlags.Public);
                    Text += string.Format( CultureInfo.InvariantCulture, "\tcase {0}: output0 = input{1}; break;//{2}\n", i, i + 1, EntryName );

                    GodotShaderExpressionParameter NewInputParameter = new GodotShaderExpressionParameter(true, string.Format( CultureInfo.InvariantCulture,"input{0}", i + 1), OpType);
                    Parameters.Add( NewInputParameter );
                }
                Text += "\tdefault: output0 = input1;break;\n";
                Text += "}\n";

                return GenerateShaderExpression( ref Parameters, Text, ref Params, ShaderNode );
            }
        }        
        if( m_TypeStr == "UnityEditor.ShaderGraph.RemapNode" )
        {
            UTGSlot InSlot = ShaderNode.GetSlot("In");

            AddConstantNodesForUnconnectedInputs( ShaderData, ShaderNode, CurrentShaderType );

            string RemapString = "";
            switch( InSlot.GetVariableType() )
            {
                case GodotShaderVariableType.GSVT_FLOAT: RemapString = "Unity_Remap_float( input0, input1, input2, output0 );\n"; break;
                case GodotShaderVariableType.GSVT_VECTOR2: RemapString = "Unity_Remap_float( input0.x, input1, input2, output0.x );\n" +
                                                                        "Unity_Remap_float( input0.y, input1, input2, output0.y );\n"; break;
                case GodotShaderVariableType.GSVT_VECTOR3: RemapString = "Unity_Remap_float( input0.x, input1, input2, output0.x );\n" +
                                                                        "Unity_Remap_float( input0.y, input1, input2, output0.y );\n" +
                                                                        "Unity_Remap_float( input0.z, input1, input2, output0.z );\n"; break;
                case GodotShaderVariableType.GSVT_VECTOR4: RemapString = "Unity_Remap_float( input0.x, input1, input2, output0.x );\n" +
                                                                        "Unity_Remap_float( input0.y, input1, input2, output0.y );\n" +
                                                                        "Unity_Remap_float( input0.z, input1, input2, output0.z );\n" +
                                                                        "Unity_Remap_float( input0.w, input1, input2, output0.w );\n"; break;

            }
            string Text = string.Format( CultureInfo.InvariantCulture,
                      "//RemapNode:{0}\n" +
                      "{1}",
                        ShaderNode.ID,
                        RemapString);

            return GenerateShaderExpression( InSlot.GetVariableType(), GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_VECTOR2,
                                            Text, InSlot.GetVariableType(), ref Params, ShaderNode );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.RandomRangeNode" )
        {
            UTGSlot SeedSlot = ShaderNode.GetSlot("Seed");
            UTGSlot MinSlot = ShaderNode.GetSlot("Min");
            UTGSlot MaxSlot = ShaderNode.GetSlot("Max");

            string Text = string.Format( CultureInfo.InvariantCulture,
                  "default_input_values = [0, Vector3( {0}, {1}, 0 ), 1, {2}, 2, {3}]\n",
                  SeedSlot.Vec4.x, SeedSlot.Vec4.y, MinSlot.Vec4.x, MaxSlot.Vec4.x);

            Params += Text;

            return "VisualShaderNodeRandomRange";
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.CameraNode" )
        {
            string Text = string.Format( CultureInfo.InvariantCulture,
                      "//CameraNode:{0}\n" +
                      "output0 = CAMERA_POSITION_WORLD;\n" +
                      "output1 = CAMERA_DIRECTION_WORLD;\n" +
                      "output2 = 0.0;//Orthografic\n" +
                      "output3 = 1.0;//NearPlane\n" +
                      "output4 = 10000.0;//FarPlane\n" +
                      "output5 = 1.0;//ZBufferSign\n" +
                      "output6 = 1920.0;//Width\n" +
                      "output7 = 1080.0;//Height\n"
                      , ShaderNode.ID);
            return GenerateShaderExpression( Text, GodotShaderVariableType.GSVT_VECTOR3, GodotShaderVariableType.GSVT_VECTOR3, GodotShaderVariableType.GSVT_FLOAT,
                GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT,
                GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.ObjectNode" )
        {
            string Text = string.Format( CultureInfo.InvariantCulture,
                      "//ObjectNode:{0}\n" +
                      "output0 = MODEL_MATRIX[3].xyz;\n" +
                      "output1 = vec3(length(vec3(MODEL_MATRIX[0].x, MODEL_MATRIX[1].x, MODEL_MATRIX[2].x)),\r\n" +
                                "length(vec3(MODEL_MATRIX[0].y, MODEL_MATRIX[1].y, MODEL_MATRIX[2].y)),\r\n" +
                                "length(vec3(MODEL_MATRIX[0].z, MODEL_MATRIX[1].z, MODEL_MATRIX[2].z)));\n"
                      , ShaderNode.ID);
            return GenerateShaderExpression( Text, GodotShaderVariableType.GSVT_VECTOR3, GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.NormalStrengthNode" )
        {
            AddConstantNodesForUnconnectedInputs( ShaderData, ShaderNode, CurrentShaderType );

            string Text = string.Format( CultureInfo.InvariantCulture,
                      "//NormalStrengthNode:{0}\n" +
                      "output0 = vec3( input0.rg * input1, mix(1.0, input0.b, saturate(input1)));\n"
                      , ShaderNode.ID);
            return GenerateShaderExpression( GodotShaderVariableType.GSVT_VECTOR3, GodotShaderVariableType.GSVT_FLOAT, Text, GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.RadiansToDegreesNode" )
        {
            AddConstantNodesForUnconnectedInputs( ShaderData, ShaderNode, CurrentShaderType );

            GodotShaderVariableType OpType = ShaderNode.ProcessedSlots[0].GetVariableType();

            string Text = string.Format( CultureInfo.InvariantCulture,
                      "//RadiansToDegreesNode:{0}\n" +
                      "output0 = degrees( input0 );\n"
                      , ShaderNode.ID);
            return GenerateShaderExpression( OpType, Text, OpType, ref Params, ShaderNode );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.ViewDirectionNode" )
        {
            string Text = string.Format( CultureInfo.InvariantCulture,
                      "//ViewDirectionNode:{0}\n" +
                      "output0 = CAMERA_DIRECTION_WORLD;\n"
                      , ShaderNode.ID);
            return GenerateShaderExpression( Text, GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode );
        }

        if( m_TypeStr == "UnityEditor.ShaderGraph.NoiseNode" )
        {
            AddConstantNodesForUnconnectedInputs( ShaderData, ShaderNode, CurrentShaderType );
            UTGSlot UVSlot = ShaderNode.GetSlot("UV");
            UTGSlot ScaleSlot = ShaderNode.GetSlot("Scale");

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//NoiseNode:{0}\n" +
                    "Unity_SimpleNoise_float( input0, input1, output0 );\n"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression( GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_FLOAT, Text, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.StepNode" )
        {
            AddConstantNodesForUnconnectedInputs( ShaderData, ShaderNode, CurrentShaderType );

            UTGSlot EdgeSlot = ShaderNode.GetSlot("Edge");
            UTGSlot InSlot = ShaderNode.GetSlot("In");

            string DefaultInputValues = "";
            GodotShaderVariableType FinalType = Get2InputNodeDefaultValuesAndTypes(EdgeSlot.Vec4, InSlot.Vec4,
                                                                                EdgeSlot.GetVariableType(), InSlot.GetVariableType(),
                                                                                ref DefaultInputValues);
            GodotShaderVariableType_Mix MixFinalType = GetVariableType_Mix(FinalType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                  "{0}\n" +
                  "op_type = {1}\n",
                  DefaultInputValues,
                  (int)MixFinalType
                );
            Params = Text;

            return "VisualShaderNodeStep";
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.ConstantNode" )
        {
            object m_constant = GetValueForFieldOrProperty(m_Value, "m_constant", BindingFlags.Instance | BindingFlags.NonPublic);
            string ConstantStr = m_constant.ToString();
            float Constant = 0.0f;
            switch( ConstantStr )
            {
                case "PI": Constant = 3.1415926f; break;
                case "TAU": Constant = 6.28318530f; break;
                case "PHI": Constant = 1.618034f; break;
                case "E": Constant = 2.718282f; break;
                case "SQRT2": Constant = 1.414214f; break;
            }

            return GenerateConstantNode( Constant, ref Params );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.SaturationNode" )
        {
            AddConstantNodesForUnconnectedInputs( ShaderData, ShaderNode, CurrentShaderType );

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//SaturationNode:{0}\n" +
                    "float luma = dot( input0, vec3(0.2126729, 0.7151522, 0.0721750));\r\n" +
                    "output0 = vec3(luma,luma,luma) + vec3(input1,input1,input1) * ( input0 - vec3(luma,luma,luma) );\n"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression( GodotShaderVariableType.GSVT_VECTOR3, GodotShaderVariableType.GSVT_FLOAT,
                Text, GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode );

        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.VertexIDNode" )
        {
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//VertexIDNode:{0}\n" +
                    "output0 = float( VERTEX_ID );\r\n"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression( Text, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.InstanceIDNode" )
        {
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//InstanceIDNode:{0}\n" +
                    "output0 = float( INSTANCE_ID );\r\n"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression( Text, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.Texture2DPropertiesNode" )//TexelSize
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);            

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//TexelSize:{0}\n" +
                    "ivec2 size = textureSize(input0, 0);\n" +
                    "output0 = 1.0 / float( size.x );\n" +
                    "output1 = 1.0 / float( size.y );\n"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression( GodotShaderVariableType.GSVT_SAMPLER, Text, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode );
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.NormalFromTextureNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//NormalFromTextureNode:{0}\n" +
                    "vec2 uv = input1;\n" +
                    "float Offset = input3;\n" +
                    "float Strength = input4;\n" +
                    
                    "            Offset = pow(Offset, 3) * 0.1;\r\n" +
                    "            vec2 offsetU = vec2(uv.x + Offset, uv.y);\r\n" +
                    "            vec2 offsetV = vec2(uv.x, uv.y + Offset);\r\n" +
                    "            float normalSample = texture(input0, uv).r;\r\n" +
                    "            float uSample = texture(input0, offsetU).r;\r\n" +
                    "            float vSample = texture(input0, offsetV).r;\r\n" +
                    "            vec3 va = vec3(1.0, 0.0, (uSample - normalSample) * Strength);\r\n" +
                    "            vec3 vb = vec3(0.0, 1.0, (vSample - normalSample) * Strength);\r\n" +
                    "            output0 = normalize(cross(va, vb));\n"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression( GodotShaderVariableType.GSVT_SAMPLER, GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_FLOAT,
                GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, Text, GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode );
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.SampleCubemapNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//ReflectedCubemapNode:{0}\n" +
                    "vec3 WorldNormal   = input2;//( INV_VIEW_MATRIX * vec4( NORMAL, 0.0)).xyz;\n" +
                    "vec3 WorldPosition = ( INV_VIEW_MATRIX * vec4( VERTEX, 1.0) ).xyz;\r\n" +
                    "vec3 CameraVec = normalize( WorldPosition - CAMERA_POSITION_WORLD );\r\n" +
                    "vec3 uv = reflect( CameraVec, WorldNormal);\n" +
                    "output0 = texture( input0, uv );\n"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_SAMPLER, GodotShaderVariableType.GSVT_VECTOR3, GodotShaderVariableType.GSVT_VECTOR3,
                GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, Text, GodotShaderVariableType.GSVT_VECTOR4, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.SampleTexture2DArrayNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//SampleTexture2DArrayNode:{0}\n" +
                    "vec4 RGBA = texture( input0, vec3( input2, input1) );\n" +
                    "output0 = RGBA;\n" +
                    "output1 = RGBA.r;\n" +
                    "output2 = RGBA.g;\n" +
                    "output3 = RGBA.b;\n" +
                    "output4 = RGBA.a;\n"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_SAMPLER, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_VECTOR2,
                GodotShaderVariableType.GSVT_FLOAT, Text, GodotShaderVariableType.GSVT_VECTOR4, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT,
                GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.GatherTexture2DNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//GatherTexture2DNode:{0}\n" +
                    "vec4 RGBA = textureGather( input0, input1 );//, ivec2( int(input3.x), int(input3.y) ) );\n" +
                    "output0 = RGBA;\n" +
                    "output1 = RGBA.r;\n" +
                    "output2 = RGBA.g;\n" +
                    "output3 = RGBA.b;\n" +
                    "output4 = RGBA.a;\n"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_SAMPLER, GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_FLOAT,
                GodotShaderVariableType.GSVT_VECTOR2, Text, GodotShaderVariableType.GSVT_VECTOR4, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT,
                GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.CalculateLevelOfDetailTexture2DNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);            

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//CalculateLevelOfDetailTexture2DNode:{0}\n" +
                    "vec4 texelSize = vec4(1024,1024,1.0/1024.0,1.0/1024.0);\n" +
                    "float LOD = 0.5f*log2(max(dot(dFdx(input1.xy * texelSize.zw), dFdx(input1.xy * texelSize.zw)), dot(dFdy(input1.xy * texelSize.zw), dFdy(input1.xy * texelSize.zw))));\n" +
                    "LOD = max( LOD, 0.0 );"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_SAMPLER, GodotShaderVariableType.GSVT_VECTOR2, Text, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.TriplanarNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
            string SamplerExp = "texture";
            UTGSlot TextureSlot = ShaderNode.GetSlot("Texture");
            if ( TextureSlot != null && TextureSlot.isConnected)
            {
                object ConnectingSlot = null;
                object PropertyNode = ShaderData.GetConnectingNodeAndSlot( TextureSlot.Slot, ref ConnectingSlot );
                if ( PropertyNode != null )
                {
                    object m_Property = GetValueForFieldOrProperty(PropertyNode, "m_Property", BindingFlags.Instance | BindingFlags.NonPublic);
                    object PropertyValue = GetValueForFieldOrProperty(m_Property, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);
                    object m_DefaultType = GetValueForFieldOrProperty(PropertyValue, "m_DefaultType", BindingFlags.Instance | BindingFlags.NonPublic);
                    UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty.DefaultType DefaultType = (UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty.DefaultType)m_DefaultType;
                    if ( DefaultType == Texture2DShaderProperty.DefaultType.NormalMap )
                        SamplerExp = "SampleNormalMap";
                }
            }
            
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//TriplanarNode:{0}\n" +
                    "vec3 uv = input2 * input4;\r\n" +
                    "vec3 Blend;\n" +
                    "Blend.x = SafePositivePow_float( input3.x, min(float(input5), floor(log2({1})/log2(1.0/sqrt(3.0)))) );\r\n" +
                    "Blend.y = SafePositivePow_float( input3.y, min(float(input5), floor(log2({1})/log2(1.0/sqrt(3.0)))) );\r\n" +
                    "Blend.z = SafePositivePow_float( input3.z, min(float(input5), floor(log2({1})/log2(1.0/sqrt(3.0)))) );\r\n" +
                    "Blend /= dot(Blend, vec3(1.0,1.0,1.0) );\r\n" +
                    "vec4 SampleX = {2}( input0, uv.zy );\r\n" +
                    "vec4 SampleY = {2}( input0, uv.xz );\r\n" +
                    "vec4 SampleZ = {2}( input0, uv.xy );\r\n" +
                    "output0 = SampleX * Blend.x + SampleY * Blend.y + SampleZ * Blend.z;\n"
                    , ShaderNode.ID, float.MinValue, SamplerExp
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_SAMPLER, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_VECTOR3, GodotShaderVariableType.GSVT_VECTOR3,
                GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, Text, GodotShaderVariableType.GSVT_VECTOR4, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.ParallaxMappingNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            object m_HeightmapSampleChannel = GetValueForFieldOrProperty(m_Value, "m_Channel", BindingFlags.Instance | BindingFlags.NonPublic);

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//ParallaxMappingNode:{0}\n" +
                    
                    "float scale = input2 * 0.01;\n" +
                    "vec2 uv = input3;\n" +
                    "int channel = {1};\r\n" +

                   //"vec3 WorldTangent  = ( INV_VIEW_MATRIX * vec4( TANGENT, 0.0)).xyz;\n" +
                   //"vec3 WorldBinormal = ( INV_VIEW_MATRIX * vec4( BINORMAL, 0.0)).xyz;\n" +
                   //"vec3 WorldNormal   = ( INV_VIEW_MATRIX * vec4( NORMAL, 0.0)).xyz;\n" +
                   //"mat3 TBN = mat3( WorldTangent, WorldBinormal, WorldNormal );\n" +
                   "mat3 TBN = mat3( TANGENT, BINORMAL, NORMAL );\n" +
                   //"vec3 WorldPosition = ( INV_VIEW_MATRIX * vec4( VERTEX, 1.0)).xyz;\n" +
                   //"vec3 ViewDirection = WorldPosition - CAMERA_POSITION_WORLD;\n" +
                   "vec3 ViewDirection = VIEW;\n" +
                   //"vec3 viewDirTS = inverse(TBN) * ViewDirection;\n" +
                   "vec3 viewDirTS = ViewDirection * TBN;\n" +
                    "    float h = texture( input0, uv)[channel];\r\n" +
                    //"    float2 offset = ParallaxOffset1Step(h, scale, viewDirTS);\r\n" +
                    //"    output0 = offset;\r\n"

                    "    float height = h * scale - scale / 2.0;\r\n" +
                    "    vec3 v = normalize(viewDirTS);\r\n" +
                    "    v.z += 0.42;\r\n" +
                    "    output0 = uv - vec2(height,height) * (v.xy / v.z);\n"


                    , ShaderNode.ID, (int)m_HeightmapSampleChannel
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_SAMPLER, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_VECTOR2,
                 Text, GodotShaderVariableType.GSVT_VECTOR2, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.ParallaxOcclusionMappingNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);            

            object m_HeightmapSampleChannel = GetValueForFieldOrProperty(m_Value, "m_Channel", BindingFlags.Instance | BindingFlags.NonPublic);

            //TBD : shader is a copy-paste of ParallaxMappingNode
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//ParallaxOcclusionMappingNode:{0}\n" +
                    "float scale = input2 * 0.01;\n" +
                    "vec2 uv = input4;\n" +
                    "int channel = {1};\r\n" +
                   "mat3 TBN = mat3( TANGENT, BINORMAL, NORMAL );\n" +
                   "vec3 ViewDirection = VIEW;\n" +
                   "vec3 viewDirTS = ViewDirection * TBN;\n" +
                    "    float h = texture( input0, uv)[channel];\r\n" +                 
                    "    float height = h * scale - scale / 2.0;\r\n" +
                    "    vec3 v = normalize(viewDirTS);\r\n" +
                    "    v.z += 0.42;\r\n" +
                    "    output0 = 0.5;//PixelDepthOffset\n" +
                    "    output1 = uv - vec2(height,height) * (v.xy / v.z);\n"

                    , ShaderNode.ID, (int)m_HeightmapSampleChannel
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_SAMPLER, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT,
                GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_VECTOR2,
                GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT,
                 Text, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_VECTOR2, ref Params, ShaderNode);
        }
        if ( m_TypeStr == "UnityEditor.ShaderGraph.BlendNode" )
        {
            AddConstantNodesForUnconnectedInputs( ShaderData, ShaderNode, CurrentShaderType );
            UTGSlot BaseSlot = ShaderNode.GetSlot("Base");
            UTGSlot BlendSlot = ShaderNode.GetSlot("Blend");

            object m_BlendMode = GetValueForFieldOrProperty(m_Value, "m_BlendMode", BindingFlags.Instance | BindingFlags.NonPublic);
            string BlendMode = m_BlendMode.ToString();

            string FunctionText = "";
            switch( BlendMode )
            {
                case "Burn": FunctionText = "Unity_Blend_Burn_vec4( BaseIn, BlendIn, input2, Out );\n"; break;
                case "Darken": FunctionText = "Unity_Blend_Darken_vec4( BaseIn, BlendIn, input2, Out );\n"; break;
                case "Difference": FunctionText = "Unity_Blend_Difference_vec4( BaseIn, BlendIn, input2, Out );\n"; break;
                case "Dodge": FunctionText = "Unity_Blend_Dodge_vec4( BaseIn, BlendIn, input2, Out );\n"; break;
                case "Divide": FunctionText = "Unity_Blend_Divide_vec4( BaseIn, BlendIn, input2, Out );\n"; break;
                case "Exclusion": FunctionText = "Unity_Blend_Exclusion_vec4( BaseIn, BlendIn, input2, Out );\n"; break;
                case "HardLight": FunctionText = "Unity_Blend_HardLight_vec4( BaseIn, BlendIn, input2, Out );\n"; break;
                case "HardMix": FunctionText = "Unity_Blend_HardMix_vec4( BaseIn, BlendIn, input2, Out );\n"; break;
                case "Lighten": FunctionText = "Unity_Blend_Lighten_vec4( BaseIn, BlendIn, input2, Out );\n"; break;
                case "LinearBurn": FunctionText = "Unity_Blend_LinearBurn_vec4( BaseIn, BlendIn, input2, Out );\n"; break;
                case "LinearDodge": FunctionText = "Unity_Blend_LinearDodge_vec4( BaseIn, BlendIn, input2, Out );\n"; break;
                case "LinearLight": FunctionText = "Unity_Blend_LinearLight_vec4( BaseIn, BlendIn, input2, Out );\n"; break;
                case "LinearLightAddSub": FunctionText = "Unity_Blend_LinearLightAddSub_vec4( BaseIn, BlendIn, input2, Out );\n"; break;
                case "Multiply": FunctionText = "Unity_Blend_Multiply_vec4( BaseIn, BlendIn, input2, Out );\n"; break;
                case "Negation": FunctionText = "Unity_Blend_Negation_vec4( BaseIn, BlendIn, input2, Out );\n"; break;
                case "Overlay": FunctionText = "Unity_Blend_Overlay_vec4( BaseIn, BlendIn, input2, Out );\n"; break;
                case "PinLight": FunctionText = "Unity_Blend_PinLight_vec4( BaseIn, BlendIn, input2, Out );\n"; break;
                case "Screen": FunctionText = "Unity_Blend_Screen_vec4( BaseIn, BlendIn, input2, Out );\n"; break;
                case "SoftLight": FunctionText = "Unity_Blend_SoftLight_vec4( BaseIn, BlendIn, input2, Out );\n"; break;
                case "Subtract": FunctionText = "Unity_Blend_Subtract_vec4( BaseIn, BlendIn, input2, Out );\n"; break;
                case "VividLight": FunctionText = "Unity_Blend_VividLight_vec4( BaseIn, BlendIn, input2, Out );\n"; break;
                case "Overwrite": FunctionText = "Unity_Blend_Overwrite_vec4( BaseIn, BlendIn, input2, Out );\n"; break;
                default:Debug.LogError("missing BlendMode " + BlendMode );break;
            }

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//BlendNode:{0}\n" +
                    "vec4 BaseIn  = " + PromoteInputToVec4("input0", BaseSlot.GetVariableType()) + ";\n" +
                    "vec4 BlendIn = " + PromoteInputToVec4("input1", BlendSlot.GetVariableType()) + ";\n" +
                    "vec4 Out;\n" +
                    "{1}" +
                    "output0 = " + GetSwizzleFromVec4("Out", BaseSlot.GetVariableType()) + ";"
                    , ShaderNode.ID, FunctionText
                    );
            return GenerateShaderExpression( BaseSlot.GetVariableType(), BlendSlot.GetVariableType(), GodotShaderVariableType.GSVT_FLOAT, Text,
                BaseSlot.GetVariableType(), ref Params, ShaderNode );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.CustomInterpolatorNode" )
        {
            object customBlockNodeName = GetValueForFieldOrProperty(m_Value, "customBlockNodeName", BindingFlags.Instance | BindingFlags.NonPublic);
            object e_targetBlockNode = GetValueForFieldOrProperty(m_Value, "e_targetBlockNode", BindingFlags.Instance | BindingFlags.NonPublic);
            object serializedType = GetValueForFieldOrProperty(m_Value, "serializedType", BindingFlags.Instance | BindingFlags.NonPublic);

            GodotShaderVariableType DataType = GetVariableTypeFromString(serializedType.ToString());

            string Text = string.Format( CultureInfo.InvariantCulture,
                      "varying_name = \"{0}\"\n" +
                      "varying_type = {1}\n",
                      customBlockNodeName.ToString(),
                      (int)DataType);

            Params = Text;

            return "VisualShaderNodeVaryingGetter";
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.CustomFunctionNode" )
        {
            AddConstantNodesForUnconnectedInputs( ShaderData, ShaderNode, CurrentShaderType );

            object functionName = GetValueForFieldOrProperty(m_Value, "functionName", BindingFlags.Instance | BindingFlags.NonPublic);
            object sourceType = GetValueForFieldOrProperty(m_Value, "sourceType", BindingFlags.Instance | BindingFlags.NonPublic);
            object functionSource = GetValueForFieldOrProperty(m_Value, "functionSource", BindingFlags.Instance | BindingFlags.NonPublic);
            object functionBody = GetValueForFieldOrProperty(m_Value, "functionBody", BindingFlags.Instance | BindingFlags.NonPublic);
            object concretePrecision = GetValueForFieldOrProperty(m_Value, "concretePrecision", BindingFlags.Instance | BindingFlags.NonPublic);

            string ExpressionStr = functionSource.ToString();
            if (ExpressionStr.Length == 0)
            {
                ExpressionStr = functionBody.ToString();
            }
            if( sourceType.ToString() == "File" && functionSource.ToString().Length > 0 )
            {
                string AssetPath = AssetDatabase.GUIDToAssetPath(functionSource.ToString());
                string FunctionText = File.ReadAllText(AssetPath, Encoding.UTF8);
                ExpressionStr = "//" + AssetPath + "\n" + FunctionText;
            }

            FixCustomExpressionCode( ref ExpressionStr );

            if( sourceType.ToString() == "File" )
            {
                ShaderData.CustomFuncFiles.Add( ExpressionStr );

                string Precision = "float";
                if( concretePrecision.ToString().Contains("Half", StringComparison.OrdinalIgnoreCase ))
                {
                    Precision = "half";
                }
                ExpressionStr = string.Format( CultureInfo.InvariantCulture, "{0}_{1}( ", functionName, Precision );

                //First inputs in InputIndex order, then outputs by OutputIndex order
                ShaderNode.ProcessedSlots.Sort((a, b) =>
                {
                    int cmp = a.Input.CompareTo(b.Input) * -1;
                    if (cmp != 0) return cmp;
                    if ( a.Input )
                        return a.InputIndex.CompareTo(b.InputIndex);
                    else
                        return a.OutputIndex.CompareTo(b.OutputIndex);
                });

                for( int i = 0; i < ShaderNode.ProcessedSlots.Count; i++ )
                {
                    UTGSlot slot = ShaderNode.ProcessedSlots[i];
                    if( i > 0 )
                        ExpressionStr += ",";

                    ExpressionStr += slot.Name;
                }

                ExpressionStr += ");";
            }

            List<GodotShaderExpressionParameter> Parameters = new List<GodotShaderExpressionParameter>();

            for( int i = 0; i < ShaderNode.ProcessedSlots.Count; i++ )
            {
                UTGSlot Slot = ShaderNode.ProcessedSlots[i];
                GodotShaderExpressionParameter NewParameter = new GodotShaderExpressionParameter(Slot.Input, Slot.Name, Slot.GetVariableType());
                if ( !NewParameter.IsInput && Slot.GetVariableType() == GodotShaderVariableType.GSVT_SAMPLER )
                {
                    Debug.LogError("Godot doesn't allow Sampler outputs in Custom Expressions !");
                }
                Parameters.Add( NewParameter );
            }

            return GenerateShaderExpression( ref Parameters, ExpressionStr, ref Params, ShaderNode );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.ComparisonNode" )
        {
            AddConstantNodesForUnconnectedInputs( ShaderData, ShaderNode, CurrentShaderType );
            object comparisonType = GetValueForFieldOrProperty(m_Value, "comparisonType", BindingFlags.Instance | BindingFlags.NonPublic);
            string OperationStr = "";
            if( comparisonType.ToString() == "Equal" )
            {
                OperationStr = "==";
            }
            else if( comparisonType.ToString() == "NotEqual" )
            {
                OperationStr = "!=";
            }
            else if( comparisonType.ToString() == "Less" )
            {
                OperationStr = "<";
            }
            else if( comparisonType.ToString() == "LessOrEqual" )
            {
                OperationStr = "<=";
            }
            else if( comparisonType.ToString() == "Greater" )
            {
                OperationStr = ">";
            }
            else if( comparisonType.ToString() == "GreaterOrEqual" )
            {
                OperationStr = ">=";
            }
            else
            {
                Debug.LogError("Couldn't determine operator for UnityEditor.ShaderGraph.ComparisonNode !");
            }
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//ComparisonNode:{0}:{1}\n" +
                    "output0 = input0 " + OperationStr + " input1;"
                    , ShaderNode.ID, ShaderNode.GetParentGraphName()
                    );
            return GenerateShaderExpression( GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, Text,
                GodotShaderVariableType.GSVT_BOOL, ref Params, ShaderNode );
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.NotNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                   "//NotNode:{0}\n" +
                   "output0 = !input0;"
                   , ShaderNode.ID
                   );

            return GenerateShaderExpression(GodotShaderVariableType.GSVT_BOOL, Text,
                GodotShaderVariableType.GSVT_BOOL, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.AndNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                   "//AndNode:{0}\n" +
                   "output0 = input0 && input1;"
                   , ShaderNode.ID
                   );

            return GenerateShaderExpression(GodotShaderVariableType.GSVT_BOOL, GodotShaderVariableType.GSVT_BOOL, Text,
                GodotShaderVariableType.GSVT_BOOL, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.NandNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                   "//NandNode:{0}\n" +
                   "output0 = !(input0 && input1);"
                   , ShaderNode.ID
                   );

            return GenerateShaderExpression(GodotShaderVariableType.GSVT_BOOL, GodotShaderVariableType.GSVT_BOOL, Text,
                GodotShaderVariableType.GSVT_BOOL, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.OrNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                   "//OrNode:{0}\n" +
                   "output0 = input0 || input1;"
                   , ShaderNode.ID
                   );

            return GenerateShaderExpression(GodotShaderVariableType.GSVT_BOOL, GodotShaderVariableType.GSVT_BOOL, Text,
                GodotShaderVariableType.GSVT_BOOL, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.AllNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
            UTGSlot InSlot = ShaderNode.GetSlot("In");
            string Text = string.Format( CultureInfo.InvariantCulture,
                   "//AllNode:{0}\n" +
                   "output0 = all( input0 );"
                   , ShaderNode.ID
                   );

            return GenerateShaderExpression( InSlot.GetVariableType(), Text,
                GodotShaderVariableType.GSVT_BOOL, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.AnyNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
            UTGSlot InSlot = ShaderNode.GetSlot("In");
            string Text = string.Format( CultureInfo.InvariantCulture,
                   "//AnyNode:{0}\n" +
                   "output0 = any( input0 );"
                   , ShaderNode.ID
                   );

            return GenerateShaderExpression(InSlot.GetVariableType(), Text,
                GodotShaderVariableType.GSVT_BOOL, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.IsNanNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.IsInfiniteNode" )
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            int FunctionID = 0;
            if (m_TypeStr == "UnityEditor.ShaderGraph.IsNanNode")
                FunctionID = 1;

            string Text = string.Format( CultureInfo.InvariantCulture,
                      "function = {0}\n",
                      FunctionID
            );

            Params += Text;
            return "VisualShaderNodeIs";
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.ModuloNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
            UTGSlot InSlot = ShaderNode.GetSlot("A");
            string Text = string.Format( CultureInfo.InvariantCulture,
                   "//ModuloNode:{0}\n" +
                   "output0 = mod( input0, input1 );"
                   , ShaderNode.ID
                   );

            return GenerateShaderExpression(InSlot.GetVariableType(), InSlot.GetVariableType(), Text,
                InSlot.GetVariableType(), ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.ClampNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
            UTGSlot InSlot = ShaderNode.GetSlot("In");
            UTGSlot Min = ShaderNode.GetSlot("Min");
            UTGSlot Max = ShaderNode.GetSlot("Max");

            string DefaultInputValues = GenerateDefaultInputValues3(InSlot.GetVariableType(), InSlot.Vec4, Min.Vec4, Max.Vec4);
            Params += string.Format( CultureInfo.InvariantCulture,"{0}" +
                "op_type = {1}\n",
                DefaultInputValues, (int)InSlot.GetVariableType());

            return "VisualShaderNodeClamp";
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.FresnelNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
           
            return "VisualShaderNodeFresnel";
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.NormalFromHeightNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
            
            string Text = string.Format( CultureInfo.InvariantCulture,
                   "//NormalFromHeightNode:{0}\n" +
                   "vec3 WorldTangent  = ( vec4( TANGENT, 0.0) * INV_VIEW_MATRIX ).xyz;\n" +
                   "vec3 WorldBinormal = ( vec4( BINORMAL, 0.0) * INV_VIEW_MATRIX ).xyz;\n" +
                   "vec3 WorldNormal   = ( vec4( NORMAL, 0.0) * INV_VIEW_MATRIX ).xyz;\n" +
                   "mat3 TangentMatrix = mat3( WorldTangent, WorldBinormal, WorldNormal );\r\n" +
                   "vec3 Position = ( INV_VIEW_MATRIX * vec4( VERTEX, 1.0) ).xyz;\n" +
                   "float In = input0;\n" +
                   "float Strength = input1;\n" +

                   "vec3 worldDerivativeX = dFdx(Position);\r\n" +
                   "vec3 worldDerivativeY = dFdy(Position);\r\n" +
                   "vec3 crossX = cross(TangentMatrix[2].xyz, worldDerivativeX);\r\n" +
                   "vec3 crossY = cross(worldDerivativeY, TangentMatrix[2].xyz);\r\n" +
                   "float d = dot(worldDerivativeX, crossY);\r\n" +
                   "float sgn = d < 0.0 ? (-1.0f) : 1.0f;\r\n" +
                   "float surface = sgn / max(0.000000000000001192093f, abs(d));\r\n" +
                   "float dHdx = dFdx(In);\r\n" +
                   "float dHdy = dFdy(In);\r\n" +
                   "vec3 surfGrad = surface * (dHdx*crossY + dHdy*crossX);\r\n" +
                   "output0 = SafeNormalize(TangentMatrix[2].xyz - (Strength * surfGrad));\r\n" +
                   "output0 = TransformWorldToTangent(output0, TangentMatrix);"
                   , ShaderNode.ID
                   );

            return GenerateShaderExpression(GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, Text,
                GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.BooleanNode")
        {
            object Value = GetValueForFieldOrProperty(m_Value, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);

            string Text = string.Format( CultureInfo.InvariantCulture,
                   "//BooleanNode:{0}\n" +
                   "output0 = {1};"
                   , ShaderNode.ID,
                   Value.ToString().ToLower()
                   );

            return GenerateShaderExpression( Text,
                GodotShaderVariableType.GSVT_BOOL, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.IntegerNode")
        {
            object Value = GetValueForFieldOrProperty(m_Value, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);

            string Text = string.Format( CultureInfo.InvariantCulture,
                   "//IntegerNode:{0}\n" +
                   "output0 = {1};"
                   , ShaderNode.ID,
                   Value.ToString()
                   );

            return GenerateShaderExpression( Text,
                GodotShaderVariableType.GSVT_INT, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.RefractNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            return "VisualShaderNodeVectorRefract";
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.Matrix2Node")
        {
            object m_Row0 = GetValueForFieldOrProperty(m_Value, "m_Row0", BindingFlags.Instance | BindingFlags.NonPublic);
            object m_Row1 = GetValueForFieldOrProperty(m_Value, "m_Row1", BindingFlags.Instance | BindingFlags.NonPublic);
            Vector2 Row0 = (Vector2)m_Row0;
            Vector2 Row1 = (Vector2)m_Row1;

            string Text = string.Format( CultureInfo.InvariantCulture,
                   "//Matrix2Node:{4}\n" +
                   "vec4 m_Row0 = vec4( {0}, {1}, 0, 0 );\n" +
                   "vec4 m_Row1 = vec4( {2}, {3}, 0, 0 );\n" +
                   "output0 = mat4( m_Row0, m_Row1, vec4(0,0,0,0), vec4(0,0,0,0) );\n"
                   ,
                   Row0.x, Row0.y,
                   Row1.x, Row1.y,
                   ShaderNode.ID
                   );

            return GenerateShaderExpression(Text,
                GodotShaderVariableType.GSVT_TRANSFORM, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.Matrix3Node")
        {
            object m_Row0 = GetValueForFieldOrProperty(m_Value, "m_Row0", BindingFlags.Instance | BindingFlags.NonPublic);
            object m_Row1 = GetValueForFieldOrProperty(m_Value, "m_Row1", BindingFlags.Instance | BindingFlags.NonPublic);
            object m_Row2 = GetValueForFieldOrProperty(m_Value, "m_Row2", BindingFlags.Instance | BindingFlags.NonPublic);            
            Vector3 Row0 = (Vector3)m_Row0;
            Vector3 Row1 = (Vector3)m_Row1;
            Vector3 Row2 = (Vector3)m_Row2;            

            string Text = string.Format( CultureInfo.InvariantCulture,
                   "//Matrix3Node:{9}\n" +
                   "vec4 m_Row0 = vec4( {0}, {1}, {2}, 0 );\n" +
                   "vec4 m_Row1 = vec4( {3}, {4}, {5}, 0 );\n" +
                   "vec4 m_Row2 = vec4( {6}, {7}, {8}, 0);\n" +
                   "output0 = mat4( m_Row0, m_Row1, m_Row2, vec4(0,0,0,0) );\n"
                   ,
                   Row0.x, Row0.y, Row0.z,
                   Row1.x, Row1.y, Row1.z,
                   Row2.x, Row2.y, Row2.z,
                   ShaderNode.ID
                   );

            return GenerateShaderExpression(Text,
                GodotShaderVariableType.GSVT_TRANSFORM, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.Matrix4Node")
        {
            object m_Row0 = GetValueForFieldOrProperty(m_Value, "m_Row0", BindingFlags.Instance | BindingFlags.NonPublic);
            object m_Row1 = GetValueForFieldOrProperty(m_Value, "m_Row1", BindingFlags.Instance | BindingFlags.NonPublic);
            object m_Row2 = GetValueForFieldOrProperty(m_Value, "m_Row2", BindingFlags.Instance | BindingFlags.NonPublic);
            object m_Row3 = GetValueForFieldOrProperty(m_Value, "m_Row3", BindingFlags.Instance | BindingFlags.NonPublic);
            Vector4 Row0 = (Vector4)m_Row0;
            Vector4 Row1 = (Vector4)m_Row1;
            Vector4 Row2 = (Vector4)m_Row2;
            Vector4 Row3 = (Vector4)m_Row3;

            string Text = string.Format( CultureInfo.InvariantCulture,
                   "//Matrix4Node:{16}\n" +
                   "vec4 m_Row0 = vec4( {0}, {1}, {2}, {3} );\n" +
                   "vec4 m_Row1 = vec4( {4}, {5}, {6}, {7} );\n" +
                   "vec4 m_Row2 = vec4( {8}, {9}, {10}, {11} );\n" +
                   "vec4 m_Row3 = vec4( {12}, {13}, {14}, {15} );\n" +
                   "output0 = mat4( m_Row0, m_Row1, m_Row2, m_Row3 );\n"
                   ,
                   Row0.x, Row0.y, Row0.z, Row0.w,
                   Row1.x, Row1.y, Row1.z, Row1.w,
                   Row2.x, Row2.y, Row2.z, Row2.w,
                   Row3.x, Row3.y, Row3.z, Row3.w,
                   ShaderNode.ID
                   );

            return GenerateShaderExpression( Text,
                GodotShaderVariableType.GSVT_TRANSFORM, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.MatrixConstructionNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                   "//MatrixConstructionNode:{0}\n" +
                   "output0 = mat4( input0, input1, input2, input3 );\n" +
                   "output1 = output0;\n" +
                   "output2 = output0;\n"
                   ,
                   ShaderNode.ID
                   );

            return GenerateShaderExpression(GodotShaderVariableType.GSVT_VECTOR4, GodotShaderVariableType.GSVT_VECTOR4, GodotShaderVariableType.GSVT_VECTOR4,
                GodotShaderVariableType.GSVT_VECTOR4, Text, GodotShaderVariableType.GSVT_TRANSFORM, GodotShaderVariableType.GSVT_TRANSFORM, GodotShaderVariableType.GSVT_TRANSFORM, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.MatrixSplitNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
            UTGSlot InSlot = ShaderNode.GetSlot("In");
            string Text = string.Format( CultureInfo.InvariantCulture,
                   "//MatrixSplitNode:{0}\n" +
                   "output0 = input0[0].xyzw;\n" +
                   "output1 = input0[1].xyzw;\n" +
                   "output2 = input0[2].xyzw;\n" +
                   "output3 = input0[3].xyzw;\n"
                   , ShaderNode.ID                   
                   );

            return GenerateShaderExpression(GodotShaderVariableType.GSVT_TRANSFORM, Text,
                GodotShaderVariableType.GSVT_VECTOR4, GodotShaderVariableType.GSVT_VECTOR4, GodotShaderVariableType.GSVT_VECTOR4, GodotShaderVariableType.GSVT_VECTOR4,
                ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.MatrixTransposeNode")
        {
            Params += "function = 1\n";
            return "VisualShaderNodeTransformFunc";
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.TransformationMatrixNode")
        {
            object m_MatrixType = GetValueForFieldOrProperty(m_Value, "m_MatrixType", BindingFlags.Instance | BindingFlags.NonPublic);

            string GodotMatrix = "";
            string m_MatrixTypeStr = m_MatrixType.ToString();
            switch(m_MatrixTypeStr)
            {
                default:
                case "Model": GodotMatrix = "output0 = MODEL_MATRIX"; break;
                case "Inverse Model": GodotMatrix = "output0 = inverse( MODEL_MATRIX )"; break;
                case "View": GodotMatrix = "output0 = VIEW_MATRIX"; break;
                case "InverseView": GodotMatrix = "output0 = INV_VIEW_MATRIX"; break;
                case "Projection": GodotMatrix = "output0 = PROJECTION_MATRIX"; break;
                case "InverseProjection": GodotMatrix = "output0 = INV_PROJECTION_MATRIX"; break;
                case "ViewProjection": GodotMatrix = "output0 = VIEW_MATRIX * PROJECTION_MATRIX"; break;
                case "InverseViewProjection": GodotMatrix = "output0 = inverse(VIEW_MATRIX * PROJECTION_MATRIX)"; break;
            }
            
            string Text = string.Format( CultureInfo.InvariantCulture,
                   "//TransformationMatrixNode:{0}\n" +
                   "{1};\n"                   
                   , ShaderNode.ID,
                   GodotMatrix
                   );

            return GenerateShaderExpression( Text,
                GodotShaderVariableType.GSVT_TRANSFORM,
                ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.RectTransformSizeNode")
        {
            object previewSize = GetValueForFieldOrProperty(m_Value, "previewSize", BindingFlags.Instance | BindingFlags.NonPublic);
            object previewScaleFactor = GetValueForFieldOrProperty(m_Value, "previewScaleFactor", BindingFlags.Instance | BindingFlags.NonPublic);
            object previewPixelsPerUnit = GetValueForFieldOrProperty(m_Value, "previewPixelsPerUnit", BindingFlags.Instance | BindingFlags.NonPublic);

            Vector2 previewSizeV2 = (Vector2)previewSize;
            float previewScaleFactorFloat = (float)previewScaleFactor;
            float previewPixelsPerUnitFloat = (float)previewPixelsPerUnit;

            string Text = string.Format( CultureInfo.InvariantCulture,
                   "//RectTransformSizeNode:{0}\n" +
                   "output0 = vec2( {1}, {2} );\n" +
                   "output1 = {3:F1};\n" +
                   "output2 = {4:F1};\n"
                   , ShaderNode.ID,
                   previewSizeV2.x, previewSizeV2.y,
                   previewScaleFactorFloat,
                   previewPixelsPerUnitFloat
                   );

            return GenerateShaderExpression(Text,
                GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT,
                ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.SplitTextureTransformNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
            
            string Text = string.Format( CultureInfo.InvariantCulture,
                   "//SplitTextureTransformNode:{0}\n" +
                   "output0 = vec2( 1.0, 1.0 );\n" +
                   "output1 = vec2( 0.0, 0.0 );\n"
                   //"output2 = input0;\n"
                   , ShaderNode.ID
                   );

            return GenerateShaderExpression(GodotShaderVariableType.GSVT_SAMPLER, Text,
                GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_VECTOR2,
                //GodotShaderVariableType.GSVT_SAMPLER,//Can't output samplers, only input
                ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.BranchOnInputConnectionNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
            UTGSlot InputSlot = ShaderNode.GetSlot("Input");

            bool IsConnected = false;
            object ConnectingSlot = null;
            object PropertyNode = ShaderData.GetConnectingNodeAndSlot( InputSlot.Slot, ref ConnectingSlot );
            if ( PropertyNode != null )
            {
                object m_Property = GetValueForFieldOrProperty(PropertyNode, "m_Property", BindingFlags.Instance | BindingFlags.NonPublic);
                object PropertyValue = GetValueForFieldOrProperty(m_Property, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);
                object displayName = GetValueForFieldOrProperty(PropertyValue, "displayName", BindingFlags.Instance | BindingFlags.NonPublic);                
                string ProperyNameStr = (string)displayName;

                if ( ShaderNode.ParentGraphNode != null )
                {
                    UTGSlot SlotInParentGraph = ShaderNode.ParentGraphNode.GetSlot( ProperyNameStr );
                    IsConnected = SlotInParentGraph.isConnected;
                }
            }
            UTGSlot ConnectedSlot = ShaderNode.GetSlot("Connected");
            UTGSlot NotConnectedSlot = ShaderNode.GetSlot("NotConnected");

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//BranchOnInputConnectionNode:{0}\n" +
                    "if ( {1} )\n\toutput0 = input1;\n" +
                    "else\n\toutput0 = input2;\n"
                    , ShaderNode.ID, IsConnected.ToString().ToLower()
                    );
            return GenerateShaderExpression(InputSlot.GetVariableType(), ConnectedSlot.GetVariableType(), NotConnectedSlot.GetVariableType(), Text,
                ConnectedSlot.GetVariableType(), ref Params, ShaderNode);
        }
        if ( m_TypeStr == "UnityEditor.ShaderGraph.BranchNode" )
        {
            AddConstantNodesForUnconnectedInputs( ShaderData, ShaderNode, CurrentShaderType );
            UTGSlot TrueSlot = ShaderNode.GetSlot("True");
            UTGSlot FalseSlot = ShaderNode.GetSlot("False");

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//BranchNode:{0}\n" +
                    "if ( input0 )\n\toutput0 = input1;\n" +
                    "else\n\toutput0 = input2;\n"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression( GodotShaderVariableType.GSVT_BOOL, TrueSlot.GetVariableType(), FalseSlot.GetVariableType(), Text,
                TrueSlot.GetVariableType(), ref Params, ShaderNode );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.SubGraphNode" )
        {
            AddConstantNodesForUnconnectedInputs( ShaderData, ShaderNode, CurrentShaderType );

            object Subgraph = GetValueForFieldOrProperty(m_Value, "asset", BindingFlags.Instance | BindingFlags.NonPublic);
            if( Subgraph != null )
            {
                object PropertyIds = GetValueForFieldOrProperty(m_Value, "m_PropertyIds", BindingFlags.Instance | BindingFlags.NonPublic);
                object PropertyGuids = GetValueForFieldOrProperty(m_Value, "m_PropertyGuids", BindingFlags.Instance | BindingFlags.NonPublic);

                object inputCapabilities = GetValueForFieldOrProperty(Subgraph, "inputCapabilities", BindingFlags.Instance | BindingFlags.Public);
                object outputCapabilities = GetValueForFieldOrProperty(Subgraph, "outputCapabilities", BindingFlags.Instance | BindingFlags.Public);
                object inputDependencies = GetValueForFieldOrProperty(Subgraph, "m_InputDependencies", BindingFlags.Instance | BindingFlags.NonPublic);
                object SubgraphAssetGUID = GetValueForFieldOrProperty(Subgraph, "assetGuid", BindingFlags.Instance | BindingFlags.Public);
                object inputs = GetValueForFieldOrProperty(Subgraph, "inputs", BindingFlags.Instance | BindingFlags.Public);
                object inputs_mList = GetValueForFieldOrProperty(inputs, "m_List", BindingFlags.Instance | BindingFlags.NonPublic);
                List<object> InputsList = GetList(inputs_mList);

                object outputs = GetValueForFieldOrProperty(Subgraph, "outputs", BindingFlags.Instance | BindingFlags.Public);
                object outputs_mList = GetValueForFieldOrProperty(outputs, "m_List", BindingFlags.Instance | BindingFlags.NonPublic);
                List<object> OutputsList = GetList(outputs_mList);

                List<object> DependencyList = GetList(inputDependencies);
                for( int i = 0; i < DependencyList.Count; i++ )
                {
                    object Key = GetValueForFieldOrProperty(DependencyList[i], "Key", BindingFlags.Instance | BindingFlags.NonPublic);
                    object DepValue = GetValueForFieldOrProperty(DependencyList[i], "Value", BindingFlags.Instance | BindingFlags.NonPublic);
                }

                string SubgraphPath = AssetDatabase.GUIDToAssetPath(SubgraphAssetGUID.ToString());

                var SubGraphObject = GetGraphFromPath(SubgraphPath);
                ProcessGraphObject( SubGraphObject, ShaderNode, ShaderData );
            }
            else
            {
                Debug.LogError( "Subgraph wasn't found in " + ShaderData.FilePath );
            }

            return GenerateRerouteNode(GodotShaderVariableType.GSVT_VECTOR4, ref Params);
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.RotateAboutAxisNode" )
        {
            AddConstantNodesForUnconnectedInputs( ShaderData, ShaderNode, CurrentShaderType );

            object Unit = GetValueForFieldOrProperty(m_Value, "m_Unit", BindingFlags.Instance | BindingFlags.NonPublic);
            string DegreeToRadians = "";
            if( Unit.ToString() == "Degrees" )
            {
                DegreeToRadians = "input2 = radians(input2);\n";
            }
            //Unity_Rotate_About_Axis_Degrees_float(vec3 In, vec3 Axis, float Rotation, out vec3 Out)
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//RotateNode:{0}:{1}\n" +
                    DegreeToRadians +
                    "Unity_Rotate_About_Axis_Degrees_float( input0, input1, input2, output0 );\n"
                    , ShaderNode.ID, ShaderNode.GetParentGraphName()
                    );
            return GenerateShaderExpression( GodotShaderVariableType.GSVT_VECTOR3, GodotShaderVariableType.GSVT_VECTOR3,
                GodotShaderVariableType.GSVT_FLOAT, Text, GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.RotateNode" )
        {
            AddConstantNodesForUnconnectedInputs( ShaderData, ShaderNode, CurrentShaderType );

            object Unit = GetValueForFieldOrProperty(m_Value, "m_Unit", BindingFlags.Instance | BindingFlags.NonPublic);
            string DegreeToRadians = "";
            if( Unit.ToString() == "Degrees" )
            {
                DegreeToRadians = "input2 = input2 * (3.1415926f/180.0f);\n";
            }

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//RotateNode:{0}:{1}\n" +
                    DegreeToRadians +
                    "Unity_Rotate_Radians_float( input0, input1, input2, output0 );\n"
                    , ShaderNode.ID, ShaderNode.GetParentGraphName()
                    );
            return GenerateShaderExpression( GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_VECTOR2,
                GodotShaderVariableType.GSVT_FLOAT, Text, GodotShaderVariableType.GSVT_VECTOR2, ref Params, ShaderNode );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.CombineNode" )
        {
            AddConstantNodesForUnconnectedInputs( ShaderData, ShaderNode, CurrentShaderType );

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//CombineNode:{0}\n" +
                    "output0 = vec4( input0, input1, input2, input3 );\n" +
                    "output1 = vec3( input0, input1, input2 );\n" +
                    "output2 = vec2( input0, input1 );\n"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression( GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT,
                GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, Text, GodotShaderVariableType.GSVT_VECTOR4,
                GodotShaderVariableType.GSVT_VECTOR3, GodotShaderVariableType.GSVT_VECTOR2, ref Params, ShaderNode );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.TilingAndOffsetNode" )
        {
            AddConstantNodesForUnconnectedInputs( ShaderData, ShaderNode, CurrentShaderType );

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//TilingAndOffsetNode:{0}\n" +
                    "output0 = input0 * input1 + input2;\n"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression( GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_VECTOR2,
                GodotShaderVariableType.GSVT_VECTOR2, Text, GodotShaderVariableType.GSVT_VECTOR2, ref Params, ShaderNode );
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.LengthNode" )
        {
            AddConstantNodesForUnconnectedInputs( ShaderData, ShaderNode, CurrentShaderType );

            UTGSlot InSlot = ShaderNode.GetSlot("In");
            GodotShaderVariableType_VectorOp OpType = (GodotShaderVariableType_VectorOp)((int)InSlot.GetVariableType() - (int)GodotShaderVariableType.GSVT_VECTOR2);

            string DefaultInputs = GenerateDefaultInputValues(InSlot.GetVariableType(), InSlot.Vec4 );
            string Text = string.Format( CultureInfo.InvariantCulture,
                      "{0}\n" +
                      "op_type = {1}\n"
                      , DefaultInputs,
                      (int)OpType );

            Params = Text;

            return "VisualShaderNodeVectorLen";
        }
        if( m_TypeStr == "UnityEditor.ShaderGraph.DDXNode" || m_TypeStr == "UnityEditor.ShaderGraph.DDYNode" ||
            m_TypeStr == "UnityEditor.ShaderGraph.DDXYNode" )
        {
            AddConstantNodesForUnconnectedInputs( ShaderData, ShaderNode, CurrentShaderType );
            UTGSlot InSlot = ShaderNode.GetSlot("In");
            GodotShaderVariableType_DerivateFunc InputTypeDerivate = GetVariableType_DerivateFunc( InSlot.GetVariableType() );

            int FunctionID = 1;
            if( m_TypeStr == "UnityEditor.ShaderGraph.DDYNode" )
                FunctionID = 2;
            if (m_TypeStr == "UnityEditor.ShaderGraph.DDXYNode")
                FunctionID = 0;
            string DefaultInputs = GenerateDefaultInputValues(InSlot.GetVariableType(), InSlot.Vec4);
            string Text = string.Format( CultureInfo.InvariantCulture,
                     "{0}\n" +
                      "op_type = {1}\n" +
                      "function = {2}\n",
                     DefaultInputs,
                      (int)InputTypeDerivate,
                      FunctionID
            );

            Params += Text;
            return "VisualShaderNodeDerivativeFunc";
        }
        if ( m_TypeStr == "UnityEditor.ShaderGraph.NormalBlendNode" )
        {
            AddConstantNodesForUnconnectedInputs( ShaderData, ShaderNode, CurrentShaderType );

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//NormalBlendNode:{0}\n" +
                    "output0 = normalize( vec3( input0.rg + input1.rg, input0.b * input1.b ));\n"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression( GodotShaderVariableType.GSVT_VECTOR3, GodotShaderVariableType.GSVT_VECTOR3, Text,
                GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode );
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.PolarCoordinatesNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            return "VisualShaderNodeUVPolarCoord";
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.NormalReconstructZNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//NormalReconstructZNode:{0}\n" +
                    "float reconstructZ = sqrt(1.0 - saturate(dot(input0.xy, input0.xy)));\r\n" +
                    "vec3 normalVector = vec3(input0.x, input0.y, reconstructZ);\r\n" +
                    "output0 = normalize(normalVector);\n"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_VECTOR2,Text,
                GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.MainLightDirectionNode")
        {
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//MainLightDirectionNode:{0}\n" +
                    "output0 = vec3(0,-1,0);\n"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression( Text,
                GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.SceneColorNode")
        {
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//SceneColorNode:{0}\n" +
                    "output0 = vec4(0,0,0,1.0);\n"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression( GodotShaderVariableType.GSVT_VECTOR4, Text,
                GodotShaderVariableType.GSVT_VECTOR4, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.SceneDepthNode")
        {
            return "VisualShaderNodeLinearSceneDepth";
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.SceneDepthDifferenceNode")
        {
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//SceneDepthDifferenceNode:{0}\n" +
                    "output0 = vec4(1,1,1,1);\n"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_VECTOR4, GodotShaderVariableType.GSVT_VECTOR3, Text,
                GodotShaderVariableType.GSVT_VECTOR4, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.BakedGINode")
        {
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//{1}:{0}\n" +
                    "output0 = vec3(1,1,1);\n"
                    , ShaderNode.ID, m_TypeStr
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_VECTOR3, GodotShaderVariableType.GSVT_VECTOR3, GodotShaderVariableType.GSVT_VECTOR2,
                GodotShaderVariableType.GSVT_VECTOR2, Text, GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.ScreenNode")
        {
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//ScreenNode:{0}\n" +
                    "output0 = VIEWPORT_SIZE.x;\n" +
                    "output1 = VIEWPORT_SIZE.y;\n"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression( Text, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.ScreenPositionNode")
        {
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//ScreenPositionNode:{0}\n" +
                    "output0 = FRAGCOORD;\n" +
                    "float depth = FRAGCOORD.z;\n" +
                    "float linear_depth = PROJECTION_MATRIX[3][2] / \n" +
                    "            (depth - PROJECTION_MATRIX[2][2]);\n" +
                    "output0.w = linear_depth;\n"
                    //"output0.xy = SCREEN_UV;\n" +
                    //"output0.zw = FRAGCOORD.zw;\n"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression( Text, GodotShaderVariableType.GSVT_VECTOR4, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.SliderNode")
        {
            object SliderValue = GetValueForFieldOrProperty(m_Value, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);
            Vector3 SliderVector = (Vector3)SliderValue;
            return GenerateConstantNode(SliderVector.x, ref Params);            
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.ViewVectorNode")
        {
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//ViewVectorNode:{0}\n" +
                    //"output0 = VIEW;\n"
                    "vec3 WorldPosition = (INV_VIEW_MATRIX * vec4(VERTEX,1.0) ).xyz;\r\n" +
                    "output0 = CAMERA_POSITION_WORLD - WorldPosition;"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression(Text, GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.PolygonNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//PolygonNode:{0}\n" +
                    
                    "float Sides = input1;\n" +
                    "float Width = input2;\n" +
                    "float Height = input3;\n" +
                    "float pi = 3.14159265359;\r\n" +
                    "            float aWidth = Width * cos(pi / Sides);\r\n" +
                    "            float aHeight = Height * cos(pi / Sides);\r\n" +
                    "            vec2 uv = (input0 * 2.0 - 1.0) / vec2(aWidth, aHeight);\r\n" +
                    "            uv.y *= -1.0;\r\n            float pCoord = atan(uv.x, uv.y);\r\n" +
                    "            float r = 2.0 * pi / Sides;\r\n" +
                    "            float distance = cos(floor(0.5 + pCoord / r) * r - pCoord) * length(uv);\r\n" +
                    "        \r\n" +
                    "        #if defined(SHADER_STAGE_RAY_TRACING)\r\n" +
                    "            output0 = saturate((1.0 - distance) * 1e7);\r\n" +
                    "        #else\r\n" +
                    "            output0 = saturate((1.0 - distance) / fwidth(distance));\r\n" +
                    "        #endif"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT,
                GodotShaderVariableType.GSVT_FLOAT, Text, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.RoundedPolygonNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//RoundedPolygonNode:{0}\n" +
                    "float Sides = input3;\n" +
                    "float Width = input1;\n" +
                    "float Height = input2;\n" +
                    "float pi = 3.14159265359;\r\n" +
                    "float HALF_PI = pi/2.0;\r\n" +
                    "vec2 uv = input0;\r\n" +
                    "float Roundness = input4;\n" +

                    "            uv = uv * 2. + vec2(-1.,-1.);\r\n" +
                    "            float epsilon = 1e-6;\r\n" +
                    "            uv.x = uv.x / ( Width + float(Width==0.0)*epsilon);\r\n" +
                    "            uv.y = uv.y / ( Height + float(Height==0.0)*epsilon);\r\n" +
                    "            Roundness = clamp(Roundness, 1e-6, 1.);\r\n" +
                    "            float i_sides = floor( abs( Sides ) );\r\n" +
                    "           float fullAngle = 2. * PI / i_sides;\r\n" +
                    "            float halfAngle = fullAngle / 2.;\r\n" +
                    "            float opositeAngle = HALF_PI - halfAngle;\r\n" +
                    "            float diagonal = 1. / cos( halfAngle );\r\n" +
                    "            // Chamfer values\r\n" +
                    "            float chamferAngle = Roundness * halfAngle; // Angle taken by the chamfer\r\n" +
                    "            float remainingAngle = halfAngle - chamferAngle; // Angle that remains\r\n" +
                    "            float ratio = tan(remainingAngle) / tan(halfAngle); // This is the ratio between the length of the polygon's triangle and the distance of the chamfer center to the polygon center\r\n" +
                    "            // Center of the chamfer arc\r\n" +
                    "            vec2 chamferCenter = vec2(\r\n" +
                    "                cos(halfAngle) ,\r\n" +
                    "                sin(halfAngle)\r\n" +
                    "            )* ratio * diagonal;\r\n" +
                    "            // starting of the chamfer arc\r\n" +
                    "            vec2 chamferOrigin = vec2(\r\n" +
                    "               1.,\r\n" +
                    "                tan(remainingAngle)\r\n" +
                    "            );\r\n" +
                    "            // Using Al Kashi algebra, we determine:\r\n" +
                    "            // The distance distance of the center of the chamfer to the center of the polygon (side A)\r\n" +
                    "            float distA = length(chamferCenter);\r\n            // The radius of the chamfer (side B)\r\n" +
                    "            float distB = 1. - chamferCenter.x;\r\n            // The refence length of side C, which is the distance to the chamfer start\r\n" +
                    "            float distCref = length(chamferOrigin);\r\n            // This will rescale the chamfered polygon to fit the uv space\r\n" +
                    "            // diagonal = length(chamferCenter) + distB;\r\n" +
                    "            float uvScale = diagonal;\r\n" +
                    "            uv *= uvScale;\r\n" +
                    "            vec2 polaruv = vec2 (\r\n" +
                    "                atan( uv.y, uv.x ),\r\n" +
                    "                length(uv)\r\n" +
                    "            );\r\n" +
                    "            polaruv.x += HALF_PI + 2.0*PI;\r\n" +
                    "            polaruv.x = mod( polaruv.x + halfAngle, fullAngle );\r\n" +
                    "            polaruv.x = abs(polaruv.x - halfAngle);\r\n" +
                    "            uv = vec2( cos(polaruv.x), sin(polaruv.x) ) * polaruv.y;\r\n" +
                    "            // Calculate the angle needed for the Al Kashi algebra\r\n" +
                    "            float angleRatio = 1. - (polaruv.x-remainingAngle) / chamferAngle;\r\n" +
                    "            // Calculate the distance of the polygon center to the chamfer extremity\r\n" +
                    "            float distC = sqrt( distA*distA + distB*distB - 2.*distA*distB*cos( PI - halfAngle * angleRatio ) );\r\n" +
                    "            output0 = uv.x;\r\n" +
                    "            float chamferZone = float( ( halfAngle - polaruv.x ) < chamferAngle );\r\n" +
                    "            output0 = mix( uv.x, polaruv.y / distC, chamferZone );\r\n" +
                    "            // Output this to have the shape mask instead of the distance field\r\n" +
                    "        #if defined(SHADER_STAGE_RAY_TRACING)\r\n" +
                    "            output0 = saturate((1.0 - output0) * 1e7);\r\n" +
                    "        #else\n" +
                    "            output0 = saturate((1.0 - output0) / fwidth(output0));\r\n" +
                    "        #endif"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT,
                GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, Text, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.RectangleNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//RectangleNode:{0}\n" +

                    "vec2 uv = input0;\n" +
                    "float Width = input1;\n" +
                    "float Height = input2;\n" +

                    "vec2 d = abs(uv * 2.0 - 1.0 ) - vec2(Width, Height);\r\n" +
                    "            d = saturate2( vec2(1.0,1.0) - d / fwidth(d));\r\n " +
                    "            output0 = min(d.x, d.y);"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT,
                 Text, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.RoundedRectangleNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//RoundedRectangleNode:{0}\n" +

                    "vec2 uv = input0;\n" +
                    "float Width = input1;\n" +
                    "float Height = input2;\n" +
                    "float Radius = input3;\n" +

                    "Radius = max(min(min(abs(Radius * 2.0), abs(Width)), abs(Height)), 1e-5);\r\n" +
                    "            uv = abs(uv * 2.0 - 1.0 ) - vec2(Width, Height) + Radius;\r\n" +
                    "            float d = length(max( vec2(0.0,0.0), uv)) / Radius;\r\n" +
                 
                    "            float fwd = max(fwidth(d), 1e-5);\r\n" +
                    "            output0 = saturate((1.0 - d) / fwd);\r\n"
                  
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT,
                 Text, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.SpherizeNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//SpherizeNode:{0}\n" +
                    "            vec2 uv = input0;\r\n" +
                    "            vec2 Center = input1;\r\n" +
                    "            vec2 Strength = input2;\r\n" +
                    "            vec2 Offset = input3;\r\n" +

                    "            vec2 delta = uv - Center;\r\n" +
                    "            float delta2 = dot(delta.xy, delta.xy);\r\n" +
                    "            float delta4 = delta2 * delta2;\r\n" +
                    "            vec2 delta_offset = delta4 * Strength;\r\n" +
                    "            output0 = uv + delta * delta_offset + Offset;\r\n"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_VECTOR2,
                GodotShaderVariableType.GSVT_VECTOR2, Text, GodotShaderVariableType.GSVT_VECTOR2, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.BitangentVectorNode")
        {
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//BitangentVectorNode:{0}\n" +
                    "vec3 WorldTangent  = ( vec4( TANGENT, 0.0) * INV_VIEW_MATRIX ).xyz;\n" +
                    "vec3 WorldNormal   = ( vec4( NORMAL, 0.0) * INV_VIEW_MATRIX ).xyz;\n" +
                    "vec3 bitang = 1 * cross( WorldNormal.xyz, WorldTangent.xyz);\n" +
                    "output0 = bitang;\n"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression( Text, GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.ContrastNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//ContrastNode:{0}\n" +
                   "float midpoint = pow(0.5, 2.2);\r\n" +
                   "output0 =  (input0 - midpoint) * input1 + midpoint;"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_VECTOR3, GodotShaderVariableType.GSVT_FLOAT, Text, GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.FlipNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
            object m_AlphaChannel = GetValueForFieldOrProperty(m_Value, "m_AlphaChannel", BindingFlags.Instance | BindingFlags.NonPublic);
            object m_BlueChannel  = GetValueForFieldOrProperty(m_Value, "m_BlueChannel", BindingFlags.Instance | BindingFlags.NonPublic);
            object m_GreenChannel = GetValueForFieldOrProperty(m_Value, "m_GreenChannel", BindingFlags.Instance | BindingFlags.NonPublic);
            object m_RedChannel   = GetValueForFieldOrProperty(m_Value, "m_RedChannel", BindingFlags.Instance | BindingFlags.NonPublic);

            UTGSlot In = ShaderNode.GetSlot("In");
            string Suffix = GenerateSuffix(In.GetVariableType());

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//FlipNode:{0}\n" +
                    "vec4 Flip = vec4( {1}, {2}, {3}, {4});\n" +
                   "output0 = ( Flip{5} * -2.0 + 1.0 ) * input0;"
                    , ShaderNode.ID, Convert.ToInt32( (bool)m_RedChannel), Convert.ToInt32( (bool)m_GreenChannel ),
                    Convert.ToInt32( (bool)m_BlueChannel ), Convert.ToInt32( (bool)m_AlphaChannel ), Suffix 
                    );
            return GenerateShaderExpression(In.GetVariableType(), Text, In.GetVariableType(), ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.InvertColorsNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
            object m_AlphaChannel = GetValueForFieldOrProperty(m_Value, "m_AlphaChannel", BindingFlags.Instance | BindingFlags.NonPublic);
            object m_BlueChannel = GetValueForFieldOrProperty(m_Value, "m_BlueChannel", BindingFlags.Instance | BindingFlags.NonPublic);
            object m_GreenChannel = GetValueForFieldOrProperty(m_Value, "m_GreenChannel", BindingFlags.Instance | BindingFlags.NonPublic);
            object m_RedChannel = GetValueForFieldOrProperty(m_Value, "m_RedChannel", BindingFlags.Instance | BindingFlags.NonPublic);

            UTGSlot In = ShaderNode.GetSlot("In");
            string Suffix = GenerateSuffix(In.GetVariableType());

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//InvertColorsNode:{0}\n" +
                    "vec4 InvertColors = vec4( {1}, {2}, {3}, {4});\n" +
                    "output0 = abs(InvertColors{5} - input0 );"
                    , ShaderNode.ID, Convert.ToInt32((bool)m_RedChannel), Convert.ToInt32((bool)m_GreenChannel),
                    Convert.ToInt32((bool)m_BlueChannel), Convert.ToInt32((bool)m_AlphaChannel), Suffix
                    );
            return GenerateShaderExpression(In.GetVariableType(), Text, In.GetVariableType(), ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.PosterizeNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
            UTGSlot In = ShaderNode.GetSlot("In");
            UTGSlot Steps = ShaderNode.GetSlot("Steps");
            string Suffix = GenerateSuffix(In.GetVariableType());

            string TypeName = GetDataTypeName( In.GetVariableType(), GetDataTypeMode.GDTM_FOR_GLSL );
            string OneStr = string.Format( CultureInfo.InvariantCulture,"{0}(1.0)", TypeName );

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//PosterizeNode:{0}\n" +
                    //"vec4 In4 = input0;\n" +
                    //"vec4 Steps4 = input1;\n" +
                    "output0 = floor( input0 / ( {1} / input1)) * ( {1} / input1);"
                    , ShaderNode.ID,
                    OneStr
                    );
            return GenerateShaderExpression(In.GetVariableType(), In.GetVariableType(), Text, In.GetVariableType(), ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.SphereMaskNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
            UTGSlot In = ShaderNode.GetSlot("Coords");
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//SphereMaskNode:{0}\n" +
                    "output0 = 1.0 - saturate((distance(input0, input1) - input2) / (1.0 - input3));"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression(In.GetVariableType(), In.GetVariableType(), GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT,
                Text, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.TriangleWaveNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
            UTGSlot In = ShaderNode.GetSlot("In");

            string Suffix = GenerateSuffix( In.GetVariableType());

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//TriangleWaveNode:{0}\n" +
                    "vec4 In4 = input0;//vec4( input0.x, input0.y, input0.z, input0.w);\n" +
                    "vec4 Out = vec4(2.0,2.0,2.0,2.0) * abs( vec4(2.0,2.0,2.0,2.0) * ( In4 - floor( vec4(0.5,0.5,0.5,0.5) + In4)) ) - vec4( 1.0,1.0,1.0,1.0);\n" +
                    "output0 = Out{1};\n"
                   , ShaderNode.ID,                    
                    Suffix
                    );
            return GenerateShaderExpression(In.GetVariableType(), Text, In.GetVariableType(), ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.SquareWaveNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
            UTGSlot In = ShaderNode.GetSlot("In");

            string Suffix = GenerateSuffix(In.GetVariableType());

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//SquareWaveNode:{0}\n" +
                    "vec4 In4 = input0;//vec4( input0.x, input0.y, input0.z, input0.w);\n" +
                    "vec4 Out = vec4( 1.0,1.0,1.0,1.0 ) - vec4( 2.0,2.0,2.0,2.0) * round(fract(In4));\n" +
                    "output0 = Out{5};\n"
                   , ShaderNode.ID,
                    In.Vec4.x, In.Vec4.y, In.Vec4.z, In.Vec4.w,
                    Suffix
                    );
            return GenerateShaderExpression(In.GetVariableType(), Text, In.GetVariableType(), ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.SawtoothWaveNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
            UTGSlot In = ShaderNode.GetSlot("In");

            string Suffix = GenerateSuffix(In.GetVariableType());

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//SawtoothWaveNode:{0}\n" +
                    "vec4 In4 = input0;//vec4( input0.x, input0.y, input0.z, input0.w);\n" +
                    "vec4 Out = vec4(2.0,2.0,2.0,2.0) * (In4 - floor( vec4(0.5,0.5,0.5,0.5) + In4));\n" +
                    "output0 = Out{5};\n"
                   , ShaderNode.ID,
                    In.Vec4.x, In.Vec4.y, In.Vec4.z, In.Vec4.w,
                    Suffix
                    );
            return GenerateShaderExpression(In.GetVariableType(), Text, In.GetVariableType(), ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.NoiseSineWaveNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
            UTGSlot In = ShaderNode.GetSlot("In");

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//NoiseSineWaveNode:{0}\n" +
                    "vec4 In4 = input0;\n" +
                    "float sinIn = sin(In);\r\n" +
                    "float sinInOffset = sin(In + 1.0);\r\n" +
                    "float randomno =  fract(sin((sinIn - sinInOffset) * (12.9898 + 78.233))*43758.5453);\r\n" +
                    "float noise = mix(MinMax.x, MinMax.y, randomno);\r\n" +
                    "float Out = sinIn + noise;\n" +
                    "output0 = Out;\n"
                   , ShaderNode.ID
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_FLOAT, Text, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.EllipseNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
            
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//EllipseNode:{0}\n" +
                    "float d = length((input0.xy * vec2(2.0,2.0) - vec2(1.0,1.0) ) / vec2(input1, input2));\r\n" +
                    "output0 = saturate((1.0 - d) / fwidth(d));\n"
                   , ShaderNode.ID
                    );
            return GenerateShaderExpression( GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, 
                Text, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.DitherNode")
        {
            AddConstantNodesForUnconnectedInputs( ShaderData, ShaderNode, CurrentShaderType );

            string UV = "vec2 uv = SCREEN_UV.xy;\r\n";
            UTGSlot In = ShaderNode.GetSlot("In");
            if ( In.isConnected )
                UV = "vec2 uv = input1.xy;\r\n";

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//DitherNode:{0}\n" +
                    "{1}" +
                    "float DITHER_THRESHOLDS[16] =\r\n" +
                    "{{\r\n" +
                    "                1.0 / 17.0,  9.0 / 17.0,  3.0 / 17.0, 11.0 / 17.0,\r\n" +
                    "                13.0 / 17.0,  5.0 / 17.0, 15.0 / 17.0,  7.0 / 17.0,\r\n" +
                    "                4.0 / 17.0, 12.0 / 17.0,  2.0 / 17.0, 10.0 / 17.0,\r\n" +
                    "                16.0 / 17.0,  8.0 / 17.0, 14.0 / 17.0,  6.0 / 17.0\r\n" +
                    "}};\r\n" +
                    "            uint index = (uint(uv.x) % 4u) * 4u + uint(uv.y) % 4u;\r\n" +
                    "            output0 = input0 - DITHER_THRESHOLDS[index];"
                   , ShaderNode.ID, UV
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_VECTOR2,
                Text, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode);
        }

        if (m_TypeStr == "UnityEditor.ShaderGraph.NormalUnpackNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//NormalUnpackNode:{0}\n" +
                   "// This do the trick\r\n" +
                   "    input0.x *= input0.w;\r\n\r\n" +
                   "    vec3 normal;\r\n" +
                   "    normal.xy = input0.xy * vec2(2.0,2.0) - vec2(1.0, 1.0);\r\n" +
                   "   normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy)));\r\n" +
                   "    output0 = normal;"
                   , ShaderNode.ID
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_VECTOR4,
                Text, GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.MeterValueNode")
        {
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//{1}:{0}\n" +
                    "output0 = 0.5;\n"
                    , ShaderNode.ID, m_TypeStr
                    );
            return GenerateShaderExpression( Text, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.SelectableStateNode")
        {
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//{1}:{0}\n" +
                    "output0 = 0.0;\n"
                    , ShaderNode.ID, m_TypeStr
                    );
            return GenerateShaderExpression(Text, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.ToggleStateNode")
        {
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//{1}:{0}\n" +
                    "output0 = false;\n"
                    , ShaderNode.ID, m_TypeStr
                    );
            return GenerateShaderExpression(Text, GodotShaderVariableType.GSVT_BOOL, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.SliderValueNode")
        {
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//{1}:{0}\n" +
                    "output0 = 0.0;\n" +
                    "output1 = vec2( 0.0, 0.0 );\n"
                    , ShaderNode.ID, m_TypeStr
                    );
            return GenerateShaderExpression(Text, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_VECTOR2, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.RangeBarNode")
        {
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//{1}:{0}\n" +
                    "output0 = 0.0;\n" +
                    "output1 = 0.0;\n" +
                    "output2 = vec2( 0.0, 0.0 );\n"
                    , ShaderNode.ID, m_TypeStr
                    );
            return GenerateShaderExpression(Text, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_VECTOR2, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.EyeIndexNode")
        {
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//{1}:{0}\n" +
                    "output0 = 0;\n"
                    , ShaderNode.ID, m_TypeStr
                    );
            return GenerateShaderExpression(Text, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.MetalReflectanceNode")
        {
            object Material = GetValueForFieldOrProperty(m_Value, "m_Material", BindingFlags.Instance | BindingFlags.NonPublic);
            string MaterialStr = Material.ToString();

            string ValStr = "";
            switch (MaterialStr)
            {
                default:
                case "Iron":        ValStr = "vec3(0.560, 0.570, 0.580);"; break;
                case "Silver":      ValStr = "vec3(0.972, 0.960, 0.915);"; break;
                case "Aluminium":   ValStr = "vec3(0.913, 0.921, 0.925);"; break;
                case "Gold"     :   ValStr = "vec3(1.000, 0.766, 0.336);"; break;
                case "Copper":      ValStr = "vec3(0.955, 0.637, 0.538);"; break;
                case "Chromium":    ValStr = "vec3(0.550, 0.556, 0.554);"; break;
                case "Nickel":      ValStr = "vec3(0.660, 0.609, 0.526);"; break;
                case "Titanium":    ValStr = "vec3(0.542, 0.497, 0.449);"; break;
                case "Cobalt":      ValStr = "vec3(0.662, 0.655, 0.634);"; break;
                case "Platinum":    ValStr = "vec3(0.672, 0.637, 0.585);"; break;
            }
              
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//{1}:{0}\n" +
                    "output0 = {2}\n"
                    , ShaderNode.ID, m_TypeStr, ValStr
                    );
            return GenerateShaderExpression(Text, GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.DielectricSpecularNode")
        {
            object Material = GetValueForFieldOrProperty(m_Value, "m_Material", BindingFlags.Instance | BindingFlags.NonPublic);
            object MaterialType = GetValueForFieldOrProperty( Material, "type", BindingFlags.Instance | BindingFlags.Public);

            object Range = GetValueForFieldOrProperty(Material, "range", BindingFlags.Instance | BindingFlags.Public);
            object IOR = GetValueForFieldOrProperty(Material, "indexOfRefraction", BindingFlags.Instance | BindingFlags.Public);
            string MaterialTypeStr = MaterialType.ToString();

            string ValStr = "";
            switch (MaterialTypeStr)
            {
                default:
                case "Common": ValStr = string.Format( CultureInfo.InvariantCulture,"mix(0.034, 0.048, {0:F2});", Range ); break;
                case "Custom": ValStr = string.Format( CultureInfo.InvariantCulture,"pow( {0:F2} - 1.0, 2.0) / pow( {0:F2} + 1.0, 2.0);", IOR ); break;

                case "RustedMetal"  : ValStr = "0.030"; break;
                case "Water"        : ValStr = "0.020"; break;
                case "Ice"          : ValStr = "0.018"; break;
                case "Glass"        : ValStr = "0.040"; break;
            }

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//{1}:{0}\n" +
                    "output0 = {2}\n"
                    , ShaderNode.ID, m_TypeStr, ValStr
                    );
            return GenerateShaderExpression(Text, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.ReplaceColorNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//{1}:{0}\n" +
                    "vec3 In    = input0;\n" +
                    "vec3 From  = input1;\n" +
                    "vec3 To    = input2;\n" +
                    "float Range = input3;\n" +
                    "float Fuzziness = input4;\n" +

                    "float Distance = distance(From, In);\r\n" +
                    "output0 = mix(To, input0, saturate((Distance - Range) / max(Fuzziness, 1e-5f)));\n"
                    , ShaderNode.ID, m_TypeStr
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_VECTOR3, GodotShaderVariableType.GSVT_VECTOR3, GodotShaderVariableType.GSVT_VECTOR3,
                GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, Text, GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.ColorMaskNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//{1}:{0}\n" +
                    "vec3 In    = input0;\n" +
                    "vec3 MaskColor  = input1;\n" +
                    "float Range = input2;\n" +
                    "float Fuzziness = input3;\n" +

                    "float Distance = distance(MaskColor, In);\r\n" +
                    "output0 = saturate(1.0 - (Distance - Range) / max(Fuzziness, 1e-5));\n"
                    , ShaderNode.ID, m_TypeStr
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_VECTOR3, GodotShaderVariableType.GSVT_VECTOR3,
                GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, Text, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.FadeTransitionNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//{1}:{0}\n" +
                    "output0 = saturate(input1*(input2+1.0)+(input0-1.0)*input2);"
                    , ShaderNode.ID, m_TypeStr
                    );
            return GenerateShaderExpression( GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT,
                 Text, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode );
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.RadialShearNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);
            UTGSlot In = ShaderNode.GetSlot("Coords");
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//RadialShearNode:{0}\n" +
                    " vec2 uv = input0;\r\n" +
                    " vec2 Center = input1;\r\n" +
                    " vec2 Strength = input2;\r\n" +
                    " vec2 Offset = input3;\r\n" +

                    " vec2 delta = uv - Center;\r\n" +
                    "            float delta2 = dot(delta.xy, delta.xy);\r\n" +
                    "            vec2 delta_offset = delta2 * Strength;\r\n" +
                    "            output0 = uv + vec2(delta.y, -delta.x) * delta_offset + Offset;\n"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_VECTOR2,
                Text, GodotShaderVariableType.GSVT_VECTOR2, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.ChannelMaskNode")
        {
            object m_ChannelMask = GetValueForFieldOrProperty(m_Value, "m_ChannelMask", BindingFlags.Instance | BindingFlags.NonPublic);
            int m_ChannelMaskInt = (int)m_ChannelMask;
            bool red = (m_ChannelMaskInt & 1) != 0;
            bool green = (m_ChannelMaskInt & 2) != 0;
            bool blue = (m_ChannelMaskInt & 4) != 0;
            bool alpha = (m_ChannelMaskInt & 8) != 0;

            UTGSlot In = ShaderNode.GetSlot("In");
            string Suffix = GenerateSuffix(In.GetVariableType());

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//ChannelMaskNode:{0}\n" +
                     "vec4 Mask{0} = vec4( {1}, {2}, {3}, {4});\n" +
                     "output0 = input0 * Mask{0}{5};\n"
                    , ShaderNode.ID,
                    Convert.ToInt32(red), Convert.ToInt32(green), Convert.ToInt32(blue), Convert.ToInt32(alpha),
                    Suffix
                    );
            return GenerateShaderExpression(In.GetVariableType(), Text, In.GetVariableType(), ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.DegreesToRadiansNode")
        {
            UTGSlot In = ShaderNode.GetSlot("In");
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//DegreesToRadiansNode:{0}\n" +
                     "output0 = radians( input0 );\n"
                    , ShaderNode.ID                    
                    );
            return GenerateShaderExpression(In.GetVariableType(), Text, In.GetVariableType(), ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.VoronoiNode")
        {
            AddConstantNodesForUnconnectedInputs( ShaderData, ShaderNode, CurrentShaderType );

            object HashType = GetValueForFieldOrProperty(m_Value, "m_HashType", BindingFlags.Instance | BindingFlags.NonPublic );
            string Text = "";
            string FunctionType = "Unity_Voronoi_RandomVector_Deterministic_float";
            if (HashType.ToString() == "LegacySine")
            {
                FunctionType = "Unity_Voronoi_RandomVector_LegacySine_float";
            }
            Text = string.Format( CultureInfo.InvariantCulture,
                    "//VoronoiNode:{0}\n" +
                    "vec2 uv = input0;\n" +                        
                    "float AngleOffset = input1;\n" +
                    "float CellDensity = input2;\n" +

                        "            vec2 g = floor(uv * CellDensity);\r\n" +
                        "            vec2 f = fract(uv * CellDensity);\r\n" +
                        "            float t = 8.0;\r\n" +
                        "            vec3 res = vec3(8.0, 0.0, 0.0);\r\n" +
                        "           for (int y = -1; y <= 1; y++)\r\n" +
                        "            {{\r\n" +
                        "                for (int x = -1; x <= 1; x++)\r\n" +
                        "                {{\r\n" +
                        "                    vec2 lattice = vec2( float(x), float(y));\r\n" +
                        "                    vec2 offset = {1}(lattice + g, AngleOffset);\r\n" +
                        "                    float d = distance(lattice + offset, f);\r\n" +
                        "                    if (d < res.x)\r\n" +
                        "                    {{\r\n" +
                        "                        res = vec3(d, offset.x, offset.y);\r\n" +
                        "                        output0 = res.x;\r\n" +
                        "                        output1 = res.y;\r\n" +
                        "                    }}\r\n" +
                        "                }}\r\n" +
                        "            }}"
                    , ShaderNode.ID, FunctionType
                    );
            
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT,
                Text, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.ColorspaceConversionNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            object Conversion = GetValueForFieldOrProperty(m_Value, "m_Conversion", BindingFlags.Instance | BindingFlags.NonPublic);
            object from = GetValueForFieldOrProperty(Conversion, "from", BindingFlags.Instance | BindingFlags.Public);
            object to = GetValueForFieldOrProperty(Conversion, "to", BindingFlags.Instance | BindingFlags.Public);
            string fromStr = from.ToString();
            string toStr = to.ToString();

            string Contents = "output0 = input0;\n";
            if (fromStr == "RGB" )
            {
                if ( toStr == "Linear" )
                {
                    Contents = string.Format( CultureInfo.InvariantCulture,
                        "vec3 linearRGBLo = input0 / 12.92;\r\n" +
                        "vec3 linearRGBHi = pow(max(abs((input0 + 0.055) / 1.055), 1.192092896e-07), vec3(2.4, 2.4, 2.4));\r\n" +
                        "output0.x = input0.x <= 0.04045 ? linearRGBLo.x : linearRGBHi.x;\n" +
                        "output0.y = input0.y <= 0.04045 ? linearRGBLo.y : linearRGBHi.y;\n" +
                        "output0.z = input0.z <= 0.04045 ? linearRGBLo.z : linearRGBHi.z;\n");
                }
                if (toStr == "HSV")
                {
                    Contents = string.Format( CultureInfo.InvariantCulture,
                        "vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);\r\n" +
                        "vec4 P = mix(vec4(input0.bg, K.wz), vec4(input0.gb, K.xy), step(input0.b, input0.g));\r\n" +
                        "vec4 Q = mix(vec4(P.xyw, input0.r), vec4(input0.r, P.yzx), step(P.x, input0.r));\r\n" +
                        "float D = Q.x - min(Q.w, Q.y);\r\n" +
                        "float E2 = 1e-10;\r\n" +
                        "float V = (D == 0.0) ? Q.x : (Q.x + E2);\r\n" +
                        "output0 = vec3(abs(Q.z + (Q.w - Q.y)/(6.0 * D + E2)), D / (Q.x + E2), V);");
                }
            }
            if (fromStr == "Linear")
            {
                if (toStr == "RGB")
                {
                    Contents = string.Format( CultureInfo.InvariantCulture,
                        "vec3 sRGBLo = input0 * 12.92;\r\n" +
                        "vec3 sRGBHi = (pow(max(abs(input0), 1.192092896e-07), vec3(1.0 / 2.4, 1.0 / 2.4, 1.0 / 2.4)) * 1.055) - 0.055;\r\n" +
                        "output0.x = input0.x <= 0.0031308 ? sRGBLo.x : sRGBHi.x;\n" +
                        "output0.y = input0.y <= 0.0031308 ? sRGBLo.y : sRGBHi.y;\n" +
                        "output0.z = input0.z <= 0.0031308 ? sRGBLo.z : sRGBHi.z;\n");
                }
                if (toStr == "HSV")
                {
                    Contents = string.Format( CultureInfo.InvariantCulture,
                        "vec3 sRGBLo = input0 * 12.92;\r\n" +
                        "vec3 sRGBHi = (pow(max(abs(input0), 1.192092896e-07), vec3(1.0 / 2.4, 1.0 / 2.4, 1.0 / 2.4)) * 1.055) - 0.055;\r\n" +
                        "vec3 Linear;\n" +
                        "Linear.x = input0.x <= 0.0031308 ? sRGBLo.x : sRGBHi.x;\r\n" +
                        "Linear.y = input0.y <= 0.0031308 ? sRGBLo.y : sRGBHi.y;\r\n" +
                        "Linear.z = input0.z <= 0.0031308 ? sRGBLo.z : sRGBHi.z;\r\n" +
                        "vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);\r\n" +
                        "vec4 P = mix(vec4(Linear.bg, K.wz), vec4(Linear.gb, K.xy), step(Linear.b, Linear.g));\r\n" +
                        "vec4 Q = mix(vec4(P.xyw, Linear.r), vec4(Linear.r, P.yzx), step(P.x, Linear.r));\r\n" +
                        "float D = Q.x - min(Q.w, Q.y);\r\n" +
                        "float E2 = 1e-10;\r\n" +
                        "output0 = vec3(abs(Q.z + (Q.w - Q.y)/(6.0 * D + E2)), D / (Q.x + E2), Q.x);");
                }
            }
            if (fromStr == "HSV")
            {
                if (toStr == "RGB")
                {
                    Contents = string.Format( CultureInfo.InvariantCulture,
                        "vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);\r\n" +
                        "vec3 P = abs(fract(input0.xxx + K.xyz) * 6.0 - K.www);\r\n" +
                        "output0 = input0.z * mix(K.xxx, saturate3(P - K.xxx), input0.y);");
                }
                if (toStr == "Linear")
                {
                    Contents = string.Format( CultureInfo.InvariantCulture,
                        "vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);\r\n" +
                        "vec3 P = abs(fract(input0.xxx + K.xyz) * 6.0 - K.www);\r\n" +
                        "vec3 RGB = input0.z * mix(K.xxx, saturate3(P - K.xxx), input0.y);\r\n" +
                        "vec3 linearRGBLo = RGB / 12.92;\r\n" +
                        "vec3 linearRGBHi = pow(max(abs((RGB + 0.055) / 1.055), 1.192092896e-07), vec3(2.4, 2.4, 2.4));\r\n" +
                        "output0.x = RGB.x <= 0.04045 ? linearRGBLo.x : linearRGBHi.x;\n" +
                        "output0.y = RGB.y <= 0.04045 ? linearRGBLo.y : linearRGBHi.y;\n" +
                        "output0.z = RGB.z <= 0.04045 ? linearRGBLo.z : linearRGBHi.z;\n");
                }
                
            }

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//ColorspaceConversionNode:{0}\n" +
                     "{1}"
                    , ShaderNode.ID, Contents
                    );
            return GenerateShaderExpression( GodotShaderVariableType.GSVT_VECTOR3, Text, GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.CheckerboardNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//CheckerboardNode:{0}\n" +
                    "vec2 uv = input0;\n" +
                    "vec3 ColorA = input1;\n" +
                    "vec3 ColorB = input2;\n" +
                    "vec2 Frequency = input3;\n" +

                     "uv = (uv.xy + 0.5) * Frequency;\r\n" +
                     "            vec2 distance3 = 4.0 * abs(fract(uv + 0.25) - 0.5) - 1.0;\r\n" +
                     "            vec4 derivatives = vec4(dFdx(uv), dFdy(uv));\r\n" +
                     "            vec2 duv_length = sqrt(vec2(dot(derivatives.xz, derivatives.xz), dot(derivatives.yw, derivatives.yw)));\r\n" +
                     "            vec2 scale = 0.35 / duv_length.xy;\r\n" +
                     "            float freqLimiter = sqrt(clamp(1.1f - max(duv_length.x, duv_length.y), 0.0, 1.0));\r\n" +
                     "            vec2 vector_alpha = clamp(distance3 * scale.xy, -1.0, 1.0);\r\n" +
                     "            float alpha = saturate(0.5f + 0.5f * vector_alpha.x * vector_alpha.y * freqLimiter);\r\n" +
                     "            output0 = mix(ColorA, ColorB, vec3(alpha,alpha,alpha) );"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_VECTOR3, GodotShaderVariableType.GSVT_VECTOR3, GodotShaderVariableType.GSVT_VECTOR2,
                Text, GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.TwirlNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//TwirlNode:{0}\n" +
                    "vec2 uv = input0;\n" +
                    "vec2 Center = input1;\n" +
                    "float Strength = input2;\n" +
                    "vec2 Offset = input3;\n" +

                    "vec2 delta = uv - Center;\r\n" +
                    "            float angle = Strength * length(delta);\r\n" +
                    "            float x = cos(angle) * delta.x - sin(angle) * delta.y;\r\n" +
                    "            float y = sin(angle) * delta.x + cos(angle) * delta.y;\r\n" +
                    "            output0 = vec2(x + Center.x + Offset.x, y + Center.y + Offset.y);"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_VECTOR2,
                Text, GodotShaderVariableType.GSVT_VECTOR2, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.FlipbookNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            object m_InvertX = GetValueForFieldOrProperty(m_Value, "m_InvertX", BindingFlags.Instance | BindingFlags.NonPublic);
            object m_InvertY = GetValueForFieldOrProperty(m_Value, "m_InvertY", BindingFlags.Instance | BindingFlags.NonPublic);

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//FlipbookNode:{0}\n" +
                    "vec2 uv = input0;\n" +
                    "float Width = input1;\n" +
                    "float Height = input2;\n" +
                    "float Tile = input3;\n" +
                    "vec2 Invert = vec2( {1}, {2} );\n" +

                    "Tile = floor( mod(Tile + float(0.00001), Width*Height));\r\n" +
                    "            vec2 tileCount = vec2(1.0, 1.0) / vec2(Width, Height);\r\n" +
                    "            float base = floor((Tile + float(0.5)) * tileCount.x);\r\n" +
                    "            float tileX = (Tile - Width * base);\r\n" +
                    "            float tileY = (Invert.y * Height - (base + Invert.y * 1.0));\r\n" +
                    "            output0 = (uv + vec2(tileX, tileY)) * tileCount;"
                    , ShaderNode.ID, Convert.ToInt32(m_InvertX), Convert.ToInt32(m_InvertY)
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT,
                Text, GodotShaderVariableType.GSVT_VECTOR2, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.WhiteBalanceNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//WhiteBalanceNode:{0}\n" +
                    "vec3 In = input0;\n" +
                    "float Temperature = input1;\n" +
                    "float Tint = input2;\n" +

                    "// Range ~[-1.67;1.67] works best\r\n" +
                    "                float t1 = Temperature * 10.0 / 6.0;\r\n" +
                    "                float t2 = Tint * 10.0 / 6.0;\r\n" +
                    "                float x = 0.31271 - t1 * (t1 < 0.0 ? 0.1 : 0.05);\r\n" +
                    "                float standardIlluminantY = 2.87 * x - 3.0 * x * x - 0.27509507;\r\n" +
                    "                float y = standardIlluminantY + t2 * 0.05;\r\n" +
                    "                // Calculate the coefficients in the LMS space.\r\n" +
                    "                vec3 w1 = vec3(0.949237, 1.03542, 1.08728); // D65 white point\r\n" +
                    "                float Y = 1.0;\r\n" +
                    "                float X = Y * x / y;\r\n" +
                    "                float Z = Y * (1.0 - x - y) / y;\r\n" +
                    "                float L = 0.7328 * X + 0.4296 * Y - 0.1624 * Z;\r\n" +
                    "                float M = -0.7036 * X + 1.6975 * Y + 0.0061 * Z;\r\n" +
                    "                float S = 0.0030 * X + 0.0136 * Y + 0.9834 * Z;\r\n" +
                    "                vec3 w2 = vec3(L, M, S);\r\n" +
                    "                vec3 balance = vec3(w1.x / w2.x, w1.y / w2.y, w1.z / w2.z);\r\n" +
                    "               mat3 LIN_2_LMS_MAT = mat3(\r\n" +
                    "                vec3( 3.90405e-1, 5.49941e-1, 8.92632e-3),\r\n" +
                    "                vec3( 7.08416e-2, 9.63172e-1, 1.35775e-3),\r\n" +
                    "                vec3( 2.31082e-2, 1.28021e-1, 9.36245e-1)\r\n" +
                    "            );\r\n" +
                    "                mat3 LMS_2_LIN_MAT = mat3(\r\n" +
                    "                vec3(  2.85847e+0, -1.62879e+0, -2.48910e-2 ),\r\n" +
                    "                vec3( -2.10182e-1,  1.15820e+0,  3.24281e-4 ),\r\n" +
                    "                vec3( -4.18120e-2, -1.18169e-1,  1.06867e+0 )\r\n" +
                    "            );\r\n" +                    
                    "            vec3 lms = LIN_2_LMS_MAT * In;\r\n" +
                    "            lms *= balance;\r\n" +
                    "            output0 = LMS_2_LIN_MAT * lms ;\n"
                    , ShaderNode.ID
                    );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_VECTOR3, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT,
                Text, GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.GradientNode")
        {
            object gradientObj = GetValueForFieldOrProperty(m_Value, "gradient", BindingFlags.Instance | BindingFlags.NonPublic);

            UnityEngine.Gradient gradient = gradientObj as Gradient;
            string GradientDeclaration = GetGradientDeclaration( gradient );
            
            string Text = string.Format( CultureInfo.InvariantCulture,
                    "//GradientNode:{0}\n" +
                    "{1}\n"
                    , ShaderNode.ID, GradientDeclaration
                    );
            return GenerateShaderExpression( Text, GodotShaderVariableType.GSVT_TRANSFORM, ref Params, ShaderNode );
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.SampleGradient")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            UTGSlot GradientSlot = ShaderNode.GetSlot("Gradient");
            object ConnectingSlot = null;
            object GradientNode = ShaderData.GetConnectingNodeAndSlot(GradientSlot.Slot, ref ConnectingSlot);
            string GradientDeclaration = "";
            if (GradientNode != null)
            {
                object gradientObj = GetValueForFieldOrProperty(GradientNode, "gradient", BindingFlags.Instance | BindingFlags.NonPublic);
                if ( GradientNode.GetType().FullName.Contains("UnityEditor.ShaderGraph.PropertyNode"))
                {
                    object GradientProperty = GetPropertyValue(GradientNode);
                    gradientObj = GradientProperty;
                }
                
                UnityEngine.Gradient gradient = gradientObj as Gradient;
                GradientDeclaration = GetGradientDeclaration(gradient);                
            }
            string Text = string.Format( CultureInfo.InvariantCulture,
                "//SampleGradient:{0}\n" +
                "{1}" +
                "float Time = input1;\n" +
                "// convert to OkLab if we need perceptual color space.\r\n" +
                "            vec3 color = mix(gradient.colors[0].rgb, LinearToOklab(gradient.colors[0].rgb), gradient.type == 2 ? 1.0 : 0.0 );\r\n" +
                "        \r\n" +
                "            for (int c = 1; c < gradient.colorsLength; c++)\r\n" +
                "            {{\r\n" +
                "                float colorPos = saturate((Time - gradient.colors[c - 1].w) / (gradient.colors[c].w - gradient.colors[c - 1].w)) * step( float(c), float(gradient.colorsLength - 1) );\r\n" +
                "                vec3 color2 = mix(gradient.colors[c].rgb, LinearToOklab(gradient.colors[c].rgb), gradient.type == 2 ? 1.0 : 0.0 );\r\n" +
                "                color = mix(color, color2, mix(colorPos, step(0.01, colorPos), float(gradient.type % 2))); // grad.type == 1 is fixed, 0 and 2 are blends.\r\n" +
                "            }}\r\n" +
                "            color = mix(color, OklabToLinear(color), gradient.type == 2 ? 1.0 : 0.0 );\r\n" +
                "        \r\n" +
                "        #ifdef UNITY_COLORSPACE_GAMMA\r\n" +
                "            color = LinearToSRGB(color);\r\n" +
                "        #endif\r\n" +
                "        \r\n" +
                "            float alpha = gradient.alphas[0].x;\r\n" +
                "            for (int a = 1; a < gradient.alphasLength; a++)\r\n" +
                "            {{\r\n" +
                "                float alphaPos = saturate((Time - gradient.alphas[a - 1].y) / (gradient.alphas[a].y - gradient.alphas[a - 1].y)) * step( float(a), float(gradient.alphasLength - 1) );\r\n" +
                "                alpha = mix(alpha, gradient.alphas[a].x, mix(alphaPos, step(0.01, alphaPos), float(gradient.type % 2)));\r\n" +
                "            }}\r\n" +
                "        \r\n" +
                "            output0 = vec4(color, alpha);"
                , ShaderNode.ID, GradientDeclaration
                );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_TRANSFORM, GodotShaderVariableType.GSVT_FLOAT, Text, GodotShaderVariableType.GSVT_VECTOR4, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.GradientNoiseNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                "//GradientNoiseNode:{0}\n" +
                "vec2 uv = input0;\n" +
                "vec3 Scale = vec3(input1,input1,input1);\n" +
                
                "            vec2 p = uv * Scale.xy;\r\n" +
                "            vec2 ip = floor(p);\r\n" +
                "            vec2 fp = fract(p);\r\n" +
                "            float d00 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip), fp);\r\n" +
                "            float d01 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip + vec2(0.0, 1.0)), fp - vec2(0.0, 1.0));\r\n" +
                "            float d10 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip + vec2(1.0, 0.0)), fp - vec2(1.0, 0.0));\r\n" +
                "            float d11 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip + vec2(1.0, 1.0)), fp - vec2(1.0, 1.0));\r\n" +
                "            fp = fp * fp * fp * (fp * (fp * 6.0 - 15.0) + 10.0);\r\n" +
                "            output0 = mix(mix(d00, d01, fp.y), mix(d10, d11, fp.y), fp.x) + 0.5;"
                , ShaderNode.ID
                );
            return GenerateShaderExpression( GodotShaderVariableType.GSVT_VECTOR2, GodotShaderVariableType.GSVT_FLOAT, Text, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.SelectableBranchNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                "//SelectableBranchNode:{0}\n" +
                "switch(int(input0))\r\n" +
                "{{\r\n" +
                "    case 0:\r\n" +
                "        output0 = input1; \r\n" +
                "        break;\r\n" +
                "    case 1:\r\n" +
                "        output0 = input2; \r\n" +
                "       break;\r\n" +
                "    case 2:\r\n" +
                "        output0 = input3; \r\n" +
                "       break;\r\n" +
                "    case 3:\r\n" +
                "        output0 = input4; \r\n" +
                "       break;\r\n" +
                "    case 4:\r\n" +
                "        output0 = input5; \r\n" +
                "       break;\r\n" +
                "    default:\r\n" +
                "        output0 = input1; \r\n" +
                "       break;\r\n" +
                "}}"
                , ShaderNode.ID
                );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT,
                GodotShaderVariableType.GSVT_FLOAT, GodotShaderVariableType.GSVT_FLOAT, Text, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.Rendering.Universal.UniversalSampleBufferNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            object SourceBuffer = GetValueForFieldOrProperty(m_Value, "m_BufferType", BindingFlags.Instance | BindingFlags.NonPublic);
            string SourceBufferStr = SourceBuffer.ToString();
            string SampleExpression = "output0 = vec3(0,1,0);";
            if (SourceBufferStr == "MotionVectors" )
            {
                SampleExpression = "output0 = vec3(0,0,0);";
            }
            else if ( SourceBufferStr == "BlitSource" )
            {
                SampleExpression = "output0 = vec3(0,0,0);";
            }
            string Text = string.Format( CultureInfo.InvariantCulture,
                "//UniversalSampleBufferNode:{0}\n" +
                "//{2}\n" +
                "{1}"
                , ShaderNode.ID, SampleExpression, SourceBufferStr
                );
            return GenerateShaderExpression(GodotShaderVariableType.GSVT_VECTOR2, Text, GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.AmbientNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            var ambientColor = ToString( RenderSettings.ambientLight );
            if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight)
            {
                ambientColor = ToString( RenderSettings.ambientSkyColor );
            }
            var EquatorColor = ToString( RenderSettings.ambientEquatorColor);
            var GroundColor = ToString( RenderSettings.ambientGroundColor);
            string Text = string.Format( CultureInfo.InvariantCulture,
                "//AmbientNode:{0}\n" +
                "output0 = {1}.rgb;\n" +//Color/Sky
                "output1 = {2}.rgb;\n" +//Equator
                "output2 = {3}.rgb;\n"  //Ground
                , ShaderNode.ID, ambientColor, EquatorColor, GroundColor
                );
            return GenerateShaderExpression( Text, GodotShaderVariableType.GSVT_VECTOR3, GodotShaderVariableType.GSVT_VECTOR3, GodotShaderVariableType.GSVT_VECTOR3, ref Params, ShaderNode);
        }
        if (m_TypeStr == "UnityEditor.ShaderGraph.FogNode")
        {
            AddConstantNodesForUnconnectedInputs(ShaderData, ShaderNode, CurrentShaderType);

            string Text = string.Format( CultureInfo.InvariantCulture,
                "//FogNode:{0}\n" +
                "float CameraNearPlane = 0.1;\n" +
                "float viewZ = ( vec4( input0, 1.0) * INV_VIEW_MATRIX ).z;//-TransformWorldToView(TransformObjectToWorld( input0 )).z;\n" +
                "    float nearZ0ToFarZ = max(viewZ - CameraNearPlane, 0.0);\n" +
                "    // ComputeFogFactorZ0ToFar returns the fog occlusion (0 for full fog and 1 for no fog) so this has to be inverted for density.\n" +
                "output0 = {1};\n" +
                "output1 = 1.0f - ComputeFogIntensity(ComputeFogFactorZ0ToFar(nearZ0ToFarZ,{2}));"
                , ShaderNode.ID, ToString( RenderSettings.fogColor ), RenderSettings.fogDensity.ToString()
                );
            return GenerateShaderExpression( GodotShaderVariableType.GSVT_VECTOR3, Text,
                GodotShaderVariableType.GSVT_VECTOR4, GodotShaderVariableType.GSVT_FLOAT, ref Params, ShaderNode);
        }

        #endif
        if ( MissingNodeTypes.Find( Element => Element.Equals( m_TypeStr ) ) == null )
            MissingNodeTypes.Add( m_TypeStr );

        return null;
    }

    public static List<string> MissingNodeTypes = new List<string>();

    public static Type GetTypeInHierarchyByName( Type BaseType, string name )
    {
        if( BaseType != null && BaseType.Name == name )
            return BaseType;
        if( BaseType.BaseType != null )
            return GetTypeInHierarchyByName( BaseType.BaseType, name );
        else
            return null;
    }
    static public List<Type> GetTypeHierarchy( Type TheType )
    {
        List<Type> Ret = new List<Type>();
        Ret.Add( TheType );
        Type BaseType = TheType.BaseType;
        while( BaseType != null )
        {
            Ret.Add( BaseType );
            BaseType = BaseType.BaseType;
        }
        return Ret;
    }
    static public bool RemapIndices( int[] ReorderIndices, int Num, ref int TestIndex )
    {
        for( int i = 0; i < Num; i++ )
        {
            if( TestIndex == i )
            {
                TestIndex = ReorderIndices[i];
                return true;
            }
        }

        return false;
    }
    //redirect connection
    static public bool ReorderConnection( GodotShaderNode SourceNode, GodotShaderNode DestinationNode, ShaderConnection Connection )
    {
        if( SourceNode == null || DestinationNode == null )
            return false;

        string SourceNodeType = "";
        string DestinationNodeType = "";
        if( SourceNode.NodeObject != null )
            SourceNodeType = SourceNode.NodeObject.GetType().FullName;
        if( DestinationNode.NodeObject != null )
            DestinationNodeType = DestinationNode.NodeObject.GetType().FullName;

        if( DestinationNodeType == "UnityEditor.ShaderGraph.SampleTexture2DNode" ||
            DestinationNodeType == "UnityEditor.ShaderGraph.SampleTexture2DLODNode" ||
            DestinationNodeType == "UnityEditor.ShaderGraph.SampleRawCubemapNode" ||
            DestinationNodeType == "UnityEditor.ShaderGraph.SampleTexture3DNode")
        {
            int[] Reorder = { 2, 0, -1, 1 };
            RemapIndices( Reorder, Reorder.Length, ref Connection.DestinationNodeInput );
            return true;
        }
        
        //if( DestinationNodeType == "UnityEditor.ShaderGraph.KeywordNode" )
        //{
        //    int[] Reorder = { 1, 2, 0 };
        //    RemapIndices( Reorder, Reorder.Length, ref Connection.DestinationNodeInput );
        //    return true;
        //}
        if (DestinationNodeType == "UnityEditor.ShaderGraph.PolarCoordinatesNode" )
        {
            int[] Reorder = { 0, -1, 1, -1 };
            RemapIndices(Reorder, Reorder.Length, ref Connection.DestinationNodeInput);
            return true;
        }
        if (DestinationNodeType == "UnityEditor.ShaderGraph.FresnelNode")
        {
            int[] Reorder = { 0, 1, 3 };
            RemapIndices(Reorder, Reorder.Length, ref Connection.DestinationNodeInput);
            return true;
        }
        if (DestinationNodeType == "UnityEditor.ShaderGraph.RefractNode")
        {
            int[] Reorder = { 0, 1, -1, 2 };
            RemapIndices(Reorder, Reorder.Length, ref Connection.DestinationNodeInput);
            return true;
        }
        if( DestinationNodeType == "UnityEditor.ShaderGraph.MultiplyNode")
        {
            if (DestinationNode.DefinitionText != null &&
                DestinationNode.DefinitionText.Contains("VisualShaderNodeTransformVecMult"))
            {
                UTGSlot SourceSlot = SourceNode.GetSlot(Connection.SourceNodeOutput);
                if (SourceSlot.GetVariableType() == GodotShaderVariableType.GSVT_TRANSFORM)
                {
                    if (Connection.DestinationNodeInput == 1)
                    {
                        Connection.DestinationNodeInput = 0;
                        return true;
                    }
                }
                else
                {
                    if (Connection.DestinationNodeInput == 0)
                    {
                        Connection.DestinationNodeInput = 1;
                        return true;
                    }
                }                
            }
        }

        return false;
    }
    static public int GetOutputIndexForDisplayName( string customName, GodotShaderData ShaderData)
    {
        int Index = -1;
        int VSOutput = -1;
        if( customName == "Position" )
        {
            VSOutput = 0;
        }
        else if( customName == "Normal" )
        {
            VSOutput = 1;
        }
        else if( customName == "Tangent" )
        {
            VSOutput = 2;
        }
        else if( customName == "BaseColor" )
        {
            Index = 0;
        }
        else if (customName == "Alpha" )
        {
            if ( (ShaderData.AlphaClipping && !ShaderData.IsTransparent)
                                            || ShaderData.IsTransparent )
                Index = 1;
        }
        //Transparent + Alpha test results in some weirdness in Godot sometimes
        else if (customName == "AlphaClipThreshold" )
        {
            if ( ShaderData.AlphaClipping && !ShaderData.IsTransparent )
                Index = 19;
        }
        else if( customName == "NormalTS" )
        {
            Index = 9;//Normal=8,NormalMap=9
        }
        else if( customName == "Metallic" )
        {
            Index = 2;
        }
        else if( customName == "Smoothness" )
        {
            Index = 3;
        }
        else if( customName == "Emission" )
        {
            Index = 5;
        }
        else if( customName == "Occlusion" )
        {
            Index = 6;
        }
        if( VSOutput != -1 )
            return VSOutput;
        return Index;
    }
    static public string GetResourcePath( UnityEngine.Object obj )
    {
        string OutPath = GetAssetPath(obj);
        if( OutPath.Length > 0 )
        {
            string AssetName = GetAssetNameFromPath(OutPath, false);
            string FolderPath = GetGodotPathFromUnityPath(OutPath);
            OutPath = FolderPath + AssetName;
        }
        return OutPath;
    }
    static public string GetProjectPath()
    {
        string Path = UnityEngine.Application.dataPath;
        int AssetsOffset = Path.IndexOf("Assets");
        return Path.Substring( 0, AssetsOffset );
    }
    public class UTGSlot
    {
        public string Name;
        public bool Input = false;
        public string ValueType;
        public bool isConnected;
        public Vector4 Vec4;
        //public SerializableTexture Texture;
        public Texture texture;
#if UNITY_SHADER_GRAPH
        public UVChannel Channel;
#endif
        public object Slot;
        public int OutputIndex = 0;
        public int InputIndex = 0;

        public GodotShaderVariableType GetVariableType()
        {
            return GetVariableTypeFromString( ValueType );
        }
    }

    public class ExportTexture
    {
        public Texture Tex;
        public string GUID;
        public string LocalGUID;
        public string ResourcePath;
        public GodotShaderNode Node;
        public bool IsSmoothnessTexture = false;
    }
    public class ShaderVarying
    {
        public int Index = -1;
        public string Name;
        public GodotShaderVariableType Type = GodotShaderVariableType.GSVT_UNKNOWN;
    };
    public static ExportTexture CreateExportTexture( Texture Tex, int LocalIndex = 1 )
    {
        ExportTexture NewExportTexture = new ExportTexture();
        NewExportTexture.Tex = Tex;
        NewExportTexture.GUID = GetGodotGUID( Tex.GetHashCode() );
        NewExportTexture.LocalGUID = GenerateLocalGUID( Tex, LocalIndex );
        NewExportTexture.ResourcePath = GetResourcePath( Tex );

        return NewExportTexture;
    }
    public static ExportTexture AddExportTexture( Texture Tex, ref List<ExportTexture> Textures )
    {
        if ( Tex == null )
            return null;
        for( int i = 0; i < Textures.Count; i++ )
        {
            if( Textures[i].Tex == Tex )
                return Textures[i];
        }

        ExportTexture NewExportTexture = CreateExportTexture( Tex, Textures.Count + 1 );
        Textures.Add( NewExportTexture );

        return NewExportTexture;
    }    
    public static bool IsNormalMap(Texture texture)
    {
        string path = AssetDatabase.GetAssetPath(texture);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            return importer.textureType == TextureImporterType.NormalMap;
        }
        return false;
    }
    public static bool SetIsNormalMap(Texture texture, bool Enable )
    {
        string path = AssetDatabase.GetAssetPath(texture);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            if ( Enable )
                importer.textureType = TextureImporterType.NormalMap;
            else
                importer.textureType = TextureImporterType.Default;

            importer.SaveAndReimport();
        }
        return false;
    }
    public static int GetSlicesXYFromDepth( int Depth )
    {
        int SquareRoot = (int)Math.Sqrt( Depth );
        if ( Depth - SquareRoot * SquareRoot > 0 )
            return SquareRoot + 1;
        else
            return SquareRoot;
    }
    public static System.Drawing.Point GetTextureCells(Texture texture)
    {
        string path = AssetDatabase.GetAssetPath(texture);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;        
        if (importer != null)
        {
            UnityEditor.TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings( settings );
            return new System.Drawing.Point( settings.flipbookColumns, settings.flipbookRows );
        }
        else
        {
            Texture2DArray Tex2DArray = texture as Texture2DArray;
            if ( Tex2DArray != null )
            {
                int SlicesXY = GetSlicesXYFromDepth( Tex2DArray.depth );
                return new System.Drawing.Point( SlicesXY, SlicesXY );
            }
            Texture3D Tex3D = texture as Texture3D;
            if ( Tex3D != null )
            {
                int SlicesXY = GetSlicesXYFromDepth( Tex3D.depth );
                return new System.Drawing.Point( SlicesXY, SlicesXY );
            }
        }
        return new System.Drawing.Point(0, 0);
    }
    public static void SetTextureReadable(Texture texture)
    {
        string path = AssetDatabase.GetAssetPath(texture);
        AssetImporter importer = AssetImporter.GetAtPath(path);
        TextureImporter textureimporter = importer as TextureImporter;
        if (textureimporter != null)
        {
            UnityEditor.TextureImporterSettings settings = new TextureImporterSettings();
            textureimporter.ReadTextureSettings(settings);
            settings.readable = true;
            textureimporter.SetTextureSettings( settings );
            EditorUtility.SetDirty(importer);
            textureimporter.SaveAndReimport();
        }
    }
    public static void SetTextureCompression(Texture texture, TextureImporterCompression Compression )
    {
        string path = AssetDatabase.GetAssetPath(texture);
        AssetImporter importer = AssetImporter.GetAtPath(path);
        TextureImporter textureimporter = importer as TextureImporter;
        if (textureimporter != null)
        {
            TextureImporterPlatformSettings PlatformSettings = textureimporter.GetDefaultPlatformTextureSettings();

            PlatformSettings.textureCompression = Compression;
            textureimporter.SetPlatformTextureSettings(PlatformSettings);
            EditorUtility.SetDirty( textureimporter );
            textureimporter.SaveAndReimport();
        }
    }
    public static Texture2D RenderTextureToTexture2D(RenderTexture rt, TextureFormat textureFormat = TextureFormat.RGBA32, bool mipChain = false)
    {
        // Backup the currently active RenderTexture
        RenderTexture previous = RenderTexture.active;

        // Set the supplied RenderTexture as active
        RenderTexture.active = rt;

        if ( rt.width == 0 || rt.height == 0 )
        {
            return null;
        }
        // Create a new Texture2D with the same dimensions as the RenderTexture
        Texture2D tex = new Texture2D(rt.width, rt.height, textureFormat, mipChain);

        // Read the pixels from the RenderTexture into the Texture2D
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        // Restore the previously active RenderTexture
        RenderTexture.active = previous;

        return tex;
    }
    public static bool IsFontTexture( Texture tex )
    {
        string path = AssetDatabase.GetAssetPath(tex);
        AssetImporter importer = AssetImporter.GetAtPath(path);
        TrueTypeFontImporter fontImporter = importer as TrueTypeFontImporter;
        UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
        Font font = obj as Font;
        if ( font != null )
        {
            return true;
        }

        return false;
    }
    public static Texture GetFontTexture( Texture tex )
    {
        string path = AssetDatabase.GetAssetPath(tex);
        AssetImporter importer = AssetImporter.GetAtPath(path);
        TrueTypeFontImporter fontImporter = importer as TrueTypeFontImporter;
        UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
        Font font = obj as Font;
        if (font != null)
        {
            font.RequestCharactersInTexture("A"); // any character will allocate atlas
            return font.material.mainTexture;
        }        

        return null;
    }
    public static RenderTexture BlitToRenderTexture(Texture source, RenderTextureFormat format = RenderTextureFormat.ARGB32, Material mat = null )
    {
        if (source == null)
        {
            Debug.LogError("BlitToRenderTexture: Source texture is null!");
            return null;
        }

        // Create RenderTexture matching the source texture's size
        var rt = new RenderTexture(source.width, source.height, 0, format);
        rt.Create();

        // Backup the currently active RenderTexture
        RenderTexture previous = RenderTexture.active;

        // Blit (copy) the texture into the new RenderTexture
        if ( mat != null )
            UnityEngine.Graphics.Blit(source, rt, mat );
        else
            UnityEngine.Graphics.Blit(source, rt);//, new Vector2(1,-1), new Vector2(0,0) );

        // Restore previous active RT
        RenderTexture.active = previous;

        return rt;
    }
    public static ComputeShader FindComputeShader(string shaderName)
    {
        string[] guids = AssetDatabase.FindAssets($"{shaderName} t:ComputeShader");
        if (guids.Length == 0)
        {
            Debug.LogWarning($"ComputeShader '{shaderName}' not found in project.");
            return null;
        }

        // If multiple results, just take the first
        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);

        if (shader == null)
            Debug.LogWarning($"Asset found at '{path}' is not a ComputeShader.");

        return shader;
    }
    public static void BlitTextureToAtlas(Texture source, Texture target, int slicesX, int slicesY)
    {
        ComputeShader compute = null;
        if ( source as Texture2DArray )
            compute = FindComputeShader("CopyArrayToAtlas");
        else
            compute = FindComputeShader("Copy3DToAtlas");

        int kernel = compute.FindKernel("CSMain");
        compute.SetTexture(kernel, "_Source", source);
        compute.SetTexture(kernel, "_Target", target);
        compute.SetInt("_SlicesX", slicesX);
        compute.SetInt("_SlicesY", slicesY);
        compute.SetInt("_SliceWidth", source.width);
        compute.SetInt("_SliceHeight", source.height);

        int threadX = Mathf.CeilToInt((float)(source.width * slicesX) / 8);
        int threadY = Mathf.CeilToInt((float)(source.height * slicesY) / 8);

        compute.Dispatch(kernel, threadX, threadY, 1);
    }
    static uint MAKEFOURCC( char ch0, char ch1, char ch2, char ch3)
    {
        uint Ret = ((uint)(byte)(ch0) | ((uint)(byte)(ch1) << 8) |
                ((uint)(byte)(ch2) << 16) | ((uint)(byte)(ch3) << 24));
        return Ret;
    }
    static uint MAKEFOURCC(int ch0, int ch1, int ch2, int ch3)
    {
        uint Ret = ((uint)(byte)(ch0) | ((uint)(byte)(ch1) << 8) |
                ((uint)(byte)(ch2) << 16) | ((uint)(byte)(ch3) << 24));
        return Ret;
    }
    static void CopyUintTo(uint u, byte[] Destination, ref int Offset)
    {
        byte[] uBytes = System.BitConverter.GetBytes(u);
        
        uBytes.CopyTo(Destination, Offset);
        
        Offset += 4;
    }
    class DDPIXELFORMAT
    {
        public uint dwSize;//32
        public uint dwFlags;
        public uint dwFourCC;
        public uint dwRGBBitCount;
        public uint dwRBitMask;
        public uint dwGBitMask;
        public uint dwBBitMask;
        public uint dwRGBAlphaBitMask;

        public void CopyTo( byte[] Destination, ref int Offset)
        {
            CopyUintTo(dwSize, Destination, ref Offset);
            CopyUintTo(dwFlags, Destination, ref Offset);
            CopyUintTo(dwFourCC, Destination, ref Offset);
            CopyUintTo(dwRGBBitCount, Destination, ref Offset);
            CopyUintTo(dwRBitMask, Destination, ref Offset);
            CopyUintTo(dwGBitMask, Destination, ref Offset);
            CopyUintTo(dwBBitMask, Destination, ref Offset);
            CopyUintTo(dwRGBAlphaBitMask, Destination, ref Offset);
        }
    };
    class DDCAPS2
    {
        public uint dwCaps1;
        public uint dwCaps2;
        public uint[] dwReserved = new uint[2];

        public void CopyTo(byte[] Destination, ref int Offset)
        {
            CopyUintTo(dwCaps1, Destination, ref Offset);
            CopyUintTo(dwCaps2, Destination, ref Offset);
            for (int i = 0; i < 2; i++)
            {
                CopyUintTo(dwReserved[i], Destination, ref Offset);
            }
        }
    };
    class DDSURFACEDESC2
    {
        public uint dwSize;
        public uint dwFlags;
        public uint dwHeight;
        public uint dwWidth;
        public uint dwPitchOrLinearSize;
        public uint dwDepth;
        public uint dwMipMapCount;
        public uint[] dwReserved1 = new uint[11];
        public DDPIXELFORMAT ddpfPixelFormat = new DDPIXELFORMAT();//32
        public DDCAPS2 ddsCaps = new DDCAPS2();//16
        public uint dwReserved2;

        public void CopyTo(byte[] Destination, ref int Offset)
        {
            CopyUintTo(dwSize, Destination, ref Offset);
            CopyUintTo(dwFlags, Destination, ref Offset);
            CopyUintTo(dwHeight, Destination, ref Offset);
            CopyUintTo(dwWidth, Destination, ref Offset);
            CopyUintTo(dwPitchOrLinearSize, Destination, ref Offset);
            CopyUintTo(dwDepth, Destination, ref Offset);
            CopyUintTo(dwMipMapCount, Destination, ref Offset);

            for (int i = 0; i < 11; i++)
            {
                CopyUintTo(dwReserved1[i], Destination, ref Offset);
            }

            ddpfPixelFormat.CopyTo(Destination, ref Offset);
            ddsCaps.CopyTo(Destination, ref Offset);
            CopyUintTo(dwReserved2, Destination, ref Offset);
        }
    };
    static int GetMipSize( TextureFormat Format, int Width, int Height )
    {
        int x = Width;
        int y = Height;
        int nBlockSize = 16;

        if (Format == TextureFormat.DXT1 )//|| Format == TextureFormat.DXT5)
        {
            nBlockSize = 8;
        }
        int MipSize = ((x + 3) / 4) * ((y + 3) / 4) * nBlockSize;
        return MipSize;
    }
    static void SaveDDS(TextureFormat Format, int Width, int Height, byte[] RawImageData, int NumMipMaps, string FileName )
    {
	    uint DDSmagic = 0x20534444;
        uint DDSCAPS_TEXTURE = 0x00001000;

        int MipSize = GetMipSize( Format, Width, Height );
        

        DDSURFACEDESC2 DDSHeader = new DDSURFACEDESC2();
        
	    DDSHeader.dwSize = 124;
	    DDSHeader.dwFlags = DDSCAPS_TEXTURE;// 659463;
	    DDSHeader.dwHeight = (uint)Height;
	    DDSHeader.dwWidth = (uint)Width;
        //Godot 4.5 complains about this being non-zero!!
	    DDSHeader.dwPitchOrLinearSize = 0;//(uint)MipSize;
	    DDSHeader.dwDepth = 0;
        DDSHeader.dwMipMapCount = (uint)NumMipMaps;// GetMipLevels( Width, Height );
	    
	    DDSHeader.dwReserved2 = 0;	    

	    DDSHeader.ddpfPixelFormat.dwSize = 32;
	    DDSHeader.ddpfPixelFormat.dwFlags = 4;

	    switch ( Format )
	    {
		    case TextureFormat.DXT1: DDSHeader.ddpfPixelFormat.dwFourCC = MAKEFOURCC('D', 'X', 'T', '1' ); break;
            case TextureFormat.DXT5: DDSHeader.ddpfPixelFormat.dwFourCC = MAKEFOURCC('D', 'X', 'T', '5'); break;
	    }
	
	    DDSHeader.ddsCaps.dwCaps1 = 0;// 4198408;
	    DDSHeader.ddsCaps.dwCaps2 = 0;
        
        byte[] HeaderData = new byte[ 300 ];
        int Offset = 0;
        CopyUintTo(DDSmagic, HeaderData, ref Offset );
        DDSHeader.CopyTo(HeaderData, ref Offset);

        int TotalFileSize = RawImageData.Length + Offset;
        byte[] FileData = new byte[TotalFileSize];
        HeaderData.CopyTo( FileData, 0 );
        RawImageData.CopyTo( FileData, Offset );

        File.WriteAllBytes(FileName, FileData);
    }
    public static void ExportTextures( ref string ResourceReferences, List<ExportTexture> Textures )
    {
        for( int i = 0; i < Textures.Count; i++ )
        {
            ExportTexture GodotTex = Textures[ i ];
            Texture Tex = GodotTex.Tex;
            string Type = GetTextureTypeForResource(Tex);
            string ExtResource = GenerateExternalResourceReference(Type, GodotTex.GUID, GodotTex.ResourcePath, GodotTex.LocalGUID );
            ResourceReferences += ExtResource;
            ResourceReferences += "\n";

            bool NormalMap = IsNormalMap( Tex );
            string ResourceFolder = GetFolderFromFilePath( GodotTex.ResourcePath );
            Directory.CreateDirectory( ExportFolder + "/" + ResourceFolder );
            string TextureAssetPath = GetAssetPath(Tex);
            byte[] PNGBytes = null;
            RenderTexture renderTexture = Tex as RenderTexture;

            string UnityTexturePath = GetProjectPath() + TextureAssetPath;
            string ExportTexturePath = ExportFolder + "/" + GodotTex.ResourcePath;

            if ( IsFontTexture( Tex ))
            {
                Tex = GetFontTexture( Tex );
            }
            //Probably a TextMeshPro asset
            if ( Tex.width == 0 || Tex.height == 0 )
            {
                continue;
            }

            Texture2D Tex2D = Tex as Texture2D;
            Texture2DArray Tex2DArray = Tex as Texture2DArray;
            Texture3D Tex3D = Tex as Texture3D;

            bool NeedsConversion = false;
            if ( (!TextureAssetPath.Contains(".png", StringComparison.OrdinalIgnoreCase)
                  && !TextureAssetPath.Contains(".tga", StringComparison.OrdinalIgnoreCase)
                  && !TextureAssetPath.Contains(".jpg", StringComparison.OrdinalIgnoreCase)
                  && !TextureAssetPath.Contains(".jpeg", StringComparison.OrdinalIgnoreCase))
                  || Tex2DArray != null || Tex3D != null
                  || GodotTex.IsSmoothnessTexture
                  )
            {
                NeedsConversion = true;
                ExportTexturePath = System.IO.Path.ChangeExtension(ExportTexturePath, ".png");
                GodotTex.ResourcePath = System.IO.Path.ChangeExtension(GodotTex.ResourcePath, ".png");
            }

            if (File.Exists(ExportTexturePath))
                continue;

            bool IsDDS = false;
            if ( renderTexture != null )
            {
                Tex2D = RenderTextureToTexture2D( renderTexture );
                PNGBytes = Tex2D.EncodeToPNG();
            }
            else if (NeedsConversion)
            {
                if (!Tex.isReadable)
                {
                    SetTextureReadable( Tex );
                    if (!Tex.isReadable)//Gets here if it's a builtin texture
                    {
                        renderTexture = BlitToRenderTexture( Tex, RenderTextureFormat.ARGB32 );
                        Tex2D = RenderTextureToTexture2D( renderTexture );
                        PNGBytes = Tex2D.EncodeToPNG();
                    }
                }
                if ( Tex2D != null )
                {
                    if (Tex2D.format != TextureFormat.ARGB32 &&
                        Tex2D.format != TextureFormat.RGBA32 &&
                        Tex2D.format != TextureFormat.RGB24)
                    {
                        SetTextureCompression( Tex2D, TextureImporterCompression.Uncompressed);
                        if (Tex2D.format != TextureFormat.ARGB32 &&
                            Tex2D.format != TextureFormat.RGBA32 &&
                            Tex2D.format != TextureFormat.RGB24)
                        {
                            renderTexture = BlitToRenderTexture( Tex2D, RenderTextureFormat.ARGB32 );
                            Tex2D = RenderTextureToTexture2D( renderTexture );
                            PNGBytes = Tex2D.EncodeToPNG();
                        }
                    }
                    if ( GodotTex.IsSmoothnessTexture)
                    {
                        //Need to invert alpha to convert smoothness to roughness for Godot !
                        var InvertAlphaMat = new Material(Shader.Find("Hidden/InvertColors"));
                        renderTexture = BlitToRenderTexture( Tex2D, RenderTextureFormat.ARGB32, InvertAlphaMat );
                        Tex2D = RenderTextureToTexture2D( renderTexture );
                        PNGBytes = Tex2D.EncodeToPNG();
                    }
                    //if ( Tex2D.format == TextureFormat.DXT1 || Tex2D.format == TextureFormat.DXT5 )
                    //{
                    //    IsDDS = true;
                    //}
                    //EncodeToPNG apparently uses DXTnm format and fucks colors
                    bool WasNormalMap = IsNormalMap( Tex2D );
                    if ( WasNormalMap )
                        SetIsNormalMap( Tex2D, false );
                    try
                    {
                        if ( PNGBytes == null && !IsDDS )
                            PNGBytes = Tex2D.EncodeToPNG();
                    }
                    catch( Exception E )
                    {
                        Debug.LogError(E.Message + "\n" + E.StackTrace );
                    }
                    //Revert back to fix subsequent exports
                    if ( WasNormalMap )
                    {
                        SetIsNormalMap( Tex2D, true );
                    }
                }
                else if ( Tex2DArray != null || Tex3D != null)
                {
                    int SlicesXY = -1;
                    int Width = Tex.width;
                    int Height = Tex.height;
                    int Depth = -1;
                    if ( Tex2DArray != null)
                    { 
                        Depth = Tex2DArray.depth;
                    }
                    if ( Tex3D != null)
                    {
                        Depth = Tex3D.depth;
                    }
                    SlicesXY = GetSlicesXYFromDepth( Depth );
                    if ( SlicesXY * Width  > 16384 ||
                         SlicesXY * Height > 16384 )
                    {
                        Debug.LogError("Texture2DArray(" + Width + "x" + Height + "x" + Tex2DArray.depth +
                            " can't be converted to Texture2D, slices * width/height exceed 16384!");
                    }
                    renderTexture = new RenderTexture(SlicesXY * Width, SlicesXY * Height, 0, RenderTextureFormat.ARGB32);
                    renderTexture.enableRandomWrite = true;
                    renderTexture.Create();

                    BlitTextureToAtlas( Tex, renderTexture, SlicesXY, SlicesXY );
                    Tex2D = RenderTextureToTexture2D( renderTexture );
                    PNGBytes = Tex2D.EncodeToPNG();
                }
                if ((PNGBytes == null || PNGBytes.Length == 0) && !IsDDS )
                    Debug.LogError("Couldn't convert Texture to ARGB32 " + Tex.name);                
            }

            //if ( IsDDS )
            //{
            //    ExportTexturePath = System.IO.Path.ChangeExtension(ExportTexturePath, ".dds");
            //    GodotTex.ResourcePath = System.IO.Path.ChangeExtension(GodotTex.ResourcePath, ".dds");
            //
            //    byte[] RawData = Tex2D.GetRawTextureData();
            //    SaveDDS( Tex2D.format, Tex2D.width, Tex2D.height, RawData, Tex2D.mipmapCount, ExportTexturePath );
            //}
            //else
            if (PNGBytes != null)
            {
                File.WriteAllBytes( ExportTexturePath, PNGBytes );
            }
            else
            {
                if (!File.Exists(UnityTexturePath))
                    Debug.LogError("Texture doesn't exist where it should " + Tex.name);
                else
                {
                    if (!File.Exists(ExportTexturePath))
                    {
                        File.Copy(UnityTexturePath, ExportTexturePath);
                    }
                }
            }

            string TextureMeta = GenerateTextureMeta( GodotTex, NormalMap );
            string TextureMetaPath = ExportFolder + "/" + GodotTex.ResourcePath + ".import";

            if (!File.Exists(TextureMetaPath))
                File.WriteAllText( TextureMetaPath, TextureMeta );
        }
    }
    public class GodotShaderData
    {
        public string FilePath;
        public List<GodotShaderNode> ShaderNodes = new List<GodotShaderNode>();
        public List<object> EdgeList = new List<object>();
        public List<ShaderConnection> Connections = new List<ShaderConnection>();
        public List<ExportTexture> Textures = new List<ExportTexture>();
        public List<ShaderParameter> ShaderParameters = new List<ShaderParameter>();
        public List<string> OriginalParameterNames = new List<string>();
        public List<ShaderType> OriginalParameterShaderTypes = new List<ShaderType>();
        public List<ShaderVarying> Varyings = new List<ShaderVarying>();
        public List<string> CustomFuncFiles = new List<string>();
        public string GUID;
        public bool AlphaClipping = false;
        public bool IsTransparent = false;
        public bool Unlit = false;
        public bool DepthTest = true;
        public bool DepthWrite = true;
        public GodotBlendMode BlendMode = GodotBlendMode.GBM_MIX;
        public GodotCullMode CullMode = GodotCullMode.GCM_BACK;
        public object GetConnectingNodeAndSlot(object Slot, ref object ConnectingSlot )
        {
            for (int i = 0; i < EdgeList.Count; i++)
            {
                object Edge = EdgeList[i];
                object m_InputSlot = GetValueForFieldOrProperty(Edge, "m_InputSlot", BindingFlags.Instance | BindingFlags.NonPublic);
                object m_OutputSlot = GetValueForFieldOrProperty(Edge, "m_OutputSlot", BindingFlags.Instance | BindingFlags.NonPublic);
                object InputSlotm_Node = GetValueForFieldOrProperty(m_InputSlot, "m_Node", BindingFlags.Instance | BindingFlags.NonPublic);
                object OutputSlotm_Node = GetValueForFieldOrProperty(m_OutputSlot, "m_Node", BindingFlags.Instance | BindingFlags.NonPublic);

                object InputSlotNode = GetValueForFieldOrProperty(InputSlotm_Node, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);
                object OutputSlotNode = GetValueForFieldOrProperty(OutputSlotm_Node, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);

                object InputSlotReference = GetValueForFieldOrProperty(m_InputSlot, "slot", BindingFlags.Instance | BindingFlags.NonPublic);
                object OutputSlotReference = GetValueForFieldOrProperty(m_OutputSlot, "slot", BindingFlags.Instance | BindingFlags.NonPublic);

                object Ret = null;
                if (InputSlotReference == Slot)
                {
                    ConnectingSlot = OutputSlotReference;
                    Ret = OutputSlotNode;
                }
                if (OutputSlotReference == Slot)
                {
                    ConnectingSlot = InputSlotReference;
                    Ret = InputSlotNode;
                }
                if ( Ret != null )
                {
                    Type NodeType = Ret.GetType();
                    if ( NodeType.FullName.CompareTo( "UnityEditor.ShaderGraph.RedirectNodeData") == 0 )
                    {
                        var Slots = GetNodeSlots( Ret );
                        var UTGSlots = AnalyzeSlots( Slots, null );
                        UTGSlot InSlot = UTGSlots[0];//GodotShaderNode.GetSlot( "In", UTGSlots );
                        Ret = GetConnectingNodeAndSlot( InSlot.Slot, ref ConnectingSlot );
                    }
                    
                    return Ret;
                }
            }

            return null;
        }
        public UTGSlot GetConnectingSlot(object Slot)
        {
            object ConnectingSlot = null;
            object ConnectingNode = GetConnectingNodeAndSlot( Slot, ref ConnectingSlot );
            if (ConnectingNode == null)
                return null;
        
            Type GraphNodeType = ConnectingNode.GetType();
            Type AbstractMaterialNodeType = GetTypeInHierarchyByName(GraphNodeType, "AbstractMaterialNode");
            List<object> m_Slots = GetListThroughReflection("m_Slots", ConnectingNode, AbstractMaterialNodeType);
            List<UTGSlot> ProcessedSlots = AnalyzeSlots( m_Slots, this );
            
            for(int i=0; i< ProcessedSlots.Count; i++)
            {
                UTGSlot slot = ProcessedSlots[i];
                if (slot.Slot == ConnectingSlot)
                {
                    return slot;
                }
            }

            return null;
        }
        public bool AddShaderConnection(GodotShaderNode SourceNode, int SourceNodeOutput, GodotShaderNode DestinationNode, int DestinationNodeInput, ShaderType ShaderType)
        {
            ShaderConnection NewConnection = new ShaderConnection();
            if (SourceNode != null)
            {
                NewConnection.SourceNodeObject = SourceNode.NodeObject;
                NewConnection.SourceNode = SourceNode.ID;
                NewConnection.SourceNodeOutput = SourceNodeOutput;
            }
            if (DestinationNode != null)
            {
                NewConnection.DestinationNodeObject = DestinationNode.NodeObject;
                NewConnection.DestinationNode = DestinationNode.ID;
                NewConnection.DestinationNodeInput = DestinationNodeInput;
            }

            NewConnection.ShaderType = ShaderType;

            bool Reordered = ReorderConnection(SourceNode, DestinationNode, NewConnection);

            AddShaderConnection(NewConnection);
            return true;
        }
        public bool AddShaderConnection(ShaderConnection NewConnection)
        {
            for (int i = 0; i < Connections.Count; i++)
            {
                if (Connections[i].SourceNode == NewConnection.SourceNode &&
                     Connections[i].SourceNodeOutput == NewConnection.SourceNodeOutput &&
                     Connections[i].DestinationNode == NewConnection.DestinationNode &&
                     Connections[i].DestinationNodeInput == NewConnection.DestinationNodeInput &&
                     Connections[i].ShaderType == NewConnection.ShaderType)
                {
                    return false;
                }
            }
            if (NewConnection.DestinationNodeInput == -1)
                return false;

            Connections.Add(NewConnection);
            return true;
        }
        public ShaderConnection GetShaderConnection(int DestinationNode, int DestinationNodeInput, ShaderType Type)
        {
            for (int i = 0; i < Connections.Count; i++)
            {
                if (Connections[i].DestinationNode == DestinationNode &&
                     Connections[i].DestinationNodeInput == DestinationNodeInput &&
                     Connections[i].ShaderType == Type)
                {
                    return Connections[i];
                }
            }

            return null;
        }
        public static void MakeShaderTypeParameterName(ref string Name, ShaderType ShaderType)
        {
            if (ShaderType == ShaderType.VERTEX)
            {
                Name += "_VS";
            }
            else if (ShaderType == ShaderType.FRAGMENT)
            {
                Name += "_PS";
            }
        }
        public bool EnsureParametersHaveDifferentNamesForDifferentStages(ShaderParameter NewShaderParameter)
        {
            bool FoundWithDifferentShaderStage = false;
            for (int i = 0; i < OriginalParameterNames.Count; i++)
            {
                if (NewShaderParameter.Name == OriginalParameterNames[i])
                {
                    if (NewShaderParameter.ShaderType != OriginalParameterShaderTypes[i])
                    {
                        FoundWithDifferentShaderStage = true;
                    }
                }
            }
            if (FoundWithDifferentShaderStage)
            {
                MakeShaderTypeParameterName(ref NewShaderParameter.Name, NewShaderParameter.ShaderType);
                return true;
            }

            return false;
        }
        public ShaderParameter GetShaderParameter(string Name, ShaderParameterType Type, ShaderType ShaderType)
        {
            for (int i = 0; i < ShaderParameters.Count(); i++)
            {
                if (ShaderParameters[i].Name.Equals(Name) && ShaderParameters[i].ShaderType == ShaderType)
                {
                    return ShaderParameters[i];
                }
            }
            return null;
        }
        public ShaderParameter AddShaderParameter(ref string InName, string ReferenceName, ShaderParameterType Type, ShaderType ShaderType, float DefaultFloat, Vector4 DefaultVector, Texture DefaultTexture,
                                          bool DefaultBool, int DefaultInt, GodotShaderNode Node)
        {
            string Name = InName;
            //Only adding it once
            for (int i = 0; i < OriginalParameterNames.Count(); i++)
            {
                if (OriginalParameterNames[i] == Name && OriginalParameterShaderTypes[i] == ShaderType)
                {
                    if (ShaderParameters.Count > i)
                        return ShaderParameters[i];

                    return null;
                }
            }

            ShaderParameter NewShaderParameter = new ShaderParameter();
            NewShaderParameter.Name = NewShaderParameter.SanitizedName = Name;
            NewShaderParameter.ReferenceName = ReferenceName;
            SanitizeParamName(ref NewShaderParameter.SanitizedName);
            NewShaderParameter.Type = Type;
            NewShaderParameter.ShaderType = ShaderType;
            NewShaderParameter.DefaultFloat = DefaultFloat;
            NewShaderParameter.DefaultVector = DefaultVector;
            NewShaderParameter.DefaultTexture = DefaultTexture;
            NewShaderParameter.DefaultBool = DefaultBool;
            NewShaderParameter.DefaultInt = DefaultInt;
            NewShaderParameter.Node = Node;

            InName = NewShaderParameter.SanitizedName;

            if (EnsureParametersHaveDifferentNamesForDifferentStages(NewShaderParameter))
            {
                InName = NewShaderParameter.Name;
            }
            ShaderParameters.Add(NewShaderParameter);
            OriginalParameterNames.Add(Name);
            OriginalParameterShaderTypes.Add(ShaderType);

            return NewShaderParameter;
        }
        public GodotShaderNode GetShaderNode(object NodeObject)
        {
            string PropertyNameIn = null;
            string TypeNameIn = null;
            GetPropertyNameAndType(NodeObject, ref PropertyNameIn, ref TypeNameIn);

            for (int i = 0; i < ShaderNodes.Count; i++)
            {
                GodotShaderNode ShaderNode = ShaderNodes[i];
                if (ShaderNode.NodeObject == NodeObject)
                    return ShaderNode;
                //Don't share properties between subgraph instances !
                if (ShaderNode.ParentGraphNode != null)
                    continue;
                string PropertyNameCompare = null;
                string TypeNameCompare = null;
                GetPropertyNameAndType(ShaderNode.NodeObject, ref PropertyNameCompare, ref TypeNameCompare);
                if (PropertyNameIn != null && TypeNameIn != null &&
                    PropertyNameCompare != null && TypeNameCompare != null)
                {
                    if (PropertyNameIn.Equals(PropertyNameCompare) && TypeNameIn.Equals(TypeNameCompare))
                        return ShaderNode;
                }

            }

            return null;
        }
        public GodotShaderNode GetShaderNode(object NodeObject, int NodeID)
        {
            for (int i = 0; i < ShaderNodes.Count; i++)
            {
                GodotShaderNode ShaderNode = ShaderNodes[i];
                if (ShaderNode.NodeObject == NodeObject &&
                    ShaderNode.ID == NodeID)
                    return ShaderNode;
            }

            return null;
        }
        public GodotShaderNode GetShaderNode(int NodeID)
        {
            for (int i = 0; i < ShaderNodes.Count; i++)
            {
                GodotShaderNode ShaderNode = ShaderNodes[i];
                if (ShaderNode.ID == NodeID)
                    return ShaderNode;
            }

            return null;
        }
        public void ModifyNodeForVertexShader(GodotShaderNode Node)
        {
            Node.ShaderType = ShaderType.VERTEX;

            string Params = "";
            if (Node.NodeObject != null)
            {
                string NodeTypeString = GetShaderNodeType(this, ref Params, Node, Node.NodeObject);

                Node.DefinitionText = string.Format( CultureInfo.InvariantCulture,"[sub_resource type=\"{0}\" id=\"{1}\"]\n", NodeTypeString, Node.GUID);
                Node.DefinitionText += Params;

                if (Node.GraphConfigText != null && Node.GraphConfigText.Length > 0)
                {
                    Node.GraphConfigText = Node.GraphConfigText.Replace("/fragment", "/vertex");
                }
            }
        }
        public void MarkVertexShaderNodes()
        {
            bool StillEvaluating = true;
            while (StillEvaluating)
            {
                StillEvaluating = false;
                for (int i = 0; i < Connections.Count; i++)
                {
                    ShaderConnection Connection = Connections[i];
                    GodotShaderNode SourceNode = GetShaderNode(Connection.SourceNodeObject, Connection.SourceNode);
                    if ( SourceNode == null )
                        SourceNode = GetShaderNode( Connection.SourceNode );//This is how we get the reroute node
                    
                    GodotShaderNode DestinationNode = GetShaderNode(Connection.DestinationNodeObject, Connection.DestinationNode);
                    if ( DestinationNode == null )
                        DestinationNode = GetShaderNode( Connection.DestinationNode );//This is how we get the reroute node
                    if (DestinationNode != null)
                    {
                        if ( ExportVertexShaders )
                        { 
                            if (DestinationNode.ShaderType == ShaderType.VERTEX)
                            {
                                if (SourceNode != null && SourceNode.ShaderType != ShaderType.VERTEX)
                                {
                                    ModifyNodeForVertexShader(SourceNode);
                                    StillEvaluating = true;
                                }
                            }
                            if ((DestinationNode.ShaderType == ShaderType.VERTEX || (SourceNode != null && SourceNode.ShaderType == ShaderType.VERTEX)) &&
                                Connection.ShaderType != ShaderType.VERTEX)
                            {
                                Connection.ShaderType = ShaderType.VERTEX;
                                StillEvaluating = true;
                            }
                        }
                        else
                        {
                            //Remove vertex connections because they'll connect to fragment outputs
                            if (DestinationNode.ShaderType == ShaderType.VERTEX)
                            {
                                Connections.RemoveAt( i );
                                i--;
                            }
                        }
                    }
                }
            }
        }
        public void RemoveShaderNodeType(string Type)
        {
            for (int i = 0; i < ShaderNodes.Count; i++)
            {
                GodotShaderNode Node = ShaderNodes[i];
                if (Node.NodeObject == null)
                    continue;
                var NodeType = Node.NodeObject.GetType();
                if (NodeType.Name == Type)
                {
                    ShaderNodes.RemoveAt(i);
                    i--;
                }
            }
        }
        public void RouteSubgraphConnections()
        {
#if UNITY_SHADER_GRAPH
            //This needs to be done after I know all connections
            for (int i = 0; i < ShaderNodes.Count; i++)
            {
                GodotShaderNode Node = ShaderNodes[i];
                if (Node.NodeObject == null)
                    continue;
                var NodeType = Node.NodeObject.GetType();
                if (NodeType.Name == "PropertyNode")
                {
                    if (IsSubGraph(Node.GraphObject))
                    {
                        object m_Property = GetValueForFieldOrProperty(Node.NodeObject, "m_Property", BindingFlags.Instance | BindingFlags.NonPublic);
                        object PropertyValue = GetValueForFieldOrProperty(m_Property, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);
                        object displayName = GetValueForFieldOrProperty(PropertyValue, "displayName", BindingFlags.Instance | BindingFlags.NonPublic);

                        UTGSlot Slot = Node.ParentGraphNode.GetSlotByNameAndInput(displayName.ToString(), true );
                        if (Slot == null)
                        {
                            Debug.LogError("Property " + displayName.ToString() + " not found in inputs for " + FilePath);
                        }
                        else
                        {
                            for (int c = 0; c < Connections.Count; c++)
                            {
                                var Connection = Connections[c];
                                if (Connection.DestinationNode == Node.ParentGraphNode.ID &&
                                    Connection.DestinationNodeInput == Slot.InputIndex)
                                {
                                    Connection.DestinationNode = Node.ID;
                                    Connection.DestinationNodeInput = 0;
                                }
                            }
                        }
                    }
                }
                if (NodeType.Name == "SubGraphOutputNode")
                {
                    for (int s = 0; s < Node.ProcessedSlots.Count; s++)
                    {
                        UTGSlot Slot = Node.ProcessedSlots[s];
                        string Params = "";
                        string NewNodeType = GenerateRerouteNode(Slot.GetVariableType(), ref Params);
                        GodotShaderNode OutputNode = GenerateShaderNode(this, null, null,
                            Node.ShaderType, NewNodeType, Node.GraphObject, Node.ParentGraphNode, (int)Node.Position.x, (int)Node.Position.y + 200 * s);
                        OutputNode.DefinitionText += Params;

                        for (int c = 0; c < Connections.Count; c++)
                        {
                            var Connection = Connections[c];
                            if (Connection.SourceNode == Node.ParentGraphNode.ID &&
                                Connection.SourceNodeOutput == Slot.InputIndex)
                            {
                                Connection.SourceNode = OutputNode.ID;
                                Connection.SourceNodeOutput = 0;
                            }
                            if (Connection.DestinationNode == Node.ID &&
                               Connection.DestinationNodeInput == Slot.InputIndex)
                            {
                                Connection.DestinationNode = OutputNode.ID;
                                Connection.DestinationNodeInput = 0;
                            }
                        }
                    }
                }
            }            
#endif
        }       
    }

    public static void FindSourceConnections(GodotShaderData shaderData, int nodeID, List<ShaderConnection> outConnections)
    {
        for (int i = 0; i < shaderData.Connections.Count; i++)
        {
            ShaderConnection connection = shaderData.Connections[i];
            if (connection.DestinationNode == nodeID)
            {
                outConnections.Add(connection);
            }
        }
    }
    public static void LoopThroughInputs(GodotShaderData shaderData, ShaderConnection connection,
                           List<ShaderConnection> connectionStack, ref ShaderConnection loopConnection)
    {
        for (int c = 0; c < connectionStack.Count; c++)
        {
            if (connectionStack[c] == connection)
            {
                loopConnection = connection;
                return;
            }
        }

        connectionStack.Add(connection);

        var inputConnections = new List<ShaderConnection>();
        FindSourceConnections(shaderData, connection.SourceNode, inputConnections);

        for (int c = 0; c < inputConnections.Count; c++)
        {
            var childStack = new List<ShaderConnection>(connectionStack);
            LoopThroughInputs(shaderData, inputConnections[c], childStack, ref loopConnection);
        }
    }
    public static void DetectLoops(GodotShaderData shaderData)
    {
        var outputNodes = new List<ShaderConnection>();
        FindSourceConnections(shaderData, 0, outputNodes);

        for (int i = 0; i < outputNodes.Count; i++)
        {
            ShaderConnection connection = outputNodes[i];
            var connectionStack = new List<ShaderConnection>();
            ShaderConnection loopConnection = null;

            LoopThroughInputs(shaderData, connection, connectionStack, ref loopConnection);

            if (loopConnection != null)
            {
                GodotShaderNode sourceNode = shaderData.GetShaderNode(loopConnection.SourceNode);
                GodotShaderNode destinationNode = shaderData.GetShaderNode(loopConnection.DestinationNode);

                Debug.LogError("Shadergraph " + shaderData.FilePath + " has looping connection: " + sourceNode.ID + " -> " + destinationNode.ID + " this will crash Godot !");
                // do something with sourceNode and destinationNode
            }
        }
    }
    public static UTGSlot GetSlot(object Node, string TargetSlotName, GodotShaderData ShaderData)
    {
        var NodeType = Node.GetType();
        Type AbstractMaterialNodeType = GetTypeInHierarchyByName(NodeType, "AbstractMaterialNode");
        List<object> m_Slots = GetListThroughReflection("m_Slots", Node, AbstractMaterialNodeType);

        int OutputIndex = 0;
        int InputIndex = 0;

        for (int s = 0; s < m_Slots.Count; s++)
        {
            object Slot = m_Slots[s];
            var SlotValue = GetValueForFieldOrProperty(Slot, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);
            Type SlotValueType = SlotValue.GetType();
            Type MaterialSlotType = GetTypeInHierarchyByName(SlotValueType, "MaterialSlot");
            string SlotName = GetValueForFieldOrProperty(SlotValue, MaterialSlotType, "m_DisplayName", BindingFlags.Instance | BindingFlags.NonPublic) as string;
            UTGSlot NewSlot = GenerateUTGSlotFor(Slot, ref InputIndex, ref OutputIndex, ShaderData);

            if (SlotName == TargetSlotName)
                return NewSlot;
        }

        return null;
    }
    public static UTGSlot GenerateUTGSlotFor( object Slot, ref int InputIndex, ref int OutputIndex, GodotShaderData ShaderData )
    {
        var SlotValue = GetValueForFieldOrProperty(Slot, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);
        return GenerateUTGSlotForInner( SlotValue, ref InputIndex, ref OutputIndex, ShaderData);
    }
    public static UTGSlot GenerateUTGSlotForInner( object SlotValue, ref int InputIndex, ref int OutputIndex, GodotShaderData ShaderData )
    {
        UTGSlot NewSlot = new UTGSlot();
        Type SlotValueType = SlotValue.GetType();
        Type MaterialSlotType = GetTypeInHierarchyByName(SlotValueType, "MaterialSlot");
        object valueType = GetValueForFieldOrProperty(SlotValue, MaterialSlotType, "valueType", BindingFlags.Instance | BindingFlags.NonPublic);
        NewSlot.ValueType = valueType.ToString();
        object m_SlotType = GetValueForFieldOrProperty(SlotValue, MaterialSlotType, "m_SlotType", BindingFlags.Instance | BindingFlags.NonPublic);
        NewSlot.Name = GetValueForFieldOrProperty(SlotValue, MaterialSlotType, "m_DisplayName", BindingFlags.Instance | BindingFlags.NonPublic) as string;
        NewSlot.isConnected = (bool)GetValueForFieldOrProperty(SlotValue, MaterialSlotType, "isConnected", BindingFlags.Instance | BindingFlags.NonPublic);
        NewSlot.Slot = SlotValue;

        if (m_SlotType.ToString() == "Input")
        {
            NewSlot.Input = true;
            NewSlot.InputIndex = InputIndex;
            InputIndex++;
        }
        else
        {
            NewSlot.OutputIndex = OutputIndex;
            OutputIndex++;
        }

        string FieldName = "m_Value";// "defaultValue";
        if (NewSlot.ValueType == "Vector1")
        {
            Type ScalarSlotType = GetTypeInHierarchyByName(SlotValueType, "Vector1MaterialSlot");
            if (ScalarSlotType == null || SlotValue == null)
            {
                if (ShaderData != null )
                    Debug.LogError("Unknown Material Slot : " + SlotValueType.ToString() + " for shader " + ShaderData.FilePath);
                return null;
            }
            float defaultValue = (float)GetValueForFieldOrProperty(SlotValue, ScalarSlotType, FieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            NewSlot.Vec4.x = defaultValue;
        }
        if (NewSlot.ValueType == "Vector2")
        {
            Type Vector2SlotType = GetTypeInHierarchyByName(SlotValueType, "Vector2MaterialSlot");
            Vector2 defaultValue = (Vector2)GetValueForFieldOrProperty(SlotValue, Vector2SlotType, FieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            NewSlot.Vec4 = new Vector4(defaultValue.x, defaultValue.y);
        }
        if (NewSlot.ValueType == "Vector3")
        {
            Type Vector3SlotType = GetTypeInHierarchyByName(SlotValueType, "Vector3MaterialSlot");
            Vector3 defaultValue = (Vector3)GetValueForFieldOrProperty(SlotValue, Vector3SlotType, FieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            NewSlot.Vec4 = new Vector4(defaultValue.x, defaultValue.y, defaultValue.z);
        }
        if (NewSlot.ValueType == "Vector4")
        {
            Type Vector4SlotType = GetTypeInHierarchyByName(SlotValueType, "Vector4MaterialSlot");
            Vector4 defaultValue = (Vector4)GetValueForFieldOrProperty(SlotValue, Vector4SlotType, FieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            NewSlot.Vec4 = defaultValue;
        }
#if UNITY_SHADER_GRAPH
        if (NewSlot.ValueType == "Texture2D")
        {
            Type SlotType = GetTypeInHierarchyByName(SlotValueType, "Texture2DInputMaterialSlot");
            SerializableTexture defaultValue = (SerializableTexture)GetValueForFieldOrProperty(SlotValue, SlotType, "m_Texture", BindingFlags.Instance | BindingFlags.NonPublic);
            if ( defaultValue != null )
                NewSlot.texture = defaultValue.texture;
        }
        if (NewSlot.ValueType == "Cubemap")
        {
            Type SlotType = GetTypeInHierarchyByName(SlotValueType, "CubemapInputMaterialSlot");
            SerializableCubemap SerializableCubemap = (SerializableCubemap)GetValueForFieldOrProperty(SlotValue, SlotType, "m_Cubemap", BindingFlags.Instance | BindingFlags.NonPublic);
            if ( SerializableCubemap != null )
                NewSlot.texture = SerializableCubemap.cubemap;
        }
        if (NewSlot.ValueType == "Texture3D")
        {
            Type SlotType = GetTypeInHierarchyByName(SlotValueType, "Texture3DInputMaterialSlot");            
            SerializableTexture defaultValue = (SerializableTexture)GetValueForFieldOrProperty(SlotValue, SlotType, "m_Texture", BindingFlags.Instance | BindingFlags.NonPublic);
            if ( defaultValue != null )
                NewSlot.texture = defaultValue.texture;
        }
        if (NewSlot.ValueType == "Texture2DArray")
        {
            Type SlotType = GetTypeInHierarchyByName(SlotValueType, "Texture2DArrayInputMaterialSlot");
            SerializableTextureArray defaultValue = (SerializableTextureArray)GetValueForFieldOrProperty(SlotValue, SlotType, "m_TextureArray", BindingFlags.Instance | BindingFlags.NonPublic);
            if (defaultValue != null)
                NewSlot.texture = defaultValue.textureArray;
        }
#endif
        if (SlotValueType.Name == "UVMaterialSlot")
        {
            object ChannelValue = GetValueForFieldOrProperty(SlotValue, "m_Channel", BindingFlags.Instance | BindingFlags.NonPublic);
#if UNITY_SHADER_GRAPH
                NewSlot.Channel = (UnityEditor.ShaderGraph.Internal.UVChannel)ChannelValue;
#endif
            NewSlot.ValueType = "UVMaterialSlot";
        }
        if (SlotValueType.Name == "SamplerStateMaterialSlot")
        {
            //object ChannelValue = GetValueForFieldOrProperty( SlotValue, "m_Channel", BindingFlags.Instance | BindingFlags.NonPublic );
            //NewSlot.Channel = (UnityEditor.ShaderGraph.Internal.UVChannel)ChannelValue;
        }
        if (NewSlot.ValueType == "DynamicVector")
        {
            Type DynamicVectorSlotType = GetTypeInHierarchyByName(SlotValueType, "DynamicVectorMaterialSlot");
            object m_ConcreteValueType = GetValueForFieldOrProperty(SlotValue, DynamicVectorSlotType, "m_ConcreteValueType", BindingFlags.Instance | BindingFlags.NonPublic);
            //This is needed because Power nodes with single float can transform to vector4 with default values that are not visible
            NewSlot.ValueType = m_ConcreteValueType.ToString();//"Vector4";
            object defaultValue = GetValueForFieldOrProperty(SlotValue, DynamicVectorSlotType, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);
            NewSlot.Vec4 = (Vector4)defaultValue;
        }
        if (NewSlot.ValueType == "Dynamic")
        {
            Type DynamicSlotType = GetTypeInHierarchyByName(SlotValueType, "DynamicValueMaterialSlot");
            object m_ConcreteValueType = GetValueForFieldOrProperty(SlotValue, DynamicSlotType, "m_ConcreteValueType", BindingFlags.Instance | BindingFlags.NonPublic);
            NewSlot.ValueType = m_ConcreteValueType.ToString();
            object defaultValue = GetValueForFieldOrProperty(SlotValue, DynamicSlotType, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);
            Matrix4x4 mat4 = (Matrix4x4)defaultValue;
                                      //00      01      02        03
            NewSlot.Vec4 = new Vector4(mat4[0], mat4[4], mat4[8], mat4[12]);
        }
        if (NewSlot.ValueType == "Boolean")
        {
            Type SlotType = GetTypeInHierarchyByName(SlotValueType, "BooleanMaterialSlot");           
            bool defaultValue = (bool)GetValueForFieldOrProperty(SlotValue, SlotType, FieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            NewSlot.Vec4.x = defaultValue ? 1.0f : 0.0f;
        }
        return NewSlot;
    }
    public static object GetPropertyValue( object GraphNode )
    {
        if (GraphNode == null)
            return null;
        object m_Value = GraphNode;
        Type GraphNodeType = GraphNode.GetType();
        var m_TypeStr = GraphNodeType.FullName;
      
        if ( m_TypeStr == "UnityEditor.ShaderGraph.PropertyNode" )
        {
            object m_Property = GetValueForFieldOrProperty(m_Value, "m_Property", BindingFlags.Instance | BindingFlags.NonPublic);
            object PropertyValue = GetValueForFieldOrProperty(m_Property, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);
            object displayName = GetValueForFieldOrProperty(PropertyValue, "displayName", BindingFlags.Instance | BindingFlags.NonPublic);
            object PropertyValue_m_Value = GetValueForFieldOrProperty(PropertyValue, PropertyValue.GetType().BaseType, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);
            return PropertyValue_m_Value;
        }
        return null;
    }
    static void GetPropertyNameAndType( object GraphNode, ref string PropertyName, ref string TypeName )
    {
        PropertyName = null;
        TypeName = null;
        if (GraphNode == null)
            return;
        object m_Value = GraphNode;
        Type GraphNodeType = GraphNode.GetType();
        var m_TypeStr = GraphNodeType.FullName;
      
        if ( m_TypeStr == "UnityEditor.ShaderGraph.PropertyNode" )
        {
            object m_Property = GetValueForFieldOrProperty(m_Value, "m_Property", BindingFlags.Instance | BindingFlags.NonPublic);
            object PropertyValue = GetValueForFieldOrProperty(m_Property, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);
            object displayName = GetValueForFieldOrProperty(PropertyValue, "displayName", BindingFlags.Instance | BindingFlags.NonPublic);
            object PropertyValue_m_Value = GetValueForFieldOrProperty(PropertyValue, PropertyValue.GetType().BaseType, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);
            //if( IsSubGraph( ShaderNode.GraphObject ) )
            //{
            //    return GenerateRerouteNode(GodotShaderVariableType.GSVT_VECTOR4, ref Params);
            //}
            PropertyName = displayName.ToString();
            TypeName = PropertyValue.GetType().FullName;
        }
    }
    public static List<object> GetNodeSlots( object GraphNode )
    {
        Type GraphNodeType = GraphNode.GetType();
        Type AbstractMaterialNodeType = GetTypeInHierarchyByName(GraphNodeType, "AbstractMaterialNode");
        List<object> m_Slots = GetListThroughReflection("m_Slots", GraphNode, AbstractMaterialNodeType);
        return m_Slots;
    }
    static List<UTGSlot> AnalyzeSlots(List<object> SlotsList, GodotShaderData ShaderData)
    {
        List<UTGSlot> ProcessedSlotList = new List<UTGSlot>();
        int OutputIndex = 0;
        int InputIndex = 0;
        for (int s = 0; s < SlotsList.Count; s++)
        {
            object Slot = SlotsList[s];
            UTGSlot NewSlot = GenerateUTGSlotFor(Slot, ref InputIndex, ref OutputIndex, ShaderData);
            ProcessedSlotList.Add(NewSlot);
        }

        return ProcessedSlotList;
    }
    public static int RuntimeNodeIncrementor = 0;
    public static GodotShaderNode GenerateShaderNode(GodotShaderData ShaderData, List<UTGSlot> ProcessedSlots, object GraphNode,
            ShaderType ShaderType, string OverrideNodeType, object GraphObject, GodotShaderNode ParentGraphNode, int PosX = 0, int PosY = 0)
    {
        GodotShaderNode NewShaderNode = new GodotShaderNode();
        NewShaderNode.ProcessedSlots = ProcessedSlots;
        NewShaderNode.ID = ShaderData.ShaderNodes.Count + 2;
        NewShaderNode.GraphObject = GraphObject;
        NewShaderNode.ParentGraphNode = ParentGraphNode;
        ShaderData.ShaderNodes.Add(NewShaderNode);

        NewShaderNode.ShaderType = ShaderType;
        NewShaderNode.Position = new Vector2(PosX, PosY);
        NewShaderNode.NodeObject = GraphNode;
        string Params = "";
        string NodeTypeString = OverrideNodeType;

        if (GraphNode != null)
        {
            NodeTypeString = GetShaderNodeType(ShaderData, ref Params, NewShaderNode, GraphNode);
            if (NodeTypeString == null)
            {
                ShaderData.ShaderNodes.Remove(NewShaderNode);
                return null;
            }
            NewShaderNode.GUID = GetGodotGUID(GraphNode.GetHashCode());
        }
        else
        {
            NewShaderNode.GUID = GetGodotGUID(OverrideNodeType.GetHashCode() + RuntimeNodeIncrementor);
            RuntimeNodeIncrementor++;
        }
        NewShaderNode.GUID = NodeTypeString + "_" + NewShaderNode.GUID;

        NewShaderNode.DefinitionText = string.Format( CultureInfo.InvariantCulture,"[sub_resource type=\"{0}\" id=\"{1}\"]\n", NodeTypeString, NewShaderNode.GUID);
        NewShaderNode.DefinitionText += Params;
        NewShaderNode.OutputIndex = 0;

        return NewShaderNode;
    }
    public static GodotShaderNode GenerateShaderNode( GodotShaderData ShaderData, object GraphNode, ShaderType ShaderType, object GraphObject, GodotShaderNode ParentGraphNode )
    {
        Type GraphNodeType = GraphNode.GetType();
        var m_TypeStr = GraphNodeType.FullName;

        Rect rect = new Rect();
        Type AbstractMaterialNodeType = GetTypeInHierarchyByName(GraphNodeType, "AbstractMaterialNode");
        if (AbstractMaterialNodeType != null)
        {
            PropertyInfo RectProp = AbstractMaterialNodeType.GetProperty("UnityEditor.ShaderGraph.IRectInterface.rect", BindingFlags.Instance | BindingFlags.NonPublic);
            object RectVal = RectProp.GetValue(GraphNode);
            rect = (Rect)RectVal;
        }

        List<object> m_Slots = GetListThroughReflection("m_Slots", GraphNode, AbstractMaterialNodeType);
        List<UTGSlot> ProcessedSlots = AnalyzeSlots(m_Slots, ShaderData);
        ShaderType Type = ShaderType.FRAGMENT;

        return GenerateShaderNode( ShaderData, ProcessedSlots, GraphNode, Type, null, GraphObject, ParentGraphNode, (int)rect.position.x, (int)rect.position.y);
    }
    public static string GenerateExternalResourceReference(string Type, string GUID, string ResourcePath, string LocalGUID)
    {
        string Text = string.Format( CultureInfo.InvariantCulture,
                  "[ext_resource type=\"{0}\" uid=\"uid://{1}\" path=\"res://{2}\" id=\"{3}\"]\n",
                  Type, GUID, ResourcePath, LocalGUID);
        return Text;
    }
    static public UnityEngine.Object[] GetSelectedAssets()
    {
        //return Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets);
        return Selection.objects;
    }
    static public void CreateDirectoriesForFile(string FilePath)
    {
        string Folder = GetFolderFromFilePath(FilePath);
        Directory.CreateDirectory(Folder);
    }
    public static string GetCurrentRenderPipeline()
    {
        var rpAsset = GraphicsSettings.defaultRenderPipeline;// renderPipelineAsset;

        if (rpAsset == null)
        {
            return "Built-In";
        }

        var rpType = rpAsset.GetType().ToString();

        if (rpType.Contains("Universal"))
            return "Universal";
        else if (rpType.Contains("HDRenderPipeline"))
            return "HDRP";
        else
            return "Custom";
    }
#if UNITY_SHADER_GRAPH
    //[MenuItem("UnityToGodot/ExportShaderGraphs")]
    //static public void ExportShaderGraphs()
    //{
    //    MissingNodeTypes.Clear();
    //
    //    string path = "Assets/New Shader Graph.shadergraph";
    //    var SelectedAssets = GetSelectedAssets();
    //    foreach (UnityEngine.Object obj in SelectedAssets)
    //    {
    //        path = AssetDatabase.GetAssetPath(obj);
    //        if (!string.IsNullOrEmpty(path) && File.Exists(path) && path.Contains(".shadergraph"))
    //        {
    //            //path = Path.GetFileName( path );
    //            ExportShaderGraph( path );
    //        }
    //    }
    //
    //    if (MissingNodeTypes.Count > 0 )
    //    {
    //        string Text = "Missing ShaderGraph Node Types :\n";
    //        for (int i=0; i< MissingNodeTypes.Count; i++)
    //        {
    //            Text += MissingNodeTypes[i] + "\n";
    //        }
    //        bool Result = EditorUtility.DisplayDialog("Error", Text, "OK" );
    //    }
    //}
    [MenuItem("UnityToGodot/PlaceCubesWithSelectedMaterials")]
    static public void PlaceCubesWithSelectedMaterials()
    {
        UnityEngine.Object[] SelectedAssets = GetSelectedAssets();
        int GridSize = (int)Math.Sqrt((float)SelectedAssets.Length );
        for(int i=0; i< SelectedAssets.Length; i++)
        {
            UnityEngine.Object obj = SelectedAssets[i];
            Material Mat = obj as Material;
            Shader shader = obj as Shader;
            if ( shader != null )
            {
                Mat = new Material( shader );
            }
            if (Mat != null && Mat.shader != null)
            {
                string ShaderName = Mat.shader.name;
                
                GameObject NewCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                NewCube.name = Mat.name + "(" + ShaderName + ")";

                float CubeSize = 1.0f;
                int x = i % GridSize;
                int y = i / GridSize;
                Vector3 Position = new Vector3( x * CubeSize, 0, y * CubeSize);

                NewCube.transform.position = Position;

                // Optional: add a Rigidbody so it falls with physics
                MeshRenderer MR = NewCube.GetComponent<MeshRenderer>();
                if ( MR != null )
                {
                    MR.sharedMaterial = Mat;
                }
            }
        }
    }
    public static Bounds ComputeTotalBounds(GameObject root)
    {
        if (root == null)
            return new Bounds();

        bool hasBounds = false;
        Bounds totalBounds = new Bounds();

        // --- Include MeshRenderers ---
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
        {
            if (!hasBounds)
            {
                totalBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                totalBounds.Encapsulate(renderer.bounds);
            }
        }

        // --- Include SkinnedMeshRenderers ---
        foreach (var skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!hasBounds)
            {
                totalBounds = skinned.bounds;
                hasBounds = true;
            }
            else
            {
                totalBounds.Encapsulate(skinned.bounds);
            }
        }

        // If no renderers found, fallback to transform
        if (!hasBounds)
        {
            totalBounds = new Bounds(root.transform.position, Vector3.zero);
        }

        return totalBounds;
    }
    [MenuItem("UnityToGodot/PlaceSelectedAssetsInScene")]
    static public void PlaceSelectedAssetsInScene()
    {
        UnityEngine.Object[] SelectedAssets = GetSelectedAssets();
        List<GameObject> SpawnedGOs = new List<GameObject> ();
        
        for(int i=0; i< SelectedAssets.Length; i++)
        {
            UnityEngine.Object obj = SelectedAssets[i];
            GameObject GO = obj as GameObject;
            Mesh mesh = obj as Mesh;
            if ( mesh != null )
            {
                GameObject NewGO = new GameObject( mesh.name );
                NewGO.AddComponent<MeshRenderer>();
                MeshFilter MF = NewGO.AddComponent<MeshFilter>();
                MF.sharedMesh = mesh;
                GO = NewGO;
            }
            if ( GO != null )
            {
                GameObject InstanceGO = Instantiate( GO );
                SpawnedGOs.Add( InstanceGO );
            }
        }
        Vector3 Offset = new Vector3( 0, 0, 0 );
        for(int i=0; i<SpawnedGOs.Count; i++)
        {
            GameObject InstanceGO = SpawnedGOs[i];
            InstanceGO.transform.position = Offset;
            Bounds bounds = ComputeTotalBounds( InstanceGO );
            Offset.x += bounds.size.x/2;
            if ( i + 1 < SelectedAssets.Length)
            {
                Bounds BoundsNext = ComputeTotalBounds( SpawnedGOs[i + 1 ] );
                Offset.x += BoundsNext.size.x/2;
            }
        }
    }
    static public object GetGraphFromPath(string path)
    {
        var textGraph = File.ReadAllText(path, Encoding.UTF8);

        Assembly ShaderGraphAssembly = Assembly.GetAssembly(typeof(UnityEditor.ShaderGraph.ColorControl));
        Type GraphDataType = ShaderGraphAssembly.GetType("UnityEditor.ShaderGraph.GraphData");
        var GraphDataTypeCtors = GraphDataType.GetConstructors();
        object graph = GraphDataTypeCtors[0].Invoke(new object[] { });

        Type MultiJsonType = ShaderGraphAssembly.GetType("UnityEditor.ShaderGraph.Serialization.MultiJson");
        var MultiJsonTypeMethods = MultiJsonType.GetRuntimeMethods();
        object[] parameters = { graph, textGraph, null, false };

        Type MultiJsonInternalType = ShaderGraphAssembly.GetType("UnityEditor.ShaderGraph.Serialization.MultiJsonInternal");
        var MultiJsonInternal_Methods = MultiJsonInternalType.GetRuntimeMethods();
        Type[] ParseTypes = new Type[1] { typeof(string) };
        var MultiJsonInternal_Parse = MultiJsonInternalType.GetRuntimeMethod("Parse", ParseTypes);
        var MultiJsonInternal_Deserialize = MultiJsonInternalType.GetMethod("Deserialize"); //MultiJsonInternal_Methods.ToArray()[5];
        object[] ParseParams = new object[1] { textGraph };
        var entries = MultiJsonInternal_Parse.Invoke(null, ParseParams);
        object[] DeserializeParams = new object[3] { graph, entries, false };
        MultiJsonInternal_Deserialize.Invoke(null, DeserializeParams);

        var GraphData_ValidateGraph_Types = new Type[1] { GraphDataType };
        var GraphData_ValidateGraph2 = GraphDataType.GetMethod("ValidateGraph");
        object[] GraphData_ValidateGraph_Params = new object[1] { graph };
        GraphData_ValidateGraph2.Invoke(graph, null);

        return graph;
    }
    static public bool IsSubGraph( object GraphObject )
    {
        object outputNode = GetValueForFieldOrProperty( GraphObject, "outputNode", BindingFlags.Instance | BindingFlags.NonPublic );
        if (outputNode != null)
            return true;
        else
            return false;
    }
    public static void SetTargetProperties(object GraphObject, GodotShaderData ShaderData)
    {
        List<object> AllPotentialTargets = GetListThroughReflection("m_AllPotentialTargets", GraphObject);

        string CurrentRenderPipeline = GetCurrentRenderPipeline();

        for (int i = 0; i < AllPotentialTargets.Count; i++)
        {
            object Item = AllPotentialTargets[i];
            object Target = GetValueForFieldOrProperty(Item, "m_Target", BindingFlags.Instance | BindingFlags.NonPublic);

            object displayName = GetValueForFieldOrProperty(Target, "displayName", BindingFlags.Instance | BindingFlags.NonPublic);

            if (displayName.ToString().Equals(CurrentRenderPipeline))
            {
                object m_ActiveSubTarget = GetValueForFieldOrProperty(Target, "m_ActiveSubTarget", BindingFlags.Instance | BindingFlags.NonPublic);
                object m_ActiveSubTarget_value = GetValueForFieldOrProperty(m_ActiveSubTarget, "value", BindingFlags.Instance | BindingFlags.NonPublic);

                object shaderID = GetValueForFieldOrProperty(m_ActiveSubTarget_value, "shaderID", BindingFlags.Instance | BindingFlags.NonPublic);
                string shaderIDStr = shaderID.ToString();

                object m_AlphaClip = GetValueForFieldOrProperty(Target, "m_AlphaClip", BindingFlags.Instance | BindingFlags.NonPublic);
                object m_AlphaMode = GetValueForFieldOrProperty(Target, "m_AlphaMode", BindingFlags.Instance | BindingFlags.NonPublic);
                object m_SurfaceType = GetValueForFieldOrProperty(Target, "m_SurfaceType", BindingFlags.Instance | BindingFlags.NonPublic);
                object m_ZTestMode = GetValueForFieldOrProperty(Target, "m_ZTestMode", BindingFlags.Instance | BindingFlags.NonPublic);
                object m_ZWriteControl = GetValueForFieldOrProperty(Target, "m_ZWriteControl", BindingFlags.Instance | BindingFlags.NonPublic);

                if (CurrentRenderPipeline == "HDRP")
                {
                    List<object> m_Datas = GetListThroughReflection( "m_Datas", Target );
                    for(int d=0; d<m_Datas.Count; d++)
                    {
                        object m_Data = GetValueForFieldOrProperty(m_Datas[d], "value", BindingFlags.Instance | BindingFlags.NonPublic);
                        if ( m_Data != null && m_Data.GetType().FullName.Contains("SystemData") )
                        {
                            m_SurfaceType = GetValueForFieldOrProperty(m_Data, "m_SurfaceType", BindingFlags.Instance | BindingFlags.NonPublic);
                            object m_OpaqueCullMode = GetValueForFieldOrProperty(m_Data, "m_OpaqueCullMode", BindingFlags.Instance | BindingFlags.NonPublic);
                            object m_AlphaTest = GetValueForFieldOrProperty(m_Data, "m_AlphaTest", BindingFlags.Instance | BindingFlags.NonPublic);
                            object m_DoubleSidedMode = GetValueForFieldOrProperty(m_Data, "m_DoubleSidedMode", BindingFlags.Instance | BindingFlags.NonPublic);

                            if (m_AlphaTest != null )
                            {
                                ShaderData.AlphaClipping = (bool)m_AlphaTest;
                            }
                            if ( m_OpaqueCullMode != null )
                            {
                                ShaderData.CullMode = GetGodotCullMode( m_OpaqueCullMode.ToString() );
                            }
                            if (m_DoubleSidedMode != null )
                            {
                                if ( m_DoubleSidedMode.ToString().Equals("Enabled"))
                                {
                                    ShaderData.CullMode = GodotCullMode.GCM_OFF;
                                }
                            }

                            //On Opaque all those properties are hidden so don't read them
                            if (m_SurfaceType != null && m_SurfaceType.ToString().Equals("Transparent"))
                            {
                                m_AlphaMode = GetValueForFieldOrProperty(m_Data, "m_BlendMode", BindingFlags.Instance | BindingFlags.NonPublic);
                                m_ZTestMode = GetValueForFieldOrProperty(m_Data, "m_ZTest", BindingFlags.Instance | BindingFlags.NonPublic);
                                object m_ZWrite = GetValueForFieldOrProperty(m_Data, "m_ZWrite", BindingFlags.Instance | BindingFlags.NonPublic);
                                if (m_ZWrite != null)
                                    ShaderData.DepthWrite = (bool)m_ZWrite;
                            }
                            break;
                        }
                    }
                }
                if (CurrentRenderPipeline == "Universal")
                {
                    object m_RenderFace = GetValueForFieldOrProperty(Target, "m_RenderFace", BindingFlags.Instance | BindingFlags.NonPublic);
                    if ( m_RenderFace.ToString().Equals("Front"))
                    {
                        ShaderData.CullMode = GodotCullMode.GCM_BACK;
                    }
                    if ( m_RenderFace.ToString().Equals("Back"))
                    {
                        ShaderData.CullMode = GodotCullMode.GCM_FRONT;
                    }
                    if ( m_RenderFace.ToString().Equals("Both"))
                    {
                        ShaderData.CullMode = GodotCullMode.GCM_OFF;
                    }
                }
                //else// if (CurrentRenderPipeline == "Universal")
                {
                    if ( m_AlphaClip != null )
                        ShaderData.AlphaClipping = (bool)m_AlphaClip;
                    if (m_SurfaceType != null )
                        ShaderData.IsTransparent = m_SurfaceType.ToString().Equals("Transparent");
                    if (shaderIDStr != null)
                        ShaderData.Unlit = shaderIDStr.Equals("SG_Unlit");
                    if (m_ZTestMode != null )
                        ShaderData.DepthTest = !m_ZTestMode.Equals("Never");
                    if (m_ZWriteControl != null )
                        ShaderData.DepthWrite = !m_ZWriteControl.Equals("ForceDisabled");
                    if (m_AlphaMode != null && m_AlphaMode.ToString() == "Additive")
                        ShaderData.BlendMode = GodotBlendMode.GBM_ADD;
                }
            }
        }
    }
    public static void ProcessGraphObject( object GraphObject, GodotShaderNode ParentGraphNode, GodotShaderData ShaderData )
    {
        List<object> NodeList = GetListThroughReflection("m_Nodes", GraphObject);
        List<object> NewEdgeList = GetListThroughReflection("m_Edges", GraphObject);
        
        //This needs to be done due to subgraphs adding new connections
        for (int i = 0; i < NewEdgeList.Count; i++)
        {
            ShaderData.EdgeList.Add(NewEdgeList[i]);
        }
        string Text;
        for (int i = 0; i < NodeList.Count; i++)
        {
            object Node = NodeList[i];
            Type NodeType = Node.GetType();
            FieldInfo ValueField = NodeType.GetField("m_Value", BindingFlags.Instance | BindingFlags.NonPublic);
            object GraphNode = ValueField.GetValue(Node);
            Type GraphNodeType = GraphNode.GetType();
            var m_TypeStr = GraphNodeType.FullName;

            Rect rect = new Rect();
            Type AbstractMaterialNodeType = GetTypeInHierarchyByName(GraphNodeType, "AbstractMaterialNode");
            if (AbstractMaterialNodeType != null)
            {
                PropertyInfo RectProp = AbstractMaterialNodeType.GetProperty("UnityEditor.ShaderGraph.IRectInterface.rect", BindingFlags.Instance | BindingFlags.NonPublic);
                object RectVal = RectProp.GetValue(GraphNode);
                rect = (Rect)RectVal;
            }
            List<object> m_Slots = GetListThroughReflection("m_Slots", GraphNode, AbstractMaterialNodeType);
            List<UTGSlot> ProcessedSlots = AnalyzeSlots(m_Slots, ShaderData);
            GodotShaderNode NewShaderNode;
            //Usually output nodes ->
            ShaderType Type = ShaderType.FRAGMENT;
            if (m_TypeStr == "UnityEditor.ShaderGraph.BlockNode")
            {
                object customName = GetValueForFieldOrProperty(GraphNode, "customName", BindingFlags.Instance | BindingFlags.NonPublic);
                object displayName = GetValueForFieldOrProperty(GraphNode, "displayName", BindingFlags.Instance | BindingFlags.NonPublic);
                object descriptor = GetValueForFieldOrProperty(GraphNode, "descriptor", BindingFlags.Instance | BindingFlags.NonPublic);
                object customWidth = GetValueForFieldOrProperty(GraphNode, "customWidth", BindingFlags.Instance | BindingFlags.NonPublic);
                object isCustom = GetValueForFieldOrProperty(descriptor, "isCustom", BindingFlags.Instance | BindingFlags.NonPublic);
                object shaderStage = GetValueForFieldOrProperty(descriptor, "shaderStage", BindingFlags.Instance | BindingFlags.NonPublic);

                if (shaderStage.ToString() == "Vertex")
                    Type = ShaderType.VERTEX;

                bool IsCustomBool = (bool)isCustom;
                if (IsCustomBool)
                {
                    GodotShaderVariableType DataType = GetVariableTypeFromString(customWidth.ToString());

                    NewShaderNode = GenerateShaderNode(ShaderData, ProcessedSlots, null, Type, "VisualShaderNodeVaryingSetter", GraphObject, ParentGraphNode, 
                        (int)rect.position.x, (int)rect.position.y - 150 * (ShaderData.Varyings.Count + 1));
                    Text = string.Format( CultureInfo.InvariantCulture,
                          "varying_name = \"{0}\"\n" +
                          "varying_type = {1}\n",
                          customName.ToString(),
                          (int)DataType);
                    NewShaderNode.DefinitionText += Text;
                    NewShaderNode.NodeObject = GraphNode;

                    ShaderVarying NewShaderVarying = new ShaderVarying();
                    NewShaderVarying.Index = ShaderData.Varyings.Count;
                    NewShaderVarying.Name = customName.ToString();
                    NewShaderVarying.Type = DataType;
                    ShaderData.Varyings.Add(NewShaderVarying);
                }
                else
                {
                    NewShaderNode = new GodotShaderNode();
                    NewShaderNode.ProcessedSlots = ProcessedSlots;
                    NewShaderNode.ID = 0;
                    NewShaderNode.OutputIndex = GetOutputIndexForDisplayName(customName as string, ShaderData );
                    if (ProcessedSlots.Count > 0 && ProcessedSlots[0] != null)
                        ProcessedSlots[0].InputIndex = NewShaderNode.OutputIndex;
                    NewShaderNode.NodeObject = GraphNode;
                    NewShaderNode.GraphObject = GraphObject;
                    NewShaderNode.ParentGraphNode = ParentGraphNode;
                    NewShaderNode.ShaderType = Type;

                    ShaderData.ShaderNodes.Add(NewShaderNode);

                    if ( Type == ShaderType.FRAGMENT && !customName.Equals("NormalTS"))
                    {
                        AddConstantNodesForUnconnectedMaterialOutputs( ShaderData, NewShaderNode, Type );
                    }
                }
                continue;
            }
            else
            {
                GodotShaderNode ExistentShaderNode = ShaderData.GetShaderNode(GraphNode);
                if (ExistentShaderNode == null)
                    NewShaderNode = GenerateShaderNode(ShaderData, ProcessedSlots, GraphNode, Type, null, GraphObject, ParentGraphNode, (int)rect.position.x, (int)rect.position.y);
            }
        }

        //Only process the newly added connections (potentially from a subgraph)
        for (int i = 0; i < NewEdgeList.Count; i++)
        {
            object Edge = NewEdgeList[i];
            
            object m_InputSlot = GetValueForFieldOrProperty(Edge, "m_InputSlot", BindingFlags.Instance | BindingFlags.NonPublic);
            object m_OutputSlot = GetValueForFieldOrProperty(Edge, "m_OutputSlot", BindingFlags.Instance | BindingFlags.NonPublic);
            object InputSlotm_Node = GetValueForFieldOrProperty(m_InputSlot, "m_Node", BindingFlags.Instance | BindingFlags.NonPublic);
            object OutputSlotm_Node = GetValueForFieldOrProperty(m_OutputSlot, "m_Node", BindingFlags.Instance | BindingFlags.NonPublic);

            object InputSlotNode = GetValueForFieldOrProperty(InputSlotm_Node, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);
            object OutputSlotNode = GetValueForFieldOrProperty(OutputSlotm_Node, "m_Value", BindingFlags.Instance | BindingFlags.NonPublic);

            object InputSlotReference = GetValueForFieldOrProperty(m_InputSlot, "slot", BindingFlags.Instance | BindingFlags.NonPublic);
            object OutputSlotReference = GetValueForFieldOrProperty(m_OutputSlot, "slot", BindingFlags.Instance | BindingFlags.NonPublic);

            ShaderConnection NewConnection = new ShaderConnection();
            NewConnection.SourceNodeObject = OutputSlotNode;
            NewConnection.DestinationNodeObject = InputSlotNode;
            NewConnection.ShaderType = ShaderType.FRAGMENT;

            GodotShaderNode SourceShaderNode = ShaderData.GetShaderNode(OutputSlotNode);
            GodotShaderNode DestinationShaderNode = ShaderData.GetShaderNode(InputSlotNode);
            if (SourceShaderNode == null || DestinationShaderNode == null)
                continue;

            //In case I had rerouted property nodes from GetShaderNode
            NewConnection.SourceNodeObject = SourceShaderNode.NodeObject;
            NewConnection.DestinationNodeObject = DestinationShaderNode.NodeObject;

            UTGSlot SourceSlot = SourceShaderNode.GetSlot(OutputSlotReference);
            NewConnection.SourceNode = SourceShaderNode.ID;
            NewConnection.SourceNodeOutput = SourceSlot.OutputIndex;

            if (DestinationShaderNode == null)
            {
                NewConnection.DestinationNode = 0;
                NewConnection.DestinationNodeInput = 0;
            }
            else
            {
                UTGSlot DestinationSlot = DestinationShaderNode.GetSlot(InputSlotReference);
                NewConnection.DestinationNode = DestinationShaderNode.ID;
                if ( DestinationSlot != null )
                    NewConnection.DestinationNodeInput = DestinationSlot.InputIndex;
            }

            bool Reordered = ReorderConnection(SourceShaderNode, DestinationShaderNode, NewConnection);

            ShaderData.AddShaderConnection(NewConnection);            
        }
    }
    public static string GetParentGraphName( GodotShaderNode Node )
    {
        string Ret = "";
        if ( Node.ParentGraphNode != null )
        {
            object Subgraph = GetValueForFieldOrProperty( Node.ParentGraphNode.NodeObject, "asset", BindingFlags.Instance | BindingFlags.NonPublic);
            object SubgraphName = GetValueForFieldOrProperty( Subgraph, "name", BindingFlags.Instance | BindingFlags.NonPublic);
            Ret = SubgraphName.ToString();
        }
        return Ret;
    }
    static public void FixNormalOutput(GodotShaderData ShaderData)
    {
        ShaderConnection OutputConnection = ShaderData.GetShaderConnection(0, 9, ShaderType.FRAGMENT);
        if (OutputConnection != null)
        {
            GodotShaderNode NormalizeNode = GenerateShaderNode(ShaderData, null, null, ShaderType.FRAGMENT, "VisualShaderNodeVectorFunc", null, null, -200, 0 );
            NormalizeNode.DefinitionText += "op_type = 1\n" +
                                            "function = 0\n";

            GodotShaderNode MADNode = GenerateShaderNode(ShaderData, null, null, ShaderType.FRAGMENT, "VisualShaderNodeMultiplyAdd", null, null, -200, 0 );
            MADNode.DefinitionText += "default_input_values = [0, Vector3(0, 0, 0), 1, Vector3(0.5, 0.5, 0.5), 2, Vector3(0.5, 0.5, 0.5)]\n" +
                                     "op_type = 2\n";

            ShaderConnection NormalizeToOutput = new ShaderConnection();
            NormalizeToOutput.SourceNode = NormalizeNode.ID;
            NormalizeToOutput.SourceNodeOutput = 0;
            NormalizeToOutput.DestinationNode = MADNode.ID;
            NormalizeToOutput.DestinationNodeInput = 0;
            NormalizeToOutput.ShaderType = ShaderType.FRAGMENT;
            ShaderData.AddShaderConnection( NormalizeToOutput );

            ShaderConnection MADToOutput = new ShaderConnection();
            MADToOutput.SourceNode = MADNode.ID;
            MADToOutput.SourceNodeOutput = 0;
            MADToOutput.DestinationNode = 0;
            MADToOutput.DestinationNodeInput = 9;
            MADToOutput.ShaderType = ShaderType.FRAGMENT;
            ShaderData.AddShaderConnection( MADToOutput );
        
            OutputConnection.DestinationNode = NormalizeNode.ID;
            OutputConnection.DestinationNodeInput = 0;
        }
    }
    static public void FixSmoothnessOutput(GodotShaderData ShaderData)
    {
        ShaderConnection OutputConnection = ShaderData.GetShaderConnection(0, 3, ShaderType.FRAGMENT);
        if (OutputConnection != null)
        {
            GodotShaderNode ClampNode = GenerateShaderNode(ShaderData, null, null, ShaderType.FRAGMENT, "VisualShaderNodeClamp", null, null, -300, 0);

            GodotShaderNode OneMinusNode = GenerateShaderNode(ShaderData, null, null, ShaderType.FRAGMENT, "VisualShaderNodeFloatFunc", null, null, -200, 0);
            OneMinusNode.DefinitionText += "function = 31\n";

            ShaderConnection ClampToOneMinus = new ShaderConnection();
            ClampToOneMinus.SourceNode = ClampNode.ID;
            ClampToOneMinus.SourceNodeOutput = 0;
            ClampToOneMinus.DestinationNode = OneMinusNode.ID;
            ClampToOneMinus.DestinationNodeInput = 0;
            ClampToOneMinus.ShaderType = ShaderType.FRAGMENT;
            ShaderData.AddShaderConnection(ClampToOneMinus);

            ShaderConnection OneMinusToOutput = new ShaderConnection();
            OneMinusToOutput.SourceNode = OneMinusNode.ID;
            OneMinusToOutput.SourceNodeOutput = 0;
            OneMinusToOutput.DestinationNode = 0;
            OneMinusToOutput.DestinationNodeInput = 3;
            OneMinusToOutput.ShaderType = ShaderType.FRAGMENT;
            ShaderData.AddShaderConnection( OneMinusToOutput );

            OutputConnection.DestinationNode = ClampNode.ID;
            OutputConnection.DestinationNodeInput = 0;
        }
    }
    static public void FixEmissionOutput(GodotShaderData ShaderData)
    {
        ShaderConnection OutputConnection = ShaderData.GetShaderConnection(0, 5, ShaderType.FRAGMENT);
        if (OutputConnection != null)
        {
            //Prevent emission from going negative and making the object pitch black by mistake
            GodotShaderNode ClampNode = GenerateShaderNode(ShaderData, null, null, ShaderType.FRAGMENT, "VisualShaderNodeClamp", null, null, -300, 0);
            ClampNode.DefinitionText += string.Format( CultureInfo.InvariantCulture, "default_input_values = [0, Vector3(0, 0, 0), 1, Vector3(0, 0, 0), 2, Vector3({0}, {0}, {0})]\n" +
                                        "op_type = 4\n", EmissionMultiplier );

            GodotShaderNode MultiplyNode = GenerateShaderNode(ShaderData, null, null, ShaderType.FRAGMENT, "VisualShaderNodeVectorOp", null, null, -200, 0);
            MultiplyNode.DefinitionText += string.Format( CultureInfo.InvariantCulture,"default_input_values = [0, Quaternion(0, 0, 0, 0), 1, Quaternion({1}, {1}, {1}, {1})]\n" +
                                     "op_type = 2\n" +
                                     "operator = {0}\n", (int)GodotVectorOperation.GVO_MULTIPLY, EmissionMultiplier );

            ShaderConnection ClampToMultiplyNode = new ShaderConnection();
            ClampToMultiplyNode.SourceNode = ClampNode.ID;
            ClampToMultiplyNode.SourceNodeOutput = 0;
            ClampToMultiplyNode.DestinationNode = MultiplyNode.ID;
            ClampToMultiplyNode.DestinationNodeInput = 0;
            ClampToMultiplyNode.ShaderType = ShaderType.FRAGMENT;
            ShaderData.AddShaderConnection(ClampToMultiplyNode);

            ShaderConnection MultiplyToOutput = new ShaderConnection();
            MultiplyToOutput.SourceNode = MultiplyNode.ID;
            MultiplyToOutput.SourceNodeOutput = 0;
            MultiplyToOutput.DestinationNode = 0;
            MultiplyToOutput.DestinationNodeInput = OutputConnection.DestinationNodeInput;
            MultiplyToOutput.ShaderType = ShaderType.FRAGMENT;
            ShaderData.AddShaderConnection(MultiplyToOutput);

            OutputConnection.DestinationNode = ClampNode.ID;
            OutputConnection.DestinationNodeInput = 0;
        }
    }
    static public void FixAlphaClippingOutput(GodotShaderData ShaderData)
    {
        ShaderConnection OutputConnection = ShaderData.GetShaderConnection(0, 19, ShaderType.FRAGMENT);
        if (OutputConnection != null)
        {
            //Add a boolean parameter so AlphaClipping can be enabled/disabled per material instead of shader only
            GodotShaderNode BooleanParameterNode = GenerateShaderNode(ShaderData, null, null, ShaderType.FRAGMENT, "VisualShaderNodeBooleanParameter", null, null, -300, 0);

            BooleanParameterNode.DefinitionText += string.Format( CultureInfo.InvariantCulture, 
                  "parameter_name = \"EnableAlphaClipping\"\n" +
                  "expanded_output_ports = [0]\n" +
                  "default_value_enabled = true\n" +
                  "default_value = true\n" );

            GodotShaderNode SwitchNode = GenerateShaderNode(ShaderData, null, null, ShaderType.FRAGMENT, "VisualShaderNodeSwitch", null, null, -200, 0);
            SwitchNode.DefinitionText += string.Format( CultureInfo.InvariantCulture,
                 "default_input_values = [0, true, 1, 0.5, 2, 0.0]\n" +
                 "op_type = {0}\n", (int)GodotShaderVariableType.GSVT_FLOAT );

            ShaderConnection BooleanParameterToSwitchNode = new ShaderConnection();
            BooleanParameterToSwitchNode.SourceNode = BooleanParameterNode.ID;
            BooleanParameterToSwitchNode.SourceNodeOutput = 0;
            BooleanParameterToSwitchNode.DestinationNode = SwitchNode.ID;
            BooleanParameterToSwitchNode.DestinationNodeInput = 0;
            BooleanParameterToSwitchNode.ShaderType = ShaderType.FRAGMENT;
            ShaderData.AddShaderConnection(BooleanParameterToSwitchNode);

            ShaderConnection SwitchNodeToOutput = new ShaderConnection();
            SwitchNodeToOutput.SourceNode = SwitchNode.ID;
            SwitchNodeToOutput.SourceNodeOutput = 0;
            SwitchNodeToOutput.DestinationNode = 0;
            SwitchNodeToOutput.DestinationNodeInput = OutputConnection.DestinationNodeInput;
            SwitchNodeToOutput.ShaderType = ShaderType.FRAGMENT;
            ShaderData.AddShaderConnection(SwitchNodeToOutput);

            OutputConnection.DestinationNode = SwitchNode.ID;
            OutputConnection.DestinationNodeInput = 1;
        }
    }
    static public GodotShaderData ExportShaderGraph( string path )
    {
        var textGraph = File.ReadAllText(path, Encoding.UTF8);
        
        var GraphObject = GetGraphFromPath(path);
        
        GodotShaderData ShaderData = new GodotShaderData();
        ShaderData.FilePath = path;

        SetTargetProperties( GraphObject, ShaderData );
        ProcessGraphObject( GraphObject, null, ShaderData );

        AddGlobalExpression(ShaderData);

        ShaderData.RouteSubgraphConnections();
        ShaderData.MarkVertexShaderNodes();
        FixNormalOutput( ShaderData );
        FixSmoothnessOutput( ShaderData );
        FixEmissionOutput( ShaderData );
        FixAlphaClippingOutput( ShaderData );

        //Must delete nodes after creating all extra nodes due to graphconfig being already written with the ID
        ShaderData.RemoveShaderNodeType("SubGraphOutputNode");
        ShaderData.RemoveShaderNodeType("SubGraphNode");

        string FileData = "";
        int NumTextures = ShaderData.Textures.Count;
        int LoadSteps = ShaderData.ShaderNodes.Count + NumTextures + 1;
        ShaderData.GUID = GetGodotGUID( path.GetHashCode() );

        string Text = string.Format( CultureInfo.InvariantCulture, "[gd_resource type=\"VisualShader\" load_steps={0} format=3 uid=\"uid://{1}\"]\n\n", LoadSteps, ShaderData.GUID );
        FileData += Text;

        ExportTextures( ref FileData, ShaderData.Textures );

        for( int i = 0; i < ShaderData.ShaderNodes.Count; i++ )
        {
            GodotShaderNode Node = ShaderData.ShaderNodes[ i ];
            if( Node.ID == 0 )
                continue;

            FileData += Node.DefinitionText;
            FileData += "\n";
        }

        GodotBlendMode BlendMode = ShaderData.BlendMode;
        string DepthPrepassAlpha = ShaderData.DepthWrite.ToString().ToLower();
        string DepthTestDisabled = (!ShaderData.DepthTest).ToString().ToLower();
        if( BlendMode != GodotBlendMode.GBM_MIX )
        {
            DepthPrepassAlpha = "false";
        }
        int cull_mode = (int)ShaderData.CullMode;
        string ExtraStates = "";
        
        if ( ShaderData.Unlit )
        {
            ExtraStates += "flags/unshaded = true\n";
        }
        Vector2 MaterialNodePosition = new Vector2(0,0);
        Text = string.Format( CultureInfo.InvariantCulture,
			  "[resource]\n" +
              //"graph_offset = Vector2( -523.783, 120.943 )\n" +
              "modes/blend = {0}\n" +
              "modes/cull = {1}\n" +
              "{8}" +
              "flags/depth_prepass_alpha = {2}\n" +
              "flags/depth_test_disabled = {3}\n" +
              "nodes/fragment/0/position = Vector2( {4}, {5} )\n" +
              "nodes/vertex/0/position   = Vector2( {6}, {7} )\n",
			  (int)BlendMode,
			  cull_mode,
			  DepthPrepassAlpha,
			  DepthTestDisabled,
			  MaterialNodePosition.x, MaterialNodePosition.y,
			  MaterialNodePosition.x, MaterialNodePosition.y,
              ExtraStates
               );
        FileData += Text;

        for( int i = 0; i < ShaderData.Varyings.Count; i++ )
        {
            var Varying = ShaderData.Varyings[ i ];
            Text = string.Format( CultureInfo.InvariantCulture,
                      "varyings/{0} = \"{1},{2}\"\n",
                      Varying.Name, Varying.Index, (int)Varying.Type
                    );

            FileData += Text;
        }

        for( int i = 0; i < ShaderData.ShaderNodes.Count; i++ )
        {
            GodotShaderNode Node = ShaderData.ShaderNodes[ i ];
            if( Node.ID == 0 )
                continue;

            float Scale = 1.2f;
            Vector2 EditorPosition = Node.Position;
            EditorPosition *= Scale;
            string ShaderTypeText = GetShaderTypeText( Node.ShaderType );
            Text = string.Format( CultureInfo.InvariantCulture,
                      "nodes/{0}/{1}/node = SubResource(\"{2}\")\n" +
                      "nodes/{3}/{4}/position = Vector2({5}, {6})\n",
                      ShaderTypeText, Node.ID, Node.GUID,
                      ShaderTypeText, Node.ID, (int)EditorPosition.x, (int)EditorPosition.y );
            FileData += Text;
            if( Node.GraphConfigText.Length > 0 )
            {
                FileData += Node.GraphConfigText;
            }
        }

        string VertexConnectionsString ="";
	    string FragmentConnectionsString="";
	    int FragmentConnections = 0;
	    int VertexConnections = 0;
	    for( int i = 0; i < ShaderData.Connections.Count; i++ )
	    {
		    ShaderConnection Connection = ShaderData.Connections[ i ];

            Text = string.Format( CultureInfo.InvariantCulture, "{0}, {1}, {2}, {3}", Connection.SourceNode, Connection.SourceNodeOutput,
				      Connection.DestinationNode, Connection.DestinationNodeInput );

		    if( Connection.ShaderType == ShaderType.VERTEX )
		    {
			    if( VertexConnections > 0 )
				    VertexConnectionsString += ",\n";
			    VertexConnectionsString += Text;
			    VertexConnections++;
		    }
		    else if( Connection.ShaderType == ShaderType.FRAGMENT )
		    {
			    if( FragmentConnections > 0 )
				    FragmentConnectionsString += ",\n";
			    FragmentConnectionsString += Text;
			    FragmentConnections++;
		    }
	    }
	    if( VertexConnections > 0 )
	    {
            Text = string.Format( CultureInfo.InvariantCulture,"nodes/vertex/connections = PackedInt32Array(\n{0})\n", VertexConnectionsString );
		    FileData += Text;
	    }

        Text = string.Format( CultureInfo.InvariantCulture, "nodes/fragment/connections = PackedInt32Array(\n{0})\n", FragmentConnectionsString );
	    FileData += Text;

        string TrimmedPath = path;
        if ( path.StartsWith("Assets"))
            TrimmedPath = path.Remove( 0, "Assets".Length );
        TrimmedPath = TrimmedPath.Replace( ".shadergraph", ".tres" );
        string GodotShaderPath = ExportFolder + "/" + TrimmedPath;
        CreateDirectoriesForFile( GodotShaderPath );
        File.WriteAllText( GodotShaderPath, FileData );

        bool TestLoops = true;
        if ( TestLoops )
        {
            DetectLoops( ShaderData );
        }
        return ShaderData;
    }
#endif
    public class PassData
    {
        public string PassName;
        public string Code;
        public int StartOffset;
        public int EndOffset;
        public string SurfaceFuncName;
    }
    public static List<string> GetStringsBetween( string Code, string Start, string End )
    {
        List<string> Ret = new List<string>();

        int StartOffset = Code.IndexOf( Start );
        
        while ( StartOffset != -1 )
        {
            StartOffset += Start.Length;
            int EndOffset = Code.IndexOf( End, StartOffset );
            if (EndOffset == -1)
                break;

            string Section = Code.Substring( StartOffset, EndOffset - StartOffset );

            Ret.Add( Section );

            StartOffset = Code.IndexOf( Start, EndOffset );
        }
        return Ret;
    }
    public static string MergeStrings( List<string> Strings )
    {
        if ( Strings.Count == 0)
            return "";

        StringBuilder sb = new StringBuilder();
        for(int i=0; i<Strings.Count; i++)
        {
            sb.Append( Strings[i] );
        }

        return sb.ToString();
    }
    public static List<PassData> GetPassData( string Code )
    {
        List<string> CGIncludes = GetStringsBetween( Code, "CGINCLUDE", "ENDCG" );
        string CGIncludesCode = MergeStrings( CGIncludes );

        string Start = "CGPROGRAM";
        string End = "ENDCG";
        List<PassData> Ret = new List<PassData>();

        int StartOffset = Code.IndexOf( Start );

        while ( StartOffset != -1 )
        {
            StartOffset += Start.Length;
            int EndOffset = Code.IndexOf( End, StartOffset );
            if (EndOffset == -1)
                break;

            PassData NewPassData = new PassData();
            NewPassData.StartOffset = StartOffset;
            NewPassData.EndOffset = EndOffset;
            NewPassData.Code = Code.Substring( StartOffset, EndOffset - StartOffset );

            if (Ret.Count > 0)
            {
                int LightModeStart = Code.IndexOf("\"LightMode\"", Ret[Ret.Count - 1].EndOffset);
                if (LightModeStart != -1 && LightModeStart < StartOffset )
                {
                    string LightModeCode = Code.Substring(LightModeStart, StartOffset - LightModeStart );
                    var Identifiers = GetIdentifiers(LightModeCode);
                    if (Identifiers.Count > 1)
                        NewPassData.PassName = Identifiers[2];
                }
            }

            NewPassData.SurfaceFuncName = GetSurfaceFuncName( NewPassData.Code );
            NewPassData.Code = CGIncludesCode + NewPassData.Code;
            Ret.Add( NewPassData );
            StartOffset = Code.IndexOf( Start, EndOffset );
        }

        return Ret;
    }
    public static string RemoveSemantics( string Code )
    {
        int Start = Code.IndexOf(":");
        while ( Start != -1 )
        {
            int End = Code.IndexOfAny( ",;\n".ToCharArray(), Start );
            if ( End == -1 )
                break;

            string RemoveStr = Code.Substring( Start, End - Start );
            Code = Code.Replace( RemoveStr, "" );
            Start = Code.IndexOf( ":", Start );
        }

        return Code;
    }
    public static PassData SelectPass( List<PassData> Passes )
    {
        for (int i = 0; i < Passes.Count; i++)
        {
            if (Passes[i].SurfaceFuncName != null && Passes[i].SurfaceFuncName.Length > 0 &&
                !Passes[i].SurfaceFuncName.Equals("ShadowCaster"))
                return Passes[i];
        }

        return null;
    }
    public static string MergeLines(string[] InLines, int[] Offsets)
    {
        string Merged = "";
        for (int i = Offsets[0]; i <= Offsets[1]; i++)
        {
            Merged += InLines[i] + "\n";
        }
        return Merged;
    }
    public static bool IsIdentChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_' || c == '$';
    }

    public static string NormalizeNumbers(string input)
    {
        if (input == null)
            return string.Empty;

        var output = new StringBuilder(input.Length * 2);
        int n = input.Length;

        for (int i = 0; i < n;)
        {
            char c = input[i];

            bool startsWithMinus = (c == '-' && i + 1 < n && char.IsDigit(input[i + 1]));
            bool startsWithDigit = char.IsDigit(c);
            if (!startsWithMinus && !startsWithDigit)
            {
                output.Append(c);
                i++;
                continue;
            }

            // Check if part of identifier or preceded by '.'
            char before = (i > 0) ? input[i - 1] : '\0';
            if (before != '\0' && (IsIdentChar(before) || before == '.'))
            {
                output.Append(c);
                i++;
                continue;
            }

            int j = i;
            if (input[j] == '-') j++; // consume sign

            // Hex literal (0x...)
            if (j + 1 < n && input[j] == '0' && (input[j + 1] == 'x' || input[j + 1] == 'X'))
            {
                int hexStart = j + 2;
                j += 2;
                while (j < n && Uri.IsHexDigit(input[j])) j++;
                if (j > hexStart)
                {
                    output.Append(input.Substring(i, j - i));
                    i = j;
                    continue;
                }
                // else fall through
            }

            // Integer part
            int intStart = j;
            while (j < n && char.IsDigit(input[j])) j++;
            bool hadDigits = (j > intStart);

            // Fractional part
            bool isFloat = false;
            if (j < n && input[j] == '.')
            {
                isFloat = true;
                j++;
                while (j < n && char.IsDigit(input[j])) j++;
            }

            // Exponent part
            if (j < n && (input[j] == 'e' || input[j] == 'E'))
            {
                int k = j + 1;
                if (k < n && (input[k] == '+' || input[k] == '-')) k++;
                int expDigitsStart = k;
                while (k < n && char.IsDigit(input[k])) k++;
                if (k > expDigitsStart)
                {
                    isFloat = true;
                    j = k;
                }
            }

            // Suffix parsing (f, u, L, f32, etc.)
            bool hasSuffix = false;
            //bool suffixIndicatesFloat = false;
            if (j < n && char.IsLetter(input[j]))
            {
                int k = j;
                while (k < n && (char.IsLetterOrDigit(input[k]))) k++;
                string suff = input.Substring(j, k - j);

                bool validSuffix = true;
                foreach (char ch in suff)
                {
                    if (char.IsLetter(ch))
                    {
                        if (ch == 'f' || ch == 'F')
                        {
                            //suffixIndicatesFloat = true;
                        }
                        else if (ch == 'u' || ch == 'U' || ch == 'l' || ch == 'L')
                        {
                            // integer-type suffix, valid
                        }
                        else
                        {
                            validSuffix = false;
                            break;
                        }
                    }
                }

                if (validSuffix)
                {
                    hasSuffix = true;
                    j = k;
                }
                else
                {
                    output.Append(c);
                    i++;
                    continue;
                }
            }

            // Check next char after number
            char after = (j < n) ? input[j] : '\0';
            if (!hasSuffix && after != '\0' && IsIdentChar(after))
            {
                output.Append(c);
                i++;
                continue;
            }

            // Append the numeric token
            output.Append(input.Substring(i, j - i));

            // Add ".0" if it's a plain integer
            if (hadDigits && !isFloat && !hasSuffix)
            {
                output.Append(".0");
            }

            i = j;
        }

        return output.ToString();
    }
    public static void FixSurfaceShaderCode(ref string Expression)
    {
        StringReplacement(ref Expression, "#pragma", "//#pragma");
        StringReplacement(ref Expression, "#include", "//#include");
        StringReplacement(ref Expression, "inline", "");

        StringReplacement(ref Expression, "_Time.x", "TIME/20.0");
        StringReplacement(ref Expression, "_Time.y", "TIME");
        StringReplacement(ref Expression, "_Time.z", "TIME*2.0");
        StringReplacement(ref Expression, "_Time.w", "TIME*3.0");

        StringReplacement(ref Expression, "_SinTime.x", "sin(TIME/8.0)");
        StringReplacement(ref Expression, "_SinTime.y", "sin(TIME/4.0)");
        StringReplacement(ref Expression, "_SinTime.z", "sin(TIME/2.0)");
        StringReplacement(ref Expression, "_SinTime.w", "sin(TIME)");

        StringReplacement(ref Expression, "INTERNAL_DATA", "");

        Expression = ModifyGlobalDeclarations( Expression );

        //Add .0 to all numbers
        Expression = NormalizeNumbers( Expression );
    }
    public static string GetSurfaceFuncName( string Code )
    {
        string Identifier = "#pragma surface ";
        int NameStart = Code.IndexOf( Identifier );
        if (NameStart != -1)
        {
            int IdentifierEnd = NameStart + Identifier.Length;
            int NextLine = Code.IndexOf("\n", IdentifierEnd );
            int NextWhite = Code.IndexOf(" ", IdentifierEnd );
            int Next = Math.Min(NextLine, NextWhite);
            int Offset = NameStart + Identifier.Length;

            int NameLength = Next - Offset;
            if (NameLength > 0)
            {
                string Name = Code.Substring(Offset, Next - Offset);
                return Name;
            }
        }

        return null;
    }
    public static string GetIdentifier( string Code, int Start )
    {
        Again:
        int NextWhiteCharacter = Code.IndexOfAny( " ,();".ToCharArray(), Start);
        if (NextWhiteCharacter != -1) 
        {
            int Length = NextWhiteCharacter - Start;
            if ( Length == 0 )
            {
                Start++;
                goto Again;
                //return GetIdentifier( Code, Start + 1 );
            }
            string Identifier = Code.Substring(Start, Length );
            return Identifier;
        }

        return null;
    }
    public static string TrimIdentifier( string Identifier)
    {
        string WhiteCharacters = " ,(){};\n";
        for (int i = 0; i < WhiteCharacters.Length; i++)
        {
            Identifier = Identifier.Replace(WhiteCharacters[i].ToString(), "");
        }
        return Identifier;
    }
    public static List<string> GetIdentifiers( string Code )
    {
        List<string> Identifiers = new List<string>();

        string WhiteCharacters = " ,(){};:\"\t\n\r";
        int PreviousOffset = 0;
        int NextWhiteCharacter = Code.IndexOfAny( WhiteCharacters.ToCharArray(), PreviousOffset );
        while( NextWhiteCharacter != -1 )
        {
            string Identifier = Code.Substring( PreviousOffset, NextWhiteCharacter - PreviousOffset);
            Identifier = TrimIdentifier( Identifier );
            if (Identifier.Length > 0)
                Identifiers.Add( Identifier );

            PreviousOffset = NextWhiteCharacter + 1;
            NextWhiteCharacter = Code.IndexOfAny( WhiteCharacters.ToCharArray(), PreviousOffset);
        }

        if (Identifiers.Count > 0)
            return Identifiers;
        return null;
    }
    public static string GetInputStructName(string Code, string SurfaceFuncName, ref int SurfaceFuncStart)
    {
        int Offset = 1;
        int ParamsStart = Code.IndexOf(SurfaceFuncName + "(");
        if ( ParamsStart == -1)
        { 
            ParamsStart = Code.IndexOf(SurfaceFuncName + " (");
            Offset = 2;
        }
        if (ParamsStart != -1)
        {
            string InputStructName = GetIdentifier( Code, ParamsStart + SurfaceFuncName.Length + Offset );
            SurfaceFuncStart = ParamsStart;
            return InputStructName;
        }

        return null;
    }
    public static List<string> GetInputStructFields(ref string Code, string InputStructName)
    {
        List<string> Ret = new List<string>();

        string SearchFor = "struct " + InputStructName;
        int StructStart = Code.IndexOf( SearchFor);

        int StructEnd = Code.IndexOf("}", StructStart + SearchFor.Length );
        if ( StructStart != -1 && StructEnd != -1 )
        {
            StructStart += SearchFor.Length;
            string Contents = Code.Substring( StructStart, StructEnd - StructStart );
            //remove it
            Code = Code.Remove(StructStart, StructEnd - StructStart);
            try
            {
                Contents = RemoveSemantics( Contents );
                //insert the clean version
                Code = Code.Insert( StructStart, Contents );
            }
            catch(Exception E)
            {
                Debug.LogError( E.Message );
                Debug.LogError( E.StackTrace );
            }
            List<string> Identifiers = GetIdentifiers( Contents );

            return Identifiers;
        }

        return Ret;
    }
    public static string GenerateInputDeclarations(List<string> InputIdentifiers)
    {
        string Ret = "";
        for (int i = 0; i < InputIdentifiers.Count; i+=2)
        {
            //happens when INTERNAL_DATA is the last identifier
            if ( i + 1 >= InputIdentifiers.Count )
                break;
            string Type = InputIdentifiers[i];
            string Name = InputIdentifiers[i + 1];
            string Value = "";
            //skip over semantics and Input
            if (Type.Equals("VFACE") || Type.Equals("Input") )
            {
                i--;
                continue;
            }
            //skip Unity defines
            if (Type.Equals( "INTERNAL_DATA") || Name.Equals("INTERNAL_DATA") )
            {
                continue;
            }

            if ( Name.Contains("uv_", StringComparison.OrdinalIgnoreCase))
                Value = "UV";
            else if (Name.Contains("uv2", StringComparison.OrdinalIgnoreCase) ||
                     Name.Contains("uv3", StringComparison.OrdinalIgnoreCase) ||
                     Name.Contains("uv4", StringComparison.OrdinalIgnoreCase))
                Value = "UV2";
            else if (Name.Contains("normal", StringComparison.OrdinalIgnoreCase) || Name.Contains("worldNormal", StringComparison.OrdinalIgnoreCase))
                Value = "NORMAL";
            else if (Name.Contains("color", StringComparison.OrdinalIgnoreCase))
                Value = "COLOR";
            else if (Name.Contains("tangent", StringComparison.OrdinalIgnoreCase))
                Value = "TANGENT";
            else if (Name.Contains("viewdir", StringComparison.OrdinalIgnoreCase))
                Value = "CAMERA_DIRECTION_WORLD";
            else if (Name.Contains("worldPos", StringComparison.OrdinalIgnoreCase))
                //This matches world position when outputting to albedo for testing
                Value = "( INV_VIEW_MATRIX * vec4( VERTEX, 1.0) ).xyz";
            else if (Name.Contains("screenPos", StringComparison.OrdinalIgnoreCase))
                Value = "SCREEN_UV";
            else if (Name.Contains("facing", StringComparison.OrdinalIgnoreCase))
                Value = "1.0";
            else if (Name.Contains("UNITY_VERTEX_OUTPUT_STEREO", StringComparison.OrdinalIgnoreCase))
                Value = "0.0";
            else if (Name.Contains("UNITY_VERTEX_INPUT_INSTANCE_ID", StringComparison.OrdinalIgnoreCase))
                Value = "0.0";
            else if (Name.Contains("VFACE", StringComparison.OrdinalIgnoreCase))
                Value = "FRONT_FACING ? 1.0 : -1.0";

            if ( Value.Length > 0 )
                Ret += string.Format( CultureInfo.InvariantCulture,"\tIn.{0} = {1};\n", Name, Value);
            else
            {
                Debug.LogError("Surface shader Input " + Name + " wasn't recognized !");
            }
        }

        return Ret;
    }
    public static bool IsSurfaceShader( string path )
    {
        if ( !File.Exists(path))
            return false;
        string AllText = File.ReadAllText(path);        

        string SurfaceFuncName = GetSurfaceFuncName(AllText);
        if (SurfaceFuncName == null)
            return false;//not a surface shader

        return true;
    }
    public static string GetGodotFilePath(string path, string Extension )
    {
        string TrimmedPath = path.Remove(0, "Assets".Length);
        TrimmedPath = TrimmedPath.Replace(".shadergraph", Extension );
        TrimmedPath = TrimmedPath.Replace(".shader", Extension );

        string GodotPath = ExportFolder + "/" + TrimmedPath;
        return GodotPath;
    }
    public static string GetRenderStateValue(string shaderSource, string name)
    {
        if (shaderSource.Contains(name))
        {
            int start = shaderSource.IndexOf(name);
            int EOL = shaderSource.IndexOf("\n", start);
            if (EOL != -1)
            {
                string value = shaderSource.Substring(start + name.Length, EOL - (start + name.Length));
                return value;
            }
        }

        return null;
    }
    public static string ModifyGlobalDeclarations( string ShaderSource )
    {
        int Depth = 0;
        int LastLineStart = 0;
        string Ret = "";
        string LastLine = "";
        int Modifications = 0;
        for(int i=0; i< ShaderSource.Length; i++)
        {
            if (ShaderSource[i] == '{')
            {
                Depth++;
            }
            else if (ShaderSource[i] == '}')
            {
                Depth--;
            }

            if (ShaderSource[i] == '\n')
            {
                int NewLineStart = i + 1;
                LastLine = ShaderSource.Substring( LastLineStart, NewLineStart - LastLineStart );
                if (Depth == 0)
                {
                    //prevent uniform uniform
                    //prevent function declarations from using uniform
                    if ( !LastLine.Contains("uniform") && LastLine.Contains(";") && !LastLine.Contains("{"))
                    {
                        Modifications += StringReplacement(ref LastLine, "sampler2D", "uniform sampler2D");
                        Modifications += StringReplacement(ref LastLine, "float", "uniform float");
                        Modifications += StringReplacement(ref LastLine, "vec2",  "uniform vec2");
                        Modifications += StringReplacement(ref LastLine, "vec3",  "uniform vec3");
                        Modifications += StringReplacement(ref LastLine, "vec4",  "uniform vec4");
                    }
                }
                Ret += LastLine;
                LastLineStart = NewLineStart;
            }
        }

        LastLine = ShaderSource.Substring(LastLineStart, ShaderSource.Length - LastLineStart);
        Ret += LastLine;

        if (Modifications > 0)
            return Ret;
        else
            return ShaderSource;
    }
    public static string GetGodotCullModeFromShaderIdentifier( string UnityCullMode )
    {
        if (UnityCullMode.CompareTo("Off") == 0 )
        {
            return "cull_disabled";
        }
        if (UnityCullMode.CompareTo("Front") == 0)
        {
            return "cull_front";
        }

        //Back
        return "cull_back";
    }
    public static GodotCullMode GetGodotCullMode(string m_OpaqueCullMode)
    {
        if (m_OpaqueCullMode.CompareTo("Back") == 0)
        {
            return GodotCullMode.GCM_BACK;
        }
        if (m_OpaqueCullMode.CompareTo("Front") == 0)
        {
            return GodotCullMode.GCM_FRONT;
        }
        if (m_OpaqueCullMode.CompareTo("Off") == 0)
        {
            return GodotCullMode.GCM_OFF;
        }

        //Back
        return GodotCullMode.GCM_BACK;
    }
    public static string GetBlendMode(string UnityBlendMode)
    {
        if (UnityBlendMode.CompareTo("SrcAlpha OneMinusSrcAlpha") == 0)
        {
            return "blend_mix";
        }

        return null;
    }
    //ChatGPT hallucination lol
    public static string GetColorMask(string ColorMask)
    {
        if (ColorMask.CompareTo("RGB") == 0)
        {
            return "color_write_rgb";
        }
        if (ColorMask.CompareTo("A") == 0)
        {
            return "color_write_a";
        }

        //RGBA
        return "color_write_rgba";
    }
    public static void GetRenderStates( string shaderSource, ref string OutDeclarations, ref Dictionary<string,string> dictionary )
    {
        string cull = GetRenderStateValue(shaderSource, "Cull ");
        string blend = GetRenderStateValue(shaderSource, "Blend ");
        string colorMask = GetRenderStateValue(shaderSource, "ColorMask ");

        if ( cull != null )
        {
            string CullType = GetGodotCullModeFromShaderIdentifier(cull);
            OutDeclarations += string.Format( CultureInfo.InvariantCulture, "render_mode {0};\n", CullType );
            dictionary.Add("Cull", CullType);
        }
        if (blend != null)
        {
            string BlendType = GetBlendMode(blend);
            if (BlendType != null)
            {
                OutDeclarations += string.Format( CultureInfo.InvariantCulture,"render_mode {0};\n", BlendType);
                dictionary.Add("Blend", BlendType);
            }
        }
        if (colorMask != null )
        {
            dictionary.Add("ColorMask", colorMask);
            //OutDeclarations += string.Format( CultureInfo.InvariantCulture, "render_mode {0};\n", GetColorMask(colorMask) );
        }
    }
    public static void DoExportSurfaceShader( string path )
    {
        string AllText = File.ReadAllText( path );
        //string[] AllLines = File.ReadAllLines( path );

        string SurfaceFuncName = GetSurfaceFuncName( AllText );
        if (SurfaceFuncName == null)
            return;//not a surface shader

        List<PassData> PassData = GetPassData( AllText );
        PassData Pass = SelectPass(PassData);
        if ( Pass == null )
        {
            Debug.LogError( path + " is not a custom shader !");
            return;
        }
        
        string HLSL = Pass.Code;

        string GLSL = HLSL;
        FixCustomExpressionCode( ref GLSL );
        FixSurfaceShaderCode( ref GLSL );

        int SurfaceFuncStart = -1;
        string InputStructName = GetInputStructName( GLSL, SurfaceFuncName, ref SurfaceFuncStart );
        List<string> InputFields = GetInputStructFields( ref GLSL, InputStructName );
        string InputDeclarations = GenerateInputDeclarations( InputFields );

        string RenderStates = "";
        //render_mode unshaded; -> Unlit
        Dictionary<string, string> dictionary = new Dictionary<string, string>();
        GetRenderStates(AllText, ref RenderStates, ref dictionary );

        string ShaderPrefix = string.Format( CultureInfo.InvariantCulture,@"shader_type spatial;
{2} {1}
{0}
", RenderStates, "\"res://globals.gdshaderinc\"", "#include" );

        //
        
        string AlphaCode = "//ALPHA = o.Alpha;\n";
        if (dictionary.ContainsKey("Blend") && dictionary["Blend"] == "blend_mix")
        {
            AlphaCode = "ALPHA = o.Alpha;\n";
        }
        string ShaderSuffix = string.Format( CultureInfo.InvariantCulture,@"// Vertex function
void vertex() {{
    // Simple vertex displacement (sine wave on Y)
    //float wave = sin(TIME * wave_speed + VERTEX.x * 2.0) * wave_strength;
    //VERTEX.y += wave;
}}

// Fragment (pixel) function
void fragment() {{
	Input In;
	SurfaceOutputStandard o;
    o.Albedo = vec3( 1.0, 1.0, 1.0 );
    o.Normal = vec3( 0.0, 0.0, 1.0 );
    o.Emission = vec3( 0.0, 0.0, 0.0 );
    o.Occlusion = 1.0;
    o.Specular = vec3( 0.0, 0.0, 0.0 );
    o.Gloss = 1.0;
    o.Smoothness = 0.0;
    o.Metallic = 0.0;
    o.Alpha = 1.0;
{2}
	{0}( In, o );	 
    ALBEDO = o.Albedo;
    {1}
    mat3 TBN = mat3(TANGENT, BINORMAL, NORMAL);
	vec3 NormalInWorldSpace = o.Normal;//Unity allegedly outputs WorldSpace normals
    NORMAL = normalize(TBN * NormalInWorldSpace);//apparently Godot wants NORMAL in view space
    EMISSION = o.Emission * {3};//To match Godot nits units
    ROUGHNESS = 1.0 - o.Smoothness;
    METALLIC = o.Metallic;
    AO = o.Occlusion;
}}", SurfaceFuncName, AlphaCode, InputDeclarations, EmissionMultiplier );
        GLSL = ShaderPrefix + GLSL + ShaderSuffix;

        string GodotFilePath = GetGodotFilePath( path, ".gdshader");
        CreateDirectoriesForFile( GodotFilePath );
        File.WriteAllText(GodotFilePath, GLSL );
    }
    public static string GetGodotPathFromUnityAssetPath(string AssetPath)
    {
        string TrimmedPath="";
        if ( AssetPath.StartsWith("Assets"))
        { 
            TrimmedPath = AssetPath.Remove( 0, "Assets".Length );
        }
        else
            TrimmedPath = AssetPath;
        string GodotPath = ExportFolder + "/" + TrimmedPath;
        return GodotPath;
    }
    public static string GetPluginFilePath(string FileName)
    {
        string[] AllAssetPaths = AssetDatabase.GetAllAssetPaths();
        foreach (string Path in AllAssetPaths)
        {
            if (Path.Contains(FileName))
                return Path;
        }

        Debug.LogError("Plugin file " + FileName + " was not found ! Make sure you installed UTG properly !");

        return null;
    }
    public static void CopyToOutput(string SourceFile, string OutFilePath, bool Overwrite)
    {
        string ProjectPath = GetProjectPath();
        string UnityPath = ProjectPath + "/" + SourceFile;
        string GodotPath = ExportFolder + "/" + OutFilePath;

        CreateDirectoriesForFile(GodotPath);

        try
        {
            bool FileExists = File.Exists(GodotPath);
            if (!FileExists || (FileExists && Overwrite))
                File.Copy(SourceFile, GodotPath, Overwrite);
        }
        catch (Exception E)
        {
            Debug.Log("CopyToOutput failed for " + SourceFile + " -> " + OutFilePath + E.Message + E.StackTrace );
        }
    }
    public static void WriteToOutput(string SourceData, string OutFilePath, bool Overwrite)
    {
        string GodotPath = ExportFolder + "/" + OutFilePath;
        try
        {
            File.WriteAllText( GodotPath, SourceData );
        }
        catch (Exception E)
        {
            Debug.Log("WriteToOutput failed for " + OutFilePath + E.Message + E.StackTrace );
        }
    }
    public static void CopyGlobalFiles()
    {
        string ProjectName = Application.productName;
        string GodotProjectData = string.Format( CultureInfo.InvariantCulture, @"
config_version=5

[application]

config/name=""{0}""
config/features=PackedStringArray(""4.4"", ""Forward Plus"")

[importer_defaults]

texture={{
""detect_3d/compress_to"": 0
}}

[rendering]

textures/canvas_textures/default_texture_filter=2
textures/canvas_textures/default_texture_repeat=1
lights_and_shadows/use_physical_light_units=true
textures/default_filters/anisotropic_filtering_level=4
", ProjectName );
        WriteToOutput( GodotProjectData, "project.godot", false );
        CopyToOutput(GetPluginFilePath("globals.gdshaderinc"), "globals.gdshaderinc", true );
    }
}