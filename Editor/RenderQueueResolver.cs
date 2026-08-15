using System.Collections.Generic;

namespace Narazaka.VRChat.ChangeRenderQueue.Editor
{
    internal static class RenderQueueResolver
    {
        /// <summary>
        /// 1つのレンダラーに付いた ChangeRenderQueue 群から、指定スロットの実効 Render Queue を求める。
        /// スロット指定 (MaterialIndex &gt;= 0) が wildcard (MaterialIndex == -1) に優先し、
        /// 同じ区分の中では後に定義されたコンポーネントが優先される (Modular Avatar の慣習に揃える)。
        /// </summary>
        /// <param name="settings">1つのレンダラー上の ChangeRenderQueue の値を、コンポーネントの並び順に並べたもの</param>
        public static bool TryResolve(IEnumerable<(int MaterialIndex, int RenderQueue)> settings, int slotIndex, out int renderQueue)
        {
            renderQueue = 0;
            var specificFound = false;
            var wildcardQueue = 0;
            var wildcardFound = false;
            foreach (var setting in settings)
            {
                if (setting.MaterialIndex == slotIndex)
                {
                    renderQueue = setting.RenderQueue;
                    specificFound = true;
                }
                else if (setting.MaterialIndex == -1)
                {
                    wildcardQueue = setting.RenderQueue;
                    wildcardFound = true;
                }
            }
            if (specificFound) return true;
            if (wildcardFound) renderQueue = wildcardQueue;
            return wildcardFound;
        }
    }
}
