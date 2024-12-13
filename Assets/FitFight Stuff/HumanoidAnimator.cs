using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class HumanoidAnimator : MonoBehaviour
{
    NavMeshAgent agent;
    Animator animator;
    const float locomotionAnimationSmoothTime = 0.1f;


    // Start is called before the first frame update
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        //float speedPercent = agent.velocity.magnitude / agent.speed; //this would determine the animation based on the percent of the agent's max speed
        float speedPercent = (agent.velocity.magnitude * 33) / 100; //this makes sure that the animation has parameters based on absolute speed, not speed percentage
        animator.SetFloat("speedPercent", speedPercent, locomotionAnimationSmoothTime, Time.deltaTime); //the 0.1f is the damp time to transition between states

    }
}
