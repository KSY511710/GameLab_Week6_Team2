using System;
using UnityEngine;

namespace Special.Data
{
    [CreateAssetMenu(menuName = "Special/Draw Table", fileName = "SpecialDrawTable")]
    public class SpecialDrawTable : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public SpecialBlockDefinition definition;
            [Min(0f)] public float weight = 1f;
            public bool isEnabled = true;
        }

        public Entry[] entries;

        public SpecialBlockDefinition RollRandom()
        {
            if (entries == null || entries.Length == 0) return null;

            float totalWeight = 0f;
            for (int i = 0; i < entries.Length; i++)
            {
                Entry e = entries[i];
                if (e == null || !e.isEnabled || e.definition == null) continue;
                totalWeight += Mathf.Max(0f, e.weight);
            }
            if (totalWeight <= 0f) return null;

            float roll = UnityEngine.Random.value * totalWeight;
            float accum = 0f;
            for (int i = 0; i < entries.Length; i++)
            {
                Entry e = entries[i];
                if (e == null || !e.isEnabled || e.definition == null) continue;
                accum += Mathf.Max(0f, e.weight);
                if (roll <= accum) return e.definition;
            }
            return null;
        }
    }
}
