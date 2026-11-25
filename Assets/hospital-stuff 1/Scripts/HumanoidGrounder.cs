using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class HumanoidGrounder : MonoBehaviour
{
    public LayerMask groundMask = -1;
    public float rayStartHeight = 0.3f;
    public float rayLength = 0.6f;
    public float verticalOffset = 0f;
    public bool ignoreWhenOnNavMesh = true;

    private Animator animator;
    private NavMeshAgent agent;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
    }

    private void LateUpdate()
    {
        if (ignoreWhenOnNavMesh && agent != null && agent.enabled && agent.isOnNavMesh)
            return;
        Vector3 origin = transform.position + Vector3.up * rayStartHeight;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayStartHeight + rayLength, groundMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 pos = transform.position;
            float targetY = hit.point.y + verticalOffset;
            if (Mathf.Abs(pos.y - targetY) > 0.001f)
            {
                pos.y = targetY;
                transform.position = pos;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 origin = transform.position + Vector3.up * rayStartHeight;
        Gizmos.DrawLine(origin, origin + Vector3.down * (rayStartHeight + rayLength));
        Gizmos.DrawSphere(origin + Vector3.down * (rayStartHeight + rayLength), 0.02f);
    }
}
