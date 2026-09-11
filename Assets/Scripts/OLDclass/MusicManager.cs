using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
public static MusicManager Instance { get; private set; }

 [Header("Audio Sources")]

 [SerializeField] private AudioSource menuMusicSource;
 [SerializeField] private AudioSource gameplayMusicSource;
 [SerializeField] private string gameplaySceneName = "SampleScene";
 [SerializeField] private string mainMenuSceneName = "MainMenu";

 private void Awake()
{
    if (Instance != null && Instance != this) 
    {
        Destroy(gameObject);
        return; 
        }

    Instance = this;
    DontDestroyOnLoad(gameObject);

     menuMusicSource.loop = true;
     gameplayMusicSource.loop = true;
     menuMusicSource.Play();
     gameplayMusicSource.Pause();

    SceneManager.activeSceneChanged += OnSceneChanged;
}

private void OnDestroy()
{
    SceneManager.activeSceneChanged -= OnSceneChanged;
}

private void OnSceneChanged(Scene oldScene, Scene newScene)
{
 //Ignore ResetScene
 // if (newScene.name == resetSceneName)
 //{return;}
 // Switch music based on scene name
 if (newScene.name == mainMenuSceneName)
 {
    gameplayMusicSource.Stop();
    menuMusicSource.Play();
 }

 else if (newScene.name == gameplaySceneName)
 {
     menuMusicSource.Stop();
    gameplayMusicSource.Play();
 }

}

}



