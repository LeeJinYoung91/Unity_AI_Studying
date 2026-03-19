using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class PlayerAgent : CharacterAgent
{
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        var discreteActionsOut = actionsOut.DiscreteActions;

        // 1. 이동 조작 (WASD 또는 방향키)
        continuousActionsOut[0] = Input.GetAxisRaw("Horizontal"); // A, D
        continuousActionsOut[1] = Input.GetAxisRaw("Vertical");   // W, S

        // 2. 액션 초기화 (기본상태: 가만히 있기, 방패 내림)
        discreteActionsOut[0] = 0; // Branch 0 (메인 액션)
        discreteActionsOut[1] = 0; // Branch 1 (가드 액션)

        // 3. 메인 액션 조작 (우선순위 적용)
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

        // 4. 가드(방패) 조작
        if (Input.GetKey(KeyCode.LeftShift))
        {
            discreteActionsOut[1] = 1; // 쉬프트: 방패 올리기 (Block)
        }
    }
}