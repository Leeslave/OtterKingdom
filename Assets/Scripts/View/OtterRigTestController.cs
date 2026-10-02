using UnityEngine;
using UnityEngine.InputSystem;

// Test driver for the bone-rigged otter in RiggingTestScene (built by
// OtterRigSetup). WASD / arrow keys walk the otter; the Animator picks the
// facing view and Idle/Walk clip from Dir + Moving.
// autoDemo walks a square on its own so the rig can be watched hands-free.
[RequireComponent(typeof(Animator))]
public class OtterRigTestController : MonoBehaviour
{
    // Must match OtterRigSetup's view order.
    private enum Dir { Down = 0, Right = 1, Left = 2, Up = 3 }

    private static readonly int DirHash = Animator.StringToHash("Dir");
    private static readonly int MovingHash = Animator.StringToHash("Moving");

    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private bool autoDemo;
    [SerializeField] private float autoDemoLegSeconds = 1.5f;

    private static readonly Vector2[] DemoPath = { Vector2.right, Vector2.up, Vector2.left, Vector2.down };

    private Animator _animator;
    private float _demoTimer;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void Update()
    {
        Vector2 input = ReadInput();
        bool moving = input.sqrMagnitude > 0.01f;

        if (moving)
        {
            transform.position += (Vector3)(input.normalized * (moveSpeed * Time.deltaTime));
            _animator.SetInteger(DirHash, (int)ToDir(input));
        }
        _animator.SetBool(MovingHash, moving);
    }

    private Vector2 ReadInput()
    {
        var keyboard = Keyboard.current;
        var v = Vector2.zero;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) v.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) v.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) v.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) v.y += 1f;
        }
        if (v != Vector2.zero || !autoDemo) return v;

        _demoTimer += Time.deltaTime;
        int leg = (int)(_demoTimer / autoDemoLegSeconds) % (DemoPath.Length * 2);
        // Every other leg stands still so Idle gets shown too.
        return leg % 2 == 0 ? DemoPath[leg / 2] : Vector2.zero;
    }

    private static Dir ToDir(Vector2 v)
    {
        if (Mathf.Abs(v.x) > Mathf.Abs(v.y)) return v.x > 0f ? Dir.Right : Dir.Left;
        return v.y > 0f ? Dir.Up : Dir.Down;
    }
}
