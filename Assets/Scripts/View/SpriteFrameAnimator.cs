using System;
using UnityEngine;

// Minimal code-driven sprite animation: named clips of frames, played by name.
// Used instead of an Animator for the fishing otter, whose fishing loop is a
// strict chain of one-shot clips (cast -> wait -> bite -> pull -> reaction)
// that the controller steps through by checking IsFinished — no controller
// graph, triggers or "back in Idle yet?" polling to keep in sync.
// Clips are filled in by FishingSceneSetup; fps/loop can be tuned per clip
// in the Inspector.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFrameAnimator : MonoBehaviour
{
    [Serializable]
    public class Clip
    {
        public string name;
        public Sprite[] frames;
        [Min(0.1f)] public float fps = 10f;
        public bool loop = true;
    }

    [SerializeField] private Clip[] clips;
    [Tooltip("Global playback speed multiplier.")]
    [SerializeField, Min(0.01f)] private float speed = 1f;

    private SpriteRenderer spriteRenderer;
    private Clip current;
    private float time;

    public string CurrentClipName => current != null ? current.name : null;

    // True once a non-looping clip has shown its last frame for a full frame
    // duration. Always false for looping clips.
    public bool IsFinished => current != null && !current.loop && time * current.fps >= current.frames.Length;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // Replaying the clip that is already playing keeps its progress unless
    // restart is set, so this can be called every frame (e.g. per walk step).
    public void Play(string clipName, bool restart = false)
    {
        if (!restart && current != null && current.name == clipName) return;

        var clip = Array.Find(clips, c => c.name == clipName);
        if (clip == null || clip.frames == null || clip.frames.Length == 0)
        {
            Debug.LogWarning($"[{nameof(SpriteFrameAnimator)}] Missing clip '{clipName}' on {name}.", this);
            return;
        }

        current = clip;
        time = 0f;
        ApplyFrame();
    }

    private void Update()
    {
        if (current == null) return;
        time += Time.deltaTime * speed;
        ApplyFrame();
    }

    private void ApplyFrame()
    {
        int count = current.frames.Length;
        int index = Mathf.FloorToInt(time * current.fps);
        index = current.loop ? index % count : Mathf.Min(index, count - 1);
        spriteRenderer.sprite = current.frames[index];
    }
}
