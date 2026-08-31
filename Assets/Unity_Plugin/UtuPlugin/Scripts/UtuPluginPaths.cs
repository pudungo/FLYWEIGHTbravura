// Copyright Alex Quevillon. All Rights Reserved.

using System.IO;
using UnityEditor;
using UnityEngine;

[System.Serializable]
public class UtuPluginConfig {
	public string unrealFile_Full_Engine;
	public string unrealFile_Full_UProject;
}

class UtuPluginPaths
{
	// Utilities
	public static bool isConstructed = false;
	// Windows
	public static readonly char slash = '/';
	public static readonly char backslash = '\\';
	// Unity
	public static string unityFolder_Full_Project;
    public static readonly string default_unityFolder_Full_EditorLogFile = "{%LocalAppData%}/Unity/Editor/Editor.log";
    public static string unityFolder_Full_EditorLogFile;
    // plugin
    public static string pluginFolder_Rel_UtuPlugin;
	public static string pluginShader_Rel_EmptyShader;
	public static string pluginIcon_Rel_Icon_LookingGlass;
    public static string pluginIcon_Rel_Icon_CopyNormal;
	public static string pluginIcon_Rel_Icon_CopyClicked;
	public static string pluginIcon_Rel_Icon_RefreshNormal;
	public static string pluginIcon_Rel_Icon_RefreshClicked;
	public static string pluginIcon_Rel_Tab_Selected;
	public static string pluginIcon_Rel_Tab_Normal;
	public static string pluginIcon_Rel_Color_DarkGrey;
	public static string pluginIcon_Rel_Color_LightGrey;
	public static string pluginIcon_Rel_Color_Grey;
	public static readonly string default_pluginFolder_Full_Exports = "{%AppData%}/AlexQuevillon/UtuPlugin/Exports";
	public static string pluginFolder_Full_Exports;
    public static string pluginFolder_Full_Exports_Override = "";

	public static string current_timestamp = "";


    public static string GetValidPluginFolderFullExport()
	{
		string path = GetPluginFolderFullExport();
        while (path.EndsWith("/"))
        {
            path = path.Substring(0, path.Length - 1);
        }

        if (Directory.Exists(path) && path.Split('/').Length > 1)
        {
            return path;
        }

		path = path.Replace("/UtuPlugin/Exports", "");
        if (Directory.Exists(path) && path.Split('/').Length > 1)
        {
            return path;
        }

        int itt = 0;
		while(path.Contains("/") && path.Split('/').Length > 1 && itt < 50)
		{
			itt++;
            int idx = path.LastIndexOf('/');
			path = path.Substring(0, idx);

            if (Directory.Exists(path))
            {
                return path;
            }
		}
		return "";
    }

    public static string GetPluginFolderFullExport()
	{
		if (pluginFolder_Full_Exports_Override == "")
		{
			return pluginFolder_Full_Exports;
        }
		else
		{
			if (pluginFolder_Full_Exports_Override.EndsWith("/UtuPlugin/Exports"))
			{
				return pluginFolder_Full_Exports_Override;
			}
			else
			{
				if (pluginFolder_Full_Exports_Override.EndsWith("/"))
				{
                    return pluginFolder_Full_Exports_Override + "UtuPlugin/Exports";
                }
                else
				{
                    return pluginFolder_Full_Exports_Override + "/UtuPlugin/Exports";
                }
            }
        }
    }


    public static string GetExportedFbxFilesFolder()
    {
        return UtuPluginPaths.GetPluginFolderFullExport().Replace("UtuPlugin/Exports", "UtuPlugin/ExportedFbxFiles/");
    }


    public static string GetExportedTextureFilesFolder()
    {
        return UtuPluginPaths.GetPluginFolderFullExport().Replace("UtuPlugin/Exports", "UtuPlugin/ExportedTextureFiles/");
    }


    public static string GetCurrentExportFolderPath()
	{
		return GetPluginFolderFullExport() + UtuPluginPaths.slash + current_timestamp + UtuPluginPaths.slash;
    }


	// Constructor
	public static void ConstructUtuPluginPaths()
	{
		// Utilities
		isConstructed = true;
		// Unity
		unityFolder_Full_Project = Application.dataPath.Remove(Application.dataPath.Length - 6);
		// Plugin
		pluginFolder_Rel_UtuPlugin = GetPluginRelativeFolder();
        pluginShader_Rel_EmptyShader = pluginFolder_Rel_UtuPlugin + slash + "Scripts" + slash + "UtuEmptyShader.shader";
        pluginIcon_Rel_Icon_LookingGlass = pluginFolder_Rel_UtuPlugin + slash + "Images" + slash + "UTU_Icon_LookingGlass.png";
		pluginIcon_Rel_Icon_CopyNormal = pluginFolder_Rel_UtuPlugin + slash + "Images" + slash + "UTU_Icon_CopyNormal.png";
		pluginIcon_Rel_Icon_CopyClicked = pluginFolder_Rel_UtuPlugin + slash + "Images" + slash + "UTU_Icon_CopyClicked.png";
		pluginIcon_Rel_Icon_RefreshNormal = pluginFolder_Rel_UtuPlugin + slash + "Images" + slash + "UTU_Icon_RefreshNormal.png";
		pluginIcon_Rel_Icon_RefreshClicked = pluginFolder_Rel_UtuPlugin + slash + "Images" + slash + "UTU_Icon_RefreshClicked.png";
		pluginIcon_Rel_Tab_Normal = pluginFolder_Rel_UtuPlugin + slash + "Images" + slash + "UTU_Tab_Normal.png";
		pluginIcon_Rel_Tab_Selected = pluginFolder_Rel_UtuPlugin + slash + "Images" + slash + "UTU_Tab_Selected.png";
		pluginIcon_Rel_Color_DarkGrey = pluginFolder_Rel_UtuPlugin + slash + "Images" + slash + "UTU_Color_DarkGrey.png";
		pluginIcon_Rel_Color_LightGrey = pluginFolder_Rel_UtuPlugin + slash + "Images" + slash + "UTU_Color_LightGrey.png";
		pluginIcon_Rel_Color_Grey = pluginFolder_Rel_UtuPlugin + slash + "Images" + slash + "UTU_Color_Grey.png";
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        unityFolder_Full_EditorLogFile = "~/Library/Logs/Unity/Editor.log";
		pluginFile_Full_Config = default_pluginFile_Full_Config.Replace("{%AppData%}", System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments) + "/Documents");
		pluginFolder_Full_Exports = default_pluginFolder_Full_Exports.Replace("{%AppData%}", System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments).Replace("\\", "/") + "/Documents");
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
        unityFolder_Full_EditorLogFile = "~/.config/unity3d/Editor.log";
        string linuxHomePath = "/home/" + System.Environment.UserName;
        pluginFile_Full_Config = default_pluginFile_Full_Config.Replace("{%AppData%}", linuxHomePath).Replace("\\", "/");
        pluginFolder_Full_Exports = default_pluginFolder_Full_Exports.Replace("{%AppData%}", linuxHomePath).Replace("\\", "/");
#else
        unityFolder_Full_EditorLogFile = default_unityFolder_Full_EditorLogFile.Replace("{%LocalAppData%}", System.Environment.GetEnvironmentVariable("LocalAppData")).Replace("\\", "/");
        pluginFolder_Full_Exports = default_pluginFolder_Full_Exports.Replace("{%AppData%}", System.Environment.GetEnvironmentVariable("AppData")).Replace("\\", "/");
#endif
	}


	private static string GetPluginRelativeFolder()
	{
		foreach (string x in AssetDatabase.GetAllAssetPaths())
		{
			if (x.EndsWith("UtuPluginPaths.cs"))
			{
				return x.Replace("/Scripts/UtuPluginPaths.cs", "");
			}
		}
		return "";
	}


	public static string FormatForWindows(string path, bool addQuotes = false)
	{
		if (addQuotes)
		{
			return "\"" + path.Replace(slash, backslash) + "\"";
		}
		else
		{
			return path.Replace(slash, backslash);
		}
	}

	public static string MakeAbsolute(string path)
	{
		return unityFolder_Full_Project + path;
	}
}
