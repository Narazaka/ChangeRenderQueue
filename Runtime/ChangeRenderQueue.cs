using UnityEngine;
using VRC.SDKBase;

namespace Narazaka.VRChat.ChangeRenderQueue
{
    [AddComponentMenu("ChangeRenderQueue")]
    [RequireComponent(typeof(Renderer))]
    public class ChangeRenderQueue : MonoBehaviour, IEditorOnly
    {
        public int RenderQueue = 2460;
        public int MaterialIndex = -1;
    }
}
