using System;
using System.Collections.Generic;

[Serializable]
public class UIStateMachine
{
    public List<State> states = new List<State>();
}

[Serializable]
public class State
{
    public string name;
    public float time;
    public List<Property> properties = new List<Property>();
}

[Serializable]
public class Property
{
    public string path;
    public string componentType;
    public string propertyName;
    public float value;
    public string objectReference; // Should be a Resources-relative path without extension
}