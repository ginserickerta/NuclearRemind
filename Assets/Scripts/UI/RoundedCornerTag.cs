using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// Marks an Image whose sprite is the runtime-generated RoundedSprite. Those sprites are not
    /// assets, so when the hierarchy is baked into a prefab (ResearchLabPrefabSetup) the sprite
    /// reference serializes to None and the corner rounding is lost. This tag remembers the radius
    /// and re-applies the sprite the first time the prefab instance wakes up. On a normal
    /// code-built panel the sprite is already set, so Awake is a no-op.
    /// </summary>
    [DisallowMultipleComponent]
    public class RoundedCornerTag : MonoBehaviour
    {
        public int radius = 4;

        private void Awake()
        {
            var img = GetComponent<Image>();
            if (img == null || img.sprite != null) return;
            img.sprite = RoundedSprite.Get(radius);
            img.type = Image.Type.Sliced;
        }
    }
}
