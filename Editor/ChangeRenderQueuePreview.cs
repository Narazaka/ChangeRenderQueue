using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace Narazaka.VRChat.ChangeRenderQueue.Editor
{
    public class ChangeRenderQueuePreview : IRenderFilter
    {
        static readonly TogglablePreviewNode EnableNode = TogglablePreviewNode.Create(
            () => "Change Render Queue",
            qualifiedName: "net.narazaka.vrchat.change-render-queue/Preview",
            initialState: true);

        public IEnumerable<TogglablePreviewNode> GetPreviewControlNodes()
        {
            yield return EnableNode;
        }

        public bool IsEnabled(ComputeContext context) => context.Observe(EnableNode.IsEnabled);

        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
        {
            var settings = new Dictionary<Renderer, List<(int MaterialIndex, int RenderQueue)>>();
            foreach (var changeRenderQueue in context.GetComponentsByType<ChangeRenderQueue>())
            {
                var renderer = context.GetComponent<Renderer>(changeRenderQueue.gameObject);
                if (renderer == null) continue;
                if (!settings.TryGetValue(renderer, out var rendererSettings))
                {
                    settings[renderer] = rendererSettings = new List<(int, int)>();
                }
                rendererSettings.Add(context.Observe(changeRenderQueue, c => (c.MaterialIndex, c.RenderQueue)));
            }

            return settings
                .Select(pair => RenderGroup.For(pair.Key).WithData(pair.Value.ToImmutableList()))
                .ToImmutableList();
        }

        public Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
        {
            var settings = group.GetData<ImmutableList<(int MaterialIndex, int RenderQueue)>>();
            var materials = new Dictionary<int, Material>();

            foreach (var (_, proxy) in proxyPairs)
            {
                if (proxy == null) continue;
                var proxyMaterials = proxy.sharedMaterials;
                for (var i = 0; i < proxyMaterials.Length; i++)
                {
                    var material = proxyMaterials[i];
                    if (material == null) continue;
                    if (!RenderQueueResolver.TryResolve(settings, i, out var renderQueue)) continue;
                    if (material.renderQueue == renderQueue) continue;
                    materials[i] = new Material(material)
                    {
                        renderQueue = renderQueue,
                        name = $"{material.name} (RenderQueue {renderQueue})",
                    };
                }
            }

            return Task.FromResult<IRenderFilterNode>(new Node(materials));
        }

        class Node : IRenderFilterNode
        {
            readonly Dictionary<int, Material> Materials;

            public Node(Dictionary<int, Material> materials)
            {
                Materials = materials;
            }

            public RenderAspects WhatChanged => RenderAspects.Material;

            public Task<IRenderFilterNode> Refresh(IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context, RenderAspects updatedAspects)
            {
                // 保持しているのは上流のマテリアルの複製なので、差し替えられたら作り直させる
                if ((updatedAspects & RenderAspects.Material) != 0) return Task.FromResult<IRenderFilterNode>(null);
                return Task.FromResult<IRenderFilterNode>(this);
            }

            public void OnFrame(Renderer original, Renderer proxy)
            {
                if (proxy == null || Materials.Count == 0) return;
                var proxyMaterials = proxy.sharedMaterials;
                var changed = false;
                foreach (var pair in Materials)
                {
                    if (pair.Key >= proxyMaterials.Length) continue;
                    proxyMaterials[pair.Key] = pair.Value;
                    changed = true;
                }
                if (changed) proxy.sharedMaterials = proxyMaterials;
            }

            public void Dispose()
            {
                foreach (var material in Materials.Values) UnityEngine.Object.DestroyImmediate(material);
                Materials.Clear();
            }
        }
    }
}
