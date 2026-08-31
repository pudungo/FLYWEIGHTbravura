// Copyright Alex Quevillon. All Rights Reserved.

// #define WITH_FBX_EXPORTER
// https://docs.unity3d.com/Packages/com.unity.formats.fbx@2.0/manual/index.html

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using System;
using System.Reflection;

using System.IO;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public class FbxExportSupport
{
    private Type fbxExporterType;
    private MethodInfo exportMethod;

    FbxExportSupport()
    {
        // Try to find FBX Exporter class
        fbxExporterType = Type.GetType("UnityEditor.Formats.Fbx.Exporter.ModelExporter, Unity.Formats.Fbx.Editor");

        if (fbxExporterType != null)
        {
            exportMethod = fbxExporterType.GetMethod("ExportObject", BindingFlags.Static | BindingFlags.Public, null,
                new Type[] { typeof(string), typeof(UnityEngine.Object) }, null);
        }
    }

    public static bool IsFbxExporterAvailable()
    {
        FbxExportSupport fbxExporterSupport = new FbxExportSupport();

        return fbxExporterSupport.fbxExporterType != null && fbxExporterSupport.exportMethod != null;
    }

    public static bool Export(string path, UnityEngine.Object obj)
    {
        if (!IsFbxExporterAvailable())
        {
            Debug.LogWarning("FBX Exporter is not installed.");
            return false;
        }

        FbxExportSupport fbxExporterSupport = new FbxExportSupport();
        fbxExporterSupport.exportMethod.Invoke(null, new object[] { path, obj });
        return true;
    }
}


public static class RenderPipelineUtils
{

    public enum PipelineType
    {
        Unsupported,
        BuiltInPipeline,
        UniversalPipeline,
        HDPipeline
    }

    public static PipelineType GetPipeline()
    {
#if UNITY_2019_1_OR_NEWER
#if UNITY_2023_1_OR_NEWER
        if (GraphicsSettings.defaultRenderPipeline != null)
#else
        if (GraphicsSettings.renderPipelineAsset != null)
#endif
        {
            // SRP
#if UNITY_2023_1_OR_NEWER
            var srpType = GraphicsSettings.defaultRenderPipeline.GetType().ToString();
#else
            var srpType = GraphicsSettings.renderPipelineAsset.GetType().ToString();
#endif
            if (srpType.Contains("HDRenderPipelineAsset"))
            {
                return PipelineType.HDPipeline;
            }
            else if (srpType.Contains("UniversalRenderPipelineAsset") || srpType.Contains("LightweightRenderPipelineAsset"))
            {
                return PipelineType.UniversalPipeline;
            }
            else return PipelineType.Unsupported;
        }
#elif UNITY_2017_1_OR_NEWER
        if (GraphicsSettings.renderPipelineAsset != null) {
            // SRP not supported before 2019
            return PipelineType.Unsupported;
        }
#endif
        // no SRP
        return PipelineType.BuiltInPipeline;
    }
}




public class UtuPluginAssets
{
    private static Dictionary<string, List<string>> exportableAssets = new Dictionary<string, List<string>>();
    public static bool needFbxExporterToProcessAlMeshes = false;

    #region All ------------------------------------------------------------------------------------------
    // Added as an optimization. in massive projects old implementaion had severe allocation overhead that would simply crash the editor for running out of memory
    public static void Refresh()
    {
        UtuPluginAssets.ClearCachedScenesRelativeFilenames();
        UtuPluginAssets.ClearCachedPrefabsRelativeFilenames();
        UtuPluginAssets.ClearCachedMeshesRelativeFilenames();
        UtuPluginAssets.ClearCachedAnimatorControllersRelativeFilenames();
        UtuPluginAssets.ClearCachedAnimationsRelativeFilenames();
        UtuPluginAssets.ClearCachedMaterialsRelativeFilenames();
        UtuPluginAssets.ClearCachedTexturesRelativeFilenames();
        UtuPluginAssets.ClearCachedSkeletons();
        UtuPluginAssets.LoadAllPaths();
    }


    public static void LoadAllPaths()
    {
        if (!isCachedScenesValid || !exportableAssets.ContainsKey("t:Scene"))
        {
            LoadPathsByType("t:Scene");
            isCachedScenesValid = true;
        }

        if (!isCachedPrefabsValid || !exportableAssets.ContainsKey("t:Prefab"))
        {
            LoadPathsByType("t:Prefab");
            isCachedPrefabsValid = true;
        }

        if (!isCachedMeshesValid || !exportableAssets.ContainsKey("t:Mesh"))
        {
            needFbxExporterToProcessAlMeshes = false;
            LoadPathsByType("t:Mesh");
            isCachedMeshesValid = true;
        }

        if (!isCachedAnimatorControllersValid || !exportableAssets.ContainsKey("t:" + typeof(UnityEditor.Animations.AnimatorController).Name))
        {
            LoadPathsByType("t:" + typeof(UnityEditor.Animations.AnimatorController).Name);
            isCachedAnimatorControllersValid = true;
        }

        if (!isCachedAnimationsValid || !exportableAssets.ContainsKey("t:Animation"))
        {
            LoadPathsByType("t:Animation");
            isCachedAnimationsValid = true;

            //if (exportableAssets.ContainsKey("t:Mesh"))
            //{
            //	foreach (string path in GetAnimationsRelativeFilenames())
            //	{
            //		// Animations also have a model so remove them from the mesh
            //		exportableAssets["t:Mesh"].Remove(path);
            //             }
            //}
        }

        if (!isCachedMaterialsValid || !exportableAssets.ContainsKey("t:Material"))
        {
            LoadPathsByType("t:Material");
            isCachedMaterialsValid = true;
        }

        if (!isCachedTexturesValid || !exportableAssets.ContainsKey("t:Texture"))
        {
            LoadPathsByType("t:Texture");
            isCachedTexturesValid = true;
        }
    }

    private static void LoadPathsByType(string type)
    {
        // Paths is a ptr to the previous cached list
        if (!exportableAssets.TryGetValue(type, out var paths))
        {
            paths = new List<string>();
            exportableAssets.Add(type, paths);
        }

        paths.Clear();
        AssetDatabase.Refresh();

        var guids = AssetDatabase.FindAssets(type);
        for (int i = 0; i < guids.Length; i++)
        {
            string p = AssetDatabase.GUIDToAssetPath(guids[i]);

            if (!(p.ToLower().EndsWith(".ttf") || p.ToLower().EndsWith(".otf") || p.ToLower().EndsWith(".rendertexture") || p.ToLower().EndsWith(".anim")) && !paths.Contains(p))
            {
                paths.Add(p);

                if (type == "t:Mesh" && !(!p.Contains(".") || p.ToLower().EndsWith(".fbx") || p.ToLower().EndsWith(".obj")))
                {
                    needFbxExporterToProcessAlMeshes = true;
                }
            }
        }
        paths.Sort();
    }
    #endregion

    #region Scenes ------------------------------------------------------------------------------------------
    private static bool isCachedScenesValid = false;
    public static List<string> GetScenesRelativeFilenames()
    {
        var type = "t:Scene";

        if (!exportableAssets.ContainsKey(type))
            LoadAllPaths();

        return exportableAssets[type];
    }

    public static void ClearCachedScenesRelativeFilenames()
    {
        isCachedScenesValid = false;
    }
    #endregion

    #region Prefabs ------------------------------------------------------------------------------------------
    private static bool isCachedPrefabsValid = false;
    public static List<string> GetPrefabsRelativeFilenames()
    {
        var type = "t:Prefab";

        if (!exportableAssets.ContainsKey(type))
            LoadAllPaths();

        return exportableAssets[type];
    }

    public static void ClearCachedPrefabsRelativeFilenames()
    {
        isCachedPrefabsValid = false;
    }
    #endregion

    #region Meshes ------------------------------------------------------------------------------------------
    private static bool isCachedMeshesValid = false;
    public static List<string> GetMeshesRelativeFilenames()
    {
        var type = "t:Mesh";

        if (!exportableAssets.ContainsKey(type))
            LoadAllPaths();

        return exportableAssets[type];
    }

    public static void ClearCachedMeshesRelativeFilenames()
    {
        isCachedMeshesValid = false;
    }
    #endregion

    #region AnimatorControllers ------------------------------------------------------------------------------------------
    private static bool isCachedAnimatorControllersValid = false;
    public static List<string> GetAnimatorControllersRelativeFilenames()
    {
        var type = "t:" + typeof(UnityEditor.Animations.AnimatorController).Name;

        if (!exportableAssets.ContainsKey(type))
            LoadAllPaths();

        return exportableAssets[type];
    }

    public static void ClearCachedAnimatorControllersRelativeFilenames()
    {
        isCachedAnimatorControllersValid = false;
    }
    #endregion

    #region Animations ------------------------------------------------------------------------------------------
    private static bool isCachedAnimationsValid = false;
    public static List<string> GetAnimationsRelativeFilenames()
    {
        var type = "t:Animation";

        if (!exportableAssets.ContainsKey(type))
            LoadAllPaths();

        return exportableAssets[type];
    }

    public static void ClearCachedAnimationsRelativeFilenames()
    {
        isCachedAnimationsValid = false;
    }
    #endregion

    #region Materials ------------------------------------------------------------------------------------------
    private static bool isCachedMaterialsValid = false;
    public static List<string> GetMaterialsRelativeFilenames()
    {
        var type = "t:Material";

        if (!exportableAssets.ContainsKey(type))
            LoadAllPaths();

        return exportableAssets[type];
    }

    public static void ClearCachedMaterialsRelativeFilenames()
    {
        isCachedMaterialsValid = false;
    }
    #endregion

    #region Textures ------------------------------------------------------------------------------------------
    private static bool isCachedTexturesValid = false;
    public static List<string> GetTexturesRelativeFilenames()
    {
        var type = "t:Texture";

        if (!exportableAssets.ContainsKey(type))
            LoadAllPaths();

        return exportableAssets[type];
    }

    public static void ClearCachedTexturesRelativeFilenames()
    {
        isCachedTexturesValid = false;
    }
    #endregion


    public static bool isCachedSkeletonsValid = false;
    public static Dictionary<string, List<string>> cachedClipsToAvatars = new Dictionary<string, List<string>>();
    public static Dictionary<string, List<string>> cachedAvatarsToMeshes = new Dictionary<string, List<string>>();
    public static List<string> cachedScenesForSkeletons = new List<string>();
    public static Dictionary<string, List<string>> clipsToMeshesFoundDuringProcess = new Dictionary<string, List<string>>();

    public static void ClearCachedSkeletons()
    {
        isCachedSkeletonsValid = false;
    }
    public static List<string> GetSkeletalMeshesAssociatedWithAnimation(string animationRelativeFilename)
    {
        // Find skeletons for animation
        GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(animationRelativeFilename);
        if (go == null)
        {
            UtuLog.Error("    Animation asset is not a valid GameObject. Failed to load the GameObject. The animation will not be imported in Unreal. I don't know the exact solution, but make sure your asset is a valid GameObject. Animation: " + animationRelativeFilename);
            return new List<string>();
        }

        SkinnedMeshRenderer meshRenderer = go.GetComponent<SkinnedMeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = go.GetComponentInParent<SkinnedMeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = go.GetComponentInChildren<SkinnedMeshRenderer>();
            }
        }

        if (meshRenderer == null)
        {
            UtuLog.Warning("    Animation doesn't have a valid SkinnedMeshRenderer. May not import properly in Unreal. Animation: " + animationRelativeFilename);
        }

        // Update Cache
        if (!isCachedSkeletonsValid)
        {
            isCachedSkeletonsValid = true;
            cachedClipsToAvatars.Clear();
            cachedAvatarsToMeshes.Clear();
            cachedScenesForSkeletons.Clear();

            foreach (string meshFilename in GetMeshesRelativeFilenames())
            {
                //UtuLog.Log(meshFilename);
                GameObject meshGameObject = AssetDatabase.LoadAssetAtPath<GameObject>(meshFilename);
                if (meshGameObject != null)
                {
                    // Link Animator name to a sekeletal mesh relative filename
                    Animator skinnedMeshAnimator = meshGameObject.gameObject.GetComponent<Animator>();
                    if (skinnedMeshAnimator == null)
                    {
                        skinnedMeshAnimator = meshGameObject.gameObject.GetComponentInParent<Animator>();
                        if (skinnedMeshAnimator == null)
                        {
                            skinnedMeshAnimator = meshGameObject.gameObject.GetComponentInChildren<Animator>();
                        }
                    }

                    if (skinnedMeshAnimator != null && skinnedMeshAnimator.avatar != null)
                    {
                        if (!cachedAvatarsToMeshes.ContainsKey(skinnedMeshAnimator.avatar.name))
                        {
                            cachedAvatarsToMeshes.Add(skinnedMeshAnimator.avatar.name, new List<string>());
                        }
                        if (!cachedAvatarsToMeshes[skinnedMeshAnimator.avatar.name].Contains(meshFilename))
                        {
                            cachedAvatarsToMeshes[skinnedMeshAnimator.avatar.name].Add(meshFilename); // AddUnique
                        }
                        //UtuLog.Log("    " + skinnedMeshAnimator.avatar.name + "  ->  " + meshFilename);
                    }
                }
            }


            foreach (string x in GetPrefabsRelativeFilenames())
            {
                GameObject prefabGameObject = AssetDatabase.LoadAssetAtPath<GameObject>(x);
                // Link Animator name to a sekeletal mesh relative filename
                Animator skinnedMeshAnimator = prefabGameObject.gameObject.GetComponent<Animator>();
                if (skinnedMeshAnimator == null)
                {
                    skinnedMeshAnimator = prefabGameObject.gameObject.GetComponentInParent<Animator>();
                    if (skinnedMeshAnimator == null)
                    {
                        skinnedMeshAnimator = prefabGameObject.gameObject.GetComponentInChildren<Animator>();
                    }
                }

                if (skinnedMeshAnimator != null && skinnedMeshAnimator.avatar != null && skinnedMeshAnimator.runtimeAnimatorController != null)
                {
                    foreach (AnimationClip clip in skinnedMeshAnimator.runtimeAnimatorController.animationClips)
                    {
                        if (!cachedClipsToAvatars.ContainsKey(AssetDatabase.GetAssetPath(clip)))
                        {
                            cachedClipsToAvatars.Add(AssetDatabase.GetAssetPath(clip), new List<string>());
                        }
                        if (!cachedClipsToAvatars[AssetDatabase.GetAssetPath(clip)].Contains(skinnedMeshAnimator.avatar.name))
                        {
                            cachedClipsToAvatars[AssetDatabase.GetAssetPath(clip)].Add(skinnedMeshAnimator.avatar.name); // AddUnique
                        }
                        //UtuLog.Error(AssetDatabase.GetAssetPath(clip) + "  ->  " + skinnedMeshAnimator.avatar.name);
                    }
                }
            }
        }

        if (!cachedScenesForSkeletons.Contains(EditorSceneManager.GetActiveScene().ToString()))
        {
            cachedScenesForSkeletons.Add(EditorSceneManager.GetActiveScene().ToString());

            GameObject[] gameObjects = UtuPluginSceneProcessor.GetAllGameObjectsInCurrentScene();
            foreach (GameObject gameObject in gameObjects)
            {
                // Link Animator name to a sekeletal mesh relative filename
                Animator skinnedMeshAnimator = gameObject.gameObject.GetComponent<Animator>();
                if (skinnedMeshAnimator == null)
                {
                    skinnedMeshAnimator = gameObject.gameObject.GetComponentInParent<Animator>();
                    if (skinnedMeshAnimator == null)
                    {
                        skinnedMeshAnimator = gameObject.gameObject.GetComponentInChildren<Animator>();
                    }
                }

                if (skinnedMeshAnimator != null && skinnedMeshAnimator.avatar != null && skinnedMeshAnimator.runtimeAnimatorController != null)
                {
                    foreach (AnimationClip clip in skinnedMeshAnimator.runtimeAnimatorController.animationClips)
                    {
                        if (!cachedClipsToAvatars.ContainsKey(AssetDatabase.GetAssetPath(clip)))
                        {
                            cachedClipsToAvatars.Add(AssetDatabase.GetAssetPath(clip), new List<string>());
                        }
                        if (!cachedClipsToAvatars[AssetDatabase.GetAssetPath(clip)].Contains(skinnedMeshAnimator.avatar.name))
                        {
                            cachedClipsToAvatars[AssetDatabase.GetAssetPath(clip)].Add(skinnedMeshAnimator.avatar.name); // AddUnique
                        }
                        //UtuLog.Error(AssetDatabase.GetAssetPath(clip) + "  ->  " + skinnedMeshAnimator.avatar.name);
                    }
                }
            }
        }

        // Find associated skeletons in cache based on Mesh name and Animator name
        List<string> skeletons = new List<string>();
        if (cachedClipsToAvatars.ContainsKey(animationRelativeFilename))
        {
            foreach (string avatar in cachedClipsToAvatars[animationRelativeFilename])
            {
                if (cachedAvatarsToMeshes.ContainsKey(avatar))
                {
                    foreach (string mesh in cachedAvatarsToMeshes[avatar])
                    {
                        skeletons.Add(mesh);
                    }
                }
            }
        }

        if (clipsToMeshesFoundDuringProcess.ContainsKey(animationRelativeFilename))
        {
            foreach (string mesh in clipsToMeshesFoundDuringProcess[animationRelativeFilename])
            {
                skeletons.Add(mesh);
            }
        }


        if (skeletons.Count == 0)
        {
            if (meshRenderer == null)
            {
                UtuLog.Error("    Animation: " + animationRelativeFilename + "\n        Was not able to find any Mesh using this Animation. In Unreal, it is required to link an Animation to a Skeletal Mesh.\n        Please create a GameObject in a scene (or Prefab) that links this Animation to a Mesh through an Animator component.\n        Animator.Controller = Controller that contains this Animation Clip\n        Animator.Avatar = The Mesh");
            }
            else
            {
                UtuLog.Warning("    Animation: " + animationRelativeFilename + "\n        Was not able to find any Mesh using this Animation. In Unreal, it is required to link an Animation to a Skeletal Mesh.\n        Please create a GameObject in a scene (or Prefab) that links this Animation to a Mesh through an Animator component.\n        Animator.Controller = Controller that contains this Animation Clip\n        Animator.Avatar = The Mesh");
            }
        }
        else
        {
            UtuLog.Log("    Animation: " + animationRelativeFilename + "\n        Linked to meshes: " + string.Join(", ", skeletons.ToArray()));
        }

        return skeletons;
    }

    public static void LinkClipToSkeletalMesh(string animationRelativeFilename, string skeletalMeshRelativeFilename)
    {
        if (!clipsToMeshesFoundDuringProcess.ContainsKey(animationRelativeFilename))
        {
            clipsToMeshesFoundDuringProcess.Add(animationRelativeFilename, new List<string>());
        }
        if (!clipsToMeshesFoundDuringProcess[animationRelativeFilename].Contains(skeletalMeshRelativeFilename))
        {
            clipsToMeshesFoundDuringProcess[animationRelativeFilename].Add(skeletalMeshRelativeFilename); // AddUnique
        }
    }

}


public class UtuPluginSceneProcessor
{
    private UtuPluginJson json;
    private UtuPluginScene scene;
    private List<UtuPluginActor> actors = new List<UtuPluginActor>();
    private List<GameObject> gameObjects = new List<GameObject>();
    private Dictionary<int, List<UtuPluginActorPrefabComponentOverride>> prefabOverridesDetectedInScene = new Dictionary<int, List<UtuPluginActorPrefabComponentOverride>>();

    public int countActorsToProcess = 1;
    public int amountActorsToProcess = 0;
    public float percentActorsToProcess = 0.0f;
    public string nameActorToProcess = "";


    public void ExportScene(UtuPluginJson utuPluginJson, string sceneRelativeFilename, bool executeFullExportOnSameFrame)
    {
        BeginExportScene(utuPluginJson, sceneRelativeFilename);
        if (executeFullExportOnSameFrame)
        {
            while (ContinueExportScene() != true)
            {
                // ContinueExport 
            }
        }
    }

    public void BeginExportScene(UtuPluginJson utuPluginJson, string sceneRelativeFilename)
    {
        json = utuPluginJson;
        scene = CreateScene(sceneRelativeFilename);
        UtuLog.Separator();
        UtuLog.Log("    Generating Data for Scene...");
        UtuLog.Log("        Scene Name: " + scene.asset_name);
        UtuLog.Log("        Scene Relative Name: " + scene.asset_relative_filename);
        UtuLog.SemiSeparator("    ");
        UtuLog.Log("    Opening Scene...");
        gameObjects = new List<GameObject>(OpenSceneWorld(sceneRelativeFilename));
        actors = new List<UtuPluginActor>();
        prefabOverridesDetectedInScene = new Dictionary<int, List<UtuPluginActorPrefabComponentOverride>>();
        countActorsToProcess = 1;
        amountActorsToProcess = gameObjects.Count;
        percentActorsToProcess = (float)countActorsToProcess / (float)amountActorsToProcess;
        UtuLog.Log("        Quantity GameObjects in Scene: " + gameObjects.Count.ToString());
        UtuLog.SemiSeparator("    ");
        UtuLog.Log("    Generating Data for GameObjects...");
    }

    public bool ContinueExportScene()
    {
        if (gameObjects.Count == 0)
        {
            UtuLog.Error("UtuPluginSceneProcessor::ContinueExportScene() Was called even though the list is already empty. This should never happen!");
            return true;
        }
        nameActorToProcess = gameObjects[0].name;
        UtuPluginActor actor = ProcessActor(gameObjects[0], true);
        if (actor != null)
        {
            scene.scene_actors.Add(actor);
        }
        gameObjects.RemoveAt(0);
        countActorsToProcess++;
        percentActorsToProcess = (float)countActorsToProcess / (float)amountActorsToProcess;
        if (gameObjects.Count == 0)
        {
            CompleteExportScene();
            return true;
        }
        return false;
    }

    private void CompleteExportScene()
    {
        UtuLog.SemiSeparator("    ");
        UtuLog.Log("    Applying Prefabs Overrides...");
        foreach (UtuPluginActor actor in scene.scene_actors)
        {
            int id = actor.actor_id;
            if (prefabOverridesDetectedInScene.ContainsKey(id) && actor.actor_prefab != null)
            {
                actor.actor_prefab.actor_prefab_component_overrides = prefabOverridesDetectedInScene[id];
            }
        }
        prefabOverridesDetectedInScene = new Dictionary<int, List<UtuPluginActorPrefabComponentOverride>>();

        UtuLog.SemiSeparator("    ");
        UtuLog.Log("    Data Generation for Scene Completed!");
        json.scenes.Add(scene);
        json.json_info.scenes.Add(scene.asset_name);
    }



    public void ExportAsset(UtuPluginJson utuPluginJson, string assetRelativeFilename, bool isPrefab, bool isMesh, bool isAnimation, bool isMaterial, bool isTexture)
    {
        json = utuPluginJson;
        UtuLog.Separator();
        UtuLog.Log("    Generating Data for Asset...");
        UtuLog.Log("        Asset Relative Name: " + assetRelativeFilename);
        UtuLog.SemiSeparator("    ");

        if (isPrefab)
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(assetRelativeFilename);
            AddPrefabToGlobalPrefabList(assetRelativeFilename, go);
        }
        else if (isMesh)
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(assetRelativeFilename);
            if (go == null)
            {
                AddMeshToGlobalMeshList(assetRelativeFilename, null);
            }
            else if (go.GetComponent<SkinnedMeshRenderer>() != null)
            {
                AddMeshToGlobalSkeletalMeshList(assetRelativeFilename, go.GetComponent<SkinnedMeshRenderer>(), null);
            }
            else
            {
                AddMeshToGlobalMeshList(assetRelativeFilename, go.GetComponent<MeshFilter>());
            }
        }
        else if (isAnimation)
        {
            AnimationClip anim = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetRelativeFilename);
            AddAnimationToGlobalAnimationList(assetRelativeFilename, anim, "");
        }
        else if (isMaterial)
        {
            UnityEngine.Object[] possiblePaterials = AssetDatabase.LoadAllAssetsAtPath(assetRelativeFilename);
            foreach (UnityEngine.Object obj in possiblePaterials)
            {
                Material mat = obj as Material;
                if (mat != null)
                {
                    AddMaterialToGlobalMaterialList(GetUnrealMaterialRelativeFilenameFromRelativeFilename(assetRelativeFilename, mat), mat);
                }
            }
        }
        else if (isTexture)
        {
            Texture tex = AssetDatabase.LoadAssetAtPath<Texture>(assetRelativeFilename);
            AddTextureToGlobalMaterialList(assetRelativeFilename, tex);
        }

        UtuLog.SemiSeparator("    ");
        UtuLog.Log("    Data Generation for Asset Completed!");
    }


    private string GetUnrealMaterialRelativeFilenameFromRelativeFilename(string materialRelativeFilename, Material material)
    {
        if (!materialRelativeFilename.ToLower().EndsWith(".mat") && material != null && Path.GetExtension(materialRelativeFilename) != "")
        {
            return materialRelativeFilename.Replace(Path.GetExtension(materialRelativeFilename), material.name + Path.GetExtension(materialRelativeFilename));
        }
        return materialRelativeFilename;
    }


    private UtuPluginScene CreateScene(string sceneRelativeFilename)
    {
        UtuPluginScene utuPluginScene = new UtuPluginScene();
        utuPluginScene.asset_relative_filename = sceneRelativeFilename;
        utuPluginScene.asset_name = sceneRelativeFilename.Split(UtuPluginPaths.slash).Last<string>();
        ErrorIfBadCharactersInPath(utuPluginScene.asset_relative_filename);
        return utuPluginScene;
    }

    private GameObject[] OpenSceneWorld(string sceneRelativeFilename)
    {
        EditorSceneManager.OpenScene(sceneRelativeFilename, OpenSceneMode.Single);
        return GetAllGameObjectsInCurrentScene();
    }

    public static GameObject[] GetAllGameObjectsInCurrentScene()
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
#else
        return UnityEngine.Object.FindObjectsOfType<GameObject>();
#endif
    }

    private UtuPluginActor ProcessActor(GameObject go, bool enableLog)
    {
        UtuPluginActor utuPluginActor = CreateActor(go);
        if (PrefabUtility.IsPartOfRegularPrefab(go))
        {
            // Gather overrides
            int actorId = PrefabUtility.GetOutermostPrefabInstanceRoot(go).GetInstanceID();
            if (!prefabOverridesDetectedInScene.ContainsKey(actorId))
            {
                prefabOverridesDetectedInScene.Add(actorId, new List<UtuPluginActorPrefabComponentOverride>());
            }
            UtuPluginActorPrefabComponentOverride overrides = new UtuPluginActorPrefabComponentOverride();
            overrides.component_display_name = go.name;

            if (go.GetComponent<MeshFilter>() != null || go.GetComponent<SkinnedMeshRenderer>() != null)
            {
                // Mesh
                if (go.GetComponent<SkinnedMeshRenderer>() != null)
                {
                    overrides.mesh_relative_filename = AssetDatabase.GetAssetPath(go.GetComponent<SkinnedMeshRenderer>().sharedMesh);
                    overrides.mesh_relative_filename_if_separated = overrides.mesh_relative_filename;
                    AddMeshToGlobalSkeletalMeshList(overrides.mesh_relative_filename, go.GetComponent<SkinnedMeshRenderer>(), null);
                }
                else if (go.GetComponent<MeshFilter>() != null)
                {
                    List<string> mesh_filenames = GetMeshFileNames(go);
                    overrides.mesh_relative_filename = mesh_filenames[0];
                    overrides.mesh_relative_filename_if_separated = mesh_filenames[1];
                    AddMeshToGlobalMeshList(overrides.mesh_relative_filename, go.GetComponent<MeshFilter>());
                }

                // Animations
                Animator animator = go.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = go.GetComponentInParent<Animator>();
                }
                if (animator != null && animator.gameObject != null)
                {
                    foreach (AnimationClip animationClip in AnimationUtility.GetAnimationClips(animator.gameObject))
                    {
                        string animationRelativeFilename = AssetDatabase.GetAssetPath(animationClip);
                        if (!overrides.animation_relative_filenames.Contains(animationRelativeFilename))
                        {
                            overrides.animation_relative_filenames.Add(animationRelativeFilename);
                        }
                        AddAnimationToGlobalAnimationList(animationRelativeFilename, animationClip, overrides.mesh_relative_filename);
                    }
                }

                // Materials
                if (go.GetComponent<Renderer>())
                {
                    Material[] materials = go.GetComponent<Renderer>().sharedMaterials;
                    foreach (Material material in materials)
                    {
                        string materialRelativeFilename = GetUnrealMaterialRelativeFilenameFromRelativeFilename(AssetDatabase.GetAssetPath(material), material);
                        overrides.material_relative_filenames.Add(materialRelativeFilename);
                        AddMaterialToGlobalMaterialList(materialRelativeFilename, material);
                    }
                }
            }
            prefabOverridesDetectedInScene[actorId].Add(overrides);

            // Process actual prefab
            if (PrefabUtility.GetOutermostPrefabInstanceRoot(go) == go)
            {
                if (/*!go.name.Contains("(Missing Prefab)") &&*/ PrefabUtility.GetCorrespondingObjectFromSource(go) != null)
                {
                    utuPluginActor.actor_types.Add(UtuActorType.Prefab);
                    utuPluginActor.actor_prefab = CreateActorPrefab(go);
                }
                else
                {
                    UtuLog.Warning("        Missing Prefab detected! Exporting GameObject '" + go.name + "' as Empty GameObject.");
                    utuPluginActor.actor_types.Add(UtuActorType.Empty);
                }
            }
            else
            {
                return null; // Do not return prefabs components as they won't be added individually in the scene!
            }
        }
        else
        {
            if (go.GetComponent<MeshFilter>())
            {
                if (go.GetComponent<MeshFilter>().sharedMesh)
                {
                    utuPluginActor.actor_types.Add(UtuActorType.StaticMesh);
                    utuPluginActor.actor_mesh = CreateActorMesh(go);
                    // Separated
                    {
                        MeshFilter meshFilter = go.GetComponent<MeshFilter>();
                        string subMeshName = meshFilter.sharedMesh.ToString().Replace(" (UnityEngine.Mesh)", "");
                    }
                    if (go.GetComponent<Renderer>())
                    {
                        utuPluginActor.actor_is_visible = utuPluginActor.actor_is_visible && go.GetComponent<Renderer>().enabled;
                    }
                }
                else
                {
                    UtuLog.Warning("        Missing Mesh in MeshFilter component detected while analysing: '" + go.name + "'. Component will be ignored.");
                }
            }
            if (go.GetComponent<SkinnedMeshRenderer>())
            {
                if (go.GetComponent<SkinnedMeshRenderer>().sharedMesh)
                {
                    utuPluginActor.actor_types.Add(UtuActorType.SkeletalMesh);
                    utuPluginActor.actor_mesh = CreateActorSkeletalMesh(go);
                    // Cancel the transform. Because ... I don't know.
                    utuPluginActor.actor_relative_location = Vector3.zero;
                    utuPluginActor.actor_relative_rotation = Quaternion.identity;
                    utuPluginActor.actor_relative_scale = Vector3.one;
                    utuPluginActor.actor_world_location = go.transform.parent.position;
                    utuPluginActor.actor_world_rotation = go.transform.parent.rotation;
                    utuPluginActor.actor_world_scale = go.transform.parent.lossyScale;

                    if (go.GetComponent<SkinnedMeshRenderer>())
                    {
                        utuPluginActor.actor_is_visible = utuPluginActor.actor_is_visible && go.GetComponent<SkinnedMeshRenderer>().enabled;
                    }
                }
                else
                {
                    UtuLog.Warning("        Missing Mesh in SkinnedMeshRenderer component detected while analysing: '" + go.name + "'. Component will be ignored.");
                }
            }
            if (go.GetComponent<Light>())
            {
                Light light = go.GetComponent<Light>();
                if (light.type == LightType.Spot)
                {
                    utuPluginActor.actor_types.Add(UtuActorType.SpotLight);
                    utuPluginActor.actor_light = CreateActorLight(go);
                }
                else if (light.type == LightType.Directional)
                {
                    utuPluginActor.actor_types.Add(UtuActorType.DirectionalLight);
                    utuPluginActor.actor_light = CreateActorLight(go);
                }
                else if (light.type == LightType.Point)
                {
                    utuPluginActor.actor_types.Add(UtuActorType.PointLight);
                    utuPluginActor.actor_light = CreateActorLight(go);
                }
                else
                {
                    UtuLog.Warning("        Unsupported LightType '" + light.type.ToString() + "' detected while analysing: '" + go.name + "'. Component will be ignored.");
                }
                utuPluginActor.actor_is_visible = utuPluginActor.actor_is_visible && light.enabled;
            }
            if (go.GetComponent<Camera>())
            {
                utuPluginActor.actor_types.Add(UtuActorType.Camera);
                utuPluginActor.actor_camera = CreateActorCamera(go);
            }
            if (utuPluginActor.actor_types.Count != 1)
            { // If actor_types == 1, we don't need to have an empty root above the other components.
                UtuLog.Log("        Because GameObject '" + go.name + "' has " + utuPluginActor.actor_types.Count.ToString() + " supported components, adding an Empty GameObject as root of its hierarchy.");
                utuPluginActor.actor_types.Add(UtuActorType.Empty);
            }
        }

        //// Export animations of gameobject if detected
        //if (go.GetComponent<Animator>() != null)
        //{
        //    foreach (AnimationClip animationClip in AnimationUtility.GetAnimationClips(go))
        //    {
        //        string path = AssetDatabase.GetAssetPath(animationClip);
        //        AddAnimationToGlobalAnimationList(path, animationClip);
        //    }
        //}

        return utuPluginActor;
    }

    // Actor -----------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private UtuPluginActor CreateActor(GameObject go)
    {
        UtuPluginActor utuPluginActor = new UtuPluginActor();
        utuPluginActor.actor_id = go.GetInstanceID();
        utuPluginActor.actor_parent_id = go.transform.parent ? go.transform.parent.gameObject.GetInstanceID() : UtuConst.INVALID_INT;
        utuPluginActor.actor_display_name = go.name;
        utuPluginActor.actor_tag = go.tag;
        utuPluginActor.actor_is_visible = go.activeInHierarchy;
        utuPluginActor.actor_world_location = go.transform.position;
        utuPluginActor.actor_world_rotation = go.transform.rotation;
        utuPluginActor.actor_world_scale = go.transform.lossyScale;
        utuPluginActor.actor_relative_location = go.transform.localPosition;
        utuPluginActor.actor_relative_rotation = go.transform.localRotation;
        utuPluginActor.actor_relative_scale = go.transform.localScale;
        utuPluginActor.actor_is_movable = !go.isStatic;

        // Because Unreal does not give you the possibility to have Static actor child of a Movable actor
        if (go.isStatic)
        {
            Transform transform = go.transform;
            while (transform.parent != null)
            {
                transform = transform.parent;
                if (!transform.gameObject.isStatic)
                {
                    // Movable Parent Detected
                    UtuLog.Warning("        Detected a Static Gameobject with a Movable parent, which cannot be done in Unreal. This object will be set to movable. \n            Movable Object: " + transform.name + " \n            Static Object: " + go.name);
                    utuPluginActor.actor_is_movable = true;
                }
            }
        }

        return utuPluginActor;
    }


    // Camera -----------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private UtuPluginActorCamera CreateActorCamera(GameObject go)
    {
        Camera camera = go.GetComponent<Camera>();
        UtuPluginActorCamera utuPluginActorCamera = new UtuPluginActorCamera();
        utuPluginActorCamera.camera_aspect_ratio = camera.aspect;
        utuPluginActorCamera.camera_far_clip_plane = camera.farClipPlane;
        utuPluginActorCamera.camera_persp_field_of_view = camera.fieldOfView;
        utuPluginActorCamera.camera_phys_focal_length = camera.focalLength;
        utuPluginActorCamera.camera_is_perspective = !camera.orthographic;
        utuPluginActorCamera.camera_is_physical = camera.usePhysicalProperties;
        utuPluginActorCamera.camera_near_clip_plane = camera.nearClipPlane;
        utuPluginActorCamera.camera_phys_sensor_size = camera.sensorSize;
        utuPluginActorCamera.camera_viewport_rect = new Quaternion(camera.rect.x, camera.rect.y, camera.rect.height, camera.rect.width);
        utuPluginActorCamera.camera_ortho_size = camera.orthographicSize;
        return utuPluginActorCamera;
    }


    // Light -----------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private UtuPluginActorLight CreateActorLight(GameObject go)
    {
        Light light = go.GetComponent<Light>();
        UtuPluginActorLight utuPluginActorLight = new UtuPluginActorLight();
        utuPluginActorLight.light_color = UnityEngine.ColorUtility.ToHtmlStringRGBA(light.color); //new Quaternion(light.color.r, light.color.g, light.color.b, light.color.a);
        utuPluginActorLight.light_intensity = light.intensity;
        if (RenderPipelineUtils.GetPipeline() == RenderPipelineUtils.PipelineType.HDPipeline)
        {
            utuPluginActorLight.light_intensity = light.intensity * 0.0001f;
        }
        utuPluginActorLight.light_is_casting_shadows = light.shadows != LightShadows.None;
        utuPluginActorLight.light_range = light.range;
        utuPluginActorLight.light_spot_angle = light.spotAngle;
        return utuPluginActorLight;
    }


    // Static Mesh -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private List<string> GetMeshFileNames(GameObject go)
    {
        List<string> list = new List<string>();
        if (go == null)
        {
            list.Add("");
            list.Add("");
            return list;
        }

        MeshFilter meshFilter = go.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            list.Add("");
            list.Add("");
            return list;
        }

        string assetRelativeFilename = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
        if (assetRelativeFilename == "")
        {
            // fake it for instances
            if (meshFilter.sharedMesh.name.Contains(" Instance"))
            {
                foreach (UtuPluginMesh mesh in json.meshes)
                {
                    if (mesh.asset_relative_filename.Contains(meshFilter.sharedMesh.name.Replace(" Instance", "")))
                    {
                        assetRelativeFilename = mesh.asset_relative_filename;
                        break;
                    }
                }
            }
            else
            {
                // Fake it for meshes with broken MeshFilters
                string meshName = go.name;
                if (go.name.Contains(" ("))
                {
                    meshName = go.name.Substring(0, go.name.LastIndexOf('('));
                }
                foreach (UtuPluginMesh mesh in json.meshes)
                {
                    if (mesh.asset_relative_filename.Contains(meshName))
                    {
                        assetRelativeFilename = mesh.asset_relative_filename;
                        UtuLog.Warning("        Using mesh '" + assetRelativeFilename + "' for gameobject '" + go.name + "' because it doesn't have a valid MeshFilter component.");
                        break;
                    }
                }
            }
            if (assetRelativeFilename == "")
            {
                UtuLog.Warning("        Mesh filter is empty on gameobject  '" + go.name + "'");
            }
        }

        string subMeshName = meshFilter.sharedMesh.ToString().Replace(" (UnityEngine.Mesh)", ""); //meshFilter.sharedMesh.ToString().Split(' ').Last<string>()
        string mesh_relative_filename = assetRelativeFilename.Contains(UtuConst.DEFAULT_RESOURCES) ? meshFilter.sharedMesh.name : assetRelativeFilename;
        mesh_relative_filename = AddMeshToGlobalMeshList(mesh_relative_filename, meshFilter);
        string extension = "." + mesh_relative_filename.Split('.').Last<string>();
        string mesh_relative_filename_if_separated = mesh_relative_filename.Replace(extension, "_" + subMeshName.Replace(" Instance", "") + extension);

        list.Add(mesh_relative_filename);
        list.Add(mesh_relative_filename_if_separated);
        return list;
    }



    private UtuPluginActorMesh CreateActorMesh(GameObject go)
    {
        UtuPluginActorMesh utuPluginActorMesh = new UtuPluginActorMesh();
        if (go.GetComponent<Renderer>())
        {
            Material[] materials = go.GetComponent<Renderer>().sharedMaterials;
            foreach (Material material in materials)
            {
                string materialRelativeFilename = GetUnrealMaterialRelativeFilenameFromRelativeFilename(AssetDatabase.GetAssetPath(material), material);
                utuPluginActorMesh.actor_mesh_materials_relative_filenames.Add(materialRelativeFilename);
                AddMaterialToGlobalMaterialList(materialRelativeFilename, material);
            }
        }

        // Because Unreal does not give you the possibility of stretching the vertices and deform the mesh's default shape.
        Vector3 rot = go.transform.rotation.eulerAngles;
        if (!UtuConst.NearlyEquals(rot.x, 0.0f, 1.0f) || !UtuConst.NearlyEquals(rot.y, 0.0f, 1.0f) || !UtuConst.NearlyEquals(rot.y, 0.0f, 1.0f))
        {
            // Rotation Detected
            Transform transform = go.transform;
            while (transform.parent != null)
            {
                transform = transform.parent;
                if (!UtuConst.NearlyEquals(transform.lossyScale.x, transform.lossyScale.y, 0.1f) || !UtuConst.NearlyEquals(transform.lossyScale.x, transform.lossyScale.z, 0.1f))
                {
                    // Non-Uniform Scaling Detected
                    UtuLog.Warning("        Non-Uniform Scaling Detected on a Parent of a Rotated Static Mesh! The Static Mesh may not be imported correctly in Unreal. \n            Scaled Object: " + transform.name + " \n            Rotated Child Static Mesh: " + go.name);
                }
            }
        }

        List<string> mesh_filenames = GetMeshFileNames(go);
        utuPluginActorMesh.actor_mesh_relative_filename = mesh_filenames[0];
        utuPluginActorMesh.actor_mesh_relative_filename_if_separated = mesh_filenames[1];

        // Animations
        Animator animator = go.GetComponent<Animator>();
        if (animator == null)
        {
            animator = go.GetComponentInParent<Animator>();
        }
        if (animator != null && animator.gameObject != null)
        {
            foreach (AnimationClip animationClip in AnimationUtility.GetAnimationClips(animator.gameObject))
            {
                string animationRelativeFilename = AssetDatabase.GetAssetPath(animationClip);
                utuPluginActorMesh.actor_mesh_animations_relative_filenames.Add(animationRelativeFilename);
                AddAnimationToGlobalAnimationList(animationRelativeFilename, animationClip, utuPluginActorMesh.actor_mesh_relative_filename);
                AddAnimationToGlobalAnimationList(animationRelativeFilename, animationClip, utuPluginActorMesh.actor_mesh_relative_filename_if_separated);
            }
        }

        return utuPluginActorMesh;
    }

    private UtuPluginActorMesh CreateActorSkeletalMesh(GameObject go)
    {
        SkinnedMeshRenderer meshRenderer = go.GetComponent<SkinnedMeshRenderer>();
        UtuPluginActorMesh utuPluginActorMesh = new UtuPluginActorMesh();
        string assetRelativeFilename = AssetDatabase.GetAssetPath(meshRenderer.sharedMesh);
        utuPluginActorMesh.actor_mesh_relative_filename = assetRelativeFilename.Contains(UtuConst.DEFAULT_RESOURCES) ? meshRenderer.sharedMesh.name : assetRelativeFilename;

        // Materials
        Material[] materials = meshRenderer.sharedMaterials;
        foreach (Material material in materials)
        {
            string materialRelativeFilename = GetUnrealMaterialRelativeFilenameFromRelativeFilename(AssetDatabase.GetAssetPath(material), material);
            utuPluginActorMesh.actor_mesh_materials_relative_filenames.Add(materialRelativeFilename);
            AddMaterialToGlobalMaterialList(materialRelativeFilename, material);
        }

        // Animations
        Animator animator = go.GetComponent<Animator>();
        if (animator == null)
        {
            animator = go.GetComponentInParent<Animator>();
        }
        if (animator != null && animator.gameObject != null)
        {
            foreach (AnimationClip animationClip in AnimationUtility.GetAnimationClips(animator.gameObject))
            {
                string animationRelativeFilename = AssetDatabase.GetAssetPath(animationClip);
                utuPluginActorMesh.actor_mesh_animations_relative_filenames.Add(animationRelativeFilename);
                AddAnimationToGlobalAnimationList(animationRelativeFilename, animationClip, utuPluginActorMesh.actor_mesh_relative_filename);
            }
        }

        AddMeshToGlobalSkeletalMeshList(utuPluginActorMesh.actor_mesh_relative_filename, meshRenderer, null);
        return utuPluginActorMesh;
    }

    private bool IsFbxExporterNeededForThisMesh(string meshRelativeFilename)
    {
        return meshRelativeFilename.Contains(".") && !(meshRelativeFilename.ToLower().EndsWith(".fbx") || meshRelativeFilename.ToLower().EndsWith(".obj"));
    }

    private string AddMeshToGlobalMeshList(string meshRelativeFilename, MeshFilter meshFilter)
    {
        string neverDotAssetMeshRelativeFilename = meshRelativeFilename;

        // Replace weird extensions with .fbx since they'll be exported
        if (FbxExportSupport.IsFbxExporterAvailable())
        {
            string extension = "." + meshRelativeFilename.Split('.').Last<string>();
            if (IsFbxExporterNeededForThisMesh(meshRelativeFilename))
            {
                neverDotAssetMeshRelativeFilename = meshRelativeFilename.Replace(extension, ".fbx");
            }
        }

        if (meshRelativeFilename != "" && !json.existing_meshes.Contains(meshRelativeFilename) && !json.existing_meshes.Contains(neverDotAssetMeshRelativeFilename))
        {
            string ExportedFbxPath = UtuPluginPaths.GetExportedFbxFilesFolder();

            if (IsFbxExporterNeededForThisMesh(meshRelativeFilename))
            {
                if (FbxExportSupport.IsFbxExporterAvailable())
                {
                    // Export the asset WITH_FBX_EXPORTER
                    if (!File.Exists(ExportedFbxPath + meshRelativeFilename) || UtuPluginCurrentExport.forceReExportOfFbxExporterMeshes)
                    {
                        GameObject asset = AssetDatabase.LoadMainAssetAtPath(meshRelativeFilename) as GameObject;
                        Mesh mesh = AssetDatabase.LoadMainAssetAtPath(meshRelativeFilename) as Mesh;
                        if (mesh == null)
                        {
                            if (asset != null && asset.GetComponent<MeshFilter>() != null)
                            {
                                mesh = asset.GetComponent<MeshFilter>().mesh != null ? asset.GetComponent<MeshFilter>().mesh : asset.GetComponent<MeshFilter>().sharedMesh;
                            }
                        }
                        if (mesh != null)
                        {
                            // Create object
                            GameObject SpawnedGo = new GameObject();
                            SpawnedGo.AddComponent<MeshFilter>();
                            SpawnedGo.GetComponent<MeshFilter>().mesh = mesh;
                            SpawnedGo.AddComponent<MeshRenderer>();

                            List<Material> Mats = new List<Material>();
                            if (asset != null)
                            {
                                if (asset.GetComponent<Renderer>() != null)
                                {
                                    Mats.AddRange(asset.GetComponent<Renderer>().sharedMaterials);
                                }
                            }

                            int noInfinite = 0;
                            while (Mats.Count < mesh.subMeshCount && noInfinite < 500)
                            {
                                noInfinite++;

                                Shader emptyShader = (Shader)AssetDatabase.LoadAssetAtPath(UtuPluginPaths.pluginShader_Rel_EmptyShader, typeof(Shader));
                                Mats.Add(new Material(emptyShader));
                            }
                            SpawnedGo.GetComponent<MeshRenderer>().sharedMaterials = Mats.ToArray();

                            // Export object
                            FbxExportSupport.Export(ExportedFbxPath + neverDotAssetMeshRelativeFilename, SpawnedGo);
                            // Destroy object
                            UnityEngine.Object.DestroyImmediate(SpawnedGo);
                        }
                    }
                }
                else
                {
                    UtuLog.Error("        This mesh is not a .fbx or .obj file and you don't have FBX Exporter installed. It won't be imported in Unreal.\n            Static Mesh: " + meshRelativeFilename + "\n            Please install FBX Exporter if you want to process this mesh. \n            https://docs.unity3d.com/Packages/com.unity.formats.fbx@2.0/manual/index.html");
                }
            }

            json.existing_meshes.Add(neverDotAssetMeshRelativeFilename);
            UtuPluginMesh utuPluginMesh = new UtuPluginMesh();
            utuPluginMesh.asset_relative_filename = neverDotAssetMeshRelativeFilename;
            ErrorIfBadCharactersInPath(utuPluginMesh.asset_relative_filename);
            utuPluginMesh.asset_name = neverDotAssetMeshRelativeFilename.Split(UtuPluginPaths.slash).Last<string>();
            utuPluginMesh.is_skeletal_mesh = false;

            if (neverDotAssetMeshRelativeFilename.Contains(UtuConst.DEFAULT_RESOURCES))
            {
                utuPluginMesh.mesh_file_absolute_filename = meshFilter.sharedMesh.name;
            }
            else
            {
                utuPluginMesh.mesh_file_absolute_filename = UtuPluginPaths.unityFolder_Full_Project + neverDotAssetMeshRelativeFilename;
            }

            if (FbxExportSupport.IsFbxExporterAvailable() && IsFbxExporterNeededForThisMesh(meshRelativeFilename))
            {
                utuPluginMesh.mesh_file_absolute_filename = ExportedFbxPath + neverDotAssetMeshRelativeFilename;
            }

            ModelImporter modelImporter = (AssetImporter.GetAtPath(neverDotAssetMeshRelativeFilename) as ModelImporter);
            utuPluginMesh.mesh_import_scale_factor = modelImporter ? modelImporter.useFileScale == false ? modelImporter.globalScale * 100.0f : modelImporter.globalScale : 1.0f;
            utuPluginMesh.use_file_scale = modelImporter ? modelImporter.useFileScale : false;

            if (FbxExportSupport.IsFbxExporterAvailable() && IsFbxExporterNeededForThisMesh(meshRelativeFilename))
            {
                UtuLog.Warning("        It is not possible to detect the proper import scale of this mesh, it may be imported with a wrong scale in Unreal.\n            Static Mesh: " + meshRelativeFilename);
                utuPluginMesh.mesh_import_scale_factor = 1.0f;
                utuPluginMesh.use_file_scale = true;
            }


            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(meshRelativeFilename);
            if (go != null)
            {
                utuPluginMesh.mesh_import_position_offset = go.transform.position;
                utuPluginMesh.mesh_import_rotation_offset = go.transform.rotation;
                utuPluginMesh.mesh_import_scale_offset = go.transform.lossyScale;

                if (!UtuConst.NearlyEquals(go.transform.lossyScale.x, go.transform.lossyScale.y, 0.01f) || !UtuConst.NearlyEquals(go.transform.lossyScale.x, go.transform.lossyScale.z, 0.01f))
                {
                    UtuLog.Warning("        Non-Uniform Scaling Detected on this Static Mesh! Unreal only allows Uniform Scaling when importing meshes. (X,Y,Z channels must have the same value.) The Static Mesh may not be imported correctly in Unreal. \n            Static Mesh: " + utuPluginMesh.asset_relative_filename);
                }
                if (go.transform.childCount > 0)
                {
                    Transform[] children_transforms = go.transform.GetComponentsInChildren<Transform>();
                    foreach (Transform transform in children_transforms)
                    {
                        // Because GetComponentsInChildren includes self for some reasons
                        if (transform != go.transform)
                        {
                            UtuPluginSubmesh subMesh = new UtuPluginSubmesh();

                            subMesh.submesh_name = transform.name;
                            subMesh.submesh_relative_location = transform.localPosition;
                            subMesh.submesh_relative_rotation = transform.localRotation;
                            subMesh.submesh_relative_scale = transform.localScale;
                            subMesh.submesh_world_location = transform.position;
                            subMesh.submesh_world_rotation = transform.rotation;
                            subMesh.submesh_world_scale = transform.lossyScale;
                            if (transform.gameObject.GetComponent<Renderer>() != null)
                            {
                                Material[] materials = transform.gameObject.GetComponent<Renderer>().sharedMaterials;
                                foreach (Material material in materials)
                                {
                                    string materialRelativeFilename = GetUnrealMaterialRelativeFilenameFromRelativeFilename(AssetDatabase.GetAssetPath(material), material);
                                    subMesh.submesh_materials_relative_filenames.Add(materialRelativeFilename);
                                    AddMaterialToGlobalMaterialList(materialRelativeFilename, material);
                                }
                            }
                            utuPluginMesh.submeshes.Add(subMesh);
                        }
                    }
                    UtuLog.Log("        More than one mesh detected in fbx file. The Static Mesh may not be imported correctly in Unreal. \n            Static Mesh: " + utuPluginMesh.asset_relative_filename);
                }
                if (go.GetComponent<Renderer>() != null)
                {
                    Material[] materials = go.GetComponent<Renderer>().sharedMaterials;
                    foreach (Material material in materials)
                    {
                        string materialRelativeFilename = GetUnrealMaterialRelativeFilenameFromRelativeFilename(AssetDatabase.GetAssetPath(material), material);
                        utuPluginMesh.mesh_materials_relative_filenames.Add(materialRelativeFilename);
                        AddMaterialToGlobalMaterialList(materialRelativeFilename, material);
                    }
                }

                // Animations
                if (meshFilter != null)
                {
                    GameObject goForAnim = meshFilter.gameObject;

                    if (goForAnim != null)
                    {
                        Animator animator = goForAnim.GetComponent<Animator>();
                        if (animator == null)
                        {
                            animator = goForAnim.GetComponentInParent<Animator>();
                        }
                        if (animator != null && animator.gameObject != null)
                        {
                            foreach (AnimationClip animationClip in AnimationUtility.GetAnimationClips(animator.gameObject))
                            {
                                string path = AssetDatabase.GetAssetPath(animationClip);
                                AddAnimationToGlobalAnimationList(path, animationClip, utuPluginMesh.asset_relative_filename);
                            }
                        }
                    }
                }
            }
            json.meshes.Add(utuPluginMesh);
            json.json_info.meshes.Add(utuPluginMesh.asset_name);
        }

        return neverDotAssetMeshRelativeFilename;
    }

    // Non skinned mesh renderer just used for animations. Sometimes they can play on normal mesh renderer for some reason
    private void AddMeshToGlobalSkeletalMeshList(string meshRelativeFilename, SkinnedMeshRenderer skinnedMeshRenderer, MeshRenderer meshRenderer)
    {
        if (!json.existing_skel_meshes.Contains(meshRelativeFilename) && meshRelativeFilename != "")
        {
            json.existing_skel_meshes.Add(meshRelativeFilename);
            UtuPluginMesh utuPluginMesh = new UtuPluginMesh();
            utuPluginMesh.asset_relative_filename = meshRelativeFilename;
            ErrorIfBadCharactersInPath(utuPluginMesh.asset_relative_filename);
            utuPluginMesh.asset_name = meshRelativeFilename.Split(UtuPluginPaths.slash).Last<string>();
            if (meshRelativeFilename.Contains(UtuConst.DEFAULT_RESOURCES))
            {
                if (skinnedMeshRenderer != null)
                {
                    utuPluginMesh.mesh_file_absolute_filename = skinnedMeshRenderer.sharedMesh.name;
                }
            }
            else
            {
                utuPluginMesh.mesh_file_absolute_filename = UtuPluginPaths.unityFolder_Full_Project + meshRelativeFilename;
            }
            utuPluginMesh.is_skeletal_mesh = true;
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(meshRelativeFilename);
            utuPluginMesh.mesh_import_scale_factor = (AssetImporter.GetAtPath(meshRelativeFilename) as ModelImporter) ? (AssetImporter.GetAtPath(meshRelativeFilename) as ModelImporter).globalScale : 1.0f;
            if (go != null)
            {
                utuPluginMesh.mesh_import_position_offset = go.transform.position;
                utuPluginMesh.mesh_import_rotation_offset = go.transform.rotation;
                utuPluginMesh.mesh_import_scale_offset = go.transform.lossyScale;
                if (go.GetComponent<Renderer>() != null)
                {
                    Material[] materials = go.GetComponent<Renderer>().sharedMaterials;
                    foreach (Material material in materials)
                    {
                        string materialRelativeFilename = GetUnrealMaterialRelativeFilenameFromRelativeFilename(AssetDatabase.GetAssetPath(material), material);
                        utuPluginMesh.mesh_materials_relative_filenames.Add(materialRelativeFilename);
                        AddMaterialToGlobalMaterialList(materialRelativeFilename, material);
                    }
                }
            }

            // Animations
            if (skinnedMeshRenderer != null || meshRenderer != null)
            {
                GameObject goForAnim = null;
                if (skinnedMeshRenderer != null)
                {
                    goForAnim = skinnedMeshRenderer.gameObject;
                }
                if (meshRenderer != null)
                {
                    goForAnim = meshRenderer.gameObject;

                }

                if (goForAnim != null)
                {
                    Animator animator = goForAnim.GetComponent<Animator>();
                    if (animator == null)
                    {
                        animator = goForAnim.GetComponentInParent<Animator>();
                    }
                    if (animator != null && animator.gameObject != null)
                    {
                        foreach (AnimationClip animationClip in AnimationUtility.GetAnimationClips(animator.gameObject))
                        {
                            string path = AssetDatabase.GetAssetPath(animationClip);
                            AddAnimationToGlobalAnimationList(path, animationClip, utuPluginMesh.asset_relative_filename);
                        }
                    }
                }
            }

            json.meshes.Add(utuPluginMesh);
            json.json_info.meshes.Add(utuPluginMesh.asset_name);
        }
    }

    private void AddAnimationToGlobalAnimationList(string animationRelativeFilename, AnimationClip animation, string linkedMeshRelativeFilename)
    {
        // Mesh found during process, add it to the animation list if needed
        if (linkedMeshRelativeFilename.Contains("Assets"))
        {
            UtuPluginAssets.LinkClipToSkeletalMesh(animationRelativeFilename, linkedMeshRelativeFilename);
            if (json.existing_animations.Contains(animationRelativeFilename))
            {
                for (int x = 0; x < json.animations.Count(); x++)
                {
                    if (json.animations[x].asset_relative_filename == animationRelativeFilename)
                    {
                        if (!json.animations[x].associated_skeletal_meshes_relative_filenames.Contains(linkedMeshRelativeFilename))
                        {
                            json.animations[x].associated_skeletal_meshes_relative_filenames.Add(linkedMeshRelativeFilename); // Add unique
                        }
                        break;
                    }
                }
            }
        }

        if (!json.existing_animations.Contains(animationRelativeFilename) && animationRelativeFilename != "")
        {
            json.existing_animations.Add(animationRelativeFilename);
            UtuPluginAnimation utuPluginAnimation = new UtuPluginAnimation();
            utuPluginAnimation.asset_relative_filename = animationRelativeFilename;
            ErrorIfBadCharactersInPath(utuPluginAnimation.asset_relative_filename);
            utuPluginAnimation.asset_name = animationRelativeFilename.Split(UtuPluginPaths.slash).Last<string>();
            utuPluginAnimation.animation_file_absolute_filename = UtuPluginPaths.unityFolder_Full_Project + animationRelativeFilename;
            utuPluginAnimation.associated_skeletal_meshes_relative_filenames = UtuPluginAssets.GetSkeletalMeshesAssociatedWithAnimation(animationRelativeFilename);
            json.animations.Add(utuPluginAnimation);
            json.json_info.animations.Add(utuPluginAnimation.asset_name);

            foreach (string skeletalMesh in utuPluginAnimation.associated_skeletal_meshes_relative_filenames)
            {
                GameObject skGo = AssetDatabase.LoadAssetAtPath<GameObject>(skeletalMesh);
                SkinnedMeshRenderer skMeshRenderer = null;
                MeshRenderer meshRenderer = null;

                if (skGo != null && skGo.gameObject != null)
                {
                    skMeshRenderer = skGo.gameObject.GetComponent<SkinnedMeshRenderer>();
                    if (skMeshRenderer == null)
                    {
                        skMeshRenderer = skGo.gameObject.GetComponentInParent<SkinnedMeshRenderer>();
                        if (skMeshRenderer == null)
                        {
                            skMeshRenderer = skGo.gameObject.GetComponentInChildren<SkinnedMeshRenderer>();
                        }
                    }

                    meshRenderer = skGo.gameObject.GetComponent<MeshRenderer>();
                    if (meshRenderer == null)
                    {
                        meshRenderer = skGo.gameObject.GetComponentInParent<MeshRenderer>();
                        if (meshRenderer == null)
                        {
                            meshRenderer = skGo.gameObject.GetComponentInChildren<MeshRenderer>();
                        }
                    }
                }

                if (skMeshRenderer != null || meshRenderer != null)
                {
                    AddMeshToGlobalSkeletalMeshList(skeletalMesh, skMeshRenderer, meshRenderer);
                }
            }
        }
    }


    private string GetTexturePathAndAddItToGlobal(Texture texture)
    {
        string textureRelativeFilename = AssetDatabase.GetAssetPath(texture);
        AddTextureToGlobalMaterialList(textureRelativeFilename, texture);
        return textureRelativeFilename;
    }

    private Quaternion ColorToQuat(Color color)
    {
        return new Quaternion(color.r, color.g, color.b, color.a);
    }

    private void AddMaterialToGlobalMaterialList(string materialRelativeFilename, Material material)
    {
        if (!json.existing_materials.Contains(materialRelativeFilename) && materialRelativeFilename != "")
        {
            json.existing_materials.Add(materialRelativeFilename);
            UtuPluginMaterial utuPluginMaterial = new UtuPluginMaterial();
            utuPluginMaterial.asset_relative_filename = materialRelativeFilename;
            ErrorIfBadCharactersInPath(utuPluginMaterial.asset_relative_filename);
            utuPluginMaterial.asset_name = materialRelativeFilename.Split(UtuPluginPaths.slash).Last<string>();
            utuPluginMaterial.shader_name = material.shader.name;

            // Get all other props
            MaterialProperty[] properties = MaterialEditor.GetMaterialProperties(new UnityEngine.Object[] { material });

            foreach (MaterialProperty prop in properties)
            {
#if USING_URP || USING_HDRP || UNITY_6000_1_OR_NEWER
                    switch (prop.propertyType)
#else
                    switch (prop.type)
#endif
                    {
#if USING_URP || USING_HDRP || UNITY_6000_1_OR_NEWER
                    case UnityEngine.Rendering.ShaderPropertyType.Color:
#else
                    case MaterialProperty.PropType.Color:
#endif
                        utuPluginMaterial.material_colors_names.Add(prop.name);
                        utuPluginMaterial.material_colors.Add(UnityEngine.ColorUtility.ToHtmlStringRGBA(prop.colorValue));
                        break;


#if USING_URP || USING_HDRP || UNITY_6000_1_OR_NEWER
                    case UnityEngine.Rendering.ShaderPropertyType.Vector:
#else
                    case MaterialProperty.PropType.Vector:
#endif
                        utuPluginMaterial.material_vectors_names.Add(prop.name);
                        utuPluginMaterial.material_vectors.Add(ColorToQuat(prop.vectorValue));
                        break;


#if USING_URP || USING_HDRP || UNITY_6000_1_OR_NEWER
                    case UnityEngine.Rendering.ShaderPropertyType.Float:
#else
                    case MaterialProperty.PropType.Float:
#endif
                        utuPluginMaterial.material_floats_names.Add(prop.name);
                        utuPluginMaterial.material_floats.Add(prop.floatValue);
                        if (prop.name.Contains("_DoubleSidedEnable") || prop.displayName.Contains("_DoubleSidedEnable"))
                        {
                            utuPluginMaterial.two_sided = prop.floatValue > 0;
                        }
                        break;


#if USING_URP || USING_HDRP || UNITY_6000_1_OR_NEWER
                    case UnityEngine.Rendering.ShaderPropertyType.Range:
#else
                    case MaterialProperty.PropType.Range:
#endif
                        utuPluginMaterial.material_vector2s_names.Add(prop.name);
                        utuPluginMaterial.material_vector2s.Add(prop.rangeLimits);
                        break;


#if USING_URP || USING_HDRP || UNITY_6000_1_OR_NEWER
                    case UnityEngine.Rendering.ShaderPropertyType.Texture:
#else
                    case MaterialProperty.PropType.Texture:
#endif
                        utuPluginMaterial.material_textures_names.Add(prop.name);
                        utuPluginMaterial.material_textures.Add(GetTexturePathAndAddItToGlobal(prop.textureValue));
                        Color St = new Color(1.0f, 1.0f, 1.0f, 1.0f);
                        if (material.HasProperty(prop.name + "_ST"))
                        {
                            St = material.GetColor(prop.name + "_ST");
                        }
                        utuPluginMaterial.material_vector2s_names.Add(prop.name + "_ST_TexCoord");
                        utuPluginMaterial.material_vector2s.Add(new Vector2(St.r, St.g));
                        utuPluginMaterial.material_vector2s_names.Add(prop.name + "_ST_Panner");
                        utuPluginMaterial.material_vector2s.Add(new Vector2(St.b, St.a));
                        break;


#if UNITY_2021_1_OR_NEWER
#if USING_URP || USING_HDRP || UNITY_6000_1_OR_NEWER
                    case UnityEngine.Rendering.ShaderPropertyType.Int:
#else
                    case MaterialProperty.PropType.Int:
#endif

                        utuPluginMaterial.material_ints_names.Add(prop.name);
                        utuPluginMaterial.material_ints.Add(prop.intValue);
                        if (prop.name.Contains("_DoubleSidedEnable") || prop.name.Contains("_DoubleSidedEnable"))
                        {
                            utuPluginMaterial.two_sided = prop.intValue > 0;
                        }
                        break;
#endif
                    default:
                        break;
                }
            }



            // Try to find Main Texture
            List<string> main_texture_names = new List<string>();
            main_texture_names.Add("_MainTex");
            main_texture_names.Add("_MAIN_TEX");
            main_texture_names.Add("_BaseColorMap");
            main_texture_names.Add("_BASE_COLOR_MAP");
            main_texture_names.Add("_BaseMap");
            main_texture_names.Add("_BASE_MAP");
            main_texture_names.Add("_Texture2D");
            main_texture_names.Add("_TEXTURE_2D");
            foreach (string main_texture_name in main_texture_names)
            {
                if (material.HasProperty(main_texture_name))
                {
                    utuPluginMaterial.main_texture = GetTexturePathAndAddItToGlobal(material.GetTexture(main_texture_name));
                    utuPluginMaterial.main_texture_scale = material.GetTextureScale(main_texture_name);
                    utuPluginMaterial.main_texture_offset = material.GetTextureOffset(main_texture_name);
                    break;
                }
            }

            if (utuPluginMaterial.main_texture == null)
            {
                List<string> p_ones = new List<string>();
                p_ones.Add("Main");
                p_ones.Add("MAIN>");
                p_ones.Add("Base");
                p_ones.Add("BASE");
                p_ones.Add("Base");
                p_ones.Add("BASE");
                p_ones.Add("Texture");
                p_ones.Add("TEXTURE");
                List<string> p_twos = new List<string>();
                p_twos.Add("Tex");
                p_twos.Add("TEX");
                p_twos.Add("Color");
                p_twos.Add("COLOR");
                p_twos.Add("Map");
                p_twos.Add("MAP");
                p_twos.Add("2D");
                p_twos.Add("2D");
                for (int X = 0; X < p_ones.Count; X++)
                {
                    foreach (MaterialProperty prop in properties)
                    {
#if USING_URP || USING_HDRP || UNITY_6000_1_OR_NEWER
                        if (prop.propertyType == UnityEngine.Rendering.ShaderPropertyType.Texture)
#else
                        if (prop.type == MaterialProperty.PropType.Texture)
#endif
                        {
                            if ((prop.name.Contains(p_ones[X]) && prop.name.Contains(p_twos[X])) || (prop.displayName.Contains(p_ones[X]) && prop.displayName.Contains(p_twos[X])))
                            {
                                utuPluginMaterial.main_texture = GetTexturePathAndAddItToGlobal(material.GetTexture(prop.name));
                                utuPluginMaterial.main_texture_scale = material.GetTextureScale(prop.name);
                                utuPluginMaterial.main_texture_offset = material.GetTextureOffset(prop.name);
                            }
                        }
                        if (utuPluginMaterial.main_texture != null)
                        {
                            break;
                        }
                    }
                    if (utuPluginMaterial.main_texture != null)
                    {
                        break;
                    }
                }
            }

            if (utuPluginMaterial.main_texture == null)
            {
                UtuLog.Warning("        No MainTexture found for this material, will be using the first valid texture instead. Material: '" + materialRelativeFilename + "'");
                if (utuPluginMaterial.material_textures_names.Count > 0)
                {
                    utuPluginMaterial.main_texture = GetTexturePathAndAddItToGlobal(material.GetTexture(utuPluginMaterial.material_textures_names[0]));
                    utuPluginMaterial.main_texture_scale = material.GetTextureScale(utuPluginMaterial.material_textures_names[0]);
                    utuPluginMaterial.main_texture_offset = material.GetTextureOffset(utuPluginMaterial.material_textures_names[0]);
                }
            }


            // Try to find Main Color
            List<string> main_color_names = new List<string>();
            main_color_names.Add("_Color");
            main_color_names.Add("_COLOR");
            main_color_names.Add("_BaseColor");
            main_color_names.Add("_BASE_COLOR");
            main_color_names.Add("_Base_Color");
            main_color_names.Add("_DiffuseColor");
            main_color_names.Add("_Diffuse_Color");
            foreach (string main_color_name in main_color_names)
            {
                if (material.HasProperty(main_color_name))
                {
                    utuPluginMaterial.main_color = UnityEngine.ColorUtility.ToHtmlStringRGBA(material.GetColor(main_color_name));
                    break;
                }
            }

            if (utuPluginMaterial.main_color == null || utuPluginMaterial.main_color == "" || utuPluginMaterial.main_color == "00000000")
            {
                List<string> p_ones = new List<string>();
                p_ones.Add("Base");
                p_ones.Add("BASE");
                p_ones.Add("Main");
                p_ones.Add("MAIN");
                p_ones.Add("Diffuse");
                p_ones.Add("DIFFUSE");
                p_ones.Add("Texture");
                p_ones.Add("TEXTURE");
                List<string> p_twos = new List<string>();
                p_twos.Add("Color");
                p_twos.Add("COLOR");
                p_twos.Add("Color");
                p_twos.Add("COLOR");
                p_twos.Add("Color");
                p_twos.Add("COLOR");
                p_twos.Add("Color");
                p_twos.Add("COLOR");
                for (int X = 0; X < p_ones.Count; X++)
                {
                    foreach (MaterialProperty prop in properties)
                    {
#if USING_URP || USING_HDRP || UNITY_6000_1_OR_NEWER
                        if (prop.propertyType == UnityEngine.Rendering.ShaderPropertyType.Color)
#else
                        if (prop.type == MaterialProperty.PropType.Color)
#endif
                        {
                            if ((prop.name.Contains(p_ones[X]) && prop.name.Contains(p_twos[X])) || (prop.displayName.Contains(p_ones[X]) && prop.displayName.Contains(p_twos[X])))
                            {
                                utuPluginMaterial.main_color = UnityEngine.ColorUtility.ToHtmlStringRGBA(material.GetColor(prop.name));
                            }
                        }
                        if (!(utuPluginMaterial.main_color == null || utuPluginMaterial.main_color == "" || utuPluginMaterial.main_color == "00000000"))
                        {
                            break;
                        }
                    }
                    if (!(utuPluginMaterial.main_color == null || utuPluginMaterial.main_color == "" || utuPluginMaterial.main_color == "00000000"))
                    {
                        break;
                    }
                }
            }

            if (utuPluginMaterial.main_color == null || utuPluginMaterial.main_color == "" || utuPluginMaterial.main_color == "00000000")
            {
                UtuLog.Warning("        No MainColor found for this material, will be using the first valid color instead. Material: '" + materialRelativeFilename + "'");
                if (utuPluginMaterial.material_colors_names.Count > 0)
                {
                    utuPluginMaterial.main_color = UnityEngine.ColorUtility.ToHtmlStringRGBA(material.GetColor(utuPluginMaterial.material_colors_names[0]));
                }
            }


            int render_mode = -1;
            if (material.HasProperty("_Mode"))
            {
                // Standard
                render_mode = Mathf.RoundToInt(material.GetFloat("_Mode"));
            }
            else if (material.HasProperty("_SurfaceType"))
            {
                // HDRP
                render_mode = Mathf.RoundToInt(material.GetFloat("_SurfaceType"));
            }
            else if (material.HasProperty("_Surface"))
            {
                // URP
                render_mode = Mathf.RoundToInt(material.GetFloat("_Surface"));
                if (render_mode == 1)
                {
                    render_mode = 2;
                }
            }
            else if (material.shader.name.Contains("Cutout"))
            {
                // Weird Standard
                render_mode = 1;
            }
            else if (material.shader.name.Contains("Transparent"))
            {
                // Weird Standard
                render_mode = 2;
            }
            else if (material.HasProperty("_Opacity") || material.HasProperty("_OPACITY"))
            {
                // Shader Graphs and Custom shader
                render_mode = 1;
            }
            switch (render_mode)
            {
                case 0:
                    //UtuLog.Log("Opaque");
                    utuPluginMaterial.shader_opacity = UtuShaderOpacity.Opaque;
                    break;
                case 1:
                    //UtuLog.Log("Masked");
                    utuPluginMaterial.shader_opacity = UtuShaderOpacity.Masked;
                    break;
                case 2:
                case 3:
                    //UtuLog.Log("Translucent");
                    utuPluginMaterial.shader_opacity = UtuShaderOpacity.Translucent;
                    break;
                default:
                    UtuLog.Warning("        Unsupported material shader mode detected: '" + render_mode.ToString() + "'. Will mark this material as Opaque. Material: '" + materialRelativeFilename + "'");
                    utuPluginMaterial.shader_opacity = UtuShaderOpacity.Opaque;
                    break;
            }


            string formatted_name = utuPluginMaterial.shader_name;
            formatted_name = formatted_name.Replace(" ", "");
            formatted_name = formatted_name.Replace(".", "_");
            formatted_name = formatted_name.Replace("/", "_");
            formatted_name = formatted_name.Replace("(", "");
            formatted_name = formatted_name.Replace(")", "");

            List<string> supported_shaders = new List<string>();
            supported_shaders.Add("HDRP_Lit");
            supported_shaders.Add("HDRP_Unlit");
            supported_shaders.Add("LegacyShaders_BumpedDiffuse");
            supported_shaders.Add("LegacyShaders_BumpedSpecular");
            supported_shaders.Add("LegacyShaders_Diffuse");
            supported_shaders.Add("LegacyShaders_Specular");
            supported_shaders.Add("Mobile_BumpedDiffuse");
            supported_shaders.Add("Mobile_BumpedSpecular");
            supported_shaders.Add("Mobile_Diffuse");
            supported_shaders.Add("Mobile_UnlitSupportsLightmap");
            supported_shaders.Add("Standard");
            supported_shaders.Add("StandardSpecularsetup");
            supported_shaders.Add("UniversalRenderPipeline_BakedLit");
            supported_shaders.Add("UniversalRenderPipeline_ComplexLit");
            supported_shaders.Add("UniversalRenderPipeline_Lit");
            supported_shaders.Add("UniversalRenderPipeline_SimpleLit");
            supported_shaders.Add("UniversalRenderPipeline_Unlit");
            supported_shaders.Add("Unlit_Color");
            supported_shaders.Add("Unlit_Texture");
            supported_shaders.Add("Unlit_Transparent");
            supported_shaders.Add("Unlit_TransparentCutout");
            supported_shaders.Add("ShaderGraphs_ArnoldStandardSurface");
            supported_shaders.Add("ShaderGraphs_ArnoldStandardSurfaceTransparent");
            supported_shaders.Add("ShaderGraphs_PhysicalMaterial3DsMax");
            supported_shaders.Add("ShaderGraphs_PhysicalMaterial3DsMaxTransparent");
            supported_shaders.Add("ShaderGraphs_VFXSpriteLit");
            supported_shaders.Add("ShaderGraphs_VFXSpriteUnlit");

            if (!supported_shaders.Contains(formatted_name))
            {
                UtuLog.Warning("        Unsupported material shader detected: '" + material.shader.name + "' for material: '" + materialRelativeFilename + "'" +
                    "\n            Supported shaders are: " + string.Join(", ", supported_shaders.ToArray()) +
                    "\n            Will still import all the data in Unreal and create a custom shader for you");
            }

            json.materials.Add(utuPluginMaterial);
            json.json_info.materials.Add(utuPluginMaterial.asset_name);
        }
    }

    private void AddTextureToGlobalMaterialList(string textureRelativeFilename, Texture texture)
    {
        if (!json.existing_textures.Contains(textureRelativeFilename) && textureRelativeFilename != "")
        {
            json.existing_textures.Add(textureRelativeFilename);
            UtuPluginTexture utuPluginTexture = new UtuPluginTexture();
            utuPluginTexture.asset_relative_filename = textureRelativeFilename;
            ErrorIfBadCharactersInPath(utuPluginTexture.asset_relative_filename);
            utuPluginTexture.asset_name = textureRelativeFilename.Split(UtuPluginPaths.slash).Last<string>();

            if (!textureRelativeFilename.ToLower().EndsWith(".tif") && !textureRelativeFilename.ToLower().EndsWith(".tiff"))
            {
                utuPluginTexture.texture_file_absolute_filename = UtuPluginPaths.unityFolder_Full_Project + textureRelativeFilename;
            }
            else
            {
                TextureImporter Importer = AssetImporter.GetAtPath(textureRelativeFilename) as TextureImporter;
                if (Importer.textureType == TextureImporterType.NormalMap)
                {
                    UtuLog.Warning("        .tif Texture: '" + textureRelativeFilename + "'" +
                    "\n            Unreal does not support that texture format. This texture will be converted to a .png." +
                    "\n            Since this is a Normal Map, the result might be wrong.");
                }
                else
                {
                    UtuLog.Log("        .tif Texture: '" + textureRelativeFilename + "'" +
                    "\n            Unreal does not support that texture format. This texture will be converted to a .png.");
                }

                try
                {
                    utuPluginTexture.texture_file_absolute_filename = ConvertTifTextureToPng(textureRelativeFilename, texture);
                }
                catch
                {
                    UtuLog.Error("        .tif Texture: '" + textureRelativeFilename + "'" +
                    "\n            Failed to export a .png version if this file. This texture will not be imported in Unreal.");
                    utuPluginTexture.texture_file_absolute_filename = UtuPluginPaths.unityFolder_Full_Project + textureRelativeFilename;
                }
            }

            json.textures.Add(utuPluginTexture);
            json.json_info.textures.Add(utuPluginTexture.asset_name);
            
        }
    }

    private String ConvertTifTextureToPng(string textureRelativeFilename, Texture texture)
    {
        // Make texture readable if needed
        bool wasTextureReadable = IsTextureReadable(textureRelativeFilename);
        if (!wasTextureReadable)
        {
            SetTextureReadable(textureRelativeFilename, true);
        }

        // Convert to PNG data
        byte[] bytes = DecompressTexture(texture as Texture2D).EncodeToPNG();

        // Format texture name
        string exportedTexturePath = UtuPluginPaths.GetExportedTextureFilesFolder() + textureRelativeFilename;
        string extension = "." + exportedTexturePath.Split('.').Last<string>();
        exportedTexturePath = exportedTexturePath.Replace(extension, ".png");

        // Create folder
        Directory.CreateDirectory(exportedTexturePath.Replace(new DirectoryInfo(exportedTexturePath).Name, ""));

        // Write file
        File.WriteAllBytes(exportedTexturePath, bytes);

        // Restore readable state
        if (!wasTextureReadable)
        {
            SetTextureReadable(textureRelativeFilename, false);
        }

        return exportedTexturePath;
    }

    private bool IsTextureReadable(string textureRelativeFilename)
    {
        string AbsoluteFilePath = System.IO.Path.GetFullPath(textureRelativeFilename);
        string metadataPath = AbsoluteFilePath + ".meta";
        if (File.Exists(metadataPath))
        {
            string[] lines = File.ReadAllLines(metadataPath);
            foreach (string line in lines)
            {
                if (line.Contains("isReadable: "))
                {
                    return line.Contains("isReadable: 1");
                }
            }
        }
        return false;
    }

    private void SetTextureReadable(string textureRelativeFilename, bool isReadable)
    {
        string desiredReadable = isReadable ? "isReadable: 1" : "isReadable: 0";
        string metadataPath = System.IO.Path.GetFullPath(textureRelativeFilename) + ".meta";
        if (File.Exists(metadataPath))
        {
            List<string> newfile = new List<string>();

            string[] lines = File.ReadAllLines(metadataPath);
            foreach (string line in lines)
            {
                string newline = line;
                if (newline.Contains("isReadable: "))
                {
                    newline = newline.Replace("isReadable: 0", desiredReadable).Replace("isReadable: 1", desiredReadable);
                }
                newfile.Add(newline);
            }

            File.WriteAllLines(metadataPath, newfile.ToArray());
            AssetDatabase.Refresh();
        }
    }

    Texture2D DecompressTexture(Texture2D source)
    {
        RenderTexture renderTex = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
        Graphics.Blit(source, renderTex);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTex;
        Texture2D readableText = new Texture2D(source.width, source.height);
        readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
        readableText.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTex);
        return readableText;
    }

    // Prefab -----------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private UtuPluginActorPrefab CreateActorPrefab(GameObject go)
    {
        GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(go);
        UtuPluginActorPrefab utuPluginActorPrefab = new UtuPluginActorPrefab();
        string asset_relative_filename = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(go));
        utuPluginActorPrefab.actor_prefab_relative_filename = asset_relative_filename;
        AddPrefabToGlobalPrefabList(utuPluginActorPrefab.actor_prefab_relative_filename, go);
        // Because Unreal does not give you the possibility of stretching the vertices and deform the mesh's default shape.
        Transform transform = go.transform;
        if (!UtuConst.NearlyEquals(transform.lossyScale.x, transform.lossyScale.y, 0.1f) || !UtuConst.NearlyEquals(transform.lossyScale.x, transform.lossyScale.z, 0.1f) || !UtuConst.NearlyEquals(transform.lossyScale.y, transform.lossyScale.z, 0.1f))
        {
            // Non-Uniform Scaling Detected
            foreach (Transform t in transform)
            {
                if (t.gameObject.GetComponent<MeshFilter>())
                {
                    Vector3 rot = t.rotation.eulerAngles;
                    if (!UtuConst.NearlyEquals(rot.x, 0.0f, 1.0f) || !UtuConst.NearlyEquals(rot.y, 0.0f, 1.0f) || !UtuConst.NearlyEquals(rot.y, 0.0f, 1.0f))
                    {
                        // Rotation Detected
                        UtuLog.Warning("        Non-Uniform Scaling Detected on a Parent of at least one Rotated Static Mesh! The Static Mesh may not be imported correctly in Unreal. \n            Scaled Prefab In Scene: " + transform.name + " \n            First Rotated Child Static Mesh Found: " + t.name);
                        break;
                    }
                }
            }
        }
        return utuPluginActorPrefab;
    }

    public void AddPrefabToGlobalPrefabList(string prefabRelativeFilename, GameObject prefabRoot)
    {
        if (!json.existing_prefabs.Contains(prefabRelativeFilename))
        {
            json.existing_prefabs.Add(prefabRelativeFilename);
            // First Pass
            UtuPluginPrefabFirstPass utuPluginPrefabFirstPass = new UtuPluginPrefabFirstPass();
            utuPluginPrefabFirstPass.asset_relative_filename = prefabRelativeFilename;
            utuPluginPrefabFirstPass.asset_name = prefabRelativeFilename.Split(UtuPluginPaths.slash).Last<string>();
            json.prefabs_first_pass.Add(utuPluginPrefabFirstPass);
            json.json_info.prefabs.Add(utuPluginPrefabFirstPass.asset_name);
            utuPluginPrefabFirstPass.has_any_static_child = false;
            // Second Pass
            UtuPluginPrefabSecondPass utuPluginPrefabSecondPass = new UtuPluginPrefabSecondPass();
            utuPluginPrefabSecondPass.asset_relative_filename = prefabRelativeFilename;
            ErrorIfBadCharactersInPath(utuPluginPrefabSecondPass.asset_relative_filename);
            utuPluginPrefabSecondPass.asset_name = prefabRelativeFilename.Split(UtuPluginPaths.slash).Last<string>();
            GameObject contentsRoot = PrefabUtility.LoadPrefabContents(prefabRelativeFilename);
            // Little hack cause unity prefab system is dumb. Bref, have to reset to root transform sinc it's just a defaut value and it does not affect anything
            Vector3 rootPos = contentsRoot.transform.position;
            Quaternion rootRot = contentsRoot.transform.rotation;
            Vector3 rootScale = contentsRoot.transform.localScale;
            contentsRoot.transform.position = Vector3.zero;
            contentsRoot.transform.rotation = Quaternion.identity;
            contentsRoot.transform.localScale = Vector3.one;
            Transform[] children_transforms = contentsRoot.GetComponentsInChildren<Transform>(true);
            foreach (Transform transform in children_transforms)
            {
                UtuPluginActor component = ProcessActor(transform.gameObject, false);
                if (component != null)
                { // Else it's probably just some components inside another nested prefab
                    utuPluginPrefabFirstPass.has_any_static_child = utuPluginPrefabFirstPass.has_any_static_child || !component.actor_is_movable;
                    utuPluginPrefabSecondPass.prefab_components.Add(component);
                }
            }
            contentsRoot.transform.position = rootPos;
            contentsRoot.transform.rotation = rootRot;
            contentsRoot.transform.localScale = rootScale;
            PrefabUtility.UnloadPrefabContents(contentsRoot);
            json.prefabs_second_pass.Add(utuPluginPrefabSecondPass);
        }
    }

    private void ErrorIfBadCharactersInPath(string filename)
    {
        string error_message = "";
        foreach (char c in filename.ToCharArray())
        {
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' || c == '/'))
            {
                if (error_message == "")
                {
                    error_message = "        Filename contains invalid characters: '" + filename + "' - Invalid characters:";
                }
                error_message += " " + c;
            }
        }
        if (error_message != "")
        {
            UtuLog.Warning(error_message);
            UtuLog.Warning("            Unreal valid characters are A-Z 0-9 _ - . (no spaces)");
            UtuLog.Warning("            Invalid characters will be replaced by _ during the import.");
        }
    }
}