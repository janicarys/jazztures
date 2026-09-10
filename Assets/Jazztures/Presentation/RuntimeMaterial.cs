using UnityEngine;
using UnityEngine.Rendering;

namespace Jazztures.Presentation
{
    /// <summary>
    /// A translucent unlit material for runtime-generated presentation geometry — selector
    /// plates, the exit button, pointer rays. Prefers <c>Sprites/Default</c>: it is in the
    /// project's always-included shaders (so it survives a device build) and honours vertex
    /// colour, so the same material works for a mesh and a <see cref="LineRenderer"/>.
    /// </summary>
    internal static class RuntimeMaterial
    {
        public static Material Unlit(string name)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            return new Material(shader)
            {
                name = name,
                renderQueue = (int)RenderQueue.Transparent,
            };
        }
    }
}
