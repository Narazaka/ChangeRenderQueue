using nadena.dev.ndmf;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

[assembly: ExportsPlugin(typeof(Narazaka.VRChat.ChangeRenderQueue.Editor.ChangeRenderQueuePlugin))]

namespace Narazaka.VRChat.ChangeRenderQueue.Editor
{
    public class ChangeRenderQueuePlugin : Plugin<ChangeRenderQueuePlugin>
    {
        public override string DisplayName => nameof(ChangeRenderQueuePlugin);
        public override string QualifiedName => "net.narazaka.vrchat.change-render-queue";
        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming).AfterPlugin("nadena.dev.modular-avatar").Run(DisplayName, ctx =>
            {
                var changeRenderQueues = ctx.AvatarRootTransform.GetComponentsInChildren<ChangeRenderQueue>(true);
                if (changeRenderQueues.Length == 0) return;

                var rendererAnalyzer = new RendererAnalyzer(ctx.AvatarRootTransform);
                foreach (var changeRenderQueue in changeRenderQueues)
                {
                    var renderer = changeRenderQueue.GetComponent<Renderer>();
                    if (renderer == null) continue;
                    var materials = renderer.sharedMaterials;
                    if (changeRenderQueue.MaterialIndex < 0)
                    {
                        for (int i = 0; i < materials.Length; i++)
                        {
                            rendererAnalyzer.AddMaterialSlot(renderer, i, changeRenderQueue.RenderQueue);
                        }
                    }
                    else if (changeRenderQueue.MaterialIndex < materials.Length)
                    {
                        rendererAnalyzer.AddMaterialSlot(renderer, changeRenderQueue.MaterialIndex, changeRenderQueue.RenderQueue);
                    }
                    Object.DestroyImmediate(changeRenderQueue);
                }

                var animatorAnalyzer = rendererAnalyzer.AnimatorAnalyzer();
                animatorAnalyzer.Process(ctx.AvatarDescriptor);
                var materialGenerator = animatorAnalyzer.MaterialGenerator();
                materialGenerator.GenerateAndReplace(ctx.AvatarDescriptor);
            });
        }

        class RendererAnalyzer
        {
            Dictionary<MaterialSlot, MaterialSlotInfo> MaterialInfo = new Dictionary<MaterialSlot, MaterialSlotInfo>();
            Transform Avatar;
            public RendererAnalyzer(Transform avatar)
            {
                Avatar = avatar;
            }

            public void AddMaterialSlot(Renderer renderer, int slotIndex, int renderQueue)
            {
                var materials = renderer.sharedMaterials;
                var material = materials[slotIndex];
                if (material == null) return;

                var key = new MaterialSlot
                {
                    Renderer = renderer,
                    RendererType = renderer.GetType(),
                    RendererPath = RelativePath(Avatar, renderer.transform),
                    MaterialIndex = slotIndex,
                };
                if (!MaterialInfo.TryGetValue(key, out var materialSlots))
                {
                    MaterialInfo[key] = new MaterialSlotInfo(renderQueue);
                }
                MaterialInfo[key].AddMaterial(material);
            }

            public AnimatorAnalyzer AnimatorAnalyzer()
            {
                var editorCurveBindings = new Dictionary<EditorCurveBindingPartial, MaterialSlot>();
                foreach (var key in MaterialInfo.Keys)
                {
                    foreach (var binding in key.EditorCurveBindings())
                    {
                        Debug.Log($"make binding: {binding.type} [{binding.path}] [{binding.propertyName}]");
                        editorCurveBindings[binding] = key;
                    }
                }
                return new AnimatorAnalyzer(MaterialInfo, editorCurveBindings);
            }

            string RelativePath(Transform root, Transform target)
            {
                var paths = new List<string>();
                var current = target;
                while (current != root)
                {
                    paths.Add(current.name);
                    current = current.parent;
                }
                paths.Reverse();
                return string.Join("/", paths);
            }
        }

        class AnimatorAnalyzer
        {
            Dictionary<MaterialSlot, MaterialSlotInfo> MaterialInfo;
            Dictionary<EditorCurveBindingPartial, MaterialSlot> EditorCurveBindings;

            public AnimatorAnalyzer(Dictionary<MaterialSlot, MaterialSlotInfo> materialInfo, Dictionary<EditorCurveBindingPartial, MaterialSlot> editorCurveBindings)
            {
                MaterialInfo = materialInfo;
                EditorCurveBindings = editorCurveBindings;
            }

            public void Process(VRCAvatarDescriptor avatar)
            {
                foreach (var layer in avatar.baseAnimationLayers)
                {
                    ProcessLayer(layer);
                }
                foreach (var layer in avatar.specialAnimationLayers)
                {
                    ProcessLayer(layer);
                }
            }

            void ProcessLayer(VRCAvatarDescriptor.CustomAnimLayer animLayer)
            {
                Debug.Log($"layer: {animLayer.isDefault} {animLayer.animatorController}");
                if (animLayer.isDefault || animLayer.animatorController == null) return;

                var animator = animLayer.animatorController;
                foreach (var clip in animator.animationClips)
                {
                    Debug.Log($"clip: {clip.name}");
                    ProcessClip(clip);
                }
            }

            void ProcessClip(AnimationClip clip)
            {
                var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                foreach (var binding in bindings)
                {
                    Debug.Log($"binding: {binding.type} [{binding.path}] [{binding.propertyName}]");
                    if (EditorCurveBindings.TryGetValue(EditorCurveBindingPartial.From(binding), out var materialSlot))
                    {
                        Debug.Log($"found binding MaterialSlot: {materialSlot.RendererType} [{materialSlot.RendererPath}] [{materialSlot.MaterialIndex}]");
                        var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                        foreach (var keyframe in keyframes)
                        {
                            if (keyframe.value is Material material)
                            {
                                MaterialInfo[materialSlot].AddMaterial(material);
                            }
                        }
                    }
                }
            }

            public MaterialGenerator MaterialGenerator()
            {
                return new MaterialGenerator(MaterialInfo, EditorCurveBindings);
            }
        }

        class MaterialGenerator
        {
            Dictionary<MaterialSlot, MaterialSlotInfo> MaterialInfo;
            Dictionary<EditorCurveBindingPartial, MaterialSlot> EditorCurveBindings;
            Dictionary<int, HashSet<Material>> Materials = new Dictionary<int, HashSet<Material>>();
            Dictionary<(Material, int), Material> ReplacedMaterials = new Dictionary<(Material, int), Material>();

            public MaterialGenerator(Dictionary<MaterialSlot, MaterialSlotInfo> materialInfo, Dictionary<EditorCurveBindingPartial, MaterialSlot> editorCurveBindings)
            {
                MaterialInfo = materialInfo;
                EditorCurveBindings = editorCurveBindings;

                foreach (var materialSlot in MaterialInfo.Keys)
                {
                    var materialSlotInfo = MaterialInfo[materialSlot];
                    if (!Materials.TryGetValue(materialSlotInfo.RenderQueue, out var materials))
                    {
                        Materials[materialSlotInfo.RenderQueue] = materials = new HashSet<Material>();
                    }
                    materials.UnionWith(materialSlotInfo.GetMaterials());
                }
            }

            public void GenerateAndReplace(VRCAvatarDescriptor avatar)
            {
                GenerateMaterials();
                ReplaceRendererMaterials();
                ProcessLayers(avatar);

                // debug
                foreach (var materialSlot in MaterialInfo.Keys)
                {
                    Debug.Log($"MaterialSlot: {materialSlot.RendererType} [{materialSlot.RendererPath}] [{materialSlot.MaterialIndex}]");
                    foreach (var material in MaterialInfo[materialSlot].GetMaterials())
                    {
                        Debug.Log($"  Material: {material}");
                    }
                }
                foreach (var (material, renderQueue) in ReplacedMaterials.Keys)
                {
                    Debug.Log($"ReplacedMaterial: {material} [{renderQueue}] => {ReplacedMaterials[(material, renderQueue)]}");
                }
            }

            void GenerateMaterials()
            {
                foreach (var renderQueue in Materials.Keys)
                {
                    var materials = Materials[renderQueue];
                    foreach (var material in materials)
                    {
                        var newMaterial = new Material(material);
                        newMaterial.renderQueue = renderQueue;
                        ObjectRegistry.RegisterReplacedObject(material, newMaterial);
                        ReplacedMaterials[(material, renderQueue)] = newMaterial;
                    }
                }
            }

            void ReplaceRendererMaterials()
            {
                foreach (var materialSlot in MaterialInfo.Keys)
                {
                    var materialSlotInfo = MaterialInfo[materialSlot];
                    var materials = materialSlotInfo.GetMaterials();
                    foreach (var material in materials)
                    {
                        if (ReplacedMaterials.TryGetValue((material, materialSlotInfo.RenderQueue), out var newMaterial))
                        {
                            materialSlot.Renderer.sharedMaterials[materialSlot.MaterialIndex] = newMaterial;
                        }
                    }
                }
            }

            void ProcessLayers(VRCAvatarDescriptor avatar)
            {
                foreach (var layer in avatar.baseAnimationLayers)
                {
                    ProcessLayer(layer);
                }
                foreach (var layer in avatar.specialAnimationLayers)
                {
                    ProcessLayer(layer);
                }
            }

            void ProcessLayer(VRCAvatarDescriptor.CustomAnimLayer animLayer)
            {
                if (animLayer.isDefault || animLayer.animatorController == null) return;

                var animator = animLayer.animatorController;
                foreach (var clip in animator.animationClips)
                {
                    ProcessClip(clip);
                }
            }

            void ProcessClip(AnimationClip clip)
            {
                var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                foreach (var binding in bindings)
                {
                    if (EditorCurveBindings.TryGetValue(EditorCurveBindingPartial.From(binding), out var materialSlot))
                    {
                        var materialSlotInfo = MaterialInfo[materialSlot];
                        var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                        for (var i = 0; i < keyframes.Length; ++i)
                        {
                            var keyframe = keyframes[i];
                            if (keyframe.value is Material material)
                            {
                                if (ReplacedMaterials.TryGetValue((material, materialSlotInfo.RenderQueue), out var newMaterial))
                                {
                                    keyframes[i] = new ObjectReferenceKeyframe
                                    {
                                        time = keyframe.time,
                                        value = newMaterial,
                                    };
                                }
                            }
                        }
                        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
                    }
                }
            }
        }

        struct MaterialSlot
        {
            public Renderer Renderer;
            public System.Type RendererType;
            public string RendererPath;
            public int MaterialIndex;

            string PropertyName => $"m_Materials.Array.data[{MaterialIndex}]";

            public EditorCurveBindingPartial[] EditorCurveBindings()
            {
                var original = OriginalEditorCurveBindings();
                var generic = original.Select(binding => new EditorCurveBindingPartial
                {
                    type = typeof(Renderer),
                    path = binding.path,
                    propertyName = binding.propertyName,
                }).ToArray();
                return original.Concat(generic).ToArray();
            }

            EditorCurveBindingPartial[] OriginalEditorCurveBindings()
            {
                return new EditorCurveBindingPartial[]
                {
                    EditorCurveBinding(),
                    FileIdEditorCurveBinding(),
                    PathIdEditorCurveBinding(),
                };
            }

            public EditorCurveBindingPartial EditorCurveBinding() {
                return new EditorCurveBindingPartial
                {
                    type = RendererType,
                    path = RendererPath,
                    propertyName = PropertyName,
                };
            }

            public EditorCurveBindingPartial FileIdEditorCurveBinding() {
                return new EditorCurveBindingPartial
                {
                    type = RendererType,
                    path = RendererPath,
                    propertyName = PropertyName + ".m_FileID",
                };
            }

            public EditorCurveBindingPartial PathIdEditorCurveBinding() {
                return new EditorCurveBindingPartial
                {
                    type = RendererType,
                    path = RendererPath,
                    propertyName = PropertyName + ".m_PathID",
                };
            }
        }

        struct EditorCurveBindingPartial : System.IEquatable<EditorCurveBindingPartial>
        {
            public static EditorCurveBindingPartial From(EditorCurveBinding binding)
            {
                return new EditorCurveBindingPartial
                {
                    type = binding.type,
                    path = binding.path,
                    propertyName = binding.propertyName,
                };
            }

            public System.Type type;
            public string path;
            public string propertyName;

            public bool Equals(EditorCurveBindingPartial other)
            {
                return type == other.type && path == other.path && propertyName == other.propertyName;
            }

            public override bool Equals(object obj)
            {
                return obj is EditorCurveBindingPartial other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (type, path, propertyName).GetHashCode();
            }

            public static bool operator ==(EditorCurveBindingPartial left, EditorCurveBindingPartial right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(EditorCurveBindingPartial left, EditorCurveBindingPartial right)
            {
                return !left.Equals(right);
            }
        }

        class MaterialSlotInfo
        {
            public readonly int RenderQueue;
            HashSet<Material> Materials = new HashSet<Material>();

            public MaterialSlotInfo(int renderQueue)
            {
                RenderQueue = renderQueue;
            }

            public void AddMaterial(Material material) => Materials.Add(material);

            public HashSet<Material> GetMaterials() => Materials;
        }
    }
}
