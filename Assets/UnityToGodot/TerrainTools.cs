using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;
using System.Globalization;

#if UNITY_EDITOR
using UnityEditor;


public class TerrainTools : EditorWindow
{

#if UNITY_EDITOR

    class GrassInstances
    {
        public GameObject prefab;
        public MeshRenderer prefabMeshRenderer;
        public Mesh prefabMesh;

        public List<CombineInstance> combineInstances = new List<CombineInstance>();
        public void Initialize( GameObject prefab )
        {
            this.prefab = prefab;
            if( prefab == null )
                return;

            prefabMeshRenderer = prefab.GetComponent<MeshRenderer>();
            MeshFilter meshFiilter = prefab.GetComponent<MeshFilter>();
            prefabMesh = meshFiilter.sharedMesh;
        }
        public void BuildMesh( GameObject parent )
        {
            if( prefab == null )
                return;

            if ( combineInstances.Count > 0 )
            {
                GameObject rootObject = new GameObject("GrassInstances " + prefab.name );
                rootObject.transform.SetParent( parent.transform );

                Mesh combinedMesh = new Mesh();
                combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                combinedMesh.CombineMeshes( combineInstances.ToArray(), true, true );

                MeshRenderer combinedMeshRenderer = rootObject.AddComponent<MeshRenderer>();
                MeshFilter combinedMeshFilter =  rootObject.AddComponent<MeshFilter>();
                combinedMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                combinedMeshFilter.sharedMesh = combinedMesh;
                combinedMeshRenderer.sharedMaterial = prefabMeshRenderer.sharedMaterial;
            }
            else
            {
                //this means the grass uses prefabs instead of planes so this object is not needed
                DestroyImmediate( prefab );
            }
        }
    }
    public static void DoExtractTreesWOptions()
    {
        GameObject Parent = null;

        Terrain[] AllTerrains = Object.FindObjectsOfType<Terrain>();

        for( int i = 0; i < AllTerrains.Length; i++ )
        {
            Terrain T = AllTerrains[i];
            if( T == null )
                continue;

            if ( Parent == null )
                Parent = new GameObject("ExtractedTrees");

            TreePrototype[] TreePrototypes = T.terrainData.treePrototypes;
            DetailPrototype[] DetailPrototypes = T.terrainData.detailPrototypes;

            for( int u = 0; u < T.terrainData.treeInstanceCount; u++ )
            {
                var TreeInstance = T.terrainData.treeInstances[u];
                int TemplateIndex = TreeInstance.prototypeIndex;
                var Prototype = TreePrototypes[ TemplateIndex ];
                if (Prototype.prefab == null)
                    continue;

                float Completion = (float)u / (float)T.terrainData.treeInstanceCount;
                EditorUtility.DisplayProgressBar("Extracting Trees... ", "Extracting Trees " + u + " / " + T.terrainData.treeInstanceCount, Completion);

                GameObject GOInstance = Instantiate(Prototype.prefab, Parent.transform );

                Vector3 WorldTreePos = Vector3.Scale(TreeInstance.position, T.terrainData.size) + T.transform.position;

                GOInstance.transform.position = WorldTreePos;
                Vector3 InitialScale = GOInstance.transform.localScale;
                GOInstance.transform.localScale = new Vector3( InitialScale.x * TreeInstance.widthScale, InitialScale.y * TreeInstance.heightScale, InitialScale.z * TreeInstance.widthScale );
                float rotationAngles = TreeInstance.rotation / ( Mathf.PI / 180.0f );
                GOInstance.transform.rotation = Quaternion.Euler( new Vector3( 0, rotationAngles, 0 ) );
                GOInstance.name = Prototype.prefab.name + " " + u;
                //Debug.Log("Instance " + u + " widthScale " + TreeInstance.widthScale);
                //Debug.Log("Instance " + u + " heightScale " + TreeInstance.heightScale);
            }
        }

        EditorUtility.ClearProgressBar();
    }
    public static void DoExtractDetails( bool MergePlanes )
    {
        Terrain[] AllTerrains = Object.FindObjectsOfType<Terrain>();
        for (int i = 0; i < AllTerrains.Length; i++)
        {
            Terrain T = AllTerrains[i];
            if (T == null)
                continue;
            detailIndex = 0;
            TerrainCollider collider = T.gameObject.GetComponent<TerrainCollider>();

            int[] Layers = T.terrainData.GetSupportedLayers(0, 0, T.terrainData.detailWidth, T.terrainData.detailHeight);

            GrassInstances[] grassInstances = new GrassInstances[ Layers.Length ];
            for (int u = 0; u < grassInstances.Length; u++)
            {
                grassInstances[u] = new GrassInstances();

                DetailPrototype Prototype = T.terrainData.detailPrototypes[Layers[u]];

                GameObject NewGrassPrototype = GameObject.CreatePrimitive(PrimitiveType.Plane);
                NewGrassPrototype.name = "GrassPrototype" + u;
                NewGrassPrototype.transform.localScale = new Vector3( 0.05f, 0.05f, 0.05f);
                //NewGrassPrototype.transform.localScale = new Vector3( 0.1f, 0.1f, 0.1f);
                NewGrassPrototype.transform.rotation = Quaternion.Euler(90, 0, 0);
                MeshRenderer MR = NewGrassPrototype.GetComponent<MeshRenderer>();

                Shader UnlitShader = Shader.Find("UI/Unlit/Transparent");
                string CurrentRP = UnityToGodot.GetCurrentRenderPipeline();
                if ( CurrentRP == "Universal")
                {
                    UnlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                }
                else if ( CurrentRP == "HDRP")
                {
                    UnlitShader = Shader.Find("HDRP/Unlit");
                }
                Material NewMaterial = new Material( UnlitShader );// MR.sharedMaterial.shader );
                NewMaterial.name = "Material for " + NewGrassPrototype.name;
                if (Prototype != null)
                {
                    #if USING_URP
                    if ( CurrentRP == "Universal")
                    {
                        NewMaterial.SetTexture("_BaseMap", Prototype.prototypeTexture);
                    }
                    #endif
                    #if USING_HDRP
                    if ( CurrentRP == "HDRP")
                    {
                        NewMaterial.SetTexture("_UnlitColorMap", Prototype.prototypeTexture);
                    }
                    #endif
                    if ( CurrentRP == "Built-In")
                    {
                        NewMaterial.SetTexture("_MainTex", Prototype.prototypeTexture);
                        NewMaterial.SetColor("_Color", Prototype.healthyColor );
                    }
                    
                    NewMaterial.SetFloat("_AlphaClip", 1.0f );
                    NewMaterial.SetFloat("_Cull", 0.0f);
                    NewMaterial.EnableKeyword("_ALPHATEST_ON");                    
                }
                MR.material = NewMaterial;

                grassInstances[u].Initialize( NewGrassPrototype );
            }

            float[] GrassHeights = new float[grassInstances.Length];
            for (int u = 0; u < GrassHeights.Length; u++)
            {
                if (grassInstances[u].prefab != null)
                    GrassHeights[u] = GetBoundsY(grassInstances[u].prefab);
            }

            GameObject GrassInstancesRoot = null;
            if ( Layers.Length > 0 )
                GrassInstancesRoot = new GameObject("GrassInstances");

            for (int u = 0; u < Layers.Length; u++)
            {
                System.Int64 AllDensities = 0;
                int DensitiesRecorded = 0;
                if ( EditorUtility.DisplayCancelableProgressBar( "Extract Details", "Processing Detail Layers ( " + T.terrainData.detailWidth + " x " + T.terrainData.detailHeight + " ) "
                    + " Layer " + u + "/" + Layers.Length, (float)u/(float)Layers.Length ))
                    return;
                int[,] Details = T.terrainData.GetDetailLayer(0, 0, T.terrainData.detailWidth, T.terrainData.detailHeight, Layers[u]);

                List<CombineInstance> combineInstances = new List<CombineInstance>();
                Material PrototypeMaterial = null;
                if (Details != null)
                {
                    //float PrevPercent = 0;
                    System.Int64 LastDensities = 0;
                    for (int y = 0; y < T.terrainData.detailHeight; y++)
                        for (int x = 0; x < T.terrainData.detailWidth; x++)
                        {
                            int Density = 0;
                            try
                            {
                                Density = (int)( (float)Details[x, y]  * DetailDensityScale );
                                AllDensities += Density;
                                DensitiesRecorded++;
                                LastDensities++;
                            }
                            catch (System.Exception E)
                            {
                                Debug.LogError("ErrorAt " + x + " " + y + E.Message + E.StackTrace );
                            }

                            float percent = (float)(y * T.terrainData.detailWidth + x) / (float)(T.terrainData.detailWidth * T.terrainData.detailHeight);
                            if ( LastDensities > 10000 )
                            {
                                LastDensities = 0;
                                if ( EditorUtility.DisplayCancelableProgressBar("Processing Detail Layer " + (u + 1 ) + "/" + Layers.Length, " Layer " + u + " Current Instances " + AllDensities + " AverageDensity " + (float)AllDensities/(float)DensitiesRecorded, percent))
                                {
                                     EditorUtility.ClearProgressBar();
                                    return;
                                }
                            }

                            DetailPrototype Prototype = T.terrainData.detailPrototypes[Layers[u]];
                            MeshFilter meshFilter = null;
                            MeshRenderer meshRenderer = null;
                            if (Prototype.prototype != null)
                            {
                                meshFilter = Prototype.prototype.GetComponent<MeshFilter>();
                                meshRenderer = Prototype.prototype.GetComponent<MeshRenderer>();
                            }
                            
                            //if (Density > 0)
                            for(int d=0; d<Density; d++ )
                            {
                                //Debug.Log( "Density " + Density + " at " + x + " " + y );

                                float OffsetX = ( (float)x / (float)T.terrainData.detailWidth) * T.terrainData.size.x;
                                float OffsetZ = ( (float)y / (float)T.terrainData.detailHeight) * T.terrainData.size.z;
                                float DiffX = ( 1.0f / (float)T.terrainData.detailWidth) * T.terrainData.size.x;
                                float DiffZ = ( 1.0f / (float)T.terrainData.detailWidth) * T.terrainData.size.z;

                                float RandX = Random.Range( 0, DiffX );
                                float RandZ = Random.Range( 0, DiffZ );

                                Vector3 RayStart = new Vector3( OffsetZ + RandZ, 1000, OffsetX + RandX ) + T.transform.position;
                                Ray ray = new Ray( RayStart, new Vector3( 0, -1, 0) );
                                RaycastHit hit;
                                collider.Raycast( ray, out hit, 1500 );

                                if( hit.transform != null )
                                {
                                    float rand = Random.Range(0, 360 );
                                    CombineInstance instance = new CombineInstance();
                                    
                                    GameObject clone;

                                    float RandomXPercent = UnityEngine.Random.value;
                                    float RandomYPercent = UnityEngine.Random.value;

                                    float Width = Prototype.minWidth + RandomXPercent * (Prototype.maxWidth - Prototype.minWidth);
                                    float Height = Prototype.minHeight + RandomYPercent * (Prototype.maxHeight - Prototype.minHeight);

                                    if ( Prototype.prototype != null )
                                    {
                                        bool CombineAll = false;
                                        if( CombineAll )
                                        {
                                            if( meshFilter != null )
                                                instance.mesh = meshFilter.sharedMesh;
                                            if( meshRenderer != null )
                                                PrototypeMaterial = meshRenderer.sharedMaterial;

                                            instance.transform = Matrix4x4.TRS( hit.point, Quaternion.Euler( 0, rand, 0 ), new Vector3(Width, Height, 1 ) );
                                            combineInstances.Add( instance );
                                        }
                                        else
                                        {
                                            clone = Instantiate( Prototype.prototype, hit.point, Quaternion.Euler( 0, rand, 0 ), GrassInstancesRoot.transform );
                                            clone.transform.localScale = new Vector3(Width, Height, 1 );
                                        }
                                    }
                                    else
                                    {
                                        //Grass Rotation should be around terrain normal
                                        Vector3 GrassNormal = hit.normal;
                                        int GrassIndex = Layers[ u ];
                                        
                                        GameObject template = grassInstances[ GrassIndex ].prefab;
                                        float HeightOffset = GrassHeights[GrassIndex];
                                        Vector3 InitialEuler = template.transform.rotation.eulerAngles;

                                        instance.mesh = grassInstances[GrassIndex].prefabMesh;
                                        Vector3 Scale = new Vector3( template.transform.localScale.x * Width,
                                            template.transform.localScale.y,
                                            template.transform.localScale.z * Height );//Height is because it's a rotated plane !
                                        instance.transform = Matrix4x4.TRS( hit.point + new Vector3( 0, HeightOffset, 0 ), Quaternion.Euler( InitialEuler.x, rand, InitialEuler.z ), Scale );
                                        grassInstances[GrassIndex].combineInstances.Add( instance );
                                        //clone = Instantiate( template, hit.point + new Vector3(0, HeightOffset,0), Quaternion.Euler(InitialEuler.x, rand, InitialEuler.z ), GrassInstances.transform );
                                    }

                                    //clone.name = "Detail " + detailIndex;
                                    detailIndex++;
                                }
                            }
                        }                    
                }
                if ( combineInstances.Count > 0)
                {
                    GameObject CombineGO = new GameObject("GrassInstances" + u );
                    Mesh combinedMesh = new Mesh();
                    combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    combinedMesh.CombineMeshes( combineInstances.ToArray(), true, true );

                    MeshRenderer combinedMeshRenderer = CombineGO.AddComponent<MeshRenderer>();
                    MeshFilter combinedMeshFilter =  CombineGO.AddComponent<MeshFilter>();
                    combinedMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    combinedMeshFilter.sharedMesh = combinedMesh;
                    combinedMeshRenderer.sharedMaterial = PrototypeMaterial;
                }
            }
            EditorUtility.ClearProgressBar();
            
            if ( MergePlanes )
            { 
                for( int u = 0; u < grassInstances.Length; u++ )
                {
                    if ( EditorUtility.DisplayCancelableProgressBar( "Extract Details", "Combining Meshes " + u + "/" + grassInstances.Length ,(float)u/(float)grassInstances.Length ))
                    {
                        EditorUtility.ClearProgressBar();
                        return;
                    }
                    grassInstances[u].BuildMesh( GrassInstancesRoot );
                }
            }

            EditorUtility.ClearProgressBar();
        }
    }
    static float GetBoundsY(GameObject obj)
    {
        MeshRenderer mr = obj.GetComponent<MeshRenderer>();
        return mr.bounds.extents.y;
    }
    static int detailIndex = 0;
    static Mesh CreateNewMesh( Vector3 Position )
    {
        GameObject NewGO = new GameObject("NewMesh");
        MeshRenderer MR = NewGO.AddComponent<MeshRenderer>();
        MeshFilter MF = NewGO.AddComponent<MeshFilter>();
        Mesh NewMesh = new Mesh();
        MF.mesh = NewMesh;
        MF.sharedMesh = NewMesh;
        MR.sharedMaterial = new Material(Shader.Find("Standard"));
        NewGO.transform.position = Position;
        //MR.receiveShadows = false;
        return NewMesh;
    }
    static float[,] RescaleHeights( float[,] InitialHeights, int TargetLength, int InitialLength )
    {
        float Ratio = (float)InitialLength / (float)TargetLength;
        int RatioI = (int)Ratio;
        float[,] DestinationHeights = new float[TargetLength, TargetLength];
        for(int i=0; i<TargetLength; i++)
            for(int u=0; u<TargetLength; u++)
            {
                float Sum = 0;
                int Count = 0;
                for(int x=-RatioI/2; x < RatioI / 2; x++)
                    for (int y = -RatioI / 2; y < RatioI / 2; y++)
                    {
                        float OriginalX = (float)i * Ratio + (float)x;
                        float OriginalY =  (float)u * Ratio + (float)y ;
                        OriginalX = Mathf.Clamp(OriginalX, 0, InitialLength - 1);
                        OriginalY = Mathf.Clamp(OriginalY, 0, InitialLength - 1);

                        Sum += InitialHeights[(int)OriginalX, (int)OriginalY];
                        Count++;
                    }

                if ( Count  == 0)
                {
                    int InitialX = (int)( (float)i * Ratio );
                    int InitialY = (int)( (float)u * Ratio );
                    Sum = InitialHeights[ InitialX, InitialY ];
                    Count = 1;
                }
                Sum /= (float)Count;
                DestinationHeights[i, u] = Sum;
            }
        return DestinationHeights;
    }
    static bool[,] RescaleHoles( bool[,] InitialHoles, int TargetLength, int InitialLength )
    {
        float Ratio = (float)InitialLength / (float)TargetLength;
        int RatioI = (int)Ratio;
        bool[,] Destination = new bool[TargetLength, TargetLength];
        for(int i=0; i<TargetLength; i++)
            for(int u=0; u<TargetLength; u++)
            {
                float Sum = 0;
                int Count = 0;
                for(int x=-RatioI/2; x < RatioI / 2; x++)
                    for (int y = -RatioI / 2; y < RatioI / 2; y++)
                    {
                        float OriginalX = (float)i * Ratio + (float)x;
                        float OriginalY =  (float)u * Ratio + (float)y ;
                        OriginalX = Mathf.Clamp(OriginalX, 0, InitialLength - 1);
                        OriginalY = Mathf.Clamp(OriginalY, 0, InitialLength - 1);

                        if ( InitialHoles[(int)OriginalX, (int)OriginalY])
                            Sum++;
                        Count++;
                    }

                if ( Count  == 0)
                {
                    int InitialX = (int)( (float)i * Ratio );
                    int InitialY = (int)( (float)u * Ratio );
                    if ( InitialHoles[ InitialX, InitialY ])
                        Sum++;
                    Count = 1;
                }
                Sum /= (float)Count;
                bool Value = false;
                if( Sum > 0 )
                    Value = true;
                Destination[i, u] = Value;
            }
        return Destination;
    }
    [MenuItem("UnityToGodot/TerrainTools...")]
    static public void ShowWindow()
    {
        EditorWindow window = EditorWindow.GetWindow(typeof(TerrainTools), true, "TerrainTools");
        
        Rect NewPosition = new Rect(new Vector2(300, 500), new Vector2(400, 200));

        window.position = NewPosition;
    }
    public static int MaxTriangles = 1000000;
    public static string FolderPath = "Assets\\ExportedTerrain\\";// ExportSetup.FolderPath + "\\" + ExportSetup.SceneName;
    public static bool ExportTextures = true;
    public static bool ExportGeometry = true;
    public static bool DoRescale = true;
    static bool MergeDetailPlanes = true;
    public static float DetailDensityScale = 1.0f;

    public void OnGUI()
    {        
        FolderPath = EditorGUILayout.TextField("ExportFolder", FolderPath);
        ExportTextures = EditorGUILayout.Toggle("ExportTextures", ExportTextures);
        ExportGeometry = EditorGUILayout.Toggle("ExportGeometry", ExportGeometry);
        DoRescale = EditorGUILayout.Toggle("RescaleHeightmap", DoRescale );
        MaxTriangles = EditorGUILayout.IntField("MaxRescaleTriangles", MaxTriangles);
        MergeDetailPlanes = EditorGUILayout.Toggle("MergeDetailPlanes", MergeDetailPlanes);
        if (GUILayout.Button("ConvertTerrainToStaticMesh"))
        {
            ConvertTerrainToStaticMesh();
        }
        if (GUILayout.Button("ConvertTreesToGameObjects"))
        {
            DoExtractTreesWOptions();
        }
        if (GUILayout.Button("ConvertDetailsToStaticMesh"))
        {
            DoExtractDetails( MergeDetailPlanes );
        }
    }
    
    public static void ConvertTerrainToStaticMesh()
    {
        if ( EditorUtility.DisplayCancelableProgressBar("ConvertTerrainToStaticMesh", "Exporting Textures...", 0 ))
        {
            EditorUtility.ClearProgressBar();
            return;
        }
        
        Terrain[] AllTerrains = Object.FindObjectsOfType<Terrain>();

        for (int i = 0; i < AllTerrains.Length; i++)
        {
            Terrain T = AllTerrains[i];
            if (T == null)
                continue;
            
            if (!Directory.Exists(FolderPath))
                Directory.CreateDirectory(FolderPath);

            List<string> AlphaMapsExportedPaths = new List<string>();
            List<Texture> SplatPrototypesDiffuses = new List<Texture>();
            List<Texture> SplatPrototypesNormals = new List<Texture>();
            SplatPrototype[] SplatPrototypes = T.terrainData.splatPrototypes;
            if (ExportTextures)
            {
                Texture2D[] AlphaMaps = T.terrainData.alphamapTextures;
                
                for (int u = 0; u < AlphaMaps.Length; u++)
                {
                    Texture2D Tex = AlphaMaps[u];

                    string FileName = FolderPath + "\\" + Tex.name + ".png";
                    
                    DoExportTexture(Tex, FileName, FileName);
                    AlphaMapsExportedPaths.Add( FileName );
                }

                for (int u = 0; u < SplatPrototypes.Length; u++)
                {
                    if ( EditorUtility.DisplayCancelableProgressBar("ConvertTerrainToStaticMesh", "Exporting Textures...", (float)u/(float)SplatPrototypes.Length ))
                    {
                        EditorUtility.ClearProgressBar();
                        return;
                    }
                    if ( u == 0)
                    {
                        //SplatTexcoordsStr += "float2 SplatTexcoordScaled[ " + SplatPrototypes.Length + " ];\n";
                    }
                    Texture2D Diffuse = SplatPrototypes[u].texture;
                    Texture2D Normal = SplatPrototypes[u].normalMap;
                    float Metallic = SplatPrototypes[u].metallic;
                    float Smoothness = SplatPrototypes[u].smoothness;
                    Vector2 TileSize = SplatPrototypes[u].tileSize;

                    Vector2 FinalTexcoord = new Vector2( T.terrainData.size.x / SplatPrototypes[u].tileSize.x, T.terrainData.size.z / SplatPrototypes[u].tileSize.y );

                    string LayerName = "" + u;
                    if (T.terrainData.terrainLayers.Length > u)
                    {
                        //LayerName = T.terrainData.terrainLayers[u].name;
                    }

                    //string DiffuseFileName = FolderPath + "\\" + LayerName + "_Diffuse" + ".png";
                    //if (Diffuse != null )
                    //{ 
                    //    DoExportTexture(Diffuse, DiffuseFileName, DiffuseFileName);
                    //}
                    //string NormalFileName = FolderPath + "\\" + LayerName + "_Normal" + ".png";
                    //if (Normal != null)
                    //{
                    //    DoExportTexture( Normal, NormalFileName, NormalFileName );
                    //}

                    SplatPrototypesDiffuses.Add( Diffuse );
                    SplatPrototypesNormals.Add( Normal );
                }
            }

            if (!ExportGeometry)
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
                return;
            }

            float[,] Heights = T.terrainData.GetHeights(0, 0, T.terrainData.heightmapResolution, T.terrainData.heightmapResolution);
            bool[,] Holes = T.terrainData.GetHoles( 0, 0, T.terrainData.holesResolution, T.terrainData.holesResolution );
                        
            int OriginalWidth = T.terrainData.heightmapResolution;
            int OriginalHeight = T.terrainData.heightmapResolution;
            Vector3 OriginalSize = T.terrainData.size;
            int TargetHeightmapSize = T.terrainData.heightmapResolution;
            
            if (DoRescale)
            {
                if (EditorUtility.DisplayCancelableProgressBar("ConvertTerrainToStaticMesh", "Rescaling Heights...", 0))
                {
                    EditorUtility.ClearProgressBar();
                    return;
                }

                TargetHeightmapSize = (int)Mathf.Sqrt(MaxTriangles / 2.0f);
                float Ratio = (float)T.terrainData.heightmapResolution/ (float)TargetHeightmapSize;
                Heights = RescaleHeights(Heights, TargetHeightmapSize, T.terrainData.heightmapResolution);
                Holes = RescaleHoles( Holes, TargetHeightmapSize, T.terrainData.holesResolution );
            }
            int TotalVerts = TargetHeightmapSize * TargetHeightmapSize;
            int HeightmapWidth = TargetHeightmapSize;
            int HeightmapHeight = TargetHeightmapSize;

            bool ModifyOriginal = false;// true;
            if (ModifyOriginal)
            {
                //This works but valid sizes are only powers of two !
                T.terrainData.heightmapResolution = TargetHeightmapSize;
                T.terrainData.SetHeights(0, 0, Heights);
                T.terrainData.size = OriginalSize;
            }

            Vector3 Scale = T.terrainData.heightmapScale;

            Vector3[] TerrainVertices = new Vector3[TotalVerts];
            Vector2[] TerrainTexcoords = new Vector2[TotalVerts];
            int[] TerrainIndices = new int[ (HeightmapWidth - 1 ) * (HeightmapHeight - 1 ) * 6 ];

            if ( EditorUtility.DisplayCancelableProgressBar("ConvertTerrainToStaticMesh", "Generating vertices...", 0) )
            {
                EditorUtility.ClearProgressBar();
                return;
            }

            int MaxIterations = HeightmapWidth * HeightmapHeight;
            Debug.Log("Generating vertices...");
            float PrevPercentage = 0;
            for (int x = 0; x < HeightmapWidth; x++)
                for (int y = 0; y < HeightmapHeight; y++)
                {
                    float NewPercentage = (float)(x * HeightmapHeight + y) / (float)MaxIterations;
                    if( NewPercentage - PrevPercentage > 1.0 )
                    {
                        if ( EditorUtility.DisplayCancelableProgressBar( "ConvertTerrainToStaticMesh", "Generating vertices...", NewPercentage ))
                        {
                            EditorUtility.ClearProgressBar();
                            return;
                        }
                        PrevPercentage = NewPercentage;
                    }

                    //TerrainVertices[y * HeightmapWidth + x].Set(x * Scale.x, Heights[x, y] * Scale.y, y * Scale.z);

                    float XScaled = ((float)x / (float)HeightmapWidth ) * T.terrainData.size.x;
                    float ZScaled = ((float)y / (float)HeightmapHeight) * T.terrainData.size.z;

                    TerrainVertices[y * HeightmapWidth + x].Set(XScaled, Heights[x, y] * Scale.y, ZScaled);
                    TerrainTexcoords[y * HeightmapWidth + x].Set((float)x / (float)HeightmapWidth, (float)y / (float)HeightmapHeight );
                }

            Debug.Log("Generating indices...");

            MaxIterations = (HeightmapWidth - 1 ) * ( HeightmapHeight - 1 );
            PrevPercentage = 0;
            int CurrentIndex = 0;
            for (int x = 0; x < HeightmapWidth - 1; x++)
                for (int y = 0; y < HeightmapHeight - 1; y++)
                {
                    float NewPercentage = (float)( x * ( HeightmapHeight - 1 ) + y ) / (float)MaxIterations ;
                    if( NewPercentage - PrevPercentage > 1.0 )
                    {
                        if (EditorUtility.DisplayCancelableProgressBar( "ConvertTerrainToStaticMesh", "Generating indices...", NewPercentage ))
                        {
                            EditorUtility.ClearProgressBar();
                            return;
                        }
                        PrevPercentage = NewPercentage;
                    }                    

                    int DimensionX = Holes.GetLength(0);
                    int DimensionY = Holes.GetLength(1);
                    //Sometimes heightmap res != hole res
                    if ( x + 1 >= DimensionX || y + 1 >= DimensionY)
                        continue;
                    if ( Holes == null ||
                        ( Holes[x,y]       && Holes[x + 1, y]  &&
                          Holes[x, y + 1]  && Holes[ x + 1, y + 1 ] ))
                    {
                        TerrainIndices[CurrentIndex++] = (y + 1 ) * HeightmapWidth + x;
                        TerrainIndices[CurrentIndex++] = y * HeightmapWidth + x + 1;
                        TerrainIndices[CurrentIndex++] = y * HeightmapWidth + x;
                    
                        TerrainIndices[CurrentIndex++] = ( y + 1 ) * HeightmapWidth + x;
                        TerrainIndices[CurrentIndex++] = (y + 1) * HeightmapWidth + x + 1;
                        TerrainIndices[CurrentIndex++] = y * HeightmapWidth + x + 1;
                    }
                    else
                    {
                        //Debug.Log("Hole");
                        //Hole
                    }
                }
            //Mesh TerrainMesh = CreateNewMesh(T.gameObject.transform.position);
            //TerrainMesh.vertices = TerrainVertices;

            StringBuilder sb = new StringBuilder();

            string MTLFile = "Terrain.mtl";
            sb.AppendLine("mtllib " + MTLFile );
            sb.AppendLine("g " + "TerrainGeom");

            if( EditorUtility.DisplayCancelableProgressBar("ConvertTerrainToStaticMesh", "Building OBJ File...", 0.5f ) )
            {
                EditorUtility.ClearProgressBar();
                return;
            }

            StringBuilder TempSB = new StringBuilder();
            for (int v = 0; v < TerrainVertices.Length; v++)
            {
                //TempSB.Clear();

                string VertexDefinition = string.Format( CultureInfo.InvariantCulture, "v {0} {1} {2}", TerrainVertices[v].x,TerrainVertices[v].y,TerrainVertices[v].z);
                //TempSB.Append("v ");
                //TempSB.Append(TerrainVertices[v].x);
                //TempSB.Append(" ");
                //TempSB.Append(TerrainVertices[v].y);
                //TempSB.Append(" ");
                //TempSB.Append(TerrainVertices[v].z);                
                sb.AppendLine( VertexDefinition );
            }
            //for (int v = 0; v < TerrainVertices.Length; v++)
            //{
              //  sb.AppendLine("vn 0 1 0" );
            //}
            for (int v = 0; v < TerrainTexcoords.Length; v++)
            {
                //TempSB.Clear();

                string UVDefinition = string.Format( CultureInfo.InvariantCulture, "vt {0} {1}", TerrainTexcoords[v].x,TerrainTexcoords[v].y);
                //TempSB.Append("vt ");
                //TempSB.Append(TerrainTexcoords[v].x);
                //TempSB.Append(" ");
                //TempSB.Append(TerrainTexcoords[v].y);
                sb.AppendLine( UVDefinition );
            }

            sb.AppendLine("\n");

            for (int f = 0; f < TerrainIndices.Length; f+= 3)
            {
                int i1 = TerrainIndices[f + 0] + 1;
                int i2 = TerrainIndices[f + 1] + 1;
                int i3 = TerrainIndices[f + 2] + 1;
                string i1str = i1 + "/" + i1;// + "/" + i1;
                string i2str = i2 + "/" + i2;// + "/" + i2;
                string i3str = i3 + "/" + i3;// + "/" + i3;

                TempSB.Clear();

                TempSB.Append("f ");
                TempSB.Append(i1str);
                TempSB.Append(" ");
                TempSB.Append(i2str);
                TempSB.Append(" ");
                TempSB.Append(i3str);
                //sb.AppendLine("f " + i1str + " " + i2str + " " + i3str );
                sb.AppendLine(TempSB.ToString());
            }

            string TerrainOBJPath = FolderPath + "\\terrain_export.obj";
            System.IO.File.WriteAllText(TerrainOBJPath, sb.ToString());

            sb = new StringBuilder();
            sb.AppendLine("newmtl " + "TerrainMat" );
            sb.AppendLine("map_Kd " + "TerrainTex.png");
            sb.AppendLine("illum 2");

            if( EditorUtility.DisplayCancelableProgressBar("ConvertTerrainToStaticMesh", "Writing Files...", 1.0f) )
            {
                EditorUtility.ClearProgressBar();
                return;
            }

            string TerrainMTLPath = FolderPath + "\\" + MTLFile;
            System.IO.File.WriteAllText(TerrainMTLPath, sb.ToString());

            //string ShaderTexcoordsText = FolderPath + "\\Texcoords.txt";
            //System.IO.File.WriteAllText(ShaderTexcoordsText, SplatTexcoordsStr);

            Debug.Log("Terrain OBJ Exporterd");

            EditorUtility.ClearProgressBar();

            AssetDatabase.Refresh();
            //UnityEngine.Object mainAssetFile = AssetDatabase.LoadMainAssetAtPath( TerrainOBJPath );
            //GameObject mainAssetFile = Resources.Load<GameObject>( TerrainOBJPath );
            UnityEngine.Object mainAssetFile = AssetDatabase.LoadAssetAtPath<GameObject>(TerrainOBJPath);

            Transform TerrainTransform = T.gameObject.transform;
            GameObject InstatiatedObject = (GameObject)PrefabUtility.InstantiatePrefab(mainAssetFile);
            InstatiatedObject.transform.SetParent( TerrainTransform, false );
            InstatiatedObject.transform.rotation *= Quaternion.Euler(0, 90, 0);//6.0 + URP requires this

            //This fixes Terrain Width/Length not being 1:1
            float XScale = Scale.z / Scale.x;
            float ZScale = 1.0f / XScale;
            InstatiatedObject.transform.localScale = new Vector3( XScale, 1, ZScale );

            GameObject StaticMeshGO = InstatiatedObject as GameObject;
            MeshRenderer MR = null;
            if (StaticMeshGO != null )
                MR = StaticMeshGO.GetComponentInChildren<MeshRenderer>();
            if ( MR != null )
            {
                Shader UTG_Terrain = Shader.Find("Shader Graphs/UTG_Terrain");
                if (UTG_Terrain == null)
                {
                    Debug.LogError("Shader UTG_Terrain probably didn't compile, can't assigned the material to the exported terrain !");
                    return;
                }
                Material CurrentMaterial = new Material( UTG_Terrain );
                MR.sharedMaterial = CurrentMaterial;
                //CurrentMaterial.shader = UTG_Terrain;

                for (int t = 0; t < AlphaMapsExportedPaths.Count; t++)
                {
                    Texture Tex = AssetDatabase.LoadAssetAtPath<Texture>(AlphaMapsExportedPaths[t]);
                    if ( Tex != null )
                    {
                        CurrentMaterial.SetTexture("_Splat" + t, Tex );
                    }
                }
                for (int t = 0; t < SplatPrototypesDiffuses.Count; t++)
                {
                    Texture Tex = SplatPrototypesDiffuses[t];
                    if (Tex != null)
                    {
                        CurrentMaterial.SetTexture("_Albedo" + t, Tex);
                    }
                }
                for (int t = 0; t < SplatPrototypesNormals.Count; t++)
                {
                    Texture Tex = SplatPrototypesNormals[t];
                    if (Tex != null)
                    {
                        //if (!UnityToGodot.IsNormalMap( Tex ))
                        //{
                        //    UnityToGodot.SetIsNormalMap( Tex, true );
                        //}
                        CurrentMaterial.SetTexture("_Normal" + t, Tex);
                    }
                }

                for (int s = 0; s < SplatPrototypes.Length; s++)
                {
                    float Metallic = SplatPrototypes[s].metallic;
                   
                    float SmoothnessSource = 0;
                    TerrainLayer Layer = null;
                    #if UNITY_6000_0_OR_NEWER
                        if ( T.terrainData.terrainLayers.Length > s )
                            Layer =  T.terrainData.terrainLayers[s];
                        #endif
                    string CurrentRP = UnityToGodot.GetCurrentRenderPipeline();
                    if ( CurrentRP == "Universal")
                    {
                        #if UNITY_6000_0_OR_NEWER
                        if ( Layer != null )
                            SmoothnessSource =  (float)Layer.smoothnessSource;
                        #endif
                    }
                    if ( CurrentRP == "HDRP" && Layer != null && Layer.maskMapTexture != null )
                    {
                        CurrentMaterial.SetTexture("_MaskMap" + s, Layer.maskMapTexture );
                        CurrentMaterial.SetVector("_MaskRemapMin" + s, Layer.maskMapRemapMin );
                        CurrentMaterial.SetVector("_MaskRemapMax" + s, Layer.maskMapRemapMax );
                        CurrentMaterial.SetInt("_UseMaskMaps", 1 );
                    }
                    float Smoothness = SplatPrototypes[s].smoothness;
                    Vector2 TileSize = SplatPrototypes[s].tileSize;

                    CurrentMaterial.SetVector("_Tile" + s, new Vector2( 1.0f / TileSize.x, 1.0f / TileSize.y ) );
                    CurrentMaterial.SetFloat("_SmoothnessSource" + s, SmoothnessSource );
                    CurrentMaterial.SetFloat("_Smoothness" + s, Smoothness );
                    CurrentMaterial.SetFloat("_Metallic" + s, Metallic );
                }

                AssetDatabase.CreateAsset(CurrentMaterial, FolderPath + "TerrainMaterial.mat" );
                AssetDatabase.SaveAssets();
            }
        }
    }

    public static Texture2D ConvertTexture(Texture2D texture)
    {
        RenderTexture tmp = RenderTexture.GetTemporary(
                        texture.width,
                        texture.height,
                        0,
                        RenderTextureFormat.ARGB32,
                        RenderTextureReadWrite.Default);

        // Blit the pixels on texture to the RenderTexture
        Graphics.Blit(texture, tmp);

        // Backup the currently set RenderTexture
        RenderTexture previous = RenderTexture.active;

        // Set the current RenderTexture to the temporary one we created
        RenderTexture.active = tmp;

        // Create a new readable Texture2D to copy the pixels to it
        Texture2D ConvertedTexture = new Texture2D(texture.width, texture.height, TextureFormat.ARGB32, false);

        // Copy the pixels from the RenderTexture to the new Texture
        ConvertedTexture.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
        ConvertedTexture.Apply();

        // Reset the active RenderTexture
        RenderTexture.active = previous;

        // Release the temporary RenderTexture
        RenderTexture.ReleaseTemporary(tmp);
        return ConvertedTexture;
    }
    public static void DoExportTexture(Texture2D Texture, string FileName, string FileName2)
    {
        if (Texture.format == TextureFormat.ARGB32 || Texture.format == TextureFormat.RGBA32 ||
               Texture.format == TextureFormat.RGB24)
        {
            byte[] PNGBytes = Texture.EncodeToPNG();

            if (PNGBytes != null && PNGBytes.Length > 0)
            {
                File.WriteAllBytes(FileName, PNGBytes);
                //Debug.Log("Exported Texture " + Texture.name);
            }
            else
                Debug.Log("WTF for Texture" + Texture.name);            
        }
        else
        {
            Texture2D RGBATexture = ConvertTexture( Texture );
            byte[] PNGBytes = RGBATexture.EncodeToPNG();

            if (PNGBytes != null && PNGBytes.Length > 0)
            {
                File.WriteAllBytes(FileName, PNGBytes);
                //Debug.Log("Exported Texture " + Texture.name);
            }
            else
                Debug.Log("WTF for Texture" + Texture.name);                        
        }
    }
#endif
}

#endif