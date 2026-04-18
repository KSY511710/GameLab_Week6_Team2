using System;
using System.Collections.Generic;
using Special.Data;
using Special.Effects;
using UnityEngine;

namespace Special.Runtime
{
    /// <summary>
    /// 게임 전체의 특수 블럭 설치 상태를 추적. 설치 허용 여부 질의와 activate/deactivate 수명 관리.
    /// MonoBehaviour 지만 씬 부트스트랩 편의를 위해 정적 Instance 에서 lazy-create 한다.
    /// </summary>
    public class SpecialBlockRegistry : MonoBehaviour
    {
        private static SpecialBlockRegistry _instance;
        public static SpecialBlockRegistry Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = FindFirstObjectByType<SpecialBlockRegistry>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("[SpecialBlockRegistry]");
                    _instance = go.AddComponent<SpecialBlockRegistry>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private readonly Dictionary<string, int> installCountByDefId = new();
        private readonly Dictionary<(string defId, int zoneId), int> installCountByZone = new();
        private readonly List<SpecialBlockInstance> installed = new();
        private int nextInstanceId = 1;

        public event Action<SpecialBlockInstance> OnSpecialPlaced;
        public event Action<SpecialBlockInstance> OnSpecialRemoved;

        public IReadOnlyList<SpecialBlockInstance> Installed => installed;

        public bool CanPlace(SpecialBlockDefinition def, int zoneId)
        {
            if (def == null || string.IsNullOrEmpty(def.id)) return false;

            if (installCountByDefId.TryGetValue(def.id, out int total) && total >= def.maxPerGame) return false;

            var zoneKey = (def.id, zoneId);
            if (installCountByZone.TryGetValue(zoneKey, out int zoneCount) && zoneCount >= def.maxPerZone) return false;

            return true;
        }

        public SpecialBlockInstance RegisterPlacement(SpecialBlockDefinition def, Vector2Int anchor, IReadOnlyList<Vector2Int> footprint, int zoneId)
        {
            SpecialBlockInstance instance = new SpecialBlockInstance(nextInstanceId++, def, anchor, footprint, zoneId);
            installed.Add(instance);
            installCountByDefId[def.id] = installCountByDefId.TryGetValue(def.id, out int c) ? c + 1 : 1;
            var zoneKey = (def.id, zoneId);
            installCountByZone[zoneKey] = installCountByZone.TryGetValue(zoneKey, out int zc) ? zc + 1 : 1;

            ActivateEffects(instance);

            OnSpecialPlaced?.Invoke(instance);
            return instance;
        }

        public void Unregister(SpecialBlockInstance instance)
        {
            if (instance == null) return;

            DeactivateEffects(instance);

            installed.Remove(instance);
            if (installCountByDefId.TryGetValue(instance.definition.id, out int c)) installCountByDefId[instance.definition.id] = Mathf.Max(0, c - 1);
            var zoneKey = (instance.definition.id, instance.zoneId);
            if (installCountByZone.TryGetValue(zoneKey, out int zc)) installCountByZone[zoneKey] = Mathf.Max(0, zc - 1);

            OnSpecialRemoved?.Invoke(instance);
        }

        public SpecialBlockInstance FindByFootprintCell(Vector2Int cell)
        {
            for (int i = 0; i < installed.Count; i++)
                if (installed[i].FootprintContains(cell)) return installed[i];
            return null;
        }

        private void ActivateEffects(SpecialBlockInstance owner)
        {
            SpecialBlockDefinition def = owner.definition;
            EffectRuntime runtime = EffectRuntime.Instance;

            if (def.effectAssets != null)
            {
                foreach (var asset in def.effectAssets)
                {
                    if (asset == null) continue;
                    owner.AddEffect(asset);
                    asset.Activate(owner, runtime);
                }
            }

            if (def.customEffectPrefabs != null)
            {
                foreach (var prefab in def.customEffectPrefabs)
                {
                    if (prefab == null) continue;
                    GameObject go = Instantiate(prefab, transform);
                    go.name = $"{def.id}#{owner.instanceId}_CustomFx";
                    CustomEffectBehaviour[] behaviours = go.GetComponents<CustomEffectBehaviour>();
                    foreach (var b in behaviours)
                    {
                        owner.AddEffect(b);
                        b.Activate(owner, runtime);
                    }
                }
            }
        }

        private void DeactivateEffects(SpecialBlockInstance owner)
        {
            EffectRuntime runtime = EffectRuntime.Instance;
            foreach (var eff in owner.EffectInstances)
            {
                try { eff.Deactivate(owner, runtime); } catch (Exception e) { Debug.LogException(e); }
            }
            runtime.UnhookAll(owner);
            owner.ClearEffects();
        }
    }
}
