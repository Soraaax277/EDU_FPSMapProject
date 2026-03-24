using UnityEngine;

/// <summary>
/// Moves a camera to follow a target without being parented to it.
/// 
/// Two modes:
///   useWorldOffset = false (default) — local-space offset, rotates with target (FPS cam).
///   useWorldOffset = true            — world-space offset, stays fixed relative to world
///                                      regardless of target rotation (overhead spectator cam).
/// </summary>
public class SimpleCameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3   offset;
    public bool      useWorldOffset = false;  // true = overhead spectator cam
    public bool      lookAtTarget   = false;  // true = camera always faces the target

    private Vector3    _lastPos;
    private Quaternion _lastRot;

    void Awake()
    {
        _lastPos = transform.position;
        _lastRot = transform.rotation;
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (target.gameObject.activeInHierarchy)
        {
            if (useWorldOffset)
            {
                // World-space: camera is always exactly "offset" above/behind in world coords
                _lastPos = target.position + offset;

                if (lookAtTarget)
                {
                    Vector3 dir = (target.position + Vector3.up * 1f) - _lastPos;
                    if (dir.sqrMagnitude > 0.001f)
                        _lastRot = Quaternion.LookRotation(dir.normalized);
                }
            }
            else
            {
                // Local-space: offset rotates with the target (FPS behind-camera)
                _lastPos = target.position + target.TransformDirection(offset);
                _lastRot = target.rotation;
            }
        }
        // Freeze at last known good position when target is inactive (dead)
        transform.position = _lastPos;
        transform.rotation = _lastRot;
    }
}
