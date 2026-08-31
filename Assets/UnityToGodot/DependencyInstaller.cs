#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

[InitializeOnLoad]
public class DependencyInstaller
{
    static ListRequest listRequest;
    static List<AddRequest> AddRequests;

    static DependencyInstaller()
    {
        listRequest = Client.List();
        AddRequests = new List<AddRequest>();
        EditorApplication.update += CheckListProgress;
    }

    static bool AddPackage( string PackageName )
    {
        bool hasPackage = listRequest.Result.Any(p => p.name == PackageName);
        if (!hasPackage)
        {
            AddRequests.Add( Client.Add( PackageName ) );
            return true;
        }
        else
            return false;
    }
    static void CheckListProgress()
    {
        if (!listRequest.IsCompleted)
            return;

        EditorApplication.update -= CheckListProgress;

        bool Result = AddPackage("com.unity.mathematics");//GLTF
        Result = AddPackage("com.unity.burst") || Result;//GLTF
        Result = AddPackage("com.unity.shadergraph") || Result;//for UTG terrain
        
        if (Result)
        {
            EditorApplication.update += CheckAddProgress;
        }
    }

    static void CheckAddProgress()
    {
        for (int i = 0; i < AddRequests.Count; i++)
        {
            if (!AddRequests[i].IsCompleted)
                continue;

            EditorApplication.update -= CheckAddProgress;

            if (AddRequests[i].Status == StatusCode.Failure)
                UnityEngine.Debug.LogError("Failed to install Package : " + AddRequests[i].Error.message);
        }
    }
}
#endif
