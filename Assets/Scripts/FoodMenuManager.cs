using UnityEngine;
using TMPro;

public class FoodMenuManager : MonoBehaviour
{
    [Header("Canvas")]
    public CanvasGroup canvasGroup;

    [Header("Models")]
    public GameObject pizzaModel;
    public GameObject burgerModel;
    public GameObject friesModel;

    [Header("TextMeshPro Fields")]
    public TMP_Text foodNameText;
    public TMP_Text caloriesText;
    public TMP_Text proteinText;
    public TMP_Text oilText;

    [Header("Food Info Data")]
    public FoodInfo pizzaInfo;
    public FoodInfo burgerInfo;
    public FoodInfo friesInfo;

    void Start()
    {
        HideUIInstant();
        ShowPizza(); // default
    }

    // Called from Vuforia events
    public void ShowUI()
    {
        if (!canvasGroup) return;
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    public void HideUI()
    {
        if (!canvasGroup) return;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    void HideUIInstant()
    {
        if (!canvasGroup) return;
        canvasGroup.alpha = 0f;
    }

    public void ShowPizza()
    {
        SetOnly(pizzaModel);
        UpdateUI(pizzaInfo);
    }

    public void ShowBurger()
    {
        SetOnly(burgerModel);
        UpdateUI(burgerInfo);
    }

    public void ShowFries()
    {
        SetOnly(friesModel);
        UpdateUI(friesInfo);
    }

    void SetOnly(GameObject activeModel)
    {
        if (pizzaModel) pizzaModel.SetActive(activeModel == pizzaModel);
        if (burgerModel) burgerModel.SetActive(activeModel == burgerModel);
        if (friesModel) friesModel.SetActive(activeModel == friesModel);
    }

    void UpdateUI(FoodInfo info)
    {
        if (foodNameText) foodNameText.text = info.foodName;
        if (caloriesText) caloriesText.text = info.calories;
        if (proteinText) proteinText.text = info.protein;
        if (oilText) oilText.text = info.oil;
    }

    // ------------------------------------------------------------------
    // Key-based access used by the AR Menu UI (Assets/ARMenu).
    // These only route to the existing methods/fields above, so the
    // original button wiring and model switching keep working unchanged.
    // ------------------------------------------------------------------

    public static readonly string[] ModelKeys = { "pizza", "burger", "fries" };

    public GameObject GetModel(string key)
    {
        switch (Normalize(key))
        {
            case "pizza": return pizzaModel;
            case "burger": return burgerModel;
            case "fries": return friesModel;
            default: return null;
        }
    }

    public FoodInfo GetInfo(string key)
    {
        switch (Normalize(key))
        {
            case "pizza": return pizzaInfo;
            case "burger": return burgerInfo;
            case "fries": return friesInfo;
            default: return null;
        }
    }

    /// <summary>Shows the model for the given key using the original ShowPizza/ShowBurger/ShowFries methods.</summary>
    public bool ShowByKey(string key)
    {
        switch (Normalize(key))
        {
            case "pizza": ShowPizza(); return true;
            case "burger": ShowBurger(); return true;
            case "fries": ShowFries(); return true;
            default: return false;
        }
    }

    static string Normalize(string key) => string.IsNullOrEmpty(key) ? string.Empty : key.Trim().ToLowerInvariant();
}