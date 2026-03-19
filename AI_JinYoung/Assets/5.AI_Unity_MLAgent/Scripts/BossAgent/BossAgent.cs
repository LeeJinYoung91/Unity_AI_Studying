using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class BossAgent : CharacterAgent
{
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        var discreteActionsOut = actionsOut.DiscreteActions;

        continuousActionsOut[0] = Input.GetAxisRaw("Horizontal"); // A, D
        continuousActionsOut[1] = Input.GetAxisRaw("Vertical");   // W, S

        discreteActionsOut[0] = 0; // Branch 0 (메인 액션)
        discreteActionsOut[1] = 0; // Branch 1 (가드 액션)

        if (Input.GetKey(KeyCode.Space))
        {
            discreteActionsOut[0] = 3; // 스페이스바: 구르기
        }
        else if (Input.GetMouseButton(1))
        {
            discreteActionsOut[0] = 2; // 우클릭: 강공격
        }
        else if (Input.GetMouseButton(0))
        {
            discreteActionsOut[0] = 1; // 좌클릭: 약공격
        }

        float turn = 0f;
        if (Input.GetKey(KeyCode.Q)) turn = -1f;
        if (Input.GetKey(KeyCode.E)) turn = 1f;
        continuousActionsOut[2] = turn;

        if (Input.GetKey(KeyCode.LeftShift))
        {
            discreteActionsOut[1] = 1; // 쉬프트: 방패 올리기 (Block)
        }
    }
}