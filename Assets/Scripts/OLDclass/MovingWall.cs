using UnityEngine;

public class MovingWall : MonoBehaviour
{
    public Transform wall;
    public Transform pointA;
    public Transform pointB;
    public float speed = 20f;

    private Transform targetPoint;

    private void Start()
    {
        // Pick whichever point is closer as the starting target
        float distA = Vector3.Distance(wall.position, pointA.position);
        float distB = Vector3.Distance(wall.position, pointB.position);

        targetPoint = distA < distB ? pointB : pointA;
    }

    private void Update()
    {
        wall.position = Vector3.MoveTowards(
            wall.position,
            targetPoint.position,
            speed * Time.deltaTime
        );

        // Use a slightly larger threshold to guarantee switching
        if (Vector3.Distance(wall.position, targetPoint.position) < 1f)
        {
            targetPoint = (targetPoint == pointA) ? pointB : pointA;
        }
    }
}

