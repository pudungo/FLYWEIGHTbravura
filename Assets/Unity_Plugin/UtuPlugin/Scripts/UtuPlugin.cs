// Copyright Alex Quevillon. All Rights Reserved.

using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;

public class UtuAssetsToExport
{
	public List<string> scenes = new List<string>();
	public List<string> prefabs = new List<string>();
	public List<string> meshes = new List<string>();
	public List<string> animations = new List<string>();
    public List<string> materials = new List<string>();
	public List<string> textures = new List<string>();

	public int GetTotalAssetsToProcess()
	{
		return scenes.Count + prefabs.Count + meshes.Count + animations.Count + materials.Count + textures.Count;
	}

	public bool HasAnyAssetsLeftToProcess()
    {
		return GetTotalAssetsToProcess() > 0;
	}
}

public class UtuPluginCurrentExport
{
	// Global
	public UtuPluginJson json;
	// Delayed Specific
	private UtuAssetsToExport assetsToProcess;
	public UtuPluginSceneProcessor currentSceneProcessor = null;
	public int countAssetsToProcess = 1;
	public int amountAssetsToProcess = 0;
	public float percentAssetsToProcess = 0.0f;
	public string nameAssetToProcess = "";
	private bool isProcessingScene = false;
	private int frameCountForThisAsset = 0;
    static public bool forceReExportOfFbxExporterMeshes = false;

    public void Export(UtuAssetsToExport utuAssetsToExport, string exportName, bool forceReExport, bool executeFullExportOnSameFrame)
	{
		BeginExport(utuAssetsToExport, exportName, forceReExport);
		if (executeFullExportOnSameFrame)
		{
			while (ContinueExport(executeFullExportOnSameFrame) != true)
			{
				// ContinueExport 
			}
		}
	}

	public bool ContinueExport(bool executeFullExportOnSameFrame)
	{
		if (currentSceneProcessor == null && !assetsToProcess.HasAnyAssetsLeftToProcess())
		{
			UtuLog.Error("UtuPluginCurrentExport::ContinueImport() Was called even though the list is already empty. This should never happen!");
			return true;
		}
		if (currentSceneProcessor == null && assetsToProcess.HasAnyAssetsLeftToProcess())
		{
			currentSceneProcessor = new UtuPluginSceneProcessor();
			isProcessingScene = false;
			if (assetsToProcess.scenes.Count > 0)
			{
				isProcessingScene = true;
				nameAssetToProcess = assetsToProcess.scenes[0];
				assetsToProcess.scenes.RemoveAt(0);
				currentSceneProcessor.ExportScene(json, nameAssetToProcess, executeFullExportOnSameFrame);
			}
			else if (assetsToProcess.prefabs.Count > 0)
			{
                nameAssetToProcess = assetsToProcess.prefabs[0];
                assetsToProcess.prefabs.RemoveAt(0);
                currentSceneProcessor.ExportAsset(json, nameAssetToProcess, true, false, false, false, false);
            }
			else if (assetsToProcess.meshes.Count > 0)
			{
				nameAssetToProcess = assetsToProcess.meshes[0];
				assetsToProcess.meshes.RemoveAt(0);
				currentSceneProcessor.ExportAsset(json, nameAssetToProcess, false, true, false, false, false);
			}
            else if (assetsToProcess.animations.Count > 0)
            {
                nameAssetToProcess = assetsToProcess.animations[0];
                assetsToProcess.animations.RemoveAt(0);
                currentSceneProcessor.ExportAsset(json, nameAssetToProcess, false, false, true, false, false);
            }
            else if (assetsToProcess.materials.Count > 0)
			{
				nameAssetToProcess = assetsToProcess.materials[0];
				assetsToProcess.materials.RemoveAt(0);
				currentSceneProcessor.ExportAsset(json, nameAssetToProcess, false, false, false, true, false);
			}
			else if (assetsToProcess.textures.Count > 0)
			{
				nameAssetToProcess = assetsToProcess.textures[0];
				assetsToProcess.textures.RemoveAt(0);
				currentSceneProcessor.ExportAsset(json, nameAssetToProcess, false, false, false, false, true);
			}
		}
		if (isProcessingScene)
		{
			if (executeFullExportOnSameFrame)
			{
				currentSceneProcessor = null;
				countAssetsToProcess = amountAssetsToProcess;
				percentAssetsToProcess = (float)countAssetsToProcess / (float)amountAssetsToProcess;
			}
			else
			{
				if (currentSceneProcessor.ContinueExportScene())
				{
					currentSceneProcessor = null;
					countAssetsToProcess++;
					percentAssetsToProcess = (float)countAssetsToProcess / (float)amountAssetsToProcess;
				}
			}
		}
		else
        {
			if (frameCountForThisAsset > 0)
			{
				currentSceneProcessor = null;
				countAssetsToProcess++;
				percentAssetsToProcess = (float)countAssetsToProcess / (float)amountAssetsToProcess;
				frameCountForThisAsset = 0;
			}
			else
            {
				frameCountForThisAsset++;
			}
		}
		if (currentSceneProcessor == null && !assetsToProcess.HasAnyAssetsLeftToProcess())
		{
			CompleteExport();
			return true;
		}
		return false;
	}

	public void BeginExport(UtuAssetsToExport utuAssetsToExport, string exportName, bool forceReExport)
	{
		forceReExportOfFbxExporterMeshes = forceReExport;
        assetsToProcess = utuAssetsToExport;
		countAssetsToProcess = 1;
		amountAssetsToProcess = assetsToProcess.GetTotalAssetsToProcess();
		percentAssetsToProcess = (float)countAssetsToProcess / (float)amountAssetsToProcess;
		json = new UtuPluginJson();
		json.json_info = new UtuPluginJsonInfo();
		json.json_info.export_datetime = System.DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
		json.json_info.export_name = exportName;
        UtuPluginPaths.current_timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
		json.json_info.export_timestamp = UtuPluginPaths.current_timestamp;
		CreateExportDirectory();
		UtuLog.ClearLog();
		UtuLog.InitializeNewLog();
		UtuLog.Separator();
		UtuLog.Log("Beginning New Export...");
		UtuLog.Log("    Time: " + System.DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
		UtuLog.Log("    Export Name: " + json.json_info.export_name);
		UtuLog.Log("    Scene      Count to Process: " + assetsToProcess.scenes.Count.ToString());
		UtuLog.Log("    Prefab     Count to Process: " + assetsToProcess.prefabs.Count.ToString());
		UtuLog.Log("    Mesh       Count to Process: " + assetsToProcess.meshes.Count.ToString());
		UtuLog.Log("    Animation  Count to Process: " + assetsToProcess.animations.Count.ToString());
		UtuLog.Log("    Material   Count to Process: " + assetsToProcess.materials.Count.ToString());
		UtuLog.Log("    Texture    Count to Process: " + assetsToProcess.textures.Count.ToString());
	}

	private void CompleteExport()
	{
		UtuLog.Separator();
		UtuLog.Log("Completing Export...");
		UtuLog.Log("    Time: " + System.DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
		UtuPluginJsonUtilities.DumpExportJsonToFile(json);
		UtuLog.Separator();
		UtuLog.Log("Generated Data:");
		UtuLog.Log("    Scenes (" + json.scenes.Count.ToString() + "):");
		foreach (UtuPluginScene x in json.scenes)
		{
			UtuLog.Log("        " + x.asset_relative_filename);
		}
		UtuLog.Empty();
		UtuLog.Log("    Prefabs (" + json.prefabs_first_pass.Count.ToString() + "):");
		foreach (UtuPluginPrefabFirstPass x in json.prefabs_first_pass)
		{
			UtuLog.Log("        " + x.asset_relative_filename);
		}
		UtuLog.Empty();
		UtuLog.Log("    Meshes (" + json.meshes.Count.ToString() + "):");
		foreach (UtuPluginMesh x in json.meshes)
		{
			UtuLog.Log("        " + x.asset_relative_filename);
			if (x.mesh_materials_relative_filenames.Count > 0)
			{
				UtuLog.Log("            Linked Materials:");
				foreach (string y in x.mesh_materials_relative_filenames)
				{
					UtuLog.Log("                " + y);
				}
			}
			UtuLog.SemiSeparator("        ");
		}
		UtuLog.Empty();
        UtuLog.Log("    Animations (" + json.animations.Count.ToString() + "):");
        foreach (UtuPluginAnimation x in json.animations)
        {
            UtuLog.Log("        " + x.asset_relative_filename);
        }
        UtuLog.Empty();
        UtuLog.Log("    Materials (" + json.materials.Count.ToString() + "):");
		foreach (UtuPluginMaterial x in json.materials)
		{
			UtuLog.Log("        " + x.asset_relative_filename);
			List<string> validTextures = new List<string>();
			foreach (string y in x.material_textures)
			{
				if (y != "")
				{
					validTextures.Add(y);
				}
			}
			if (validTextures.Count > 0)
			{
				UtuLog.Log("            Linked Textures:");
				foreach (string y in validTextures)
				{
					UtuLog.Log("                " + y);
				}
			}
			UtuLog.SemiSeparator("        ");
		}
		UtuLog.Empty();
		UtuLog.Log("    Textures (" + json.textures.Count.ToString() + "):");
		foreach (UtuPluginTexture x in json.textures)
		{
			UtuLog.Log("        " + x.asset_relative_filename);
		}
		UtuLog.Separator();
		UtuLog.Log("Export Completed!");
		UtuLog.Log("    Time: " + System.DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
		UtuLog.GetLogState(out EUtuLog logState, out int warningCount, out int errorCount);
		if (warningCount == 0)
		{
			UtuLog.Log("Warning Count: " + warningCount.ToString());
		}
		else
		{
			UtuLog.Warning("Warning Count: " + warningCount.ToString());
		}
		if (errorCount == 0)
		{
			UtuLog.Log("Error Count: " + errorCount.ToString());
		}
		else
		{
			UtuLog.Error("Error Count: " + errorCount.ToString());
		}
		UtuLog.Separator();


		// Copy unity log into export folder
		if (File.Exists(UtuPluginPaths.unityFolder_Full_EditorLogFile))
		{
            try
            {
				File.Copy(UtuPluginPaths.unityFolder_Full_EditorLogFile, UtuPluginPaths.GetCurrentExportFolderPath() + "UnityEditorLog_" + UtuPluginPaths.current_timestamp + ".log");
            }
            catch
            {

            }
        }

		// Reopen scene to cancel changes that happened during the export
		try 
		{ 
			EditorSceneManager.OpenScene(EditorSceneManager.GetActiveScene().path, OpenSceneMode.Single);
        }
		catch
		{

		}
    }

	private void CreateExportDirectory()
	{
		string path = UtuPluginPaths.GetCurrentExportFolderPath();
		if (!Directory.Exists(path))
		{
			Directory.CreateDirectory(path);
		}
	}
}


public class UtuPlugin
{
	private static UtuPluginCurrentExport currentExportJob = null;

	private static string utuPluginVersion = "missing";
	public static string GetUtuPluginVersion()
	{
		if (utuPluginVersion == "missing")
        {
			string filePath = "";
			foreach (string x in AssetDatabase.GetAllAssetPaths())
			{
				if (x.StartsWith("Assets") && x.EndsWith("UtuPluginVersion.txt"))
				{
					filePath = x;
					break;
				}
			}
			if (File.Exists(filePath))
            {
				utuPluginVersion = File.ReadAllText(filePath);
			}
		}

		return utuPluginVersion;
	}

	public static void Export(UtuAssetsToExport utuAssetsToExport, string exportName, bool forceReExport, bool executeFullExportOnSameFrame)
	{
		currentExportJob = new UtuPluginCurrentExport();
		currentExportJob.Export(utuAssetsToExport, exportName, forceReExport, executeFullExportOnSameFrame);
		if (executeFullExportOnSameFrame)
		{
			currentExportJob = null;
		}
	}

	public static UtuPluginCurrentExport ContinueCurrentExport()
	{
		if (currentExportJob != null)
		{
			if (currentExportJob.ContinueExport(false))
			{
				currentExportJob = null;
			}
		}
		return currentExportJob;
	}

	public static void CancelExport()
	{
		currentExportJob = null;
	}
}
