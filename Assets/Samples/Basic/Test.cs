using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EasyStateful.Runtime;

public class Test : MonoBehaviour
{
    public StatefulRoot statefulRoot;
    public float nextStateTime;

    private float _time;
    private int _currentState;

    private void Start()
    {
        if (statefulRoot == null)
        {
            statefulRoot = GetComponent<StatefulRoot>();
        }
    }

    // Update is called once per frame
    void Update()
    {
        _time += Time.deltaTime;
        if (_time < nextStateTime)
        {
            return;
        }

        _time = 0;
        string[] stateNames = statefulRoot.stateNames;
        if (stateNames == null || stateNames.Length == 0) return; // Guard against empty/null state names

        _currentState = (_currentState + 1) % stateNames.Length;
        statefulRoot.TweenToState(stateNames[_currentState]);
    }
}