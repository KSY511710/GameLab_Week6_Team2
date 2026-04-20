using UnityEngine;

public class LoopingCameraMover : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private Vector3 startPosition = new Vector3(1f, 14f, -8f);
    [SerializeField] private Vector3 endPosition = new Vector3(-16.5f, 9.6f, -8f);
    [SerializeField, Min(0.01f)] private float moveSpeed = 1f;

    private void Start()
    {
        transform.position = startPosition;
    }

    private void Update()
    {
        Vector3 nextPosition = Vector3.MoveTowards(
            transform.position,
            endPosition,
            moveSpeed * Time.deltaTime);

        if (nextPosition == endPosition)
        {
            transform.position = startPosition;
            return;
        }

        transform.position = nextPosition;
    }
}
