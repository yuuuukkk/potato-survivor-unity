using UnityEngine;

namespace RogueLike.Core
{
    /// <summary>
    /// 素材预制加载：所有需要素材的对象统一从这里加载 Resources/Prefabs/ 下的同名预制。
    /// 用户替换素材 = 直接编辑预制里的 SpriteRenderer（或 Image）并保存，游戏立即生效；
    /// 未提供预制时回退运行时构建的占位色块（开箱即玩，无需任何资源）。
    /// 预制一键生成：Unity 菜单「土豆幸存者 / 生成占位预制」。
    /// </summary>
    public static class PrefabProvider
    {
        public static GameObject Load(string key) => Resources.Load<GameObject>("Prefabs/" + key);

        /// <summary>实例化预制（挂到 parent 下）；无预制返回 null，由调用方回退占位构建。</summary>
        public static GameObject Instantiate(string key, Transform parent)
        {
            var prefab = Load(key);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab);
            go.name = prefab.name;
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }
    }
}
