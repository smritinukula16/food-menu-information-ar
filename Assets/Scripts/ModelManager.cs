using UnityEngine;

public class ModelManager : MonoBehaviour
{
    public GameObject pizza;
    public GameObject burger;
    public GameObject fries;

    void Start()
    {
        DisableAll();
    }

    void DisableAll()
    {
        pizza.SetActive(false);
        burger.SetActive(false);
        fries.SetActive(false);
    }

    public void ShowPizza()
    {
        DisableAll();
        pizza.SetActive(true);
    }

    public void ShowBurger()
    {
        DisableAll();
        burger.SetActive(true);
    }

    public void ShowFries()
    {
        DisableAll();
        fries.SetActive(true);
    }
}
