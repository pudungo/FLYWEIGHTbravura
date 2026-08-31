// Copyright Alex Quevillon. All Rights Reserved.

using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System;

public class UtuPluginUi : EditorWindow
{
	enum UtuPluginStatus { None, Failed, Completed, Cancelled, Exporting };
	UtuPluginStatus status = UtuPluginStatus.None;

	[MenuItem("Plugins/Utu Plugin")]
	private static void OpenWindow()
	{
		UtuPluginPaths.isConstructed = false;
		UtuPluginUi UtuPluginUi = (UtuPluginUi)GetWindow(typeof(UtuPluginUi));
		UtuPluginUi.titleContent = new GUIContent("Utu Plugin");
		UtuPluginUi.Show();
	}


	// Constructor --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
	public void ConstructIfNeeded()
	{
        if (!UtuPluginPaths.isConstructed)
		{
            UtuPluginPaths.ConstructUtuPluginPaths();
			LoadIcons();
			BuildReleaseNote();
			BuildProcessSteps();
            UtuPluginAssets.Refresh();
        }
    }

	// OnGUI --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
	private UtuPluginCurrentExport currentExport = null;
    private float timeSinceLastRefresh = 0.0f;
    enum UtuColor { White, Grey, Green, Yellow, Orange, Red, DarkGrey};

	private UtuColor WhiteOrGrey(bool isWhite)
	{
		return isWhite ? UtuColor.White : UtuColor.Grey;

    }

    private String RText(String text, int size, bool isBold, UtuColor color)
	{
		String outText = text;
		if (isBold)
		{
			outText = $"<b>{outText}</b>";
		}
        outText = $"<size={size}>{outText}</size>";
        switch (color)
        {
            case UtuColor.White:
                outText = $"<color=#cccccc>{outText}</color>";
				break;
            case UtuColor.Grey:
                outText = $"<color=#6e6d6d>{outText}</color>";
                break;
            case UtuColor.Green:
                outText = $"<color=#45ed72>{outText}</color>";
				break;
            case UtuColor.Yellow:
                outText = $"<color=#edc645>{outText}</color>";
                break;
            case UtuColor.Orange:
                outText = $"<color=#ed8b45>{outText}</color>";
                break;
            case UtuColor.Red:
                outText = $"<color=#ed5045>{outText}</color>";
                break;
            case UtuColor.DarkGrey:
                outText = $"<color=#222222>{outText}</color>";
                break;
        }
		return outText;
    }

    private void Update()
	{
		if (currentExport != null)
		{
			timeSinceLastRefresh += Time.deltaTime;
			if (timeSinceLastRefresh >= 1.0f)
			{
				timeSinceLastRefresh = 0.0f;
				Repaint();
			}
			currentExport = UtuPlugin.ContinueCurrentExport();
			if (currentExport == null)
			{
				timeSinceLastRefresh = 0.0f;
				Repaint();
				status = status == UtuPluginStatus.Exporting ? UtuPluginStatus.Completed : status;
			}
		}
		else
		{
			currentExport = UtuPlugin.ContinueCurrentExport();
		}
	}

	private GUIStyle GetMainStyle()
	{
		GUIStyle style = new GUIStyle();
		style.normal.background = Color_DarkGrey;
		style.padding = new RectOffset(10, 10, 10, 0);
		return style;
	}

	private GUIStyle GetTabStyle()
	{
		GUIStyle style = new GUIStyle();
		style.normal.background = Color_LightGrey;
		style.padding = new RectOffset(10, 10, 10, 10);
		return style;
	}

	private Rect assetScrollRect = Rect.zero;
	float toggleHeight = 0;
    private Vector2 assetsScrollPositions = Vector2.zero;
	private bool isPreScrollClipDrawn;
	private enum ScrollJump {none, scenes, prefabs, models, animations, materials, textures }
	private ScrollJump jump;
    private void OnGUI()
	{
        ConstructIfNeeded();
		EditorGUILayout.BeginVertical(GetMainStyle());
		{
			EditorGUI.BeginDisabledGroup(currentExport != null);
			{
				BuildTabs();
			}
			EditorGUI.EndDisabledGroup();
			EditorGUILayout.BeginVertical(GetTabStyle());
			{
				if (selectedTabIndex == 0)
                {
                    EditorGUI.BeginDisabledGroup(currentExport != null);
					{
                        BuildFilter();
                        BuildJumpToSection();

                        // Scrollbox
                        GUIStyle scrollBoxStyle = new GUIStyle(GUI.skin.scrollView);
                        scrollBoxStyle.padding = new RectOffset(20, 0, 0, 0);
						scrollBoxStyle.normal.background = Color_Grey;
						assetsScrollPositions = GUILayout.BeginScrollView(assetsScrollPositions, scrollBoxStyle);
                        {
                            toggleHeight = Mathf.Max((int)GUI.skin.toggle.fixedHeight, (int)GUI.skin.toggle.lineHeight) + 8 + GUI.skin.toggle.margin.vertical;// 8 is the extra padding added to each toggle
                            var viewscan = new Vector2(assetsScrollPositions.y, assetScrollRect.height);
							isPreScrollClipDrawn = false;

                            GUI.backgroundColor = new Color(0.7f, 0.7f, 0.7f, 1f);
                            viewscan = BuildScenesScrollBox(viewscan);
                            viewscan = BuildPrefabsScrollBox(viewscan);
                            viewscan = BuildMeshesScrollBox(viewscan);
                            viewscan = BuildAnimationsScrollBox(viewscan);
                            viewscan = BuildMaterialsScrollBox(viewscan);
                            viewscan = BuildTexturesScrollBox(viewscan);
                            DrawPostScrollClip(viewscan);
                            GUI.backgroundColor = new Color(1.0f, 1.0f, 1.0f, 1f);

                        }
                        GUILayout.EndScrollView();

                        if (Event.current.type == EventType.Repaint)
						{
							assetScrollRect = GUILayoutUtility.GetLastRect();
                        }

						if (UtuPluginAssets.needFbxExporterToProcessAlMeshes)
						{
							if (!FbxExportSupport.IsFbxExporterAvailable())
							{
								GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
								labelStyle.richText = true;
								labelStyle.stretchHeight = false;
								labelStyle.stretchWidth = true;
								labelStyle.alignment = TextAnchor.MiddleCenter;
								GUILayout.Label(RText("Please install FBX Exporter if you want to process Meshes coming from other sources than .fbx or .obj.", 10, true, UtuColor.Yellow), labelStyle, GUILayout.Height(22.0f));
							}
						}

                        BuildExportButton();
                    }
					EditorGUI.EndDisabledGroup();

					BuildProgressBars();
					BuildStatusText();
				}
				else if (selectedTabIndex == 1)
				{
					BuildLog();
				}
				else if (selectedTabIndex == 2)
				{
					BuildHelpAndReleaseNote();
				}
			}
			EditorGUILayout.EndVertical();
		}
		EditorGUILayout.EndVertical();
	}

    private void DrawPreScrollClip(Vector2 viewscan, float elementHeight)
    {
        if (!isPreScrollClipDrawn)
        {
            var preHeight = Mathf.Max(10,assetsScrollPositions.y- elementHeight);
            var lastColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            GUILayout.Space(preHeight);

            GUI.backgroundColor = lastColor;
            isPreScrollClipDrawn = true;
        }

    }
    private void DrawPostScrollClip(Vector2 viewscan)
    {
		var postHeight = Mathf.Max(10,- viewscan.x - viewscan.y);
        var lastColor = GUI.backgroundColor;
        GUI.backgroundColor = Color.green;

		try
        {
			GUILayout.Space(postHeight);
		}
		catch
		{ 
		}
        GUI.backgroundColor = lastColor;
    }
    private enum ClippingType{ Before, InView, After}
	private ClippingType GetClippingType(float elementHeight, Vector2 viewScan, out Vector2 newScan)
	{

		newScan = new Vector2(viewScan.x - elementHeight, viewScan.y);
		if (viewScan.x > elementHeight)
		{
			return ClippingType.Before;
		}

		if (viewScan.x + viewScan.y + elementHeight*2 < 0)
        {
            return ClippingType.After;
        }

		return ClippingType.InView;
    }


	// Icons --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
	private Texture2D Icon_LookingGlass;
	private Texture2D Icon_CopyNormal;
	private Texture2D Icon_CopyClicked;
	private Texture2D Icon_RefreshNormal;
	private Texture2D Icon_RefreshClicked;
	private Texture2D Tab_Normal;
	private Texture2D Tab_Selected;
	private Texture2D Color_DarkGrey;
	private Texture2D Color_LightGrey;
	private Texture2D Color_Grey;
	private void LoadIcons()
	{
		Icon_LookingGlass = (Texture2D)AssetDatabase.LoadAssetAtPath(UtuPluginPaths.pluginIcon_Rel_Icon_LookingGlass, typeof(Texture));
		Icon_CopyNormal = (Texture2D)AssetDatabase.LoadAssetAtPath(UtuPluginPaths.pluginIcon_Rel_Icon_CopyNormal, typeof(Texture));
		Icon_CopyClicked = (Texture2D)AssetDatabase.LoadAssetAtPath(UtuPluginPaths.pluginIcon_Rel_Icon_CopyClicked, typeof(Texture));
		Icon_RefreshNormal = (Texture2D)AssetDatabase.LoadAssetAtPath(UtuPluginPaths.pluginIcon_Rel_Icon_RefreshNormal, typeof(Texture));
		Icon_RefreshClicked = (Texture2D)AssetDatabase.LoadAssetAtPath(UtuPluginPaths.pluginIcon_Rel_Icon_RefreshClicked, typeof(Texture));
		Tab_Normal = (Texture2D)AssetDatabase.LoadAssetAtPath(UtuPluginPaths.pluginIcon_Rel_Tab_Normal, typeof(Texture));
		Tab_Selected = (Texture2D)AssetDatabase.LoadAssetAtPath(UtuPluginPaths.pluginIcon_Rel_Tab_Selected, typeof(Texture));
		Color_DarkGrey = (Texture2D)AssetDatabase.LoadAssetAtPath(UtuPluginPaths.pluginIcon_Rel_Color_DarkGrey, typeof(Texture));
		Color_LightGrey = (Texture2D)AssetDatabase.LoadAssetAtPath(UtuPluginPaths.pluginIcon_Rel_Color_LightGrey, typeof(Texture));
		Color_Grey = (Texture2D)AssetDatabase.LoadAssetAtPath(UtuPluginPaths.pluginIcon_Rel_Color_Grey, typeof(Texture));
	}

	private int selectedTabIndex = 0;
	private void BuildTabs()
	{
		EditorGUILayout.BeginHorizontal();
		{
			GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
			buttonStyle.richText = true;
            buttonStyle.active.background = Tab_Selected;
			buttonStyle.hover.background = Tab_Selected;
			buttonStyle.normal.background = selectedTabIndex == 0 ? Tab_Selected : Tab_Normal;
			if (GUILayout.Button(RText(" Export ", 12, true, UtuColor.White), buttonStyle, GUILayout.Height(30.0f), GUILayout.Width(170.0f)))
			{
				selectedTabIndex = 0;
			}
			buttonStyle.normal.background = selectedTabIndex == 1 ? Tab_Selected : Tab_Normal;
			if (GUILayout.Button(RText(" Log ", 12, true, UtuColor.White), buttonStyle, GUILayout.Height(30.0f), GUILayout.Width(170.0f)))
			{
				selectedTabIndex = 1;
				BuildFormattedLog();
			}
			buttonStyle.normal.background = selectedTabIndex == 2 ? Tab_Selected : Tab_Normal;
			if (GUILayout.Button(RText(" Information ", 12, true, UtuColor.White), buttonStyle, GUILayout.Height(30.0f), GUILayout.Width(170.0f)))
			{
				selectedTabIndex = 2;
			}
			GUILayout.FlexibleSpace();
			GUIStyle titleLabelStyle = new GUIStyle(GUI.skin.label);
			titleLabelStyle.richText = true;
			titleLabelStyle.padding = new RectOffset(5, 5, 10, 10);
			GUILayout.Label(RText(UtuPlugin.GetUtuPluginVersion(), 12, true, UtuColor.Grey), titleLabelStyle);
		}
		EditorGUILayout.EndHorizontal();
	}

	private FilterData filterData= new FilterData();
    private class FilterData
	{
		public bool isMixedToggle;
        public bool filterToggle;
        public string filterText;

		public int count;
		public bool last;

	}
    private Dictionary<ScrollJump, HashSet<string>> filterDict= new Dictionary<ScrollJump, HashSet<string>>() 
	{
		{ ScrollJump.scenes, new HashSet<string>() }, 
		{ ScrollJump.prefabs, new HashSet<string>() },
		{ ScrollJump.models, new HashSet<string>() }, 
		{ ScrollJump.animations, new HashSet<string>() },
        { ScrollJump.materials, new HashSet<string>() },
		{ ScrollJump.textures, new HashSet<string>() } 
	};

	private void BuildJumpToSection()
	{
        GUI.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        EditorGUILayout.BeginHorizontal();
        {
            GUIStyle titleLabelStyle = new GUIStyle(GUI.skin.label);
            titleLabelStyle.richText = true;
			titleLabelStyle.stretchWidth = false;
			titleLabelStyle.alignment = TextAnchor.MiddleLeft;
            GUILayout.Label(RText("Jump to: ", 12, true, UtuColor.Grey), titleLabelStyle);

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.richText = true;
            buttonStyle.alignment = TextAnchor.MiddleCenter;

            jump = ScrollJump.none;

            if (GUILayout.Button(RText("Scenes", 12, true, UtuColor.White), buttonStyle))
            {
                jump = ScrollJump.scenes;
            }
            if (GUILayout.Button(RText("Prefabs", 12, true, UtuColor.White), buttonStyle))
            {
                jump = ScrollJump.prefabs;
            }
            if (GUILayout.Button(RText("Meshes", 12, true, UtuColor.White), buttonStyle))
            {
                jump = ScrollJump.models;
            }
            if (GUILayout.Button(RText("Animations", 12, true, UtuColor.White), buttonStyle))
            {
                jump = ScrollJump.animations;
            }
            if (GUILayout.Button(RText("Materials", 12, true, UtuColor.White), buttonStyle))
            {
                jump = ScrollJump.materials;
            }
            if (GUILayout.Button(RText("Textures", 12, true, UtuColor.White), buttonStyle))
            {
                jump = ScrollJump.textures;
            }
        }
        EditorGUILayout.EndHorizontal();

        GUI.backgroundColor = new Color(1.0f, 1.0f, 1.0f, 1f);
    }

    private void BuildFilter()
	{
        EditorGUILayout.BeginHorizontal();
        {
			// Checkbox "All"
			GUIStyle toggleStyle = new GUIStyle(GUI.skin.toggle);
			toggleStyle.richText = true;
			toggleStyle.padding = new RectOffset(0, 0, 0, 10);
			toggleStyle.stretchWidth = false;

			//RText("  (Press Enter to confirm)  ", 10, false, UtuColor.Grey);

            EditorGUI.showMixedValue = filterData.isMixedToggle;
            GUI.backgroundColor = new Color(0.7f, 0.7f, 0.7f, 1f);
			var newfilterToggle = GUILayout.Toggle(!filterData.isMixedToggle && filterData.filterToggle, RText($"     Asset paths containing subtext ({filterData.count}) : ", 11, true, UtuColor.White), toggleStyle);
            GUI.backgroundColor = new Color(1.0f, 1.0f, 1.0f, 1f);
            EditorGUI.showMixedValue = false;
            if (newfilterToggle != (!filterData.isMixedToggle && filterData.filterToggle))
			{
				filterData.filterToggle = newfilterToggle;
				filterData.isMixedToggle = false;

				SetFilterGroup(filterDict[ScrollJump.scenes], scenesDict);
				SetFilterGroup(filterDict[ScrollJump.prefabs], prefabsDict);
				SetFilterGroup(filterDict[ScrollJump.models], meshesDict);
				SetFilterGroup(filterDict[ScrollJump.animations], animationsDict);
				SetFilterGroup(filterDict[ScrollJump.materials], materialsDict);
				SetFilterGroup(filterDict[ScrollJump.textures], texturesDict);
			}

			GUI.backgroundColor = new Color(0.6f, 0.6f, 0.6f, 1f);
			var newFilterText = EditorGUILayout.TextField(filterData.filterText, GUILayout.ExpandWidth(true));
            GUI.backgroundColor = new Color(1.0f, 1.0f, 1.0f, 1f);

            if (newFilterText != filterData.filterText)
			{
				filterData.filterText = newFilterText;
				RepopulateFilterGroup();
			}

		}
		EditorGUILayout.EndHorizontal();

		GUIStyle titleLabelStyle = new GUIStyle(GUI.skin.label);
		titleLabelStyle.richText = true;
		titleLabelStyle.padding = new RectOffset(0, 0, 0, 10);
		//GUILayout.Label(RText("(Filter is a simple String.Contains() on asset names. You can use it to filter things like directories or file extensions.)", 10, false, UtuColor.Grey), titleLabelStyle);
    }

    private void RepopulateFilterGroup()
    {
        filterData.isMixedToggle = false;
		filterData.last = false;
        filterData.count = 0;

        PopulateFilterGroup(filterDict[ScrollJump.scenes], scenesDict);
        PopulateFilterGroup(filterDict[ScrollJump.prefabs], prefabsDict);
        PopulateFilterGroup(filterDict[ScrollJump.models], meshesDict);
        PopulateFilterGroup(filterDict[ScrollJump.animations], animationsDict);
        PopulateFilterGroup(filterDict[ScrollJump.materials], materialsDict);
        PopulateFilterGroup(filterDict[ScrollJump.textures], texturesDict);

		if(filterData.count == 0)
        {
            filterData.filterToggle = false;
        }

		if(!filterData.isMixedToggle && filterData.count>0)
		{
			filterData.filterToggle = filterData.last;
		}
    }
	private void PopulateFilterGroup(HashSet<string> subFilter, Dictionary<string, bool> group)
    {
		subFilter.Clear();
		if(!string.IsNullOrEmpty(filterData.filterText))
		{
			foreach (var kvp in group)
			{
				if (kvp.Key.Contains(filterData.filterText))
				{
					subFilter.Add(kvp.Key);

					if(filterData.count>0 && !filterData.isMixedToggle && filterData.last != kvp.Value)
					{
                        filterData.isMixedToggle = true;
					}

                    filterData.last = kvp.Value;
					filterData.count++;

				}
			}
		}


    }
	private void SetFilterGroup(HashSet<string> subFilter, Dictionary<string, bool> group)
    {
        foreach (var path in subFilter)
        {
			group[path] = filterData.filterToggle;
        }
    }

	// Scenes Scroll Box --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
	private string ALL_SCENES = "All";
	private string PREVIOUS_ALL_SCENES = "PreviousAll";
	private Dictionary<string, bool> scenesDict = new Dictionary<string, bool>() { { "All", true }, { "PreviousAll", true } };
	private Vector2 scenesScrollPosition = Vector2.zero;
	private Vector2 BuildScenesScrollBox(Vector2 viewScan)
	{
		if(jump == ScrollJump.scenes)
		{
			assetsScrollPositions.y = assetsScrollPositions.y - viewScan.x;
        }
        var selected = scenesDict.Count(kvp => kvp.Value == true) - (scenesDict[ALL_SCENES] ? 1 : 0) - (scenesDict[PREVIOUS_ALL_SCENES] ? 1 : 0);
        List<string> scenes = UtuPluginAssets.GetScenesRelativeFilenames();
        
        // Title
        GUIStyle titleLabelStyle = new GUIStyle(GUI.skin.label);
		titleLabelStyle.richText = true;
		titleLabelStyle.padding = new RectOffset(0, 5, 10, 10);
        var titleHeight = Mathf.Max(titleLabelStyle.fixedHeight, titleLabelStyle.lineHeight) + 20 + GUI.skin.toggle.margin.vertical;
		switch(GetClippingType(titleHeight, viewScan, out var newScan))
		{
			case ClippingType.Before://do nothing
            case ClippingType.After:
                break;
			case ClippingType.InView:
                DrawPreScrollClip(viewScan, titleHeight);
				try
				{
					GUILayout.Label(RText($"Select scenes to export ({selected}/{scenes.Count})", 14, true, UtuColor.White) + RText(" (All referenced assets will be included)", 11, false, UtuColor.Grey), titleLabelStyle);
				}
				catch
				{
				}
                break;
		}

        // Checkbox "All"
        GUIStyle toggleStyle = new GUIStyle(GUI.skin.toggle);

        toggleStyle.richText = true;
		toggleStyle.padding = new RectOffset(0, 0, 4, 4);

        viewScan = newScan;
        switch (GetClippingType(toggleHeight, viewScan, out newScan))
        {
            case ClippingType.Before://do nothing
            case ClippingType.After:
                break;
            case ClippingType.InView:
                DrawPreScrollClip(viewScan, toggleHeight);
				try
                {
                    scenesDict[ALL_SCENES] = GUILayout.Toggle(scenesDict[ALL_SCENES], RText($"     {ALL_SCENES}", 11, false, WhiteOrGrey(scenesDict[ALL_SCENES])), toggleStyle);
				}
                catch
                {

                }
				break;
        }

		if (scenesDict[ALL_SCENES] != scenesDict[PREVIOUS_ALL_SCENES])
		{
			scenesDict[PREVIOUS_ALL_SCENES] = scenesDict[ALL_SCENES];
			List<string> keys = scenesDict.Keys.ToList<string>();
			foreach (string key in keys)
			{
				if (filterDict[ScrollJump.scenes].Count() == 0 || filterDict[ScrollJump.scenes].Contains(key))
				{
                    scenesDict[key] = scenesDict[ALL_SCENES];
                }
            }

            RepopulateFilterGroup();
        }
		// Checkbox Scenes
		foreach (string scene in scenes)
		{
			if (!scenesDict.ContainsKey(scene))
			{
				scenesDict.Add(scene, true);
			}
			if(!string.IsNullOrEmpty(filterData.filterText))
			{
				if (!filterDict[ScrollJump.scenes].Contains(scene))
				{
					continue;
				}
			}


            viewScan = newScan;
            switch (GetClippingType(toggleHeight, viewScan, out newScan))
            {
                case ClippingType.Before://do nothing
                case ClippingType.After:
                    break;
                case ClippingType.InView:
                    DrawPreScrollClip(viewScan, toggleHeight);
					try
                    {
                        var newValue = GUILayout.Toggle(scenesDict[scene], RText($"     {scene}", 11, false, WhiteOrGrey(scenesDict[scene])), toggleStyle);
                        if (newValue != scenesDict[scene])
						{
							scenesDict[scene] = newValue;
							RepopulateFilterGroup();
						}
					}
					catch
                    {

                    }
                    
                    break;
            }
		}

        return newScan;
    }


	private List<string> GetSelectedScenesRelativeFilenames()
	{
		List<string> ret = new List<string>();
		List<string> keys = scenesDict.Keys.ToList<string>();
		foreach (string key in keys)
		{
			if (key != ALL_SCENES && key != PREVIOUS_ALL_SCENES)
			{
				if (scenesDict[key] == true)
				{
					ret.Add(key);
				}
			}
		}
		return ret;
	}

	// Prefabs Scroll Box --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
	private string ALL_PREFABS = "All";
	private string PREVIOUS_ALL_PREFABS = "PreviousAll";
	private Dictionary<string, bool> prefabsDict = new Dictionary<string, bool>() { { "All", false }, { "PreviousAll", false } };
	private Vector2 prefabsScrollPosition = Vector2.zero;
	private Vector2 BuildPrefabsScrollBox(Vector2 viewScan)
    {
        if (jump == ScrollJump.prefabs)
        {
            assetsScrollPositions.y = assetsScrollPositions.y - viewScan.x;
        }
        var selected = prefabsDict.Count(kvp => kvp.Value == true) - (prefabsDict[ALL_PREFABS] ? 1 : 0) - (prefabsDict[PREVIOUS_ALL_PREFABS] ? 1 : 0);
        List<string> prefabs = UtuPluginAssets.GetPrefabsRelativeFilenames();
        // Title
        GUIStyle titleLabelStyle = new GUIStyle(GUI.skin.label);
		titleLabelStyle.richText = true;
		titleLabelStyle.padding = new RectOffset(0, 5, 40, 10);

        var titleHeight = Mathf.Max(titleLabelStyle.fixedHeight, titleLabelStyle.lineHeight) + 50 + GUI.skin.toggle.margin.vertical;
        switch (GetClippingType(titleHeight, viewScan, out var newScan))
        {
            case ClippingType.Before://do nothing
            case ClippingType.After:
                break;
            case ClippingType.InView:
                DrawPreScrollClip(newScan, titleHeight);
				try
                {
					GUILayout.Label(RText($"Select prefabs to export ({selected}/{prefabs.Count})", 14, true, UtuColor.White) + RText(" (All referenced assets will be included)", 11, false, UtuColor.Grey), titleLabelStyle);
				}
				catch
                {

                }
				break;
        }

		// Checkbox "All"
		GUIStyle toggleStyle = new GUIStyle(GUI.skin.toggle);
		toggleStyle.richText = true;
		toggleStyle.padding = new RectOffset(0, 0, 4, 4);
        viewScan = newScan;
        switch (GetClippingType(toggleHeight, viewScan, out newScan))
        {
            case ClippingType.Before://do nothing
            case ClippingType.After:
                break;
            case ClippingType.InView:
                DrawPreScrollClip(viewScan, toggleHeight);
				try
                {
                    prefabsDict[ALL_PREFABS] = GUILayout.Toggle(prefabsDict[ALL_PREFABS], RText($"     {ALL_PREFABS}", 11, false, WhiteOrGrey(prefabsDict[ALL_PREFABS])), toggleStyle);
				}
				catch
                {

                }
				break;
        }

		if (prefabsDict[ALL_PREFABS] != prefabsDict[PREVIOUS_ALL_PREFABS])
		{
			prefabsDict[PREVIOUS_ALL_PREFABS] = prefabsDict[ALL_PREFABS];
			List<string> keys = prefabsDict.Keys.ToList<string>();
			foreach (string key in keys)
			{
                if (filterDict[ScrollJump.prefabs].Count() == 0 || filterDict[ScrollJump.prefabs].Contains(key))
                {
					prefabsDict[key] = prefabsDict[ALL_PREFABS];
                }
            }
            RepopulateFilterGroup();
        }
		// Checkbox Prefabs
		foreach (string prefab in prefabs)
		{
			if (!prefabsDict.ContainsKey(prefab))
			{
				prefabsDict.Add(prefab, false);
            }

            if (!string.IsNullOrEmpty(filterData.filterText))
            {
                if (!filterDict[ScrollJump.prefabs].Contains(prefab))
                {
                    continue;
                }
            }

            viewScan = newScan;
            switch (GetClippingType(toggleHeight, viewScan, out newScan))
            {
                case ClippingType.Before://do nothing
                case ClippingType.After:
                    break;
                case ClippingType.InView:
                    DrawPreScrollClip(viewScan, toggleHeight);
					try
					{
                        var newValue = GUILayout.Toggle(prefabsDict[prefab], RText($"     {prefab}", 11, false, WhiteOrGrey(prefabsDict[prefab])), toggleStyle);
                        if (newValue != prefabsDict[prefab])
						{
							prefabsDict[prefab] = newValue;
							RepopulateFilterGroup();
						}
					}
					catch
					{

					}
					
                    break;
            }
        }
        return newScan;
    }


	private List<string> GetSelectedPrefabsRelativeFilenames()
	{
		List<string> ret = new List<string>();
		List<string> keys = prefabsDict.Keys.ToList<string>();
		foreach (string key in keys)
		{
			if (key != ALL_PREFABS && key != PREVIOUS_ALL_PREFABS)
			{
				if (prefabsDict[key] == true)
				{
					ret.Add(key);
				}
			}
		}
		return ret;
	}

	// Meshes Scroll Box --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
	private string ALL_MESHES = "All";
	private string PREVIOUS_ALL_MESHES = "PreviousAll";
	private Dictionary<string, bool> meshesDict = new Dictionary<string, bool>() { { "All", false }, { "PreviousAll", false } };
	private Vector2 meshesScrollPosition = Vector2.zero;
	private Vector2 BuildMeshesScrollBox(Vector2 viewScan)
    {
        if (jump == ScrollJump.models)
        {
            assetsScrollPositions.y = assetsScrollPositions.y - viewScan.x;
        }
        var selected = meshesDict.Count(kvp => kvp.Value == true) - (meshesDict[ALL_MESHES] ? 1 : 0) - (meshesDict[PREVIOUS_ALL_MESHES] ? 1 : 0);
		List<string> meshes = UtuPluginAssets.GetMeshesRelativeFilenames();
        // Title
        GUIStyle titleLabelStyle = new GUIStyle(GUI.skin.label);
		titleLabelStyle.richText = true;
		titleLabelStyle.padding = new RectOffset(0, 5, 40, 10);

        var titleHeight = Mathf.Max(titleLabelStyle.fixedHeight, titleLabelStyle.lineHeight) + 50 + GUI.skin.toggle.margin.vertical;
        switch (GetClippingType(titleHeight, viewScan, out var newScan))
        {
            case ClippingType.Before://do nothing
            case ClippingType.After:
                break;
            case ClippingType.InView:
                DrawPreScrollClip(newScan, titleHeight);
				try
                {
					GUILayout.Label(RText($"Select meshes to export ({selected}/{meshes.Count})", 14, true, UtuColor.White) + RText(" (All referenced assets will be included)", 11, false, UtuColor.Grey), titleLabelStyle);
				}
				catch
                {

                }
				break;
        }
		// Checkbox "All"
		GUIStyle toggleStyle = new GUIStyle(GUI.skin.toggle);
		toggleStyle.richText = true;
		toggleStyle.padding = new RectOffset(0, 0, 4, 4);
        viewScan = newScan;
        switch (GetClippingType(toggleHeight, viewScan, out newScan))
        {
            case ClippingType.Before://do nothing
            case ClippingType.After:
                break;
            case ClippingType.InView:
                DrawPreScrollClip(viewScan, toggleHeight);
				try
                {
                    meshesDict[ALL_MESHES] = GUILayout.Toggle(meshesDict[ALL_MESHES], RText($"     {ALL_MESHES}", 11, false, WhiteOrGrey(meshesDict[ALL_MESHES])), toggleStyle);
				}
				catch
                {

                }
				break;
        }
		if (meshesDict[ALL_MESHES] != meshesDict[PREVIOUS_ALL_MESHES])
		{
			meshesDict[PREVIOUS_ALL_MESHES] = meshesDict[ALL_MESHES];
			List<string> keys = meshesDict.Keys.ToList<string>();
			foreach (string key in keys)
			{
                if (filterDict[ScrollJump.models].Count() == 0 || filterDict[ScrollJump.models].Contains(key))
                {
					meshesDict[key] = meshesDict[ALL_MESHES];
                }
            }
            RepopulateFilterGroup();
        }
		// Checkbox Meshes
		foreach (string mesh in meshes)
		{
			if (!meshesDict.ContainsKey(mesh))
			{
				meshesDict.Add(mesh, false);
            }
            if (!string.IsNullOrEmpty(filterData.filterText))
            {
                if (!filterDict[ScrollJump.models].Contains(mesh))
                {
                    continue;
                }
            }
            viewScan = newScan;
            switch (GetClippingType(toggleHeight, viewScan, out newScan))
            {
                case ClippingType.Before://do nothing
                case ClippingType.After:
                    break;
                case ClippingType.InView:
                    DrawPreScrollClip(viewScan, toggleHeight);
					try
					{
						var newValue = GUILayout.Toggle(meshesDict[mesh], RText($"     {mesh}", 11, false, WhiteOrGrey(meshesDict[mesh])), toggleStyle);
						if (newValue != meshesDict[mesh])
						{
							meshesDict[mesh] = newValue;
							RepopulateFilterGroup();
						}
					}
					catch
					{

					}
					
                    break;
            }
        }
        return newScan;
    }


	private List<string> GetSelectedMeshesRelativeFilenames()
	{
		List<string> ret = new List<string>();
		List<string> keys = meshesDict.Keys.ToList<string>();
		foreach (string key in keys)
		{
			if (key != ALL_MESHES && key != PREVIOUS_ALL_MESHES)
			{
				if (meshesDict[key] == true)
				{
					ret.Add(key);
				}
			}
		}
		return ret;
	}

    // Animations Scroll Box --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private string ALL_ANIMATIONS = "All";
    private string PREVIOUS_ALL_ANIMATIONS = "PreviousAll";
    private Dictionary<string, bool> animationsDict = new Dictionary<string, bool>() { { "All", false }, { "PreviousAll", false } };
    private Vector2 animationsScrollPosition = Vector2.zero;
    private Vector2 BuildAnimationsScrollBox(Vector2 viewScan)
    {
        if (jump == ScrollJump.animations)
        {
            assetsScrollPositions.y = assetsScrollPositions.y - viewScan.x;
        }
        List<string> animations = UtuPluginAssets.GetAnimationsRelativeFilenames();
        var selected = animationsDict.Count(kvp => kvp.Value == true) - (animationsDict[ALL_ANIMATIONS] ? 1 : 0) - (animationsDict[PREVIOUS_ALL_ANIMATIONS] ? 1 : 0);
        // Title
        GUIStyle titleLabelStyle = new GUIStyle(GUI.skin.label);
        titleLabelStyle.richText = true;
        titleLabelStyle.padding = new RectOffset(0, 5, 40, 10);

        var titleHeight = Mathf.Max(titleLabelStyle.fixedHeight, titleLabelStyle.lineHeight) + 50 + GUI.skin.toggle.margin.vertical;
        switch (GetClippingType(titleHeight, viewScan, out var newScan))
        {
            case ClippingType.Before://do nothing
            case ClippingType.After:
                break;
            case ClippingType.InView:
                DrawPreScrollClip(newScan, titleHeight);
                try
                {
					GUILayout.Label(RText($"Select animations to export ({selected}/{animations.Count})", 14, true, UtuColor.White) + RText(" (All referenced assets will be included)", 11, false, UtuColor.Grey) + RText("\nExporting animations directly should work, but doesn't give the best results. \nInstead, I recommend placing them in a scene (or prefab) and export the scene (or prefab).", 11, false, UtuColor.Orange), titleLabelStyle);
                }
                catch
                {
                }
                break;
        }

        // Checkbox "All"
        GUIStyle toggleStyle = new GUIStyle(GUI.skin.toggle);
        toggleStyle.richText = true;
        toggleStyle.padding = new RectOffset(0, 0, 4, 4);
        viewScan = newScan;
        switch (GetClippingType(toggleHeight, viewScan, out newScan))
        {
            case ClippingType.Before://do nothing
            case ClippingType.After:
                break;
            case ClippingType.InView:
                DrawPreScrollClip(viewScan, toggleHeight);
                try
                {
                    animationsDict[ALL_ANIMATIONS] = GUILayout.Toggle(animationsDict[ALL_ANIMATIONS], RText($"     {ALL_ANIMATIONS}", 11, false, WhiteOrGrey(animationsDict[ALL_ANIMATIONS])), toggleStyle);
                }
                catch
                {

                }
                break;
        }
        if (animationsDict[ALL_ANIMATIONS] != animationsDict[PREVIOUS_ALL_ANIMATIONS])
        {
            animationsDict[PREVIOUS_ALL_ANIMATIONS] = animationsDict[ALL_ANIMATIONS];
            List<string> keys = animationsDict.Keys.ToList<string>();
            foreach (string key in keys)
            {
                if(filterDict[ScrollJump.animations].Count() == 0 || filterDict[ScrollJump.animations].Contains(key))
                {
					animationsDict[key] = animationsDict[ALL_ANIMATIONS];
                }
            }
            RepopulateFilterGroup();
        }
        // Checkbox Animations
        foreach (string animation in animations)
        {
            if (!animationsDict.ContainsKey(animation))
            {
                animationsDict.Add(animation, false);
            }
            if (!string.IsNullOrEmpty(filterData.filterText))
            {
                if (!filterDict[ScrollJump.animations].Contains(animation))
                {
                    continue;
                }
            }
            viewScan = newScan;
            switch (GetClippingType(toggleHeight, viewScan, out newScan))
            {
                case ClippingType.Before://do nothing
                case ClippingType.After:
                    break;
                case ClippingType.InView:
                    DrawPreScrollClip(viewScan, toggleHeight);
                    try
                    {
						var newValue = GUILayout.Toggle(animationsDict[animation], RText($"     {animation}", 11, false, WhiteOrGrey(animationsDict[animation])), toggleStyle);
                        if (newValue != animationsDict[animation])
                        {
                            animationsDict[animation] = newValue;
                            RepopulateFilterGroup();
                        }
                    }
                    catch
                    {

                    }
                    break;
            }
        }
        return newScan;
    }


    private List<string> GetSelectedAnimationsRelativeFilenames()
    {
        List<string> ret = new List<string>();
        List<string> keys = animationsDict.Keys.ToList<string>();
        foreach (string key in keys)
        {
            if (key != ALL_ANIMATIONS && key != PREVIOUS_ALL_ANIMATIONS)
            {
                if (animationsDict[key] == true)
                {
                    ret.Add(key);
                }
            }
        }
        return ret;
    }

    // Materials Scroll Box --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private string ALL_MATERIALS = "All";
	private string PREVIOUS_ALL_MATERIALS = "PreviousAll";
	private Dictionary<string, bool> materialsDict = new Dictionary<string, bool>() { { "All", false }, { "PreviousAll", false } };
	private Vector2 materialsScrollPosition = Vector2.zero;
	private Vector2 BuildMaterialsScrollBox(Vector2 viewScan)
    {
        if (jump == ScrollJump.materials)
        {
            assetsScrollPositions.y = assetsScrollPositions.y - viewScan.x;
        }
		List<string> materials = UtuPluginAssets.GetMaterialsRelativeFilenames();
		var selected = materialsDict.Count(kvp=> kvp.Value== true) - (materialsDict[ALL_MATERIALS]?1:0) - (materialsDict[PREVIOUS_ALL_MATERIALS] ? 1 : 0);
        // Title
        GUIStyle titleLabelStyle = new GUIStyle(GUI.skin.label);
		titleLabelStyle.richText = true;
		titleLabelStyle.padding = new RectOffset(0, 5, 40, 10);

        var titleHeight = Mathf.Max(titleLabelStyle.fixedHeight, titleLabelStyle.lineHeight) + 50 + GUI.skin.toggle.margin.vertical;
        switch (GetClippingType(titleHeight, viewScan, out var newScan))
        {
            case ClippingType.Before://do nothing
            case ClippingType.After:
                break;
            case ClippingType.InView:
                DrawPreScrollClip(newScan, titleHeight);
                try 
				{
					GUILayout.Label(RText($"Select materials to export ({selected}/{materials.Count})", 14, true, UtuColor.White) + RText(" (All referenced assets will be included)", 11, false, UtuColor.Grey), titleLabelStyle);
				}
				catch
                {

                }
				break;
        }

        // Checkbox "All"
        GUIStyle toggleStyle = new GUIStyle(GUI.skin.toggle);
		toggleStyle.richText = true;
		toggleStyle.padding = new RectOffset(0, 0, 4, 4);
        viewScan = newScan;
        switch (GetClippingType(toggleHeight, viewScan, out newScan))
        {
            case ClippingType.Before://do nothing
            case ClippingType.After:
                break;
            case ClippingType.InView:
                DrawPreScrollClip(viewScan, toggleHeight);
				try
                {
                    materialsDict[ALL_MATERIALS] = GUILayout.Toggle(materialsDict[ALL_MATERIALS], RText($"     {ALL_MATERIALS}", 11, false, WhiteOrGrey(materialsDict[ALL_MATERIALS])), toggleStyle);
				}
				catch
                {

                }
				break;
        }
		if (materialsDict[ALL_MATERIALS] != materialsDict[PREVIOUS_ALL_MATERIALS])
		{
			materialsDict[PREVIOUS_ALL_MATERIALS] = materialsDict[ALL_MATERIALS];
			List<string> keys = materialsDict.Keys.ToList<string>();
			foreach (string key in keys)
			{
                if (filterDict[ScrollJump.materials].Count() == 0 || filterDict[ScrollJump.materials].Contains(key))
                {
					materialsDict[key] = materialsDict[ALL_MATERIALS];
                }
            }
            RepopulateFilterGroup();
        }
		// Checkbox Materials
		foreach (string material in materials)
		{
			if (!materialsDict.ContainsKey(material))
			{
				materialsDict.Add(material, false);
            }
            if (!string.IsNullOrEmpty(filterData.filterText))
            {
                if (!filterDict[ScrollJump.materials].Contains(material))
                {
                    continue;
                }
            }

            viewScan = newScan;
            switch (GetClippingType(toggleHeight, viewScan, out newScan))
            {
                case ClippingType.Before://do nothing
                case ClippingType.After:
                    break;
                case ClippingType.InView:
                    DrawPreScrollClip(viewScan, toggleHeight);
					try
					{
						var newValue = GUILayout.Toggle(materialsDict[material], RText($"     {material}", 11, false, WhiteOrGrey(materialsDict[material])), toggleStyle);
                        if (newValue != materialsDict[material])
						{
							materialsDict[material] = newValue;
							RepopulateFilterGroup();
						}
					}
					catch
					{

					}
					
                    break;
            }
		}

        return newScan;
    }


	private List<string> GetSelectedMaterialsRelativeFilenames()
	{
		List<string> ret = new List<string>();
		List<string> keys = materialsDict.Keys.ToList<string>();
		foreach (string key in keys)
		{
			if (key != ALL_MATERIALS && key != PREVIOUS_ALL_MATERIALS)
			{
				if (materialsDict[key] == true)
				{
					ret.Add(key);
				}
			}
		}
		return ret;
	}


	// Textures Scroll Box --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
	private string ALL_TEXTURES = "All";
	private string PREVIOUS_ALL_TEXTURES = "PreviousAll";
	private Dictionary<string, bool> texturesDict = new Dictionary<string, bool>() { { "All", false }, { "PreviousAll", false } };
	private Vector2 texturesScrollPosition = Vector2.zero;
	private Vector2 BuildTexturesScrollBox(Vector2 viewScan)
    {
        if (jump == ScrollJump.textures)
        {
            assetsScrollPositions.y = assetsScrollPositions.y - viewScan.x;
        }
		List<string> textures = UtuPluginAssets.GetTexturesRelativeFilenames();
        var selected = texturesDict.Count(kvp => kvp.Value == true) - (texturesDict[ALL_TEXTURES] ? 1 : 0) - (texturesDict[PREVIOUS_ALL_TEXTURES] ? 1 : 0);
        // Title
        GUIStyle titleLabelStyle = new GUIStyle(GUI.skin.label);
		titleLabelStyle.richText = true;
		titleLabelStyle.padding = new RectOffset(0, 5, 40, 10);

        var titleHeight = Mathf.Max(titleLabelStyle.fixedHeight, titleLabelStyle.lineHeight) + 50 + GUI.skin.toggle.margin.vertical;
        switch (GetClippingType(titleHeight, viewScan, out var newScan))
        {
            case ClippingType.Before://do nothing
            case ClippingType.After:
                break;
            case ClippingType.InView:
                DrawPreScrollClip(newScan, titleHeight);
				try
				{
					GUILayout.Label(RText($"Select textures to export ({selected}/{textures.Count})", 14, true, UtuColor.White), titleLabelStyle);
				}
				catch
                {
                }
				break;
        }

		// Checkbox "All"
		GUIStyle toggleStyle = new GUIStyle(GUI.skin.toggle);
		toggleStyle.richText = true;
		toggleStyle.padding = new RectOffset(0, 0, 4, 4);
        viewScan = newScan;
        switch (GetClippingType(toggleHeight, viewScan, out newScan))
        {
            case ClippingType.Before://do nothing
            case ClippingType.After:
                break;
            case ClippingType.InView:
                DrawPreScrollClip(viewScan, toggleHeight);
				try
                {
                    texturesDict[ALL_TEXTURES] = GUILayout.Toggle(texturesDict[ALL_TEXTURES], RText($"     {ALL_TEXTURES}", 11, false, WhiteOrGrey(texturesDict[ALL_TEXTURES])), toggleStyle);
				}
				catch
                {

                }
				break;
        }
		if (texturesDict[ALL_TEXTURES] != texturesDict[PREVIOUS_ALL_TEXTURES])
		{
			texturesDict[PREVIOUS_ALL_TEXTURES] = texturesDict[ALL_TEXTURES];
			List<string> keys = texturesDict.Keys.ToList<string>();
			foreach (string key in keys)
			{
                if (filterDict[ScrollJump.textures].Count() == 0 || filterDict[ScrollJump.textures].Contains(key))
                {
					texturesDict[key] = texturesDict[ALL_TEXTURES];
                }
            }
            RepopulateFilterGroup();
        }
		// Checkbox Textures
		foreach (string texture in textures)
		{
			if (!texturesDict.ContainsKey(texture))
			{
				texturesDict.Add(texture, false);
            }
            if (!string.IsNullOrEmpty(filterData.filterText))
            {
                if (!filterDict[ScrollJump.textures].Contains(texture))
                {
                    continue;
                }
            }
            viewScan = newScan;
            switch (GetClippingType(toggleHeight, viewScan, out newScan))
            {
                case ClippingType.Before://do nothing
                case ClippingType.After:
                    break;
                case ClippingType.InView:
                    DrawPreScrollClip(viewScan, toggleHeight);
					try
					{
                        var newValue = GUILayout.Toggle(texturesDict[texture], RText($"     {texture}", 11, false, WhiteOrGrey(texturesDict[texture])), toggleStyle);
                        if (newValue != texturesDict[texture])
						{
							texturesDict[texture] = newValue;
							RepopulateFilterGroup();
						}
					}
					catch
                    {

                    }
                    break;
            }
        }
        return newScan;
    }


	private List<string> GetSelectedTexturesRelativeFilenames()
	{
		List<string> ret = new List<string>();
		List<string> keys = texturesDict.Keys.ToList<string>();
		foreach (string key in keys)
		{
			if (key != ALL_TEXTURES && key != PREVIOUS_ALL_TEXTURES)
			{
				if (texturesDict[key] == true)
				{
					ret.Add(key);
				}
			}
		}
		return ret;
	}


	// Export Button --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
	List<string> randomNames = new List<string>(new string[] { "vivacious", "closed", "voracious", "mammoth", "fantastic", "square", "tender", "flippant", "ragged", "abnormal", "psychedelic", "bright", "judicious", "shrill", "addicted", "unused", "wasteful", "upbeat", "bitter", "encouraging", "soggy", "flat", "open", "somber", "loving", "tiny", "cold", "aware", "gray", "fair", "difficult", "magnificent", "amusing", "better", "tangible", "tidy", "drab", "wretched", "plain", "vigorous", "daily", "permissible", "brainy", "maddening", "kind", "lean", "willing", "ablaze", "beautiful", "clammy", "obtainable", "chunky", "windy", "insidious", "military", "madly", "extra-small", "abrupt", "standing", "inconclusive", "sassy", "rustic", "neighborly", "tall", "shiny", "redundant", "outgoing", "tiresome", "disturbed", "spooky", "flowery", "tasteless", "beneficial", "present", "evasive", "sore", "eager", "flawless", "demonic", "possessive", "lacking", "godly", "extra-large", "mysterious", "delightful", "freezing", "abstracted", "witty", "scandalous", "slow", "jagged", "taboo", "omniscient", "hospitable", "jumpy", "silky", "last", "dry", "special", "habitual", "wiggly", "bouncy", "furry", "fearful", "thick", "bright", "grotesque" });
	private string exportName = "random_name";
	private bool forceReExport = false;

	private void BuildExportButton()
	{
        GUI.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        if (currentExport == null)
        {
            GUIStyle style = new GUIStyle();
			style.padding = new RectOffset(0, 0, 20, 0);
			GUILayout.BeginHorizontal(style);
			{
				GUILayout.BeginVertical();
                {
                    GUILayout.BeginHorizontal();
					{
						GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
						labelStyle.richText = true;
						labelStyle.stretchHeight = false;
						labelStyle.stretchWidth = false;
                        labelStyle.alignment = TextAnchor.LowerLeft;
						GUILayout.Label(RText("Export Location", 12, true, UtuColor.White), labelStyle, GUILayout.Height(22.0f));
						GUILayout.Label(RText("Custom path detected. You will also need to enter it in Unreal.", 10, true, UtuPluginPaths.GetPluginFolderFullExport() != UtuPluginPaths.pluginFolder_Full_Exports ? UtuColor.Orange : UtuColor.DarkGrey), labelStyle, GUILayout.Height(22.0f));
					}
                    GUILayout.EndHorizontal();


                    // Text Field
                    GUIStyle textStyle = new GUIStyle(GUI.skin.textField);
                    textStyle.alignment = TextAnchor.MiddleCenter;

                    GUI.backgroundColor = new Color(0.6f, 0.6f, 0.6f, 1f);
                    UtuPluginPaths.pluginFolder_Full_Exports_Override = GUILayout.TextField(UtuPluginPaths.GetPluginFolderFullExport().Replace("UtuPlugin/Exports", ""), textStyle, GUILayout.Height(22.0f));
                    UtuPluginPaths.pluginFolder_Full_Exports_Override = UtuPluginPaths.pluginFolder_Full_Exports_Override.Replace("\\", "/");
                    GUI.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1f);

                    GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
                    buttonStyle.richText = true;
                    buttonStyle.alignment = TextAnchor.MiddleCenter;

					string validDir = UtuPluginPaths.GetValidPluginFolderFullExport();
                    EditorGUI.BeginDisabledGroup(validDir == "");
					{
						if (GUILayout.Button(RText("Open Export Folder", 12, true, validDir == "" ? UtuColor.Grey : UtuColor.White), buttonStyle, GUILayout.Height(22.0f)))
						{
							Process.Start(validDir.Replace("\\", "/") + "/");
						}
					}
					EditorGUI.EndDisabledGroup();
                }
                GUILayout.EndVertical();


                //GUILayout.FlexibleSpace();
				

				{
					string validDir = UtuPluginPaths.GetValidPluginFolderFullExport();
                    bool isDisabled = validDir == "" || (GetSelectedScenesRelativeFilenames().Count() == 0 && GetSelectedPrefabsRelativeFilenames().Count() == 0 && GetSelectedMeshesRelativeFilenames().Count() == 0 && GetSelectedAnimationsRelativeFilenames().Count() == 0 && GetSelectedMaterialsRelativeFilenames().Count() == 0 && GetSelectedTexturesRelativeFilenames().Count() == 0);
					EditorGUI.BeginDisabledGroup(isDisabled);
					{
                        GUIStyle vBoxStyle = new GUIStyle();
                        vBoxStyle.padding = new RectOffset(10, 0, 0, 0);
                        GUILayout.BeginVertical(vBoxStyle);
						{
                            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
                            buttonStyle.richText = true;
                            buttonStyle.alignment = TextAnchor.MiddleCenter;

							bool showFbxExporterCheckbox = UtuPluginAssets.needFbxExporterToProcessAlMeshes && FbxExportSupport.IsFbxExporterAvailable();
							if (showFbxExporterCheckbox)
							{
                                GUILayout.BeginHorizontal();
								{
									GUILayout.FlexibleSpace();
                                    GUIStyle toggleStyle = new GUIStyle(GUI.skin.toggle);
									toggleStyle.richText = true;
									toggleStyle.alignment = TextAnchor.MiddleRight;
									forceReExport = GUILayout.Toggle(forceReExport, RText(" Always re-export FBX Exporter meshes", 11, false, WhiteOrGrey(!isDisabled)), toggleStyle);
									GUILayout.FlexibleSpace();
                                }
                                GUILayout.EndHorizontal();
                            }

                            GUILayout.BeginHorizontal();
							{
                                GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
                                labelStyle.richText = true;
								labelStyle.stretchWidth = false;
								labelStyle.stretchHeight = false;
                                labelStyle.alignment = TextAnchor.LowerLeft;
                                GUILayout.Label(RText("Name ", 12, true, UtuColor.White), labelStyle, GUILayout.Height(22.0f));

                                // Text Field
                                GUIStyle textStyle = new GUIStyle(GUI.skin.textField);
								textStyle.alignment = TextAnchor.MiddleCenter;

								GUI.backgroundColor = new Color(0.6f, 0.6f, 0.6f, 1f);
								exportName = GUILayout.TextField(exportName, textStyle, GUILayout.Height(22.0f));
                                if (exportName == "")
								{
									exportName = "random_name";
                                }
                                GUI.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1f);

								// Random Button
								if (GUILayout.Button(RText("R", 12, true, WhiteOrGrey(!isDisabled)), buttonStyle, GUILayout.Height(22.0f), GUILayout.Width(30.0f)))
								{
									int x = (int)UnityEngine.Random.Range(0, randomNames.Count - 1);
									exportName = randomNames[x] + "_export";
								}
							}
							GUILayout.EndHorizontal();

							// Invalid export location
							if (validDir == "")
							{
                                GUILayout.BeginHorizontal();
                                {
                                    GUILayout.FlexibleSpace();
                                    GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
                                    labelStyle.richText = true;
                                    labelStyle.stretchWidth = false;
                                    labelStyle.stretchHeight = false;
                                    labelStyle.alignment = TextAnchor.LowerCenter;
                                    GUILayout.Label(RText("Invalid Export Location ", 12, true, UtuColor.Red), labelStyle, GUILayout.Height(22.0f));
                                    GUILayout.FlexibleSpace();
                                }
                                GUILayout.EndHorizontal();
                            }
                            else
							{
                                if (GUILayout.Button(RText(" Export Assets ", 12, true, WhiteOrGrey(!isDisabled)), buttonStyle, GUILayout.Height(showFbxExporterCheckbox ? 22.0f : 44.0f)))
                                {
                                    if (exportName == "" || exportName == "random_name")
                                    {
                                        int x = (int)UnityEngine.Random.Range(0, randomNames.Count - 1);
                                        exportName = randomNames[x] + "_export";
                                    }
                                    UtuAssetsToExport utuAssetsToExport = new UtuAssetsToExport();
                                    utuAssetsToExport.scenes = GetSelectedScenesRelativeFilenames();
                                    utuAssetsToExport.prefabs = GetSelectedPrefabsRelativeFilenames();
                                    utuAssetsToExport.meshes = GetSelectedMeshesRelativeFilenames();
                                    utuAssetsToExport.animations = GetSelectedAnimationsRelativeFilenames();
                                    utuAssetsToExport.materials = GetSelectedMaterialsRelativeFilenames();
                                    utuAssetsToExport.textures = GetSelectedTexturesRelativeFilenames();
                                    UtuPlugin.Export(utuAssetsToExport, exportName, forceReExport, false/*All On One Frame*/);
                                    status = false/*All On One Frame*/ ? UtuPluginStatus.Completed : UtuPluginStatus.Exporting;
                                    BuildFormattedLog();
                                }
                            }
                        }
						GUILayout.EndVertical();
					}
					EditorGUI.EndDisabledGroup();
				}
			}
			GUILayout.EndHorizontal();
		}
		GUI.backgroundColor = new Color(1.0f, 1.0f, 1.0f, 1f);
    }

    // Open Json Button --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private void BuildOpenJsonButton()
	{
		//List<string> jsons = UtuPluginJsonUtilities.GetAvailableExportJsons();
		//EditorGUI.BeginDisabledGroup(jsons.Count <= 0); {
		//	if (GUILayout.Button("Json", GUILayout.Height(40.0f), GUILayout.Width(200.0f))) {
		//		System.Diagnostics.Process.Start(jsons[0]);
		//	}
		//}
		//EditorGUI.EndDisabledGroup();
	}

	// Progress Bars --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
	private void BuildProgressBars()
	{
		if (currentExport != null)
		{
			GUI.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1f);
			GUILayout.BeginHorizontal();
			{
				EditorGUILayout.Separator();
                GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.richText = true;
                if (GUILayout.Button(RText("Cancel Export", 12, true, UtuColor.White), buttonStyle, GUILayout.Height(40.0f), GUILayout.Width(200.0f)))
				{
					UtuPlugin.CancelExport();
					status = UtuPluginStatus.Cancelled;
				}
				EditorGUILayout.Separator();
			}
			GUI.backgroundColor = new Color(1.0f, 1.0f, 1.0f, 1f);
			GUILayout.EndHorizontal();
            EditorGUILayout.Separator();
			Rect sceneProgressRect = EditorGUILayout.BeginVertical();
			EditorGUI.ProgressBar(sceneProgressRect, currentExport.percentAssetsToProcess, currentExport.nameAssetToProcess);
			GUILayout.Space(24);
			EditorGUILayout.EndVertical();
			Rect actorProgressRect = EditorGUILayout.BeginVertical();
			if (currentExport.currentSceneProcessor != null)
			{
				EditorGUI.ProgressBar(actorProgressRect, currentExport.currentSceneProcessor.percentActorsToProcess, currentExport.currentSceneProcessor.nameActorToProcess);
			}
			else
			{
				EditorGUI.ProgressBar(actorProgressRect, 1.0f, "");
			}
			GUILayout.Space(16);
			EditorGUILayout.EndVertical();
			EditorGUILayout.Separator();
        }
    }

	private void BuildStatusText()
	{
		if (status == UtuPluginStatus.Cancelled)
		{
			GUILayout.BeginHorizontal();
			{
				EditorGUILayout.Separator();
				GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.richText = true;
                GUILayout.Label(RText("Export Cancelled", 12, true, UtuColor.Red), labelStyle);
			}
			GUILayout.EndHorizontal();
		}
		else if (status == UtuPluginStatus.Completed)
		{
			GUILayout.BeginHorizontal();
			{
				EditorGUILayout.Separator();
				UtuColor color = UtuColor.White;
                UtuLog.GetLogState(out EUtuLog logState, out int wCount, out int eCount);
				if (logState == EUtuLog.Warning)
				{
					color = UtuColor.Yellow;
				}
				else if (logState == EUtuLog.Error)
				{
					color = UtuColor.Red;
                }
                GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.richText = true;
                GUILayout.Label(RText($"       Export Completed!\n{Mathf.Max(0, wCount - 1)} Warnings  &  {Mathf.Max(0, eCount - 1)} Errors", 12, true, color), labelStyle);
			}
			GUILayout.EndHorizontal();
		}
		else if (status == UtuPluginStatus.Exporting)
		{
			GUILayout.BeginHorizontal();
			{
				EditorGUILayout.Separator();
				GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.richText = true;
				labelStyle.normal.textColor = Color.white;
                GUILayout.Label(RText("Export In Progress...", 12, true, UtuColor.White), labelStyle);
			}
			GUILayout.EndHorizontal();
		}
		EditorGUILayout.Separator();
	}


	// Log --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
	private string formattedLog = "";
	private bool showLog = true;
	private bool showLogPrev = true;
	private bool showWarning = true;
	private bool showWarningPrev = true;
	private bool showError = true;
	private bool showErrorPrev = true;
	private void BuildFormattedLog()
	{
		formattedLog = "";
		foreach (FUtuLog log in UtuLog.GetLog())
		{
			if (log.logCategory == EUtuLog.Log && showLog == true)
			{
				formattedLog += RText($"{log.message}\n", 11, false, UtuColor.White);
			}
            else if (log.logCategory == EUtuLog.Warning && showWarning == true)
			{
				formattedLog += RText($"{log.message}\n", 11, false, UtuColor.Yellow);
            }
			else if (log.logCategory == EUtuLog.Error && showError == true)
			{
				formattedLog += RText($"{log.message}\n", 11, false, UtuColor.Red);
            }
		}
	}

	private void CopyLogToClipboard()
	{
		string fullLog = "";
		foreach (FUtuLog log in UtuLog.GetLog())
		{
			if (log.logCategory == EUtuLog.Log)
			{
				fullLog += "L    " + log.message + "\n";
			}
			else if (log.logCategory == EUtuLog.Warning)
			{
				fullLog += "W    " + log.message + "\n";
			}
			else if (log.logCategory == EUtuLog.Error)
			{
				fullLog += "E    " + log.message + "\n";
			}
		}
		EditorGUIUtility.systemCopyBuffer = fullLog;
	}

	private Vector2 logcrollPosition = Vector2.zero;
	private void BuildLog()
	{
		if (showLog != showLogPrev || showWarning != showWarningPrev || showError != showErrorPrev)
		{
			showLogPrev = showLog;
			showWarningPrev = showWarning;
			showErrorPrev = showError;
			BuildFormattedLog();
		}
		GUILayout.BeginVertical();
		{
			GUIStyle hBoxStyle = new GUIStyle();
			hBoxStyle.padding = new RectOffset(0, 0, 10, 10);
			hBoxStyle.normal.background = Color_DarkGrey;
			GUILayout.BeginHorizontal(hBoxStyle);
			{
				GUILayout.BeginVertical();
				{
					GUILayout.BeginHorizontal(hBoxStyle);
					{
						GUIStyle toggleStyle = new GUIStyle(GUI.skin.toggle);
						toggleStyle.richText = true;
                        showLog = GUILayout.Toggle(showLog, RText(" Log  ", 12, true, UtuColor.White), toggleStyle);
						showWarning = GUILayout.Toggle(showWarning, RText(" Warning  ", 12, true, UtuColor.Yellow), toggleStyle);
						showError = GUILayout.Toggle(showError, RText(" Error  ", 12, true, UtuColor.Red), toggleStyle);
					}
					GUILayout.EndHorizontal();
				}
				GUILayout.EndVertical();
				GUILayout.FlexibleSpace();
				GUIStyle copyButtonStyle = new GUIStyle(GUI.skin.button);
				copyButtonStyle.active.background = Icon_CopyClicked;
				copyButtonStyle.hover.background = Icon_CopyNormal;
				copyButtonStyle.normal.background = Icon_CopyNormal;
				if (GUILayout.Button("", copyButtonStyle, GUILayout.Height(35.0f), GUILayout.Width(35.0f)))
				{
					CopyLogToClipboard();
				}
				GUIStyle refreshButtonStyle = new GUIStyle(GUI.skin.button);
				refreshButtonStyle.active.background = Icon_RefreshClicked;
				refreshButtonStyle.hover.background = Icon_RefreshNormal;
				refreshButtonStyle.normal.background = Icon_RefreshNormal;
				if (GUILayout.Button("", refreshButtonStyle, GUILayout.Height(35.0f), GUILayout.Width(35.0f)))
				{
					BuildFormattedLog();
					Repaint();
				}
			}
			GUILayout.EndHorizontal();
			// Scrollbox
			GUIStyle scrollBoxStyle = new GUIStyle(GUI.skin.scrollView);
			//scrollBoxStyle.padding = new RectOffset(10, 10, 10, 10);
			logcrollPosition = GUILayout.BeginScrollView(logcrollPosition, scrollBoxStyle);
			{
				GUIStyle vBoxStyle = new GUIStyle();
				vBoxStyle.normal.background = Color_Grey;
				GUILayout.BeginVertical(vBoxStyle);
				{
					// Release Note
					GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
					labelStyle.richText = true;
					labelStyle.padding = new RectOffset(5, 5, 10, 10);
					GUILayout.Label(formattedLog, labelStyle);
				}
				GUILayout.EndVertical();
			}
			GUILayout.EndScrollView();
		}
		GUILayout.EndVertical();
	}

	// Help and release note --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
	private string releaseNote = "";
	private void BuildReleaseNote()
	{
		string filePath = "";
		foreach (string x in AssetDatabase.GetAllAssetPaths())
		{
			if (x.StartsWith("Assets") && x.EndsWith("UtuPluginInfo.txt"))
			{
				filePath = x;
				break;
			}

		}
		releaseNote = "";
		if (File.Exists(filePath))
		{
			List<string> asList = File.ReadAllLines(filePath).ToList();
			foreach (string x in asList)
			{
				releaseNote += RText($"\n{x}", 12, false, UtuColor.White);
			}
		}
	}

	private string processSteps = "";
	private void BuildProcessSteps()
	{
		List<string> asList = new List<string>();
		asList.Add("* In Unity");
		asList.Add("    1 - Install the tool in your Unity project.");
		asList.Add("    2 - Open your Unity project.");
		asList.Add("    3 - Open the tool using the 'Plugins/Utu Plugin' drop down menu at the top.");
		asList.Add("    4 - Select the scenes you want to Export. (The tool will export evertything that is referenced par the selected scenes.)");
		asList.Add("    5 - Name your export (optional)");
		asList.Add("    6 - Press Export");
		asList.Add("");
		asList.Add("* In Unreal");
		asList.Add("    7 - Install the tool in your Unreal project in which you want to Import your scenes.");
		asList.Add("    8 - Open the destination Unreal project.");
		asList.Add("    9 - Open the tool using the 'Utu Plugin' button at the top.");
		asList.Add("   10 - Select the Export that you want to Import.");
		asList.Add("   11 - Press Import.");
		asList.Add("   12 - Go get some coffee   :) ");
		asList.Add("   13 - Once completed, you can look at the 'Log' tab to get more information about the process. (Including the warnings and errors.)");
		asList.Add("");
		asList.Add("");
		processSteps = "";
		foreach (string x in asList)
		{
            processSteps += RText($"\n{x}", 12, false, UtuColor.White);
        }
	}

	private Vector2 releaseNoteScrollPosition = Vector2.zero;
	private void BuildHelpAndReleaseNote()
	{
		// Scrollbox
		GUIStyle scrollBoxStyle = new GUIStyle(GUI.skin.scrollView);
		scrollBoxStyle.padding = new RectOffset(10, 10, 10, 10);
		releaseNoteScrollPosition = GUILayout.BeginScrollView(releaseNoteScrollPosition, scrollBoxStyle);
		GUILayout.BeginVertical();
		{
			GUIStyle vBoxStyle = new GUIStyle();
			vBoxStyle.normal.background = Color_Grey;
			GUILayout.BeginVertical(vBoxStyle);
			{
				// Title
				GUIStyle titleLabelStyle = new GUIStyle(GUI.skin.label);
				titleLabelStyle.richText = true;
				titleLabelStyle.padding = new RectOffset(5, 5, 10, 10);
				titleLabelStyle.alignment = TextAnchor.MiddleCenter;
				GUILayout.Label(RText(" Information ", 14, true, UtuColor.White), titleLabelStyle);
                // Release Note
                GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
				labelStyle.richText = true;
				labelStyle.padding = new RectOffset(5, 5, 10, 10);
				labelStyle.wordWrap = true;
                GUILayout.Label(RText(releaseNote, 12, false, UtuColor.Grey), labelStyle);
			}
			GUILayout.EndVertical();
			EditorGUILayout.Separator();
			EditorGUILayout.Separator();
			EditorGUILayout.Separator();
			EditorGUILayout.Separator();
			EditorGUILayout.Separator();
			EditorGUILayout.Separator();
			EditorGUILayout.Separator();
			EditorGUILayout.Separator();
			GUILayout.BeginVertical(vBoxStyle);
			{
				// Title
				GUIStyle titleLabelStyle = new GUIStyle(GUI.skin.label);
				titleLabelStyle.richText = true;
				titleLabelStyle.padding = new RectOffset(5, 5, 10, 10);
				titleLabelStyle.alignment = TextAnchor.MiddleCenter;
                GUILayout.Label(RText(" Process Steps ", 14, true, UtuColor.White), titleLabelStyle);
				// Contact Informations
				GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
				labelStyle.richText = true;
				labelStyle.padding = new RectOffset(5, 5, 10, 10);
				GUILayout.Label(RText(processSteps, 12, false, UtuColor.Grey), labelStyle);
			}
			GUILayout.EndVertical();
		}
		GUILayout.EndVertical();
		GUILayout.EndScrollView();
	}
}
