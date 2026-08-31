// Copyright Alex Quevillon. All Rights Reserved.

using System;
using System.IO;
using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public enum UtuActorType { Empty, StaticMesh, SkeletalMesh, PointLight, DirectionalLight, SpotLight, Camera, Prefab };

[System.Serializable]
public enum UtuShaderOpacity { Opaque, Masked, Translucent };

[System.Serializable]
public class UtuPluginSubmesh {
	public string submesh_name;
	public Vector3 submesh_relative_location;
	public Quaternion submesh_relative_rotation;
	public Vector3 submesh_relative_scale;
    public Vector3 submesh_world_location;
    public Quaternion submesh_world_rotation;
    public Vector3 submesh_world_scale;
    public List<string> submesh_materials_relative_filenames = new List<string>();
}

			[System.Serializable]
			public class UtuPluginActorCamera {
				public Quaternion camera_viewport_rect; // XYWH
				public float camera_near_clip_plane;
				public float camera_far_clip_plane;
				public float camera_aspect_ratio;
				public bool camera_is_perspective;
				public float camera_ortho_size;
				public float camera_persp_field_of_view;
				public bool camera_is_physical;
				public float camera_phys_focal_length;
				public Vector2 camera_phys_sensor_size;
			}

			[System.Serializable]
			public class UtuPluginActorLight {
				public string light_color; // Hex
				public float light_intensity;
				public float light_range;
				public float light_spot_angle;
				public bool light_is_casting_shadows;
				//public float light_bounce_intensity;
				//public float light_shadow_strength;
			}

			[System.Serializable]
			public class UtuPluginActorMesh {
				public string actor_mesh_relative_filename; // Warning: if == "", means that the mesh is invalid / empty
				public string actor_mesh_relative_filename_if_separated; // Warning: if == "", means that the mesh is invalid / empty
				public List<string> actor_mesh_materials_relative_filenames = new List<string>();
				public List<string> actor_mesh_animations_relative_filenames = new List<string>();
			}

				[System.Serializable]
				public class UtuPluginActorPrefabComponentOverride {
					public string component_display_name;
					public string mesh_relative_filename;
					public string mesh_relative_filename_if_separated;
					public List<string> animation_relative_filenames = new List<string>();
					public List<string> material_relative_filenames = new List<string>();
}


			[System.Serializable]
			public class UtuPluginActorPrefab {
				public string actor_prefab_relative_filename; // Warning: if == "", means that the mesh is invalid / empty
				public List<UtuPluginActorPrefabComponentOverride> actor_prefab_component_overrides = new List<UtuPluginActorPrefabComponentOverride>();
			}

		[System.Serializable]
		public class UtuPluginActor {
			public int actor_id;
			public int actor_parent_id;
			public string actor_display_name;
			public string actor_tag;
			public bool actor_is_visible;
			public Vector3 actor_world_location;
			public Quaternion actor_world_rotation;
			public Vector3 actor_world_scale;
			public Vector3 actor_relative_location;
			public Quaternion actor_relative_rotation;
			public Vector3 actor_relative_scale;
			public bool actor_is_movable;
			public List<UtuActorType> actor_types = new List<UtuActorType>();
			public UtuPluginActorMesh actor_mesh;
			public UtuPluginActorPrefab actor_prefab;
			public UtuPluginActorLight actor_light;
			public UtuPluginActorCamera actor_camera;
		}

	[System.Serializable]
	public class UtuPluginAsset {
		public string asset_name;
		public string asset_relative_filename;
	}

	[System.Serializable]
	public class UtuPluginScene : UtuPluginAsset {
		public List<UtuPluginActor> scene_actors = new List<UtuPluginActor>();
	}

	[System.Serializable]
	public class UtuPluginMesh : UtuPluginAsset {
		public string mesh_file_absolute_filename;
		public Vector3 mesh_import_position_offset;
		public Quaternion mesh_import_rotation_offset;
		public Vector3 mesh_import_scale_offset;
		public float mesh_import_scale_factor;
        public List<string> mesh_materials_relative_filenames = new List<string>();
        public bool is_skeletal_mesh;
        public bool use_file_scale;
        public List<UtuPluginSubmesh> submeshes = new List<UtuPluginSubmesh>();
	}

	[System.Serializable]
	public class UtuPluginMaterial : UtuPluginAsset {
		public string shader_name;
		public UtuShaderOpacity shader_opacity;
		public bool two_sided;
		public string main_texture;
		public Vector2 main_texture_scale;
		public Vector2 main_texture_offset;
		public string main_color;
		public List<string> material_floats_names = new List<string>();
        public List<float> material_floats = new List<float>();
		public List<string> material_ints_names = new List<string>();
		public List<int> material_ints = new List<int>();
		public List<string> material_textures_names = new List<string>();
		public List<string> material_textures = new List<string>();
		public List<string> material_colors_names = new List<string>();
		public List<string> material_colors = new List<string>();
		public List<string> material_vectors_names = new List<string>();
		public List<Quaternion> material_vectors = new List<Quaternion>();
		public List<string> material_vector2s_names = new List<string>();
		public List<Vector2> material_vector2s = new List<Vector2>();
	}

	[System.Serializable]
	public class UtuPluginAnimation : UtuPluginAsset {
		public string animation_file_absolute_filename;
		public List<string> associated_skeletal_meshes_relative_filenames = new List<string>();
}

	[System.Serializable]
	public class UtuPluginTexture : UtuPluginAsset {
		public string texture_file_absolute_filename;
	}

	[System.Serializable]
	public class UtuPluginPrefabSecondPass : UtuPluginAsset {
		public List<UtuPluginActor> prefab_components = new List<UtuPluginActor>();
	}
	[System.Serializable]
	public class UtuPluginPrefabFirstPass : UtuPluginAsset {
		public bool has_any_static_child;
	}

[System.Serializable]
public class UtuPluginJsonInfo {
	public string export_name;
	public string export_datetime;
	public string export_timestamp;
	public string json_file_fullname;
	public string utu_plugin_version;
	public List<string> scenes = new List<string>();
	public List<string> meshes = new List<string>();
	public List<string> animations = new List<string>();
	public List<string> materials = new List<string>();
    public List<string> textures = new List<string>();
	public List<string> prefabs = new List<string>();
}

[System.Serializable]
public class UtuPluginJson {
	public UtuPluginJsonInfo json_info;
	public List<UtuPluginScene> scenes = new List<UtuPluginScene>();
	public List<UtuPluginMesh> meshes = new List<UtuPluginMesh>();
	public List<UtuPluginAnimation> animations = new List<UtuPluginAnimation>();
	public List<UtuPluginMaterial> materials = new List<UtuPluginMaterial>();
	public List<UtuPluginTexture> textures = new List<UtuPluginTexture>();
	public List<UtuPluginPrefabFirstPass> prefabs_first_pass = new List<UtuPluginPrefabFirstPass>();
	public List<UtuPluginPrefabSecondPass> prefabs_second_pass = new List<UtuPluginPrefabSecondPass>();
    [System.NonSerialized] public List<string> existing_meshes = new List<string>();
    [System.NonSerialized] public List<string> existing_skel_meshes = new List<string>();
    [System.NonSerialized] public List<string> existing_animations = new List<string>();
	[System.NonSerialized] public List<string> existing_materials = new List<string>();
    [System.NonSerialized] public List<string> existing_textures = new List<string>();
	[System.NonSerialized] public List<string> existing_prefabs = new List<string>();
}



public class UtuPluginJsonUtilities
{

	public static void DumpExportJsonToFile(UtuPluginJson json)
	{
		UtuLog.Log("        Exporting Generated Data to file for future Import in Unreal...");
		string path = UtuPluginPaths.GetCurrentExportFolderPath() + "UtuPlugin.json";
		json.json_info.json_file_fullname = path;
		json.json_info.utu_plugin_version = UtuPlugin.GetUtuPluginVersion();
		File.WriteAllText(path, JsonUtility.ToJson(json).Replace("Infinity,", "999999999,").Replace("NaN,", "-999999999,"));
		string infoPath = UtuPluginPaths.GetCurrentExportFolderPath() + "UtuPluginInfo.json";
		File.WriteAllText(infoPath, JsonUtility.ToJson(json.json_info));
		UtuLog.Log("        Generated Data Exported!");
		UtuLog.Log("            Data: \"" + path + "\"");
		UtuLog.Log("            Info: \"" + infoPath + "\"");
		UtuLog.Log("            Log:  \"" + UtuLog.GetUnityExportLogFilePath() + "\"");
	}

	public static List<string> GetAvailableExportJsons()
	{
		List<string> ret = new List<string>();
		if (Directory.Exists(UtuPluginPaths.GetPluginFolderFullExport()))
		{
			string[] timestamps = Directory.GetDirectories(UtuPluginPaths.GetPluginFolderFullExport());
			Array.Sort(timestamps);
			Array.Reverse(timestamps);
			foreach (string x in timestamps)
			{
				ret.Add(x + UtuPluginPaths.slash + "UtuPlugin.json");
			}
		}
		return ret;
	}
}
