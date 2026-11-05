using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ARMenu
{
    /// <summary>
    /// Local cart + favourites (functional prototype, no backend).
    /// State is saved on the device with PlayerPrefs so it survives app restarts.
    /// </summary>
    public class OrderService
    {
        public const int MaxQuantity = 20;
        const string CartKey = "armenu.cart";
        const string FavKey = "armenu.favourites";

        readonly Dictionary<string, int> mCart = new Dictionary<string, int>();
        readonly List<string> mCartOrder = new List<string>();
        readonly HashSet<string> mFavourites = new HashSet<string>();
        readonly bool mPersist;

        public event Action CartChanged;
        public event Action<string> FavouriteChanged;

        public OrderService(bool persist, Func<string, bool> isValidId = null)
        {
            mPersist = persist;
            if (!persist) return;
            Load(isValidId);
        }

        // ------------------------------------------------------------------ cart

        public int TotalUnits => mCart.Values.Sum();
        public int LineCount => mCart.Count;
        public IReadOnlyList<string> Lines => mCartOrder;
        public int QuantityOf(string id) => id != null && mCart.TryGetValue(id, out var q) ? q : 0;

        public void Add(string id, int quantity)
        {
            if (string.IsNullOrEmpty(id) || quantity <= 0) return;
            SetQuantity(id, QuantityOf(id) + quantity);
        }

        public void SetQuantity(string id, int quantity)
        {
            if (string.IsNullOrEmpty(id)) return;
            quantity = Mathf.Clamp(quantity, 0, MaxQuantity);
            if (quantity == 0)
            {
                if (!mCart.Remove(id)) return;
                mCartOrder.Remove(id);
            }
            else
            {
                if (!mCart.ContainsKey(id)) mCartOrder.Add(id);
                else if (mCart[id] == quantity) return;
                mCart[id] = quantity;
            }
            Save();
            CartChanged?.Invoke();
        }

        public void Clear()
        {
            if (mCart.Count == 0) return;
            mCart.Clear();
            mCartOrder.Clear();
            Save();
            CartChanged?.Invoke();
        }

        public float Total(Func<string, float> priceOf, out bool hasUnpriced)
        {
            hasUnpriced = false;
            var total = 0f;
            foreach (var kv in mCart)
            {
                var p = priceOf(kv.Key);
                if (p <= 0f) hasUnpriced = true;
                else total += p * kv.Value;
            }
            return total;
        }

        // ------------------------------------------------------------------ favourites

        public bool IsFavourite(string id) => id != null && mFavourites.Contains(id);
        public int FavouriteCount => mFavourites.Count;

        public bool ToggleFavourite(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            var now = !mFavourites.Remove(id);
            if (now) mFavourites.Add(id);
            Save();
            FavouriteChanged?.Invoke(id);
            return now;
        }

        // ------------------------------------------------------------------ persistence

        void Save()
        {
            if (!mPersist) return;
            PlayerPrefs.SetString(CartKey, string.Join(",", mCartOrder.Select(id => id + ":" + mCart[id])));
            PlayerPrefs.SetString(FavKey, string.Join(",", mFavourites));
            PlayerPrefs.Save();
        }

        void Load(Func<string, bool> isValidId)
        {
            foreach (var entry in PlayerPrefs.GetString(CartKey, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[1], out var q) || q <= 0) continue;
                if (isValidId != null && !isValidId(parts[0])) continue;
                if (mCart.ContainsKey(parts[0])) continue;
                mCart[parts[0]] = Mathf.Min(q, MaxQuantity);
                mCartOrder.Add(parts[0]);
            }
            foreach (var id in PlayerPrefs.GetString(FavKey, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                if (isValidId == null || isValidId(id)) mFavourites.Add(id);
        }
    }
}
