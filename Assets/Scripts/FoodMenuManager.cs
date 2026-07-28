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
}