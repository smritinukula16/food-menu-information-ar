using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARMenu
{
    /// <summary>
    /// Root of the editable menu file (Assets/ARMenu/Resources/ARMenu/menu_data.json).
    /// Any text value that starts with "TODO" is treated as a placeholder and is
    /// rendered in a muted "[Placeholder]" style so it is easy to spot and replace.
    /// </summary>
    [Serializable]
    public class MenuDatabase
    {
        public RestaurantInfo restaurant = new RestaurantInfo();
        public string currencySymbol = "";
        public string[] categories = Array.Empty<string>();
        public MenuItemData[] items = Array.Empty<MenuItemData>();
        public DeveloperInfo developer = new DeveloperInfo();
        public CreditEntry[] credits = Array.Empty<CreditEntry>();
        public string creditsNote = "";

        public IEnumerable<MenuItemData> ItemsIn(string category)
        {
            foreach (var item in items)
                if (string.IsNullOrEmpty(category) || item.category == category)
                    yield return item;
        }

        public MenuItemData Find(string id)
        {
            foreach (var item in items)
                if (item.id == id) return item;
            return null;
        }

        public MenuItemData FindByModelKey(string modelKey)
        {
            foreach (var item in items)
                if (!string.IsNullOrEmpty(item.modelKey) &&
                    string.Equals(item.modelKey, modelKey, StringComparison.OrdinalIgnoreCase))
                    return item;
            return null;
        }
    }

    [Serializable]
    public class RestaurantInfo
    {
        public string name = "TODO: Restaurant name";
        public string welcome = "Welcome";
        public string description = "TODO: Short description of the restaurant";
        /// <summary>Optional Resources path of a logo sprite/texture (e.g. "ARMenu/logo").</summary>
        public string logoResource = "";
    }

    /// <summary>The creator of this AR menu – shown on the splash screen, footer, AR view and credits.</summary>
    [Serializable]
    public class DeveloperInfo
    {
        public string name = "";
        public string role = "";
        public string link = "";
        public string year = "";
        /// <summary>Teammates credited next to the developer in "Done by".</summary>
        public string[] team = Array.Empty<string>();

        /// <summary>"Smriti Nukula & Shreyas Abiram YR" (developer first, then teammates).</summary>
        public string DoneBy
        {
            get
            {
                var names = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(name)) names.Add(name.Trim());
                if (team != null)
                    foreach (var t in team)
                        if (!string.IsNullOrWhiteSpace(t)) names.Add(t.Trim());
                if (names.Count <= 1) return names.Count == 1 ? names[0] : "";
                return string.Join(", ", names.GetRange(0, names.Count - 1)) + " & " + names[names.Count - 1];
            }
        }
    }

    /// <summary>One line in the Credits panel. Entries with the same section are grouped together.</summary>
    [Serializable]
    public class CreditEntry
    {
        public string section;
        public string title;
        public string author;
        public string detail;
        public string license;
    }

    public enum Diet { Unspecified, Vegetarian, NonVegetarian }

    [Serializable]
    public class MenuItemData
    {
        public string id;
        /// <summary>Key understood by FoodMenuManager ("pizza", "burger", "fries"). Empty = no 3D model.</summary>
        public string modelKey;
        public string name;
        public string category;
        /// <summary>0 or less = price not set yet.</summary>
        public float price;
        public string tagline;
        public string description;
        public string[] ingredients = Array.Empty<string>();
        public string preparationStyle;
        public string tasteProfile;
        /// <summary>-1 = not specified, 0 = not spicy, 1 = mild, 2 = medium, 3 = hot.</summary>
        public int spiceLevel = -1;
        public string portionSize;
        /// <summary>"veg", "nonveg" or "" (not specified).</summary>
        public string diet = "";
        /// <summary>Allergens are only displayed when this is true (verified by the restaurant).</summary>
        public bool allergensVerified;
        public string[] allergens = Array.Empty<string>();
        public bool isPopular;
        public bool isChefSpecial;
        /// <summary>Optional Resources path of a food photo. If empty, a render of the 3D model is used.</summary>
        public string imageResource = "";

        // Filled at runtime from the existing FoodMenuManager scene data (FoodInfo).
        [NonSerialized] public string calories;
        [NonSerialized] public string protein;
        [NonSerialized] public string oil;

        public Diet DietType
        {
            get
            {
                var d = (diet ?? "").Trim().ToLowerInvariant();
                if (d == "veg" || d == "vegetarian") return Diet.Vegetarian;
                if (d == "nonveg" || d == "non-veg" || d == "non-vegetarian") return Diet.NonVegetarian;
                return Diet.Unspecified;
            }
        }

        public bool HasPrice => price > 0f;
        public bool HasModel => !string.IsNullOrEmpty(modelKey);
    }

    /// <summary>Helpers for the "TODO:" placeholder convention.</summary>
    public static class Placeholder
    {
        public static bool Is(string value) =>
            string.IsNullOrWhiteSpace(value) || value.TrimStart().StartsWith("TODO", StringComparison.OrdinalIgnoreCase);

        public static bool IsEmptyList(string[] values)
        {
            if (values == null || values.Length == 0) return true;
            foreach (var v in values)
                if (!Is(v)) return false;
            return true;
        }

        /// <summary>Returns the human text of a placeholder ("TODO: Add ingredients" -> "Add ingredients").</summary>
        public static string Strip(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            var v = value.Trim();
            if (v.StartsWith("TODO", StringComparison.OrdinalIgnoreCase))
            {
                v = v.Substring(4).TrimStart(':', ' ', '-');
                return string.IsNullOrEmpty(v) ? fallback : v;
            }
            return v;
        }
    }

    public static class MenuLoader
    {
        public const string DefaultResource = "ARMenu/menu_data";

        public static MenuDatabase Load(string resourcePath)
        {
            var asset = Resources.Load<TextAsset>(string.IsNullOrEmpty(resourcePath) ? DefaultResource : resourcePath);
            if (asset == null)
            {
                Debug.LogError($"[ARMenu] Menu data not found at Resources/{resourcePath}.json");
                return new MenuDatabase();
            }

            MenuDatabase db;
            try
            {
                db = JsonUtility.FromJson<MenuDatabase>(asset.text) ?? new MenuDatabase();
            }
            catch (Exception e)
            {
                Debug.LogError($"[ARMenu] Could not parse {resourcePath}.json: {e.Message}");
                db = new MenuDatabase();
            }

            db.restaurant ??= new RestaurantInfo();
            db.categories ??= Array.Empty<string>();
            db.items ??= Array.Empty<MenuItemData>();
            db.developer ??= new DeveloperInfo();
            db.developer.team ??= Array.Empty<string>();
            db.credits ??= Array.Empty<CreditEntry>();
            foreach (var item in db.items)
            {
                item.ingredients ??= Array.Empty<string>();
                item.allergens ??= Array.Empty<string>();
                if (string.IsNullOrEmpty(item.id)) item.id = item.name;
            }
            return db;
        }

        /// <summary>Copies the nutrition values that already exist in the scene (FoodMenuManager.FoodInfo).</summary>
        public static void MergeSceneNutrition(MenuDatabase db, FoodMenuManager manager)
        {
            if (manager == null) return;
            foreach (var item in db.items)
            {
                if (!item.HasModel) continue;
                var info = manager.GetInfo(item.modelKey);
                if (info == null) continue;
                item.calories = info.calories;
                item.protein = info.protein;
                item.oil = info.oil;
                if (string.IsNullOrWhiteSpace(item.name)) item.name = info.foodName;
            }
        }
    }
}
