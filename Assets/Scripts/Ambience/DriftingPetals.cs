using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 꽃잎 몇 장이 바람을 타고 화면을 가로질러 하늘하늘 날린다 (광장, 밭). 화면 왼쪽 위 바깥에서 나타나
/// 흔들리고 돌며 오른쪽 아래로 흘러가 사라진다. 카메라 크기에 맞춰 크기·빠르기를 정한다.
/// 조명을 받는 재질이라 밤에는 어둑하게. SettlementSetup.Life가 붙인다.
/// </summary>
public class DriftingPetals : MonoBehaviour
{
    [SerializeField] private Sprite[] _sprites;

    [Tooltip("한꺼번에 날리는 꽃잎 수")]
    [SerializeField] private int _count = 5;

    [Tooltip("꽃잎 크기 (화면 높이 대비)")]
    [SerializeField] private Vector2 _size = new Vector2(0.016f, 0.026f);

    [Tooltip("화면을 가로지르는 시간 (초, 최소~최대)")]
    [SerializeField] private Vector2 _crossSeconds = new Vector2(9f, 15f);

    [SerializeField] private int _sortingOrder = 31960;

    private class Petal
    {
        public SpriteRenderer Renderer;
        public Vector2 ViewPosition; // 화면 비율 (0~1, 왼쪽 아래 0)
        public Vector2 Velocity;     // 화면 비율 / 초
        public float Size;
        public float Phase;
        public float SpinSpeed;
        public float FlipSpeed;
        public float Delay;
    }

    private readonly List<Petal> _petals = new List<Petal>();

    private void Awake()
    {
        for (int i = 0; i < _count; i++)
        {
            var go = new GameObject("Petal");
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _sprites[i % _sprites.Length];
            renderer.sortingOrder = _sortingOrder;
            var petal = new Petal { Renderer = renderer };
            Restart(petal, true);
            _petals.Add(petal);
        }
    }

    // 처음에는 화면 곳곳에서, 그다음부터는 왼쪽·위 바깥에서 들어옴
    private void Restart(Petal petal, bool anywhere)
    {
        petal.ViewPosition = anywhere
            ? new Vector2(Random.value, Random.value)
            : Random.value < 0.5f ? new Vector2(-0.05f, Random.Range(0.3f, 1f)) : new Vector2(Random.Range(-0.1f, 0.7f), 1.05f);
        float seconds = Random.Range(_crossSeconds.x, _crossSeconds.y);
        petal.Velocity = new Vector2(Random.Range(0.8f, 1.2f), -Random.Range(0.55f, 0.9f)) / seconds;
        petal.Size = Random.Range(_size.x, _size.y);
        petal.Phase = Random.value * 10f;
        petal.SpinSpeed = Random.Range(-90f, 90f);
        petal.FlipSpeed = Random.Range(1.5f, 3f);
        petal.Delay = anywhere ? 0f : Random.Range(0f, 4f);
        petal.Renderer.enabled = false;
    }

    private void LateUpdate()
    {
        if (!CameraView.TryGet(out var center, out var size))
            return;
        var bottomLeft = center - size * 0.5f;
        foreach (var petal in _petals)
        {
            if (petal.Delay > 0f)
            {
                petal.Delay -= Time.deltaTime;
                continue;
            }
            petal.ViewPosition += petal.Velocity * Time.deltaTime;
            if (petal.ViewPosition.x > 1.08f || petal.ViewPosition.y < -0.08f)
            {
                Restart(petal, false);
                continue;
            }

            float time = Time.time + petal.Phase;
            // 좌우로 하늘거림
            var view = petal.ViewPosition + new Vector2(Mathf.Sin(time * 1.3f) * 0.012f, Mathf.Sin(time * 0.9f) * 0.006f);
            var t = petal.Renderer.transform;
            t.position = new Vector3(bottomLeft.x + view.x * size.x, bottomLeft.y + view.y * size.y, 0f);
            float world = petal.Size * size.y / Mathf.Max(0.001f, petal.Renderer.sprite.bounds.size.y);
            // 뒤집히며 도는 느낌: 가로 크기를 줄였다 늘림
            t.localScale = new Vector3(world * (0.35f + 0.65f * Mathf.Abs(Mathf.Sin(time * petal.FlipSpeed))), world, 1f);
            t.localRotation = Quaternion.Euler(0f, 0f, time * petal.SpinSpeed);
            petal.Renderer.enabled = true;
        }
    }
}
