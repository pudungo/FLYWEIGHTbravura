using UnityEngine;

public interface IEnemyMoveable
{
    Rigidbody RB { get; set; }


    void MoveEnemy(Vector3 direction);
    float MoveSpeed { get; set; }

}
