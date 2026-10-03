using UnityEngine;

/// <summary>
/// 바람에 살랑: 밑동은 그대로 두고 위로 갈수록 좌우로 흔들린다 (그림을 통째로 돌리면 뿌리까지 움직여 어색함).
/// 실제 흔들림은 셰이더(OtterKingdom/Sprite Wind Sway)가 하고, 여기서는 그림마다 높이·세기·박자를 정해 준다.
/// 나무·덤불 소품에 AmbienceDirector가 붙인다. 그루마다 박자가 달라 한꺼번에 움직이지 않는다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class IdleSway : MonoBehaviour
{
    private static readonly int SwayAmount = Shader.PropertyToID("_SwayAmount");
    private static readonly int SwayHeight = Shader.PropertyToID("_SwayHeight");
    private static readonly int SwaySpeed = Shader.PropertyToID("_SwaySpeed");
    private static readonly int SwayPhase = Shader.PropertyToID("_SwayPhase");

    /// <param name="material">바람 셰이더 재질</param>
    /// <param name="strength">꼭대기가 밀리는 정도 (그림 높이에 대한 비율, 예: 0.025)</param>
    /// <param name="speed">흔들리는 빠르기 (라디안/초)</param>
    public void Configure(Material material, float strength, float speed)
    {
        var renderer = GetComponent<SpriteRenderer>();
        if (material == null || renderer.sprite == null)
            return;

        renderer.sharedMaterial = material;
        float height = Mathf.Max(0.01f, renderer.sprite.bounds.max.y);
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetFloat(SwayHeight, height);
        block.SetFloat(SwayAmount, height * strength);
        block.SetFloat(SwaySpeed, speed);
        block.SetFloat(SwayPhase, Random.value * Mathf.PI * 2f);
        renderer.SetPropertyBlock(block);
    }
}
