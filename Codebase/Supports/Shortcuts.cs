using UnityEngine;
public delegate bool KeyShortcut(KeyCode code);
public delegate void Method();
public delegate void MethodObject(object value);
public delegate void MethodInt(int value);
public delegate void MethodFloat(float value);
public delegate void MethodString(string value);
public delegate void MethodBool(bool value);
public delegate void MethodVector2(Vector2 value);
public delegate void MethodVector3(Vector3 value);
public delegate void MethodFull(object[] values);
public delegate object MethodReturn();
public delegate object MethodObjectReturn(object value);
public delegate object MethodIntReturn(int value);
public delegate object MethodFloatReturn(float value);
public delegate object MethodStringReturn(string value);
public delegate object MethodBoolReturn(bool value);
public delegate object MethodVector2Return(Vector2 value);
public delegate object MethodVector3Return(Vector3 value);