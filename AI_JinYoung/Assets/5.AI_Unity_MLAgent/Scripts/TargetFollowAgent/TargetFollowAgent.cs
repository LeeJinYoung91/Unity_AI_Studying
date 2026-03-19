using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class TargetFollowAgent : Agent
{
    Rigidbody rBody;
    public Transform Target;
    public Transform[] Walls;

    public float spawnRadius = 4f;

    public float forceMultiplier = 10;

    void Start()
    {
        rBody = GetComponent<Rigidbody>();
    }

    public override void OnEpisodeBegin()
    {
        this.rBody.angularVelocity = Vector3.zero;
        this.rBody.linearVelocity = Vector3.zero;
        if (this.transform.localPosition.y < 0)
        {
            this.transform.localPosition = new Vector3(0, 0.5f, 0);
        }

        bool isValidPosition = false;
        Vector3 randomPos = Vector3.zero;
        int tryCount = 0;

        // 합격할 때까지 찾되, 100번 넘게 시도하면 쿨하게 포기 (무한루프 방지)
        while (!isValidPosition && tryCount < 100)
        {
            tryCount++;

            float rx = Random.Range(-spawnRadius, spawnRadius);
            float rz = Random.Range(-spawnRadius, spawnRadius);
            randomPos = new Vector3(rx, 0.5f, rz);
            isValidPosition = true;

            if (Vector3.Distance(randomPos, this.transform.localPosition) < 1.5f)
                isValidPosition = false;

            foreach (Transform wall in Walls)
            {
                if (Vector3.Distance(randomPos, wall.localPosition) < 1.5f)
                {
                    isValidPosition = false;
                    break;
                }
            }
        }

        if (!isValidPosition)
        {
            randomPos = new Vector3(0, 0.5f, 0);
        }

        Target.localPosition = randomPos;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(Target.localPosition); // 타겟의 위치 (3개 값: x,y,z)
        sensor.AddObservation(this.transform.localPosition); // 내 위치 (3개 값: x,y,z)
        sensor.AddObservation(rBody.linearVelocity.x); // 내 속도 x (1개 값)
        sensor.AddObservation(rBody.linearVelocity.z); // 내 속도 z (1개 값)
        // 관찰값 총합 = 3 + 3 + 1 + 1 = 8개
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        AddReward(-0.001f);

        Vector3 controlSignal = Vector3.zero;
        controlSignal.x = actionBuffers.ContinuousActions[0];
        controlSignal.z = actionBuffers.ContinuousActions[1];
        rBody.AddForce(controlSignal * forceMultiplier);

        float distanceToTarget = Vector3.Distance(this.transform.localPosition, Target.localPosition);

        if (distanceToTarget < 1.42f)
        {
            SetReward(1.0f);
            EndEpisode();
        }
        else if (this.transform.localPosition.y < 0)
        {
            SetReward(-1.0f);
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = Input.GetAxis("Horizontal");
        continuousActionsOut[1] = Input.GetAxis("Vertical");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            SetReward(-1.0f);
            EndEpisode();
        }
    }
}