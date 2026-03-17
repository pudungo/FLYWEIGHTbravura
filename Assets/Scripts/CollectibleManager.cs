using UnityEngine;
using UnityEngine.SceneManagement;
using static Collectible;

public class CollectibleManager : MonoBehaviour
{
    public static CollectibleManager Instance { get; private set; }

    [Header("Collectible Counts")]
    [SerializeField] private int coinsTotal;
    [SerializeField] private int gemsTotal;

    public int CoinsTotal => coinsTotal;
    public int GemsTotal => gemsTotal;
    
[SerializeField] private string winSceneName = "WinScene";
[SerializeField] private int coinsToWin = 9;

private bool hasWon;


    /// Fired every time any collectible is collected.
    /// Provides the updated totals for all types.
    public event System.Action<int, int> OnTotalsChanged;

    public void ResetTotals()
    {
        coinsTotal = 0;
        gemsTotal = 0;
        hasWon = false;
        OnTotalsChanged?.Invoke(coinsTotal, gemsTotal);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        // Subscribe to the collectible event
        CollectibleEventSystem.OnCollectibleCollected += HandleCollectibleCollected;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        // Unsubscribe from the event
        CollectibleEventSystem.OnCollectibleCollected -= HandleCollectibleCollected;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.buildIndex == 0)
        {
            ResetTotals();
        }
    }

    private void HandleCollectibleCollected(CollectibleType type, int amount)
    {
        switch (type)
        {
            case CollectibleType.Coin:
                coinsTotal += amount;
                break;
            case CollectibleType.Gem:
                gemsTotal += amount;
                break;
        }

        OnTotalsChanged?.Invoke(coinsTotal, gemsTotal);

    if (!hasWon && coinsTotal >= coinsToWin)
    {
        hasWon = true;
        SceneManager.LoadScene(winSceneName);
        Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;
    }

    }

 
    /// Helper to get the total for a specific collectible type.
    public int GetTotalForType(CollectibleType type)
    {
        return type switch
        {
            CollectibleType.Coin => coinsTotal,
            CollectibleType.Gem => gemsTotal,
            _ => 0
        };
    }

}
